namespace SysGlance.Services;

/// <summary>
/// Represents a point-in-time snapshot of system hardware metrics.
/// Null values indicate the sensor was unavailable.
/// </summary>
public sealed class SystemMetrics
{
    /// <summary>
    /// CPU package temperature in degrees Celsius.
    /// </summary>
    public float? CpuTemperature { get; init; }

    /// <summary>
    /// CPU total load as a percentage (0-100).
    /// </summary>
    public float? CpuUsage { get; init; }

    /// <summary>
    /// CPU core clock speed in MHz.
    /// </summary>
    public float? CpuClock { get; init; }

    /// <summary>
    /// CPU package power draw in watts.
    /// </summary>
    public float? CpuPower { get; init; }

    /// <summary>
    /// GPU core temperature in degrees Celsius.
    /// </summary>
    public float? GpuTemperature { get; init; }

    /// <summary>
    /// GPU core load as a percentage (0-100).
    /// </summary>
    public float? GpuUsage { get; init; }

    /// <summary>
    /// GPU core clock speed in MHz.
    /// </summary>
    public float? GpuClock { get; init; }

    /// <summary>
    /// GPU memory clock speed in MHz.
    /// </summary>
    public float? GpuMemClock { get; init; }

    /// <summary>
    /// GPU VRAM usage in megabytes.
    /// </summary>
    public float? GpuVram { get; init; }

    /// <summary>
    /// GPU power draw in watts.
    /// </summary>
    public float? GpuPower { get; init; }

    /// <summary>
    /// GPU core voltage in millivolts.
    /// </summary>
    public float? GpuVoltage { get; init; }

    /// <summary>
    /// GPU fan speed in RPM.
    /// </summary>
    public float? GpuFan { get; init; }

    /// <summary>
    /// GPU hot spot temperature in degrees Celsius.
    /// </summary>
    public float? GpuHotSpot { get; init; }

    /// <summary>
    /// Physical RAM currently in use, in gigabytes.
    /// </summary>
    public float? RamUsedGb { get; init; }

    /// <summary>
    /// Total physical RAM installed, in gigabytes.
    /// </summary>
    public float? RamTotalGb { get; init; }

    /// <summary>
    /// Current frames per second from MSI Afterburner.
    /// </summary>
    public float? Fps { get; init; }

    /// <summary>
    /// Current frame time in milliseconds from MSI Afterburner.
    /// </summary>
    public float? Frametime { get; init; }

    /// <summary>
    /// GPU fan speed as a percentage (0-100).
    /// </summary>
    public float? GpuFanPercent { get; init; }

    /// <summary>
    /// GPU power draw as a percentage of TDP.
    /// </summary>
    public float? GpuPowerPercent { get; init; }

    /// <summary>
    /// Minimum FPS from MSI Afterburner.
    /// </summary>
    public float? FpsMin { get; init; }

    /// <summary>
    /// Average FPS from MSI Afterburner.
    /// </summary>
    public float? FpsAvg { get; init; }

    /// <summary>
    /// Maximum FPS from MSI Afterburner.
    /// </summary>
    public float? FpsMax { get; init; }

    /// <summary>
    /// 1% low FPS from MSI Afterburner.
    /// </summary>
    public float? Fps1Low { get; init; }

    /// <summary>
    /// 0.1% low FPS from MSI Afterburner.
    /// </summary>
    public float? Fps01Low { get; init; }

    /// <summary>
    /// GPU framebuffer usage as a percentage.
    /// </summary>
    public float? GpuFbUsage { get; init; }

    /// <summary>
    /// GPU video engine usage as a percentage.
    /// </summary>
    public float? GpuVidUsage { get; init; }

    /// <summary>
    /// GPU bus interface usage as a percentage.
    /// </summary>
    public float? GpuBusUsage { get; init; }

    /// <summary>
    /// GPU second fan speed as a percentage (0-100).
    /// </summary>
    public float? GpuFan2Percent { get; init; }

    /// <summary>
    /// GPU second fan speed in RPM.
    /// </summary>
    public float? GpuFan2Rpm { get; init; }

    /// <summary>
    /// GPU temperature limit flag (1 = throttling).
    /// </summary>
    public float? GpuTempLimit { get; init; }

    /// <summary>
    /// GPU power limit flag (1 = throttling).
    /// </summary>
    public float? GpuPowerLimit { get; init; }

    /// <summary>
    /// GPU voltage limit flag (1 = throttling).
    /// </summary>
    public float? GpuVoltageLimit { get; init; }

    /// <summary>
    /// GPU no load limit flag (1 = idle downclocking).
    /// </summary>
    public float? GpuNoLoadLimit { get; init; }

    /// <summary>
    /// System commit charge in megabytes.
    /// </summary>
    public float? CommitCharge { get; init; }

    /// <summary>
    /// GPU1 temperature in degrees Celsius.
    /// </summary>
    public float? Gpu1Temperature { get; init; }

    /// <summary>
    /// GPU1 core load as a percentage (0-100).
    /// </summary>
    public float? Gpu1Usage { get; init; }

