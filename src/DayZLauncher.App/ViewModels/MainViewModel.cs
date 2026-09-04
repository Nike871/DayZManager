using System.Windows.Input;
using System.Windows.Threading;
using DayZLauncher.App.Mvvm;
using DayZLauncher.App.Services;
using DayZLauncher.Core.Launching;
using DayZLauncher.Core.Models;

namespace DayZLauncher.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly LauncherSettings _settings;
    private readonly GameProcessManager _processManager = new();
    private readonly DispatcherTimer _statusTimer;

    private Branch _activeBranch;
    private string _statusText = "Готово";

    public MainViewModel()
    {
        _settings = SettingsService.Load();
        _activeBranch = _settings.ActiveBranch;
        StartupRegistration.SetEnabled(_settings.StartWithWindows);

        StableProfile = new BranchProfileViewModel(Branch.Stable, _settings.Stable, _processManager, Persist, SetStatus,
            () => ClearClientLogsOnLaunch, () => ClearServerLogsOnLaunch, () => WipeServerStorageOnLaunch, () => CopyBikeyOnModAdd,
            () => ClientLaunchDelaySeconds);
        ExperimentalProfile = new BranchProfileViewModel(Branch.Experimental, _settings.Experimental, _processManager, Persist, SetStatus,
            () => ClearClientLogsOnLaunch, () => ClearServerLogsOnLaunch, () => WipeServerStorageOnLaunch, () => CopyBikeyOnModAdd,
            () => ClientLaunchDelaySeconds);

        StableProfile.ModMissingSignatureKeys += () => ModMissingSignatureKeys?.Invoke();
        ExperimentalProfile.ModMissingSignatureKeys += () => ModMissingSignatureKeys?.Invoke();
        StableProfile.ModAlreadyAdded += () => ModAlreadyAdded?.Invoke();
        ExperimentalProfile.ModAlreadyAdded += () => ModAlreadyAdded?.Invoke();

        AttachActiveProfile();

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _statusTimer.Tick += (_, _) =>
        {
            StableProfile.RefreshRunningState();
            ExperimentalProfile.RefreshRunningState();
            CommandManager.InvalidateRequerySuggested();
        };
        _statusTimer.Start();
    }

    public BranchProfileViewModel StableProfile { get; }
    public BranchProfileViewModel ExperimentalProfile { get; }
    public ServerConfigViewModel ServerConfig { get; } = new();
    public LogsViewModel Logs { get; } = new();

    public BranchProfileViewModel Profile => _activeBranch == Branch.Stable ? StableProfile : ExperimentalProfile;

    /// <summary>Raised regardless of which branch's mod list triggered it - MainWindow shows the
    /// same warning dialog either way.</summary>
    public event Action? ModMissingSignatureKeys;

    /// <summary>Raised regardless of which branch's mod list triggered it - MainWindow shows the
    /// same "already added" dialog either way.</summary>
    public event Action? ModAlreadyAdded;

    public bool IsStableActive
    {
        get => _activeBranch == Branch.Stable;
        set { if (value) SetBranch(Branch.Stable); }
    }

    public bool IsExperimentalActive
    {
        get => _activeBranch == Branch.Experimental;
        set { if (value) SetBranch(Branch.Experimental); }
    }

    public string StatusText { get => _statusText; private set => SetField(ref _statusText, value); }

    public AppTheme Theme
    {
        get => _settings.Theme;
        set
        {
            if (_settings.Theme == value) return;
            _settings.Theme = value;
            OnPropertyChanged();
            ThemeManager.Apply(value);
            Persist();
        }
    }

    public int ClientLaunchDelaySeconds
    {
        get => _settings.ClientLaunchDelaySeconds;
        set
        {
            if (_settings.ClientLaunchDelaySeconds == value) return;
            _settings.ClientLaunchDelaySeconds = value;
            OnPropertyChanged();
            Persist();
        }
    }

    public bool ClearClientLogsOnLaunch
    {
        get => _settings.ClearClientLogsOnLaunch;
        set
        {
            if (_settings.ClearClientLogsOnLaunch == value) return;
            _settings.ClearClientLogsOnLaunch = value;
            OnPropertyChanged();
            Persist();
        }
    }

    public bool ClearServerLogsOnLaunch
    {
        get => _settings.ClearServerLogsOnLaunch;
        set
        {
            if (_settings.ClearServerLogsOnLaunch == value) return;
            _settings.ClearServerLogsOnLaunch = value;
            OnPropertyChanged();
            Persist();
        }
    }

    public bool WipeServerStorageOnLaunch
    {
        get => _settings.WipeServerStorageOnLaunch;
        set
        {
            if (_settings.WipeServerStorageOnLaunch == value) return;
            _settings.WipeServerStorageOnLaunch = value;
            OnPropertyChanged();
            Persist();
        }
    }

    public bool CopyBikeyOnModAdd
    {
        get => _settings.CopyBikeyOnModAdd;
        set
        {
            if (_settings.CopyBikeyOnModAdd == value) return;
            _settings.CopyBikeyOnModAdd = value;
            OnPropertyChanged();
            Persist();
        }
    }

    public bool EmergencyStopEnabled
    {
        get => _settings.EmergencyStopEnabled;
        set
        {
            if (_settings.EmergencyStopEnabled == value) return;
            _settings.EmergencyStopEnabled = value;
            OnPropertyChanged();
            Persist();
        }
    }

    public string EmergencyStopHotkey
    {
        get => _settings.EmergencyStopHotkey;
        set
        {
            if (_settings.EmergencyStopHotkey == value) return;
            _settings.EmergencyStopHotkey = value;
            OnPropertyChanged();
            Persist();
        }
    }

    /// <summary>Force-kills every client/server process across both branches - the emergency-stop
    /// hotkey action.</summary>
    public void EmergencyStop()
    {
        _processManager.StopAll();
        StableProfile.RefreshRunningState();
        ExperimentalProfile.RefreshRunningState();
        StatusText = "Принудительная остановка: клиент и сервер остановлены.";
    }

    public bool StartServerHotkeyEnabled
    {
        get => _settings.StartServerHotkeyEnabled;
        set
        {
            if (_settings.StartServerHotkeyEnabled == value) return;
            _settings.StartServerHotkeyEnabled = value;
            OnPropertyChanged();
            Persist();
        }
    }

    public string StartServerHotkey
    {
        get => _settings.StartServerHotkey;
        set
        {
            if (_settings.StartServerHotkey == value) return;
            _settings.StartServerHotkey = value;
            OnPropertyChanged();
            Persist();
        }
    }

    public bool StartClientHotkeyEnabled
    {
        get => _settings.StartClientHotkeyEnabled;
        set
        {
            if (_settings.StartClientHotkeyEnabled == value) return;
            _settings.StartClientHotkeyEnabled = value;
            OnPropertyChanged();
            Persist();
        }
    }

    public string StartClientHotkey
    {
        get => _settings.StartClientHotkey;
        set
        {
            if (_settings.StartClientHotkey == value) return;
            _settings.StartClientHotkey = value;
            OnPropertyChanged();
            Persist();
        }
    }

    public bool MinimizeToTray
    {
        get => _settings.MinimizeToTray;
        set
        {
            if (_settings.MinimizeToTray == value) return;
            _settings.MinimizeToTray = value;
            OnPropertyChanged();
            Persist();
        }
    }

    public bool CloseToTray
    {
        get => _settings.CloseToTray;
        set
        {
            if (_settings.CloseToTray == value) return;
            _settings.CloseToTray = value;
            OnPropertyChanged();
            Persist();
        }
    }

    public bool StartWithWindows
    {
        get => _settings.StartWithWindows;
        set
        {
            if (_settings.StartWithWindows == value) return;
            _settings.StartWithWindows = value;
            OnPropertyChanged();
            StartupRegistration.SetEnabled(value);
            Persist();
        }
    }

    public bool StartMinimized
    {
        get => _settings.StartMinimized;
        set
        {
            if (_settings.StartMinimized == value) return;
            _settings.StartMinimized = value;
            OnPropertyChanged();
            Persist();
        }
    }

    /// <summary>Returns the last saved main-window bounds, or null on first run.</summary>
    public bool TryGetWindowBounds(out double left, out double top, out double width, out double height)
    {
        left = _settings.WindowLeft ?? 0;
        top = _settings.WindowTop ?? 0;
        width = _settings.WindowWidth ?? 0;
        height = _settings.WindowHeight ?? 0;
        return _settings.WindowLeft.HasValue && _settings.WindowTop.HasValue
               && _settings.WindowWidth.HasValue && _settings.WindowHeight.HasValue;
    }

    public void SaveWindowBounds(double left, double top, double width, double height)
    {
        _settings.WindowLeft = left;
        _settings.WindowTop = top;
        _settings.WindowWidth = width;
        _settings.WindowHeight = height;
        Persist();
    }

    /// <summary>Returns the last saved log-window bounds, or null on first run.</summary>
    public bool TryGetLogWindowBounds(out double left, out double top, out double width, out double height)
    {
        left = _settings.LogWindowLeft ?? 0;
        top = _settings.LogWindowTop ?? 0;
        width = _settings.LogWindowWidth ?? 0;
        height = _settings.LogWindowHeight ?? 0;
        return _settings.LogWindowLeft.HasValue && _settings.LogWindowTop.HasValue
               && _settings.LogWindowWidth.HasValue && _settings.LogWindowHeight.HasValue;
    }

    public void SaveLogWindowBounds(double left, double top, double width, double height)
    {
        _settings.LogWindowLeft = left;
        _settings.LogWindowTop = top;
        _settings.LogWindowWidth = width;
        _settings.LogWindowHeight = height;
        Persist();
    }

    public bool LogWindowMaximized
    {
        get => _settings.LogWindowMaximized;
        set
        {
            if (_settings.LogWindowMaximized == value) return;
            _settings.LogWindowMaximized = value;
            Persist();
        }
    }

    private void SetBranch(Branch branch)
    {
        if (_activeBranch == branch) return;
        _activeBranch = branch;
        _settings.ActiveBranch = branch;

        OnPropertyChanged(nameof(Profile));
        OnPropertyChanged(nameof(IsStableActive));
        OnPropertyChanged(nameof(IsExperimentalActive));

        AttachActiveProfile();
        Persist();
    }

    private void AttachActiveProfile()
    {
        ServerConfig.AttachProfile(Profile);
        Logs.AttachProfile(Profile);
    }

    private void SetStatus(string message) => StatusText = message;

    private void Persist() => SettingsService.Save(_settings);
}
