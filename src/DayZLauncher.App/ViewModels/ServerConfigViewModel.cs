using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using DayZLauncher.App.Mvvm;
using DayZLauncher.Core.Config;
using DayZLauncher.Core.Missions;

namespace DayZLauncher.App.ViewModels;

/// <summary>Quick-edit view over one serverDZ.cfg (+ its mission template), shown inside the
/// "Сервер" tab. Every quick field, the mission radio buttons and the raw text box write through
/// the same <see cref="ServerConfigDocument"/>, so editing any one of them keeps the others in
/// sync. <see cref="_isSyncing"/> guards against that sync loop writing back into the document
/// while it is only pushing the document's own values out into the quick-field properties.</summary>
public sealed class ServerConfigViewModel : ObservableObject
{
    private BranchProfileViewModel? _profile;
    private ServerConfigDocument? _document;
    private bool _isSyncing;

    private string _rawText = "";
    private string _loadStatus = "Конфиг не загружен.";
    private string _currentTemplate = "";

    // Input fields: every one is int?/string so an absent cfg key can render as a genuinely empty
    // box instead of a guessed default. A blank box therefore always means "not in serverDZ.cfg yet".
    private string _hostname = "";
    private string _password = "";
    private string _passwordAdmin = "";
    private int? _maxPlayers;
    private int? _vonCodecQuality;
    private string _serverTime = "";
    private int? _serverTimeAcceleration;
    private int? _serverNightTimeAcceleration;
    private int? _loginQueueConcurrentPlayers;
    private int? _loginQueueMaxPlayers;
    private string _shardId = "";
    private int? _respawnTime;
    private string _timeStampFormat = "";
    private int? _logAverageFps;
    private int? _logMemory;
    private int? _logPlayers;
    private string _logFile = "";
    private int? _simulatedPlayersBatch;
    private int? _steamQueryPort;

    // Toggle fields: absent cfg key always means "off", matching the input fields' "absent = empty" rule.
    private bool _enableWhitelist;
    private bool _disableBanlist;
    private bool _disablePrioritylist;
    private bool _verifySignatures;
    private bool _forceSameBuild;
    private bool _disableVoN;
    private bool _disable3rdPerson;
    private bool _disableCrosshair;
    private bool _serverTimePersistent;
    private bool _guaranteedUpdates;
    private bool _storageAutoFix;
    private bool _adminLogPlayerHitsOnly;
    private bool _adminLogPlacement;
    private bool _adminLogBuildActions;
    private bool _adminLogPlayerList;
    private bool _enableDebugMonitor;
    private bool _allowFilePatching;
    private bool _multithreadedReplication;
    private bool _lightingConfig;
    private bool _disablePersonalLight;
    private bool _disableBaseDamage;
    private bool _disableContainerDamage;
    private bool _disableRespawnDialog;

    // "Is this key actually present in serverDZ.cfg?" flags - one per field above, driving the small
    // presence checkbox next to every row. Checking it adds the key with a sensible default value;
    // unchecking it removes the key entirely. A toggle's own CheckBox is disabled while its presence
    // flag is false, since there is nothing to flip yet.
    private bool _hostnamePresent;
    private bool _passwordPresent;
    private bool _passwordAdminPresent;
    private bool _maxPlayersPresent;
    private bool _vonCodecQualityPresent;
    private bool _serverTimePresent;
    private bool _serverTimeAccelerationPresent;
    private bool _serverNightTimeAccelerationPresent;
    private bool _loginQueueConcurrentPlayersPresent;
    private bool _loginQueueMaxPlayersPresent;
    private bool _shardIdPresent;
    private bool _respawnTimePresent;
    private bool _timeStampFormatPresent;
    private bool _logAverageFpsPresent;
    private bool _logMemoryPresent;
    private bool _logPlayersPresent;
    private bool _logFilePresent;
    private bool _simulatedPlayersBatchPresent;
    private bool _steamQueryPortPresent;

    private bool _enableWhitelistPresent;
    private bool _disableBanlistPresent;
    private bool _disablePrioritylistPresent;
    private bool _verifySignaturesPresent;
    private bool _forceSameBuildPresent;
    private bool _disableVoNPresent;
    private bool _disable3rdPersonPresent;
    private bool _disableCrosshairPresent;
    private bool _serverTimePersistentPresent;
    private bool _guaranteedUpdatesPresent;
    private bool _storageAutoFixPresent;
    private bool _adminLogPlayerHitsOnlyPresent;
    private bool _adminLogPlacementPresent;
    private bool _adminLogBuildActionsPresent;
    private bool _adminLogPlayerListPresent;
    private bool _enableDebugMonitorPresent;
    private bool _allowFilePatchingPresent;
    private bool _multithreadedReplicationPresent;
    private bool _lightingConfigPresent;
    private bool _disablePersonalLightPresent;
    private bool _disableBaseDamagePresent;
    private bool _disableContainerDamagePresent;
    private bool _disableRespawnDialogPresent;

