using System.Windows;

namespace DayZLauncher.App;

/// <summary>Shared clamp-to-screen restore/capture logic for remembering a window's position and
/// size between runs - used by both the main window and the log window.</summary>
internal static class WindowBoundsHelper
{
    public static void Restore(Window window, double? left, double? top, double? width, double? height)
    {
        if (left is null || top is null || width is null || height is null) return;
        if (width < 200 || height < 200) return;

        // Keep the window on-screen even if it was last saved on a monitor that's since been
        // disconnected or had its resolution changed.
        var maxLeft = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 100;
        var maxTop = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 100;
        window.Left = Math.Max(SystemParameters.VirtualScreenLeft, Math.Min(left.Value, maxLeft));
        window.Top = Math.Max(SystemParameters.VirtualScreenTop, Math.Min(top.Value, maxTop));
        window.Width = width.Value;
        window.Height = height.Value;
    }

    public static Rect CaptureBounds(Window window) =>
        window.WindowState == WindowState.Normal ? new Rect(window.Left, window.Top, window.Width, window.Height) : window.RestoreBounds;
}