    /// <summary>
    /// GPU1 memory usage in megabytes.
    /// </summary>
    public float? Gpu1Mem { get; init; }

    /// <summary>
    /// GPU1 per-process memory usage in megabytes.
    /// </summary>
    public float? Gpu1MemProcess { get; init; }

    /// <summary>
    /// GPU2 per-process memory usage in megabytes.
    /// </summary>
    public float? Gpu2MemProcess { get; init; }

    /// <summary>
    /// GPU1 core clock speed in MHz.
    /// </summary>
    public float? Gpu1Clock { get; init; }

    /// <summary>
    /// GPU1 memory clock speed in MHz.
    /// </summary>
    public float? Gpu1MemClock { get; init; }

    /// <summary>
    /// GPU1 power draw in watts.
    /// </summary>
    public float? Gpu1Power { get; init; }

    /// <summary>
    /// GPU1 fan speed in RPM.
    /// </summary>
    public float? Gpu1Fan { get; init; }

    /// <summary>
    /// Per-process RAM usage in megabytes.
    /// </summary>
    public float? RamProcess { get; init; }

    /// <summary>
    /// CPU core 1 temperature in degrees Celsius.
    /// </summary>
    public float? Cpu1Temp { get; init; }

    /// <summary>
    /// CPU core 2 temperature in degrees Celsius.
    /// </summary>
    public float? Cpu2Temp { get; init; }

    /// <summary>
    /// CPU core 3 temperature in degrees Celsius.
    /// </summary>
    public float? Cpu3Temp { get; init; }

    /// <summary>
    /// CPU core 4 temperature in degrees Celsius.
    /// </summary>
    public float? Cpu4Temp { get; init; }

    /// <summary>
    /// CPU core 5 temperature in degrees Celsius.
    /// </summary>
    public float? Cpu5Temp { get; init; }

    /// <summary>
    /// CPU core 6 temperature in degrees Celsius.
    /// </summary>
    public float? Cpu6Temp { get; init; }

    /// <summary>
    /// CPU core 7 temperature in degrees Celsius.
    /// </summary>
    public float? Cpu7Temp { get; init; }

    /// <summary>
    /// CPU core 8 temperature in degrees Celsius.
    /// </summary>
    public float? Cpu8Temp { get; init; }

    /// <summary>
    /// CPU core 9 temperature in degrees Celsius.
    /// </summary>
    public float? Cpu9Temp { get; init; }

    /// <summary>
    /// CPU core 10 temperature in degrees Celsius.
    /// </summary>
    public float? Cpu10Temp { get; init; }

    /// <summary>
    /// CPU core 11 temperature in degrees Celsius.
    /// </summary>
    public float? Cpu11Temp { get; init; }

    /// <summary>
    /// CPU core 12 temperature in degrees Celsius.
    /// </summary>
    public float? Cpu12Temp { get; init; }

    /// <summary>
    /// CPU core 13 temperature in degrees Celsius.
    /// </summary>
    public float? Cpu13Temp { get; init; }

    /// <summary>
    /// CPU core 14 temperature in degrees Celsius.
    /// </summary>
    public float? Cpu14Temp { get; init; }

    /// <summary>
    /// CPU core 15 temperature in degrees Celsius.
    /// </summary>
    public float? Cpu15Temp { get; init; }

    /// <summary>
    /// CPU core 16 temperature in degrees Celsius.
    /// </summary>
    public float? Cpu16Temp { get; init; }

    /// <summary>
    /// CPU core 1 load as a percentage (0-100).
    /// </summary>
    public float? Cpu1Usage { get; init; }

    /// <summary>
    /// CPU core 2 load as a percentage (0-100).
    /// </summary>
    public float? Cpu2Usage { get; init; }

    /// <summary>
    /// CPU core 3 load as a percentage (0-100).
    /// </summary>
    public float? Cpu3Usage { get; init; }

    /// <summary>
    /// CPU core 4 load as a percentage (0-100).
    /// </summary>
    public float? Cpu4Usage { get; init; }

    /// <summary>
    /// CPU core 5 load as a percentage (0-100).
    /// </summary>
    public float? Cpu5Usage { get; init; }

    /// <summary>
    /// CPU core 6 load as a percentage (0-100).
    /// </summary>
    public float? Cpu6Usage { get; init; }

    /// <summary>
    /// CPU core 7 load as a percentage (0-100).
    /// </summary>
    public float? Cpu7Usage { get; init; }

    /// <summary>
    /// CPU core 8 load as a percentage (0-100).
    /// </summary>
    public float? Cpu8Usage { get; init; }

    /// <summary>
    /// CPU core 9 load as a percentage (0-100).
    /// </summary>
    public float? Cpu9Usage { get; init; }

    /// <summary>
    /// CPU core 10 load as a percentage (0-100).
    /// </summary>
    public float? Cpu10Usage { get; init; }

    /// <summary>
    /// CPU core 11 load as a percentage (0-100).
    /// </summary>
    public float? Cpu11Usage { get; init; }

    /// <summary>
    /// CPU core 12 load as a percentage (0-100).
    /// </summary>
    public float? Cpu12Usage { get; init; }