    public ServerConfigViewModel()
    {
        LoadCommand = new RelayCommand(ReloadFromDisk);
        SaveCommand = new RelayCommand(SaveToDisk);
        CreateDefaultCommand = new RelayCommand(CreateDefault);
        EditCommand = new RelayCommand(EditFile);
        RescanMissionsCommand = new RelayCommand(RescanMissions);
        OpenMissionsFolderCommand = new RelayCommand(OpenMissionsFolder);
    }

    public ObservableCollection<MissionOptionViewModel> MissionOptions { get; } = new();

    public string CurrentTemplate { get => _currentTemplate; private set => SetField(ref _currentTemplate, value); }

    public string RawText
    {
        get => _rawText;
        set
        {
            if (!SetField(ref _rawText, value)) return;
            if (_isSyncing) return;
            _document ??= new ServerConfigDocument(value);
            _document.RawText = value;
            RefreshQuickFieldsFromDocument();
        }
    }

    public string LoadStatus { get => _loadStatus; private set => SetField(ref _loadStatus, value); }

    public string Hostname { get => _hostname; set => SetQuick(ref _hostname, value, d => d.SetString("hostname", value)); }
    public string Password { get => _password; set => SetQuick(ref _password, value, d => d.SetString("password", value)); }
    public string PasswordAdmin { get => _passwordAdmin; set => SetQuick(ref _passwordAdmin, value, d => d.SetString("passwordAdmin", value)); }
    public int? MaxPlayers { get => _maxPlayers; set => SetQuick(ref _maxPlayers, value, d => d.SetIntOrRemove("maxPlayers", value)); }
    public int? VonCodecQuality { get => _vonCodecQuality; set => SetQuick(ref _vonCodecQuality, value, d => d.SetIntOrRemove("vonCodecQuality", value)); }
    public string ServerTime { get => _serverTime; set => SetQuick(ref _serverTime, value, d => d.SetString("serverTime", value)); }
    public int? ServerTimeAcceleration { get => _serverTimeAcceleration; set => SetQuick(ref _serverTimeAcceleration, value, d => d.SetIntOrRemove("serverTimeAcceleration", value)); }
    public int? ServerNightTimeAcceleration { get => _serverNightTimeAcceleration; set => SetQuick(ref _serverNightTimeAcceleration, value, d => d.SetIntOrRemove("serverNightTimeAcceleration", value)); }
    public int? LoginQueueConcurrentPlayers { get => _loginQueueConcurrentPlayers; set => SetQuick(ref _loginQueueConcurrentPlayers, value, d => d.SetIntOrRemove("loginQueueConcurrentPlayers", value)); }
    public int? LoginQueueMaxPlayers { get => _loginQueueMaxPlayers; set => SetQuick(ref _loginQueueMaxPlayers, value, d => d.SetIntOrRemove("loginQueueMaxPlayers", value)); }
    public string ShardId { get => _shardId; set => SetQuick(ref _shardId, value, d => d.SetString("shardId", value)); }
    public int? RespawnTime { get => _respawnTime; set => SetQuick(ref _respawnTime, value, d => d.SetIntOrRemove("respawnTime", value)); }
    public string TimeStampFormat { get => _timeStampFormat; set => SetQuick(ref _timeStampFormat, value, d => d.SetString("timeStampFormat", value)); }
    public int? LogAverageFps { get => _logAverageFps; set => SetQuick(ref _logAverageFps, value, d => d.SetIntOrRemove("logAverageFps", value)); }
    public int? LogMemory { get => _logMemory; set => SetQuick(ref _logMemory, value, d => d.SetIntOrRemove("logMemory", value)); }
    public int? LogPlayers { get => _logPlayers; set => SetQuick(ref _logPlayers, value, d => d.SetIntOrRemove("logPlayers", value)); }
    public string LogFile { get => _logFile; set => SetQuick(ref _logFile, value, d => d.SetString("logFile", value)); }
    public int? SimulatedPlayersBatch { get => _simulatedPlayersBatch; set => SetQuick(ref _simulatedPlayersBatch, value, d => d.SetIntOrRemove("simulatedPlayersBatch", value)); }
    public int? SteamQueryPort { get => _steamQueryPort; set => SetQuick(ref _steamQueryPort, value, d => d.SetIntOrRemove("steamQueryPort", value)); }

