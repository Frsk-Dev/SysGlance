using System.Media;
using System.Windows;
using System.Windows.Threading;
using SysGlance.Services;

namespace SysGlance;

/// <summary>
/// Notification-style prompt shown when a Xeneon Edge widget asks to connect. Shows the same 6-digit code
/// as the widget so the user can check it before pressing Trust.
/// </summary>
public partial class PairingWindow : Window
{
    private readonly DispatcherTimer countdown;
    private readonly DateTime expiresAtUtc;
    private bool resolved;

    /// <summary>
    /// Raised once: (approved, timedOut). Closing the window any other way counts as Decline.
    /// </summary>
    public event Action<bool, bool>? Resolved;

    /// <summary>
    /// The pairing request this prompt is for.
    /// </summary>
    public string RequestId { get; }

    internal PairingWindow(PairingRequest request)
    {
        InitializeComponent();
        WindowEffects.Apply(this, transient: true);

        RequestId = request.Id;
        expiresAtUtc = request.ExpiresAtUtc;
        CodeText.Text = $"{request.Code[..3]} {request.Code[3..]}";
        TitleText.Text = $"Connect {request.ClientName}?";

        countdown = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        countdown.Tick += (_, _) => UpdateCountdown();
        UpdateCountdown();
        countdown.Start();

        Loaded += (_, _) =>
        {
            // Bottom-right of the work area, like a Windows notification.
            Rect area = SystemParameters.WorkArea;
            Left = area.Right - ActualWidth - 16;
            Top = area.Bottom - ActualHeight - 16;
            Activate();
            SystemSounds.Asterisk.Play();
        };

        Closed += (_, _) =>
        {
            countdown.Stop();
            Finish(approved: false, timedOut: false);
        };
    }

    /// <summary>
    /// Closes without reporting a result (the request was already closed elsewhere).
    /// </summary>
    public void CloseQuietly()
    {
        resolved = true;
        Close();
    }

    private void UpdateCountdown()
    {
        TimeSpan left = expiresAtUtc - DateTime.UtcNow;

        if (left <= TimeSpan.Zero)
        {
            Finish(approved: false, timedOut: true);
            Close();
            return;
        }

        ExpiryText.Text = $"Expires in {(int)left.TotalMinutes}:{left.Seconds:D2}";
    }

    private void Trust_Click(object sender, RoutedEventArgs e)
    {
        Finish(approved: true, timedOut: false);
        Close();
    }

    private void Decline_Click(object sender, RoutedEventArgs e)
    {
        Finish(approved: false, timedOut: false);
        Close();
    }

    private void Finish(bool approved, bool timedOut)
    {
        if (resolved)
        {
            return;
        }

        resolved = true;
        Resolved?.Invoke(approved, timedOut);
    }
}
