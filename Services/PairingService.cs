using System.Security.Cryptography;
using System.Text;

namespace SysGlance.Services;

/// <summary>
/// An iCUE widget the user trusted. Only a SHA-256 hash of its access token is stored.
/// </summary>
public sealed class TrustedWidget
{
    /// <summary>
    /// Random identifier for this pairing.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// Name the widget reported when pairing (for example "Xeneon Edge").
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Lowercase hex SHA-256 of the access token.
    /// </summary>
    public string TokenHash { get; set; } = "";

    /// <summary>
    /// When the user pressed Trust (UTC).
    /// </summary>
    public DateTime PairedAtUtc { get; set; }
}

/// <summary>
/// A pairing request waiting for the user to press Trust or Decline in SysGlance.
/// </summary>
internal sealed record PairingRequest(string Id, string Code, string ClientName, DateTime ExpiresAtUtc);

/// <summary>
/// Outcome of asking to start pairing.
/// </summary>
internal enum PairingStartResult
{
    Started,
    Busy,
    TooSoon,
    Unavailable
}

/// <summary>
/// Pairs iCUE widgets with SysGlance. The widget asks for a request, SysGlance shows the same 6-digit code
/// the widget displays, and the user presses Trust or Decline. Trusting issues a random token that the
/// widget sends as a Bearer token on every request; only its hash is kept in settings.
/// Thread-safe: the web server calls in from its listener thread, the UI resolves requests on the UI thread.
/// </summary>
internal sealed class PairingService
{
    private static readonly TimeSpan RequestLifetime = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ResultRetention = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MinTimeBetweenRequests = TimeSpan.FromSeconds(3);
    private const int MaxClientNameLength = 40;

    private readonly AppSettings settings;
    private readonly Action<PairingRequest>? onRequest;
    private readonly object sync = new();
    private readonly Dictionary<string, Entry> entries = [];

    // iCUE runs each widget twice: on the Edge, and in its own preview with separate storage, so the preview copy
    // has no token. The paired Edge copy tells us its iCUE widget id; the preview copy's id is the same plus
    // "-preview". Normalized widget id -> hash of the token that widget uses. Kept in memory only: the Edge copy
    // reports its id again on every poll.
    private readonly Dictionary<string, string> widgetInstances = [];
    private const int MaxWidgetInstances = 32;
    private DateTime lastRequestUtc = DateTime.MinValue;

    private sealed class Entry
    {
        public required PairingRequest Request { get; init; }
        public string State { get; set; } = "pending";
        public string? Token { get; set; }
        public DateTime ResolvedAtUtc { get; set; }
    }

    /// <summary>
    /// Creates the service. <paramref name="onRequest"/> shows the Trust/Decline prompt; it is called on the
    /// listener thread and must marshal to the UI itself. Without it, pairing requests are refused.
    /// </summary>
    public PairingService(AppSettings settings, Action<PairingRequest>? onRequest)
    {
        this.settings = settings;
        this.onRequest = onRequest;
    }

    /// <summary>
    /// Raised (on the calling thread) when a request is approved, declined, cancelled, or expires.
    /// </summary>
    public event Action<string>? RequestClosed;

    /// <summary>
    /// Starts a pairing request and returns its id and code. Only one request can be pending at a time.
    /// </summary>
    public PairingStartResult StartRequest(string? clientName, out PairingRequest? request)
    {
        request = null;

        if (onRequest is null)
        {
            return PairingStartResult.Unavailable;
        }

        lock (sync)
        {
            PruneLocked();
            DateTime now = DateTime.UtcNow;

            if (entries.Values.Any(e => e.State == "pending"))
            {
                return PairingStartResult.Busy;
            }

            if (now - lastRequestUtc < MinTimeBetweenRequests)
            {
                return PairingStartResult.TooSoon;
            }

            string name = string.IsNullOrWhiteSpace(clientName) ? "iCUE widget" : clientName.Trim();
            if (name.Length > MaxClientNameLength)
            {
                name = name[..MaxClientNameLength];
            }

            request = new PairingRequest(
                Id: Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(),
                Code: RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6"),
                ClientName: name,
                ExpiresAtUtc: now + RequestLifetime);

            entries[request.Id] = new Entry { Request = request };
            lastRequestUtc = now;
        }

        onRequest(request);
        return PairingStartResult.Started;
    }

    /// <summary>
    /// Returns the request state ("pending", "approved", "declined", "cancelled", "expired", or "unknown").
    /// The token of an approved request is handed out once, then forgotten.
    /// </summary>
    public string GetStatus(string? id, out string? token)
    {
        token = null;

        lock (sync)
        {
            PruneLocked();

            if ((id is null) || !entries.TryGetValue(id, out Entry? entry))
            {
                return "unknown";
            }

            if (entry.State == "approved")
            {
                token = entry.Token;
                entry.Token = null;
            }

            return entry.State;
        }
    }

