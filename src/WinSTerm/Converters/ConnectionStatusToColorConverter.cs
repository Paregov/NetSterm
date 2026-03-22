using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WinSTerm.Converters;

public class ConnectionStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isConnected = value is true;

        var key = isConnected ? "ConnectedBrush" : "DisconnectedBrush";
        if (Application.Current.TryFindResource(key) is SolidColorBrush brush)
            return brush;

        return isConnected
            ? new SolidColorBrush(Color.FromRgb(0x0d, 0xbc, 0x79))
            : new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
