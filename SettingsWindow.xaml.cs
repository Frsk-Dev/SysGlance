using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using SysGlance.Services;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Orientation = System.Windows.Controls.Orientation;

namespace SysGlance;

/// <summary>
/// Settings window for metric visibility, order, colors, and overlay placement.
/// </summary>
public partial class SettingsWindow : Window
{
    private static readonly HashSet<string> BenchmarkRecordingMetrics =
        ["FpsMin", "FpsAvg", "FpsMax", "Fps1Low", "Fps01Low"];

    private readonly AppSettings settings;
    private readonly Action onSettingsChanged;
    private readonly DispatcherTimer saveNotifyDebounceTimer;
    private bool saveNotifyPending;
    private bool isInitializing = true;

    /// <summary>
    /// Initializes controls from the current settings.
    /// </summary>
    public SettingsWindow(AppSettings currentSettings, Action onChanged)
    {
        InitializeComponent();

        settings = currentSettings;
        onSettingsChanged = onChanged;
        saveNotifyDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(120)
        };
        saveNotifyDebounceTimer.Tick += (_, _) => FlushPendingSaveAndNotify();
        Closing += (_, _) => FlushPendingSaveAndNotify();

        BuildMetricList();
        UpdateBenchmarkNoticeVisibility();

        // Label color picker.
        LabelColorPicker.SelectedColor = (Color)ColorConverter.ConvertFromString(settings.LabelColor);
        LabelColorPicker.ColorChanged += (_, _) =>
        {
            Color c = LabelColorPicker.SelectedColor;
            settings.LabelColor = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            SaveAndNotify();
        };

        switch (settings.Position)
        {
            case "Center":
                PositionCenterRadio.IsChecked = true;
                break;

            case "Right":
                PositionRightRadio.IsChecked = true;
                break;

            default:
                PositionLeftRadio.IsChecked = true;
                break;
        }

        switch (settings.TargetMonitor)
        {
            case "Primary":
                MonitorPrimaryRadio.IsChecked = true;
                break;
            case "Tertiary":
                MonitorTertiaryRadio.IsChecked = true;
                break;
            default:
                MonitorSecondaryRadio.IsChecked = true;
                break;
        }

        StartupCheckBox.IsChecked = settings.StartWithWindows;
        StartupCheckBox.Checked += (_, _) => OnStartupToggled(true);
        StartupCheckBox.Unchecked += (_, _) => OnStartupToggled(false);

        // Hide overlay.
        HideOverlayCheckBox.IsChecked = settings.HideOverlay;
        HideOverlayCheckBox.Checked += (_, _) => OnHideOverlayToggled(true);
        HideOverlayCheckBox.Unchecked += (_, _) => OnHideOverlayToggled(false);

        // Web server.
        WebServerCheckBox.IsChecked = settings.WebServerEnabled;
        WebServerCheckBox.Checked += (_, _) => OnWebServerToggled(true);
        WebServerCheckBox.Unchecked += (_, _) => OnWebServerToggled(false);

        PortTextBox.Text = settings.WebServerPort.ToString();
        PortTextBox.TextChanged += OnPortTextChanged;
        UpdateIframeCode();
        UpdateWebServerDetailsVisibility();

        CopyIframeButton.Click += (_, _) =>
            System.Windows.Clipboard.SetText(IframeCodeTextBox.Text);

        // Dashboard color pickers.
        DashBgPicker.SelectedColor = (Color)ColorConverter.ConvertFromString(settings.DashboardBgColor);
        DashBgPicker.ColorChanged += (_, _) =>
        {
            Color c = DashBgPicker.SelectedColor;
            settings.DashboardBgColor = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            SaveAndNotify();
        };

        DashCardPicker.SelectedColor = (Color)ColorConverter.ConvertFromString(settings.DashboardCardColor);
        DashCardPicker.ColorChanged += (_, _) =>
        {
            Color c = DashCardPicker.SelectedColor;
            settings.DashboardCardColor = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            SaveAndNotify();
        };

