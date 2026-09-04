using System.IO;
using System.Linq;
using DayZLauncher.App.Mvvm;
using DayZLauncher.Core.Config;
using DayZLauncher.Core.Diagnostics;
using DayZLauncher.Core.Launching;
using DayZLauncher.Core.Logs;
using DayZLauncher.Core.Missions;
using DayZLauncher.Core.Models;
using DayZLauncher.Core.Mods;
using DayZLauncher.Core.Steam;

namespace DayZLauncher.App.ViewModels;

/// <summary>Bindable wrapper around one <see cref="BranchProfile"/> (Stable or Experimental).
/// Every setter writes straight through to the model, refreshes the command-line preview and asks
/// the owner to persist settings - there is no separate "apply"/"save" step for these fields.</summary>
public sealed class BranchProfileViewModel : ObservableObject
{
    private readonly BranchProfile _model;
    private readonly GameProcessManager _processManager;
    private readonly Action _persist;
    private readonly Action<string> _setStatus;
    private readonly Func<bool> _getClearClientLogs;
    private readonly Func<bool> _getClearServerLogs;
    private readonly Func<bool> _getWipeServer;
    private readonly Func<bool> _getCopyBikeyOnModAdd;
    private readonly Func<int> _getClientDelaySeconds;

    private bool _isClientRunning;
    private bool _isServerRunning;
    private string _clientArgsPreview = "";
    private string _serverArgsPreview = "";

    public BranchProfileViewModel(Branch branch, BranchProfile model, GameProcessManager processManager,
        Action persist, Action<string> setStatus,
        Func<bool> getClearClientLogs, Func<bool> getClearServerLogs, Func<bool> getWipeServer, Func<bool> getCopyBikeyOnModAdd,
        Func<int> getClientDelaySeconds)
    {
        Branch = branch;
        _model = model;
        _processManager = processManager;
        _persist = persist;
        _setStatus = setStatus;
        _getClearClientLogs = getClearClientLogs;
        _getClearServerLogs = getClearServerLogs;
        _getWipeServer = getWipeServer;
        _getCopyBikeyOnModAdd = getCopyBikeyOnModAdd;
        _getClientDelaySeconds = getClientDelaySeconds;

        ModsList = new ModListViewModel(() => Mods, v => Mods = v, OnModAdded, OnModRemoved, OnMissingKeys, OnDuplicateMod);
        // No missing-keys warning here - unlike ModsList/-mod, -serverMod entries only ever run on
        // the server, so the client never needs a signature key for them.
        ServerModsList = new ModListViewModel(() => ServerMods, v => ServerMods = v, OnModAdded, OnModRemoved, onDuplicate: OnDuplicateMod);

        BrowseClientExeCommand = new RelayCommand(() => BrowseFile(p => ClientExePath = p, ClientExePath, "Клиент DayZ (*.exe)|*.exe"));
        BrowseServerExeCommand = new RelayCommand(() => BrowseFile(p => ServerExePath = p, ServerExePath, "Сервер DayZ (*.exe)|*.exe"));
        BrowseServerConfigCommand = new RelayCommand(() => BrowseFile(p => ServerConfigPath = p, ServerConfigPath, "Конфиг сервера (*.cfg)|*.cfg|Все файлы (*.*)|*.*"));
        BrowseServerProfilesCommand = new RelayCommand(() => BrowseFolder(p => ServerProfilesPath = p, ServerProfilesPath));
        OpenServerProfilesFolderCommand = new RelayCommand(OpenServerProfilesFolder);
        DetectSteamCommand = new RelayCommand(DetectSteam);
        DetectServerSteamCommand = new RelayCommand(DetectServerSteam);

        ToggleServerCommand = new RelayCommand(ToggleServer);
        ToggleClientCommand = new RelayCommand(ToggleClient);

        // Covers both a fresh profile and an existing one saved before this setting existed (where
        // it defaults to on but ConnectIp/ConnectPort may still hold old manual values) - each setter
        // already no-ops if the value already matches, so this is harmless on an already-synced profile.
        if (UseLocalServerConnect) ApplyLocalServerConnect();

        RefreshPreviews();
    }

