namespace SysGlance.Services;

/// <summary>
/// Snap and align for desktop widgets while they are moved or resized in edit mode. Works on window
/// rectangles in screen pixels, as Windows hands them over in WM_MOVING / WM_SIZING.
///
/// Targets: the monitor's work area edges (flush, or with a small margin) and centre lines, and every other
/// widget on the same monitor: beside it (touching, or with a gap) when they overlap on the other axis,
/// and lined up with its edges or centre. Resizing can also match another widget's width or height.
/// The nearest target within the threshold wins, per axis.
/// </summary>
internal static class WidgetSnapper
{
    internal readonly record struct Box(int Left, int Top, int Right, int Bottom)
    {
        public int Width => Right - Left;
        public int Height => Bottom - Top;
        public int CenterX => Left + Width / 2;
        public int CenterY => Top + Height / 2;

        public bool Intersects(Box other) =>
            Left < other.Right && other.Left < Right && Top < other.Bottom && other.Top < Bottom;
    }

    /// <summary>
    /// Sizes in pixels at the widget's DPI.
    /// </summary>
    internal readonly record struct Spacing(int Threshold, int Gap, int Margin, int MinWidth, int MinHeight);

    /// <summary>
    /// The snapped rectangle and the indexes (into the "others" list) of the widgets it lines up with.
    /// </summary>
    internal sealed record Result(Box Rect, IReadOnlyList<int> Partners);

    [Flags]
    internal enum Edges
    {
        None = 0,
        Left = 1,
        Top = 2,
        Right = 4,
        Bottom = 8
    }

    /// <summary>
    /// Which edges a WM_SIZING wParam (WMSZ_*) moves.
    /// </summary>
    internal static Edges EdgesFromSizing(int wmsz) => wmsz switch
    {
        1 => Edges.Left,
        2 => Edges.Right,
        3 => Edges.Top,
        4 => Edges.Top | Edges.Left,
        5 => Edges.Top | Edges.Right,
        6 => Edges.Bottom,
        7 => Edges.Bottom | Edges.Left,
        8 => Edges.Bottom | Edges.Right,
        _ => Edges.None
    };

    private readonly record struct Candidate(int Delta, int Partner);

    /// <summary>
    /// Snaps a widget being dragged: shifts it, keeping its size.
    /// </summary>
    public static Result Move(Box r, Box work, IReadOnlyList<Box> others, Spacing s)
    {
        var xs = new List<Candidate>();
        var ys = new List<Candidate>();

        // Monitor work area: flush, with margin, and centred.
        AddScreen(xs, r.Left, work.Left, work.Left + s.Margin);
        AddScreen(xs, r.Right, work.Right, work.Right - s.Margin);
        AddScreen(xs, r.CenterX, work.CenterX);
        AddScreen(ys, r.Top, work.Top, work.Top + s.Margin);
        AddScreen(ys, r.Bottom, work.Bottom, work.Bottom - s.Margin);
        AddScreen(ys, r.CenterY, work.CenterY);

        for (int i = 0; i < others.Count; i++)
        {
            Box o = others[i];
            if (!o.Intersects(work))
            {
                continue;
            }

            // Beside it, only when they overlap (or nearly) on the other axis.
            if (r.Top < o.Bottom + s.Threshold && o.Top < r.Bottom + s.Threshold)
            {
                AddEdge(xs, r.Left, i, o.Right, o.Right + s.Gap);
                AddEdge(xs, r.Right, i, o.Left, o.Left - s.Gap);
            }

            if (r.Left < o.Right + s.Threshold && o.Left < r.Right + s.Threshold)
            {
                AddEdge(ys, r.Top, i, o.Bottom, o.Bottom + s.Gap);
                AddEdge(ys, r.Bottom, i, o.Top, o.Top - s.Gap);
            }

            // Lined up with it.
            AddEdge(xs, r.Left, i, o.Left);
            AddEdge(xs, r.Right, i, o.Right);
            AddEdge(xs, r.CenterX, i, o.CenterX);
            AddEdge(ys, r.Top, i, o.Top);
            AddEdge(ys, r.Bottom, i, o.Bottom);
            AddEdge(ys, r.CenterY, i, o.CenterY);
        }

        var partners = new SortedSet<int>();
        int dx = Best(xs, s.Threshold, partners);
        int dy = Best(ys, s.Threshold, partners);
        return new Result(new Box(r.Left + dx, r.Top + dy, r.Right + dx, r.Bottom + dy), partners.ToList());
    }

