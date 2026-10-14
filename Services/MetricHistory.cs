namespace SysGlance.Services;

/// <summary>
/// The last 15 minutes of every metric, one reading per second, for the mini graphs on cards.
/// A graph is <see cref="Points"/> points, each the average of one time bucket (1, 5 or 15 s for a 1, 5 or
/// 15 minute graph). Buckets line up with the clock and only finished buckets are shown, so a graph changes
/// once per bucket rather than on every reading.
/// </summary>
internal sealed class MetricHistory
{
    /// <summary>
    /// Points per graph.
    /// </summary>
    public const int Points = 60;

    /// <summary>
    /// Graph lengths offered in settings, in minutes (0 = graphs off).
    /// </summary>
    public static readonly int[] Choices = [0, 1, 5, 15];

    private const int Seconds = 15 * 60;

    public static MetricHistory Instance { get; } = new();

    private readonly object gate = new();
    private readonly Dictionary<string, float[]> values = [];
    private readonly long[] stamps = new long[Seconds];

    /// <summary>
    /// One graph: <see cref="Values"/> oldest first (null = no reading in that bucket). <see cref="Version"/>
    /// changes whenever a new bucket finishes, so callers can skip redrawing.
    /// </summary>
    public sealed record Graph(float?[] Values, long Version);

    private MetricHistory()
    {
        Array.Fill(stamps, -1);
    }

    /// <summary>
    /// Snaps a setting to the nearest offered graph length.
    /// </summary>
    public static int NormalizeMinutes(int minutes) =>
        Choices.OrderBy(c => Math.Abs(c - minutes)).First();

    /// <summary>
    /// Stores the current reading of every metric (called by the sensor poll; several calls in the same second
    /// keep the latest).
    /// </summary>
    public void Record(SystemMetrics metrics) => Record(metrics, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    internal void Record(SystemMetrics metrics, long second)
    {
        int slot = (int)(second % Seconds);
        lock (gate)
        {
            stamps[slot] = second;
            foreach (string key in AppSettings.AllMetricKeys)
            {
                if (!values.TryGetValue(key, out float[]? series))
                {
                    series = new float[Seconds];
                    values[key] = series;
                }

                series[slot] = MetricFormatter.GetMetricValue(key, metrics) ?? float.NaN;
            }
        }
    }

    /// <summary>
    /// The graph for <paramref name="key"/> over the last <paramref name="minutes"/> minutes, or null when graphs
    /// are off or the key is unknown.
    /// </summary>
    public Graph? Get(string key, int minutes) => Get(key, minutes, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    internal Graph? Get(string key, int minutes, long now)
    {
        if (minutes <= 0)
        {
            return null;
        }

        int bucket = Math.Max(1, minutes * 60 / Points);
        long end = now / bucket * bucket; // start of the unfinished bucket
        var result = new float?[Points];

        lock (gate)
        {
            if (!values.TryGetValue(key, out float[]? series))
            {
                return null;
            }

            for (int i = 0; i < Points; i++)
            {
                long from = end - (long)(Points - i) * bucket;
                double sum = 0;
                int count = 0;
                for (long s = from; s < from + bucket; s++)
                {
                    int slot = (int)(((s % Seconds) + Seconds) % Seconds);
                    if (stamps[slot] == s && !float.IsNaN(series[slot]))
                    {
                        sum += series[slot];
                        count++;
                    }
                }

                result[i] = count > 0 ? (float)(sum / count) : null;
            }
        }

        return new Graph(result, end / bucket);
    }
}