    public Branch Branch { get; }
    public string BranchName => Branch == Branch.Stable ? "Stable" : "Experimental";

    // ---- Paths ----
    public string ClientExePath
    {
        get => _model.ClientExePath;
        set { if (_model.ClientExePath == value) return; _model.ClientExePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(ClientVersion)); Changed(); }
    }

    public string ServerExePath
    {
        get => _model.ServerExePath;
        set { if (_model.ServerExePath == value) return; _model.ServerExePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(ServerVersion)); Changed(); }
    }

    /// <summary>Read straight off DayZ_x64.exe next to the configured client path, not the configured
    /// path itself - that's commonly DayZ_BE.exe (the BattlEye-wrapped launcher), which doesn't carry
    /// the game's real version resource.</summary>
    public string? ClientVersion => GameVersionReader.GetVersion(ClientExePath, "DayZ_x64.exe");

    public string? ServerVersion => GameVersionReader.GetVersion(ServerExePath, "DayZServer_x64.exe");

    public string ServerConfigPath
    {
        get => _model.ServerConfigPath;
        set { if (_model.ServerConfigPath == value) return; _model.ServerConfigPath = value; OnPropertyChanged(); Changed(); }
    }

    public string ServerProfilesPath
    {
        get => _model.ServerProfilesPath;
        set { if (_model.ServerProfilesPath == value) return; _model.ServerProfilesPath = value; OnPropertyChanged(); Changed(); }
    }

    // ---- Mods ----
    public string Mods
    {
        get => _model.Mods;
        set { if (_model.Mods == value) return; _model.Mods = value; OnPropertyChanged(); Changed(); }
    }

    public string ServerMods
    {
        get => _model.ServerMods;
        set { if (_model.ServerMods == value) return; _model.ServerMods = value; OnPropertyChanged(); Changed(); }
    }

    /// <summary>List view over <see cref="Mods"/> for the "+" add-via-folder-picker UI.</summary>
    public ModListViewModel ModsList { get; }

    /// <summary>List view over <see cref="ServerMods"/> for the "+" add-via-folder-picker UI.</summary>
    public ModListViewModel ServerModsList { get; }

    // ---- Network / performance ----
    public int Port
    {
        get => _model.Port;
        set
        {
            if (_model.Port == value) return;
            _model.Port = value;
            OnPropertyChanged();
            if (UseLocalServerConnect) ConnectPort = value.ToString();
            Changed();
        }
    }

    public int CpuCount
    {
        get => _model.CpuCount;
        set { if (_model.CpuCount == value) return; _model.CpuCount = value; OnPropertyChanged(); Changed(); }
    }

    public int LimitFps
    {
        get => _model.LimitFps;
        set { if (_model.LimitFps == value) return; _model.LimitFps = value; OnPropertyChanged(); Changed(); }
    }

    // ---- Client options ----
    public bool ClientNoSplash { get => _model.ClientNoSplash; set { if (_model.ClientNoSplash == value) return; _model.ClientNoSplash = value; OnPropertyChanged(); Changed(); } }
    public bool ClientSkipIntro { get => _model.ClientSkipIntro; set { if (_model.ClientSkipIntro == value) return; _model.ClientSkipIntro = value; OnPropertyChanged(); Changed(); } }
    public bool ClientNoPause { get => _model.ClientNoPause; set { if (_model.ClientNoPause == value) return; _model.ClientNoPause = value; OnPropertyChanged(); Changed(); } }
    public bool ClientWindow { get => _model.ClientWindow; set { if (_model.ClientWindow == value) return; _model.ClientWindow = value; OnPropertyChanged(); Changed(); } }
    public bool ClientScriptDebug { get => _model.ClientScriptDebug; set { if (_model.ClientScriptDebug == value) return; _model.ClientScriptDebug = value; OnPropertyChanged(); Changed(); } }
    public bool ClientWorldEmpty { get => _model.ClientWorldEmpty; set { if (_model.ClientWorldEmpty == value) return; _model.ClientWorldEmpty = value; OnPropertyChanged(); Changed(); } }
    public string ClientProfileName { get => _model.ClientProfileName; set { if (_model.ClientProfileName == value) return; _model.ClientProfileName = value; OnPropertyChanged(); Changed(); } }

    // ---- Server diagnostics ----
    public bool ServerDoLogs { get => _model.ServerDoLogs; set { if (_model.ServerDoLogs == value) return; _model.ServerDoLogs = value; OnPropertyChanged(); Changed(); } }
    public bool ServerAdminLog { get => _model.ServerAdminLog; set { if (_model.ServerAdminLog == value) return; _model.ServerAdminLog = value; OnPropertyChanged(); Changed(); } }
    public bool ServerNetLog { get => _model.ServerNetLog; set { if (_model.ServerNetLog == value) return; _model.ServerNetLog = value; OnPropertyChanged(); Changed(); } }
    public bool ServerScriptDebug { get => _model.ServerScriptDebug; set { if (_model.ServerScriptDebug == value) return; _model.ServerScriptDebug = value; OnPropertyChanged(); Changed(); } }
    public bool ServerFilePatching { get => _model.ServerFilePatching; set { if (_model.ServerFilePatching == value) return; _model.ServerFilePatching = value; OnPropertyChanged(); Changed(); } }
    public bool ServerFreezeCheck { get => _model.ServerFreezeCheck; set { if (_model.ServerFreezeCheck == value) return; _model.ServerFreezeCheck = value; OnPropertyChanged(); Changed(); } }
    public string ServerBEPath { get => _model.ServerBEPath; set { if (_model.ServerBEPath == value) return; _model.ServerBEPath = value; OnPropertyChanged(); Changed(); } }
    public string ServerStoragePath { get => _model.ServerStoragePath; set { if (_model.ServerStoragePath == value) return; _model.ServerStoragePath = value; OnPropertyChanged(); Changed(); } }

    // ---- Direct connect (client) ----
    public string ConnectIp { get => _model.ConnectIp; set { if (_model.ConnectIp == value) return; _model.ConnectIp = value; OnPropertyChanged(); Changed(); } }
    public string ConnectPort { get => _model.ConnectPort; set { if (_model.ConnectPort == value) return; _model.ConnectPort = value; OnPropertyChanged(); Changed(); } }
    public string ConnectPassword { get => _model.ConnectPassword; set { if (_model.ConnectPassword == value) return; _model.ConnectPassword = value; OnPropertyChanged(); Changed(); } }

    /// <summary>The "Локальный сервер" toggle next to the direct-connect fields - on by default,
    /// covering the common case of testing against the server this same profile launches. Writes
    /// "localhost"/the current Port straight into ConnectIp/ConnectPort (kept in sync live if Port
    /// changes afterwards - see the Port setter) rather than just overriding what's displayed, so
    /// ArgumentBuilder - which reads the model directly, not through this view model - launches with
    /// the same address the UI shows. IP/Port are read-only in the UI while this is on; Password
    /// stays free to edit either way.</summary>
    public bool UseLocalServerConnect
    {
        get => _model.UseLocalServerConnect;
        set
        {
            if (_model.UseLocalServerConnect == value) return;
            _model.UseLocalServerConnect = value;
            OnPropertyChanged();
            if (value) ApplyLocalServerConnect();
            Changed();
        }
    }

    private void ApplyLocalServerConnect()
    {
        ConnectIp = "localhost";
        ConnectPort = Port.ToString();
    }

    // ---- Extra args ----
    public string ExtraClientArgs { get => _model.ExtraClientArgs; set { if (_model.ExtraClientArgs == value) return; _model.ExtraClientArgs = value; OnPropertyChanged(); Changed(); } }
    public string ExtraServerArgs { get => _model.ExtraServerArgs; set { if (_model.ExtraServerArgs == value) return; _model.ExtraServerArgs = value; OnPropertyChanged(); Changed(); } }

    /// <summary>The "+" toggle between the server/client launch buttons - when on, starting the
    /// server also starts the client after the configured delay.</summary>
    public bool ChainClientAfterServerLaunch { get => _model.ChainClientAfterServerLaunch; set { if (_model.ChainClientAfterServerLaunch == value) return; _model.ChainClientAfterServerLaunch = value; OnPropertyChanged(); OnPropertyChanged(nameof(ClientToggleEnabled)); Changed(); } }

    // ---- Live command-line preview ----
    public string ClientArgsPreview { get => _clientArgsPreview; private set => SetField(ref _clientArgsPreview, value); }
    public string ServerArgsPreview { get => _serverArgsPreview; private set => SetField(ref _serverArgsPreview, value); }

    // ---- Running state ----
    public bool IsClientRunning
    {
        get => _isClientRunning;
        private set
        {
            if (!SetField(ref _isClientRunning, value)) return;
            OnPropertyChanged(nameof(ClientStatusText));
            OnPropertyChanged(nameof(ClientToggleLabel));
            OnPropertyChanged(nameof(ClientRunningTag));
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(ClientToggleEnabled));
        }
    }

    public bool IsServerRunning
    {
        get => _isServerRunning;
        private set
        {
            if (!SetField(ref _isServerRunning, value)) return;
            OnPropertyChanged(nameof(ServerStatusText));
            OnPropertyChanged(nameof(ServerToggleLabel));
            OnPropertyChanged(nameof(ServerRunningTag));
            OnPropertyChanged(nameof(IsBusy));
        }
    }

    /// <summary>True while either the client or the server is running - both the "Клиент" and
    /// "Сервер" tabs lock and dim on this together rather than each tracking its own process, so
    /// starting either one locks every setting for this branch, not just the half that's running.</summary>
    public bool IsBusy => IsClientRunning || IsServerRunning;

    public string ClientStatusText => IsClientRunning ? "запущен" : "не запущен";
    public string ServerStatusText => IsServerRunning ? "запущен" : "не запущен";

    public string ServerToggleLabel => IsServerRunning ? "Остановить сервер" : "Запустить сервер";
    public string ClientToggleLabel => IsClientRunning ? "Остановить клиент" : "Запустить клиент";

    /// <summary>The "Запустить клиент" button is disabled while chain-launch is on, since the client
    /// starts on its own with the server - but once it's actually running (whether chain-launched or
    /// started some other way), the button must switch to "Остановить клиент" and be clickable, or
    /// there'd be no way to stop it short of the emergency-stop hotkey.</summary>
    public bool ClientToggleEnabled => !ChainClientAfterServerLaunch || IsClientRunning;

    /// <summary>Drives the StartStopButtonStyle hover-color trigger via Button.Tag. A plain bool
    /// bound through Tag (typed object) doesn't reliably compare against Value="True"/"False" in a
    /// ControlTemplate trigger - a string does, so this exists purely for that binding.</summary>
    public string ServerRunningTag => IsServerRunning ? "Running" : "Stopped";
    public string ClientRunningTag => IsClientRunning ? "Running" : "Stopped";

    /// <summary>Where this branch's server writes its logs - shared with the log viewer so both
    /// use exactly the same resolution rule (explicit -profiles path, else "&lt;server&gt;\profiles").</summary>
    public string GetServerLogDirectory() => DayZPaths.GetServerLogDirectory(_model);

    /// <summary>Checks both processes this manager itself launched and, separately, whether a copy is
    /// running from the same install folder despite this manager never starting it - e.g. launched
    /// straight from Steam, or left running from before the app was restarted. Either one counts as
    /// "running" for the status display and for locking the settings fields while in use.</summary>
    public void RefreshRunningState()
    {
        IsClientRunning = _processManager.IsRunning(ClientKey) || GameProcessManager.IsExternalClientRunning(ClientExePath);
        IsServerRunning = _processManager.IsRunning(ServerKey) || GameProcessManager.IsExternalServerRunning(ServerExePath);
    }

    // ---- Commands ----
    public RelayCommand BrowseClientExeCommand { get; }
    public RelayCommand BrowseServerExeCommand { get; }
    public RelayCommand BrowseServerConfigCommand { get; }
    public RelayCommand BrowseServerProfilesCommand { get; }
    public RelayCommand OpenServerProfilesFolderCommand { get; }
    public RelayCommand DetectSteamCommand { get; }
    public RelayCommand DetectServerSteamCommand { get; }
    public RelayCommand ToggleServerCommand { get; }
    public RelayCommand ToggleClientCommand { get; }

    private string ClientKey => $"{Branch}-client";
    private string ServerKey => $"{Branch}-server";

    private void ToggleServer()
    {
        if (IsServerRunning) { StopServer(); return; }
        if (ChainClientAfterServerLaunch) _ = LaunchServerThenClientAsync();
        else LaunchServer();
    }

    private void ToggleClient()
    {
        if (IsClientRunning) StopClient();
        else LaunchClient();
    }

    private async Task LaunchServerThenClientAsync()
    {
        LaunchServer();
        if (!IsServerRunning) return; // server failed to start - don't chain into the client

        var delaySeconds = Math.Max(0, _getClientDelaySeconds());
        await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
        if (!IsClientRunning) LaunchClient();
    }

    private void LaunchClient()
    {
        try
        {
            if (_getClearClientLogs()) LogFileService.DeleteLogFiles(DayZPaths.GetClientLogDirectory(Branch));

            _processManager.Start(ClientKey, ClientExePath, ArgumentBuilder.BuildClientArgs(_model));
            IsClientRunning = true;
            _setStatus($"[{BranchName}] Клиент запущен.");
        }
        catch (Exception ex)
        {
            _setStatus($"[{BranchName}] Ошибка запуска клиента: {ex.Message}");
        }
    }

    private void StopClient()
    {
        _processManager.StopClient(ClientKey);
        IsClientRunning = false;
        _setStatus($"[{BranchName}] Клиент остановлен.");
    }

    private void LaunchServer()
    {
        try
        {
            if (_getClearServerLogs()) LogFileService.DeleteLogFiles(DayZPaths.GetServerLogDirectory(_model));
            if (_getWipeServer()) WipeServerStorage();

            _processManager.Start(ServerKey, ServerExePath, ArgumentBuilder.BuildServerArgs(_model));
            IsServerRunning = true;
            _setStatus($"[{BranchName}] Сервер запущен.");
        }
        catch (Exception ex)
        {
            _setStatus($"[{BranchName}] Ошибка запуска сервера: {ex.Message}");
        }
    }

    private void StopServer()
    {
        _processManager.StopServer(ServerKey);
        IsServerRunning = false;
        _setStatus($"[{BranchName}] Сервер остановлен.");
    }

    /// <summary>Deletes the currently selected mission's storage_1 folder (player/base persistence) -
    /// a full server wipe. Reads the mission name straight from serverDZ.cfg rather than depending
    /// on the Server Config tab's view-model, so this works standalone at launch time.</summary>
    private void WipeServerStorage()
    {
        if (string.IsNullOrWhiteSpace(ServerConfigPath) || !File.Exists(ServerConfigPath)) return;

        var mission = ServerConfigDocument.Load(ServerConfigPath).GetMissionTemplate();
        if (string.IsNullOrWhiteSpace(mission)) return;

        var mpMissionsDir = MissionScanner.GetMpMissionsDirectory(ServerExePath);
        if (mpMissionsDir is null) return;

        var storageDir = Path.Combine(mpMissionsDir, mission, "storage_1");
        if (Directory.Exists(storageDir))
        {
            try { Directory.Delete(storageDir, recursive: true); }
            catch { /* best effort */ }
        }
    }

    /// <summary>Called right after a mod folder is added via either mods list's "+" picker - if the
    /// setting is on, copies that one mod's .bikey (if any) into the server's keys folder straight
    /// away, instead of waiting for the next server launch. "@WorkshopName" entries never reach here
    /// since the picker only ever adds real folders.</summary>
    private void OnModAdded(string modPath)
    {
        if (!_getCopyBikeyOnModAdd()) return;
        if (string.IsNullOrWhiteSpace(ServerExePath)) return;

        var serverDir = Path.GetDirectoryName(Path.GetFullPath(ServerExePath));
        if (serverDir is null) return;

        ModKeyCopier.CopyModKeys(new[] { modPath }, serverDir);
        _setStatus($"[{BranchName}] Ключ мода «{Path.GetFileName(modPath.TrimEnd('\\', '/'))}» скопирован на сервер (если найден).");
    }

    /// <summary>Called right after a mod folder is removed from either mods list - if the setting is
    /// on, deletes that mod's .bikey (if any) from the server's keys folder, mirroring
    /// <see cref="OnModAdded"/>. "@WorkshopName" entries never reach here for the same reason.</summary>
    private void OnModRemoved(string modPath)
    {
        if (!_getCopyBikeyOnModAdd()) return;
        if (string.IsNullOrWhiteSpace(ServerExePath)) return;

        var serverDir = Path.GetDirectoryName(Path.GetFullPath(ServerExePath));
        if (serverDir is null) return;

        ModKeyCopier.RemoveModKeys(new[] { modPath }, serverDir);
        _setStatus($"[{BranchName}] Ключ мода «{Path.GetFileName(modPath.TrimEnd('\\', '/'))}» удалён с сервера (если был).");
    }

    /// <summary>Raised when a mod folder just added via either mods list's "+" picker has no "keys"
    /// subfolder at all - MainWindow shows the warning dialog for this, since a server with
    /// verifySignatures on will reject clients running an unsigned mod.</summary>
    public event Action? ModMissingSignatureKeys;

    private void OnMissingKeys(string modPath) => ModMissingSignatureKeys?.Invoke();

    /// <summary>Raised instead of adding anything when the folder picked in either mods list's "+"
    /// picker is already in that list.</summary>
    public event Action? ModAlreadyAdded;

    private void OnDuplicateMod() => ModAlreadyAdded?.Invoke();

    private void DetectSteam()
    {
        var result = SteamInstallDetector.Detect();
        var found = Branch == Branch.Stable ? result.StableClientExe : result.ExperimentalClientExe;
        if (found is null)
        {
            _setStatus($"[{BranchName}] Установка Steam не найдена автоматически - укажите путь вручную.");
            return;
        }

        ClientExePath = found;
        _setStatus($"[{BranchName}] Найдено: {found}");
    }

    private void DetectServerSteam()
    {
        var result = SteamInstallDetector.Detect();
        var found = Branch == Branch.Stable ? result.StableServerExe : result.ExperimentalServerExe;
        if (found is null)
        {
            _setStatus($"[{BranchName}] Установка сервера Steam не найдена автоматически - укажите путь вручную.");
            return;
        }

        ServerExePath = found;

        var serverDir = Path.GetDirectoryName(found);
        if (serverDir is not null)
        {
            var cfgPath = Path.Combine(serverDir, "serverDZ.cfg");
            if (File.Exists(cfgPath)) ServerConfigPath = cfgPath;

            var profilesPath = Path.Combine(serverDir, "profiles");
            if (Directory.Exists(profilesPath)) ServerProfilesPath = profilesPath;
        }

        _setStatus($"[{BranchName}] Найдено: {found}");
    }

    private void Changed()
    {
        RefreshPreviews();
        _persist();
    }

    private void RefreshPreviews()
    {
        ClientArgsPreview = ArgumentBuilder.ToDisplayString(ArgumentBuilder.BuildClientArgs(_model));
        ServerArgsPreview = ArgumentBuilder.ToDisplayString(ArgumentBuilder.BuildServerArgs(_model));
    }

    private void OpenServerProfilesFolder()
    {
        if (string.IsNullOrWhiteSpace(ServerProfilesPath) || !Directory.Exists(ServerProfilesPath))
        {
            _setStatus($"[{BranchName}] Папка profiles не найдена: {ServerProfilesPath}");
            return;
        }

        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ServerProfilesPath) { UseShellExecute = true }); }
        catch { /* best effort */ }
    }

    private static void BrowseFile(Action<string> setPath, string currentPath, string filter)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = filter, Title = "Выберите файл" };
        if (!string.IsNullOrWhiteSpace(currentPath) && File.Exists(currentPath))
            dlg.InitialDirectory = Path.GetDirectoryName(currentPath);

        if (dlg.ShowDialog() == true)
            setPath(dlg.FileName);
    }

    private static void BrowseFolder(Action<string> setPath, string currentPath)
    {
        using var dlg = new System.Windows.Forms.FolderBrowserDialog();
        if (!string.IsNullOrWhiteSpace(currentPath) && Directory.Exists(currentPath))
            dlg.SelectedPath = currentPath;

        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            setPath(dlg.SelectedPath);
    }
}