    public bool EnableWhitelist { get => _enableWhitelist; set => SetQuick(ref _enableWhitelist, value, d => d.SetBool("enableWhitelist", value)); }
    public bool DisableBanlist { get => _disableBanlist; set => SetQuick(ref _disableBanlist, value, d => d.SetBool("disableBanlist", value)); }
    public bool DisablePrioritylist { get => _disablePrioritylist; set => SetQuick(ref _disablePrioritylist, value, d => d.SetBool("disablePrioritylist", value)); }

    /// <summary>verifySignatures only accepts 0 (off) or 2 (on) in real DayZ configs - not the usual
    /// 0/1 - so the toggle maps true/false to 2/0 instead of delegating to the generic SetBool.</summary>
    public bool VerifySignatures { get => _verifySignatures; set => SetQuick(ref _verifySignatures, value, d => d.SetInt("verifySignatures", value ? 2 : 0)); }
    public bool ForceSameBuild { get => _forceSameBuild; set => SetQuick(ref _forceSameBuild, value, d => d.SetBool("forceSameBuild", value)); }
    public bool DisableVoN { get => _disableVoN; set => SetQuick(ref _disableVoN, value, d => d.SetBool("disableVoN", value)); }
    public bool Disable3rdPerson { get => _disable3rdPerson; set => SetQuick(ref _disable3rdPerson, value, d => d.SetBool("disable3rdPerson", value)); }
    public bool DisableCrosshair { get => _disableCrosshair; set => SetQuick(ref _disableCrosshair, value, d => d.SetBool("disableCrosshair", value)); }
    public bool ServerTimePersistent { get => _serverTimePersistent; set => SetQuick(ref _serverTimePersistent, value, d => d.SetBool("serverTimePersistent", value)); }
    public bool GuaranteedUpdates { get => _guaranteedUpdates; set => SetQuick(ref _guaranteedUpdates, value, d => d.SetBool("guaranteedUpdates", value)); }
    public bool StorageAutoFix { get => _storageAutoFix; set => SetQuick(ref _storageAutoFix, value, d => d.SetBool("storageAutoFix", value)); }
    public bool AdminLogPlayerHitsOnly { get => _adminLogPlayerHitsOnly; set => SetQuick(ref _adminLogPlayerHitsOnly, value, d => d.SetBool("adminLogPlayerHitsOnly", value)); }
    public bool AdminLogPlacement { get => _adminLogPlacement; set => SetQuick(ref _adminLogPlacement, value, d => d.SetBool("adminLogPlacement", value)); }
    public bool AdminLogBuildActions { get => _adminLogBuildActions; set => SetQuick(ref _adminLogBuildActions, value, d => d.SetBool("adminLogBuildActions", value)); }
    public bool AdminLogPlayerList { get => _adminLogPlayerList; set => SetQuick(ref _adminLogPlayerList, value, d => d.SetBool("adminLogPlayerList", value)); }
    public bool EnableDebugMonitor { get => _enableDebugMonitor; set => SetQuick(ref _enableDebugMonitor, value, d => d.SetBool("enableDebugMonitor", value)); }
    public bool AllowFilePatching { get => _allowFilePatching; set => SetQuick(ref _allowFilePatching, value, d => d.SetBool("allowFilePatching", value)); }
    public bool MultithreadedReplication { get => _multithreadedReplication; set => SetQuick(ref _multithreadedReplication, value, d => d.SetBool("multithreadedReplication", value)); }
    public bool LightingConfig { get => _lightingConfig; set => SetQuick(ref _lightingConfig, value, d => d.SetBool("lightingConfig", value)); }
    public bool DisablePersonalLight { get => _disablePersonalLight; set => SetQuick(ref _disablePersonalLight, value, d => d.SetBool("disablePersonalLight", value)); }
    public bool DisableBaseDamage { get => _disableBaseDamage; set => SetQuick(ref _disableBaseDamage, value, d => d.SetBool("disableBaseDamage", value)); }
    public bool DisableContainerDamage { get => _disableContainerDamage; set => SetQuick(ref _disableContainerDamage, value, d => d.SetBool("disableContainerDamage", value)); }
    public bool DisableRespawnDialog { get => _disableRespawnDialog; set => SetQuick(ref _disableRespawnDialog, value, d => d.SetBool("disableRespawnDialog", value)); }

