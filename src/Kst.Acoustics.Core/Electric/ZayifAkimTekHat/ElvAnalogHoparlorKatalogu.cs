namespace Kst.Acoustics.Core.Electric.ZayifAkimTekHat;

public enum ElvHoparlorOrtamTipi
{
    IcMekan,
    DisMekan,
    IcVeDisMekan
}

/// <summary>100 V sesli alarm hoparlörü için üreticiden bağımsız katalog şablonu.</summary>
public sealed record ElvAnalogHoparlorKatalogKalemi(
    string Kod,
    string Ad,
    string Kategori,
    IReadOnlyList<double> TapGucLariW,
    ElvHoparlorOrtamTipi Ortam,
    bool En54_24UrunSecilmeli,
    string Aciklama);

/// <summary>Analog 100 V acil anons hoparlör şablonları ve geçerli güç tap değerleri.</summary>
public static class ElvAnalogHoparlorKatalogu
{
    public static IReadOnlyList<ElvAnalogHoparlorKatalogKalemi> Kalemler { get; } =
    [
        new("TAVAN-6W", "Tavan Tipi 100 V Hoparlör 6 W", "Tavan",
            [1.5, 3, 6], ElvHoparlorOrtamTipi.IcMekan, true,
            "Asma tavan ve genel iç mekân tahliye anonsu."),
        new("TAVAN-10W", "Tavan Tipi 100 V Hoparlör 10 W", "Tavan",
            [2.5, 5, 10], ElvHoparlorOrtamTipi.IcMekan, true,
            "Daha yüksek ses basıncı gereken iç mekânlar."),
        new("DUVAR-6W", "Duvar/Kabin Tipi 100 V Hoparlör 6 W", "Duvar/Kabin",
            [1.5, 3, 6], ElvHoparlorOrtamTipi.IcMekan, true,
            "Koridor, oda ve duvar üstü uygulamalar."),
        new("DUVAR-10W", "Duvar/Kabin Tipi 100 V Hoparlör 10 W", "Duvar/Kabin",
            [2.5, 5, 10], ElvHoparlorOrtamTipi.IcMekan, true,
            "Orta büyüklükte iç mekânlar."),
        new("HORN-15W", "Horn Tipi 100 V Hoparlör 15 W", "Horn",
            [3.75, 7.5, 15], ElvHoparlorOrtamTipi.IcVeDisMekan, true,
            "Gürültülü alanlar ve açık alan yönlü anons."),
        new("HORN-30W", "Horn Tipi 100 V Hoparlör 30 W", "Horn",
            [7.5, 15, 30], ElvHoparlorOrtamTipi.DisMekan, true,
            "Yüksek gürültülü veya geniş açık alanlar."),
        new("PROJEKTOR-10W", "Projektör Tipi 100 V Hoparlör 10 W", "Projektör",
            [2.5, 5, 10], ElvHoparlorOrtamTipi.IcVeDisMekan, true,
            "Koridor ve çift yönlü kapsama uygulamaları."),
        new("PROJEKTOR-20W", "Projektör Tipi 100 V Hoparlör 20 W", "Projektör",
            [5, 10, 20], ElvHoparlorOrtamTipi.IcVeDisMekan, true,
            "Uzun koridor ve yüksek tavan uygulamaları."),
        new("KOLON-20W", "Kolon Tipi 100 V Hoparlör 20 W", "Kolon",
            [5, 10, 20], ElvHoparlorOrtamTipi.IcMekan, true,
            "Yankılı hacimlerde kontrollü düşey yayılım."),
        new("KOLON-40W", "Kolon Tipi 100 V Hoparlör 40 W", "Kolon",
            [10, 20, 40], ElvHoparlorOrtamTipi.IcVeDisMekan, true,
            "Büyük ve yankılı hacimler."),
        new("LINEARRAY-60W", "Line-Array 100 V Hoparlör 60 W", "Line-Array",
            [15, 30, 60], ElvHoparlorOrtamTipi.IcVeDisMekan, true,
            "Büyük hacimlerde yüksek konuşma anlaşılabilirliği."),
        new("LINEARRAY-120W", "Horn Array / Line-Array 100 V Hoparlör 120 W", "Line-Array",
            [30, 60, 120], ElvHoparlorOrtamTipi.DisMekan, true,
            "Uzun mesafe ve geniş dış alan tahliye anonsu."),
        new("DIS-HORN-IP66-30W", "Dış Ortam IP66 Horn 100 V Hoparlör 30 W", "Dış Ortam",
            [7.5, 15, 30], ElvHoparlorOrtamTipi.DisMekan, true,
            "Yağmur, toz ve zorlu çevre koşulları."),
        new("DIS-KABIN-IP66-30W", "Dış Ortam IP66 Kabin Hoparlör 30 W", "Dış Ortam",
            [7.5, 15, 30], ElvHoparlorOrtamTipi.DisMekan, true,
            "Cephe ve yarı açık alan uygulamaları.")
    ];

    public static ElvAnalogHoparlorKatalogKalemi? Bul(string? kod)
        => Kalemler.FirstOrDefault(k => string.Equals(k.Kod, kod, StringComparison.OrdinalIgnoreCase));
}

public sealed record ElvTapDogrulamaSonucu(bool Gecerli, string Mesaj);

public static class ElvAnalogHoparlorServisi
{
    public static ElvAnalogHoparlorGrubu KatalogdanGrupOlustur(string katalogKodu, int adet = 1)
    {
        var kalem = ElvAnalogHoparlorKatalogu.Bul(katalogKodu)
                    ?? throw new ArgumentException("Analog hoparlör katalog kodu bulunamadı.", nameof(katalogKodu));
        return new ElvAnalogHoparlorGrubu
        {
            Ad = kalem.Ad,
            KatalogKodu = kalem.Kod,
            HoparlorTipi = kalem.Ad,
            Adet = Math.Max(0, adet),
            TapGucuW = kalem.TapGucLariW[0]
        };
    }

    public static ElvTapDogrulamaSonucu TapGucunuDogrula(ElvAnalogHoparlorGrubu grup)
    {
        var kalem = ElvAnalogHoparlorKatalogu.Bul(grup.KatalogKodu);
        if (kalem is null)
            return new(false, "Hoparlör katalog kalemi seçilmemiş veya artık mevcut değil.");
        bool gecerli = kalem.TapGucLariW.Any(t => Math.Abs(t - grup.TapGucuW) < 0.001);
        return gecerli
            ? new(true, "Güç tap değeri katalog ile uyumlu.")
            : new(false, $"Geçersiz tap değeri. İzin verilen değerler: {string.Join(", ", kalem.TapGucLariW.Select(t => $"{t:0.##} W"))}.");
    }

    public static bool TapGucunuAyarla(ElvAnalogHoparlorGrubu grup, double tapGucuW)
    {
        double onceki = grup.TapGucuW;
        grup.TapGucuW = tapGucuW;
        if (TapGucunuDogrula(grup).Gecerli) return true;
        grup.TapGucuW = onceki;
        return false;
    }
}
