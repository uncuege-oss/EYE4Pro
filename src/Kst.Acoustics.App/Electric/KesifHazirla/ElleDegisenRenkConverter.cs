using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Kst.Acoustics.App.Electric.KesifHazirla;

/// <summary>
/// Birim fiyat elle değiştirilmişse (true) dikkat çekici renk, aksi halde varsayılan renk döndürür.
/// Keşif satırında elle düzeltilmiş birim fiyatları görsel olarak ayırt etmek için kullanılır.
/// </summary>
public sealed class ElleDegisenRenkConverter : IValueConverter
{
    private static readonly IBrush Degisen = new SolidColorBrush(Color.Parse("#FF8C4B"));   // turuncu — elle değişti
    private static readonly IBrush Varsayilan = new SolidColorBrush(Color.Parse("#E8F4E8"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (value as bool? ?? false) ? Degisen : Varsayilan;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