    public bool HostnamePresent { get => _hostnamePresent; set => SetPresent(ref _hostnamePresent, value, "hostname", d => d.SetString("hostname", "EXAMPLE NAME")); }
    public bool PasswordPresent { get => _passwordPresent; set => SetPresent(ref _passwordPresent, value, "password", d => d.SetString("password", "")); }
    public bool PasswordAdminPresent { get => _passwordAdminPresent; set => SetPresent(ref _passwordAdminPresent, value, "passwordAdmin", d => d.SetString("passwordAdmin", "")); }
    public bool MaxPlayersPresent { get => _maxPlayersPresent; set => SetPresent(ref _maxPlayersPresent, value, "maxPlayers", d => d.SetInt("maxPlayers", 60)); }
    public bool VonCodecQualityPresent { get => _vonCodecQualityPresent; set => SetPresent(ref _vonCodecQualityPresent, value, "vonCodecQuality", d => d.SetInt("vonCodecQuality", 20)); }
    public bool ServerTimePresent { get => _serverTimePresent; set => SetPresent(ref _serverTimePresent, value, "serverTime", d => d.SetString("serverTime", "SystemTime")); }
    public bool ServerTimeAccelerationPresent { get => _serverTimeAccelerationPresent; set => SetPresent(ref _serverTimeAccelerationPresent, value, "serverTimeAcceleration", d => d.SetInt("serverTimeAcceleration", 1)); }
    public bool ServerNightTimeAccelerationPresent { get => _serverNightTimeAccelerationPresent; set => SetPresent(ref _serverNightTimeAccelerationPresent, value, "serverNightTimeAcceleration", d => d.SetInt("serverNightTimeAcceleration", 1)); }
    public bool LoginQueueConcurrentPlayersPresent { get => _loginQueueConcurrentPlayersPresent; set => SetPresent(ref _loginQueueConcurrentPlayersPresent, value, "loginQueueConcurrentPlayers", d => d.SetInt("loginQueueConcurrentPlayers", 5)); }
    public bool LoginQueueMaxPlayersPresent { get => _loginQueueMaxPlayersPresent; set => SetPresent(ref _loginQueueMaxPlayersPresent, value, "loginQueueMaxPlayers", d => d.SetInt("loginQueueMaxPlayers", 500)); }
    public bool ShardIdPresent { get => _shardIdPresent; set => SetPresent(ref _shardIdPresent, value, "shardId", d => d.SetString("shardId", "123abc")); }
    public bool RespawnTimePresent { get => _respawnTimePresent; set => SetPresent(ref _respawnTimePresent, value, "respawnTime", d => d.SetInt("respawnTime", 5)); }
    public bool TimeStampFormatPresent { get => _timeStampFormatPresent; set => SetPresent(ref _timeStampFormatPresent, value, "timeStampFormat", d => d.SetString("timeStampFormat", "Short")); }
    public bool LogAverageFpsPresent { get => _logAverageFpsPresent; set => SetPresent(ref _logAverageFpsPresent, value, "logAverageFps", d => d.SetInt("logAverageFps", 1)); }
    public bool LogMemoryPresent { get => _logMemoryPresent; set => SetPresent(ref _logMemoryPresent, value, "logMemory", d => d.SetInt("logMemory", 1)); }
    public bool LogPlayersPresent { get => _logPlayersPresent; set => SetPresent(ref _logPlayersPresent, value, "logPlayers", d => d.SetInt("logPlayers", 1)); }
    public bool LogFilePresent { get => _logFilePresent; set => SetPresent(ref _logFilePresent, value, "logFile", d => d.SetString("logFile", "server_console.log")); }
    public bool SimulatedPlayersBatchPresent { get => _simulatedPlayersBatchPresent; set => SetPresent(ref _simulatedPlayersBatchPresent, value, "simulatedPlayersBatch", d => d.SetInt("simulatedPlayersBatch", 20)); }
    public bool SteamQueryPortPresent { get => _steamQueryPortPresent; set => SetPresent(ref _steamQueryPortPresent, value, "steamQueryPort", d => d.SetInt("steamQueryPort", 2305)); }

