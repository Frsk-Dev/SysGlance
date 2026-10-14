using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SysGlance.Services;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace SysGlance.Controls;

/// <summary>
/// One metric card for desktop widgets, matching the Xeneon Edge widget: label, 270° arc gauge and value.
/// Drawn on a fixed 160x150 design canvas inside a Viewbox, so it scales cleanly to any cell size.
/// Temperatures (°C) and percentages fill the arc to 100, RPM to 3000; anything else shows the number only.
/// </summary>
public sealed partial class GaugeCard : Border
{
    private const double DesignWidth = 160;
    private const double DesignHeight = 150;
    private const double ArcCenterX = 80;
    private const double ArcCenterY = 86;
    private const double ArcRadius = 50;
    private const double StartAngle = 135;
    private const double SweepAngle = 270;

    private readonly TextBlock label = new() { HorizontalAlignment = HorizontalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly Path track = new() { StrokeThickness = 8, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
    private readonly Path fill = new() { StrokeThickness = 8, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
    private readonly TextBlock value = new() { FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Bottom };
    private readonly TextBlock unit = new() { FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(2, 0, 0, 3) };
    private readonly StackPanel readout = new() { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Path sparkFill = new() { IsHitTestVisible = false };
    private readonly Path sparkLine = new() { StrokeThickness = 1.5, StrokeLineJoin = PenLineJoin.Round, IsHitTestVisible = false };
    private string lastState = "";
    private string lastGraphState = "";

    // Mini graph area on the design canvas: along the bottom of the card, behind the gauge and value.
    private const double SparkLeft = 10;
    private const double SparkRight = 150;
    private const double SparkTop = 108;
    private const double SparkBottom = 144;

    [GeneratedRegex(@"^(-?\d+(?:\.\d+)?)\s*(.*)$")]
    private static partial Regex ValuePattern();

    public GaugeCard()
    {
        var mono = new System.Windows.Media.FontFamily("Cascadia Mono, Consolas");
        value.FontFamily = mono;
        label.FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text, Segoe UI");
        label.FontWeight = FontWeights.SemiBold;
        track.Data = ArcGeometry(1);
        readout.Children.Add(value);
        readout.Children.Add(unit);

        var canvas = new Grid { Width = DesignWidth, Height = DesignHeight };
        canvas.Children.Add(sparkFill);
        canvas.Children.Add(sparkLine);
        canvas.Children.Add(track);
        canvas.Children.Add(fill);
        canvas.Children.Add(new Border { Child = label, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(8, 10, 8, 0) });
        canvas.Children.Add(readout);

        Child = new Viewbox { Child = canvas, Stretch = Stretch.Uniform, Margin = new Thickness(4) };
        SnapsToDevicePixels = true;
    }

    /// <summary>
    /// Updates the card. Skips all work when nothing visible changed (keeps repaints to real changes).
    /// <paramref name="graph"/> is the card's history (null = graphs off).
    /// </summary>
    internal void Update(string metricKey, SystemMetrics metrics, string color, DesktopWidgetConfig look, MetricHistory.Graph? graph = null)
    {
        float? raw = MetricFormatter.GetMetricValue(metricKey, metrics);
        string formatted = MetricFormatter.FormatMetricValue(metricKey, metrics);
        UpdateGraph(metricKey, color, graph);
        string state = string.Join("|", metricKey, formatted, color, look.CardStyle, look.TextColor, look.TextScale,
            look.CardColor, look.CardOpacity, look.CornerRadius);
        if (state == lastState)
        {
            return;
        }

        lastState = state;

        string numberText = formatted;
        string unitText = "";
        double? fraction = null;
        Match match = ValuePattern().Match(formatted);

        if (raw.HasValue && match.Success)
        {
            numberText = match.Groups[1].Value;
            unitText = match.Groups[2].Value;
            fraction = unitText switch
            {
                "°C" => raw.Value / 100.0,
                "%" => raw.Value / 100.0,
                "RPM" => raw.Value / 3000.0,
                _ => null
            };
        }
        else if (!raw.HasValue)
        {
            numberText = "--";
        }

        bool gauges = look.CardStyle == DesktopWidgetConfig.StyleGauges && fraction.HasValue;
        double scale = look.TextScale / 100.0;
        Brush accent = BrushFrom(color, 1);
        Brush text = BrushFrom(look.TextColor, 1);

        Background = BrushFrom(look.CardColor, look.CardOpacity / 100.0);
        CornerRadius = new CornerRadius(Math.Max(0, look.CornerRadius - 4));

        label.Text = AppSettings.GetLabel(metricKey).ToUpperInvariant();
        label.Foreground = text;
        label.Opacity = 0.6;
        label.FontSize = 13 * scale;

        value.Text = numberText;
        value.Foreground = raw.HasValue ? accent : text;
        value.Opacity = raw.HasValue ? 1 : 0.35;
        unit.Text = unitText;
        unit.Foreground = text;
        unit.Opacity = 0.75;

        track.Visibility = gauges ? Visibility.Visible : Visibility.Collapsed;
        fill.Visibility = gauges ? Visibility.Visible : Visibility.Collapsed;
        track.Stroke = BrushFrom("#FFFFFF", 0.09);

        if (gauges)
        {
            fill.Stroke = accent;
            fill.Data = ArcGeometry(Math.Clamp(fraction!.Value, 0, 1));
            int length = numberText.Length + unitText.Length;
            value.FontSize = (length > 6 ? 22 : length > 5 ? 26 : 32) * Math.Min(scale, 1.15);
            unit.FontSize = 13 * Math.Min(scale, 1.15);
            readout.VerticalAlignment = VerticalAlignment.Top;
            readout.Margin = new Thickness(0, ArcCenterY - 18, 0, 0);
        }
        else
        {
            value.FontSize = (numberText.Length > 6 ? 34 : 44) * scale;
            unit.FontSize = 16 * scale;
            readout.VerticalAlignment = VerticalAlignment.Center;
            readout.Margin = new Thickness(0, 22, 0, 0);
        }
    }

    /// <summary>
    /// Redraws the mini graph only when a new point arrived, the metric changed or the colour changed.
    /// </summary>
    private void UpdateGraph(string metricKey, string color, MetricHistory.Graph? graph)
    {
        string state = graph is null ? "off" : string.Join("|", metricKey, color, graph.Version, graph.Values.Length);
        if (state == lastGraphState)
        {
            return;
        }

        lastGraphState = state;
        Geometry? area = null;
        Geometry? line = graph is null ? null : SparkGeometry(metricKey, graph.Values, out area);
        if (line is null)
        {
            sparkLine.Visibility = Visibility.Collapsed;
            sparkFill.Visibility = Visibility.Collapsed;
            return;
        }

        sparkLine.Data = line;
        sparkFill.Data = area;
        sparkLine.Stroke = BrushFrom(color, 0.75);
        sparkFill.Fill = BrushFrom(color, 0.12);
        sparkLine.Visibility = Visibility.Visible;
        sparkFill.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Line and filled area for the graph, or null with fewer than two readings. Percentages use 0-100, speeds and
    /// FPS start at 0, anything else zooms to its own range. Gaps (no reading) break the line.
    /// </summary>
    internal static Geometry? SparkGeometry(string metricKey, IReadOnlyList<float?> values, out Geometry? area)
    {
        area = null;
        var known = values.Where(v => v.HasValue).Select(v => (double)v!.Value).ToList();
        if (known.Count < 2)
        {
            return null;
        }

        (double lo, double hi) = GraphRange(metricKey, known.Min(), known.Max());
        double step = (SparkRight - SparkLeft) / Math.Max(1, values.Count - 1);
        var line = new PathGeometry();
        var fillArea = new PathGeometry();
        PathFigure? lineFigure = null, areaFigure = null;
        Point last = default;

        for (int i = 0; i < values.Count; i++)
        {
            if (values[i] is not float v)
            {
                CloseArea();
                lineFigure = null;
                continue;
            }

            double t = Math.Clamp((v - lo) / (hi - lo), 0, 1);
            var p = new Point(SparkLeft + i * step, SparkBottom - t * (SparkBottom - SparkTop));
            if (lineFigure is null)
            {
                lineFigure = new PathFigure { StartPoint = p, IsClosed = false, IsFilled = false };
                line.Figures.Add(lineFigure);
                areaFigure = new PathFigure { StartPoint = new Point(p.X, SparkBottom), IsClosed = true, IsFilled = true };
                areaFigure.Segments.Add(new LineSegment(p, false));
                fillArea.Figures.Add(areaFigure);
            }
            else
            {
                lineFigure.Segments.Add(new LineSegment(p, true));
                areaFigure!.Segments.Add(new LineSegment(p, false));
            }

            last = p;
        }

        CloseArea();
        line.Freeze();
        fillArea.Freeze();
        area = fillArea;
        return line;

        void CloseArea()
        {
            if (areaFigure is not null && lineFigure is not null)
            {
                areaFigure.Segments.Add(new LineSegment(new Point(last.X, SparkBottom), false));
            }

            areaFigure = null;
        }
    }

    /// <summary>
    /// Vertical range of a graph.
    /// </summary>
    internal static (double Lo, double Hi) GraphRange(string metricKey, double min, double max)
    {
        string unit = MetricFormatter.GetMaxWidthText(metricKey);
        if (unit.EndsWith('%'))
        {
            return (0, 100);
        }

        if (metricKey is "NetDown" or "NetUp" || metricKey.StartsWith("Fps", StringComparison.Ordinal))
        {
            return (0, Math.Max(max * 1.15, 1));
        }

        double pad = Math.Max((max - min) * 0.2, Math.Max(Math.Abs(max) * 0.03, 0.5));
        return (min - pad, max + pad);
    }

    private static Geometry ArcGeometry(double fraction)
    {
        double sweep = Math.Max(0.5, SweepAngle * fraction);
        Point start = PointAt(StartAngle);
        Point end = PointAt(StartAngle + sweep);
        var figure = new PathFigure { StartPoint = start, IsClosed = false, IsFilled = false };
        figure.Segments.Add(new ArcSegment(end, new Size(ArcRadius, ArcRadius), 0, sweep > 180, SweepDirection.Clockwise, true));
        var geometry = new PathGeometry([figure]);
        geometry.Freeze();
        return geometry;
    }

    private static Point PointAt(double angleDeg)
    {
        double rad = angleDeg * Math.PI / 180;
        return new Point(ArcCenterX + ArcRadius * Math.Cos(rad), ArcCenterY + ArcRadius * Math.Sin(rad));
    }

    private static readonly Dictionary<string, SolidColorBrush> BrushCache = [];

    private static Brush BrushFrom(string hex, double opacity)
    {
        string key = hex + "@" + opacity.ToString("0.00", CultureInfo.InvariantCulture);
        if (BrushCache.TryGetValue(key, out SolidColorBrush? cached))
        {
            return cached;
        }

        Color c;
        try
        {
            c = (Color)ColorConverter.ConvertFromString(hex);
        }
        catch (FormatException)
        {
            c = Colors.White;
        }

        var brush = new SolidColorBrush(Color.FromArgb((byte)Math.Round(255 * Math.Clamp(opacity, 0, 1)), c.R, c.G, c.B));
        brush.Freeze();
        BrushCache[key] = brush;
        return brush;
    }
}
