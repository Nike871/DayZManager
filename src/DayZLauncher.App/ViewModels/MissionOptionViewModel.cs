using DayZLauncher.App.Mvvm;

namespace DayZLauncher.App.ViewModels;

/// <summary>One selectable mission folder, rendered as a radio button - checking it immediately
/// applies that mission's template, the same "click = switch now" behavior as the Stable/Experimental
/// branch switch.</summary>
public sealed class MissionOptionViewModel : ObservableObject
{
    private readonly Action<MissionOptionViewModel> _onSelected;
    private bool _isSelected;

    public MissionOptionViewModel(string name, Action<MissionOptionViewModel> onSelected)
    {
        Name = name;
        _onSelected = onSelected;
    }

    public string Name { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!SetField(ref _isSelected, value) || !value) return;
            _onSelected(this);
        }
    }

    /// <summary>Updates the checked state without re-triggering selection - used when the owner is
    /// syncing radio buttons to match the currently loaded config instead of reacting to a click.</summary>
    public void SetSelectedSilently(bool value) => SetField(ref _isSelected, value);
}
