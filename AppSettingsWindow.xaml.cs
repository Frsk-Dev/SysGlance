using System.Windows;
using SysGlance.Services;
using Button = System.Windows.Controls.Button;

namespace SysGlance;

/// <summary>
/// App-level settings, opened from ⚙ in the Settings title bar: Start with Windows, updates, settings backup,
/// about, and uninstall. The main Settings window stays about what SysGlance shows.
/// </summary>
public partial class AppSettingsWindow : Window
{
    private const string GitHubUrl = "https://github.com/Frsk-Dev/SysGlance";
    private const string KofiUrl = "https://ko-fi.com/frskdevelopment";

    private readonly AppSettings settings;
    private readonly UpdateHooks updates;
    private readonly Action saveAndNotify;

    /// <param name="onChanged">Saves and tells the app (the port change restarts the widget server).</param>
    internal AppSettingsWindow(AppSettings appSettings, UpdateHooks updateHooks, Action onChanged)
    {
        InitializeComponent();
        WindowEffects.Apply(this);
        settings = appSettings;
        updates = updateHooks;
        saveAndNotify = onChanged;

        StartupCheckBox.IsChecked = settings.StartWithWindows;
        StartupCheckBox.Checked += (_, _) => SetStartWithWindows(true);
        StartupCheckBox.Unchecked += (_, _) => SetStartWithWindows(false);

        PortTextBox.Text = settings.WebServerPort.ToString();
        PortTextBox.TextChanged += (_, _) => OnPortTextChanged();

        AutoUpdateCheckBox.IsChecked = settings.CheckForUpdates;
        AutoUpdateCheckBox.Checked += (_, _) => { settings.CheckForUpdates = true; settings.Save(); };
        AutoUpdateCheckBox.Unchecked += (_, _) => { settings.CheckForUpdates = false; settings.Save(); };
        UpdateActionButton.Click += async (_, _) =>
        {
            if (updates.Available() is not null)
            {
                updates.ShowWindow();
                return;
            }

            UpdateActionButton.IsEnabled = false;
            UpdateStatusText.Text = "Checking…";
            await updates.CheckNow();
            UpdateActionButton.IsEnabled = true;
            RefreshUpdateStatus(justChecked: true);
        };
        RefreshUpdateStatus();

        BackupButton.Click += (_, _) => OnBackupClicked();
        RestoreButton.Click += (_, _) => OnRestoreClicked();
        OpenBackupsButton.Click += (_, _) =>
        {
            System.IO.Directory.CreateDirectory(SettingsBackup.BackupDirectory);
            Open(SettingsBackup.BackupDirectory);
        };

        AboutText.Text = $"SysGlance {UpdateService.CurrentVersion.ToString(3)} by Frsk Development";
        TourButton.Click += (_, _) =>
        {
            var owner = Owner as SettingsWindow;
            Close();
            owner?.StartTour();
        };
        GitHubButton.Click += (_, _) => Open(GitHubUrl);
        KofiButton.Click += (_, _) => Open(KofiUrl);

        UninstallButton.Click += (_, _) => OnUninstallClicked();
    }

    /// <summary>
    /// Shows the version line: up to date, or which update is available.
    /// </summary>
    public void RefreshUpdateStatus(bool justChecked = false)
    {
        UpdateService.UpdateInfo? available = updates.Available();
        string current = UpdateService.CurrentVersion.ToString(3);

        if (available is not null)
        {
            UpdateStatusText.Text = $"Version {current} · {available.Version.ToString(3)} is available";
            UpdateStatusText.Foreground = (System.Windows.Media.Brush)FindResource("AccentHoverBrush");
            UpdateActionButton.Content = "Update…";
            UpdateActionButton.Style = (Style)FindResource("AccentButton");
        }
        else
        {
            UpdateStatusText.Text = justChecked ? $"Version {current} · you're up to date" : $"Version {current}";
            UpdateStatusText.Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush");
            UpdateActionButton.Content = "Check for updates";
            UpdateActionButton.Style = (Style)FindResource(typeof(Button));
        }
    }

    /// <summary>
    /// Applies a valid port (1024-65535); an invalid one gets a red border and is not saved.
    /// </summary>
    private void OnPortTextChanged()
    {
        if (int.TryParse(PortTextBox.Text, out int port) && (port >= 1024) && (port <= 65535))
        {
            PortTextBox.ClearValue(BorderBrushProperty);
            if (port != settings.WebServerPort)
            {
                settings.WebServerPort = port;
                saveAndNotify();
            }
        }
        else
        {
            PortTextBox.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD1, 0x34, 0x38));
        }
    }

    private void SetStartWithWindows(bool enabled)
    {
        settings.StartWithWindows = enabled;
        StartupRegistration.Apply(enabled);
        settings.Save();
    }

    private void OnBackupClicked()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Back up SysGlance settings",
            FileName = $"SysGlance-settings-{DateTime.Now:yyyy-MM-dd}.json",
            Filter = "SysGlance settings (*.json)|*.json",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            SettingsBackup.Export(settings, dialog.FileName);
            System.Windows.MessageBox.Show(this, "Settings backed up.", "SysGlance", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, $"Couldn't save the backup:\n\n{ex.Message}", "SysGlance", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnRestoreClicked()
    {
        System.IO.Directory.CreateDirectory(SettingsBackup.BackupDirectory);
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Restore SysGlance settings",
            Filter = "SysGlance settings (*.json)|*.json",
            InitialDirectory = SettingsBackup.BackupDirectory
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        if (!AppSettings.TryReadFile(dialog.FileName, out _, out string? problem))
        {
            System.Windows.MessageBox.Show(this, $"Can't restore this file:\n\n{problem}", "SysGlance", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (System.Windows.MessageBox.Show(this,
                "Replace your current settings with this backup? Your current settings are backed up first, and SysGlance restarts.",
                "SysGlance", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
        {
            return;
        }

        if (SettingsBackup.TryRestore(dialog.FileName, out problem))
        {
            SettingsBackup.RestartApp();
        }
        else
        {
            System.Windows.MessageBox.Show(this, $"Can't restore this file:\n\n{problem}", "SysGlance", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnUninstallClicked()
    {
        bool deleteSettings = DeleteSettingsCheckBox.IsChecked == true;
        string what = deleteSettings
            ? "SysGlance and all its settings and backups will be deleted from this PC."
            : "SysGlance will be deleted from this PC. Your settings and backups are kept.";

        if (System.Windows.MessageBox.Show(this,
                $"Uninstall SysGlance?\n\n{what}\n\nThe SysGlance widget in iCUE has to be removed in iCUE.",
                "Uninstall SysGlance", MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel) != MessageBoxResult.OK)
        {
            return;
        }

        System.Windows.MessageBox.Show(this,
            "SysGlance will close now and finish removing itself in a few seconds. Thanks for using it!",
            "SysGlance", MessageBoxButton.OK, MessageBoxImage.Information);
        Uninstaller.UninstallAndExit(deleteSettings);
    }

    private static void Open(string target) =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(target) { UseShellExecute = true });

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
