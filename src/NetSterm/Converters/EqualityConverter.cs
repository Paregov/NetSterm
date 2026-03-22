using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace NetSterm.Converters;

/// <summary>
/// Compares two bound values for reference equality.
/// Returns true when both values refer to the same object.
/// Used for session tab selected-state highlighting in the custom tab strip.
/// </summary>
public class EqualityConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2)
            return false;

        return Equals(values[0], values[1]);
    }
}
