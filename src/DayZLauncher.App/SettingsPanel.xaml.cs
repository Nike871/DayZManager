using System.Windows.Input;
using DayZLauncher.App.ViewModels;

namespace DayZLauncher.App;

/// <summary>The settings drawer's content - a plain UserControl rather than a Window, so it slides
/// in and out as part of MainWindow's own visual tree (see MainWindow.xaml's SettingsOverlay) instead
/// of living in a separate top-level HWND. DataContext is inherited straight from MainWindow (a
/// UserControl embedded in the tree gets that for free, unlike a disconnected Window/Popup root), so
/// every binding here just targets MainViewModel directly, same as before.</summary>
public partial class SettingsPanel : System.Windows.Controls.UserControl
{
    public SettingsPanel()
    {
        InitializeComponent();
    }

    private static bool TryCaptureHotkey(System.Windows.Input.KeyEventArgs e, out string formatted)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.None)
        {
            formatted = "";
            return false;
        }

        formatted = HotkeyFormat.Format(Keyboard.Modifiers, key);
        return true;
    }

    private void EmergencyStopHotkeyBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        if (TryCaptureHotkey(e, out var text) && DataContext is MainViewModel vm) vm.EmergencyStopHotkey = text;
    }

    private void StartServerHotkeyBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        if (TryCaptureHotkey(e, out var text) && DataContext is MainViewModel vm) vm.StartServerHotkey = text;
    }

    private void StartClientHotkeyBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        if (TryCaptureHotkey(e, out var text) && DataContext is MainViewModel vm) vm.StartClientHotkey = text;
    }

    private void ResetSettingsButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        // Close() runs MainWindow_Closing synchronously, which persists the in-memory settings
        // (window bounds etc.) - so it must happen BEFORE Delete(), or that save would recreate
        // settings.json with the old values right after this method erases it.
        System.Windows.Window.GetWindow(this)?.Close();

        Services.SettingsService.Delete();

        var exePath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exePath))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exePath) { UseShellExecute = true });
        }
    }
}
