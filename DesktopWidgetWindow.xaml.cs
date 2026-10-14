using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using SysGlance.Controls;
using SysGlance.Services;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace SysGlance;

/// <summary>
/// A floating desktop widget. Outside edit mode it is click-through (mouse goes to whatever is underneath),
/// never takes focus, and is hidden from Alt-Tab and the taskbar. In edit mode it can be dragged and resized.
/// </summary>
public partial class DesktopWidgetWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

    private const int WmSizing = 0x0214;
    private const int WmMoving = 0x0216;
    private const int WmExitSizeMove = 0x0232;
    private const int VkControl = 0x11;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromRect(ref NativeRect rect, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int key);

    private readonly List<GaugeCard> cardPool = [];
    private readonly List<DesktopWidgetWindow> snapPartners = [];
    private bool editing;
    private bool snapHighlight;

    /// <summary>
    /// The other widgets to snap to (set by the manager).
    /// </summary>
    public Func<IEnumerable<DesktopWidgetWindow>>? SnapTargets { get; set; }

    /// <summary>
    /// Whether snapping is on (the "Snap to edges and other widgets" setting).
    /// </summary>
    public Func<bool>? SnapEnabled { get; set; }

    /// <summary>
    /// The widget's settings (edited in place by the editor window).
    /// </summary>
    public DesktopWidgetConfig Config { get; }

    /// <summary>
    /// Raised when the user moved or resized the widget (position is already written to <see cref="Config"/>).
    /// </summary>
    public event Action? PlacementChanged;

    /// <summary>
    /// ⚙ in edit mode.
    /// </summary>
    public event Action? SettingsRequested;

    /// <summary>
    /// Done in edit mode.
    /// </summary>
    public event Action? DoneRequested;

    public DesktopWidgetWindow(DesktopWidgetConfig config)
    {
        InitializeComponent();
        Config = config;

        SourceInitialized += (_, _) =>
        {
            ApplyWindowStyles();
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(WndProc);
        };
        MouseLeftButtonDown += OnMouseDown;
        LocationChanged += (_, _) => RememberPlacement();
        SizeChanged += (_, _) => RememberPlacement();
        SettingsButton.Click += (_, _) => SettingsRequested?.Invoke();
        DoneButton.Click += (_, _) => DoneRequested?.Invoke();

        ApplyPlacement();
        ApplyLook();
    }

    /// <summary>
    /// Whether edit mode is on: draggable, resizable, clickable.
    /// </summary>
    public bool Editing
    {
        get => editing;
        set
        {
            editing = value;
            EditChrome.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            ResizeMode = value ? ResizeMode.CanResizeWithGrip : ResizeMode.NoResize;
            ApplyWindowStyles();
        }
    }

    /// <summary>
    /// Re-reads colours, name, corner radius and z-order from <see cref="Config"/>.
    /// </summary>
    public void ApplyLook()
    {
        Topmost = Config.AlwaysOnTop;
        NameText.Text = Config.Name;
        Body.CornerRadius = new CornerRadius(Config.CornerRadius);
        EditOutline.RadiusX = EditOutline.RadiusY = Config.CornerRadius;

        Color bg = ParseColor(Config.BackgroundColor);
        Body.Background = new SolidColorBrush(Color.FromArgb((byte)Math.Round(2.55 * Config.BackgroundOpacity), bg.R, bg.G, bg.B));
        double pad = Math.Max(6, Config.CornerRadius * 0.6);
        double spacing = Config.Spacing switch
        {
            DesktopWidgetConfig.SpacingCompact => 0.5,
            DesktopWidgetConfig.SpacingNone => 0,
            _ => 1
        };
        Cards.Margin = new Thickness(pad * spacing);
        Cards.Gap = 8 * spacing;
    }

    /// <summary>
    /// Refreshes the cards from the latest readings. Cards are reused; each skips work when nothing changed.
    /// </summary>
    public void UpdateMetrics(SystemMetrics metrics, IMetricSelection colors)
    {
        IReadOnlyList<string> keys = Config.Metrics;

        while (cardPool.Count < keys.Count)
        {
            var card = new GaugeCard();
            cardPool.Add(card);
            Cards.Children.Add(card);
        }

        for (int i = 0; i < cardPool.Count; i++)
        {
            bool used = i < keys.Count;
            Visibility visibility = used ? Visibility.Visible : Visibility.Collapsed;
            if (cardPool[i].Visibility != visibility)
            {
                cardPool[i].Visibility = visibility;
            }

            if (used)
            {
                cardPool[i].Update(keys[i], metrics, colors.GetColor(keys[i]), Config, Config.GraphsOff.Contains(keys[i]) ? null : MetricHistory.Instance.Get(keys[i], Config.GraphMinutes));
            }
        }

        Visibility empty = keys.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (EmptyText.Visibility != empty)
        {
            EmptyText.Visibility = empty;
        }
    }

    /// <summary>
    /// Solid outline while this widget snaps, or another widget lines up with it (edit mode).
    /// </summary>
    public bool SnapHighlight
    {
        get => snapHighlight;
        set
        {
            if (snapHighlight == value)
            {
                return;
            }

            snapHighlight = value;
            EditOutline.StrokeDashArray = value ? null : new DoubleCollection { 4, 3 };
            EditOutline.StrokeThickness = value ? 3 : 2;
        }
    }

    /// <summary>
    /// Snap while moving (WM_MOVING) or resizing (WM_SIZING): Windows passes the proposed rectangle, which is
    /// adjusted in place. Holding Ctrl places freely.
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmExitSizeMove)
        {
            SetSnapPartners([]);
            SnapHighlight = false;
            return IntPtr.Zero;
        }

        if ((msg != WmMoving && msg != WmSizing) || !editing)
        {
            return IntPtr.Zero;
        }

        bool free = (GetKeyState(VkControl) & 0x8000) != 0;
        if (free || SnapEnabled?.Invoke() == false)
        {
            SetSnapPartners([]);
            SnapHighlight = false;
            return IntPtr.Zero;
        }

        var proposed = Marshal.PtrToStructure<NativeRect>(lParam);
        var box = new WidgetSnapper.Box(proposed.Left, proposed.Top, proposed.Right, proposed.Bottom);

        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromRect(ref proposed, 2 /* nearest */), ref info))
        {
            return IntPtr.Zero;
        }

        var work = new WidgetSnapper.Box(info.Work.Left, info.Work.Top, info.Work.Right, info.Work.Bottom);
        var others = new List<DesktopWidgetWindow>();
        var otherBoxes = new List<WidgetSnapper.Box>();
        foreach (DesktopWidgetWindow other in SnapTargets?.Invoke() ?? [])
        {
            IntPtr otherHwnd = new WindowInteropHelper(other).Handle;
            if (other != this && other.IsVisible && otherHwnd != IntPtr.Zero && GetWindowRect(otherHwnd, out NativeRect r))
            {
                others.Add(other);
                otherBoxes.Add(new WidgetSnapper.Box(r.Left, r.Top, r.Right, r.Bottom));
            }
        }

        double scale = GetDpiForWindow(hwnd) / 96.0;
        int Px(double dip) => (int)Math.Round(dip * (scale > 0 ? scale : 1));
        var spacing = new WidgetSnapper.Spacing(Px(5), Px(8), Px(12), Px(MinWidth), Px(MinHeight));

        WidgetSnapper.Result result = msg == WmMoving
            ? WidgetSnapper.Move(box, work, otherBoxes, spacing)
            : WidgetSnapper.Resize(box, WidgetSnapper.EdgesFromSizing(wParam.ToInt32()), work, otherBoxes, spacing);

        var snapped = new NativeRect { Left = result.Rect.Left, Top = result.Rect.Top, Right = result.Rect.Right, Bottom = result.Rect.Bottom };
        Marshal.StructureToPtr(snapped, lParam, false);
        SetSnapPartners(result.Partners.Select(i => others[i]).ToList());
        SnapHighlight = result.Rect != box;
        handled = true;
        return new IntPtr(1);
    }

    private void SetSnapPartners(List<DesktopWidgetWindow> partners)
    {
        foreach (DesktopWidgetWindow old in snapPartners.Except(partners))
        {
            old.SnapHighlight = false;
        }

        foreach (DesktopWidgetWindow partner in partners)
        {
            partner.SnapHighlight = true;
        }

        snapPartners.Clear();
        snapPartners.AddRange(partners);
    }

    private void ApplyPlacement()
    {
        Width = Config.Width;
        Height = Config.Height;

        Rect area = SystemParameters.WorkArea;
        bool placed = Config.Left.HasValue && Config.Top.HasValue;
        Rect virtualScreen = new(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);

        // New widget, or its monitor is gone: centre it on the main screen.
        if (!placed || !virtualScreen.IntersectsWith(new Rect(Config.Left!.Value, Config.Top!.Value, Config.Width, Config.Height)))
        {
            Config.Left = area.Left + (area.Width - Config.Width) / 2;
            Config.Top = area.Top + (area.Height - Config.Height) / 2;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = Config.Left!.Value;
        Top = Config.Top!.Value;
    }

    private void RememberPlacement()
    {
        if (!IsLoaded || !editing || !double.IsFinite(Left) || !double.IsFinite(Top))
        {
            return;
        }

        Config.Left = Left;
        Config.Top = Top;
        Config.Width = ActualWidth;
        Config.Height = ActualHeight;
        PlacementChanged?.Invoke();
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (editing && (e.ButtonState == MouseButtonState.Pressed))
        {
            DragMove();
        }
    }

    /// <summary>
    /// Tool window (no Alt-Tab), never activated, and click-through unless editing.
    /// </summary>
    private void ApplyWindowStyles()
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        long style = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        style |= WsExToolWindow | WsExNoActivate;
        style = editing ? style & ~WsExTransparent : style | WsExTransparent;
        SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(style));
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch (FormatException)
        {
            return Colors.Black;
        }
    }
}
