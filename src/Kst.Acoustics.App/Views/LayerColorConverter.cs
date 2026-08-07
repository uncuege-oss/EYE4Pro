using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Kst.Acoustics.App.Views;

public class LayerColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string name = value?.ToString()?.ToLower() ?? "";
        
        if (name.Contains("panel")) return new SolidColorBrush(Color.Parse("#BCA0D8"));
        if (name.Contains("yün")) return new SolidColorBrush(Color.Parse("#D0B880"));
        if (name.Contains("hava")) return new SolidColorBrush(Color.Parse("#3A5C78"), 0.3);
        
        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return AvaloniaProperty.UnsetValue;
    }
}
