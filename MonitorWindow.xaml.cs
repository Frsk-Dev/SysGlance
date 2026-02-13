using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using SysGlance.Services;
using Orientation = System.Windows.Controls.Orientation;

namespace SysGlance;

/// <summary>
/// Taskbar overlay window that displays hardware metrics.
/// </summary>
public partial class MonitorWindow : Window
{
    /// <summary>
    /// Metrics only shown when actively reporting data (e.g. in-game).
    /// </summary>
    private static readonly HashSet<string> GameOnlyMetrics =
        ["Fps", "FpsMin", "FpsAvg", "FpsMax", "Fps1Low", "Fps01Low", "Frametime"];

    private readonly DispatcherTimer updateTimer;
    private readonly System.Windows.Forms.NotifyIcon trayIcon;
    private readonly Thread pollingThread;
    private readonly Dictionary<string, TextBlock> valueTextBlocks = new();
    private volatile SystemMetrics latestMetrics = new();
    private volatile bool isRunning = true;
    private bool gameMetricsWereVisible;
    private IntPtr windowHandle;
    private AppSettings settings;
    private MetricsWebServer? webServer;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hwnd, IntPtr hWndInsertAfter,
        int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    /// <summary>
    /// Initializes the overlay, tray icon, and polling thread.
    /// </summary>
    public MonitorWindow()
    {
        InitializeComponent();

        settings = AppSettings.Load();

        var trayMenu = new System.Windows.Forms.ContextMenuStrip
        {
            Renderer = new TrayMenuRenderer(),
            BackColor = System.Drawing.Color.FromArgb(30, 30, 30),
            ShowImageMargin = false,
            Padding = new System.Windows.Forms.Padding(2, 6, 2, 6)
        };

        trayMenu.Opened += (s, _) =>
        {
            var strip = (System.Windows.Forms.ContextMenuStrip)s!;
            int radius = 12;
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(0, 0, radius * 2, radius * 2, 180, 90);
            path.AddArc(strip.Width - radius * 2, 0, radius * 2, radius * 2, 270, 90);
            path.AddArc(strip.Width - radius * 2, strip.Height - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(0, strip.Height - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            strip.Region = new System.Drawing.Region(path);
        };

        var menuFont = new System.Drawing.Font("Segoe UI", 9.5f);
        var menuColor = System.Drawing.Color.FromArgb(204, 204, 204);
        var itemPadding = new System.Windows.Forms.Padding(8, 4, 8, 4);

        var settingsItem = new System.Windows.Forms.ToolStripMenuItem("Settings")
        {
            ForeColor = menuColor,
            Font = menuFont,
            Padding = itemPadding
        };
        settingsItem.Click += (_, _) => OnSettingsClick();

        var exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit SysGlance")
        {
            ForeColor = menuColor,
            Font = menuFont,
            Padding = itemPadding
        };
        exitItem.Click += (_, _) => System.Windows.Application.Current.Shutdown();

        trayMenu.Items.Add(settingsItem);
        trayMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        trayMenu.Items.Add(exitItem);

        trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "SysGlance",
            Visible = true,
            ContextMenuStrip = trayMenu
        };

        Closed += (_, _) =>
        {
            isRunning = false;
            webServer?.Dispose();
            trayIcon.Visible = false;
            trayIcon.Dispose();
        };

        pollingThread = new Thread(PollHardware)
        {
            IsBackground = true,
            Name = "SysGlance-HardwarePoll"
        };
        pollingThread.Start();

        updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        updateTimer.Tick += OnTimerTick;
    }

    /// <summary>
    /// Docks to the taskbar, hides from Alt+Tab, and starts the update timer.
    /// </summary>
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        windowHandle = new WindowInteropHelper(this).Handle;

        int extendedStyle = GetWindowLong(windowHandle, GWL_EXSTYLE);
        SetWindowLong(windowHandle, GWL_EXSTYLE, extendedStyle | WS_EX_TOOLWINDOW);

        BuildOverlay();
        DockToTaskbar();
        UpdateMetrics();
        ForceAboveTaskbar();
        updateTimer.Start();
        ApplyOverlayVisibility();
        StartWebServerIfEnabled();
    }

