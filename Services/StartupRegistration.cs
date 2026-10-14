using Microsoft.Win32;

namespace SysGlance.Services;

/// <summary>
/// "Start with Windows" via the HKCU Run key. The entry stores the exe path, so after an update to a different
/// folder Windows would keep starting the old version; <see cref="RepairIfEnabled"/> repoints it on every start.
/// </summary>
internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SysGlance";

    /// <summary>
    /// Adds or removes the startup entry for the running exe.
    /// </summary>
    public static void Apply(bool enabled)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key is null)
        {
            return;
        }

        string appPath = Environment.ProcessPath ?? "";
        if (enabled && !string.IsNullOrEmpty(appPath))
        {
            key.SetValue(ValueName, $"\"{appPath}\"");
        }
        else if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    /// <summary>
    /// When Start with Windows is on, makes sure the entry points at this exe (not an older copy elsewhere).
    /// </summary>
    public static void RepairIfEnabled(AppSettings settings)
    {
        if (!settings.StartWithWindows)
        {
            return;
        }

        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            string expected = $"\"{Environment.ProcessPath}\"";
            if (!string.Equals(key?.GetValue(ValueName) as string, expected, StringComparison.OrdinalIgnoreCase))
            {
                Apply(true);
            }
        }
        catch (Exception)
        {
            // Registry not writable; the old entry stays. Not worth failing start-up over.
        }
    }
}
