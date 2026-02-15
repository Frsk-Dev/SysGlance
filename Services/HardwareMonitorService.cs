using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using LibreHardwareMonitor.Hardware;

namespace SysGlance.Services;

/// <summary>
/// Singleton service that reads hardware sensors via LHM and MAHM shared memory.
/// </summary>
internal sealed class HardwareMonitorService : IDisposable
{
    private static readonly Lazy<HardwareMonitorService> LazyInstance = new(
        () => new HardwareMonitorService());

    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static HardwareMonitorService Instance => LazyInstance.Value;

    private readonly Computer computer;
    private readonly UpdateVisitor updateVisitor;
    private readonly object lockObject = new();
    private bool isOpen;

    /// <summary>
    /// Opens the hardware monitor with CPU, GPU, and memory sensors.
    /// </summary>
    private HardwareMonitorService()
    {
        updateVisitor = new UpdateVisitor();

        computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true
        };

        try
        {
            computer.Open();
            isOpen = true;
        }
        catch (Exception)
        {
            // Hardware access failed. Sensors will return null values.
            isOpen = false;
        }
    }

    /// <summary>
    /// Polls all sensors and returns a metrics snapshot. Thread-safe.
    /// </summary>
    public SystemMetrics GetCurrentMetrics()
    {
        float? cpuTemp = null;
        float? cpuUsage = null;
        float? cpuClock = null;
        float? cpuPower = null;
        float? gpuTemp = null;
        float? gpuUsage = null;
        float? gpuClock = null;
        float? gpuMemClock = null;
        float? gpuVram = null;
        float? gpuPower = null;
        float? gpuVoltage = null;
        float? gpuFan = null;
        float? gpuHotSpot = null;
        float? ramUsedGb = null;
        float? ramTotalGb = null;

        // Primary source: LibreHardwareMonitor.
        if (isOpen)
        {
            lock (lockObject)
            {
                try
                {
                    computer.Accept(updateVisitor);

                    foreach (IHardware hardware in computer.Hardware)
                    {
                        switch (hardware.HardwareType)
                        {
                            case HardwareType.Cpu:
                                ReadCpuSensors(hardware,
                                    ref cpuTemp, ref cpuUsage,
                                    ref cpuClock, ref cpuPower);
                                break;

                            case HardwareType.GpuNvidia:
                            case HardwareType.GpuAmd:
                            case HardwareType.GpuIntel:
                                ReadGpuSensors(hardware,
                                    ref gpuTemp, ref gpuUsage,
                                    ref gpuClock, ref gpuMemClock,
                                    ref gpuVram, ref gpuPower,
                                    ref gpuVoltage, ref gpuFan,
                                    ref gpuHotSpot);
                                break;

                            case HardwareType.Memory:
                                if (!hardware.Name.Contains("Virtual"))
                                {
                                    ReadMemorySensors(hardware, ref ramUsedGb, ref ramTotalGb);
                                }
                                break;

                            case HardwareType.Motherboard:
                                if (cpuTemp is null or 0)
                                {
                                    ReadMotherboardCpuTemp(hardware, ref cpuTemp);
                                }
                                break;
                        }
                    }
                }
                catch (Exception)
                {
                    // Hardware access failed mid-read. Return whatever we have so far.
                }
            }
        }

        // Supplemental source: MAHM shared memory.
        Dictionary<string, float> mahm = ReadAllMahmEntries();

        if (mahm.Count > 0)
        {
            if (cpuTemp is null or 0)
            {
                cpuTemp = FindMahmValue(mahm, "CPU temperature", 5, 120);
            }

            // Fill any gaps where LHM returned null or zero.
            cpuUsage ??= FindMahmValue(mahm, "CPU usage", 0, 100);
            gpuTemp ??= FindMahmValue(mahm, "GPU", "temperature", 5, 130);
            gpuUsage ??= FindMahmValue(mahm, "GPU", "usage", 0, 100);
            cpuClock ??= FindMahmValue(mahm, "CPU clock", 100, 10000);
            gpuClock ??= FindMahmValue(mahm, "Core clock", 100, 5000);
            gpuMemClock ??= FindMahmValue(mahm, "Memory clock", 100, 15000);
            gpuVram ??= FindMahmValue(mahm, "Memory usage", 0, 100000);
            gpuHotSpot ??= FindMahmValue(mahm, "hot spot", 5, 130);
            gpuVoltage ??= FindMahmValue(mahm, "voltage", 0, 2000);

            if (cpuPower is null or 0)
            {
                cpuPower = FindMahmValue(mahm, "CPU power", 0, 500);
            }

            if (gpuPower is null or 0)
            {
                gpuPower = FindMahmValue(mahm, "GPU power", 0, 1000);
            }

            if (gpuFan is null or 0)
            {
                gpuFan = FindMahmValue(mahm, "fan tachometer", 0, 20000);
            }
        }

        // GPU fan and power percentages are only available from Afterburner.
        float? gpuFanPercent = FindMahmValue(mahm, "GPU", "fan speed", 0, 100);
        float? gpuPowerPercent = FindMahmValue(mahm, "GPU", "power percent", 0, 100);

        // GPU secondary fan from Afterburner.
        float? gpuFan2Percent = FindMahmValue(mahm, "fan speed 2", 0, 100);
        float? gpuFan2Rpm = FindMahmValue(mahm, "fan tachometer 2", 0, 20000);

        // GPU utilization breakdowns from Afterburner.
        float? gpuFbUsage = FindMahmValue(mahm, "FB usage", 0, 100);
        float? gpuVidUsage = FindMahmValue(mahm, "VID usage", 0, 100);
        float? gpuBusUsage = FindMahmValue(mahm, "BUS usage", 0, 100);

        // GPU throttling limit flags from Afterburner.
        float? gpuTempLimit = FindMahmValue(mahm, "temp limit", -1, 2);
        float? gpuPowerLimit = FindMahmValue(mahm, "power limit", -1, 2);
        float? gpuVoltageLimit = FindMahmValue(mahm, "voltage limit", -1, 2);
        float? gpuNoLoadLimit = FindMahmValue(mahm, "no load limit", -1, 2);

        // System commit charge from Afterburner.
        float? commitCharge = FindMahmValue(mahm, "Commit charge", 0, 200000);

        // FPS and Frametime are only available from Afterburner.
        // Use exact/normalized matching here so "Framerate" doesn't get shadowed
        // by "Framerate Min/Avg/Max" substring matches.
        float? fps = FindMahmValueExact(mahm, "Framerate", 0, 10000);
        float? frametime = FindMahmValueExact(mahm, "Frametime", 0, 1000);
        float? fpsMin = FindMahmValueExact(mahm, "Framerate Min", 0, 10000);
        float? fpsAvg = FindMahmValueExact(mahm, "Framerate Avg", 0, 10000);
        float? fpsMax = FindMahmValueExact(mahm, "Framerate Max", 0, 10000);
        float? fps1Low = FindMahmValueExact(mahm, "Framerate 1% Low", 0, 10000);
        float? fps01Low = FindMahmValueExact(mahm, "Framerate 0.1% Low", 0, 10000);

        // GPU1 sensors from Afterburner.
        float? gpu1Temp = FindMahmValue(mahm, "GPU1 temperature", 5, 130);
        float? gpu1Usage = FindMahmValue(mahm, "GPU1 usage", 0, 100);
        float? gpu1Mem = FindMahmValue(mahm, "GPU1 memory usage", 0, 100000);
        float? gpu1MemProcess = FindMahmValue(mahm, "GPU1 memory usage \\ process", 0, 100000);
        float? gpu2MemProcess = FindMahmValue(mahm, "GPU2 memory usage \\ process", 0, 100000);
        float? gpu1Clock = FindMahmValue(mahm, "GPU1 core clock", 100, 5000);
        float? gpu1MemClock = FindMahmValue(mahm, "GPU1 memory clock", 100, 15000);
        float? gpu1Power = FindMahmValue(mahm, "GPU1 power", 0, 1000);
        float? gpu1Fan = FindMahmValue(mahm, "GPU1 fan tachometer", 0, 20000);

        // RAM process usage from Afterburner.
        float? ramProcess = FindMahmValue(mahm, "RAM usage \\ process", 0, 200000);

        // Per-core CPU temperatures from Afterburner.
        float? cpu1Temp = FindMahmValue(mahm, "CPU1 temperature", 5, 120);
        float? cpu2Temp = FindMahmValue(mahm, "CPU2 temperature", 5, 120);
        float? cpu3Temp = FindMahmValue(mahm, "CPU3 temperature", 5, 120);
        float? cpu4Temp = FindMahmValue(mahm, "CPU4 temperature", 5, 120);
        float? cpu5Temp = FindMahmValue(mahm, "CPU5 temperature", 5, 120);
        float? cpu6Temp = FindMahmValue(mahm, "CPU6 temperature", 5, 120);
        float? cpu7Temp = FindMahmValue(mahm, "CPU7 temperature", 5, 120);
        float? cpu8Temp = FindMahmValue(mahm, "CPU8 temperature", 5, 120);
        float? cpu9Temp = FindMahmValue(mahm, "CPU9 temperature", 5, 120);
        float? cpu10Temp = FindMahmValue(mahm, "CPU10 temperature", 5, 120);
        float? cpu11Temp = FindMahmValue(mahm, "CPU11 temperature", 5, 120);
        float? cpu12Temp = FindMahmValue(mahm, "CPU12 temperature", 5, 120);
        float? cpu13Temp = FindMahmValue(mahm, "CPU13 temperature", 5, 120);
        float? cpu14Temp = FindMahmValue(mahm, "CPU14 temperature", 5, 120);
        float? cpu15Temp = FindMahmValue(mahm, "CPU15 temperature", 5, 120);
        float? cpu16Temp = FindMahmValue(mahm, "CPU16 temperature", 5, 120);

        // Per-core CPU usage from Afterburner.
        float? cpu1Usage = FindMahmValue(mahm, "CPU1 usage", 0, 100);
        float? cpu2Usage = FindMahmValue(mahm, "CPU2 usage", 0, 100);
        float? cpu3Usage = FindMahmValue(mahm, "CPU3 usage", 0, 100);
        float? cpu4Usage = FindMahmValue(mahm, "CPU4 usage", 0, 100);
        float? cpu5Usage = FindMahmValue(mahm, "CPU5 usage", 0, 100);
        float? cpu6Usage = FindMahmValue(mahm, "CPU6 usage", 0, 100);
        float? cpu7Usage = FindMahmValue(mahm, "CPU7 usage", 0, 100);
        float? cpu8Usage = FindMahmValue(mahm, "CPU8 usage", 0, 100);
        float? cpu9Usage = FindMahmValue(mahm, "CPU9 usage", 0, 100);
        float? cpu10Usage = FindMahmValue(mahm, "CPU10 usage", 0, 100);
        float? cpu11Usage = FindMahmValue(mahm, "CPU11 usage", 0, 100);
        float? cpu12Usage = FindMahmValue(mahm, "CPU12 usage", 0, 100);
        float? cpu13Usage = FindMahmValue(mahm, "CPU13 usage", 0, 100);
        float? cpu14Usage = FindMahmValue(mahm, "CPU14 usage", 0, 100);
        float? cpu15Usage = FindMahmValue(mahm, "CPU15 usage", 0, 100);
        float? cpu16Usage = FindMahmValue(mahm, "CPU16 usage", 0, 100);

        // Per-core CPU clocks from Afterburner.
        float? cpu1Clock = FindMahmValue(mahm, "CPU1 clock", 100, 10000);
        float? cpu2Clock = FindMahmValue(mahm, "CPU2 clock", 100, 10000);
        float? cpu3Clock = FindMahmValue(mahm, "CPU3 clock", 100, 10000);
        float? cpu4Clock = FindMahmValue(mahm, "CPU4 clock", 100, 10000);
        float? cpu5Clock = FindMahmValue(mahm, "CPU5 clock", 100, 10000);
        float? cpu6Clock = FindMahmValue(mahm, "CPU6 clock", 100, 10000);
        float? cpu7Clock = FindMahmValue(mahm, "CPU7 clock", 100, 10000);
        float? cpu8Clock = FindMahmValue(mahm, "CPU8 clock", 100, 10000);
        float? cpu9Clock = FindMahmValue(mahm, "CPU9 clock", 100, 10000);
        float? cpu10Clock = FindMahmValue(mahm, "CPU10 clock", 100, 10000);
        float? cpu11Clock = FindMahmValue(mahm, "CPU11 clock", 100, 10000);
        float? cpu12Clock = FindMahmValue(mahm, "CPU12 clock", 100, 10000);
        float? cpu13Clock = FindMahmValue(mahm, "CPU13 clock", 100, 10000);
        float? cpu14Clock = FindMahmValue(mahm, "CPU14 clock", 100, 10000);
        float? cpu15Clock = FindMahmValue(mahm, "CPU15 clock", 100, 10000);
        float? cpu16Clock = FindMahmValue(mahm, "CPU16 clock", 100, 10000);

        // Per-core CPU power from Afterburner.
        float? cpu1Power = FindMahmValue(mahm, "CPU1 power", 0, 500);
        float? cpu2Power = FindMahmValue(mahm, "CPU2 power", 0, 500);
        float? cpu3Power = FindMahmValue(mahm, "CPU3 power", 0, 500);
        float? cpu4Power = FindMahmValue(mahm, "CPU4 power", 0, 500);
        float? cpu5Power = FindMahmValue(mahm, "CPU5 power", 0, 500);
        float? cpu6Power = FindMahmValue(mahm, "CPU6 power", 0, 500);
        float? cpu7Power = FindMahmValue(mahm, "CPU7 power", 0, 500);
        float? cpu8Power = FindMahmValue(mahm, "CPU8 power", 0, 500);
        float? cpu9Power = FindMahmValue(mahm, "CPU9 power", 0, 500);
        float? cpu10Power = FindMahmValue(mahm, "CPU10 power", 0, 500);
        float? cpu11Power = FindMahmValue(mahm, "CPU11 power", 0, 500);
        float? cpu12Power = FindMahmValue(mahm, "CPU12 power", 0, 500);
        float? cpu13Power = FindMahmValue(mahm, "CPU13 power", 0, 500);
        float? cpu14Power = FindMahmValue(mahm, "CPU14 power", 0, 500);
        float? cpu15Power = FindMahmValue(mahm, "CPU15 power", 0, 500);
        float? cpu16Power = FindMahmValue(mahm, "CPU16 power", 0, 500);

        return new SystemMetrics
        {
            CpuTemperature = cpuTemp,
            CpuUsage = cpuUsage,
            CpuClock = cpuClock,
            CpuPower = cpuPower,
            GpuTemperature = gpuTemp,
            GpuUsage = gpuUsage,
            GpuClock = gpuClock,
            GpuMemClock = gpuMemClock,
            GpuVram = gpuVram,
            GpuPower = gpuPower,
            GpuPowerPercent = gpuPowerPercent,
            GpuVoltage = gpuVoltage,
            GpuFan = gpuFan,
            GpuFanPercent = gpuFanPercent,
            GpuFan2Percent = gpuFan2Percent,
            GpuFan2Rpm = gpuFan2Rpm,
            GpuHotSpot = gpuHotSpot,
            GpuFbUsage = gpuFbUsage,
            GpuVidUsage = gpuVidUsage,
            GpuBusUsage = gpuBusUsage,
            GpuTempLimit = gpuTempLimit,
            GpuPowerLimit = gpuPowerLimit,
            GpuVoltageLimit = gpuVoltageLimit,
            GpuNoLoadLimit = gpuNoLoadLimit,
            RamUsedGb = ramUsedGb,
            RamTotalGb = ramTotalGb,
            CommitCharge = commitCharge,
            Fps = fps,
            FpsMin = fpsMin,
            FpsAvg = fpsAvg,
            FpsMax = fpsMax,
            Fps1Low = fps1Low,
            Fps01Low = fps01Low,
            Frametime = frametime,
            Gpu1Temperature = gpu1Temp,
            Gpu1Usage = gpu1Usage,
            Gpu1Mem = gpu1Mem,
            Gpu1MemProcess = gpu1MemProcess,
            Gpu2MemProcess = gpu2MemProcess,
            Gpu1Clock = gpu1Clock,
            Gpu1MemClock = gpu1MemClock,
            Gpu1Power = gpu1Power,
            Gpu1Fan = gpu1Fan,
            RamProcess = ramProcess,
            Cpu1Temp = cpu1Temp,
            Cpu2Temp = cpu2Temp,
            Cpu3Temp = cpu3Temp,
            Cpu4Temp = cpu4Temp,
            Cpu5Temp = cpu5Temp,
            Cpu6Temp = cpu6Temp,
            Cpu7Temp = cpu7Temp,
            Cpu8Temp = cpu8Temp,
            Cpu9Temp = cpu9Temp,
            Cpu10Temp = cpu10Temp,
            Cpu11Temp = cpu11Temp,
            Cpu12Temp = cpu12Temp,
            Cpu13Temp = cpu13Temp,
            Cpu14Temp = cpu14Temp,
            Cpu15Temp = cpu15Temp,
            Cpu16Temp = cpu16Temp,
            Cpu1Usage = cpu1Usage,
            Cpu2Usage = cpu2Usage,
            Cpu3Usage = cpu3Usage,
            Cpu4Usage = cpu4Usage,
            Cpu5Usage = cpu5Usage,
            Cpu6Usage = cpu6Usage,
            Cpu7Usage = cpu7Usage,
            Cpu8Usage = cpu8Usage,
            Cpu9Usage = cpu9Usage,
            Cpu10Usage = cpu10Usage,
            Cpu11Usage = cpu11Usage,
            Cpu12Usage = cpu12Usage,
            Cpu13Usage = cpu13Usage,
            Cpu14Usage = cpu14Usage,
            Cpu15Usage = cpu15Usage,
            Cpu16Usage = cpu16Usage,
            Cpu1Clock = cpu1Clock,
            Cpu2Clock = cpu2Clock,
            Cpu3Clock = cpu3Clock,
            Cpu4Clock = cpu4Clock,
            Cpu5Clock = cpu5Clock,
            Cpu6Clock = cpu6Clock,
            Cpu7Clock = cpu7Clock,
            Cpu8Clock = cpu8Clock,
            Cpu9Clock = cpu9Clock,
            Cpu10Clock = cpu10Clock,
            Cpu11Clock = cpu11Clock,
            Cpu12Clock = cpu12Clock,
            Cpu13Clock = cpu13Clock,
            Cpu14Clock = cpu14Clock,
            Cpu15Clock = cpu15Clock,
            Cpu16Clock = cpu16Clock,
            Cpu1Power = cpu1Power,
            Cpu2Power = cpu2Power,
            Cpu3Power = cpu3Power,
            Cpu4Power = cpu4Power,
            Cpu5Power = cpu5Power,
            Cpu6Power = cpu6Power,
            Cpu7Power = cpu7Power,
            Cpu8Power = cpu8Power,
            Cpu9Power = cpu9Power,
            Cpu10Power = cpu10Power,
            Cpu11Power = cpu11Power,
            Cpu12Power = cpu12Power,
            Cpu13Power = cpu13Power,
            Cpu14Power = cpu14Power,
            Cpu15Power = cpu15Power,
            Cpu16Power = cpu16Power
        };
    }

    /// <summary>
    /// Reads CPU sensors from the hardware node and its sub-hardware.
    /// </summary>
    private static void ReadCpuSensors(
        IHardware hardware,
        ref float? cpuTemp,
        ref float? cpuUsage,
        ref float? cpuClock,
        ref float? cpuPower)
    {
        ReadCpuSensorsFromHardware(hardware, ref cpuTemp, ref cpuUsage, ref cpuClock, ref cpuPower);

        foreach (IHardware subHardware in hardware.SubHardware)
        {
            ReadCpuSensorsFromHardware(subHardware, ref cpuTemp, ref cpuUsage, ref cpuClock, ref cpuPower);
        }
    }

    /// <summary>
    /// Reads CPU sensors from a single hardware node, preferring "Package" readings.
    /// </summary>
    private static void ReadCpuSensorsFromHardware(
        IHardware hardware,
        ref float? cpuTemp,
        ref float? cpuUsage,
        ref float? cpuClock,
        ref float? cpuPower)
    {
        foreach (ISensor sensor in hardware.Sensors)
        {
            if (!sensor.Value.HasValue)
            {
                continue;
            }

            switch (sensor.SensorType)
            {
                case SensorType.Temperature
                    when sensor.Name.Contains("Package") || (cpuTemp == null):
                    cpuTemp = sensor.Value;
                    break;

                case SensorType.Load
                    when sensor.Name.Contains("Total"):
                    cpuUsage = sensor.Value;
                    break;

                case SensorType.Clock
                    when cpuClock == null && sensor.Name.Contains("Core"):
                    cpuClock = sensor.Value;
                    break;

                case SensorType.Power
                    when sensor.Name.Contains("Package") || (cpuPower == null):
                    cpuPower = sensor.Value;
                    break;
            }
        }
    }

    /// <summary>
    /// Reads GPU sensors from a hardware node.
    /// </summary>
    private static void ReadGpuSensors(
        IHardware hardware,
        ref float? gpuTemp,
        ref float? gpuUsage,
        ref float? gpuClock,
        ref float? gpuMemClock,
        ref float? gpuVram,
        ref float? gpuPower,
        ref float? gpuVoltage,
        ref float? gpuFan,
        ref float? gpuHotSpot)
    {
        foreach (ISensor sensor in hardware.Sensors)
        {
            if (!sensor.Value.HasValue)
            {
                continue;
            }

            switch (sensor.SensorType)
            {
                case SensorType.Temperature
                    when sensor.Name.Contains("Hot Spot"):
                    gpuHotSpot = sensor.Value;
                    break;

                case SensorType.Temperature
                    when sensor.Name.Contains("Core") || (gpuTemp == null):
                    gpuTemp = sensor.Value;
                    break;

                case SensorType.Load
                    when sensor.Name.Contains("Core"):
                    gpuUsage = sensor.Value;
                    break;

                case SensorType.Clock
                    when sensor.Name.Contains("Core"):
                    gpuClock = sensor.Value;
                    break;

                case SensorType.Clock
                    when sensor.Name.Contains("Memory"):
                    gpuMemClock = sensor.Value;
                    break;

                case SensorType.SmallData
                    when sensor.Name.Contains("Memory Used"):
                    gpuVram = sensor.Value;
                    break;

                case SensorType.Power
                    when gpuPower == null:
                    gpuPower = sensor.Value;
                    break;

                case SensorType.Voltage
                    when gpuVoltage == null:
                    // Convert volts to millivolts.
                    gpuVoltage = sensor.Value * 1000f;
                    break;

                case SensorType.Fan
                    when gpuFan == null:
                    gpuFan = sensor.Value;
                    break;
            }
        }
    }

    /// <summary>
    /// Reads CPU temperature from motherboard super I/O chip as a fallback.
    /// </summary>
    private static void ReadMotherboardCpuTemp(
        IHardware hardware,
        ref float? cpuTemp)
    {
        foreach (IHardware subHardware in hardware.SubHardware)
        {
            foreach (ISensor sensor in subHardware.Sensors)
            {
                if ((sensor.SensorType == SensorType.Temperature)
                    && (sensor.Value.HasValue)
                    && (sensor.Value > 0)
                    && sensor.Name.Contains("CPU"))
                {
                    cpuTemp = sensor.Value;
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Reads physical memory usage and total capacity from hardware sensors.
    /// </summary>
    private static void ReadMemorySensors(
        IHardware hardware,
        ref float? ramUsedGb,
        ref float? ramTotalGb)
    {
        float? ramAvailable = null;

        foreach (ISensor sensor in hardware.Sensors)
        {
            // Skip virtual memory sensors — only read physical RAM.
            if (sensor.Name.Contains("Virtual"))
            {
                continue;
            }

            if ((sensor.SensorType == SensorType.Data) && (sensor.Value.HasValue))
            {
                if (sensor.Name.Contains("Used"))
                {
                    ramUsedGb = sensor.Value;
                }
                else if (sensor.Name.Contains("Available"))
                {
                    ramAvailable = sensor.Value;
                }
            }
        }

        // Total RAM = Used + Available.
        if ((ramUsedGb.HasValue) && (ramAvailable.HasValue))
        {
            ramTotalGb = ramUsedGb.Value + ramAvailable.Value;
        }
    }

    private const uint MahmSignature = 0x4D41484D;
    private const int MaxPath = 260;

    /// <summary>
    /// Reads all MAHM shared memory entries into a name-to-value dictionary.
    /// </summary>
    private static Dictionary<string, float> ReadAllMahmEntries()
    {
        var result = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using MemoryMappedFile mmf = MemoryMappedFile.OpenExisting("MAHMSharedMemory",
                MemoryMappedFileRights.Read);
            using MemoryMappedViewAccessor accessor = mmf.CreateViewAccessor(
                0, 0, MemoryMappedFileAccess.Read);

            uint signature = accessor.ReadUInt32(0);

            if (signature != MahmSignature)
            {
                return result;
            }

            uint entryCount = accessor.ReadUInt32(12);
            uint entrySize = accessor.ReadUInt32(16);
            uint headerSize = accessor.ReadUInt32(8);

            var nameBytes = new byte[MaxPath];

            for (uint i = 0; i < entryCount; i++)
            {
                long entryOffset = headerSize + (i * entrySize);

                accessor.ReadArray(entryOffset, nameBytes, 0, MaxPath);
                string srcName = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');

                if (string.IsNullOrWhiteSpace(srcName))
                {
                    continue;
                }

                long dataOffset = entryOffset + (5L * MaxPath);
                float value = accessor.ReadSingle(dataOffset);

                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    continue;
                }

                result[srcName] = value;
            }
        }
        catch (FileNotFoundException)
        {
            // MSI Afterburner is not running.
        }

        return result;
    }

    /// <summary>
    /// Finds a MAHM entry matching the pattern within the valid range.
    /// </summary>
    private static float? FindMahmValue(
        Dictionary<string, float> entries,
        string pattern,
        float minValid,
        float maxValid)
    {
        float? bestValue = null;
        int bestScore = int.MaxValue;

        foreach (KeyValuePair<string, float> entry in entries)
        {
            if (entry.Key.Contains(pattern, StringComparison.OrdinalIgnoreCase)
                && IsValidMahmValue(entry.Value, minValid, maxValid))
            {
                // Prefer exact matches, then shortest candidate name.
                int score = entry.Key.Equals(pattern, StringComparison.OrdinalIgnoreCase)
                    ? 0
                    : entry.Key.Length;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestValue = entry.Value;
                }
            }
        }

        return bestValue;
    }

    /// <summary>
    /// Finds a MAHM entry matching both patterns within the valid range.
    /// </summary>
    private static float? FindMahmValue(
        Dictionary<string, float> entries,
        string pattern1,
        string pattern2,
        float minValid,
        float maxValid)
    {
        float? bestValue = null;
        int bestScore = int.MaxValue;

        foreach (KeyValuePair<string, float> entry in entries)
        {
            if (entry.Key.Contains(pattern1, StringComparison.OrdinalIgnoreCase)
                && entry.Key.Contains(pattern2, StringComparison.OrdinalIgnoreCase)
                && IsValidMahmValue(entry.Value, minValid, maxValid))
            {
                int score = entry.Key.Length;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestValue = entry.Value;
                }
            }
        }

        return bestValue;
    }

    /// <summary>
    /// Finds a MAHM entry by exact key match (with normalized fallback) within the valid range.
    /// </summary>
    private static float? FindMahmValueExact(
        Dictionary<string, float> entries,
        string key,
        float minValid,
        float maxValid)
    {
        string normalizedKey = NormalizeMahmName(key);

        foreach (KeyValuePair<string, float> entry in entries)
        {
            if (!IsValidMahmValue(entry.Value, minValid, maxValid))
            {
                continue;
            }

            if (entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Value;
            }

            if (NormalizeMahmName(entry.Key).Equals(normalizedKey, StringComparison.Ordinal))
            {
                return entry.Value;
            }
        }

        return null;
    }

    private static bool IsValidMahmValue(float value, float minValid, float maxValid)
    {
        return !float.IsNaN(value)
            && !float.IsInfinity(value)
            && (value >= minValid)
            && (value <= maxValid);
    }

    private static string NormalizeMahmName(string name)
    {
        var builder = new StringBuilder(name.Length);

        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Closes the hardware monitor and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (isOpen)
        {
            computer.Close();
            isOpen = false;
        }
    }
}

/// <summary>
/// Visitor that traverses the hardware tree and updates sensors.
/// </summary>
internal sealed class UpdateVisitor : IVisitor
{
    public void VisitComputer(IComputer computer)
    {
        computer.Traverse(this);
    }

    public void VisitHardware(IHardware hardware)
    {
        hardware.Update();

        foreach (IHardware subHardware in hardware.SubHardware)
        {
            subHardware.Accept(this);
        }
    }

    public void VisitSensor(ISensor sensor)
    {
    }

    public void VisitParameter(IParameter parameter)
    {
    }
}
