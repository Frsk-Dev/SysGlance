using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;

namespace SysGlance.Services;

/// <summary>
/// Local HTTP server the SysGlance iCUE widget on the Xeneon Edge connects to.
///
/// Open endpoints (anyone on this PC):
///   GET  /status               app name and version, so the widget can tell SysGlance is running
///   POST /pair                 start pairing; body {"name": "..."}; returns {id, code, expiresIn}
///   GET  /pair/status?id=...   "pending" | "approved" (+ token, once) | "declined" | "cancelled" | "expired"
///   POST /pair/cancel?id=...   the widget gave up waiting
/// Trusted widgets only (Authorization: Bearer token from pairing):
///   GET  /metrics              visible metrics in the user's order, plus widget appearance settings
///   GET  /metrics?all=1        every metric
///   POST /order                JSON array of metric keys, after a card was dragged on the Edge
///
/// Bodies are read as JSON whatever their content type, so the widget can send text/plain and avoid
/// CORS preflights; OPTIONS is answered anyway for requests that carry the Authorization header.
/// </summary>
internal sealed class MetricsWebServer : IDisposable
{
    private const int MaxBodyBytes = 16_384;

    private readonly HttpListener listener = new();
    private readonly Func<SystemMetrics> getMetrics;
    private readonly Func<AppSettings> getSettings;
    private readonly PairingService pairing;
    private readonly Action<IReadOnlyList<string>>? onOrderReceived;
    private readonly Action<string, bool>? onVisibleReceived;
    private readonly int port;
    private CancellationTokenSource? cts;
    private Task? listenTask;
    private bool disposed;

    /// <summary>
    /// Whether <see cref="Start"/> got the port. False when another program (often another SysGlance) holds it.
    /// </summary>
    public bool IsListening { get; private set; }

    /// <summary>
    /// Why the last <see cref="Start"/> failed, for Settings; null when listening.
    /// </summary>
    public string? StartError { get; private set; }

    public int Port => port;

    /// <summary>
    /// Initializes the web server. <paramref name="onOrderReceived"/> is called on the listener thread when a
    /// trusted widget posts a new card order; leave it null to ignore such requests.
    /// </summary>
    public MetricsWebServer(
        int port,
        Func<SystemMetrics> getMetrics,
        Func<AppSettings> getSettings,
        PairingService pairing,
        Action<IReadOnlyList<string>>? onOrderReceived = null,
        Action<string, bool>? onVisibleReceived = null)
    {
        this.onVisibleReceived = onVisibleReceived;
        this.port = port;
        this.getMetrics = getMetrics;
        this.getSettings = getSettings;
        this.pairing = pairing;
        this.onOrderReceived = onOrderReceived;
    }

    /// <summary>
    /// Starts listening for HTTP requests on the configured port.
    /// </summary>
    public void Start()
    {
        if (disposed)
        {
            return;
        }

        try
        {
            cts = new CancellationTokenSource();
            listener.Prefixes.Clear();
            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.Start();
            listenTask = Task.Run(() => ListenLoop(cts.Token));
            IsListening = true;
            StartError = null;
        }
        catch (HttpListenerException ex)
        {
            // Port in use (another program or an old SysGlance) or permission denied. MonitorWindow retries.
            IsListening = false;
            StartError = ex.ErrorCode == 183 || ex.ErrorCode == 32
                ? $"Port {port} is used by another program"
                : $"Can't open port {port}: {ex.Message}";
        }
        catch (ObjectDisposedException)
        {
        }
    }

    /// <summary>
    /// Stops the HTTP listener and cancels the listen loop.
    /// </summary>
    public void Stop()
    {
        cts?.Cancel();

        try
        {
            listener.Stop();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed.
        }

        try
        {
            listenTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Expected on cancellation.
        }
    }

