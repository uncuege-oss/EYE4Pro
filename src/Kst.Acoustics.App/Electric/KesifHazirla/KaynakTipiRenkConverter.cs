using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Kst.Acoustics.Core.Electric.KesifHazirla;

namespace Kst.Acoustics.App.Electric.KesifHazirla;

/// <summary>
/// Keşif satırının kaynağına (Manuel / Otomatik / Karma) göre renk üretir.
/// ConverterParameter="bg" arka plan tonu, aksi halde yazı rengi döndürür.
/// Referanstan gelen satırlar (Otomatik/Karma) farklı renkte görünür.
/// </summary>
public sealed class KaynakTipiRenkConverter : IValueConverter
{
    private static readonly IBrush ManuelYazi = new SolidColorBrush(Color.Parse("#E8F4E8"));
    private static readonly IBrush OtomatikYazi = new SolidColorBrush(Color.Parse("#F2B65C")); // amber
    private static readonly IBrush KarmaYazi = new SolidColorBrush(Color.Parse("#7FD6C2"));     // teal

    private static readonly IBrush ManuelArka = Brushes.Transparent;
    private static readonly IBrush OtomatikArka = new SolidColorBrush(Color.Parse("#2A2410"));  // koyu amber tonu
    private static readonly IBrush KarmaArka = new SolidColorBrush(Color.Parse("#102A26"));     // koyu teal tonu

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var tip = value as KesifKaynakTipi? ?? KesifKaynakTipi.Manuel;
        var arkaPlan = parameter as string == "bg";
        return tip switch
        {
            KesifKaynakTipi.Otomatik => arkaPlan ? OtomatikArka : OtomatikYazi,
            KesifKaynakTipi.Karma => arkaPlan ? KarmaArka : KarmaYazi,
            _ => arkaPlan ? ManuelArka : ManuelYazi
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
