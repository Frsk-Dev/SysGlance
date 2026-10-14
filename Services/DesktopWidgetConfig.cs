namespace SysGlance.Services;

/// <summary>
/// One floating desktop widget: its sensors, look, and where it sits. Saved in <see cref="AppSettings.DesktopWidgets"/>.
/// </summary>
public sealed class DesktopWidgetConfig
{
    /// <summary>
    /// Card styles: arc gauges, or plain numbers.
    /// </summary>
    public const string StyleGauges = "Gauges";
    public const string StyleNumbers = "Numbers";

    public const string SpacingNormal = "Normal";
    public const string SpacingCompact = "Compact";
    public const string SpacingNone = "None";

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Desktop widget";
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Metric keys shown, in display order.
    /// </summary>
    public List<string> Metrics { get; set; } = ["CpuTemp", "CpuUsage", "GpuTemp", "GpuUsage"];

    /// <summary>
    /// Per-metric colours for this widget; missing keys fall back to the main Sensors colours.
    /// </summary>
    public Dictionary<string, string> Colors { get; set; } = [];

    public string CardStyle { get; set; } = StyleGauges;

    /// <summary>
    /// Padding around the cards and gaps between them: Normal, Compact (half) or None (cards reach the edge).
    /// </summary>
    public string Spacing { get; set; } = SpacingNormal;
    public string BackgroundColor { get; set; } = "#0A0A0A";
    public int BackgroundOpacity { get; set; } = 70;
    public string CardColor { get; set; } = "#1A1A1A";
    public int CardOpacity { get; set; } = 90;
    public string TextColor { get; set; } = "#FFFFFF";
    public int TextScale { get; set; } = 100;
    public int CornerRadius { get; set; } = 14;

    /// <summary>
    /// Mini graph length on each card in minutes (1, 5 or 15); 0 = no graphs.
    /// </summary>
    public int GraphMinutes { get; set; } = 5;

    /// <summary>
    /// Metrics on this widget whose card shows no mini graph.
    /// </summary>
    public List<string> GraphsOff { get; set; } = [];
    public bool AlwaysOnTop { get; set; } = true;

    /// <summary>
    /// Position and size in device-independent pixels (virtual screen coordinates). Null = not placed yet
    /// (the window centres it on first show). Not NaN: JSON can't store NaN, and saving a new widget crashed.
    /// </summary>
    public double? Left { get; set; }
    public double? Top { get; set; }
    public double Width { get; set; } = 380;
    public double Height { get; set; } = 200;

    /// <summary>
    /// Fixes values from an older or hand-edited settings file.
    /// </summary>
    public void Normalize()
    {
        Id = string.IsNullOrWhiteSpace(Id) ? Guid.NewGuid().ToString("N") : Id;
        Name = string.IsNullOrWhiteSpace(Name) ? "Desktop widget" : Name.Trim();
        var known = new HashSet<string>(AppSettings.AllMetricKeys);
        Metrics = (Metrics ?? []).Where(known.Contains).Distinct().ToList();
        Colors ??= [];
        CardStyle = CardStyle == StyleNumbers ? StyleNumbers : StyleGauges;
        Spacing = Spacing is SpacingCompact or SpacingNone ? Spacing : SpacingNormal;
        BackgroundColor ??= "#0A0A0A";
        CardColor ??= "#1A1A1A";
        TextColor ??= "#FFFFFF";
        BackgroundOpacity = Math.Clamp(BackgroundOpacity, 0, 100);
        CardOpacity = Math.Clamp(CardOpacity, 0, 100);
        TextScale = Math.Clamp(TextScale, 50, 200);
        CornerRadius = Math.Clamp(CornerRadius, 0, 40);
        GraphMinutes = MetricHistory.NormalizeMinutes(GraphMinutes);
        GraphsOff ??= [];
        Left = Left is double left && double.IsFinite(left) ? left : null;
        Top = Top is double top && double.IsFinite(top) ? top : null;
        Width = double.IsFinite(Width) ? Math.Clamp(Width, 120, 4000) : 380;
        Height = double.IsFinite(Height) ? Math.Clamp(Height, 80, 4000) : 200;
    }
}

