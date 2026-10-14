using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace SysGlance.Services;

/// <summary>
/// Windows 11 window styling: dark frame, rounded corners and the Mica backdrop.
/// On older Windows (or if DWM refuses) the window keeps a solid dark background instead.
/// </summary>
internal static class WindowEffects
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmwcpRound = 2;
    private const int DwmsbtMainWindow = 2;      // Mica
    private const int DwmsbtTransientWindow = 3; // Acrylic, for popups
    private const int GwlStyle = -16;
    private const int WsSysMenu = 0x00080000;

    /// <summary>
    /// Solid background used when Mica isn't available.
    /// </summary>
    public static readonly SolidColorBrush FallbackBackground = new(Color.FromRgb(0x20, 0x20, 0x20));

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left, Right, Top, Bottom;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

    /// <summary>
    /// Applies the effects once the window has a handle. Use <paramref name="transient"/> for small popups
    /// (Acrylic instead of Mica). The window's XAML should use a transparent Background and WindowChrome.
    /// </summary>
    public static void Apply(Window window, bool transient = false)
    {
        if (new WindowInteropHelper(window).Handle != IntPtr.Zero)
        {
            ApplyNow(window, transient);
        }
        else
        {
            window.SourceInitialized += (_, _) => ApplyNow(window, transient);
        }
    }

    private static void ApplyNow(Window window, bool transient)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        bool backdrop = false;

        // With the frame extended into the client area, DWM draws its own caption buttons behind the content,
        // showing a second X next to ours. Without the system menu style it draws none.
        long style = GetWindowLongPtr(hwnd, GwlStyle).ToInt64();
        SetWindowLongPtr(hwnd, GwlStyle, new IntPtr(style & ~WsSysMenu));

        try
        {
            int on = 1;
            DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref on, sizeof(int));
            int corner = DwmwcpRound;
            DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref corner, sizeof(int));

            // The system backdrop attribute exists from Windows 11 22H2 (build 22621).
            if (Environment.OSVersion.Version.Build >= 22621)
            {
                HwndSource.FromHwnd(hwnd).CompositionTarget.BackgroundColor = Colors.Transparent;
                var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
                DwmExtendFrameIntoClientArea(hwnd, ref margins);
                int type = transient ? DwmsbtTransientWindow : DwmsbtMainWindow;
                backdrop = DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref type, sizeof(int)) == 0;
            }
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }

        window.Background = backdrop ? Brushes.Transparent : FallbackBackground;
    }
}
