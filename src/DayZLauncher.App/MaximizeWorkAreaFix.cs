using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DayZLauncher.App;

/// <summary>Fixes the classic WindowStyle="None" + ResizeMode="CanResize" bug where maximizing covers
/// the taskbar instead of stopping at the work area - WPF's normal "respect the taskbar" logic is
/// tied to the native non-client frame, which custom-chrome windows (like this app's) don't have.
/// Hooks WM_GETMINMAXINFO and answers it from the correct monitor's work area instead of letting
/// Windows default to the full monitor bounds.</summary>
internal static class MaximizeWorkAreaFix
{
    public static void Apply(Window window)
    {
        var hwndSource = (HwndSource?)PresentationSource.FromVisual(window);
        hwndSource?.AddHook(WndProc);
    }

    private const int WmGetMinMaxInfo = 0x0024;
    private const uint MonitorDefaultToNearest = 0x00000002;

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmGetMinMaxInfo) return IntPtr.Zero;

        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor != IntPtr.Zero)
        {
            var monitorInfo = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
            GetMonitorInfo(monitor, ref monitorInfo);

            var workArea = monitorInfo.rcWork;
            var monitorArea = monitorInfo.rcMonitor;

            var minMaxInfo = Marshal.PtrToStructure<MinMaxInfo>(lParam);
            minMaxInfo.ptMaxPosition.x = workArea.left - monitorArea.left;
            minMaxInfo.ptMaxPosition.y = workArea.top - monitorArea.top;
            minMaxInfo.ptMaxSize.x = workArea.right - workArea.left;
            minMaxInfo.ptMaxSize.y = workArea.bottom - workArea.top;
            Marshal.StructureToPtr(minMaxInfo, lParam, true);
        }

        handled = true;
        return IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point ptReserved;
        public Point ptMaxSize;
        public Point ptMaxPosition;
        public Point ptMinTrackSize;
        public Point ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int left; public int top; public int right; public int bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);
}
