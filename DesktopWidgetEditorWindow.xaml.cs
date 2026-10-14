using System.Windows;
using System.Windows.Media;
using SysGlance.Services;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace SysGlance;

/// <summary>
/// Settings for one desktop widget. Edits <see cref="DesktopWidgetConfig"/> in place; every change is applied
/// to the widget immediately through the callback (which also saves).
/// </summary>
public partial class DesktopWidgetEditorWindow : Window
{
    private readonly DesktopWidgetConfig config;
    private readonly Action onChanged;
    private bool initializing = true;

    public DesktopWidgetEditorWindow(DesktopWidgetConfig widget, AppSettings settings, Action changed)
    {
        InitializeComponent();
        WindowEffects.Apply(this);
        config = widget;
        onChanged = changed;

        TitleText.Text = config.Name;
        NameBox.Text = config.Name;
        NameBox.TextChanged += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(NameBox.Text))
            {
                config.Name = NameBox.Text.Trim();
                TitleText.Text = config.Name;
                Changed();
            }
        };

        TopmostToggle.IsChecked = config.AlwaysOnTop;
        TopmostToggle.Checked += (_, _) => { config.AlwaysOnTop = true; Changed(); };
        TopmostToggle.Unchecked += (_, _) => { config.AlwaysOnTop = false; Changed(); };

        (config.CardStyle == DesktopWidgetConfig.StyleNumbers ? StyleNumbersRadio : StyleGaugesRadio).IsChecked = true;
        StyleGaugesRadio.Checked += (_, _) => { config.CardStyle = DesktopWidgetConfig.StyleGauges; Changed(); };
        StyleNumbersRadio.Checked += (_, _) => { config.CardStyle = DesktopWidgetConfig.StyleNumbers; Changed(); };

        var spacingRadios = new Dictionary<string, System.Windows.Controls.RadioButton>
        {
            [DesktopWidgetConfig.SpacingNormal] = SpacingNormalRadio,
            [DesktopWidgetConfig.SpacingCompact] = SpacingCompactRadio,
            [DesktopWidgetConfig.SpacingNone] = SpacingNoneRadio
        };
        spacingRadios[config.Spacing].IsChecked = true;
        foreach ((string spacing, System.Windows.Controls.RadioButton radio) in spacingRadios)
        {
            radio.Checked += (_, _) => { config.Spacing = spacing; Changed(); };
        }

        var graphRadios = new Dictionary<int, System.Windows.Controls.RadioButton> { [0] = GraphOff, [1] = Graph1, [5] = Graph5, [15] = Graph15 };
        graphRadios[config.GraphMinutes].IsChecked = true;
        foreach ((int minutes, System.Windows.Controls.RadioButton radio) in graphRadios)
        {
            radio.Checked += (_, _) => { config.GraphMinutes = minutes; Changed(); };
        }

        MetricList.Selection = new DesktopWidgetMetricSelection(config, settings);
        MetricList.Changed += Changed;

        BindColor(BgPicker, () => config.BackgroundColor, v => config.BackgroundColor = v);
        BindColor(CardPicker, () => config.CardColor, v => config.CardColor = v);
        BindColor(TextPicker, () => config.TextColor, v => config.TextColor = v);

        BindStepper(BgOpacityDec, BgOpacityInc, BgOpacityText, 10, 0, 100, "%", () => config.BackgroundOpacity, v => config.BackgroundOpacity = v);
        BindStepper(CardOpacityDec, CardOpacityInc, CardOpacityText, 10, 0, 100, "%", () => config.CardOpacity, v => config.CardOpacity = v);
        BindStepper(TextScaleDec, TextScaleInc, TextScaleText, 10, 50, 200, "%", () => config.TextScale, v => config.TextScale = v);
        BindStepper(RadiusDec, RadiusInc, RadiusText, 2, 0, 40, "", () => config.CornerRadius, v => config.CornerRadius = v);

        initializing = false;
    }

    private void Changed()
    {
        if (!initializing)
        {
            onChanged();
        }
    }

    private void BindColor(ColorPicker.PortableColorPicker picker, Func<string> get, Action<string> set)
    {
        try
        {
            picker.SelectedColor = (Color)ColorConverter.ConvertFromString(get());
        }
        catch (FormatException)
        {
        }

        picker.ColorChanged += (_, _) =>
        {
            Color c = picker.SelectedColor;
            set($"#{c.R:X2}{c.G:X2}{c.B:X2}");
            Changed();
        };
    }

    private void BindStepper(System.Windows.Controls.Button dec, System.Windows.Controls.Button inc,
        System.Windows.Controls.TextBlock text, int step, int min, int max, string suffix, Func<int> get, Action<int> set)
    {
        void Show() => text.Text = $"{get()}{suffix}";
        void Step(int delta)
        {
            set(Math.Clamp(get() + delta, min, max));
            Show();
            Changed();
        }

        Show();
        dec.Click += (_, _) => Step(-step);
        inc.Click += (_, _) => Step(step);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
