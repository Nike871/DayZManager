using DayZLauncher.App.Mvvm;

namespace DayZLauncher.App.ViewModels;

/// <summary>One mod list entry - either a full folder path (a dev mod) or a literal "@WorkshopName"
/// carried over from before this list existed. Only the folder's own name is shown, matching how
/// picking "D:\Modding\Source\TEST_EXP" should display as just "TEST_EXP".</summary>
public sealed class ModEntryViewModel : ObservableObject
{
    public ModEntryViewModel(string path, Action<ModEntryViewModel> onRemove)
    {
        Path = path;
        DisplayName = ComputeDisplayName(path);
        RemoveCommand = new RelayCommand(() => onRemove(this));
        OpenFolderCommand = new RelayCommand(OpenFolder);
    }

    public string Path { get; }
    public string DisplayName { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand OpenFolderCommand { get; }

    /// <summary>No-op for a "@WorkshopName" entry (carried over from before this list existed) -
    /// there's no local folder path to open for those.</summary>
    private void OpenFolder()
    {
        if (Path.StartsWith('@') || !System.IO.Directory.Exists(Path)) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path) { UseShellExecute = true }); }
        catch { /* best effort */ }
    }

    private static string ComputeDisplayName(string path)
    {
        if (path.StartsWith('@')) return path;

        var trimmed = path.TrimEnd('\\', '/');
        var name = System.IO.Path.GetFileName(trimmed);
        return string.IsNullOrEmpty(name) ? trimmed : name;
    }
}
