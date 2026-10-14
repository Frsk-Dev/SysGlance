using System.Diagnostics;
using System.IO;

namespace SysGlance.Services;

/// <summary>
/// Removes SysGlance from this PC. SysGlance is a single portable exe, so "uninstall" means:
/// remove the Start with Windows entry, optionally delete settings and backups (%APPDATA%\SysGlance), and,
/// after the app has exited, delete the exe (plus the updater's leftovers and the .NET single-file
/// extraction folder) with a small script, because a running program can't delete itself.
/// The iCUE widget is managed by iCUE and has to be removed there.
/// </summary>
internal static class Uninstaller
{
    /// <summary>
    /// Uninstalls and exits. Does not return.
    /// </summary>
    public static void UninstallAndExit(bool deleteSettings)
    {
        StartupRegistration.Apply(false);

        // From here on nothing may write settings.json again (closing windows would otherwise save).
        AppSettings.SavingSuspended = true;
        if (deleteSettings)
        {
            TryDeleteDirectory(AppSettings.SettingsDirectory);
        }

        string exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "SysGlance.exe");
        string script = WriteRemovalScript(Environment.ProcessId, exe, LeftoverFolders());
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{script}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetTempPath()
        });

        System.Windows.Application.Current?.Shutdown();
        Environment.Exit(0);
    }

    /// <summary>
    /// Temporary folders SysGlance creates: updater downloads and the .NET single-file extraction folder.
    /// </summary>
    internal static IEnumerable<string> LeftoverFolders()
    {
        string temp = Path.GetTempPath();
        yield return Path.Combine(temp, "SysGlance-update");
        yield return Path.Combine(temp, ".net", "SysGlance");
    }

    /// <summary>
    /// Writes the script that waits for <paramref name="pid"/> to exit (up to ~30 s), then deletes the exe, the
    /// updater's ".old" copy and <paramref name="folders"/>, and finally itself. Returns the script path.
    /// </summary>
    internal static string WriteRemovalScript(int pid, string exe, IEnumerable<string> folders)
    {
        string scriptFolder = Path.Combine(Path.GetTempPath(), "SysGlance-uninstall");
        Directory.CreateDirectory(scriptFolder);
        string script = Path.Combine(scriptFolder, "remove-sysglance.cmd");
        string removeFolders = string.Join(Environment.NewLine,
            folders.Select(f => $"if exist \"{f}\" rmdir /s /q \"{f}\" >nul 2>&1"));

        File.WriteAllText(script, $"""
            @echo off
            setlocal
            set /a tries=0
            :wait
            "%SystemRoot%\System32\tasklist.exe" /FI "PID eq {pid}" /NH 2>nul | "%SystemRoot%\System32\find.exe" " {pid} " >nul
            if errorlevel 1 goto gone
            set /a tries+=1
            if %tries% geq 30 goto gone
            rem ping as a 1 s sleep: "timeout" fails without a console in a hidden window
            "%SystemRoot%\System32\PING.EXE" -n 2 127.0.0.1 >nul
            goto wait
            :gone
            del /f /q "{exe}" >nul 2>&1
            del /f /q "{exe}.old" >nul 2>&1
            {removeFolders}
            (goto) 2>nul & rmdir /s /q "{scriptFolder}"
            """);
        return script;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception)
        {
            // Something holds a file open; the rest of the uninstall still goes ahead.
        }
    }
}
