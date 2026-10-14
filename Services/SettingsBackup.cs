using System.Diagnostics;
using System.IO;

namespace SysGlance.Services;

/// <summary>
/// Backups of settings.json: an automatic dated copy at every start (last <see cref="KeepAutomatic"/> kept,
/// identical copies skipped), plus manual export/import from Settings. Restoring replaces settings.json and
/// restarts SysGlance, because many parts of the app hold the loaded settings.
/// </summary>
internal static class SettingsBackup
{
    private const int KeepAutomatic = 10;
    private const string AutomaticPrefix = "settings-";

    public static string BackupDirectory => Path.Combine(AppSettings.SettingsDirectory, "backups");

    /// <summary>
    /// Copies the current settings.json into the backups folder, unless it is identical to the newest backup.
    /// Never throws: a failed backup must not stop SysGlance from starting.
    /// </summary>
    public static void CreateAutomaticBackup()
    {
        try
        {
            if (!File.Exists(AppSettings.SettingsFilePath))
            {
                return;
            }

            Directory.CreateDirectory(BackupDirectory);
            byte[] current = File.ReadAllBytes(AppSettings.SettingsFilePath);

            FileInfo? newest = AutomaticBackups().FirstOrDefault();
            if (newest is not null && File.ReadAllBytes(newest.FullName).AsSpan().SequenceEqual(current))
            {
                return;
            }

            string name = $"{AutomaticPrefix}{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json";
            File.WriteAllBytes(Path.Combine(BackupDirectory, name), current);

            foreach (FileInfo old in AutomaticBackups().Skip(KeepAutomatic))
            {
                old.Delete();
            }
        }
        catch (Exception)
        {
            // Disk full, permissions... carry on without a backup.
        }
    }

    /// <summary>
    /// Saves the current settings to <paramref name="path"/> (Back up settings…).
    /// </summary>
    public static void Export(AppSettings settings, string path)
    {
        settings.Save();
        File.Copy(AppSettings.SettingsFilePath, path, overwrite: true);
    }

    /// <summary>
    /// Replaces settings.json with a validated file. The previous settings are backed up first.
    /// </summary>
    public static bool TryRestore(string path, out string? error)
    {
        if (!AppSettings.TryReadFile(path, out _, out error))
        {
            return false;
        }

        CreateAutomaticBackup();
        AppSettings.SavingSuspended = true; // the running app must not save over the restored file on its way out
        File.Copy(path, AppSettings.SettingsFilePath, overwrite: true);
        return true;
    }

    /// <summary>
    /// Starts a fresh SysGlance that waits for this one to exit, then shuts this one down.
    /// </summary>
    public static void RestartApp()
    {
        string? exe = Environment.ProcessPath;
        if (exe is not null)
        {
            Process.Start(new ProcessStartInfo(exe, $"--after {Environment.ProcessId}") { UseShellExecute = true });
        }

        System.Windows.Application.Current.Shutdown();
    }

    private static IEnumerable<FileInfo> AutomaticBackups() =>
        new DirectoryInfo(BackupDirectory).Exists
            ? new DirectoryInfo(BackupDirectory).GetFiles(AutomaticPrefix + "*.json").OrderByDescending(f => f.Name)
            : [];
}
