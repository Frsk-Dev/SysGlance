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
    private bool fpsWasVisible;
    private IntPtr windowHandle;
    private AppSettings settings;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string? className, string? windowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

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

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

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
        })
        {
            Owner = this
        };

        settingsWindow.ShowDialog();
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
            latestMetrics = HardwareMonitorService.Instance.GetCurrentMetrics();
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

            if (GameOnlyMetrics.Contains(key) && (GetMetricValue(key, latestMetrics) == null))
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
                Foreground = new SolidColorBrush((System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter.ConvertFromString(settings.LabelColor)),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 3, 0)
            };

            var valueBlock = new TextBlock
            {
                Text = "--",
                Foreground = new SolidColorBrush((System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter.ConvertFromString(colorHex)),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = MeasureTextWidth(GetMaxWidthText(key), 12, FontWeights.SemiBold)
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
                    Foreground = new SolidColorBrush((System.Windows.Media.Color)
                        System.Windows.Media.ColorConverter.ConvertFromString("#555555")),
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

        bool fpsNowVisible = (metrics.Fps.HasValue) && settings.IsVisible("Fps");

        if (fpsNowVisible != fpsWasVisible)
        {
            fpsWasVisible = fpsNowVisible;
            BuildOverlay();
        }

        foreach (KeyValuePair<string, TextBlock> entry in valueTextBlocks)
        {
            entry.Value.Text = FormatMetricValue(entry.Key, metrics);
        }
    }

    /// <summary>
    /// Returns the raw sensor value for a metric key.
    /// </summary>
    private static float? GetMetricValue(string key, SystemMetrics metrics)
    {
        return key switch
        {
            "CpuUsage" => metrics.CpuUsage,
            "CpuTemp" => metrics.CpuTemperature,
            "CpuClock" => metrics.CpuClock,
            "CpuPower" => metrics.CpuPower,
            "GpuUsage" => metrics.GpuUsage,
            "GpuTemp" => metrics.GpuTemperature,
            "GpuClock" => metrics.GpuClock,
            "GpuMemClock" => metrics.GpuMemClock,
            "GpuVram" => metrics.GpuVram,
            "GpuPower" => metrics.GpuPower,
            "GpuPowerPercent" => metrics.GpuPowerPercent,
            "GpuVoltage" => metrics.GpuVoltage,
            "GpuFan" => metrics.GpuFan,
            "GpuFanPercent" => metrics.GpuFanPercent,
            "GpuFan2Percent" => metrics.GpuFan2Percent,
            "GpuFan2Rpm" => metrics.GpuFan2Rpm,
            "GpuHotSpot" => metrics.GpuHotSpot,
            "GpuFbUsage" => metrics.GpuFbUsage,
            "GpuVidUsage" => metrics.GpuVidUsage,
            "GpuBusUsage" => metrics.GpuBusUsage,
            "GpuTempLimit" => metrics.GpuTempLimit,
            "GpuPowerLimit" => metrics.GpuPowerLimit,
            "GpuVoltageLimit" => metrics.GpuVoltageLimit,
            "GpuNoLoadLimit" => metrics.GpuNoLoadLimit,
            "Ram" => metrics.RamUsedGb,
            "CommitCharge" => metrics.CommitCharge,
            "Fps" => metrics.Fps,
            "FpsMin" => metrics.FpsMin,
            "FpsAvg" => metrics.FpsAvg,
            "FpsMax" => metrics.FpsMax,
            "Fps1Low" => metrics.Fps1Low,
            "Fps01Low" => metrics.Fps01Low,
            "Frametime" => metrics.Frametime,
            "Gpu1Temp" => metrics.Gpu1Temperature,
            "Gpu1Usage" => metrics.Gpu1Usage,
            "Gpu1Mem" => metrics.Gpu1Mem,
            "Gpu1MemProcess" => metrics.Gpu1MemProcess,
            "Gpu2MemProcess" => metrics.Gpu2MemProcess,
            "Gpu1Clock" => metrics.Gpu1Clock,
            "Gpu1MemClock" => metrics.Gpu1MemClock,
            "Gpu1Power" => metrics.Gpu1Power,
            "Gpu1Fan" => metrics.Gpu1Fan,
            "RamProcess" => metrics.RamProcess,
            "Cpu1Temp" => metrics.Cpu1Temp,
            "Cpu2Temp" => metrics.Cpu2Temp,
            "Cpu3Temp" => metrics.Cpu3Temp,
            "Cpu4Temp" => metrics.Cpu4Temp,
            "Cpu5Temp" => metrics.Cpu5Temp,
            "Cpu6Temp" => metrics.Cpu6Temp,
            "Cpu7Temp" => metrics.Cpu7Temp,
            "Cpu8Temp" => metrics.Cpu8Temp,
            "Cpu9Temp" => metrics.Cpu9Temp,
            "Cpu10Temp" => metrics.Cpu10Temp,
            "Cpu11Temp" => metrics.Cpu11Temp,
            "Cpu12Temp" => metrics.Cpu12Temp,
            "Cpu13Temp" => metrics.Cpu13Temp,
            "Cpu14Temp" => metrics.Cpu14Temp,
            "Cpu15Temp" => metrics.Cpu15Temp,
            "Cpu16Temp" => metrics.Cpu16Temp,
            "Cpu1Usage" => metrics.Cpu1Usage,
            "Cpu2Usage" => metrics.Cpu2Usage,
            "Cpu3Usage" => metrics.Cpu3Usage,
            "Cpu4Usage" => metrics.Cpu4Usage,
            "Cpu5Usage" => metrics.Cpu5Usage,
            "Cpu6Usage" => metrics.Cpu6Usage,
            "Cpu7Usage" => metrics.Cpu7Usage,
            "Cpu8Usage" => metrics.Cpu8Usage,
            "Cpu9Usage" => metrics.Cpu9Usage,
            "Cpu10Usage" => metrics.Cpu10Usage,
            "Cpu11Usage" => metrics.Cpu11Usage,
            "Cpu12Usage" => metrics.Cpu12Usage,
            "Cpu13Usage" => metrics.Cpu13Usage,
            "Cpu14Usage" => metrics.Cpu14Usage,
            "Cpu15Usage" => metrics.Cpu15Usage,
            "Cpu16Usage" => metrics.Cpu16Usage,
            "Cpu1Clock" => metrics.Cpu1Clock,
            "Cpu2Clock" => metrics.Cpu2Clock,
            "Cpu3Clock" => metrics.Cpu3Clock,
            "Cpu4Clock" => metrics.Cpu4Clock,
            "Cpu5Clock" => metrics.Cpu5Clock,
            "Cpu6Clock" => metrics.Cpu6Clock,
            "Cpu7Clock" => metrics.Cpu7Clock,
            "Cpu8Clock" => metrics.Cpu8Clock,
            "Cpu9Clock" => metrics.Cpu9Clock,
            "Cpu10Clock" => metrics.Cpu10Clock,
            "Cpu11Clock" => metrics.Cpu11Clock,
            "Cpu12Clock" => metrics.Cpu12Clock,
            "Cpu13Clock" => metrics.Cpu13Clock,
            "Cpu14Clock" => metrics.Cpu14Clock,
            "Cpu15Clock" => metrics.Cpu15Clock,
            "Cpu16Clock" => metrics.Cpu16Clock,
            "Cpu1Power" => metrics.Cpu1Power,
            "Cpu2Power" => metrics.Cpu2Power,
            "Cpu3Power" => metrics.Cpu3Power,
            "Cpu4Power" => metrics.Cpu4Power,
            "Cpu5Power" => metrics.Cpu5Power,
            "Cpu6Power" => metrics.Cpu6Power,
            "Cpu7Power" => metrics.Cpu7Power,
            "Cpu8Power" => metrics.Cpu8Power,
            "Cpu9Power" => metrics.Cpu9Power,
            "Cpu10Power" => metrics.Cpu10Power,
            "Cpu11Power" => metrics.Cpu11Power,
            "Cpu12Power" => metrics.Cpu12Power,
            "Cpu13Power" => metrics.Cpu13Power,
            "Cpu14Power" => metrics.Cpu14Power,
            "Cpu15Power" => metrics.Cpu15Power,
            "Cpu16Power" => metrics.Cpu16Power,
            _ => null
        };
    }

    /// <summary>
    /// Formats a metric value with units, or returns a placeholder if null.
    /// </summary>
    private static string FormatMetricValue(string key, SystemMetrics metrics)
    {
        float? value = GetMetricValue(key, metrics);

        if (!value.HasValue)
        {
            return key switch
            {
                "CpuUsage" or "GpuUsage" or "GpuPowerPercent" or "GpuFanPercent"
                    or "GpuFan2Percent" or "GpuFbUsage" or "GpuVidUsage"
                    or "GpuBusUsage" or "Gpu1Usage"
                    or "Cpu1Usage" or "Cpu2Usage" or "Cpu3Usage" or "Cpu4Usage"
                    or "Cpu5Usage" or "Cpu6Usage" or "Cpu7Usage" or "Cpu8Usage"
                    or "Cpu9Usage" or "Cpu10Usage" or "Cpu11Usage" or "Cpu12Usage"
                    or "Cpu13Usage" or "Cpu14Usage" or "Cpu15Usage"
                    or "Cpu16Usage" => "--%",
                "CpuTemp" or "GpuTemp" or "GpuHotSpot" or "Gpu1Temp"
                    or "Cpu1Temp" or "Cpu2Temp" or "Cpu3Temp" or "Cpu4Temp"
                    or "Cpu5Temp" or "Cpu6Temp" or "Cpu7Temp" or "Cpu8Temp"
                    or "Cpu9Temp" or "Cpu10Temp" or "Cpu11Temp" or "Cpu12Temp"
                    or "Cpu13Temp" or "Cpu14Temp" or "Cpu15Temp"
                    or "Cpu16Temp" => "--\u00b0C",
                "CpuClock" or "GpuClock" or "GpuMemClock" or "Gpu1Clock"
                    or "Gpu1MemClock"
                    or "Cpu1Clock" or "Cpu2Clock" or "Cpu3Clock" or "Cpu4Clock"
                    or "Cpu5Clock" or "Cpu6Clock" or "Cpu7Clock" or "Cpu8Clock"
                    or "Cpu9Clock" or "Cpu10Clock" or "Cpu11Clock" or "Cpu12Clock"
                    or "Cpu13Clock" or "Cpu14Clock" or "Cpu15Clock"
                    or "Cpu16Clock" => "-- MHz",
                "CpuPower" or "GpuPower" or "Gpu1Power"
                    or "Cpu1Power" or "Cpu2Power" or "Cpu3Power" or "Cpu4Power"
                    or "Cpu5Power" or "Cpu6Power" or "Cpu7Power" or "Cpu8Power"
                    or "Cpu9Power" or "Cpu10Power" or "Cpu11Power" or "Cpu12Power"
                    or "Cpu13Power" or "Cpu14Power" or "Cpu15Power"
                    or "Cpu16Power" => "-- W",
                "GpuVoltage" => "-- mV",
                "GpuFan" or "GpuFan2Rpm" or "Gpu1Fan" => "-- RPM",
                "GpuVram" or "CommitCharge" or "Gpu1Mem" or "Gpu1MemProcess"
                    or "Gpu2MemProcess" or "RamProcess" => "-- MB",
                "Ram" => "-- GB",
                "Frametime" => "-- ms",
                "GpuTempLimit" or "GpuPowerLimit" or "GpuVoltageLimit"
                    or "GpuNoLoadLimit" => "--",
                _ => "--"
            };
        }

        return key switch
        {
            "CpuUsage" or "GpuUsage" or "GpuPowerPercent" or "GpuFanPercent"
                or "GpuFan2Percent" or "GpuFbUsage" or "GpuVidUsage"
                or "GpuBusUsage" or "Gpu1Usage"
                or "Cpu1Usage" or "Cpu2Usage" or "Cpu3Usage" or "Cpu4Usage"
                or "Cpu5Usage" or "Cpu6Usage" or "Cpu7Usage" or "Cpu8Usage"
                or "Cpu9Usage" or "Cpu10Usage" or "Cpu11Usage" or "Cpu12Usage"
                or "Cpu13Usage" or "Cpu14Usage" or "Cpu15Usage"
                or "Cpu16Usage" => $"{value:F0}%",
            "CpuTemp" or "GpuTemp" or "GpuHotSpot" or "Gpu1Temp"
                or "Cpu1Temp" or "Cpu2Temp" or "Cpu3Temp" or "Cpu4Temp"
                or "Cpu5Temp" or "Cpu6Temp" or "Cpu7Temp" or "Cpu8Temp"
                or "Cpu9Temp" or "Cpu10Temp" or "Cpu11Temp" or "Cpu12Temp"
                or "Cpu13Temp" or "Cpu14Temp" or "Cpu15Temp"
                or "Cpu16Temp" => $"{value:F0}\u00b0C",
            "CpuClock" or "GpuClock" or "GpuMemClock" or "Gpu1Clock"
                or "Gpu1MemClock"
                or "Cpu1Clock" or "Cpu2Clock" or "Cpu3Clock" or "Cpu4Clock"
                or "Cpu5Clock" or "Cpu6Clock" or "Cpu7Clock" or "Cpu8Clock"
                or "Cpu9Clock" or "Cpu10Clock" or "Cpu11Clock" or "Cpu12Clock"
                or "Cpu13Clock" or "Cpu14Clock" or "Cpu15Clock"
                or "Cpu16Clock" => $"{value:F0} MHz",
            "CpuPower" or "GpuPower" or "Gpu1Power"
                or "Cpu1Power" or "Cpu2Power" or "Cpu3Power" or "Cpu4Power"
                or "Cpu5Power" or "Cpu6Power" or "Cpu7Power" or "Cpu8Power"
                or "Cpu9Power" or "Cpu10Power" or "Cpu11Power" or "Cpu12Power"
                or "Cpu13Power" or "Cpu14Power" or "Cpu15Power"
                or "Cpu16Power" => $"{value:F1} W",
            "GpuVoltage" => $"{value:F0} mV",
            "GpuFan" or "GpuFan2Rpm" or "Gpu1Fan" => $"{value:F0} RPM",
            "GpuVram" or "CommitCharge" or "Gpu1Mem" or "Gpu1MemProcess"
                or "Gpu2MemProcess" or "RamProcess" => $"{value:F0} MB",
            "Ram" => $"{value:F1} GB",
            "Fps" or "FpsMin" or "FpsAvg" or "FpsMax"
                or "Fps1Low" or "Fps01Low" => $"{value:F0}",
            "Frametime" => $"{value:F1} ms",
            "GpuTempLimit" or "GpuPowerLimit" or "GpuVoltageLimit"
                or "GpuNoLoadLimit" => value > 0 ? "Yes" : "No",
            _ => $"{value:F1}"
        };
    }

    /// <summary>
    /// Returns the widest expected string for a metric, used to set MinWidth.
    /// </summary>
    private static string GetMaxWidthText(string key)
    {
        return key switch
        {
            "CpuUsage" or "GpuUsage" or "GpuPowerPercent"
                or "GpuFanPercent" or "GpuFan2Percent" or "GpuFbUsage"
                or "GpuVidUsage" or "GpuBusUsage" or "Gpu1Usage"
                or "Cpu1Usage" or "Cpu2Usage" or "Cpu3Usage" or "Cpu4Usage"
                or "Cpu5Usage" or "Cpu6Usage" or "Cpu7Usage" or "Cpu8Usage"
                or "Cpu9Usage" or "Cpu10Usage" or "Cpu11Usage" or "Cpu12Usage"
                or "Cpu13Usage" or "Cpu14Usage" or "Cpu15Usage"
                or "Cpu16Usage" => "100%",
            "CpuTemp" or "GpuTemp" or "GpuHotSpot" or "Gpu1Temp"
                or "Cpu1Temp" or "Cpu2Temp" or "Cpu3Temp" or "Cpu4Temp"
                or "Cpu5Temp" or "Cpu6Temp" or "Cpu7Temp" or "Cpu8Temp"
                or "Cpu9Temp" or "Cpu10Temp" or "Cpu11Temp" or "Cpu12Temp"
                or "Cpu13Temp" or "Cpu14Temp" or "Cpu15Temp"
                or "Cpu16Temp" => "100\u00b0C",
            "CpuClock" or "GpuClock" or "GpuMemClock" or "Gpu1Clock"
                or "Gpu1MemClock"
                or "Cpu1Clock" or "Cpu2Clock" or "Cpu3Clock" or "Cpu4Clock"
                or "Cpu5Clock" or "Cpu6Clock" or "Cpu7Clock" or "Cpu8Clock"
                or "Cpu9Clock" or "Cpu10Clock" or "Cpu11Clock" or "Cpu12Clock"
                or "Cpu13Clock" or "Cpu14Clock" or "Cpu15Clock"
                or "Cpu16Clock" => "0000 MHz",
            "CpuPower" or "GpuPower" or "Gpu1Power"
                or "Cpu1Power" or "Cpu2Power" or "Cpu3Power" or "Cpu4Power"
                or "Cpu5Power" or "Cpu6Power" or "Cpu7Power" or "Cpu8Power"
                or "Cpu9Power" or "Cpu10Power" or "Cpu11Power" or "Cpu12Power"
                or "Cpu13Power" or "Cpu14Power" or "Cpu15Power"
                or "Cpu16Power" => "000.0 W",
            "GpuVoltage" => "0000 mV",
            "GpuFan" or "GpuFan2Rpm" or "Gpu1Fan" => "0000 RPM",
            "GpuVram" or "CommitCharge" or "Gpu1Mem" or "Gpu1MemProcess"
                or "Gpu2MemProcess" or "RamProcess" => "00000 MB",
            "Ram" => "00.0 GB",
            "Fps" or "FpsMin" or "FpsAvg" or "FpsMax"
                or "Fps1Low" or "Fps01Low" => "000",
            "Frametime" => "00.0 ms",
            "GpuTempLimit" or "GpuPowerLimit" or "GpuVoltageLimit"
                or "GpuNoLoadLimit" => "Yes",
            _ => "000.0"
        };
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
