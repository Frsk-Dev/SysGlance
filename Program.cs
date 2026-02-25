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
    /// initialization so the Xeneon Edge iframe can load immediately, then
    /// shows a splash screen and launches the monitor overlay.
    /// </summary>
    [STAThread]
    public static void Main()
    {
        // Raise priority so JIT compilation and server startup get more CPU time.
        // This shortens the window where the server is not yet ready.
        // Lowered back to Normal once the server is up and MonitorWindow is created.
        System.Diagnostics.Process.GetCurrentProcess().PriorityClass =
            System.Diagnostics.ProcessPriorityClass.High;

        // Start the web server as the very first thing — before WPF, before the
        // splash, before hardware init.  This gives iCUE the best chance of
        // finding the server ready when it tries to load the Xeneon Edge iframe.
        AppSettings earlySettings = AppSettings.Load();
        MetricsWebServer? earlyServer = null;

        if (earlySettings.WebServerEnabled)
        {
            earlyServer = new MetricsWebServer(
                earlySettings.WebServerPort,
                () => EarlyMetrics,
                () => earlySettings);
            earlyServer.Start();
        }

        try
        {
            var app = new System.Windows.Application();

            // Show splash screen immediately.
            var splash = new SplashWindow();
            splash.Show();

            // Force the splash to render before heavy initialization.
            app.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

            // Initialize the main window (triggers LHM hardware scan).
            // Pass the early server so MonitorWindow can cleanly take it over.
            var window = new MonitorWindow(earlyServer);

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
