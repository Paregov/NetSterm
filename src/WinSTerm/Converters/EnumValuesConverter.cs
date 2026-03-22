using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace WinSTerm.Converters;

/// <summary>
/// Converts an enum value to the array of all values in that enum type.
/// </summary>
public class EnumValuesConverter : IValueConverter
{
    public static readonly EnumValuesConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return Array.Empty<object>();
        return Enum.GetValues(value.GetType());
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
