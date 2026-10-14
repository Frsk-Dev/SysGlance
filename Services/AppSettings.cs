using System.IO;
using System.Text.Json;

namespace SysGlance.Services;

/// <summary>
/// Persisted settings for metric visibility, order, colors, and overlay placement.
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// All known metric keys in their default display order.
    /// </summary>
    public static readonly string[] AllMetricKeys =
    [
        "CpuUsage", "CpuTemp", "CpuClock", "CpuPower",
        "GpuUsage", "GpuTemp", "GpuClock", "GpuMemClock",
        "GpuVram", "GpuPower", "GpuPowerPercent", "GpuVoltage",
        "GpuFan", "GpuFanPercent", "GpuFan2Percent", "GpuFan2Rpm",
        "GpuHotSpot", "GpuFbUsage", "GpuVidUsage", "GpuBusUsage",
        "GpuTempLimit", "GpuPowerLimit", "GpuVoltageLimit", "GpuNoLoadLimit",
        "Ram", "CommitCharge",
        "NetDown", "NetUp",
        "Fps", "FpsMin", "FpsAvg", "FpsMax",
        "Fps1Low", "Fps01Low", "Frametime",
        "Gpu1Temp", "Gpu1Usage", "Gpu1Mem", "Gpu1MemProcess", "Gpu2MemProcess",
        "Gpu1Clock", "Gpu1MemClock", "Gpu1Power", "Gpu1Fan",
        "RamProcess",
        "Cpu1Temp", "Cpu2Temp", "Cpu3Temp", "Cpu4Temp",
        "Cpu5Temp", "Cpu6Temp", "Cpu7Temp", "Cpu8Temp",
        "Cpu9Temp", "Cpu10Temp", "Cpu11Temp", "Cpu12Temp",
        "Cpu13Temp", "Cpu14Temp", "Cpu15Temp", "Cpu16Temp",
        "Cpu1Usage", "Cpu2Usage", "Cpu3Usage", "Cpu4Usage",
        "Cpu5Usage", "Cpu6Usage", "Cpu7Usage", "Cpu8Usage",
        "Cpu9Usage", "Cpu10Usage", "Cpu11Usage", "Cpu12Usage",
        "Cpu13Usage", "Cpu14Usage", "Cpu15Usage", "Cpu16Usage",
        "Cpu1Clock", "Cpu2Clock", "Cpu3Clock", "Cpu4Clock",
        "Cpu5Clock", "Cpu6Clock", "Cpu7Clock", "Cpu8Clock",
        "Cpu9Clock", "Cpu10Clock", "Cpu11Clock", "Cpu12Clock",
        "Cpu13Clock", "Cpu14Clock", "Cpu15Clock", "Cpu16Clock",
        "Cpu1Power", "Cpu2Power", "Cpu3Power", "Cpu4Power",
        "Cpu5Power", "Cpu6Power", "Cpu7Power", "Cpu8Power",
        "Cpu9Power", "Cpu10Power", "Cpu11Power", "Cpu12Power",
        "Cpu13Power", "Cpu14Power", "Cpu15Power", "Cpu16Power"
    ];

    /// <summary>
    /// Metric keys that are visible by default.
    /// </summary>
    private static readonly HashSet<string> DefaultVisible =
        ["CpuUsage", "CpuTemp", "GpuUsage", "GpuTemp", "Ram", "Fps"];

    /// <summary>
    /// Default color for each metric key.
    /// </summary>
    private static readonly Dictionary<string, string> DefaultColors = new()
    {
        ["CpuUsage"] = "#4389C7",
        ["CpuTemp"] = "#4389C7",
        ["CpuClock"] = "#4389C7",
        ["CpuPower"] = "#4389C7",
        ["GpuUsage"] = "#FF8A65",
        ["GpuTemp"] = "#FF8A65",
        ["GpuClock"] = "#FF8A65",
        ["GpuMemClock"] = "#FF8A65",
        ["GpuVram"] = "#FF8A65",
        ["GpuPower"] = "#FF8A65",
        ["GpuPowerPercent"] = "#FF8A65",
        ["GpuVoltage"] = "#FF8A65",
        ["GpuFan"] = "#FF8A65",
        ["GpuFanPercent"] = "#FF8A65",
        ["GpuFan2Percent"] = "#FF8A65",
        ["GpuFan2Rpm"] = "#FF8A65",
        ["GpuHotSpot"] = "#FF8A65",
        ["GpuFbUsage"] = "#FF8A65",
        ["GpuVidUsage"] = "#FF8A65",
        ["GpuBusUsage"] = "#FF8A65",
        ["GpuTempLimit"] = "#EF5350",
        ["GpuPowerLimit"] = "#EF5350",
        ["GpuVoltageLimit"] = "#EF5350",
        ["GpuNoLoadLimit"] = "#EF5350",
        ["Ram"] = "#81C784",
        ["CommitCharge"] = "#81C784",
        ["NetDown"] = "#4DD0E1",
        ["NetUp"] = "#BA68C8",
        ["Fps"] = "#E6EE9C",
        ["FpsMin"] = "#E6EE9C",
        ["FpsAvg"] = "#E6EE9C",
        ["FpsMax"] = "#E6EE9C",
        ["Fps1Low"] = "#E6EE9C",
        ["Fps01Low"] = "#E6EE9C",
        ["Frametime"] = "#E6EE9C",
        ["Gpu1Temp"] = "#FF8A65",
        ["Gpu1Usage"] = "#FF8A65",
        ["Gpu1Mem"] = "#FF8A65",
        ["Gpu1MemProcess"] = "#FF8A65",
        ["Gpu2MemProcess"] = "#FF8A65",
        ["Gpu1Clock"] = "#FF8A65",
        ["Gpu1MemClock"] = "#FF8A65",
        ["Gpu1Power"] = "#FF8A65",
        ["Gpu1Fan"] = "#FF8A65",
        ["RamProcess"] = "#81C784",
        ["Cpu1Temp"] = "#4389C7",
        ["Cpu2Temp"] = "#4389C7",
        ["Cpu3Temp"] = "#4389C7",
        ["Cpu4Temp"] = "#4389C7",
        ["Cpu5Temp"] = "#4389C7",
        ["Cpu6Temp"] = "#4389C7",
        ["Cpu7Temp"] = "#4389C7",
        ["Cpu8Temp"] = "#4389C7",
        ["Cpu9Temp"] = "#4389C7",
        ["Cpu10Temp"] = "#4389C7",
        ["Cpu11Temp"] = "#4389C7",
        ["Cpu12Temp"] = "#4389C7",
        ["Cpu13Temp"] = "#4389C7",
        ["Cpu14Temp"] = "#4389C7",
        ["Cpu15Temp"] = "#4389C7",
        ["Cpu16Temp"] = "#4389C7",
        ["Cpu1Usage"] = "#4389C7",
        ["Cpu2Usage"] = "#4389C7",
        ["Cpu3Usage"] = "#4389C7",
        ["Cpu4Usage"] = "#4389C7",
        ["Cpu5Usage"] = "#4389C7",
        ["Cpu6Usage"] = "#4389C7",
        ["Cpu7Usage"] = "#4389C7",
        ["Cpu8Usage"] = "#4389C7",
        ["Cpu9Usage"] = "#4389C7",
        ["Cpu10Usage"] = "#4389C7",
        ["Cpu11Usage"] = "#4389C7",
        ["Cpu12Usage"] = "#4389C7",
        ["Cpu13Usage"] = "#4389C7",
        ["Cpu14Usage"] = "#4389C7",
        ["Cpu15Usage"] = "#4389C7",
        ["Cpu16Usage"] = "#4389C7",
        ["Cpu1Clock"] = "#4389C7",
        ["Cpu2Clock"] = "#4389C7",
        ["Cpu3Clock"] = "#4389C7",
        ["Cpu4Clock"] = "#4389C7",
        ["Cpu5Clock"] = "#4389C7",
        ["Cpu6Clock"] = "#4389C7",
        ["Cpu7Clock"] = "#4389C7",
        ["Cpu8Clock"] = "#4389C7",
        ["Cpu9Clock"] = "#4389C7",
        ["Cpu10Clock"] = "#4389C7",
        ["Cpu11Clock"] = "#4389C7",
        ["Cpu12Clock"] = "#4389C7",
        ["Cpu13Clock"] = "#4389C7",
        ["Cpu14Clock"] = "#4389C7",
        ["Cpu15Clock"] = "#4389C7",
        ["Cpu16Clock"] = "#4389C7",
        ["Cpu1Power"] = "#4389C7",
        ["Cpu2Power"] = "#4389C7",
        ["Cpu3Power"] = "#4389C7",
        ["Cpu4Power"] = "#4389C7",
        ["Cpu5Power"] = "#4389C7",
        ["Cpu6Power"] = "#4389C7",
        ["Cpu7Power"] = "#4389C7",
        ["Cpu8Power"] = "#4389C7",
        ["Cpu9Power"] = "#4389C7",
        ["Cpu10Power"] = "#4389C7",
        ["Cpu11Power"] = "#4389C7",
        ["Cpu12Power"] = "#4389C7",
        ["Cpu13Power"] = "#4389C7",
        ["Cpu14Power"] = "#4389C7",
        ["Cpu15Power"] = "#4389C7",
        ["Cpu16Power"] = "#4389C7"
    };

    /// <summary>
    /// Display order of metric keys.
    /// </summary>
    public List<string> MetricOrder { get; set; } = [.. AllMetricKeys];

    /// <summary>
    /// Visibility state for each metric key.
    /// </summary>
    public Dictionary<string, bool> Visibility { get; set; } =
        AllMetricKeys.ToDictionary(k => k, k => DefaultVisible.Contains(k));

    /// <summary>
    /// Color hex string for each metric key.
    /// </summary>
    public Dictionary<string, string> Colors { get; set; } = new(DefaultColors);

    /// <summary>
    /// Horizontal position on the taskbar: "Left", "Center", or "Right".
    /// </summary>
    public string Position { get; set; } = "Left";

    /// <summary>
    /// Which monitor to dock to: "Primary", "Secondary", or "Tertiary".
    /// </summary>
    public string TargetMonitor { get; set; } = "Secondary";

    /// <summary>
    /// Whether to launch SysGlance automatically when Windows starts.
    /// </summary>
    public bool StartWithWindows { get; set; }

    /// <summary>
    /// Hex color string for metric label text in the overlay.
    /// </summary>
    public string LabelColor { get; set; } = "#AAAAAA";

    /// <summary>
    /// Font size in points for both labels and metric values in the overlay.
    /// </summary>
    public int FontSize { get; set; } = 12;

    /// <summary>
    /// Whether to hide the taskbar overlay while keeping the tray icon alive.
    /// </summary>
    public bool HideOverlay { get; set; }

    /// <summary>
    /// Whether the local server the Xeneon Edge iCUE widget connects to is running.
    /// </summary>
    public bool WebServerEnabled { get; set; } = true;

    /// <summary>
    /// Whether the first-run tour of the Settings window has been shown (finished or skipped).
    /// </summary>
    public bool TourShown { get; set; }

    /// <summary>
    /// Port number for the local metrics web server.
    /// </summary>
    public int WebServerPort { get; set; } = 5123;

    /// <summary>
    /// Background color of the Xeneon Edge iCUE widget.
    /// </summary>
    public string DashboardBgColor { get; set; } = "#0A0A0A";

    /// <summary>
    /// Card background color on the Xeneon Edge iCUE widget.
    /// </summary>
    public string DashboardCardColor { get; set; } = "#141414";

    /// <summary>
    /// Background opacity percentage for the Xeneon Edge iCUE widget (0–100).
    /// </summary>
    public int DashboardBgOpacity { get; set; } = 100;

    /// <summary>
    /// Card opacity percentage for the Xeneon Edge iCUE widget (0–100).
    /// </summary>
    public int DashboardCardOpacity { get; set; } = 100;

    /// <summary>
    /// Text size percentage for the Xeneon Edge iCUE widget (50–150).
    /// </summary>
    public int WidgetTextScale { get; set; } = 100;

    /// <summary>
    /// Mini graph length on the Xeneon Edge widget's cards in minutes (1, 5 or 15); 0 = no graphs.
    /// </summary>
    public int DashboardGraphMinutes { get; set; } = 5;

    /// <summary>
    /// Metrics whose card shows no mini graph (taskbar overlay has none; this is for the Xeneon Edge widget).
    /// </summary>
    public List<string> GraphsOff { get; set; } = [];

    /// <summary>
    /// iCUE widgets the user trusted through pairing. Only these can read metrics or change the order.
    /// Read and modified through <see cref="PairingService"/>, which serializes access.
    /// </summary>
    public List<TrustedWidget> TrustedWidgets { get; set; } = [];

    /// <summary>
    /// Floating desktop widgets.
    /// </summary>
    public List<DesktopWidgetConfig> DesktopWidgets { get; set; } = [];

    /// <summary>
    /// Hide all desktop widgets while a full-screen app (game, video) is in front.
    /// </summary>
    public bool HideDesktopWidgetsInFullscreen { get; set; } = true;

    /// <summary>
    /// Whether desktop widgets snap to screen edges and to each other while moved or resized in edit mode.
    /// </summary>
    public bool SnapDesktopWidgets { get; set; } = true;

    /// <summary>
    /// Check GitHub for a newer SysGlance at start-up and every few hours.
    /// </summary>
    public bool CheckForUpdates { get; set; } = true;

    /// <summary>
    /// A version the user chose to skip ("v1.2.0"); not offered again, newer versions still are.
    /// </summary>
    public string? SkippedUpdateVersion { get; set; }

    /// <summary>
    /// Reorders the visible metrics to match <paramref name="orderedKeys"/> (sent by the iCUE widget after
    /// a drag). Hidden metrics keep their positions; unknown, hidden, or duplicate keys are ignored.
    /// Returns true if the order changed.
    /// </summary>
    public bool ApplyVisibleOrder(IEnumerable<string> orderedKeys)
    {
        var seen = new HashSet<string>();
        List<string> keys = orderedKeys
            .Where(k => MetricOrder.Contains(k) && IsVisible(k) && seen.Add(k))
            .ToList();

        if (keys.Count < 2)
        {
            return false;
        }

        // The slots these metrics occupy now, filled in the new order.
        List<int> slots = keys.Select(k => MetricOrder.IndexOf(k)).OrderBy(i => i).ToList();
        bool changed = false;

        for (int i = 0; i < slots.Count; i++)
        {
            if (MetricOrder[slots[i]] != keys[i])
            {
                MetricOrder[slots[i]] = keys[i];
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// Gets the color hex string for the given metric key.
    /// </summary>
    public string GetColor(string metricKey)
    {
        return Colors.TryGetValue(metricKey, out string? color) ? color : "#FFFFFF";
    }

    /// <summary>
    /// Sets the color hex string for the given metric key.
    /// </summary>
    public void SetColor(string metricKey, string color)
    {
        Colors[metricKey] = color;
    }

    /// <summary>
    /// Gets whether the given metric key is visible.
    /// </summary>
    public bool IsVisible(string metricKey)
    {
        return Visibility.TryGetValue(metricKey, out bool visible) && visible;
    }

    /// <summary>
    /// Sets the visibility for the given metric key.
    /// </summary>
    public void SetVisible(string metricKey, bool visible)
    {
        Visibility[metricKey] = visible;
    }

    /// <summary>
    /// Whether the metric's card shows a mini graph.
    /// </summary>
    public bool HasGraph(string metricKey) => !GraphsOff.Contains(metricKey);

    public void SetGraph(string metricKey, bool on)
    {
        GraphsOff.Remove(metricKey);
        if (!on)
        {
            GraphsOff.Add(metricKey);
        }
    }

    /// <summary>
    /// Gets the display label for a metric key.
    /// </summary>
    public static string GetLabel(string metricKey)
    {
        return metricKey switch
        {
            "CpuUsage" => "CPU Usage",
            "CpuTemp" => "CPU Temp",
            "CpuClock" => "CPU Clock",
            "CpuPower" => "CPU Power",
            "GpuUsage" => "GPU Usage",
            "GpuTemp" => "GPU Temp",
            "GpuClock" => "GPU Clock",
            "GpuMemClock" => "GPU Mem Clock",
            "GpuVram" => "VRAM Usage",
            "GpuPower" => "GPU Power (W)",
            "GpuPowerPercent" => "GPU Power (%)",
            "GpuVoltage" => "GPU Voltage",
            "GpuFan" => "GPU Fan (RPM)",
            "GpuFanPercent" => "GPU Fan (%)",
            "GpuFan2Percent" => "GPU Fan 2 (%)",
            "GpuFan2Rpm" => "GPU Fan 2 (RPM)",
            "GpuHotSpot" => "GPU Hot Spot",
            "GpuFbUsage" => "GPU FB Usage",
            "GpuVidUsage" => "GPU Video Usage",
            "GpuBusUsage" => "GPU Bus Usage",
            "GpuTempLimit" => "GPU Temp Limit",
            "GpuPowerLimit" => "GPU Power Limit",
            "GpuVoltageLimit" => "GPU Voltage Limit",
            "GpuNoLoadLimit" => "GPU No Load Limit",
            "Ram" => "RAM",
            "CommitCharge" => "Commit Charge",
            "NetDown" => "Download",
            "NetUp" => "Upload",
            "Fps" => "FPS",
            "FpsMin" => "FPS Min",
            "FpsAvg" => "FPS Avg",
            "FpsMax" => "FPS Max",
            "Fps1Low" => "FPS 1% Low",
            "Fps01Low" => "FPS 0.1% Low",
            "Frametime" => "Frame Time",
            "Gpu1Temp" => "GPU1 Temp",
            "Gpu1Usage" => "GPU1 Usage",
            "Gpu1Mem" => "GPU1 Memory",
            "Gpu1MemProcess" => "GPU1 Mem (Process)",
            "Gpu2MemProcess" => "GPU2 Mem (Process)",
            "Gpu1Clock" => "GPU1 Clock",
            "Gpu1MemClock" => "GPU1 Mem Clock",
            "Gpu1Power" => "GPU1 Power",
            "Gpu1Fan" => "GPU1 Fan (RPM)",
            "RamProcess" => "RAM (Process)",
            "Cpu1Temp" => "CPU Core 1 Temp",
            "Cpu2Temp" => "CPU Core 2 Temp",
            "Cpu3Temp" => "CPU Core 3 Temp",
            "Cpu4Temp" => "CPU Core 4 Temp",
            "Cpu5Temp" => "CPU Core 5 Temp",
            "Cpu6Temp" => "CPU Core 6 Temp",
            "Cpu7Temp" => "CPU Core 7 Temp",
            "Cpu8Temp" => "CPU Core 8 Temp",
            "Cpu9Temp" => "CPU Core 9 Temp",
            "Cpu10Temp" => "CPU Core 10 Temp",
            "Cpu11Temp" => "CPU Core 11 Temp",
            "Cpu12Temp" => "CPU Core 12 Temp",
            "Cpu13Temp" => "CPU Core 13 Temp",
            "Cpu14Temp" => "CPU Core 14 Temp",
            "Cpu15Temp" => "CPU Core 15 Temp",
            "Cpu16Temp" => "CPU Core 16 Temp",
            "Cpu1Usage" => "CPU Core 1 Usage",
            "Cpu2Usage" => "CPU Core 2 Usage",
            "Cpu3Usage" => "CPU Core 3 Usage",
            "Cpu4Usage" => "CPU Core 4 Usage",
            "Cpu5Usage" => "CPU Core 5 Usage",
            "Cpu6Usage" => "CPU Core 6 Usage",
            "Cpu7Usage" => "CPU Core 7 Usage",
            "Cpu8Usage" => "CPU Core 8 Usage",
            "Cpu9Usage" => "CPU Core 9 Usage",
            "Cpu10Usage" => "CPU Core 10 Usage",
            "Cpu11Usage" => "CPU Core 11 Usage",
            "Cpu12Usage" => "CPU Core 12 Usage",
            "Cpu13Usage" => "CPU Core 13 Usage",
            "Cpu14Usage" => "CPU Core 14 Usage",
            "Cpu15Usage" => "CPU Core 15 Usage",
            "Cpu16Usage" => "CPU Core 16 Usage",
            "Cpu1Clock" => "CPU Core 1 Clock",
            "Cpu2Clock" => "CPU Core 2 Clock",
            "Cpu3Clock" => "CPU Core 3 Clock",
            "Cpu4Clock" => "CPU Core 4 Clock",
            "Cpu5Clock" => "CPU Core 5 Clock",
            "Cpu6Clock" => "CPU Core 6 Clock",
            "Cpu7Clock" => "CPU Core 7 Clock",
            "Cpu8Clock" => "CPU Core 8 Clock",
            "Cpu9Clock" => "CPU Core 9 Clock",
            "Cpu10Clock" => "CPU Core 10 Clock",
            "Cpu11Clock" => "CPU Core 11 Clock",
            "Cpu12Clock" => "CPU Core 12 Clock",
            "Cpu13Clock" => "CPU Core 13 Clock",
            "Cpu14Clock" => "CPU Core 14 Clock",
            "Cpu15Clock" => "CPU Core 15 Clock",
            "Cpu16Clock" => "CPU Core 16 Clock",
            "Cpu1Power" => "CPU Core 1 Power",
            "Cpu2Power" => "CPU Core 2 Power",
            "Cpu3Power" => "CPU Core 3 Power",
            "Cpu4Power" => "CPU Core 4 Power",
            "Cpu5Power" => "CPU Core 5 Power",
            "Cpu6Power" => "CPU Core 6 Power",
            "Cpu7Power" => "CPU Core 7 Power",
            "Cpu8Power" => "CPU Core 8 Power",
            "Cpu9Power" => "CPU Core 9 Power",
            "Cpu10Power" => "CPU Core 10 Power",
            "Cpu11Power" => "CPU Core 11 Power",
            "Cpu12Power" => "CPU Core 12 Power",
            "Cpu13Power" => "CPU Core 13 Power",
            "Cpu14Power" => "CPU Core 14 Power",
            "Cpu15Power" => "CPU Core 15 Power",
            "Cpu16Power" => "CPU Core 16 Power",
            _ => metricKey
        };
    }

    /// <summary>
    /// Gets the abbreviated overlay label for a metric key.
    /// </summary>
    public static string GetOverlayLabel(string metricKey)
    {
        return metricKey switch
        {
            "CpuUsage" => "CPU",
            "CpuTemp" => "CPU",
            "CpuClock" => "CPU",
            "CpuPower" => "CPU",
            "GpuUsage" => "GPU",
            "GpuTemp" => "GPU",
            "GpuClock" => "GPU",
            "GpuMemClock" => "MEM",
            "GpuVram" => "VRAM",
            "GpuPower" => "PWR",
            "GpuPowerPercent" => "PWR",
            "GpuVoltage" => "GPU",
            "GpuFan" => "FAN",
            "GpuFanPercent" => "FAN",
            "GpuFan2Percent" => "FN2",
            "GpuFan2Rpm" => "FN2",
            "GpuHotSpot" => "HOT",
            "GpuFbUsage" => "FB",
            "GpuVidUsage" => "VID",
            "GpuBusUsage" => "BUS",
            "GpuTempLimit" => "TLM",
            "GpuPowerLimit" => "PLM",
            "GpuVoltageLimit" => "VLM",
            "GpuNoLoadLimit" => "NLL",
            "Ram" => "RAM",
            "CommitCharge" => "CMT",
            "NetDown" => "DL",
            "NetUp" => "UL",
            "Fps" => "FPS",
            "FpsMin" => "MIN",
            "FpsAvg" => "AVG",
            "FpsMax" => "MAX",
            "Fps1Low" => "1%",
            "Fps01Low" => ".1%",
            "Frametime" => "FT",
            "Gpu1Temp" => "GP1",
            "Gpu1Usage" => "GP1",
            "Gpu1Mem" => "GP1",
            "Gpu1MemProcess" => "GP1",
            "Gpu2MemProcess" => "GP2",
            "Gpu1Clock" => "GP1",
            "Gpu1MemClock" => "MC1",
            "Gpu1Power" => "GP1",
            "Gpu1Fan" => "FN1",
            "RamProcess" => "RAM",
            "Cpu1Temp" => "C1",
            "Cpu2Temp" => "C2",
            "Cpu3Temp" => "C3",
            "Cpu4Temp" => "C4",
            "Cpu5Temp" => "C5",
            "Cpu6Temp" => "C6",
            "Cpu7Temp" => "C7",
            "Cpu8Temp" => "C8",
            "Cpu9Temp" => "C9",
            "Cpu10Temp" => "C10",
            "Cpu11Temp" => "C11",
            "Cpu12Temp" => "C12",
            "Cpu13Temp" => "C13",
            "Cpu14Temp" => "C14",
            "Cpu15Temp" => "C15",
            "Cpu16Temp" => "C16",
            "Cpu1Usage" => "C1",
            "Cpu2Usage" => "C2",
            "Cpu3Usage" => "C3",
            "Cpu4Usage" => "C4",
            "Cpu5Usage" => "C5",
            "Cpu6Usage" => "C6",
            "Cpu7Usage" => "C7",
            "Cpu8Usage" => "C8",
            "Cpu9Usage" => "C9",
            "Cpu10Usage" => "C10",
            "Cpu11Usage" => "C11",
            "Cpu12Usage" => "C12",
            "Cpu13Usage" => "C13",
            "Cpu14Usage" => "C14",
            "Cpu15Usage" => "C15",
            "Cpu16Usage" => "C16",
            "Cpu1Clock" => "C1",
            "Cpu2Clock" => "C2",
            "Cpu3Clock" => "C3",
            "Cpu4Clock" => "C4",
            "Cpu5Clock" => "C5",
            "Cpu6Clock" => "C6",
            "Cpu7Clock" => "C7",
            "Cpu8Clock" => "C8",
            "Cpu9Clock" => "C9",
            "Cpu10Clock" => "C10",
            "Cpu11Clock" => "C11",
            "Cpu12Clock" => "C12",
            "Cpu13Clock" => "C13",
            "Cpu14Clock" => "C14",
            "Cpu15Clock" => "C15",
            "Cpu16Clock" => "C16",
            "Cpu1Power" => "C1",
            "Cpu2Power" => "C2",
            "Cpu3Power" => "C3",
            "Cpu4Power" => "C4",
            "Cpu5Power" => "C5",
            "Cpu6Power" => "C6",
            "Cpu7Power" => "C7",
            "Cpu8Power" => "C8",
            "Cpu9Power" => "C9",
            "Cpu10Power" => "C10",
            "Cpu11Power" => "C11",
            "Cpu12Power" => "C12",
            "Cpu13Power" => "C13",
            "Cpu14Power" => "C14",
            "Cpu15Power" => "C15",
            "Cpu16Power" => "C16",
            _ => metricKey
        };
    }

    /// <summary>
    /// Path to the settings JSON file in the user's AppData folder.
    /// </summary>
    internal static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SysGlance");

    /// <summary>
    /// Full path to the settings JSON file.
    /// </summary>
    internal static string SettingsFilePath =>
        Path.Combine(SettingsDirectory, "settings.json");

    /// <summary>
    /// Loads settings from disk, returning defaults if missing or invalid.
    /// </summary>
    /// <summary>
    /// When true, <see cref="Save"/> does nothing (set just before a restore restarts the app).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public static bool SavingSuspended { get; set; }

    /// <summary>
    /// Reads a settings file strictly (for restoring a backup): fails instead of falling back to defaults.
    /// </summary>
    public static bool TryReadFile(string path, out AppSettings? settings, out string? error)
    {
        settings = null;
        error = null;

        try
        {
            string json = File.ReadAllText(path);
            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                // A SysGlance settings file always has these; anything else is the wrong file.
                if (doc.RootElement.ValueKind != JsonValueKind.Object
                    || !doc.RootElement.TryGetProperty(nameof(MetricOrder), out _)
                    || !doc.RootElement.TryGetProperty(nameof(Visibility), out _))
                {
                    error = "This isn't a SysGlance settings file.";
                    return false;
                }
            }

            settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (settings is null)
            {
                error = "The file is empty.";
                return false;
            }

            settings.EnsureAllMetrics();
            return true;
        }
        catch (JsonException)
        {
            error = "The file is damaged or isn't valid JSON.";
        }
        catch (IOException ex)
        {
            error = ex.Message;
        }
        catch (UnauthorizedAccessException ex)
        {
            error = ex.Message;
        }

        return false;
    }

    public static AppSettings Load()
    {
        AppSettings settings;

        try
        {
            if (File.Exists(SettingsFilePath))
            {
                string json = File.ReadAllText(SettingsFilePath);
                settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();

                // Settings from before the tour existed: that user isn't new, so don't show it.
                if (!json.Contains("\"TourShown\""))
                {
                    settings.TourShown = true;
                }
            }
            else
            {
                settings = new AppSettings();
            }
        }
        catch (JsonException)
        {
            // Corrupted settings file — use defaults.
            settings = new AppSettings();
        }
        catch (IOException)
        {
            // File access failed — use defaults.
            settings = new AppSettings();
        }

        settings.EnsureAllMetrics();
        return settings;
    }

    /// <summary>
    /// Adds missing metrics and removes unknown keys from the loaded settings.
    /// </summary>
    private void EnsureAllMetrics()
    {
        // Guard against null collections from corrupted JSON.
        MetricOrder ??= [.. AllMetricKeys];
        Visibility ??= AllMetricKeys.ToDictionary(k => k, k => DefaultVisible.Contains(k));
        Colors ??= new Dictionary<string, string>(DefaultColors);
        LabelColor ??= "#AAAAAA";
        Position ??= "Left";
        TargetMonitor ??= "Secondary";
        if ((TargetMonitor != "Primary") && (TargetMonitor != "Secondary") && (TargetMonitor != "Tertiary"))
        {
            TargetMonitor = "Secondary";
        }
        DashboardBgColor ??= "#0A0A0A";
        DashboardCardColor ??= "#141414";
        TrustedWidgets ??= [];
        TrustedWidgets.RemoveAll(w => (w is null) || string.IsNullOrEmpty(w.TokenHash));
        DesktopWidgets ??= [];
        DesktopWidgets.RemoveAll(w => w is null);
        DesktopWidgets.ForEach(w => w.Normalize());
        FontSize = Math.Clamp(FontSize, 8, 20);
        DashboardBgOpacity = Math.Clamp(DashboardBgOpacity, 0, 100);
        DashboardCardOpacity = Math.Clamp(DashboardCardOpacity, 0, 100);
        WidgetTextScale = Math.Clamp(WidgetTextScale, 50, 150);
        DashboardGraphMinutes = MetricHistory.NormalizeMinutes(DashboardGraphMinutes);
        GraphsOff ??= [];

        var knownKeys = new HashSet<string>(AllMetricKeys);

        // Add any missing metrics to the end of MetricOrder.
        foreach (string key in AllMetricKeys)
        {
            if (!MetricOrder.Contains(key))
            {
                MetricOrder.Add(key);
            }
        }

        // Remove any unknown metrics from MetricOrder.
        MetricOrder.RemoveAll(k => !knownKeys.Contains(k));

        // Ensure Visibility and Colors have entries for all metrics.
        foreach (string key in AllMetricKeys)
        {
            Visibility.TryAdd(key, DefaultVisible.Contains(key));
            Colors.TryAdd(key, DefaultColors.GetValueOrDefault(key, "#FFFFFF"));
        }
    }

    /// <summary>
    /// Indented JSON that tolerates NaN/Infinity, so one bad number can never stop settings from saving.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    /// <summary>
    /// Saves settings to disk as JSON.
    /// </summary>
    public void Save()
    {
        // Set while restoring a backup, so the shutting-down app can't overwrite the restored file.
        if (SavingSuspended)
        {
            return;
        }

        Directory.CreateDirectory(SettingsDirectory);
        string json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(SettingsFilePath, json);
    }
}
