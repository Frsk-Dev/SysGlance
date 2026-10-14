using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace SysGlance.Services;

/// <summary>
/// Owns the floating desktop widgets: keeps a window open for each enabled <see cref="DesktopWidgetConfig"/>,
/// refreshes them once a second, switches edit mode, and hides the widgets that sit on the same monitor as a
/// full-screen app (when <see cref="AppSettings.HideDesktopWidgetsInFullscreen"/> is on); widgets on other
/// monitors stay visible. Runs on the UI thread.
/// </summary>
internal sealed class DesktopWidgetManager
{
    private readonly AppSettings settings;
    private readonly Func<SystemMetrics> getMetrics;
    private readonly Dictionary<string, DesktopWidgetWindow> windows = [];
    private readonly Dictionary<string, DesktopWidgetEditorWindow> editors = [];
    private readonly DispatcherTimer tick;
    private readonly DispatcherTimer saveDebounce;


    /// <summary>
    /// Raised when edit mode turns on or off (tray menu and Settings show it).
    /// </summary>
    public event Action? EditingChanged;

    /// <summary>
    /// Raised when widgets were added, removed, renamed or toggled (Settings refreshes its list).
    /// </summary>
    public event Action? WidgetsChanged;

    public bool Editing { get; private set; }

    public DesktopWidgetManager(AppSettings settings, Func<SystemMetrics> getMetrics)
    {
        this.settings = settings;
        this.getMetrics = getMetrics;

        tick = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        tick.Tick += (_, _) => OnTick();

        saveDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        saveDebounce.Tick += (_, _) =>
        {
            saveDebounce.Stop();
            settings.Save();
        };
    }

    /// <summary>
    /// Opens the enabled widgets and starts refreshing them.
    /// </summary>
    public void Start()
    {
        Sync();
        tick.Start();
    }

    /// <summary>
    /// Closes every widget window (app exit).
    /// </summary>
    public void Stop()
    {
        tick.Stop();
        foreach (DesktopWidgetWindow window in windows.Values.ToList())
        {
            window.Close();
        }

        windows.Clear();
    }

    /// <summary>
    /// Adds a widget with default sensors, opens it in edit mode so it can be placed, and returns it.
    /// </summary>
    public DesktopWidgetConfig Add()
    {
        var config = new DesktopWidgetConfig { Name = $"Desktop widget {settings.DesktopWidgets.Count + 1}" };
        config.Normalize();
        settings.DesktopWidgets.Add(config);
        settings.Save();
        Sync();
        SetEditing(true);
        WidgetsChanged?.Invoke();
        return config;
    }

    public void Remove(string id)
    {
        settings.DesktopWidgets.RemoveAll(w => w.Id == id);
        if (editors.TryGetValue(id, out DesktopWidgetEditorWindow? editor))
        {
            editor.Close();
        }

        settings.Save();
        Sync();
        WidgetsChanged?.Invoke();
    }

    public void SetEnabled(string id, bool enabled)
    {
        DesktopWidgetConfig? config = settings.DesktopWidgets.Find(w => w.Id == id);
        if (config is null || config.Enabled == enabled)
        {
            return;
        }

        config.Enabled = enabled;
        settings.Save();
        Sync();
        WidgetsChanged?.Invoke();
    }

    /// <summary>
    /// Turns edit mode on or off for all widgets.
    /// </summary>
    public void SetEditing(bool editing)
    {
        if (Editing == editing)
        {
            return;
        }

        Editing = editing;
        foreach (DesktopWidgetWindow window in windows.Values)
        {
            window.Editing = editing;
        }

        UpdateFullscreenVisibility();
        if (!editing)
        {
            settings.Save();
        }

        EditingChanged?.Invoke();
    }

    /// <summary>
    /// Opens (or focuses) the settings window for one widget.
    /// </summary>
    public void OpenEditor(string id, Window? owner = null)
    {
        DesktopWidgetConfig? config = settings.DesktopWidgets.Find(w => w.Id == id);
        if (config is null)
        {
            return;
        }

        if (editors.TryGetValue(id, out DesktopWidgetEditorWindow? existing))
        {
            existing.Activate();
            return;
        }

        var editor = new DesktopWidgetEditorWindow(config, settings, () =>
        {
            if (windows.TryGetValue(id, out DesktopWidgetWindow? window))
            {
                window.ApplyLook();
                window.UpdateMetrics(getMetrics(), new DesktopWidgetMetricSelection(config, settings));
            }

            WidgetsChanged?.Invoke();
            QueueSave();
        });
        if (owner is not null && owner.IsVisible)
        {
            editor.Owner = owner;
        }

        editors[id] = editor;
        editor.Closed += (_, _) => editors.Remove(id);
        editor.Show();
    }

