using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using SysGlance.Services;

namespace SysGlance;

/// <summary>
/// Offers a newer SysGlance from GitHub: release notes, then one click to download, verify and restart into it.
/// </summary>
public partial class UpdateWindow : Window
{
    private readonly UpdateService.UpdateInfo update;
    private readonly AppSettings settings;
    private readonly UpdateService service = new();
    private CancellationTokenSource? download;

    internal UpdateWindow(UpdateService.UpdateInfo updateInfo, AppSettings appSettings)
    {
        InitializeComponent();
        WindowEffects.Apply(this);
        update = updateInfo;
        settings = appSettings;

        HeadingText.Text = $"SysGlance {update.Version.ToString(3)} is available";
        CurrentVersionText.Text = $"You have {UpdateService.CurrentVersion.ToString(3)}";
        NewVersionText.Text = $"New {update.Version.ToString(3)}";
        ReleaseNotesText.Text = PlainText(update.ReleaseNotes);
        ReleasePageButton.Click += (_, _) => System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(update.ReleasePageUrl) { UseShellExecute = true });

        Closing += (_, e) =>
        {
            // Closing mid-download cancels it; nothing has been replaced yet at that point.
            download?.Cancel();
        };
    }

    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        download = new CancellationTokenSource();
        SetBusy(true);
        StatusText.Text = update.DownloadBytes > 0
            ? $"Downloading {update.DownloadBytes / 1_048_576.0:0} MB…"
            : "Downloading…";

        try
        {
            var progress = new Progress<int>(percent =>
            {
                ProgressFill.Width = ProgressTrack.ActualWidth * percent / 100.0;
                StatusText.Text = $"Downloading… {percent}%";
            });

            string exe = await service.DownloadUpdateAsync(update, progress, download.Token);
            StatusText.Text = "Installing and restarting SysGlance…";
            await Task.Delay(400);
            UpdateService.ApplyUpdateAndRestart(exe);
        }
        catch (OperationCanceledException)
        {
            // Window closed during the download.
        }
        catch (Exception ex)
        {
            string reason = ex is InvalidDataException ? ex.Message : $"Couldn't download the update: {ex.Message}";
            StatusText.Text = reason + " Your current version is unchanged.";
            UpdateButton.Content = "Try again";
            SetBusy(false);
        }
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        settings.SkippedUpdateVersion = update.Tag;
        settings.Save();
        Close();
    }

    private void Later_Click(object sender, RoutedEventArgs e) => Close();

    private void SetBusy(bool busy)
    {
        ProgressPanel.Visibility = Visibility.Visible;
        UpdateButton.IsEnabled = !busy;
        SkipButton.IsEnabled = !busy;
        LaterButton.Content = busy ? "Cancel" : "Later";
        if (!busy)
        {
            ProgressFill.Width = 0;
        }
    }

    /// <summary>
    /// GitHub release notes are Markdown; show them as readable plain text.
    /// </summary>
    private static string PlainText(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return "No release notes for this version.";
        }

        var lines = markdown.Replace("\r\n", "\n").Split('\n').Select(line =>
        {
            string l = Regex.Replace(line, @"^\s*#{1,6}\s*", "");            // headings
            l = Regex.Replace(l, @"^\s*[-*+]\s+", "• ");                 // bullets
            l = Regex.Replace(l, @"\*\*(.+?)\*\*|__(.+?)__", "$1$2");          // bold
            l = Regex.Replace(l, @"(?<!\w)[*_](.+?)[*_](?!\w)", "$1");         // italics
            l = Regex.Replace(l, @"`([^`]+)`", "$1");                          // code
            l = Regex.Replace(l, @"!?\[([^\]]*)\]\([^)]*\)", "$1");            // links / images
            return l.TrimEnd();
        });

        string text = Regex.Replace(string.Join("\n", lines), @"\n{3,}", "\n\n").Trim();

        // The card already has a "What's new" heading; drop a duplicate at the top of the notes.
        return Regex.Replace(text, @"^(what'?s new|changelog|release notes)\s*\n", "", RegexOptions.IgnoreCase).TrimStart();
    }
}
