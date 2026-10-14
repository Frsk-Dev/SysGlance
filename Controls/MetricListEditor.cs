using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SysGlance.Services;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Orientation = System.Windows.Controls.Orientation;

namespace SysGlance.Controls;

/// <summary>
/// Checklist of metrics with a colour swatch and up/down arrows per shown metric, split into Active and
/// Disabled sections. Edits an <see cref="IMetricSelection"/>; the host window saves on <see cref="Changed"/>.
/// Needs the "IconButton" style from Themes/Controls.xaml in its resources.
/// </summary>
public sealed class MetricListEditor : StackPanel
{
    private IMetricSelection? selection;

    /// <summary>
    /// Raised after any tick, colour, or order change.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// The selection being edited. Setting it rebuilds the list.
    /// </summary>
    public IMetricSelection? Selection
    {
        get => selection;
        set
        {
            selection = value;
            Rebuild();
        }
    }

    /// <summary>
    /// Rebuilds the rows from the selection (call after it was changed elsewhere).
    /// </summary>
    public void Rebuild()
    {
        Children.Clear();

        if (selection is null)
        {
            return;
        }

        IReadOnlyList<string> active = selection.ActiveKeys;
        IReadOnlyList<string> inactive = selection.InactiveKeys;

        AddSectionHeader("Active");
        for (int i = 0; i < active.Count; i++)
        {
            AddRow(active[i], i, active.Count, isActive: true);
        }

        AddSectionHeader("Disabled");
        foreach (string key in inactive)
        {
            AddRow(key, 0, 0, isActive: false);
        }
    }

    private void Notify()
    {
        Rebuild();
        Changed?.Invoke();
    }

    private void AddSectionHeader(string title)
    {
        var header = new DockPanel { Margin = new Thickness(0, 6, 0, 2) };

        var text = new TextBlock
        {
            Text = title,
            Foreground = Brush("#8E8E8E"),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        DockPanel.SetDock(text, Dock.Left);
        header.Children.Add(text);
        header.Children.Add(new Border { Height = 1, Background = Brush("#15FFFFFF"), VerticalAlignment = VerticalAlignment.Center });

        Children.Add(header);
    }

    private void AddRow(string key, int indexInGroup, int groupCount, bool isActive)
    {
        var row = new Grid { Margin = new Thickness(0, 2, 0, 2), MinHeight = 28 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var checkBox = new CheckBox
        {
            IsChecked = isActive,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        };
        checkBox.Checked += (_, _) => { selection!.SetActive(key, true); Notify(); };
        checkBox.Unchecked += (_, _) => { selection!.SetActive(key, false); Notify(); };
        row.Children.Add(checkBox);

        var label = new TextBlock
        {
            Text = AppSettings.GetLabel(key),
            Foreground = Brush(isActive ? "#FFFFFF" : "#8E8E8E"),
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(label, 1);
        row.Children.Add(label);

        var colorPicker = new ColorPicker.PortableColorPicker
        {
            Width = 22,
            Height = 22,
            ShowAlpha = false,
            SelectedColor = (Color)ColorConverter.ConvertFromString(selection!.GetColor(key)),
            Margin = new Thickness(6, 0, 6, 0),
            Opacity = isActive ? 1.0 : 0.4
        };
        colorPicker.ColorChanged += (_, _) =>
        {
            Color c = colorPicker.SelectedColor;
            selection.SetColor(key, $"#{c.R:X2}{c.G:X2}{c.B:X2}");
            Changed?.Invoke();
        };
        Grid.SetColumn(colorPicker, 3);
        row.Children.Add(colorPicker);

        if (isActive)
        {
            // Mini graph on/off for this card: the chart icon is lit when on.
            bool hasGraph = selection.HasGraph(key);
            var graph = new Button
            {
                Content = "\uE9D2",
                FontFamily = TryFindResource("IconFont") as System.Windows.Media.FontFamily,
                FontSize = 14,
                Style = TryFindResource("IconButton") as Style,
                Foreground = Brush(hasGraph ? selection.GetColor(key) : "#55FFFFFF"),
                ToolTip = hasGraph ? "Mini graph on (click to turn off)" : "Mini graph off (click to turn on)",
                Margin = new Thickness(4, 0, 0, 0)
            };
            graph.Click += (_, _) => { selection.SetGraph(key, !hasGraph); Notify(); };
            Grid.SetColumn(graph, 2);
            row.Children.Add(graph);

            var arrows = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var up = new Button
            {
                Content = "▲",
                Style = TryFindResource("IconButton") as Style,
                ToolTip = "Move up",
                IsEnabled = indexInGroup > 0
            };
            up.Click += (_, _) => { selection.Move(key, -1); Notify(); };

            var down = new Button
            {
                Content = "▼",
                Style = TryFindResource("IconButton") as Style,
                ToolTip = "Move down",
                IsEnabled = indexInGroup < groupCount - 1
            };
            down.Click += (_, _) => { selection.Move(key, 1); Notify(); };

            arrows.Children.Add(up);
            arrows.Children.Add(down);
            Grid.SetColumn(arrows, 4);
            row.Children.Add(arrows);
        }

        Children.Add(row);
    }

    private static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
}
