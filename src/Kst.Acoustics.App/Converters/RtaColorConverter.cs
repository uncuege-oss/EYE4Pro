using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Kst.Acoustics.App.Converters;

public class RtaColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isActive && isActive)
            return new SolidColorBrush(Color.Parse("#EF4444")); // Red when active
        return new SolidColorBrush(Color.Parse("#1F5F94")); // Blue when inactive
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
}
