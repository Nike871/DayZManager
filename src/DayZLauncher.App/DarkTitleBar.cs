using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DayZLauncher.App;

/// <summary>Makes the native OS window chrome follow the app's chosen theme (Gray/White) via DWM:
/// the title bar text/background (immersive dark mode), the thin accent-colored border Windows 11
/// draws around the window edge (which otherwise defaults to the user's system accent color - often
/// a light color that clashes badly with a near-black app), and square (non-rounded) corners instead
/// of Windows 11's default rounding - the whole app goes for a flat, square look, not just the
/// title bar. Best effort - silently does nothing on Windows versions that don't support these
/// attributes.</summary>
internal static class DarkTitleBar
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaWindowCornerPreference = 33;
    private const uint DwmwaColorNone = 0xFFFFFFFE;
    private const int DwmwcpDoNotRound = 1;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref uint value, int valueSize);

    public static void Apply(Window window, bool useDark)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).EnsureHandle();

            var darkMode = useDark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref darkMode, sizeof(int));

            // No accent border color at all, instead of whatever light color the user's Windows
            // accent happens to be - that's what was showing up as a stray white frame.
            var noBorder = DwmwaColorNone;
            DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref noBorder, sizeof(uint));

            var squareCorners = DwmwcpDoNotRound;
            DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref squareCorners, sizeof(int));
        }
        catch
        {
            // unsupported OS version - the window still works, just with default chrome
        }
    }
}