    /// <summary>
    /// The user pressed Trust. Adds the widget to <see cref="AppSettings.TrustedWidgets"/>; the caller saves.
    /// Returns false if the request is no longer pending.
    /// </summary>
    public bool Approve(string id)
    {
        lock (sync)
        {
            if (!entries.TryGetValue(id, out Entry? entry) || (entry.State != "pending"))
            {
                return false;
            }

            string token = Base64Url(RandomNumberGenerator.GetBytes(32));
            settings.TrustedWidgets.Add(new TrustedWidget
            {
                Id = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant(),
                Name = entry.Request.ClientName,
                TokenHash = Hash(token),
                PairedAtUtc = DateTime.UtcNow
            });

            entry.State = "approved";
            entry.Token = token;
            entry.ResolvedAtUtc = DateTime.UtcNow;
        }

        RequestClosed?.Invoke(id);
        return true;
    }

    /// <summary>
    /// The user pressed Decline, or the prompt was dismissed.
    /// </summary>
    public void Decline(string id) => Close(id, "declined");

    /// <summary>
    /// The prompt's countdown ran out.
    /// </summary>
    public void Expire(string id) => Close(id, "expired");

    /// <summary>
    /// The widget gave up waiting (Cancel on the Edge).
    /// </summary>
    public void Cancel(string? id)
    {
        if (id is not null)
        {
            Close(id, "cancelled");
        }
    }

    /// <summary>
    /// Whether <paramref name="token"/> belongs to a trusted widget. Compared in constant time.
    /// </summary>
    public bool IsTrusted(string? token)
    {
        if (string.IsNullOrEmpty(token) || (token.Length > 200))
        {
            return false;
        }

        byte[] presented = Encoding.ASCII.GetBytes(Hash(token));

        lock (sync)
        {
            foreach (TrustedWidget widget in settings.TrustedWidgets)
            {
                if (CryptographicOperations.FixedTimeEquals(presented, Encoding.ASCII.GetBytes(widget.TokenHash)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// An iCUE widget id as "1057e8c5-a19f-..." whatever form it arrives in: iCUE uses it with and without braces,
    /// and gives the preview copy the same id with "-preview" on the end. Null when it doesn't look like one.
    /// </summary>
    public static string? NormalizeWidgetId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || (id.Length > 100))
        {
            return null;
        }

        string value = id.Trim();
        if (value.EndsWith("-preview", StringComparison.OrdinalIgnoreCase))
        {
            value = value[..^8];
        }

        value = value.Replace("{", "").Replace("}", "").ToLowerInvariant();
        return (value.Length is >= 8 and <= 64) && value.All(c => char.IsAsciiLetterOrDigit(c) || (c == '-')) ? value : null;
    }

    /// <summary>
    /// Records which iCUE widget a trusted token belongs to, so that widget's preview copy can be served too.
    /// Call only after <see cref="IsTrusted"/> accepted <paramref name="token"/>.
    /// </summary>
    public void RememberInstance(string? widgetId, string token)
    {
        string? key = NormalizeWidgetId(widgetId);
        if (key is null)
        {
            return;
        }

        lock (sync)
        {
            if (!widgetInstances.ContainsKey(key) && (widgetInstances.Count >= MaxWidgetInstances))
            {
                widgetInstances.Clear();
            }

            widgetInstances[key] = Hash(token);
        }
    }

    /// <summary>
    /// Whether <paramref name="previewId"/> is the iCUE preview copy of a widget that is paired and still trusted.
    /// Read-only access: callers only use this for GET /metrics.
    /// </summary>
    public bool IsPreviewOfTrusted(string? previewId)
    {
        string? key = NormalizeWidgetId(previewId);
        if (key is null)
        {
            return false;
        }

        lock (sync)
        {
            if (!widgetInstances.TryGetValue(key, out string? hash))
            {
                return false;
            }

            byte[] wanted = Encoding.ASCII.GetBytes(hash);
            foreach (TrustedWidget widget in settings.TrustedWidgets)
            {
                if (CryptographicOperations.FixedTimeEquals(wanted, Encoding.ASCII.GetBytes(widget.TokenHash)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Number of trusted widgets.
    /// </summary>
    public int TrustedCount
    {
        get
        {
            lock (sync)
            {
                return settings.TrustedWidgets.Count;
            }
        }
    }

    /// <summary>
    /// Forgets every trusted widget; they will have to pair again. The caller saves.
    /// </summary>
    public void RevokeAll()
    {
        lock (sync)
        {
            settings.TrustedWidgets.Clear();
            widgetInstances.Clear();
        }
    }

    private void Close(string id, string state)
    {
        lock (sync)
        {
            if (!entries.TryGetValue(id, out Entry? entry) || (entry.State != "pending"))
            {
                return;
            }

            entry.State = state;
            entry.ResolvedAtUtc = DateTime.UtcNow;
        }

        RequestClosed?.Invoke(id);
    }

    private void PruneLocked()
    {
        DateTime now = DateTime.UtcNow;
        var expired = new List<string>();

        foreach ((string id, Entry entry) in entries)
        {
            if ((entry.State == "pending") && (now >= entry.Request.ExpiresAtUtc))
            {
                entry.State = "expired";
                entry.ResolvedAtUtc = now;
                expired.Add(id);
            }
        }

        foreach (string id in entries.Where(e => (e.Value.State != "pending") && (now - e.Value.ResolvedAtUtc > ResultRetention))
                     .Select(e => e.Key).ToList())
        {
            entries.Remove(id);
        }

        // Lets the prompt close itself. Raised under the lock, which is fine because handlers only post to the
        // UI thread and never call back into this service synchronously.
        foreach (string id in expired)
        {
            RequestClosed?.Invoke(id);
        }
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
