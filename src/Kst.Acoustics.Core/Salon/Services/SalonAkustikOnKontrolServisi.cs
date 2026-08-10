using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Salon.Models;

namespace Kst.Acoustics.Core.Salon.Services;

public enum SalonKullanimTipi
{
    Konusma,
    Muzik,
    CokAmacli,
    Sinema
}

public sealed record SalonRtBandi(
    int FrekansHz,
    double EsdegerYutumM2,
    double TahminiRt60S,
    double HedefMinS,
    double HedefMaxS,
    bool Hedefte);

public sealed record SalonAkustikOnKontrolSonucu(
    double HavaHacmiM3,
    double ToplamYuzeyAlaniM2,
    int KoltukSayisi,
    double KoltukBasinaHacimM3,
    IReadOnlyDictionary<string, double> KategoriAlanlariM2,
    IReadOnlyList<SalonRtBandi> RtBantlari,
    IReadOnlyList<string> Bulgular)
{
    public double OrtalamaRt60S => RtBantlari.Count == 0 ? 0 : RtBantlari.Average(x => x.TahminiRt60S);
    public bool Hedefte => RtBantlari.Count > 0 && RtBantlari.All(x => x.Hedefte)
                           && Bulgular.All(x => !x.StartsWith("HATA:", StringComparison.Ordinal));
}

/// <summary>
/// M01 simülasyonuna geçmeden hacim/koltuk, yüzey oranları ve Sabine tabanlı kaba RT60
/// kontrolü yapar. Sonuç tasarım kararı içindir; malzeme atamalı M01 hesabının yerini almaz.
/// </summary>
public static class SalonAkustikOnKontrolServisi
{
    private static readonly int[] Bantlar = [125, 250, 500, 1000, 2000, 4000];
    private static readonly double[] DoluKoltukSabin = [0.16, 0.28, 0.44, 0.55, 0.62, 0.68];

    public static SalonAkustikOnKontrolSonucu Hesapla(
        SalonGeometri salon,
        SalonKullanimTipi kullanim = SalonKullanimTipi.CokAmacli,
        IReadOnlyDictionary<string, IReadOnlyList<double>>? kategoriYutumlari = null)
    {
        ArgumentNullException.ThrowIfNull(salon);
        double hacim = Math.Max(0, SalonHacimServisi.Hesapla(salon));
        var yuzeyler = Salon3DGorunumServisi.YuzeyleriOlustur(salon);
        var alanlar = yuzeyler
            .GroupBy(s => string.IsNullOrWhiteSpace(s.Category) ? "Genel" : s.Category,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(s => SurfaceTransformService.ComputeArea(s.Vertices)),
                StringComparer.OrdinalIgnoreCase);
        double toplamAlan = alanlar.Values.Sum();

        var yerlesimler = salon.SeyirciSekciyonlari
            .Select(s => SalonKoltukYerlesimServisi.Uret(s, salon.Sahne))
            .ToList();
        int koltuk = yerlesimler.Sum(x => x.Koltuklar.Count);
        double kisiHacim = koltuk > 0 ? hacim / koltuk : 0;
        var (hedefMin, hedefMax) = HedefAralik(kullanim);
        var rt = new List<SalonRtBandi>();

        for (int band = 0; band < Bantlar.Length; band++)
        {
            double a = koltuk * DoluKoltukSabin[band];
            foreach (var (kategori, alan) in alanlar)
                a += alan * Yutum(kategori, band, kategoriYutumlari);
            a = Math.Max(0.01, a);
            double rt60 = hacim > 0 ? 0.161 * hacim / a : 0;
            rt.Add(new SalonRtBandi(
                Bantlar[band], a, rt60, hedefMin, hedefMax,
                rt60 >= hedefMin && rt60 <= hedefMax));
        }

        var bulgular = new List<string>();
        if (hacim <= 0) bulgular.Add("HATA: Geçerli kapalı hava hacmi üretilemedi.");
        if (koltuk == 0) bulgular.Add("HATA: Gerçek koltuk yerleşimi üretilemedi.");
        if (koltuk > 0 && (kisiHacim < 3 || kisiHacim > 12))
            bulgular.Add($"UYARI: Koltuk başına hacim {kisiHacim:F1} m³; 3–12 m³ tipik tasarım bandının dışında.");
        var orta = rt.Where(x => x.FrekansHz is 500 or 1000).ToList();
        if (orta.Count > 0 && orta.Any(x => !x.Hedefte))
            bulgular.Add($"UYARI: 500–1000 Hz kaba RT60 tahmini {orta.Average(x => x.TahminiRt60S):F2} s; " +
                         $"{KullanimAdi(kullanim)} için hedef {hedefMin:F1}–{hedefMax:F1} s.");
        if (toplamAlan > 0)
        {
            double tavanOrani = KategoriTipAlani(salon, alanlar, SalonKatmanTipi.Tavan) / toplamAlan;
            if (tavanOrani < 0.08)
                bulgular.Add("BİLGİ: Tavan yüzey oranı düşük görünüyor; tavan/bulut yansımalarını kontrol edin.");
        }
        foreach (var uyari in yerlesimler.SelectMany(x => x.Uyarilar).Distinct())
            bulgular.Add("UYARI: " + uyari);

        return new SalonAkustikOnKontrolSonucu(hacim, toplamAlan, koltuk, kisiHacim, alanlar, rt, bulgular);
    }