    public bool EnableWhitelistPresent { get => _enableWhitelistPresent; set => SetPresent(ref _enableWhitelistPresent, value, "enableWhitelist", d => d.SetBool("enableWhitelist", false)); }
    public bool DisableBanlistPresent { get => _disableBanlistPresent; set => SetPresent(ref _disableBanlistPresent, value, "disableBanlist", d => d.SetBool("disableBanlist", false)); }
    public bool DisablePrioritylistPresent { get => _disablePrioritylistPresent; set => SetPresent(ref _disablePrioritylistPresent, value, "disablePrioritylist", d => d.SetBool("disablePrioritylist", false)); }
    public bool VerifySignaturesPresent { get => _verifySignaturesPresent; set => SetPresent(ref _verifySignaturesPresent, value, "verifySignatures", d => d.SetInt("verifySignatures", 2)); }
    public bool ForceSameBuildPresent { get => _forceSameBuildPresent; set => SetPresent(ref _forceSameBuildPresent, value, "forceSameBuild", d => d.SetBool("forceSameBuild", true)); }
    public bool DisableVoNPresent { get => _disableVoNPresent; set => SetPresent(ref _disableVoNPresent, value, "disableVoN", d => d.SetBool("disableVoN", false)); }
    public bool Disable3rdPersonPresent { get => _disable3rdPersonPresent; set => SetPresent(ref _disable3rdPersonPresent, value, "disable3rdPerson", d => d.SetBool("disable3rdPerson", false)); }
    public bool DisableCrosshairPresent { get => _disableCrosshairPresent; set => SetPresent(ref _disableCrosshairPresent, value, "disableCrosshair", d => d.SetBool("disableCrosshair", false)); }
    public bool ServerTimePersistentPresent { get => _serverTimePersistentPresent; set => SetPresent(ref _serverTimePersistentPresent, value, "serverTimePersistent", d => d.SetBool("serverTimePersistent", false)); }
    public bool GuaranteedUpdatesPresent { get => _guaranteedUpdatesPresent; set => SetPresent(ref _guaranteedUpdatesPresent, value, "guaranteedUpdates", d => d.SetBool("guaranteedUpdates", true)); }
    public bool StorageAutoFixPresent { get => _storageAutoFixPresent; set => SetPresent(ref _storageAutoFixPresent, value, "storageAutoFix", d => d.SetBool("storageAutoFix", true)); }
    public bool AdminLogPlayerHitsOnlyPresent { get => _adminLogPlayerHitsOnlyPresent; set => SetPresent(ref _adminLogPlayerHitsOnlyPresent, value, "adminLogPlayerHitsOnly", d => d.SetBool("adminLogPlayerHitsOnly", false)); }
    public bool AdminLogPlacementPresent { get => _adminLogPlacementPresent; set => SetPresent(ref _adminLogPlacementPresent, value, "adminLogPlacement", d => d.SetBool("adminLogPlacement", false)); }
    public bool AdminLogBuildActionsPresent { get => _adminLogBuildActionsPresent; set => SetPresent(ref _adminLogBuildActionsPresent, value, "adminLogBuildActions", d => d.SetBool("adminLogBuildActions", false)); }
    public bool AdminLogPlayerListPresent { get => _adminLogPlayerListPresent; set => SetPresent(ref _adminLogPlayerListPresent, value, "adminLogPlayerList", d => d.SetBool("adminLogPlayerList", false)); }
    public bool EnableDebugMonitorPresent { get => _enableDebugMonitorPresent; set => SetPresent(ref _enableDebugMonitorPresent, value, "enableDebugMonitor", d => d.SetBool("enableDebugMonitor", true)); }
    public bool AllowFilePatchingPresent { get => _allowFilePatchingPresent; set => SetPresent(ref _allowFilePatchingPresent, value, "allowFilePatching", d => d.SetBool("allowFilePatching", true)); }
    public bool MultithreadedReplicationPresent { get => _multithreadedReplicationPresent; set => SetPresent(ref _multithreadedReplicationPresent, value, "multithreadedReplication", d => d.SetBool("multithreadedReplication", true)); }
    public bool LightingConfigPresent { get => _lightingConfigPresent; set => SetPresent(ref _lightingConfigPresent, value, "lightingConfig", d => d.SetBool("lightingConfig", false)); }
    public bool DisablePersonalLightPresent { get => _disablePersonalLightPresent; set => SetPresent(ref _disablePersonalLightPresent, value, "disablePersonalLight", d => d.SetBool("disablePersonalLight", true)); }
    public bool DisableBaseDamagePresent { get => _disableBaseDamagePresent; set => SetPresent(ref _disableBaseDamagePresent, value, "disableBaseDamage", d => d.SetBool("disableBaseDamage", false)); }
    public bool DisableContainerDamagePresent { get => _disableContainerDamagePresent; set => SetPresent(ref _disableContainerDamagePresent, value, "disableContainerDamage", d => d.SetBool("disableContainerDamage", false)); }
    public bool DisableRespawnDialogPresent { get => _disableRespawnDialogPresent; set => SetPresent(ref _disableRespawnDialogPresent, value, "disableRespawnDialog", d => d.SetBool("disableRespawnDialog", false)); }

    public RelayCommand LoadCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand CreateDefaultCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand RescanMissionsCommand { get; }
    public RelayCommand OpenMissionsFolderCommand { get; }

