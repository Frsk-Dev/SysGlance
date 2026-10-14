using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace SysGlance.Services;

/// <summary>
/// One-click updates from GitHub releases (tags like "v1.2.0" with a SysGlance.exe asset).
/// <list type="number">
/// <item>Check the latest release and compare its tag with this build's version.</item>
/// <item>Download the exe to a temp file and verify it (complete size, Windows executable header, and the
/// SHA-256 digest when GitHub publishes one).</item>
/// <item>Hand over to a small script that waits for SysGlance to exit, keeps the old exe as "SysGlance.exe.old",
/// moves the new one in and starts it. If any step fails the old exe is put back and started again.</item>
/// </list>
/// </summary>
public sealed class UpdateService
{
    private const string GitHubOwner = "Frsk-Dev";
    private const string GitHubRepo = "SysGlance";
    private const string LatestReleaseUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
    private const long MinimumExeBytes = 1_000_000;

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(10),
        DefaultRequestHeaders =
        {
            { "User-Agent", "SysGlance-Updater" },
            { "Accept", "application/vnd.github+json" }
        }
    };

    /// <summary>
    /// A newer release.
    /// </summary>
    public sealed record UpdateInfo(
        Version Version,
        string Tag,
        string Title,
        string ReleaseNotes,
        string DownloadUrl,
        long DownloadBytes,
        string? Sha256,
        string ReleasePageUrl);

    /// <summary>
    /// This build's version (from the project's &lt;Version&gt;).
    /// </summary>
    public static Version CurrentVersion
    {
        get
        {
            Version v = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
        }
    }

    /// <summary>
    /// Returns the latest release if it is newer than this build, otherwise null. Never throws.
    /// </summary>
    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellation = default)
    {
        try
        {
            GitHubRelease? release = await HttpClient.GetFromJsonAsync<GitHubRelease>(LatestReleaseUrl, cancellation);
            if (release is null || release.Draft || release.Prerelease)
            {
                return null;
            }

            if (!Version.TryParse(release.TagName.TrimStart('v', 'V'), out Version? remote))
            {
                return null;
            }

            remote = new Version(remote.Major, remote.Minor, Math.Max(0, remote.Build));
            if (remote <= CurrentVersion)
            {
                return null;
            }

            GitHubAsset? asset = release.Assets.FirstOrDefault(a => a.Name.Equals("SysGlance.exe", StringComparison.OrdinalIgnoreCase))
                ?? release.Assets.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
            if (asset is null || string.IsNullOrEmpty(asset.BrowserDownloadUrl))
            {
                return null;
            }

            string? sha256 = asset.Digest?.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) == true
                ? asset.Digest[7..].ToLowerInvariant()
                : null;

            return new UpdateInfo(
                remote,
                release.TagName,
                string.IsNullOrWhiteSpace(release.Name) ? $"SysGlance {release.TagName}" : release.Name.Trim(),
                release.Body ?? "",
                asset.BrowserDownloadUrl,
                asset.Size,
                sha256,
                release.HtmlUrl ?? $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases/latest");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Downloads and verifies the update. Returns the path of the verified exe.
    /// Throws <see cref="InvalidDataException"/> if the download is incomplete or not the expected file.
    /// </summary>
    public async Task<string> DownloadUpdateAsync(UpdateInfo update, IProgress<int>? progress = null, CancellationToken cancellation = default)
    {
        string folder = Path.Combine(Path.GetTempPath(), "SysGlance-update");
        Directory.CreateDirectory(folder);
        string partial = Path.Combine(folder, $"SysGlance-{update.Version}.exe.partial");
        string final = Path.Combine(folder, $"SysGlance-{update.Version}.exe");

        using (HttpResponseMessage response = await HttpClient.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellation))
        {
            response.EnsureSuccessStatusCode();
            long? total = response.Content.Headers.ContentLength ?? (update.DownloadBytes > 0 ? update.DownloadBytes : null);

            await using Stream source = await response.Content.ReadAsStreamAsync(cancellation);
            await using FileStream target = new(partial, FileMode.Create, FileAccess.Write, FileShare.None);
            var buffer = new byte[81920];
            long read = 0;
            int count;
            int lastPercent = -1;

            while ((count = await source.ReadAsync(buffer, cancellation)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, count), cancellation);
                read += count;
                if (total is > 0)
                {
                    int percent = (int)(read * 100 / total.Value);
                    if (percent != lastPercent)
                    {
                        lastPercent = percent;
                        progress?.Report(percent);
                    }
                }
            }
        }

        Verify(partial, update);
        File.Move(partial, final, overwrite: true);
        progress?.Report(100);
        return final;
    }

    private static void Verify(string path, UpdateInfo update)
    {
        var file = new FileInfo(path);
        if (file.Length < MinimumExeBytes || (update.DownloadBytes > 0 && file.Length != update.DownloadBytes))
        {
            throw new InvalidDataException("The download is incomplete. Please try again.");
        }

        using (FileStream stream = File.OpenRead(path))
        {
            if (stream.ReadByte() != 'M' || stream.ReadByte() != 'Z')
            {
                throw new InvalidDataException("The downloaded file isn't a Windows program.");
            }
        }

        if (update.Sha256 is not null)
        {
            using FileStream stream = File.OpenRead(path);
            string actual = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            if (actual != update.Sha256)
            {
                throw new InvalidDataException("The download doesn't match the release (checksum mismatch).");
            }
        }
    }

    /// <summary>
    /// Starts the swap script and exits SysGlance. The script waits for this process to end, then replaces the
    /// exe (keeping the old one as ".old" until the new version starts) and launches the new version.
    /// </summary>
    public static void ApplyUpdateAndRestart(string downloadedExe)
    {
        string current = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "SysGlance.exe");
        string backup = current + ".old";
        int pid = Environment.ProcessId;
        string script = Path.Combine(Path.GetTempPath(), "SysGlance-update", "apply-update.cmd");

        // Up to ~30 s for SysGlance to exit; if it won't, or a move fails, the old exe is restored and started.
        File.WriteAllText(script, $"""
            @echo off
            setlocal
            set /a tries=0
            :wait
            "%SystemRoot%\System32\tasklist.exe" /FI "PID eq {pid}" /NH 2>nul | "%SystemRoot%\System32\find.exe" " {pid} " >nul
            if errorlevel 1 goto gone
            set /a tries+=1
            if %tries% geq 30 goto restore
            rem ping as a 1 s sleep: "timeout" fails without a console in a hidden window
            "%SystemRoot%\System32\PING.EXE" -n 2 127.0.0.1 >nul
            goto wait
            :gone
            if exist "{backup}" del /f /q "{backup}" >nul 2>&1
            move /y "{current}" "{backup}" >nul 2>&1 || goto restore
            move /y "{downloadedExe}" "{current}" >nul 2>&1 || goto rollback
            start "" "{current}" --updated
            goto done
            :rollback
            move /y "{backup}" "{current}" >nul 2>&1
            :restore
            start "" "{current}"
            :done
            del "%~f0" >nul 2>&1
            """);

        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{script}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetTempPath()
        });

        System.Windows.Application.Current?.Dispatcher.Invoke(() => System.Windows.Application.Current.Shutdown());
        Environment.Exit(0);
    }

    /// <summary>
    /// Removes the previous version kept by the swap script (call once the new version is running).
    /// </summary>
    public static void CleanUpAfterUpdate()
    {
        try
        {
            string? current = Environment.ProcessPath;
            if (current is not null && File.Exists(current + ".old"))
            {
                File.Delete(current + ".old");
            }

            string folder = Path.Combine(Path.GetTempPath(), "SysGlance-update");
            if (Directory.Exists(folder))
            {
                foreach (string file in Directory.GetFiles(folder, "SysGlance-*.exe*"))
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception)
        {
            // Locked or already gone; try again next start.
        }
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")] public string TagName { get; set; } = "";
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("body")] public string? Body { get; set; }
        [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
        [JsonPropertyName("draft")] public bool Draft { get; set; }
        [JsonPropertyName("prerelease")] public bool Prerelease { get; set; }
        [JsonPropertyName("assets")] public List<GitHubAsset> Assets { get; set; } = [];
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("size")] public long Size { get; set; }
        [JsonPropertyName("digest")] public string? Digest { get; set; }
        [JsonPropertyName("browser_download_url")] public string BrowserDownloadUrl { get; set; } = "";
    }
}
