using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Landscape.Models;
using Kst.Acoustics.Core.Terrain.Services;

namespace Kst.Acoustics.Core.Landscape.Services;

/// F4 alan/oran ve erişilebilirlik ön kontrol eşikleri. Varsayılanlar genel kabul;
/// proje/yerel yönetmeliğe göre dışarıdan verilebilir (maincodex §4.9 standartlar).
public sealed class ParkKurallari
{
    /// Erişilebilir yürüyüş yolu min. genişlik (m) — TS 9111 geçiş genişliği.
    public double MinYolGenisligiM { get; init; } = 1.5;

    /// Yürüyüş yolu max. boyuna eğim (oran) — %5 = 1/20.
    public double MaxYolEgimi { get; init; } = 0.05;

    /// Min. yeşil alan oranı (toplam parka göre). 0 = kontrol kapalı.
    public double MinYesilOran { get; init; } = 0.0;
}

/// M70 F4 — park alan/oran özeti + erişilebilirlik ön kontrolü. Sert zemin alanı
/// öğelerden, yeşil alan kullanılabilir alandan türetilir; yol genişlik/eğim ve
/// yeşil oran eşikleri uyarı üretir. Hesap Core'da, UI yalnız gösterir (maincodex §4.4).
public static class ParkOranServisi
{
    public static AlanOzeti Hesapla(ParkModeli park, AraziTuketim? arazi = null, ParkKurallari? kurallar = null)
    {
        kurallar ??= new ParkKurallari();

        var alan = ParkAlanServisi.KullanilabilirAlan(park);
        double sertZemin = park.Ogeler.Sum(SertZeminAlani);
        double yesil = Math.Max(0, alan.KullanilabilirM2 - sertZemin);
        double toplam = alan.ToplamParkM2;

        var ozet = new AlanOzeti
        {
            ToplamParkM2 = toplam,
            KullanilabilirM2 = alan.KullanilabilirM2,
            SertZeminM2 = sertZemin,
            YesilAlanM2 = yesil,
            YesilOran = toplam > 0 ? yesil / toplam : 0,
            SertZeminOran = toplam > 0 ? sertZemin / toplam : 0
        };

        // Yeşil oran ön kontrolü.
        if (kurallar.MinYesilOran > 0 && ozet.YesilOran < kurallar.MinYesilOran)
            ozet.Uyarilar.Add(
                $"Yeşil alan oranı %{ozet.YesilOran * 100:N1}, asgari %{kurallar.MinYesilOran * 100:N1} altında.");

        // Yürüyüş yolu erişilebilirlik ön kontrolü.
        foreach (var yol in park.Ogeler.OfType<YuruyusYolu>())
        {
            string ad = string.IsNullOrWhiteSpace(yol.Ad) ? "Yürüyüş yolu" : yol.Ad;

            if (yol.GenislikM < kurallar.MinYolGenisligiM)
                ozet.Uyarilar.Add(
                    $"'{ad}' genişliği {yol.GenislikM:N2} m, asgari {kurallar.MinYolGenisligiM:N2} m altında.");

            double? egim = MaxEgim(yol, arazi);
            if (egim is double e && e > kurallar.MaxYolEgimi)
                ozet.Uyarilar.Add(
                    $"'{ad}' boyuna eğimi %{e * 100:N1}, azami %{kurallar.MaxYolEgimi * 100:N1} üstünde.");
        }

        return ozet;
    }

    /// Bir öğenin sert zemin alanı (m²). Bank mobilyadır, alan saymaz; yol =
    /// güzergâh uzunluğu × genişlik. Öğe örtüşmeleri tekilleştirilmez (F8 union).
    private static double SertZeminAlani(ParkOgesi oge) => oge switch
    {
        DosemeAlani d => PoligonYardimci.Alan(d.Poligon),
        OturmaAlani o => PoligonYardimci.Alan(o.Poligon),
        SusHavuzu s   => PoligonYardimci.Alan(s.Poligon),
        YuruyusYolu y => GuzergahUzunlugu(y.Guzergah) * y.GenislikM,
        Bank          => 0,
        _             => 0
    };

    private static double GuzergahUzunlugu(IReadOnlyList<Point2D> g)
    {
        double u = 0;
        for (int i = 1; i < g.Count; i++)
        {
            double dx = g[i].X - g[i - 1].X, dy = g[i].Y - g[i - 1].Y;
            u += Math.Sqrt(dx * dx + dy * dy);
        }
        return u;
    }

    /// Yolun arazi üzerindeki en büyük segment boyuna eğimi (oran); arazi yoksa
    /// veya kot çözülemezse null.
    private static double? MaxEgim(YuruyusYolu yol, AraziTuketim? arazi)
    {
        if (arazi is null || yol.Guzergah.Count < 2) return null;

        double maks = 0;
        bool varMi = false;
        for (int i = 1; i < yol.Guzergah.Count; i++)
        {
            var a = yol.Guzergah[i - 1];
            var b = yol.Guzergah[i];
            var za = arazi.KotSorgula(a.X, a.Y);
            var zb = arazi.KotSorgula(b.X, b.Y);
            if (za is null || zb is null) continue;

            double yatay = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
            if (yatay < 1e-9) continue;

            maks = Math.Max(maks, Math.Abs(zb.Value - za.Value) / yatay);
            varMi = true;
        }
        return varMi ? maks : null;
    }
}