/// <summary>
/// A sensor selection with order and colours, edited by <see cref="SysGlance.Controls.MetricListEditor"/>.
/// Implemented by the main Sensors list and by each desktop widget.
/// </summary>
public interface IMetricSelection
{
    /// <summary>Shown metrics, in order.</summary>
    IReadOnlyList<string> ActiveKeys { get; }

    /// <summary>Metrics that can be added.</summary>
    IReadOnlyList<string> InactiveKeys { get; }

    void SetActive(string key, bool active);

    /// <summary>Moves an active metric one place up (-1) or down (+1).</summary>
    void Move(string key, int delta);

    string GetColor(string key);

    void SetColor(string key, string color);

    /// <summary>Whether the metric's card shows a mini graph.</summary>
    bool HasGraph(string key);

    void SetGraph(string key, bool on);
}

/// <summary>
/// The main Sensors list (taskbar overlay and Xeneon Edge widget).
/// </summary>
public sealed class AppMetricSelection(AppSettings settings) : IMetricSelection
{
    public IReadOnlyList<string> ActiveKeys => settings.MetricOrder.Where(settings.IsVisible).ToList();

    public IReadOnlyList<string> InactiveKeys => settings.MetricOrder.Where(k => !settings.IsVisible(k)).ToList();

    public void SetActive(string key, bool active) => settings.SetVisible(key, active);

    public void Move(string key, int delta)
    {
        int index = settings.MetricOrder.IndexOf(key);
        if (index < 0)
        {
            return;
        }

        // Swap with the nearest visible metric in that direction; hidden metrics keep their places.
        for (int i = index + delta; (i >= 0) && (i < settings.MetricOrder.Count); i += delta)
        {
            if (settings.IsVisible(settings.MetricOrder[i]))
            {
                (settings.MetricOrder[index], settings.MetricOrder[i]) = (settings.MetricOrder[i], settings.MetricOrder[index]);
                return;
            }
        }
    }

    public string GetColor(string key) => settings.GetColor(key);

    public void SetColor(string key, string color) => settings.SetColor(key, color);

    public bool HasGraph(string key) => settings.HasGraph(key);

    public void SetGraph(string key, bool on) => settings.SetGraph(key, on);
}

/// <summary>
/// One desktop widget's own sensor picks. Colours default to the main Sensors colours.
/// </summary>
public sealed class DesktopWidgetMetricSelection(DesktopWidgetConfig widget, AppSettings settings) : IMetricSelection
{
    public IReadOnlyList<string> ActiveKeys => widget.Metrics;

    public IReadOnlyList<string> InactiveKeys => AppSettings.AllMetricKeys.Where(k => !widget.Metrics.Contains(k)).ToList();

    public void SetActive(string key, bool active)
    {
        if (active && !widget.Metrics.Contains(key))
        {
            widget.Metrics.Add(key);
        }
        else if (!active)
        {
            widget.Metrics.Remove(key);
        }
    }

    public void Move(string key, int delta)
    {
        int index = widget.Metrics.IndexOf(key);
        int target = index + delta;
        if ((index >= 0) && (target >= 0) && (target < widget.Metrics.Count))
        {
            (widget.Metrics[index], widget.Metrics[target]) = (widget.Metrics[target], widget.Metrics[index]);
        }
    }

    public string GetColor(string key) => widget.Colors.TryGetValue(key, out string? color) ? color : settings.GetColor(key);

    public void SetColor(string key, string color) => widget.Colors[key] = color;

    public bool HasGraph(string key) => !widget.GraphsOff.Contains(key);

    public void SetGraph(string key, bool on)
    {
        widget.GraphsOff.Remove(key);
        if (!on)
        {
            widget.GraphsOff.Add(key);
        }
    }
}
