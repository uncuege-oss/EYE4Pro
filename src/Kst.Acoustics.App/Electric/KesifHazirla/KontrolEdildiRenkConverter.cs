using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Kst.Acoustics.App.Electric.KesifHazirla;

/// <summary>
/// Poz eşleşmesinin "kontrol edildi" durumuna göre renk üretir.
/// ConverterParameter="bg" arka plan, aksi halde yazı rengi.
/// Kontrol edilen eşleşme yeşil; edilmeyen varsayılan (gözden kaçmasın diye ayırt edilir).
/// </summary>
public sealed class KontrolEdildiRenkConverter : IValueConverter
{
    private static readonly IBrush KontrolYazi = new SolidColorBrush(Color.Parse("#7BE0A0")); // yeşil
    private static readonly IBrush VarsayilanYazi = new SolidColorBrush(Color.Parse("#E8F4E8"));
    private static readonly IBrush KontrolArka = new SolidColorBrush(Color.Parse("#0E2A18"));  // koyu yeşil ton
    private static readonly IBrush VarsayilanArka = Brushes.Transparent;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var kontrol = value as bool? ?? false;
        var arkaPlan = parameter as string == "bg";
        return kontrol
            ? (arkaPlan ? KontrolArka : KontrolYazi)
            : (arkaPlan ? VarsayilanArka : VarsayilanYazi);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