    /// <summary>
    /// Enables window dragging on left-click.
    /// </summary>
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    /// <summary>
    /// Refreshes displayed metrics and re-asserts z-order.
    /// </summary>
    private void OnTimerTick(object? sender, EventArgs e)
    {
        UpdateMetrics();
        ForceAboveTaskbar();
    }

    /// <summary>
    /// Opens the settings window; changes apply live.
    /// </summary>
    private void OnSettingsClick()
    {
        var settingsWindow = new SettingsWindow(settings, () =>
        {
            BuildOverlay();
            DockToTaskbar();
            ApplyOverlayVisibility();
            StartWebServerIfEnabled();
        })
        {
            Owner = this
        };

        settingsWindow.ShowDialog();
    }

    /// <summary>
    /// Shows or hides the overlay based on the HideOverlay setting.
    /// </summary>
    private void ApplyOverlayVisibility()
    {
        if (settings.HideOverlay)
        {
            Opacity = 0;
            IsHitTestVisible = false;
            updateTimer.Stop();
        }
        else
        {
            Opacity = 1;
            IsHitTestVisible = true;
            updateTimer.Start();
        }
    }

    /// <summary>
    /// Starts or restarts the local metrics web server if enabled in settings.
    /// </summary>
    private void StartWebServerIfEnabled()
    {
        webServer?.Dispose();
        webServer = null;

        if (settings.WebServerEnabled)
        {
            webServer = new MetricsWebServer(
                settings.WebServerPort,
                () => latestMetrics,
                () => settings);
            webServer.Start();
        }
    }

    /// <summary>
    /// Forces this window above the taskbar via SetWindowPos.
    /// </summary>
    private void ForceAboveTaskbar()
    {
        if (windowHandle != IntPtr.Zero)
        {
            SetWindowPos(
                windowHandle,
                HWND_TOPMOST,
                0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }
    }

    /// <summary>
    /// Background thread loop that polls hardware sensors.
    /// </summary>
    private void PollHardware()
    {
        while (isRunning)
        {
            try
            {
                latestMetrics = HardwareMonitorService.Instance.GetCurrentMetrics();
            }
            catch (Exception)
            {
                // Sensor read failure — keep last known values.
            }

            Thread.Sleep(500);
        }
    }

    /// <summary>
    /// Builds metric panels from current settings (order, visibility, colors).
    /// </summary>
    private void BuildOverlay()
    {
        MetricsContainer.Children.Clear();
        valueTextBlocks.Clear();

        var visibleKeys = new List<string>();

        foreach (string key in settings.MetricOrder)
        {
            if (!settings.IsVisible(key))
            {
                continue;
            }

            if (GameOnlyMetrics.Contains(key) && (MetricFormatter.GetMetricValue(key, latestMetrics) == null))
            {
                continue;
            }

            visibleKeys.Add(key);
        }

        for (int i = 0; i < visibleKeys.Count; i++)
        {
            string key = visibleKeys[i];
            string label = AppSettings.GetOverlayLabel(key);
            string colorHex = settings.GetColor(key);

            var labelBlock = new TextBlock
            {
                Text = label,
                Foreground = new SolidColorBrush(SafeParseColor(settings.LabelColor, "#AAAAAA")),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 3, 0)
            };

            var valueBlock = new TextBlock
            {
                Text = "--",
                Foreground = new SolidColorBrush(SafeParseColor(colorHex)),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = MeasureTextWidth(MetricFormatter.GetMaxWidthText(key), 12, FontWeights.SemiBold)
            };

            valueTextBlocks[key] = valueBlock;

            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(labelBlock);
            panel.Children.Add(valueBlock);
            MetricsContainer.Children.Add(panel);

            if (i < visibleKeys.Count - 1)
            {
                var separator = new TextBlock
                {
                    Text = "\u2502",
                    Foreground = new SolidColorBrush(SafeParseColor("#555555")),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(4, 0, 4, 0)
                };
                MetricsContainer.Children.Add(separator);
            }
        }
    }

