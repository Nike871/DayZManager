using Microsoft.Win32;

namespace DayZLauncher.App;

/// <summary>Registers/unregisters this app in the per-user Windows startup (Run key) - the same
/// mechanism most tray-style apps use for "launch on startup", no installer or scheduled task
/// needed. Safe to call every launch: writing the current path when enabling means it self-heals if
/// the exe was moved since it was last turned on.</summary>
internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DayZManager";

    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key is null) return;

            if (enabled)
            {
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath)) return;
                key.SetValue(ValueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // best effort - e.g. restricted registry permissions
        }
    }
}
