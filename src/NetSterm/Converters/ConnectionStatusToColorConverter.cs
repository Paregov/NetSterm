using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace NetSterm.Converters;

public class ConnectionStatusToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isConnected = value is true;

        // TODO: Avalonia migration - look up application resources for ConnectedBrush/DisconnectedBrush
        return isConnected
            ? new SolidColorBrush(Color.FromRgb(0x0d, 0xbc, 0x79))
            : new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