    /// <summary>
    /// Snaps a widget being resized: only the edges being dragged move.
    /// </summary>
    public static Result Resize(Box r, Edges edges, Box work, IReadOnlyList<Box> others, Spacing s)
    {
        var partners = new SortedSet<int>();
        int left = r.Left, top = r.Top, right = r.Right, bottom = r.Bottom;

        if (edges.HasFlag(Edges.Right))
        {
            right += SnapEdge(r.Right, r.Left, s.MinWidth, work.Right, work.Right - s.Margin, others, r, s, horizontal: true, leading: false, partners);
        }
        else if (edges.HasFlag(Edges.Left))
        {
            left += SnapEdge(r.Left, r.Right, s.MinWidth, work.Left, work.Left + s.Margin, others, r, s, horizontal: true, leading: true, partners);
        }

        if (edges.HasFlag(Edges.Bottom))
        {
            bottom += SnapEdge(r.Bottom, r.Top, s.MinHeight, work.Bottom, work.Bottom - s.Margin, others, r, s, horizontal: false, leading: false, partners);
        }
        else if (edges.HasFlag(Edges.Top))
        {
            top += SnapEdge(r.Top, r.Bottom, s.MinHeight, work.Top, work.Top + s.Margin, others, r, s, horizontal: false, leading: true, partners);
        }

        return new Result(new Box(left, top, right, bottom), partners.ToList());
    }

    /// <summary>
    /// Delta for one moving edge. <paramref name="fixedEdge"/> is the opposite edge, which stays put.
    /// "leading" is the left or top edge.
    /// </summary>
    private static int SnapEdge(int edge, int fixedEdge, int minSize, int screenFlush, int screenMargin,
        IReadOnlyList<Box> others, Box r, Spacing s, bool horizontal, bool leading, SortedSet<int> partners)
    {
        var c = new List<Candidate>();
        AddScreen(c, edge, screenFlush, screenMargin);

        for (int i = 0; i < others.Count; i++)
        {
            Box o = others[i];
            int oNear = horizontal ? (leading ? o.Right : o.Left) : (leading ? o.Bottom : o.Top);
            int oSame = horizontal ? (leading ? o.Left : o.Right) : (leading ? o.Top : o.Bottom);
            int oSize = horizontal ? o.Width : o.Height;
            bool overlap = horizontal
                ? r.Top < o.Bottom + s.Threshold && o.Top < r.Bottom + s.Threshold
                : r.Left < o.Right + s.Threshold && o.Left < r.Right + s.Threshold;

            if (overlap)
            {
                AddEdge(c, edge, i, oNear, leading ? oNear + s.Gap : oNear - s.Gap);
            }

            AddEdge(c, edge, i, oSame);
            AddEdge(c, edge, i, leading ? fixedEdge - oSize : fixedEdge + oSize); // same width / height
        }

        // Never snap below the minimum size.
        c.RemoveAll(k => Math.Abs(leading ? fixedEdge - (edge + k.Delta) : edge + k.Delta - fixedEdge) < minSize);
        return Best(c, s.Threshold, partners);
    }

    private static void AddScreen(List<Candidate> list, int edge, params int[] targets) => AddEdge(list, edge, -1, targets);

    private static void AddEdge(List<Candidate> list, int edge, int partner, params int[] targets)
    {
        foreach (int target in targets)
        {
            list.Add(new Candidate(target - edge, partner));
        }
    }

    /// <summary>
    /// The smallest delta within the threshold (0 when none); adds every widget that produced it to partners.
    /// </summary>
    private static int Best(List<Candidate> candidates, int threshold, SortedSet<int> partners)
    {
        Candidate? best = null;
        foreach (Candidate c in candidates)
        {
            if (Math.Abs(c.Delta) <= threshold && (best is null || Math.Abs(c.Delta) < Math.Abs(best.Value.Delta)))
            {
                best = c;
            }
        }

        if (best is null)
        {
            return 0;
        }

        foreach (Candidate c in candidates)
        {
            if (c.Delta == best.Value.Delta && c.Partner >= 0)
            {
                partners.Add(c.Partner);
            }
        }

        return best.Value.Delta;
    }
}