    /// <summary>
    /// Updates displayed values; rebuilds overlay if game metrics appear or disappear.
    /// </summary>
    private void UpdateMetrics()
    {
        SystemMetrics metrics = latestMetrics;

        bool gameMetricsNowVisible = false;
        foreach (string key in GameOnlyMetrics)
        {
            if (settings.IsVisible(key) && (MetricFormatter.GetMetricValue(key, metrics) != null))
            {
                gameMetricsNowVisible = true;
                break;
            }
        }

        if (gameMetricsNowVisible != gameMetricsWereVisible)
        {
            gameMetricsWereVisible = gameMetricsNowVisible;
            BuildOverlay();
        }

        foreach (KeyValuePair<string, TextBlock> entry in valueTextBlocks)
        {
            entry.Value.Text = MetricFormatter.FormatMetricValue(entry.Key, metrics);
        }
    }

    /// <summary>
    /// Safely parses a hex color string, returning a fallback on failure.
    /// </summary>
    private static System.Windows.Media.Color SafeParseColor(string hex, string fallback = "#FFFFFF")
    {
        try
        {
            return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(fallback);
        }
    }

    /// <summary>
    /// Measures rendered text width in device-independent pixels.
    /// </summary>
    private static double MeasureTextWidth(string text, double fontSize, FontWeight fontWeight)
    {
        var formattedText = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            System.Windows.FlowDirection.LeftToRight,
            new Typeface(new System.Windows.Media.FontFamily("Segoe UI"), FontStyles.Normal, fontWeight, FontStretches.Normal),
            fontSize,
            System.Windows.Media.Brushes.White,
            1.0);

        return formattedText.WidthIncludingTrailingWhitespace;
    }

    /// <summary>
    /// Loads the tray icon from the embedded systray.ico resource.
    /// </summary>
    private static System.Drawing.Icon LoadTrayIcon()
    {
        try
        {
            Uri icoUri = new("pack://application:,,,/systray.ico", UriKind.Absolute);
            var streamInfo = System.Windows.Application.GetResourceStream(icoUri);

            if (streamInfo != null)
            {
                return new System.Drawing.Icon(streamInfo.Stream);
            }
        }
        catch
        {
            // Fallback to default icon.
        }

        return SystemIcons.Information;
    }

    /// <summary>
    /// Positions the window on the target monitor's taskbar.
    /// </summary>
    private void DockToTaskbar()
    {
        System.Windows.Forms.Screen[] screens = System.Windows.Forms.Screen.AllScreens;

        System.Windows.Forms.Screen targetScreen;

        if (settings.TargetMonitor == "Primary")
        {
            targetScreen = screens.First(s => s.Primary);
        }
        else
        {
                targetScreen = screens.Length > 1
                ? screens.First(s => !s.Primary)
                : screens[0];
        }

        System.Drawing.Rectangle bounds = targetScreen.Bounds;
        System.Drawing.Rectangle workArea = targetScreen.WorkingArea;

        int taskbarTop = workArea.Bottom;
        int taskbarHeight = bounds.Bottom - workArea.Bottom;

        if (taskbarHeight <= 0)
        {
            taskbarHeight = 48;
            taskbarTop = bounds.Bottom - taskbarHeight;
        }

        Dispatcher.BeginInvoke(() =>
        {
            PresentationSource source = PresentationSource.FromVisual(this);
            double dpiScaleX = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
            double dpiScaleY = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;

            Top = taskbarTop * dpiScaleY;
            Height = taskbarHeight * dpiScaleY;

            UpdateLayout();

            double screenLeftWpf = bounds.Left * dpiScaleX;
            double screenWidthWpf = bounds.Width * dpiScaleX;

            switch (settings.Position)
            {
                case "Center":
                    Left = screenLeftWpf + (screenWidthWpf / 2) - (ActualWidth / 2);
                    break;

                case "Right":
                    Left = screenLeftWpf + screenWidthWpf - ActualWidth - 8;
                    break;

                default: // "Left"
                    Left = screenLeftWpf + 8;
                    break;
            }
        }, DispatcherPriority.Loaded);
    }
}

