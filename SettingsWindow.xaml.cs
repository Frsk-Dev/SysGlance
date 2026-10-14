using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

    /// <summary>
    /// Where "Get the iCUE widget" sends people.
    /// TODO: replace with the Elgato Marketplace listing once it is live.
    /// </summary>
    // The widget file attached to the latest GitHub release (until it is approved on the Elgato Marketplace).
    internal const string WidgetDownloadUrl = "https://github.com/Frsk-Dev/SysGlance/releases/latest/download/SysGlance.icuewidget";

    private readonly AppSettings settings;
    private readonly Action onSettingsChanged;
    private readonly Func<int> getTrustedWidgetCount;
    private readonly Action revokeAllWidgets;
    private readonly DesktopWidgetManager desktopWidgets;
    private readonly Func<string?> getServerProblem;
    private readonly UpdateHooks updates;
    private AppSettingsWindow? appSettingsWindow;
    private Controls.TourOverlay? tour;
    private readonly DispatcherTimer saveNotifyDebounceTimer;
    private bool saveNotifyPending;
    private bool isInitializing = true;

    /// <summary>
    /// Initializes controls from the current settings.
    /// </summary>
    internal SettingsWindow(AppSettings currentSettings, Action onChanged, Func<int> trustedWidgetCount, Action revokeWidgets,
        DesktopWidgetManager desktopWidgetManager, Func<string?> serverProblem, UpdateHooks updateHooks)
    {
        InitializeComponent();
        WindowEffects.Apply(this);

        settings = currentSettings;
        onSettingsChanged = onChanged;
        getTrustedWidgetCount = trustedWidgetCount;
        revokeAllWidgets = revokeWidgets;
        desktopWidgets = desktopWidgetManager;
        getServerProblem = serverProblem;
        updates = updateHooks;
        VersionText.Text = typeof(SettingsWindow).Assembly.GetName().Version?.ToString(3) ?? "";
        saveNotifyDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(120)
        };
        saveNotifyDebounceTimer.Tick += (_, _) => FlushPendingSaveAndNotify();
        Closing += (_, _) => FlushPendingSaveAndNotify();

        MetricList.Selection = new AppMetricSelection(settings);
        MetricList.Changed += () =>
        {
            UpdateBenchmarkNoticeVisibility();
            SaveAndNotify();
        };
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


        // Hide overlay.
        HideOverlayCheckBox.IsChecked = settings.HideOverlay;
        HideOverlayCheckBox.Checked += (_, _) => OnHideOverlayToggled(true);
        HideOverlayCheckBox.Unchecked += (_, _) => OnHideOverlayToggled(false);

        // Web server.
        WebServerCheckBox.IsChecked = settings.WebServerEnabled;
        WebServerCheckBox.Checked += (_, _) => OnWebServerToggled(true);
        WebServerCheckBox.Unchecked += (_, _) => OnWebServerToggled(false);
        UpdateWebServerDetailsVisibility();

        var edgeGraphRadios = new Dictionary<int, System.Windows.Controls.RadioButton> { [0] = EdgeGraphOff, [1] = EdgeGraph1, [5] = EdgeGraph5, [15] = EdgeGraph15 };
        edgeGraphRadios[settings.DashboardGraphMinutes].IsChecked = true;
        foreach ((int minutes, System.Windows.Controls.RadioButton radio) in edgeGraphRadios)
        {
            radio.Checked += (_, _) => { settings.DashboardGraphMinutes = minutes; SaveAndNotify(); };
        }

        // Xeneon Edge widget: download link and paired-widget status.
        GetWidgetButton.Click += (_, _) => OpenUrl(WidgetDownloadUrl);
        DisconnectButton.Click += (_, _) => OnDisconnectClicked();
        RefreshConnectionStatus();

        // Start with Windows, updates, backup, about and uninstall live in App settings (⚙ in the title bar).
        AppSettingsButton.Click += (_, _) => OpenAppSettings();

        // Desktop widgets.
        AddDesktopWidgetButton.Click += (_, _) => desktopWidgets.Add();
        EditLayoutButton.Click += (_, _) => desktopWidgets.SetEditing(!desktopWidgets.Editing);
        SnapToggle.IsChecked = settings.SnapDesktopWidgets;
        SnapToggle.Checked += (_, _) => { settings.SnapDesktopWidgets = true; SaveAndNotify(); };
        SnapToggle.Unchecked += (_, _) => { settings.SnapDesktopWidgets = false; SaveAndNotify(); };
        FullscreenHideToggle.IsChecked = settings.HideDesktopWidgetsInFullscreen;
        FullscreenHideToggle.Checked += (_, _) => { settings.HideDesktopWidgetsInFullscreen = true; SaveAndNotify(); };
        FullscreenHideToggle.Unchecked += (_, _) => { settings.HideDesktopWidgetsInFullscreen = false; SaveAndNotify(); };
        desktopWidgets.WidgetsChanged += RefreshDesktopWidgets;
        desktopWidgets.EditingChanged += RefreshDesktopWidgets;
        Closed += (_, _) =>
        {
            desktopWidgets.WidgetsChanged -= RefreshDesktopWidgets;
            desktopWidgets.EditingChanged -= RefreshDesktopWidgets;
        };
        RefreshDesktopWidgets();

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

        // Font size buttons.
        FontSizeValueText.Text = settings.FontSize.ToString();
        FontSizeDecButton.Click += (_, _) => OnFontSizeChanged(-1);
        FontSizeIncButton.Click += (_, _) => OnFontSizeChanged(1);

        // Dashboard / iCUE widget opacity buttons.
        DashBgOpacityValueText.Text = $"{settings.DashboardBgOpacity}%";
        DashBgOpacityDecButton.Click += (_, _) => OnDashOpacityChanged(card: false, -10);
        DashBgOpacityIncButton.Click += (_, _) => OnDashOpacityChanged(card: false, 10);
        DashCardOpacityValueText.Text = $"{settings.DashboardCardOpacity}%";
        DashCardOpacityDecButton.Click += (_, _) => OnDashOpacityChanged(card: true, -10);
        DashCardOpacityIncButton.Click += (_, _) => OnDashOpacityChanged(card: true, 10);

        // iCUE widget text size buttons.
        WidgetTextScaleValueText.Text = $"{settings.WidgetTextScale}%";
        WidgetTextScaleDecButton.Click += (_, _) => OnWidgetTextScaleChanged(-10);
        WidgetTextScaleIncButton.Click += (_, _) => OnWidgetTextScaleChanged(10);

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
    /// Increments or decrements the overlay font size within the allowed range.
    /// </summary>
    private void OnFontSizeChanged(int delta)
    {
        settings.FontSize = Math.Clamp(settings.FontSize + delta, 8, 20);
        FontSizeValueText.Text = settings.FontSize.ToString();
        SaveAndNotify();
    }

    /// <summary>
    /// Rebuilds the metric list after the order was changed elsewhere (e.g. dragged on the Xeneon Edge).
    /// </summary>
    public void RefreshMetricList()
    {
        MetricList.Rebuild();
    }

    /// <summary>
    /// Steps the dashboard background or card opacity percentage within 0–100.
    /// </summary>
    private void OnDashOpacityChanged(bool card, int delta)
    {
        if (card)
        {
            settings.DashboardCardOpacity = Math.Clamp(settings.DashboardCardOpacity + delta, 0, 100);
            DashCardOpacityValueText.Text = $"{settings.DashboardCardOpacity}%";
        }
        else
        {
            settings.DashboardBgOpacity = Math.Clamp(settings.DashboardBgOpacity + delta, 0, 100);
            DashBgOpacityValueText.Text = $"{settings.DashboardBgOpacity}%";
        }

        SaveAndNotify();
    }

    /// <summary>
    /// Steps the iCUE widget text size percentage within 50–150.
    /// </summary>
    private void OnWidgetTextScaleChanged(int delta)
    {
        settings.WidgetTextScale = Math.Clamp(settings.WidgetTextScale + delta, 50, 150);
        WidgetTextScaleValueText.Text = $"{settings.WidgetTextScale}%";
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
    /// Shows how many widgets are paired, with a green dot when at least one is.
    /// </summary>
    public void RefreshConnectionStatus()
    {
        string? problem = getServerProblem();
        if (problem is not null)
        {
            ConnectionDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D13438"));
            ConnectionStatusText.Text = problem;
            ConnectionStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB3B3"));
            ConnectionStatusText.TextWrapping = TextWrapping.Wrap;
            DisconnectButton.Visibility = Visibility.Collapsed;
            UpdateWebServerDetailsVisibility();
            return;
        }

        int count = getTrustedWidgetCount();
        bool connected = count > 0;

        ConnectionDot.Fill = new SolidColorBrush((Color)
            ColorConverter.ConvertFromString(connected ? "#6CCB5F" : "#8E8E8E"));
        ConnectionStatusText.Text = count switch
        {
            0 => "No widget connected",
            1 => "1 widget connected",
            _ => $"{count} widgets connected"
        };
        ConnectionStatusText.Foreground = new SolidColorBrush((Color)
            ColorConverter.ConvertFromString(connected ? "#FFFFFF" : "#C8C8C8"));
        DisconnectButton.Visibility = connected ? Visibility.Visible : Visibility.Collapsed;
        UpdateWebServerDetailsVisibility();
    }

    /// <summary>
    /// Rebuilds the desktop widget rows: show/hide toggle, name, settings and remove buttons.
    /// </summary>
    private void RefreshDesktopWidgets()
    {
        DesktopWidgetList.Children.Clear();

        foreach (DesktopWidgetConfig widget in settings.DesktopWidgets)
        {
            string id = widget.Id;
            var row = new Grid { Margin = new Thickness(0, 3, 0, 3), MinHeight = 32 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var toggle = new CheckBox
            {
                Style = (Style)FindResource("ToggleSwitch"),
                IsChecked = widget.Enabled,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Show this widget"
            };
            toggle.Checked += (_, _) => desktopWidgets.SetEnabled(id, true);
            toggle.Unchecked += (_, _) => desktopWidgets.SetEnabled(id, false);
            row.Children.Add(toggle);

            var name = new TextBlock
            {
                Text = widget.Name,
                Style = (Style)FindResource("SettingLabel"),
                Margin = new Thickness(12, 0, 8, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Opacity = widget.Enabled ? 1 : 0.5
            };
            Grid.SetColumn(name, 1);
            row.Children.Add(name);

            var iconFont = (System.Windows.Media.FontFamily)FindResource("IconFont");
            var edit = new Button { Content = "\ue713", FontFamily = iconFont, Style = (Style)FindResource("SubtleButton"), Padding = new Thickness(8, 4, 8, 4), FontSize = 14, ToolTip = "Widget settings" };
            edit.Click += (_, _) => desktopWidgets.OpenEditor(id, this);
            Grid.SetColumn(edit, 2);
            row.Children.Add(edit);

            var remove = new Button { Content = "\ue74d", FontFamily = iconFont, Style = (Style)FindResource("SubtleButton"), Padding = new Thickness(8, 4, 8, 4), FontSize = 14, ToolTip = "Remove widget" };
            remove.Click += (_, _) =>
            {
                MessageBoxResult answer = System.Windows.MessageBox.Show(this, $"Remove \"{widget.Name}\"?", "SysGlance",
                    MessageBoxButton.OKCancel, MessageBoxImage.Question);
                if (answer == MessageBoxResult.OK)
                {
                    desktopWidgets.Remove(id);
                }
            };
            Grid.SetColumn(remove, 3);
            row.Children.Add(remove);

            DesktopWidgetList.Children.Add(row);
        }

        if (settings.DesktopWidgets.Count == 0)
        {
            DesktopWidgetList.Children.Add(new TextBlock
            {
                Text = "No desktop widgets yet.",
                Style = (Style)FindResource("SettingHint")
            });
        }

        EditLayoutButton.Content = desktopWidgets.Editing ? "Done editing" : "Edit layout";
        EditLayoutButton.Style = (Style)FindResource(desktopWidgets.Editing ? "AccentButton" : typeof(Button));
        EditLayoutButton.IsEnabled = settings.DesktopWidgets.Any(w => w.Enabled);
    }

    /// <summary>
    /// Opens (or focuses) the App settings window.
    /// </summary>
    private void OpenAppSettings()
    {
        if (appSettingsWindow is not null)
        {
            appSettingsWindow.Activate();
            return;
        }

        FlushPendingSaveAndNotify();
        appSettingsWindow = new AppSettingsWindow(settings, updates, SaveAndNotify) { Owner = this };
        appSettingsWindow.Closed += (_, _) => appSettingsWindow = null;
        appSettingsWindow.Show();
    }

    /// <summary>
    /// Runs the guided tour of this window. Shown once on first run; App settings can replay it.
    /// </summary>
    public void StartTour()
    {
        if (tour is not null)
        {
            return;
        }

        FlushPendingSaveAndNotify();
        tour = new Controls.TourOverlay();
        Grid.SetRowSpan(tour, 2);
        RootGrid.Children.Add(tour);
        tour.Finished += _ =>
        {
            RootGrid.Children.Remove(tour);
            tour = null;
            MainScroll.ScrollToTop();
            if (!settings.TourShown)
            {
                settings.TourShown = true;
                settings.Save();
            }
        };
        tour.Start(TourSteps(), MainScroll);
    }

    private List<Controls.TourStep> TourSteps() =>
    [
        new(null, "Welcome to SysGlance",
            "SysGlance shows your PC's temperatures, usage and FPS on your taskbar and desktop, and on a Corsair Xeneon Edge if you have one. This quick tour shows where everything is."),
        new(SensorsCard, "Pick your sensors",
            "Tick what you want to see and use the arrows to set the order. The taskbar overlay uses this list, and so does the Xeneon Edge widget if you use one."),
        new(EdgeCard, "Xeneon Edge (optional)",
            "Only for owners of a Corsair Xeneon Edge; everything else works without one. Get the SysGlance widget for iCUE, tap Connect on the Edge, then check the code that pops up here and press Trust. Once it's connected, its colours and text size show up here.",
            GetWidgetButton),
        new(DesktopWidgetsCard, "Desktop widgets",
            "Floating sensor panels for any monitor, each with its own sensors. Use Edit layout to move and resize them (they snap to screen edges and each other); the rest of the time clicks go straight through."),
        new(OverlayCard, "Taskbar overlay",
            "A compact readout docked to your taskbar. Hide it, or change its size, colour, position and monitor here."),
        new(AppSettingsButton, "App settings",
            "Start with Windows, updates, settings backup and uninstall live here. You can take this tour again from there too."),
        new(KofiButton, "You're all set",
            "SysGlance keeps running in the system tray; right-click its icon to open Settings. If you enjoy it, you can buy me a coffee here. Totally optional, always appreciated."),
    ];

    /// <summary>
    /// An update check finished; refresh App settings if it is open.
    /// </summary>
    public void RefreshUpdateStatus() => appSettingsWindow?.RefreshUpdateStatus();

    /// <summary>
    /// Forgets all paired widgets after confirmation.
    /// </summary>
    private void OnDisconnectClicked()
    {
        MessageBoxResult answer = System.Windows.MessageBox.Show(
            this,
            "Disconnect all Xeneon Edge widgets? They will show Connect again and need to be trusted again.",
            "SysGlance",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);

        if (answer == MessageBoxResult.OK)
        {
            revokeAllWidgets();
            RefreshConnectionStatus();
        }
    }

    private static void OpenUrl(string url)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    /// <summary>
    /// Widget appearance only matters once a widget is connected, so it shows only then.
    /// </summary>
    private void UpdateWebServerDetailsVisibility()
    {
        bool show = settings.WebServerEnabled && getServerProblem() is null && getTrustedWidgetCount() > 0;
        WebServerDetailsPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
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
    private void KofiButton_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl("https://ko-fi.com/frskdevelopment");
    }

    /// <summary>
    /// Closes the settings window.
    /// </summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
