using System.Windows;
using System.Windows.Controls;
using Rect = System.Windows.Rect;
using Size = System.Windows.Size;

namespace SysGlance.Controls;

/// <summary>
/// Lays children out in the grid (columns x rows) that gives the largest cells, the same rules as the
/// Xeneon Edge widget: cells are never wider than <see cref="MaxCellAspect"/> x their height or taller than
/// <see cref="MaxCellPortrait"/> x their width, and the grid is centred.
/// </summary>
public sealed class AutoFitPanel : System.Windows.Controls.Panel
{
    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
        nameof(Gap), typeof(double), typeof(AutoFitPanel),
        new FrameworkPropertyMetadata(8.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double Gap
    {
        get => (double)GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    public double MaxCellAspect { get; set; } = 1.6;

    public double MaxCellPortrait { get; set; } = 1.3;

    private (int Cols, int Rows, double CellW, double CellH) Plan(Size available)
    {
        int count = InternalChildren.Cast<UIElement>().Count(c => c.Visibility != Visibility.Collapsed);
        if ((count == 0) || !double.IsFinite(available.Width) || !double.IsFinite(available.Height))
        {
            return (1, 1, 0, 0);
        }

        double gap = Gap;
        (int, int, double, double) best = (1, count, 0, 0);
        double bestSize = -1;
        int bestEmpty = int.MaxValue;

        for (int cols = 1; cols <= count; cols++)
        {
            int rows = (int)Math.Ceiling(count / (double)cols);
            double cellW = (available.Width - gap * (cols - 1)) / cols;
            double cellH = (available.Height - gap * (rows - 1)) / rows;
            if ((cellW <= 0) || (cellH <= 0))
            {
                continue;
            }

            cellW = Math.Min(cellW, cellH * MaxCellAspect);
            cellH = Math.Min(cellH, cellW * MaxCellPortrait);
            double size = Math.Min(cellW, cellH);
            int empty = cols * rows - count;

            if ((size > bestSize + 0.5) || ((Math.Abs(size - bestSize) <= 0.5) && (empty < bestEmpty)))
            {
                best = (cols, rows, cellW, cellH);
                bestSize = size;
                bestEmpty = empty;
            }
        }

        return best;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        (_, _, double cellW, double cellH) = Plan(availableSize);
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(Math.Max(0, cellW), Math.Max(0, cellH)));
        }

        return new Size(
            double.IsFinite(availableSize.Width) ? availableSize.Width : 0,
            double.IsFinite(availableSize.Height) ? availableSize.Height : 0);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        (int cols, int rows, double cellW, double cellH) = Plan(finalSize);
        double gap = Gap;
        double gridW = cols * cellW + (cols - 1) * gap;
        double gridH = rows * cellH + (rows - 1) * gap;
        double x0 = (finalSize.Width - gridW) / 2;
        double y0 = (finalSize.Height - gridH) / 2;

        int index = 0;
        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            int col = index % cols;
            int row = index / cols;
            child.Arrange(new Rect(x0 + col * (cellW + gap), y0 + row * (cellH + gap), Math.Max(0, cellW), Math.Max(0, cellH)));
            index++;
        }

        return finalSize;
    }
}
