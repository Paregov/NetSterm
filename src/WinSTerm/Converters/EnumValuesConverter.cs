using System.Globalization;
using System.Windows.Data;

namespace WinSTerm.Converters;

/// <summary>
/// Converts an enum value to the array of all values in that enum type.
/// Usage in XAML: ItemsSource="{Binding Source={x:Static models:ProxyType.None},
///     Converter={x:Static converters:EnumValuesConverter.Instance}}"
/// </summary>
public class EnumValuesConverter : IValueConverter
{
    public static readonly EnumValuesConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null) return Array.Empty<object>();
        return Enum.GetValues(value.GetType());
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