    /// <summary>
    /// Opens windows for enabled widgets and closes the rest.
    /// </summary>
    public void Sync()
    {
        var wanted = settings.DesktopWidgets.Where(w => w.Enabled).ToDictionary(w => w.Id);

        foreach (string id in windows.Keys.Where(id => !wanted.ContainsKey(id)).ToList())
        {
            windows[id].Close();
            windows.Remove(id);
        }

        foreach (DesktopWidgetConfig config in wanted.Values)
        {
            if (windows.ContainsKey(config.Id))
            {
                continue;
            }

            var window = new DesktopWidgetWindow(config)
            {
                Editing = Editing,
                SnapTargets = () => windows.Values,
                SnapEnabled = () => settings.SnapDesktopWidgets
            };
            window.PlacementChanged += QueueSave;
            window.SettingsRequested += () => OpenEditor(config.Id);
            window.DoneRequested += () => SetEditing(false);
            windows[config.Id] = window;
            window.UpdateMetrics(getMetrics(), new DesktopWidgetMetricSelection(config, settings));
            window.Show();
            UpdateFullscreenVisibility(); // hides it right away if its monitor has a full-screen app
        }
    }

    private void QueueSave()
    {
        saveDebounce.Stop();
        saveDebounce.Start();
    }

    private void OnTick()
    {
        UpdateFullscreenVisibility();

        SystemMetrics metrics = getMetrics();
        foreach (DesktopWidgetWindow window in windows.Values)
        {
            if (window.IsVisible)
            {
                window.UpdateMetrics(metrics, new DesktopWidgetMetricSelection(window.Config, settings));
            }
        }
    }

    /// <summary>
    /// Hides each widget whose monitor has a full-screen app in front (the monitor holding most of the widget
    /// counts) and shows it again afterwards. Never hides while editing.
    /// </summary>
    private void UpdateFullscreenVisibility()
    {
        IntPtr fullscreenMonitor = settings.HideDesktopWidgetsInFullscreen && !Editing
            ? FullscreenDetector.GetFullscreenMonitor()
            : IntPtr.Zero;

        foreach (DesktopWidgetWindow window in windows.Values)
        {
            bool hide = (fullscreenMonitor != IntPtr.Zero) && (FullscreenDetector.MonitorOf(window) == fullscreenMonitor);
            if (hide && window.IsVisible)
            {
                window.Hide();
            }
            else if (!hide && !window.IsVisible)
            {
                window.Show();
            }
        }
    }
}

/// <summary>
/// Detects a full-screen app (game, video) in front: the foreground window covers its whole monitor and is
/// not the desktop, the taskbar, or one of SysGlance's own windows. Reports which monitor, so only widgets
/// on that monitor hide.
/// </summary>
internal static class FullscreenDetector
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    private static readonly HashSet<string> ShellClasses = ["Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd"];
    private static readonly uint OwnProcessId = (uint)Environment.ProcessId;

    private const uint MonitorDefaultToNearest = 2;

    /// <summary>
    /// Whether any full-screen app is in front (on any monitor).
    /// </summary>
    public static bool IsFullscreenAppInFront() => GetFullscreenMonitor() != IntPtr.Zero;

    /// <summary>
    /// The monitor covered by the full-screen app in front, or <see cref="IntPtr.Zero"/> if there is none.
    /// </summary>
    public static IntPtr GetFullscreenMonitor()
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        GetWindowThreadProcessId(hwnd, out uint processId);
        if (processId == OwnProcessId)
        {
            return IntPtr.Zero;
        }

        var className = new StringBuilder(64);
        GetClassName(hwnd, className, className.Capacity);
        if (ShellClasses.Contains(className.ToString()))
        {
            return IntPtr.Zero;
        }

        if (!GetWindowRect(hwnd, out Rect window))
        {
            return IntPtr.Zero;
        }

        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        IntPtr monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (!GetMonitorInfo(monitor, ref info))
        {
            return IntPtr.Zero;
        }

        bool coversMonitor = (window.Left <= info.Monitor.Left) && (window.Top <= info.Monitor.Top)
            && (window.Right >= info.Monitor.Right) && (window.Bottom >= info.Monitor.Bottom);
        return coversMonitor ? monitor : IntPtr.Zero;
    }

    /// <summary>
    /// The monitor a window is on: the one that holds most of it (works for hidden windows too).
    /// </summary>
    public static IntPtr MonitorOf(Window window)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        return hwnd == IntPtr.Zero ? IntPtr.Zero : MonitorFromWindow(hwnd, MonitorDefaultToNearest);
    }
}
