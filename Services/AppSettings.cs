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
    /// Whether to enable the local HTTP server for the Xeneon Edge widget.
    /// </summary>
    public bool WebServerEnabled { get; set; }

    /// <summary>
    /// Port number for the local metrics web server.
    /// </summary>
    public int WebServerPort { get; set; } = 5123;

    /// <summary>
    /// Background color of the Xeneon Edge dashboard.
    /// </summary>
    public string DashboardBgColor { get; set; } = "#0A0A0A";

    /// <summary>
    /// Card background color on the Xeneon Edge dashboard.
    /// </summary>
    public string DashboardCardColor { get; set; } = "#141414";

    /// <summary>
    /// Font scale percentage for text in the Xeneon Edge dashboard (50–200).
    /// </summary>
    public int DashboardFontScale { get; set; } = 100;

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
    private static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SysGlance");

    /// <summary>
    /// Full path to the settings JSON file.
    /// </summary>
    private static string SettingsFilePath =>
        Path.Combine(SettingsDirectory, "settings.json");

    /// <summary>
    /// Loads settings from disk, returning defaults if missing or invalid.
    /// </summary>
    public static AppSettings Load()
    {
        AppSettings settings;

        try
        {
            if (File.Exists(SettingsFilePath))
            {
                string json = File.ReadAllText(SettingsFilePath);
                settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
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
        FontSize = Math.Clamp(FontSize, 8, 20);
        DashboardFontScale = Math.Clamp(DashboardFontScale, 50, 200);

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
    /// Saves settings to disk as JSON.
    /// </summary>
    public void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(this, options);
        File.WriteAllText(SettingsFilePath, json);
    }
}
