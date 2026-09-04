using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace DayZLauncher.App;

/// <summary>Registers system-wide hotkeys via Win32 RegisterHotKey, so shortcuts fire even while the
/// actual DayZ client/server window has focus (a plain WPF KeyDown handler only sees keys typed into
/// this app's own window). Supports several independent hotkeys at once, each identified by an
/// arbitrary string "slot" (e.g. "emergency-stop", "start-server") - registering again for the same
/// slot replaces whatever combination it held before.</summary>
internal sealed class GlobalHotkeyManager
{
    private const int WM_HOTKEY = 0x0312;
    private const int FirstHotkeyId = 0xB00;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly Window _window;
    private readonly Dictionary<string, int> _idsBySlot = new();
    private readonly Dictionary<int, Action> _handlersById = new();
    private HwndSource? _source;
    private int _nextId = FirstHotkeyId;

    public GlobalHotkeyManager(Window window)
    {
        _window = window;
        if (window.IsInitialized && PresentationSource.FromVisual(window) is not null)
            Hook();
        else
            window.SourceInitialized += (_, _) => Hook();
    }

    private void Hook()
    {
        var handle = new WindowInteropHelper(_window).EnsureHandle();
        _source = HwndSource.FromHwnd(handle);
        _source?.AddHook(WndProc);
    }

    public void Register(string slot, ModifierKeys modifiers, Key key, Action onPressed)
    {
        Unregister(slot);
        if (_source is null) return;

        var id = _nextId++;
        try
        {
            var vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            if (!RegisterHotKey(_source.Handle, id, ToNativeModifiers(modifiers), vk)) return;

            _idsBySlot[slot] = id;
            _handlersById[id] = onPressed;
        }
        catch
        {
            // best effort - e.g. another app already owns this combination
        }
    }

    public void Unregister(string slot)
    {
        if (!_idsBySlot.Remove(slot, out var id)) return;

        _handlersById.Remove(id);
        if (_source is null) return;
        try { UnregisterHotKey(_source.Handle, id); }
        catch { /* best effort */ }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _handlersById.TryGetValue(wParam.ToInt32(), out var onPressed))
        {
            onPressed();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private static uint ToNativeModifiers(ModifierKeys modifiers)
    {
        uint result = 0;
        if (modifiers.HasFlag(ModifierKeys.Alt)) result |= 0x0001;
        if (modifiers.HasFlag(ModifierKeys.Control)) result |= 0x0002;
        if (modifiers.HasFlag(ModifierKeys.Shift)) result |= 0x0004;
        if (modifiers.HasFlag(ModifierKeys.Windows)) result |= 0x0008;
        return result;
    }
}
