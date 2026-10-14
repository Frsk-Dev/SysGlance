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
    /// Metrics that are game-related and may be unavailable in some sessions.
    /// They remain visible when enabled and show placeholders until populated.
    /// </summary>
    private static readonly HashSet<string> GameOnlyMetrics =
        ["Fps", "FpsMin", "FpsAvg", "FpsMax", "Fps1Low", "Fps01Low", "Frametime"];

    private readonly DispatcherTimer updateTimer;
    private readonly System.Windows.Forms.NotifyIcon trayIcon;
    private readonly Thread pollingThread;
    private readonly Dictionary<string, TextBlock> valueTextBlocks = new();
    private volatile SystemMetrics latestMetrics = new();
    private volatile bool isRunning = true;
    private IntPtr windowHandle;
    private AppSettings settings;
    private MetricsWebServer? webServer;
    private SettingsWindow? openSettingsWindow;
    private readonly PairingService pairing;
    private PairingWindow? pairingWindow;
    private readonly DesktopWidgetManager desktopWidgets;

    /// <summary>
    /// Retries the Edge connection server while its port is taken (e.g. another program had it at start-up).
    /// </summary>
    private readonly DispatcherTimer serverRetryTimer = new() { Interval = TimeSpan.FromSeconds(5) };

    // Updates: first check shortly after start-up, then every 6 hours.
    private readonly DispatcherTimer updateCheckTimer = new() { Interval = TimeSpan.FromSeconds(15) };
    private readonly UpdateService updateService = new();
    private UpdateService.UpdateInfo? availableUpdate;
    private UpdateWindow? updateWindow;
    private System.Windows.Forms.ToolStripMenuItem? updateMenuItem;
    private bool lastWebServerEnabled;
    private int lastWebServerPort;

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
    /// Accepts the early-start server from Program.Main so it can be cleanly
    /// replaced with one that references this instance's live metrics and settings.
    /// </summary>
    public MonitorWindow(IDisposable? earlyServer = null)
    {
        InitializeComponent();

        settings = AppSettings.Load();
        lastWebServerEnabled = settings.WebServerEnabled;
        lastWebServerPort = settings.WebServerPort;

        desktopWidgets = new DesktopWidgetManager(settings, () => latestMetrics);
        pairing = new PairingService(settings, OnPairingRequested);
        pairing.RequestClosed += id => Dispatcher.BeginInvoke(() =>
        {
            // The widget cancelled or the request expired while the prompt was open.
            if (pairingWindow?.RequestId == id)
            {
                pairingWindow.CloseQuietly();
            }
        });

        // Dispose the early server and start a replacement that captures this
        // instance's live fields.  The brief gap (< 10 ms) between the two is
        // handled gracefully by the dashboard's fetch loop.
        earlyServer?.Dispose();
        StartWebServerIfEnabled();

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

        var editWidgetsItem = new System.Windows.Forms.ToolStripMenuItem("Edit desktop widgets")
        {
            ForeColor = menuColor,
            Font = menuFont,
            Padding = itemPadding,
            CheckOnClick = false
        };
        editWidgetsItem.Click += (_, _) => desktopWidgets.SetEditing(!desktopWidgets.Editing);
        desktopWidgets.EditingChanged += () => editWidgetsItem.Checked = desktopWidgets.Editing;
        trayMenu.Opening += (_, _) => editWidgetsItem.Enabled = settings.DesktopWidgets.Any(w => w.Enabled);

        updateMenuItem = new System.Windows.Forms.ToolStripMenuItem("Update available")
        {
            ForeColor = System.Drawing.Color.FromArgb(111, 176, 232),
            Font = menuFont,
            Padding = itemPadding,
            Visible = false
        };
        updateMenuItem.Click += (_, _) => ShowUpdateWindow();

        trayMenu.Items.Add(updateMenuItem);
        trayMenu.Items.Add(settingsItem);
        trayMenu.Items.Add(editWidgetsItem);
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
            desktopWidgets.Stop();
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
        desktopWidgets.Start();

        serverRetryTimer.Tick += (_, _) =>
        {
            if (settings.WebServerEnabled && (webServer is null || !webServer.IsListening))
            {
                StartWebServerIfEnabled();
            }
        };
        serverRetryTimer.Start();

        updateCheckTimer.Tick += async (_, _) =>
        {
            updateCheckTimer.Interval = TimeSpan.FromHours(6);
            await CheckForUpdatesAsync(manual: false);
        };
        updateCheckTimer.Start();

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
        SyncWebServerState();
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
        // Not modal: desktop widgets must stay draggable while Settings is open (Edit layout).
        if (openSettingsWindow is not null)
        {
            openSettingsWindow.Activate();
            return;
        }

        var settingsWindow = new SettingsWindow(settings, () =>
        {
            BuildOverlay();
            DockToTaskbar();
            ApplyOverlayVisibility();
            SyncWebServerState();
        },
        () => pairing.TrustedCount,
        RevokeAllWidgets,
        desktopWidgets,
        () => ServerProblem,
        new UpdateHooks(
            () => availableUpdate,
            () => CheckForUpdatesAsync(manual: true),
            ShowUpdateWindow));

        openSettingsWindow = settingsWindow;
        settingsWindow.Closed += (_, _) => openSettingsWindow = null;
        settingsWindow.Show();
        settingsWindow.Activate();
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
                () => settings,
                pairing,
                OnWidgetOrderReceived,
                OnWidgetVisibleReceived);
            webServer.Start();
        }

        openSettingsWindow?.RefreshConnectionStatus();
    }

    /// <summary>
    /// Why the Xeneon Edge connection isn't available, or null when it is (or is switched off).
    /// </summary>
    private string? ServerProblem =>
        settings.WebServerEnabled && webServer is { IsListening: false }
            ? webServer.StartError + ". Retrying…"
            : null;

    /// <summary>
    /// Asks GitHub for a newer release. Automatic checks respect the setting and a skipped version and only
    /// pop the update window once per version; a manual check (Settings) always reports the result.
    /// </summary>
    private async Task CheckForUpdatesAsync(bool manual)
    {
        if (!manual && !settings.CheckForUpdates)
        {
            return;
        }

        UpdateService.UpdateInfo? found = await updateService.CheckForUpdateAsync();
        bool isNew = found is not null && found.Tag != availableUpdate?.Tag;
        availableUpdate = found;

        if (updateMenuItem is not null)
        {
            updateMenuItem.Visible = found is not null;
            updateMenuItem.Text = found is null ? "Update available" : $"Update to {found.Version.ToString(3)}";
        }

        openSettingsWindow?.RefreshUpdateStatus();

        if (found is null)
        {
            return;
        }

        bool skipped = string.Equals(found.Tag, settings.SkippedUpdateVersion, StringComparison.OrdinalIgnoreCase);
        if (manual || (isNew && !skipped))
        {
            ShowUpdateWindow();
        }
    }

    /// <summary>
    /// Shows (or brings forward) the update window for the available update.
    /// </summary>
    private void ShowUpdateWindow()
    {
        if (availableUpdate is null)
        {
            return;
        }

        if (updateWindow is not null)
        {
            updateWindow.Activate();
            return;
        }

        updateWindow = new UpdateWindow(availableUpdate, settings);
        updateWindow.Closed += (_, _) => updateWindow = null;
        updateWindow.Show();
        updateWindow.Activate();
    }

    /// <summary>
    /// Opens (or brings forward) Settings. Also used when SysGlance is launched while already running.
    /// </summary>
    public void OpenSettings() => OnSettingsClick();

    /// <summary>
    /// First run: opens Settings with the guided tour.
    /// </summary>
    public void ShowFirstRunTourIfNeeded()
    {
        if (settings.TourShown)
        {
            return;
        }

        OnSettingsClick();
        openSettingsWindow?.StartTour();
    }

    /// <summary>
    /// A widget on the Xeneon Edge pressed Connect. Called on the listener thread; shows the
    /// Trust/Decline prompt with the code the widget is displaying.
    /// </summary>
    private void OnPairingRequested(PairingRequest request)
    {
        Dispatcher.BeginInvoke(() =>
        {
            pairingWindow?.CloseQuietly();
            pairingWindow = new PairingWindow(request);
            pairingWindow.Resolved += (approved, timedOut) =>
            {
                if (approved)
                {
                    if (pairing.Approve(request.Id))
                    {
                        settings.Save();
                        openSettingsWindow?.RefreshConnectionStatus();
                    }
                }
                else if (timedOut)
                {
                    pairing.Expire(request.Id);
                }
                else
                {
                    pairing.Decline(request.Id);
                }
            };
            pairingWindow.Closed += (_, _) => pairingWindow = null;
            pairingWindow.Show();
        });
    }

    /// <summary>
    /// Forgets every paired widget (Disconnect in Settings). They show Connect again on their next request.
    /// </summary>
    private void RevokeAllWidgets()
    {
        pairing.RevokeAll();
        settings.Save();
    }

    /// <summary>
    /// Applies a card order dragged on the Xeneon Edge widget. Called on the listener thread,
    /// so the change is marshalled to the UI thread where settings are owned.
    /// </summary>
    /// <summary>
    /// A card was switched on or off on the Edge's edit screen (listener thread).
    /// </summary>
    private void OnWidgetVisibleReceived(string key, bool visible)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (settings.IsVisible(key) == visible)
            {
                return;
            }

            settings.SetVisible(key, visible);
            settings.Save();
            BuildOverlay();
            openSettingsWindow?.RefreshMetricList();
        });
    }

    private void OnWidgetOrderReceived(IReadOnlyList<string> orderedKeys)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (!settings.ApplyVisibleOrder(orderedKeys))
            {
                return;
            }

            settings.Save();
            BuildOverlay();
            openSettingsWindow?.RefreshMetricList();
        });
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
                MetricHistory.Instance.Record(latestMetrics);
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
                FontSize = settings.FontSize,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 3, 0)
            };

            var valueBlock = new TextBlock
            {
                Text = "--",
                Foreground = new SolidColorBrush(SafeParseColor(colorHex)),
                FontSize = settings.FontSize,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = MeasureTextWidth(MetricFormatter.GetMaxWidthText(key), settings.FontSize, FontWeights.SemiBold)
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
                    FontSize = settings.FontSize,
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

        foreach (KeyValuePair<string, TextBlock> entry in valueTextBlocks)
        {
            entry.Value.Text = MetricFormatter.FormatMetricValue(entry.Key, metrics);
        }
    }

    /// <summary>
    /// Applies web server changes only when relevant settings actually changed.
    /// </summary>
    private void SyncWebServerState(bool force = false)
    {
        bool enabledChanged = settings.WebServerEnabled != lastWebServerEnabled;
        bool portChanged = settings.WebServerPort != lastWebServerPort;
        bool missingServer = settings.WebServerEnabled && (webServer is null);

        if (force || enabledChanged || portChanged || missingServer)
        {
            StartWebServerIfEnabled();
            lastWebServerEnabled = settings.WebServerEnabled;
            lastWebServerPort = settings.WebServerPort;
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
        System.Windows.Forms.Screen targetScreen = SelectTargetScreen(screens, settings.TargetMonitor);

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

    /// <summary>
    /// Selects the desired monitor with graceful fallbacks for lower monitor counts.
    /// </summary>
    private static System.Windows.Forms.Screen SelectTargetScreen(
        System.Windows.Forms.Screen[] screens,
        string targetMonitor)
    {
        if (screens.Length == 0)
        {
            return System.Windows.Forms.Screen.PrimaryScreen ?? throw new InvalidOperationException("No screens available.");
        }

        System.Windows.Forms.Screen primary =
            screens.FirstOrDefault(s => s.Primary) ?? screens[0];

        List<System.Windows.Forms.Screen> nonPrimary =
            screens.Where(s => !s.Primary).ToList();

        return targetMonitor switch
        {
            "Primary" => primary,
            "Secondary" => nonPrimary.ElementAtOrDefault(0)
                ?? screens.ElementAtOrDefault(1)
                ?? primary,
            "Tertiary" => nonPrimary.ElementAtOrDefault(1)
                ?? screens.ElementAtOrDefault(2)
                ?? nonPrimary.ElementAtOrDefault(0)
                ?? primary,
            _ => nonPrimary.ElementAtOrDefault(0)
                ?? screens.ElementAtOrDefault(1)
                ?? primary
        };
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
