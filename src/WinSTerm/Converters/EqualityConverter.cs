using System.Globalization;
using System.Windows.Data;

namespace WinSTerm.Converters;

/// <summary>
/// Compares two bound values for reference equality.
/// Returns true when both values refer to the same object.
/// Used for session tab selected-state highlighting in the custom tab strip.
/// </summary>
public class EqualityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return false;

        return Equals(values[0], values[1]);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