    /// <summary>
    /// Disposes the listener and cancellation token.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Stop();
        listener.Close();
        cts?.Dispose();
    }

    /// <summary>
    /// Main loop that accepts and dispatches incoming HTTP requests.
    /// </summary>
    private async Task ListenLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                HttpListenerContext context = await listener.GetContextAsync().WaitAsync(token);
                HandleRequest(context);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpListenerException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Routes the request to the appropriate handler.
    /// </summary>
    private void HandleRequest(HttpListenerContext context)
    {
        HttpListenerRequest request = context.Request;
        HttpListenerResponse response = context.Response;

        try
        {
            // The widget runs from its own origin inside iCUE, so responses must be readable cross-origin.
            // Access is controlled by the pairing token, not by origin.
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Authorization, Content-Type");
            response.Headers.Add("Access-Control-Max-Age", "600");
            response.Headers.Add("Cache-Control", "no-store");

            if (request.HttpMethod == "OPTIONS")
            {
                Respond(response, 204);
                return;
            }

            string path = (request.Url?.AbsolutePath ?? "/").TrimEnd('/').ToLowerInvariant();

            switch (path, request.HttpMethod)
            {
                case ("/status", "GET"):
                    RespondJson(response, 200, new { app = "SysGlance", version = AppVersion, pairing = true });
                    break;

                case ("/pair", "POST"):
                    HandlePairRequest(request, response);
                    break;

                case ("/pair/status", "GET"):
                    string state = pairing.GetStatus(request.QueryString["id"], out string? token);
                    RespondJson(response, state == "unknown" ? 404 : 200, new { state, token });
                    break;

                case ("/pair/cancel", "POST"):
                    pairing.Cancel(request.QueryString["id"]);
                    Respond(response, 204);
                    break;

                case ("/metrics", "GET"):
                    if (RequireTrusted(request, response))
                    {
                        ServeMetricsJson(response, request.QueryString["all"] == "1");
                    }
                    break;

                case ("/visible", "POST"):
                    if (RequireTrusted(request, response))
                    {
                        HandleVisibleRequest(request, response);
                    }
                    break;

                case ("/order", "POST"):
                    if (RequireTrusted(request, response))
                    {
                        HandleOrderRequest(request, response);
                    }
                    break;

                default:
                    RespondJson(response, 404, new { error = "not_found" });
                    break;
            }
        }
        catch
        {
            try
            {
                response.StatusCode = 500;
                response.Close();
            }
            catch
            {
                // Response may already be closed.
            }
        }
    }

    /// <summary>
    /// Starts a pairing request; SysGlance shows the Trust/Decline prompt with the same code.
    /// </summary>
    private void HandlePairRequest(HttpListenerRequest request, HttpListenerResponse response)
    {
        string? name = null;
        string? body = ReadBody(request);

        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("name", out JsonElement nameElement)
                    && nameElement.ValueKind == JsonValueKind.String)
                {
                    name = nameElement.GetString();
                }
            }
            catch (JsonException)
            {
                RespondJson(response, 400, new { error = "bad_request" });
                return;
            }
        }

        switch (pairing.StartRequest(name, out PairingRequest? started))
        {
            case PairingStartResult.Started:
                RespondJson(response, 200, new
                {
                    id = started!.Id,
                    code = started.Code,
                    expiresIn = (int)Math.Max(0, (started.ExpiresAtUtc - DateTime.UtcNow).TotalSeconds)
                });
                break;

            case PairingStartResult.Busy:
                RespondJson(response, 409, new { error = "busy" });
                break;

            case PairingStartResult.TooSoon:
                RespondJson(response, 429, new { error = "too_soon" });
                break;

            default:
                RespondJson(response, 503, new { error = "unavailable" });
                break;
        }
    }

    /// <summary>
    /// Answers 401 unless the request carries a trusted widget's Bearer token.
    /// </summary>
    private bool RequireTrusted(HttpListenerRequest request, HttpListenerResponse response)
    {
        string? header = request.Headers["Authorization"];
        string? token = (header is not null) && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header[7..].Trim()
            : null;

        if (pairing.IsTrusted(token))
        {
            return true;
        }

        RespondJson(response, 401, new { error = "unauthorized" });
        return false;
    }

    /// <summary>
    /// Serves the metrics JSON. When <paramref name="includeAll"/> is true, every known metric is returned in
    /// default order, ignoring the user's visibility settings.
    /// </summary>
    private void ServeMetricsJson(HttpListenerResponse response, bool includeAll)
    {
        AppSettings currentSettings = getSettings();
        SystemMetrics currentMetrics = getMetrics();

        var metricsList = new List<object>();
        int graphMinutes = currentSettings.DashboardGraphMinutes;
        IEnumerable<string> keys = includeAll ? AppSettings.AllMetricKeys : currentSettings.MetricOrder;

        foreach (string key in keys)
        {
            if (!includeAll && !currentSettings.IsVisible(key))
            {
                continue;
            }

            float? rawValue = MetricFormatter.GetMetricValue(key, currentMetrics);

            metricsList.Add(new
            {
                key,
                label = AppSettings.GetOverlayLabel(key),
                name = AppSettings.GetLabel(key),
                value = MetricFormatter.FormatMetricValue(key, currentMetrics),
                rawValue = rawValue ?? 0f,
                available = rawValue.HasValue,
                color = currentSettings.GetColor(key),
                visible = currentSettings.IsVisible(key),
                // The full list (?all=1, for the Edge's edit screen) leaves graphs out to stay small.
                history = includeAll || !currentSettings.HasGraph(key)
                    ? null
                    : MetricHistory.Instance.Get(key, graphMinutes)?.Values.Select(v => v.HasValue ? (double?)Math.Round(v.Value, 2) : null)
            });
        }

        RespondJson(response, 200, new
        {
            metrics = metricsList,
            bgColor = currentSettings.DashboardBgColor,
            cardColor = currentSettings.DashboardCardColor,
            bgOpacity = currentSettings.DashboardBgOpacity / 100.0,
            cardOpacity = currentSettings.DashboardCardOpacity / 100.0,
            widgetTextScale = currentSettings.WidgetTextScale / 100.0,
            graphMinutes
        });
    }

    /// <summary>
    /// Shows or hides one card, from the Edge's edit screen: {"key": "CpuTemp", "visible": true}.
    /// </summary>
    private void HandleVisibleRequest(HttpListenerRequest request, HttpListenerResponse response)
    {
        if (onVisibleReceived is null)
        {
            Respond(response, 503);
            return;
        }

        try
        {
            using JsonDocument body = JsonDocument.Parse(ReadBody(request) ?? "");
            string? key = body.RootElement.GetProperty("key").GetString();
            bool visible = body.RootElement.GetProperty("visible").GetBoolean();
            if (key is null || !AppSettings.AllMetricKeys.Contains(key))
            {
                Respond(response, 400);
                return;
            }

            onVisibleReceived(key, visible);
            Respond(response, 204);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            Respond(response, 400);
        }
    }

    /// <summary>
    /// Accepts a new card order from the widget: a JSON array of metric keys.
    /// </summary>
    private void HandleOrderRequest(HttpListenerRequest request, HttpListenerResponse response)
    {
        if (onOrderReceived is null)
        {
            Respond(response, 503);
            return;
        }

        try
        {
            string[]? keys = JsonSerializer.Deserialize<string[]>(ReadBody(request) ?? "");

            if ((keys is null) || (keys.Length > AppSettings.AllMetricKeys.Length))
            {
                Respond(response, 400);
                return;
            }

            onOrderReceived(keys);
            Respond(response, 204);
        }
        catch (JsonException)
        {
            Respond(response, 400);
        }
    }

    /// <summary>
    /// Reads a small request body, or returns null if it is missing or too large.
    /// </summary>
    private static string? ReadBody(HttpListenerRequest request)
    {
        if (!request.HasEntityBody || (request.ContentLength64 > MaxBodyBytes))
        {
            return null;
        }

        using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
        var buffer = new char[MaxBodyBytes + 1];
        int read = reader.ReadBlock(buffer, 0, buffer.Length);
        return read > MaxBodyBytes ? null : new string(buffer, 0, read);
    }

    private static string AppVersion =>
        typeof(MetricsWebServer).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    private static void Respond(HttpListenerResponse response, int status)
    {
        response.StatusCode = status;
        response.Close();
    }

    private static void RespondJson(HttpListenerResponse response, int status, object body)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body));
        response.StatusCode = status;
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = buffer.Length;
        response.OutputStream.Write(buffer, 0, buffer.Length);
        response.Close();
    }
}