/// <summary>
/// Dark-themed renderer for the system tray context menu.
/// </summary>
internal sealed class TrayMenuRenderer : System.Windows.Forms.ToolStripProfessionalRenderer
{
    private static readonly System.Drawing.Color MenuBackground =
        System.Drawing.Color.FromArgb(30, 30, 30);

    private static readonly System.Drawing.Color HoverBackground =
        System.Drawing.Color.FromArgb(51, 51, 51);

    private static readonly System.Drawing.Color NormalText =
        System.Drawing.Color.FromArgb(204, 204, 204);

    private static readonly System.Drawing.Color HoverText =
        System.Drawing.Color.FromArgb(67, 137, 199);

    private static readonly System.Drawing.Color SeparatorColor =
        System.Drawing.Color.FromArgb(68, 68, 68);

    public TrayMenuRenderer()
        : base(new TrayMenuColorTable())
    {
    }

    /// <summary>
    /// Renders menu item background with rounded hover highlight.
    /// </summary>
    protected override void OnRenderMenuItemBackground(
        System.Windows.Forms.ToolStripItemRenderEventArgs e)
    {
        if (e.Item.Selected)
        {
            using var brush = new System.Drawing.SolidBrush(HoverBackground);
            var rect = new System.Drawing.Rectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2);

            using System.Drawing.Drawing2D.GraphicsPath path = RoundedRect(rect, 6);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.FillPath(brush, path);

            e.Item.ForeColor = HoverText;
        }
        else
        {
            e.Item.ForeColor = NormalText;
        }
    }

    /// <summary>
    /// Renders the separator line between menu items.
    /// </summary>
    protected override void OnRenderSeparator(
        System.Windows.Forms.ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using var pen = new System.Drawing.Pen(SeparatorColor);
        e.Graphics.DrawLine(pen, 12, y, e.Item.Width - 12, y);
    }

    /// <summary>
    /// Fills the menu background with a solid dark color.
    /// </summary>
    protected override void OnRenderToolStripBackground(
        System.Windows.Forms.ToolStripRenderEventArgs e)
    {
        using var brush = new System.Drawing.SolidBrush(MenuBackground);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    /// <summary>
    /// Suppresses the default border; region clip handles the shape.
    /// </summary>
    protected override void OnRenderToolStripBorder(
        System.Windows.Forms.ToolStripRenderEventArgs e)
    {
    }

    /// <summary>
    /// Creates a rounded rectangle path.
    /// </summary>
    private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(
        System.Drawing.Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        var path = new System.Drawing.Drawing2D.GraphicsPath();

        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        return path;
    }
}

/// <summary>
/// Dark color table for the tray menu renderer.
/// </summary>
internal sealed class TrayMenuColorTable : System.Windows.Forms.ProfessionalColorTable
{
    private static readonly System.Drawing.Color Dark =
        System.Drawing.Color.FromArgb(30, 30, 30);

    public override System.Drawing.Color MenuItemSelected =>
        System.Drawing.Color.FromArgb(51, 51, 51);

    public override System.Drawing.Color ToolStripGradientBegin => Dark;

    public override System.Drawing.Color ToolStripGradientEnd => Dark;

    public override System.Drawing.Color MenuItemBorder =>
        System.Drawing.Color.Transparent;

    public override System.Drawing.Color ImageMarginGradientBegin => Dark;

    public override System.Drawing.Color ImageMarginGradientEnd => Dark;

    public override System.Drawing.Color SeparatorDark =>
        System.Drawing.Color.FromArgb(68, 68, 68);

    public override System.Drawing.Color SeparatorLight => Dark;
}
