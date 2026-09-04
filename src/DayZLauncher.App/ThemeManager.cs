using System.Linq;
using System.Windows;
using DayZLauncher.Core.Models;

namespace DayZLauncher.App;

/// <summary>Swaps the merged Colors.*.xaml dictionary at runtime. Every color in ControlStyles.xaml
/// is a DynamicResource, so replacing this one dictionary re-themes every already-open window
/// immediately - no restart, no re-creating controls.</summary>
internal static class ThemeManager
{
    public static void Apply(AppTheme theme)
    {
        var uri = theme == AppTheme.Gray ? "Themes/Colors.Gray.xaml" : "Themes/Colors.Light.xaml";
        var newDictionary = new ResourceDictionary { Source = new Uri(uri, UriKind.Relative) };

        var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
        var existing = dictionaries.FirstOrDefault(d => d.Source is { OriginalString: var s } && s.Contains("Colors."));

        if (existing is not null)
            dictionaries[dictionaries.IndexOf(existing)] = newDictionary;
        else
            dictionaries.Insert(0, newDictionary);
    }
}
