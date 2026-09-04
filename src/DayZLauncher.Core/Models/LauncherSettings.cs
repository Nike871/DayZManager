namespace DayZLauncher.Core.Models;

public sealed class LauncherSettings
{
    public Branch ActiveBranch { get; set; } = Branch.Stable;
    public AppTheme Theme { get; set; } = AppTheme.Gray;
    public BranchProfile Stable { get; set; } = new();
    public BranchProfile Experimental { get; set; } = new();

    /// <summary>Seconds to wait after starting the server before also starting the client, when a
    /// branch's "+" chain-launch option is on.</summary>
    public int ClientLaunchDelaySeconds { get; set; } = 5;

    /// <summary>Delete .RPT/.log files from the client's log folder right before launching it.</summary>
    public bool ClearClientLogsOnLaunch { get; set; }

    /// <summary>Delete .RPT/.log files from the server's log folder right before launching it.</summary>
    public bool ClearServerLogsOnLaunch { get; set; }

    /// <summary>Delete the current mission's storage_1 folder (player/base persistence) right
    /// before launching the server - a full server wipe.</summary>
    public bool WipeServerStorageOnLaunch { get; set; }

    /// <summary>When a mod folder is added to a mods list (the "+ Добавить мод" picker), copy its
    /// .bikey straight into the current branch's server "keys" folder, so the server accepts
    /// clients running that mod without needing a separate launch step.</summary>
    public bool CopyBikeyOnModAdd { get; set; }

    /// <summary>Global hotkey that force-kills every client/server process this app has launched,
    /// across both branches - a panic button for when the game hangs.</summary>
    public bool EmergencyStopEnabled { get; set; }

    /// <summary>Hotkey text like "Ctrl+J" - parsed by HotkeyFormat in the App layer.</summary>
    public string EmergencyStopHotkey { get; set; } = "Ctrl+J";

    /// <summary>Global hotkey that presses the active branch's "Запустить/Остановить сервер" toggle.</summary>
    public bool StartServerHotkeyEnabled { get; set; }
    public string StartServerHotkey { get; set; } = "Ctrl+Shift+S";

    /// <summary>Global hotkey that presses the active branch's "Запустить/Остановить клиент" toggle -
    /// disabled while that branch's chain-launch option is on, since the client already starts on
    /// its own in that case.</summary>
    public bool StartClientHotkeyEnabled { get; set; }
    public string StartClientHotkey { get; set; } = "Ctrl+Shift+C";

    /// <summary>Minimizing the main window hides it to the system tray instead of the taskbar.</summary>
    public bool MinimizeToTray { get; set; }

    /// <summary>Pressing the main window's own close ("✕") button hides it to the tray instead of
    /// exiting the app - only meaningful (and only exposed in the UI) while MinimizeToTray is on.</summary>
    public bool CloseToTray { get; set; }

    /// <summary>Registers/unregisters this app in the per-user Windows startup (Run key) - kept in
    /// sync with the registry every launch (see MainViewModel), so it self-heals if the exe was
    /// moved since the last time this was turned on.</summary>
    public bool StartWithWindows { get; set; }

    /// <summary>Main window opens minimized instead of normal - combines with MinimizeToTray, so
    /// with both on the app starts straight in the tray with no window flash at all.</summary>
    public bool StartMinimized { get; set; }

    // Main window position/size, remembered between runs. Null until the window has been closed
    // at least once - first run just uses the XAML defaults.
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }

    // Log window position/size, remembered between runs independently of the main window.
    public double? LogWindowLeft { get; set; }
    public double? LogWindowTop { get; set; }
    public double? LogWindowWidth { get; set; }
    public double? LogWindowHeight { get; set; }

    /// <summary>Whether the log window was maximized when last closed - the position/size fields
    /// above only ever hold its restored (non-maximized) bounds, so this is tracked separately.</summary>
    public bool LogWindowMaximized { get; set; }

    /// <summary>First-run defaults - deliberately blank. Steam auto-detection only runs when the
    /// user explicitly presses a "Найти (Steam)" button, never automatically on startup.</summary>
    public static LauncherSettings CreateDefault() => new();
}
