namespace SysGlance;

/// <summary>
/// Entry point for the SysGlance application.
/// Shows a splash screen while initializing, then launches the monitor overlay.
/// </summary>
public static class Program
{
    /// <summary>
    /// Main entry point. Shows a splash screen, initializes the monitor window,
    /// then closes the splash and runs the main overlay.
    /// </summary>
    [STAThread]
    public static void Main()
    {
        try
        {
            var app = new System.Windows.Application();

            // Show splash screen immediately.
            var splash = new SplashWindow();
            splash.Show();

            // Force the splash to render before heavy initialization.
            app.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

            // Initialize the main window (triggers LHM hardware scan).
            var window = new MonitorWindow();

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
            System.Windows.MessageBox.Show(
                $"SysGlance failed to start:\n\n{ex}",
                "SysGlance Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }
}