    private static double Yutum(
        string kategori,
        int band,
        IReadOnlyDictionary<string, IReadOnlyList<double>>? overrides)
    {
        if (overrides is not null)
        {
            var d = overrides.FirstOrDefault(x =>
                string.Equals(x.Key, kategori, StringComparison.OrdinalIgnoreCase)).Value;
            if (d is { Count: > 0 })
            {
                double deger = d[Math.Min(band, d.Count - 1)];
                if (double.IsFinite(deger)) return Math.Clamp(deger, 0, 1);
            }
        }

        string k = kategori.ToLowerInvariant();
        double orta = k.Contains("yansıt") || k.Contains("yansit") ? 0.04
            : k.Contains("pencere") ? 0.12
            : k.Contains("kapı") || k.Contains("kapi") ? 0.10
            : k.Contains("seyirci") ? 0.10
            : k.Contains("tavan") ? 0.10
            : k.Contains("zemin") || k.Contains("sahne") ? 0.06
            : k.Contains("duvar") || k.Contains("korniş") || k.Contains("kornis") ? 0.07
            : 0.08;
        double[] carpan = [0.70, 0.85, 1.0, 1.08, 1.12, 1.15];
        return Math.Clamp(orta * carpan[band], 0.01, 0.95);
    }

    private static (double Min, double Max) HedefAralik(SalonKullanimTipi tip) => tip switch
    {
        SalonKullanimTipi.Konusma => (0.7, 1.0),
        SalonKullanimTipi.Muzik => (1.4, 2.0),
        SalonKullanimTipi.Sinema => (0.8, 1.2),
        _ => (1.0, 1.5)
    };

    private static string KullanimAdi(SalonKullanimTipi tip) => tip switch
    {
        SalonKullanimTipi.Konusma => "konuşma",
        SalonKullanimTipi.Muzik => "müzik",
        SalonKullanimTipi.Sinema => "sinema",
        _ => "çok amaçlı salon"
    };

    private static double KategoriTipAlani(
        SalonGeometri salon,
        IReadOnlyDictionary<string, double> alanlar,
        SalonKatmanTipi tip)
    {
        var adlar = salon.Katmanlar.Where(k => k.Tip == tip).Select(k => k.Ad).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return alanlar.Where(x => adlar.Contains(x.Key)).Sum(x => x.Value);
    }
}
