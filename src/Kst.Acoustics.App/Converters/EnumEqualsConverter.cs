using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Kst.Acoustics.App.Converters;

/// <summary>
/// RadioButton.IsChecked'i bir enum değerine bağlamak için kullanılır.
/// ConverterParameter olarak verilen enum değeriyle eşleşirse true döner.
/// Geri yön: true ise parametreyi döner, false ise BindingOperations.DoNothing.
/// </summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public static readonly EnumEqualsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        return value.Equals(parameter);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b && parameter != null) return parameter;
        return Avalonia.Data.BindingOperations.DoNothing;
    }
}