    /// <summary>
    /// CPU core 13 load as a percentage (0-100).
    /// </summary>
    public float? Cpu13Usage { get; init; }

    /// <summary>
    /// CPU core 14 load as a percentage (0-100).
    /// </summary>
    public float? Cpu14Usage { get; init; }

    /// <summary>
    /// CPU core 15 load as a percentage (0-100).
    /// </summary>
    public float? Cpu15Usage { get; init; }

    /// <summary>
    /// CPU core 16 load as a percentage (0-100).
    /// </summary>
    public float? Cpu16Usage { get; init; }

    /// <summary>
    /// CPU core 1 clock speed in MHz.
    /// </summary>
    public float? Cpu1Clock { get; init; }

    /// <summary>
    /// CPU core 2 clock speed in MHz.
    /// </summary>
    public float? Cpu2Clock { get; init; }

    /// <summary>
    /// CPU core 3 clock speed in MHz.
    /// </summary>
    public float? Cpu3Clock { get; init; }

    /// <summary>
    /// CPU core 4 clock speed in MHz.
    /// </summary>
    public float? Cpu4Clock { get; init; }

    /// <summary>
    /// CPU core 5 clock speed in MHz.
    /// </summary>
    public float? Cpu5Clock { get; init; }

    /// <summary>
    /// CPU core 6 clock speed in MHz.
    /// </summary>
    public float? Cpu6Clock { get; init; }

    /// <summary>
    /// CPU core 7 clock speed in MHz.
    /// </summary>
    public float? Cpu7Clock { get; init; }

    /// <summary>
    /// CPU core 8 clock speed in MHz.
    /// </summary>
    public float? Cpu8Clock { get; init; }

    /// <summary>
    /// CPU core 9 clock speed in MHz.
    /// </summary>
    public float? Cpu9Clock { get; init; }

    /// <summary>
    /// CPU core 10 clock speed in MHz.
    /// </summary>
    public float? Cpu10Clock { get; init; }

    /// <summary>
    /// CPU core 11 clock speed in MHz.
    /// </summary>
    public float? Cpu11Clock { get; init; }

    /// <summary>
    /// CPU core 12 clock speed in MHz.
    /// </summary>
    public float? Cpu12Clock { get; init; }

    /// <summary>
    /// CPU core 13 clock speed in MHz.
    /// </summary>
    public float? Cpu13Clock { get; init; }

    /// <summary>
    /// CPU core 14 clock speed in MHz.
    /// </summary>
    public float? Cpu14Clock { get; init; }

    /// <summary>
    /// CPU core 15 clock speed in MHz.
    /// </summary>
    public float? Cpu15Clock { get; init; }

    /// <summary>
    /// CPU core 16 clock speed in MHz.
    /// </summary>
    public float? Cpu16Clock { get; init; }

    /// <summary>
    /// CPU core 1 power draw in watts.
    /// </summary>
    public float? Cpu1Power { get; init; }

    /// <summary>
    /// CPU core 2 power draw in watts.
    /// </summary>
    public float? Cpu2Power { get; init; }

    /// <summary>
    /// CPU core 3 power draw in watts.
    /// </summary>
    public float? Cpu3Power { get; init; }

    /// <summary>
    /// CPU core 4 power draw in watts.
    /// </summary>
    public float? Cpu4Power { get; init; }

    /// <summary>
    /// CPU core 5 power draw in watts.
    /// </summary>
    public float? Cpu5Power { get; init; }

    /// <summary>
    /// CPU core 6 power draw in watts.
    /// </summary>
    public float? Cpu6Power { get; init; }

    /// <summary>
    /// CPU core 7 power draw in watts.
    /// </summary>
    public float? Cpu7Power { get; init; }

    /// <summary>
    /// CPU core 8 power draw in watts.
    /// </summary>
    public float? Cpu8Power { get; init; }

    /// <summary>
    /// CPU core 9 power draw in watts.
    /// </summary>
    public float? Cpu9Power { get; init; }

    /// <summary>
    /// CPU core 10 power draw in watts.
    /// </summary>
    public float? Cpu10Power { get; init; }

    /// <summary>
    /// CPU core 11 power draw in watts.
    /// </summary>
    public float? Cpu11Power { get; init; }

    /// <summary>
    /// CPU core 12 power draw in watts.
    /// </summary>
    public float? Cpu12Power { get; init; }

    /// <summary>
    /// CPU core 13 power draw in watts.
    /// </summary>
    public float? Cpu13Power { get; init; }

    /// <summary>
    /// CPU core 14 power draw in watts.
    /// </summary>
    public float? Cpu14Power { get; init; }

    /// <summary>
    /// CPU core 15 power draw in watts.
    /// </summary>
    public float? Cpu15Power { get; init; }

    /// <summary>
    /// CPU core 16 power draw in watts.
    /// </summary>
    public float? Cpu16Power { get; init; }

    /// <summary>
    /// Raw MAHM shared memory entries keyed by source name.
    /// </summary>
    public Dictionary<string, float> MahmRawEntries { get; init; } = new();
}
