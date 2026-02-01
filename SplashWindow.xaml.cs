using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SysGlance;

/// <summary>
/// Borderless splash screen with an animated loading bar.
/// </summary>
public partial class SplashWindow : Window
{
    private readonly DispatcherTimer closeTimer;

    /// <summary>
    /// Initializes the splash and starts the loading animation.
    /// </summary>
    public SplashWindow()
    {
        InitializeComponent();

        // Animate loading bar from 0 to full width.
        Loaded += (_, _) =>
        {
            double trackWidth = LoadingBar.Parent is FrameworkElement parent
                ? parent.ActualWidth
                : 272;

            var animation = new DoubleAnimation
            {
                From = 0,
                To = trackWidth,
                Duration = TimeSpan.FromSeconds(3.5),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            LoadingBar.BeginAnimation(WidthProperty, animation);
        };

        // Safety-net auto-close timer.
        closeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        closeTimer.Tick += (_, _) =>
        {
            closeTimer.Stop();
            Close();
        };
        closeTimer.Start();
    }
}
