using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Orientation = System.Windows.Controls.Orientation;

namespace SysGlance.Controls;

/// <summary>
/// One stop on the tour. <see cref="Target"/> null shows the callout centred with the whole window dimmed.
/// <see cref="Until"/> cuts the highlight off below that element, for cards taller than the window.
/// </summary>
internal sealed record TourStep(FrameworkElement? Target, string Title, string Text, FrameworkElement? Until = null);

/// <summary>
/// Guided tour drawn over a window: dims everything except a highlighted area and shows a callout with
/// Back / Next / Skip. Add it on top of the window's content (spanning all rows) and call <see cref="Start"/>.
/// Clicks inside the highlight reach the control underneath; everything else is blocked while the tour runs.
/// </summary>
internal sealed class TourOverlay : Canvas
{
    private const double HolePadding = 6;
    private const double Gap = 12;
    private const double Edge = 12;
    private static readonly TimeSpan MoveTime = TimeSpan.FromMilliseconds(220);

    private readonly Path dim = new() { Fill = new SolidColorBrush(Color.FromArgb(0xB8, 0, 0, 0)) };
    private readonly Border ring = new() { BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(10), IsHitTestVisible = false };
    private readonly Border callout = new();
    private readonly TextBlock stepText = new() { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x9C, 0xC8, 0xF0)) };
    private readonly TextBlock titleText = new() { FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, Margin = new Thickness(0, 4, 0, 6), TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock bodyText = new() { FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xC8)), TextWrapping = TextWrapping.Wrap, LineHeight = 19 };
    private readonly StackPanel dots = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button backButton = new() { Content = "Back", Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 0, 8, 0) };
    private readonly Button nextButton = new() { Padding = new Thickness(14, 5, 14, 5) };
    private readonly Button skipButton = new() { Content = "Skip tour", Padding = new Thickness(0, 5, 0, 5), FontSize = 12 };

    private IReadOnlyList<TourStep> steps = Array.Empty<TourStep>();
    private ScrollViewer? scroller;
    private Window? window;
    private int index;
    private Rect? shownHole;
    private Rect? fromHole;
    private Rect? toHole;
    private DateTime moveStarted;
    private bool animating;

    /// <summary>
    /// Raised once when the tour ends: true when the last step was finished, false when skipped.
    /// </summary>
    public event Action<bool>? Finished;

    public TourOverlay()
    {
        Focusable = true;
        FocusVisualStyle = null;
        Children.Add(dim);
        Children.Add(ring);
        Children.Add(callout);
        BuildCallout();

        SizeChanged += (_, _) => Refresh(animate: false);
        backButton.Click += (_, _) => Back();
        nextButton.Click += (_, _) => Next();
        skipButton.Click += (_, _) => End(completed: false);
    }

    /// <summary>
    /// Starts the tour. <paramref name="scrollViewer"/> is the window's main scroller: steps inside it are
    /// scrolled into view and the highlight is kept within its visible area.
    /// </summary>
    public void Start(IReadOnlyList<TourStep> tourSteps, ScrollViewer? scrollViewer)
    {
        steps = tourSteps;
        scroller = scrollViewer;
        window = Window.GetWindow(this);
        ring.BorderBrush = (Brush)FindResource("AccentBrush");
        nextButton.Style = (Style)FindResource("AccentButton");
        skipButton.Style = (Style)FindResource("SubtleButton");

        if (window is not null)
        {
            window.PreviewKeyDown += OnKeyDown;
        }

        if (scroller is not null)
        {
            scroller.ScrollChanged += OnScrollChanged;
        }

        // Wait for the window's first layout before measuring targets.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            Focus();
            ShowStep(0);
        });
    }

    public void Next()
    {
        if (index >= steps.Count - 1)
        {
            End(completed: true);
        }
        else
        {
            ShowStep(index + 1);
        }
    }

    public void Back()
    {
        if (index > 0)
        {
            ShowStep(index - 1);
        }
    }

    private void BuildCallout()
    {
        callout.Width = 300;
        callout.Background = new SolidColorBrush(Color.FromRgb(0x2B, 0x2B, 0x2B));
        callout.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0x3D, 0x3D));
        callout.BorderThickness = new Thickness(1);
        callout.CornerRadius = new CornerRadius(8);
        callout.Padding = new Thickness(16, 14, 16, 12);
        callout.Effect = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 4, Opacity = 0.5, Color = Colors.Black };

        var buttons = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var right = new StackPanel { Orientation = Orientation.Horizontal };
        right.Children.Add(backButton);
        right.Children.Add(nextButton);
        Grid.SetColumn(right, 2);
        buttons.Children.Add(skipButton);
        buttons.Children.Add(right);

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(dots, 1);
        header.Children.Add(stepText);
        header.Children.Add(dots);

        var content = new StackPanel();
        content.Children.Add(header);
        content.Children.Add(titleText);
        content.Children.Add(bodyText);
        content.Children.Add(buttons);
        callout.Child = content;
    }

    private void ShowStep(int stepIndex)
    {
        index = stepIndex;
        TourStep step = steps[index];

        stepText.Text = $"Step {index + 1} of {steps.Count}";
        titleText.Text = step.Title;
        bodyText.Text = step.Text;
        backButton.Visibility = index == 0 ? Visibility.Collapsed : Visibility.Visible;
        nextButton.Content = index == steps.Count - 1 ? "Finish" : index == 0 ? "Start tour" : "Next";
        skipButton.Visibility = index == steps.Count - 1 ? Visibility.Hidden : Visibility.Visible;

        dots.Children.Clear();
        for (int i = 0; i < steps.Count; i++)
        {
            dots.Children.Add(new Ellipse
            {
                Width = i == index ? 14 : 6,
                Height = 6,
                Margin = new Thickness(3, 0, 0, 0),
                Fill = i == index ? ring.BorderBrush : new SolidColorBrush(Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF))
            });
        }

        ScrollIntoView(step);

        // Measure after the scroll has been laid out.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            Refresh(animate: true);
            callout.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
        });
    }

    /// <summary>
    /// Scrolls so the step's highlight starts near the top of the scroller, leaving room for the callout below.
    /// </summary>
    private void ScrollIntoView(TourStep step)
    {
        if (scroller?.Content is not Visual content || step.Target is null || !scroller.IsAncestorOf(step.Target))
        {
            return;
        }

        double top = step.Target.TransformToVisual(content).Transform(new Point(0, 0)).Y;
        scroller.ScrollToVerticalOffset(Math.Max(0, top - Gap));
    }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs e) => Refresh(animate: false);

    private void Refresh(bool animate)
    {
        if (steps.Count == 0 || ActualWidth <= 0)
        {
            return;
        }

        Rect? hole = HoleFor(steps[index]);
        if (animate && shownHole is not null && hole is not null)
        {
            fromHole = shownHole;
            toHole = hole;
            moveStarted = DateTime.UtcNow;
            if (!animating)
            {
                animating = true;
                CompositionTarget.Rendering += OnRendering;
            }
        }
        else
        {
            StopAnimation();
            DrawHole(hole);
        }

        PlaceCallout(hole);
    }

    private Rect? HoleFor(TourStep step)
    {
        if (step.Target is null || !step.Target.IsVisible)
        {
            return null;
        }

        Rect r = step.Target.TransformToVisual(this).TransformBounds(new Rect(step.Target.RenderSize));
        if (step.Until is { IsVisible: true })
        {
            Rect until = step.Until.TransformToVisual(this).TransformBounds(new Rect(step.Until.RenderSize));
            r.Height = Math.Max(0, until.Bottom - r.Top + 14);
        }

        r.Inflate(HolePadding, HolePadding);

        if (scroller is not null && scroller.IsAncestorOf(step.Target))
        {
            Rect viewport = scroller.TransformToVisual(this).TransformBounds(new Rect(scroller.RenderSize));
            r.Intersect(viewport);
        }

        r.Intersect(new Rect(0, 0, ActualWidth, ActualHeight));
        return r.IsEmpty || r.Height < 8 ? null : r;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        double t = Math.Min(1, (DateTime.UtcNow - moveStarted).TotalMilliseconds / MoveTime.TotalMilliseconds);
        double k = 1 - Math.Pow(1 - t, 3); // ease out
        Rect a = fromHole!.Value, b = toHole!.Value;
        DrawHole(new Rect(
            a.X + (b.X - a.X) * k, a.Y + (b.Y - a.Y) * k,
            a.Width + (b.Width - a.Width) * k, a.Height + (b.Height - a.Height) * k));

        if (t >= 1)
        {
            StopAnimation();
        }
    }

    private void StopAnimation()
    {
        if (animating)
        {
            animating = false;
            CompositionTarget.Rendering -= OnRendering;
        }
    }

    private void DrawHole(Rect? hole)
    {
        shownHole = hole;
        var full = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight));

        if (hole is not Rect r)
        {
            dim.Data = full;
            ring.Visibility = Visibility.Collapsed;
            return;
        }

        dim.Data = new CombinedGeometry(GeometryCombineMode.Exclude, full, new RectangleGeometry(r, 10, 10));
        ring.Visibility = Visibility.Visible;
        ring.Width = r.Width;
        ring.Height = r.Height;
        SetLeft(ring, r.X);
        SetTop(ring, r.Y);
    }

    /// <summary>
    /// Below the highlight if it fits, else above, else over the bottom of the window. Centred with no highlight.
    /// </summary>
    private void PlaceCallout(Rect? hole)
    {
        callout.Measure(new Size(callout.Width, double.PositiveInfinity));
        double w = callout.Width, h = callout.DesiredSize.Height;
        double x, y;

        if (hole is not Rect r)
        {
            x = (ActualWidth - w) / 2;
            y = (ActualHeight - h) / 2;
        }
        else
        {
            x = r.X + r.Width / 2 - w / 2;
            if (r.Bottom + Gap + h <= ActualHeight - Edge)
            {
                y = r.Bottom + Gap;
            }
            else if (r.Top - Gap - h >= Edge)
            {
                y = r.Top - Gap - h;
            }
            else
            {
                y = ActualHeight - Edge - h;
            }
        }

        SetLeft(callout, Math.Clamp(x, Edge, Math.Max(Edge, ActualWidth - Edge - w)));
        SetTop(callout, Math.Max(Edge, y));
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                End(completed: false);
                break;
            case Key.Right:
            case Key.Enter:
                Next();
                break;
            case Key.Left:
                Back();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void End(bool completed)
    {
        StopAnimation();
        if (window is not null)
        {
            window.PreviewKeyDown -= OnKeyDown;
        }

        if (scroller is not null)
        {
            scroller.ScrollChanged -= OnScrollChanged;
        }

        steps = Array.Empty<TourStep>();
        Finished?.Invoke(completed);
    }
}
