using System.Diagnostics;

namespace SysGlance.Services;

/// <summary>
/// Keeps one SysGlance running. Two copies fight over the Xeneon Edge connection port (only one can listen) and
/// older versions rewrite settings.json without the newer settings (paired widgets, desktop widgets).
/// <list type="bullet">
/// <item>A named mutex marks "SysGlance is running". Starting the same exe again just tells the running copy to
/// open Settings (named event) and exits; starting a different exe (an update) closes the running copy and takes over.</item>
/// <item>Older versions don't know the mutex, so on start-up any other SysGlance process is closed.</item>
/// </list>
/// </summary>
internal static class SingleInstance
{
    private const string MutexName = @"Local\SysGlance.SingleInstance";
    private const string ShowSettingsEventName = @"Local\SysGlance.ShowSettings";

    private static Mutex? mutex;
    private static EventWaitHandle? showSettingsEvent;

    /// <summary>
    /// Claims the single-instance lock. Returns false when this version is already running; that copy has then
    /// been asked to show its Settings window and this process should exit.
    /// </summary>
    public static bool TryClaim()
    {
        mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (createdNew)
        {
            showSettingsEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSettingsEventName);
            return true;
        }

        // A different exe (e.g. a newer version downloaded elsewhere) takes over instead of deferring to the
        // running copy: close it and claim the lock once it has exited.
        if (RunningCopyIsDifferentExe())
        {
            CloseOtherCopies();
            try
            {
                if (mutex.WaitOne(TimeSpan.FromSeconds(5)))
                {
                    showSettingsEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSettingsEventName);
                    return true;
                }
            }
            catch (AbandonedMutexException)
            {
                // The old copy was killed while holding the lock; it is ours now.
                showSettingsEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSettingsEventName);
                return true;
            }
        }

        try
        {
            using EventWaitHandle running = EventWaitHandle.OpenExisting(ShowSettingsEventName);
            running.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // The other copy is still starting up; it will show itself anyway.
        }

        return false;
    }

    /// <summary>
    /// Whether another SysGlance process runs from a different exe path than this one.
    /// </summary>
    private static bool RunningCopyIsDifferentExe()
    {
        string? ownPath = Environment.ProcessPath;
        int ownId = Environment.ProcessId;

        foreach (Process other in Process.GetProcessesByName("SysGlance"))
        {
            using (other)
            {
                if (other.Id == ownId)
                {
                    continue;
                }

                try
                {
                    string? otherPath = other.MainModule?.FileName;
                    if (!string.Equals(otherPath, ownPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch (Exception)
                {
                    // Can't read its path (access denied): treat it as a different copy, so updates still win.
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Calls <paramref name="onShowRequested"/> (on a background thread) whenever another launch asks this copy
    /// to show itself. The callback must marshal to the UI thread.
    /// </summary>
    public static void ListenForShowRequests(Action onShowRequested)
    {
        if (showSettingsEvent is null)
        {
            return;
        }

        var thread = new Thread(() =>
        {
            while (showSettingsEvent.WaitOne())
            {
                onShowRequested();
            }
        })
        {
            IsBackground = true,
            Name = "SysGlance-ShowSettingsListener"
        };
        thread.Start();
    }

    /// <summary>
    /// Closes any other SysGlance process (for example an old version started from Downloads or a shortcut),
    /// politely first, then forcibly after a few seconds. Returns how many were closed.
    /// </summary>
    public static int CloseOtherCopies()
    {
        int closed = 0;
        int ownId = Environment.ProcessId;

        foreach (Process other in Process.GetProcessesByName("SysGlance"))
        {
            using (other)
            {
                if (other.Id == ownId)
                {
                    continue;
                }

                try
                {
                    other.CloseMainWindow();
                    if (!other.WaitForExit(3000))
                    {
                        other.Kill();
                        other.WaitForExit(3000);
                    }

                    closed++;
                }
                catch (Exception)
                {
                    // Already gone, or not ours to close (different user); nothing else to do.
                }
            }
        }

        return closed;
    }
}
