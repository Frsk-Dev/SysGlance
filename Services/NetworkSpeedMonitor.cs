using System.Diagnostics;
using System.Net.NetworkInformation;

namespace SysGlance.Services;

/// <summary>
/// Download and upload speed across the PC's real network adapters (Ethernet and Wi-Fi), in megabits per second.
/// Virtual adapters (Hyper-V, VMs, VPN tunnels) are skipped: VPN traffic also passes through the real adapter,
/// so counting both would double it.
/// </summary>
internal sealed class NetworkSpeedMonitor
{
    private static readonly TimeSpan AdapterRefresh = TimeSpan.FromSeconds(30);

    private static readonly string[] VirtualHints =
    [
        "virtual", "hyper-v", "vmware", "virtualbox", "vethernet", "tap-", "tap ", "wintun", "wireguard",
        "tailscale", "zerotier", "openvpn", "npcap", "loopback", "pseudo", "miniport", "bluetooth"
    ];

    public static NetworkSpeedMonitor Instance { get; } = new();

    private readonly object gate = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private NetworkInterface[] adapters = [];
    private TimeSpan? adaptersReadAt;
    private long lastReceived = -1;
    private long lastSent = -1;
    private TimeSpan lastAt;
    private float? down;
    private float? up;

    private NetworkSpeedMonitor()
    {
    }

    /// <summary>
    /// Reads the byte counters and returns the speed since the previous call (null until there are two readings,
    /// or when no adapter is up). Calls closer than 400 ms apart return the previous result.
    /// </summary>
    public (float? Down, float? Up) Sample()
    {
        lock (gate)
        {
            TimeSpan now = clock.Elapsed;
            if (lastReceived >= 0 && (now - lastAt).TotalMilliseconds < 400)
            {
                return (down, up);
            }

            if (adaptersReadAt is null || now - adaptersReadAt.Value > AdapterRefresh)
            {
                adapters = ReadAdapters();
                adaptersReadAt = now;
            }

            long received = 0, sent = 0;
            bool any = false;
            foreach (NetworkInterface adapter in adapters)
            {
                try
                {
                    IPInterfaceStatistics stats = adapter.GetIPStatistics();
                    received += stats.BytesReceived;
                    sent += stats.BytesSent;
                    any = true;
                }
                catch (NetworkInformationException)
                {
                    // Adapter went away; the next refresh drops it.
                }
            }

            if (!any)
            {
                lastReceived = -1;
                down = up = null;
                return (null, null);
            }

            double seconds = (now - lastAt).TotalSeconds;
            if (lastReceived >= 0 && seconds > 0 && received >= lastReceived && sent >= lastSent)
            {
                down = (float)((received - lastReceived) * 8 / seconds / 1_000_000);
                up = (float)((sent - lastSent) * 8 / seconds / 1_000_000);
            }

            // A counter going backwards (adapter reset or list changed) restarts the measurement.
            lastReceived = received;
            lastSent = sent;
            lastAt = now;
            return (down, up);
        }
    }

    private static NetworkInterface[] ReadAdapters()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces().Where(IsRealAdapter).ToArray();
        }
        catch (NetworkInformationException)
        {
            return [];
        }
    }

    private static bool IsRealAdapter(NetworkInterface adapter)
    {
        if (adapter.OperationalStatus != OperationalStatus.Up)
        {
            return false;
        }

        bool physicalType = adapter.NetworkInterfaceType is NetworkInterfaceType.Ethernet
            or NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.GigabitEthernet
            or NetworkInterfaceType.FastEthernetT or NetworkInterfaceType.FastEthernetFx
            or NetworkInterfaceType.Ethernet3Megabit;
        if (!physicalType)
        {
            return false;
        }

        string text = (adapter.Name + " " + adapter.Description).ToLowerInvariant();
        return !VirtualHints.Any(text.Contains);
    }
}
