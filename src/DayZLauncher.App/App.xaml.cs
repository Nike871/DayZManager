using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using DayZLauncher.App.Services;

namespace DayZLauncher.App;

public partial class App : System.Windows.Application
{
    // Fixed GUID so this never collides with some other app's mutex of the same name.
    private const string MutexName = "Global\\DayZManager-8F2E1B3C-4D5A-4E6F-9A8B-1C2D3E4F5A6B";
    private const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            _singleInstanceMutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
            if (!createdNew)
            {
                ActivateRunningInstance();
                Shutdown();
                return;
            }
        }
        catch
        {
            // best effort - if the mutex can't even be created, just proceed without single-instance
            // protection rather than refusing to start at all
        }

        // No settings.json yet means this is either a genuine first run or a post-reset one (see
        // SettingsPanel's "Сбросить настройки и перезапустить") - checked before Load(), since Load()
        // always returns a usable LauncherSettings either way and would hide the distinction.
        var isFirstLaunch = !SettingsService.Exists();

        // Apply the saved theme before the window is created, so it never flashes the default theme.
        var settings = SettingsService.Load();
        ThemeManager.Apply(settings.Theme);

        new MainWindow(isFirstLaunch).ShowRespectingStartupPreferences();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _singleInstanceMutex?.ReleaseMutex();
        }
        catch
        {
            // best effort
        }
        _singleInstanceMutex?.Dispose();

        base.OnExit(e);
    }

    /// <summary>A DayZManager is already running (the mutex says so) - bring its window to the front
    /// instead of silently refusing to start, so launching it again still feels like it did
    /// something. Best effort: if the running instance is minimized to the tray, its window may not
    /// be found this way - it just won't be activated.</summary>
    private static void ActivateRunningInstance()
    {
        var current = Process.GetCurrentProcess();
        var existing = Process.GetProcessesByName(current.ProcessName).FirstOrDefault(p => p.Id != current.Id);
        if (existing is null) return;

        var handle = existing.MainWindowHandle;
        if (handle == IntPtr.Zero) return;

        try
        {
            ShowWindow(handle, SW_RESTORE);
            SetForegroundWindow(handle);
        }
        catch
        {
            // best effort
        }
    }
}