        // Wire events after initial population to avoid false triggers.
        PositionLeftRadio.Checked += OnRadioChanged;
        PositionCenterRadio.Checked += OnRadioChanged;
        PositionRightRadio.Checked += OnRadioChanged;
        MonitorPrimaryRadio.Checked += OnRadioChanged;
        MonitorSecondaryRadio.Checked += OnRadioChanged;
        MonitorTertiaryRadio.Checked += OnRadioChanged;

        isInitializing = false;
    }

    /// <summary>
    /// Rebuilds the metric list with Active and Disabled sections.
    /// </summary>
    private void BuildMetricList()
    {
        MetricListPanel.Children.Clear();

        // Split into active and disabled groups.
        var activeKeys = new List<string>();
        var disabledKeys = new List<string>();

        foreach (string key in settings.MetricOrder)
        {
            if (settings.IsVisible(key))
            {
                activeKeys.Add(key);
            }
            else
            {
                disabledKeys.Add(key);
            }
        }

        // Active section header.
        AddSectionHeader("Active");

        for (int i = 0; i < activeKeys.Count; i++)
        {
            AddMetricRow(activeKeys[i], i, activeKeys.Count, isActive: true);
        }

        // Disabled section header.
        AddSectionHeader("Disabled");

        for (int i = 0; i < disabledKeys.Count; i++)
        {
            AddMetricRow(disabledKeys[i], i, disabledKeys.Count, isActive: false);
        }
    }

    /// <summary>
    /// Adds a section header with a horizontal separator line.
    /// </summary>
    private void AddSectionHeader(string title)
    {
        var headerPanel = new DockPanel { Margin = new Thickness(0, 6, 0, 2) };

        var headerText = new TextBlock
        {
            Text = title,
            Foreground = new SolidColorBrush((Color)
                ColorConverter.ConvertFromString("#888888")),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        DockPanel.SetDock(headerText, Dock.Left);
        headerPanel.Children.Add(headerText);

        var line = new Border
        {
            Height = 1,
            Background = new SolidColorBrush((Color)
                ColorConverter.ConvertFromString("#444444")),
            VerticalAlignment = VerticalAlignment.Center
        };
        headerPanel.Children.Add(line);

        MetricListPanel.Children.Add(headerPanel);
    }

    /// <summary>
    /// Adds a metric row with checkbox, label, color swatch, and reorder buttons.
    /// </summary>
    private void AddMetricRow(string key, int indexInGroup, int groupCount, bool isActive)
    {
        var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

        var checkBox = new CheckBox
        {
            IsChecked = isActive,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        };
        string capturedKey = key;
        checkBox.Checked += (_, _) => OnVisibilityToggled(capturedKey, true);
        checkBox.Unchecked += (_, _) => OnVisibilityToggled(capturedKey, false);
        Grid.SetColumn(checkBox, 0);
        row.Children.Add(checkBox);

        var label = new TextBlock
        {
            Text = AppSettings.GetLabel(key),
            Foreground = new SolidColorBrush((Color)
                ColorConverter.ConvertFromString(isActive ? "#CCCCCC" : "#777777")),
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(label, 1);
        row.Children.Add(label);

        string currentColor = settings.GetColor(key);
        var colorPicker = new ColorPicker.PortableColorPicker
        {
            Width = 22,
            Height = 22,
            ShowAlpha = false,
            SelectedColor = (Color)ColorConverter.ConvertFromString(currentColor),
            Margin = new Thickness(6, 0, 6, 0),
            Opacity = isActive ? 1.0 : 0.4
        };
        colorPicker.ColorChanged += (_, _) =>
        {
            Color c = colorPicker.SelectedColor;
            settings.SetColor(capturedKey, $"#{c.R:X2}{c.G:X2}{c.B:X2}");
            SaveAndNotify();
        };
        Grid.SetColumn(colorPicker, 2);
        row.Children.Add(colorPicker);

        if (isActive)
        {
            var arrowPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            var upButton = new Button
            {
                Content = "\u25B2",
                Width = 22,
                Height = 20,
                FontSize = 9,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 2, 0),
                IsEnabled = indexInGroup > 0
            };
            upButton.Click += (_, _) => OnMoveUp(capturedKey);

            var downButton = new Button
            {
                Content = "\u25BC",
                Width = 22,
                Height = 20,
                FontSize = 9,
                Padding = new Thickness(0),
                IsEnabled = indexInGroup < groupCount - 1
            };
            downButton.Click += (_, _) => OnMoveDown(capturedKey);

            arrowPanel.Children.Add(upButton);
            arrowPanel.Children.Add(downButton);
            Grid.SetColumn(arrowPanel, 3);
            row.Children.Add(arrowPanel);
        }

        MetricListPanel.Children.Add(row);
    }

    /// <summary>
    /// Toggles the Windows startup registry entry.
    /// </summary>
    private void OnStartupToggled(bool enabled)
    {
        settings.StartWithWindows = enabled;

        using Microsoft.Win32.RegistryKey? key =
            Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true);

        if (key != null)
        {
            if (enabled)
            {
                string appPath = Environment.ProcessPath ?? "";

                if (!string.IsNullOrEmpty(appPath))
                {
                    key.SetValue("SysGlance", $"\"{appPath}\"");
                }
            }
            else
            {
                key.DeleteValue("SysGlance", false);
            }
        }

        SaveAndNotify();
    }

    /// <summary>
    /// Handles visibility checkbox toggle for a metric.
    /// </summary>
    private void OnVisibilityToggled(string metricKey, bool visible)
    {
        settings.SetVisible(metricKey, visible);
        BuildMetricList();
        UpdateBenchmarkNoticeVisibility();
        SaveAndNotify();
    }



    /// <summary>
    /// Moves a metric one position up in the active ordering.
    /// </summary>
    private void OnMoveUp(string metricKey)
    {
        int index = settings.MetricOrder.IndexOf(metricKey);

        if (index <= 0)
        {
            return;
        }

        // Swap with the previous active metric.
        for (int i = index - 1; i >= 0; i--)
        {
            if (settings.IsVisible(settings.MetricOrder[i]))
            {
                (settings.MetricOrder[index], settings.MetricOrder[i]) =
                    (settings.MetricOrder[i], settings.MetricOrder[index]);
                break;
            }
        }

        BuildMetricList();
        SaveAndNotify();
    }

    /// <summary>
    /// Moves a metric one position down in the active ordering.
    /// </summary>
    private void OnMoveDown(string metricKey)
    {
        int index = settings.MetricOrder.IndexOf(metricKey);

        if ((index < 0) || (index >= settings.MetricOrder.Count - 1))
        {
            return;
        }

        // Swap with the next active metric.
        for (int i = index + 1; i < settings.MetricOrder.Count; i++)
        {
            if (settings.IsVisible(settings.MetricOrder[i]))
            {
                (settings.MetricOrder[index], settings.MetricOrder[i]) =
                    (settings.MetricOrder[i], settings.MetricOrder[index]);
                break;
            }
        }

        BuildMetricList();
        SaveAndNotify();
    }

    /// <summary>
    /// Handles position and monitor radio button changes.
    /// </summary>
    private void OnRadioChanged(object sender, RoutedEventArgs e)
    {
        if (isInitializing)
        {
            return;
        }

        if (PositionCenterRadio.IsChecked == true)
        {
            settings.Position = "Center";
        }
        else if (PositionRightRadio.IsChecked == true)
        {
            settings.Position = "Right";
        }
        else
        {
            settings.Position = "Left";
        }

        if (MonitorPrimaryRadio.IsChecked == true)
        {
            settings.TargetMonitor = "Primary";
        }
        else if (MonitorTertiaryRadio.IsChecked == true)
        {
            settings.TargetMonitor = "Tertiary";
        }
        else
        {
            settings.TargetMonitor = "Secondary";
        }

        SaveAndNotify();
    }

    /// <summary>
    /// Toggles the taskbar overlay visibility setting.
    /// </summary>
    private void OnHideOverlayToggled(bool hidden)
    {
        settings.HideOverlay = hidden;
        SaveAndNotify();
    }

    /// <summary>
    /// Toggles the web server and shows or hides the details panel.
    /// </summary>
    private void OnWebServerToggled(bool enabled)
    {
        settings.WebServerEnabled = enabled;
        UpdateWebServerDetailsVisibility();
        SaveAndNotify();
    }

    /// <summary>
    /// Validates the port input and updates the setting.
    /// </summary>
    private void OnPortTextChanged(object sender, TextChangedEventArgs e)
    {
        if (isInitializing)
        {
            return;
        }

        if (int.TryParse(PortTextBox.Text, out int port) && (port >= 1024) && (port <= 65535))
        {
            settings.WebServerPort = port;
            PortTextBox.BorderBrush = new SolidColorBrush((Color)
                ColorConverter.ConvertFromString("#555555"));
            UpdateIframeCode();
            SaveAndNotify();
        }
        else
        {
            PortTextBox.BorderBrush = new SolidColorBrush((Color)
                ColorConverter.ConvertFromString("#CC4444"));
        }
    }

    /// <summary>
    /// Updates the iframe code display based on the current port.
    /// </summary>
    private void UpdateIframeCode()
    {
        IframeCodeTextBox.Text =
            $"""<iframe src="http://localhost:{settings.WebServerPort}" width="100%" height="100%" frameborder="0" allowtransparency="true" style="background:transparent"></iframe>""";
    }

    /// <summary>
    /// Shows or hides the web server details panel.
    /// </summary>
    private void UpdateWebServerDetailsVisibility()
    {
        WebServerDetailsPanel.Visibility = settings.WebServerEnabled
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>
    /// Saves settings and notifies the overlay to rebuild.
    /// </summary>
    private void SaveAndNotify()
    {
        saveNotifyPending = true;
        saveNotifyDebounceTimer.Stop();
        saveNotifyDebounceTimer.Start();
    }

    private void FlushPendingSaveAndNotify()
    {
        saveNotifyDebounceTimer.Stop();

        if (!saveNotifyPending)
        {
            return;
        }

        saveNotifyPending = false;
        settings.Save();
        onSettingsChanged();
    }

    /// <summary>
    /// Shows a warning when benchmark-only FPS stats are enabled.
    /// </summary>
    private void UpdateBenchmarkNoticeVisibility()
    {
        bool showNotice = BenchmarkRecordingMetrics.Any(settings.IsVisible);
        BenchmarkNoticeBox.Visibility = showNotice
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>
    /// Opens the Ko-fi donation page in the default browser.
    /// </summary>
    private void KofiButton_Click(object sender, MouseButtonEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://ko-fi.com/frskdevelopment",
            UseShellExecute = true
        });
    }

    /// <summary>
    /// Highlights the Ko-fi button on mouse enter.
    /// </summary>
    private void KofiButton_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        KofiButton.Background = new SolidColorBrush((Color)
            ColorConverter.ConvertFromString("#363636"));
    }

    /// <summary>
    /// Resets the Ko-fi button on mouse leave.
    /// </summary>
    private void KofiButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        KofiButton.Background = new SolidColorBrush((Color)
            ColorConverter.ConvertFromString("#2A2A2A"));
    }

    /// <summary>
    /// Enables window drag via the custom title bar.
    /// </summary>
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    /// <summary>
    /// Closes the settings window.
    /// </summary>
    private void CloseButton_Click(object sender, MouseButtonEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// Highlights the close button on hover.
    /// </summary>
    private void CloseButton_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        CloseButton.Background = new SolidColorBrush((Color)
            ColorConverter.ConvertFromString("#3A3A3A"));

        if (CloseButton.Child is Path path)
        {
            path.Stroke = new SolidColorBrush((Color)
                ColorConverter.ConvertFromString("#4389C7"));
        }
    }

    /// <summary>
    /// Resets the close button style on mouse leave.
    /// </summary>
    private void CloseButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        CloseButton.Background = System.Windows.Media.Brushes.Transparent;

        if (CloseButton.Child is Path path)
        {
            path.Stroke = new SolidColorBrush((Color)
                ColorConverter.ConvertFromString("#888888"));
        }
    }
}
