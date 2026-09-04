using System.Globalization;
using System.Windows.Data;

namespace DayZLauncher.App.Converters;

/// <summary>Binds a RadioButton's IsChecked to one value of an enum-typed property - IsChecked is
/// true when the bound enum equals ConverterParameter, and checking it sets the property to that
/// parameter value. Standard WPF pattern for "enum picked via a row of radio buttons".</summary>
public sealed class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.ToString() == parameter?.ToString();

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Enum.Parse(targetType, parameter!.ToString()!) : System.Windows.Data.Binding.DoNothing;
}
