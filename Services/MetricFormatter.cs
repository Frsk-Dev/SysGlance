namespace SysGlance.Services;

/// <summary>
/// Shared formatting utilities for metric values used by the overlay and web server.
/// </summary>
internal static class MetricFormatter
{
    /// <summary>
    /// Returns the raw sensor value for a metric key.
    /// </summary>
    public static float? GetMetricValue(string key, SystemMetrics metrics)
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
    public static string FormatMetricValue(string key, SystemMetrics metrics)
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
    public static string GetMaxWidthText(string key)
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
}
