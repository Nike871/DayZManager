using System.Collections.ObjectModel;
using System.Linq;
using DayZLauncher.App.Mvvm;

namespace DayZLauncher.App.ViewModels;

/// <summary>Bindable mod list backed by a single newline-delimited string on the model (so
/// settings.json's existing "Mods"/"ServerMods" fields keep working unchanged) - "+" opens a folder
/// picker and appends the chosen folder; each entry has its own remove button.</summary>
public sealed class ModListViewModel : ObservableObject
{
    private readonly Func<string> _get;
    private readonly Action<string> _set;
    private readonly Action<string>? _onModAdded;
    private readonly Action<string>? _onModRemoved;
    private readonly Action<string>? _onMissingKeys;
    private readonly Action? _onDuplicate;

    /// <param name="onModAdded">Called with the newly picked folder's path right after it's added -
    /// used to copy that mod's .bikey to the server immediately, without waiting for a launch.</param>
    /// <param name="onModRemoved">Called with a folder's path right after it's removed - used to
    /// delete that mod's .bikey from the server's keys folder.</param>
    /// <param name="onMissingKeys">Called with the newly picked folder's path if it has no "keys"
    /// subfolder at all - a server with verifySignatures on will reject clients running it.</param>
    /// <param name="onDuplicate">Called instead of adding anything if the picked folder is already
    /// in this exact list.</param>
    public ModListViewModel(Func<string> get, Action<string> set, Action<string>? onModAdded = null,
        Action<string>? onModRemoved = null, Action<string>? onMissingKeys = null, Action? onDuplicate = null)
    {
        _get = get;
        _set = set;
        _onModAdded = onModAdded;
        _onModRemoved = onModRemoved;
        _onMissingKeys = onMissingKeys;
        _onDuplicate = onDuplicate;
        AddCommand = new RelayCommand(Add);
        LoadFromModel();
    }

    public ObservableCollection<ModEntryViewModel> Entries { get; } = new();
    public RelayCommand AddCommand { get; }

    private void LoadFromModel()
    {
        Entries.Clear();
        foreach (var path in Core.Launching.ArgumentBuilder.SplitModList(_get()))
            Entries.Add(new ModEntryViewModel(path, Remove));
    }

    private void Add()
    {
        using var dlg = new System.Windows.Forms.FolderBrowserDialog();
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        if (Entries.Any(e => IsSamePath(e.Path, dlg.SelectedPath)))
        {
            _onDuplicate?.Invoke();
            return;
        }

        Entries.Add(new ModEntryViewModel(dlg.SelectedPath, Remove));
        SaveToModel();
        _onModAdded?.Invoke(dlg.SelectedPath);

        if (!System.IO.Directory.Exists(System.IO.Path.Combine(dlg.SelectedPath, "keys")))
            _onMissingKeys?.Invoke(dlg.SelectedPath);
    }

    private static bool IsSamePath(string a, string b) =>
        string.Equals(a.TrimEnd('\\', '/'), b.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);

    private void Remove(ModEntryViewModel entry)
    {
        Entries.Remove(entry);
        SaveToModel();
        _onModRemoved?.Invoke(entry.Path);
    }

    private void SaveToModel() => _set(string.Join(Environment.NewLine, Entries.Select(e => e.Path)));
}
