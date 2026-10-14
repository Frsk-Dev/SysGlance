using SysGlance.Services;

namespace SysGlance;

/// <summary>
/// Entry point for the SysGlance application.
/// Shows a splash screen while initializing, then launches the monitor overlay.
/// </summary>
public static class Program
{
    /// <summary>
    /// Shared metrics snapshot used by the early-start web server before the
    /// MonitorWindow polling thread has produced its first reading.
    /// </summary>
    internal static volatile SystemMetrics EarlyMetrics = new();

    /// <summary>
    /// Main entry point. Starts the web server before any WPF or hardware
    /// initialization so the Xeneon Edge widget can connect immediately, then
    /// shows a splash screen and launches the monitor overlay.
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {
        // "--after <pid>": started by a restart (e.g. restoring a backup); wait for the old copy to exit first,
        // otherwise the single-instance lock would hand control back to it.
        int afterIndex = Array.IndexOf(args, "--after");
        if ((afterIndex >= 0) && (afterIndex + 1 < args.Length) && int.TryParse(args[afterIndex + 1], out int previousPid))
        {
            try
            {
                using var previous = System.Diagnostics.Process.GetProcessById(previousPid);
                previous.WaitForExit(15000);
            }
            catch (ArgumentException)
            {
                // Already gone.
            }
        }

        // Raise priority so JIT compilation and server startup get more CPU time.
        // This shortens the window where the server is not yet ready.
        // Lowered back to Normal once the server is up and MonitorWindow is created.
        System.Diagnostics.Process.GetCurrentProcess().PriorityClass =
            System.Diagnostics.ProcessPriorityClass.High;

        // One SysGlance only. If this version is already running, it opens its Settings instead.
        // Older versions don't know the lock, so close them: they would hold the Edge connection
        // port and rewrite settings.json without the newer settings.
        if (!SingleInstance.TryClaim())
        {
            return;
        }

        SingleInstance.CloseOtherCopies();

        // Dated copy of the settings before anything can change them (last 10 kept).
        SettingsBackup.CreateAutomaticBackup();

        // This version started fine: remove the previous exe the updater kept as a fallback.
        UpdateService.CleanUpAfterUpdate();

        // Start the web server as the very first thing — before WPF, before the
        // splash, before hardware init.  This gives iCUE the best chance of
        // finding the server ready when the Xeneon Edge widget first connects.
        AppSettings earlySettings = AppSettings.Load();

        // After an update to a new folder, point "Start with Windows" at this exe instead of the old one.
        StartupRegistration.RepairIfEnabled(earlySettings);
        MetricsWebServer? earlyServer = null;

        if (earlySettings.WebServerEnabled)
        {
            // No prompt can be shown yet, so this early server refuses pairing; trusted widgets still work.
            earlyServer = new MetricsWebServer(
                earlySettings.WebServerPort,
                () => EarlyMetrics,
                () => earlySettings,
                new PairingService(earlySettings, onRequest: null));
            earlyServer.Start();
        }

        try
        {
            var app = new System.Windows.Application();

            // A bug in one button or window must not take the whole app (overlay, Edge connection) down:
            // report it and keep running. Start-up failures still reach the catch below.
            app.DispatcherUnhandledException += (_, e) =>
            {
                e.Handled = true;
                System.Windows.MessageBox.Show(
                    $"Something went wrong, but SysGlance is still running.\n\n{e.Exception.Message}\n\n{e.Exception.StackTrace}",
                    "SysGlance",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            };

            // Show splash screen immediately.
            var splash = new SplashWindow();
            splash.Show();

            // Force the splash to render before heavy initialization.
            app.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

            // Initialize the main window (triggers LHM hardware scan).
            // Pass the early server so MonitorWindow can cleanly take it over.
            var window = new MonitorWindow(earlyServer);

            // Launching SysGlance again while it runs opens this copy's Settings.
            SingleInstance.ListenForShowRequests(() => window.Dispatcher.BeginInvoke(window.OpenSettings));

            // Server is up and running — return to normal priority.
            System.Diagnostics.Process.GetCurrentProcess().PriorityClass =
                System.Diagnostics.ProcessPriorityClass.Normal;

            // Close splash after a minimum display time so the branding is visible.
            var splashShownAt = DateTime.UtcNow;
            window.Loaded += async (_, _) =>
            {
                TimeSpan elapsed = DateTime.UtcNow - splashShownAt;
                var minimumDisplay = TimeSpan.FromSeconds(3.5);

                if (elapsed < minimumDisplay)
                {
                    await System.Threading.Tasks.Task.Delay(minimumDisplay - elapsed);
                }

                splash.Close();
                window.ShowFirstRunTourIfNeeded();
            };

            app.Run(window);
        }
        catch (Exception ex)
        {
            earlyServer?.Dispose();
            System.Windows.MessageBox.Show(
                $"SysGlance failed to start:\n\n{ex}",
                "SysGlance Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }
}
