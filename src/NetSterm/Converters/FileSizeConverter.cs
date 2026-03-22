using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace NetSterm.Converters;

public class FileSizeConverter : IValueConverter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var bytes = value switch
        {
            long l => (double)l,
            int i => (double)i,
            double d => d,
            _ => 0d
        };

        if (bytes <= 0)
            return "0 B";

        var unitIndex = 0;
        var size = bytes;
        while (size >= 1024 && unitIndex < Units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{size:F0} {Units[unitIndex]}"
            : $"{size:F1} {Units[unitIndex]}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