    public void AttachProfile(BranchProfileViewModel profile)
    {
        if (_profile is not null) _profile.PropertyChanged -= OnProfilePropertyChanged;
        _profile = profile;
        _profile.PropertyChanged += OnProfilePropertyChanged;
        ReloadFromDisk();
        RescanMissions();
    }

    /// <summary>ReloadFromDisk()/RescanMissions() otherwise only ever run once, at AttachProfile time -
    /// so when ServerConfigPath/ServerExePath are filled in afterward (Steam auto-detect, either the
    /// "Найти (Steam)" button or the first-launch prompt, or just browsing to a new path) this section
    /// kept showing its stale "not specified"/empty state instead of picking up the new path.</summary>
    private void OnProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BranchProfileViewModel.ServerConfigPath)) ReloadFromDisk();
        if (e.PropertyName == nameof(BranchProfileViewModel.ServerExePath)) RescanMissions();
    }

    private void ReloadFromDisk()
    {
        if (_profile is null) return;
        var path = _profile.ServerConfigPath;

        if (string.IsNullOrWhiteSpace(path))
        {
            _document = null;
            LoadStatus = "Путь к serverDZ.cfg не указан (вкладка «Сервер»).";
        }
        else if (File.Exists(path))
        {
            _document = ServerConfigDocument.Load(path);
            LoadStatus = $"Загружено: {path}";
        }
        else
        {
            _document = null;
            LoadStatus = $"Файл не найден: {path}";
        }

        SetField(ref _rawText, _document?.RawText ?? "", nameof(RawText));
        RefreshQuickFieldsFromDocument();
    }

    private void CreateDefault()
    {
        _document = ServerConfigDocument.CreateDefault();
        LoadStatus = "Создан шаблон по умолчанию (пока не сохранён на диск).";
        SetField(ref _rawText, _document.RawText, nameof(RawText));
        RefreshQuickFieldsFromDocument();
    }

    private void SaveToDisk()
    {
        if (_profile is null) return;
        if (string.IsNullOrWhiteSpace(_profile.ServerConfigPath))
        {
            LoadStatus = "Укажите путь к файлу конфигурации на вкладке «Сервер» перед сохранением.";
            return;
        }

        EnsureDocument().Save(_profile.ServerConfigPath);
        LoadStatus = $"Сохранено: {_profile.ServerConfigPath}";
    }

    /// <summary>Opens serverDZ.cfg in whichever program Windows has associated with .cfg files -
    /// UseShellExecute with the path as the FileName (rather than launching notepad.exe explicitly)
    /// is exactly what a double-click in Explorer does, so this follows the user's own default
    /// instead of assuming Notepad.</summary>
    private void EditFile()
    {
        var path = _profile?.ServerConfigPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            LoadStatus = "Файл ещё не сохранён на диск - сначала нажмите «Сохранить».";
            return;
        }

        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch { /* best effort */ }
    }

    private void RescanMissions()
    {
        MissionOptions.Clear();
        if (_profile is null) return;
        foreach (var f in MissionScanner.ScanMissionFolders(_profile.ServerExePath))
            MissionOptions.Add(new MissionOptionViewModel(f, OnMissionSelected));
        SyncMissionSelection();
    }

    private void OnMissionSelected(MissionOptionViewModel selected)
    {
        foreach (var option in MissionOptions)
            if (!ReferenceEquals(option, selected)) option.SetSelectedSilently(false);

        EnsureDocument().SetMissionTemplate(selected.Name);
        SetField(ref _rawText, _document!.RawText, nameof(RawText));
        CurrentTemplate = selected.Name;
        LoadStatus = $"Миссия «{selected.Name}» применена. Не забудьте «Сохранить».";
    }

    private void SyncMissionSelection()
    {
        foreach (var option in MissionOptions)
            option.SetSelectedSilently(option.Name == CurrentTemplate);
    }

    private void OpenMissionsFolder()
    {
        if (_profile is null) return;

        var mpMissionsDir = MissionScanner.GetMpMissionsDirectory(_profile.ServerExePath);
        if (mpMissionsDir is null) return;

        if (string.IsNullOrWhiteSpace(CurrentTemplate))
        {
            LoadStatus = "Миссия не выбрана.";
            return;
        }

        var dir = Path.Combine(mpMissionsDir, CurrentTemplate);
        if (!Directory.Exists(dir))
        {
            LoadStatus = $"Папка миссии не найдена: {dir}";
            return;
        }

        try { Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true }); }
        catch { /* best effort */ }
    }

    private ServerConfigDocument EnsureDocument() => _document ??= ServerConfigDocument.CreateDefault();

    /// <summary>Applies a quick-field edit: updates the bound field, then (unless we're currently
    /// syncing the other direction) writes it into the document and mirrors the document back into
    /// the raw-text box.</summary>
    private void SetQuick<T>(ref T field, T value, Action<ServerConfigDocument> apply, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (!SetField(ref field, value, propertyName)) return;
        if (_isSyncing) return;

        apply(EnsureDocument());
        SetField(ref _rawText, _document!.RawText, nameof(RawText));
        LoadStatus = "Внесены изменения, не забудьте сохранить!";
    }

    /// <summary>Backs the small "present in serverDZ.cfg" checkbox next to every field. Checking it
    /// writes the key with a sensible default value; unchecking it removes the key outright. Routes
    /// through a full <see cref="RefreshQuickFieldsFromDocument"/> afterwards so the paired value
    /// property (and, for toggles, its enabled state) re-syncs from the document in one go.</summary>
    private void SetPresent(ref bool field, bool value, string key, Action<ServerConfigDocument> writeDefault, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (!SetField(ref field, value, propertyName)) return;
        if (_isSyncing) return;

        if (value) writeDefault(EnsureDocument());
        else EnsureDocument().RemoveKey(key);

        RefreshQuickFieldsFromDocument();
        LoadStatus = "Внесены изменения, не забудьте сохранить!";
    }

    private void RefreshQuickFieldsFromDocument()
    {
        _isSyncing = true;
        try
        {
            var doc = _document;
            SetField(ref _rawText, doc?.RawText ?? "", nameof(RawText));

            Hostname = doc?.GetString("hostname") ?? "";
            Password = doc?.GetString("password") ?? "";
            PasswordAdmin = doc?.GetString("passwordAdmin") ?? "";
            MaxPlayers = doc?.GetIntOrNull("maxPlayers");
            VonCodecQuality = doc?.GetIntOrNull("vonCodecQuality");
            ServerTime = doc?.GetString("serverTime") ?? "";
            ServerTimeAcceleration = doc?.GetIntOrNull("serverTimeAcceleration");
            ServerNightTimeAcceleration = doc?.GetIntOrNull("serverNightTimeAcceleration");
            LoginQueueConcurrentPlayers = doc?.GetIntOrNull("loginQueueConcurrentPlayers");
            LoginQueueMaxPlayers = doc?.GetIntOrNull("loginQueueMaxPlayers");
            ShardId = doc?.GetString("shardId") ?? "";
            RespawnTime = doc?.GetIntOrNull("respawnTime");
            TimeStampFormat = doc?.GetString("timeStampFormat") ?? "";
            LogAverageFps = doc?.GetIntOrNull("logAverageFps");
            LogMemory = doc?.GetIntOrNull("logMemory");
            LogPlayers = doc?.GetIntOrNull("logPlayers");
            LogFile = doc?.GetString("logFile") ?? "";
            SimulatedPlayersBatch = doc?.GetIntOrNull("simulatedPlayersBatch");
            SteamQueryPort = doc?.GetIntOrNull("steamQueryPort");

            EnableWhitelist = doc?.GetBool("enableWhitelist") ?? false;
            DisableBanlist = doc?.GetBool("disableBanlist") ?? false;
            DisablePrioritylist = doc?.GetBool("disablePrioritylist") ?? false;
            VerifySignatures = (doc?.GetInt("verifySignatures", 0) ?? 0) == 2;
            ForceSameBuild = doc?.GetBool("forceSameBuild") ?? false;
            DisableVoN = doc?.GetBool("disableVoN") ?? false;
            Disable3rdPerson = doc?.GetBool("disable3rdPerson") ?? false;
            DisableCrosshair = doc?.GetBool("disableCrosshair") ?? false;
            ServerTimePersistent = doc?.GetBool("serverTimePersistent") ?? false;
            GuaranteedUpdates = doc?.GetBool("guaranteedUpdates") ?? false;
            StorageAutoFix = doc?.GetBool("storageAutoFix") ?? false;
            AdminLogPlayerHitsOnly = doc?.GetBool("adminLogPlayerHitsOnly") ?? false;
            AdminLogPlacement = doc?.GetBool("adminLogPlacement") ?? false;
            AdminLogBuildActions = doc?.GetBool("adminLogBuildActions") ?? false;
            AdminLogPlayerList = doc?.GetBool("adminLogPlayerList") ?? false;
            EnableDebugMonitor = doc?.GetBool("enableDebugMonitor") ?? false;
            AllowFilePatching = doc?.GetBool("allowFilePatching") ?? false;
            MultithreadedReplication = doc?.GetBool("multithreadedReplication") ?? false;
            LightingConfig = doc?.GetBool("lightingConfig") ?? false;
            DisablePersonalLight = doc?.GetBool("disablePersonalLight") ?? false;
            DisableBaseDamage = doc?.GetBool("disableBaseDamage") ?? false;
            DisableContainerDamage = doc?.GetBool("disableContainerDamage") ?? false;
            DisableRespawnDialog = doc?.GetBool("disableRespawnDialog") ?? false;

            HostnamePresent = doc?.HasKey("hostname") ?? false;
            PasswordPresent = doc?.HasKey("password") ?? false;
            PasswordAdminPresent = doc?.HasKey("passwordAdmin") ?? false;
            MaxPlayersPresent = doc?.HasKey("maxPlayers") ?? false;
            VonCodecQualityPresent = doc?.HasKey("vonCodecQuality") ?? false;
            ServerTimePresent = doc?.HasKey("serverTime") ?? false;
            ServerTimeAccelerationPresent = doc?.HasKey("serverTimeAcceleration") ?? false;
            ServerNightTimeAccelerationPresent = doc?.HasKey("serverNightTimeAcceleration") ?? false;
            LoginQueueConcurrentPlayersPresent = doc?.HasKey("loginQueueConcurrentPlayers") ?? false;
            LoginQueueMaxPlayersPresent = doc?.HasKey("loginQueueMaxPlayers") ?? false;
            ShardIdPresent = doc?.HasKey("shardId") ?? false;
            RespawnTimePresent = doc?.HasKey("respawnTime") ?? false;
            TimeStampFormatPresent = doc?.HasKey("timeStampFormat") ?? false;
            LogAverageFpsPresent = doc?.HasKey("logAverageFps") ?? false;
            LogMemoryPresent = doc?.HasKey("logMemory") ?? false;
            LogPlayersPresent = doc?.HasKey("logPlayers") ?? false;
            LogFilePresent = doc?.HasKey("logFile") ?? false;
            SimulatedPlayersBatchPresent = doc?.HasKey("simulatedPlayersBatch") ?? false;
            SteamQueryPortPresent = doc?.HasKey("steamQueryPort") ?? false;

            EnableWhitelistPresent = doc?.HasKey("enableWhitelist") ?? false;
            DisableBanlistPresent = doc?.HasKey("disableBanlist") ?? false;
            DisablePrioritylistPresent = doc?.HasKey("disablePrioritylist") ?? false;
            VerifySignaturesPresent = doc?.HasKey("verifySignatures") ?? false;
            ForceSameBuildPresent = doc?.HasKey("forceSameBuild") ?? false;
            DisableVoNPresent = doc?.HasKey("disableVoN") ?? false;
            Disable3rdPersonPresent = doc?.HasKey("disable3rdPerson") ?? false;
            DisableCrosshairPresent = doc?.HasKey("disableCrosshair") ?? false;
            ServerTimePersistentPresent = doc?.HasKey("serverTimePersistent") ?? false;
            GuaranteedUpdatesPresent = doc?.HasKey("guaranteedUpdates") ?? false;
            StorageAutoFixPresent = doc?.HasKey("storageAutoFix") ?? false;
            AdminLogPlayerHitsOnlyPresent = doc?.HasKey("adminLogPlayerHitsOnly") ?? false;
            AdminLogPlacementPresent = doc?.HasKey("adminLogPlacement") ?? false;
            AdminLogBuildActionsPresent = doc?.HasKey("adminLogBuildActions") ?? false;
            AdminLogPlayerListPresent = doc?.HasKey("adminLogPlayerList") ?? false;
            EnableDebugMonitorPresent = doc?.HasKey("enableDebugMonitor") ?? false;
            AllowFilePatchingPresent = doc?.HasKey("allowFilePatching") ?? false;
            MultithreadedReplicationPresent = doc?.HasKey("multithreadedReplication") ?? false;
            LightingConfigPresent = doc?.HasKey("lightingConfig") ?? false;
            DisablePersonalLightPresent = doc?.HasKey("disablePersonalLight") ?? false;
            DisableBaseDamagePresent = doc?.HasKey("disableBaseDamage") ?? false;
            DisableContainerDamagePresent = doc?.HasKey("disableContainerDamage") ?? false;
            DisableRespawnDialogPresent = doc?.HasKey("disableRespawnDialog") ?? false;

            CurrentTemplate = doc?.GetMissionTemplate() ?? "";
            SyncMissionSelection();
        }
        finally
        {
            _isSyncing = false;
        }
    }
}
