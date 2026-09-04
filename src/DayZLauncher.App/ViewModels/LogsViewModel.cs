using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using DayZLauncher.App.Mvvm;
using DayZLauncher.Core.Logs;

namespace DayZLauncher.App.ViewModels;

/// <summary>Tails client or server log files (.RPT/.ADM/.log) for the active branch profile. Client
/// logs always come from %LOCALAPPDATA%\DayZ (or "DayZ Exp" for the Experimental branch - DayZ
/// itself picks that folder per-build; there's no -profiles override for the client). Server logs
/// default to a "profiles" subfolder next to the server .exe, or the profile's own -profiles path
/// when one is set.</summary>
public sealed class LogsViewModel : ObservableObject
{
    private readonly DispatcherTimer _timer;
    private readonly LogTailer _tailer = new();
    private BranchProfileViewModel? _profile;

    private const string AllLogTypes = "Все";

    private bool _isServerSource;
    private LogFileInfo? _selectedFile;
    private string _selectedLogType = AllLogTypes;

    /// <summary>Full unfiltered text of the currently tailed file. <see cref="Content"/> is always
    /// derived from this plus <see cref="FilterText"/>, so the filter applies retroactively to
    /// whatever was already loaded - not just to lines that arrive after you start typing it.</summary>
    private string _rawContent = "";

    private string _content = "";
    private bool _autoScroll = true;
    private string _filterText = "";

    public LogsViewModel()
    {
        RefreshFilesCommand = new RelayCommand(RefreshFiles);
        OpenFolderCommand = new RelayCommand(OpenFolder);
        DeleteLogsCommand = new RelayCommand(DeleteLogs);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    public ObservableCollection<LogFileInfo> Files { get; } = new();

    public ObservableCollection<string> LogTypeOptions { get; } = new() { AllLogTypes, ".log", ".ADM", ".RPT" };

    public string SelectedLogType
    {
        get => _selectedLogType;
        set
        {
            if (!SetField(ref _selectedLogType, value)) return;
            RefreshFiles();
        }
    }

    public bool IsServerSource
    {
        get => _isServerSource;
        set
        {
            if (!SetField(ref _isServerSource, value)) return;
            OnPropertyChanged(nameof(IsClientSource));
            RefreshFiles();
        }
    }

    public bool IsClientSource
    {
        get => !_isServerSource;
        set { if (value) IsServerSource = false; }
    }

    public LogFileInfo? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (!SetField(ref _selectedFile, value)) return;
            _tailer.Reset(value?.FullPath);
            _rawContent = value is null ? "" : _tailer.ReadNewText();
            RefreshDisplayedContent();
        }
    }

    public string Content { get => _content; private set => SetField(ref _content, value); }
    public bool AutoScroll { get => _autoScroll; set => SetField(ref _autoScroll, value); }

    public string FilterText
    {
        get => _filterText;
        set
        {
            if (!SetField(ref _filterText, value)) return;
            RefreshDisplayedContent();
        }
    }

    public RelayCommand RefreshFilesCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand DeleteLogsCommand { get; }

    /// <summary>Raised after new text is appended while tailing, so the view's code-behind can
    /// scroll the TextBox to the end (WPF has no pure-binding way to do this).</summary>
    public event Action? ContentAppended;

    public void AttachProfile(BranchProfileViewModel profile)
    {
        _profile = profile;
        RefreshFiles();
    }

    private string CurrentDirectory()
    {
        if (_profile is null) return "";

        return IsServerSource
            ? _profile.GetServerLogDirectory()
            : DayZPaths.GetClientLogDirectory(_profile.Branch);
    }

    /// <summary>Lists the current folder's log files, narrowed to <see cref="SelectedLogType"/> when
    /// it's not "Все" - shared by <see cref="RefreshFiles"/> and <see cref="Tick"/> so both agree on
    /// what "the file count" means once a type filter is active.</summary>
    private List<LogFileInfo> GetFilteredFiles()
    {
        var list = LogFileService.ListLogFiles(CurrentDirectory());
        if (SelectedLogType == AllLogTypes) return list;

        return list.Where(f => Path.GetExtension(f.FileName).Equals(SelectedLogType, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void RefreshFiles()
    {
        var list = GetFilteredFiles();
        var previouslySelected = SelectedFile?.FullPath;

        Files.Clear();
        foreach (var f in list) Files.Add(f);

        SelectedFile = Files.FirstOrDefault(f => f.FullPath == previouslySelected) ?? Files.FirstOrDefault();
    }

    private void Tick()
    {
        var currentCount = GetFilteredFiles().Count;
        if (currentCount != Files.Count)
        {
            RefreshFiles();
            return;
        }

        if (SelectedFile is null) return;

        var text = _tailer.ReadNewText();
        if (text.Length == 0) return;

        _rawContent += text;
        RefreshDisplayedContent();
        ContentAppended?.Invoke();
    }

    /// <summary>Recomputes <see cref="Content"/> from <see cref="_rawContent"/> and
    /// <see cref="FilterText"/> - called on every new chunk of tailed text AND on every filter
    /// keystroke, so the filter always reflects the whole file, not just what arrived after it
    /// was typed.</summary>
    private void RefreshDisplayedContent()
    {
        Content = string.IsNullOrWhiteSpace(FilterText)
            ? _rawContent
            : string.Join('\n', _rawContent.Split('\n').Where(l => l.Contains(FilterText, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Deletes every log file (.RPT/.ADM/.log/.txt) in the currently viewed folder
    /// (client or server, whichever source is selected). If the selected file survives (e.g. locked
    /// by a running process) it just stays selected and tailed as before.</summary>
    private void DeleteLogs()
    {
        var dir = CurrentDirectory();
        if (string.IsNullOrWhiteSpace(dir)) return;

        LogFileService.DeleteAllLogFiles(dir);
        RefreshFiles();
    }

    private void OpenFolder()
    {
        var dir = CurrentDirectory();
        if (!Directory.Exists(dir)) return;

        try { Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true }); }
        catch { /* best effort */ }
    }
}
