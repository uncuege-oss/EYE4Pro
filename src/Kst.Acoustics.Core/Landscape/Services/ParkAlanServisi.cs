using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Landscape.Models;

namespace Kst.Acoustics.Core.Landscape.Services;

/// M70 F2 — bina + yapı yaklaşma (setback) dışlaması ve kullanılabilir park alanı.
/// `park sınırı − (bina ayak izi ⊕ setback)` Clipper2 ile hesaplanır
/// (<see cref="ClipperAdaptor"/>). Türetilmiş veridir; serileştirilmez, çağrıldıkça üretilir.
public sealed class KullanilabilirAlanSonucu
{
    /// Kullanılabilir alanın halkaları (dış sınır + delikler). Bina/setback park
    /// içinde delik açar; park dışına taşan kısım `Fark` ile kırpılır.
    public List<List<Point2D>> Halkalar { get; init; } = new();

    public double ToplamParkM2 { get; init; }
    public double DislananM2 { get; init; }      // park içinde bina+setback'in kapladığı
    public double KullanilabilirM2 { get; init; } // yerleştirilebilir net alan
}

public static class ParkAlanServisi
{
    /// Park sınırından bina+setback dışlamalarını çıkarıp kullanılabilir alanı verir.
    public static KullanilabilirAlanSonucu KullanilabilirAlan(ParkModeli park)
    {
        var parkPoligon = park.Sinir.Poligon;
        double toplamPark = PoligonYardimci.Alan(parkPoligon);

        if (parkPoligon.Count < 3)
            return new KullanilabilirAlanSonucu { ToplamParkM2 = 0 };

        // Her bina için: ayak izi + setback offset (setback yoksa ayak izinin kendisi).
        var dislamaPoligonlari = new List<IReadOnlyList<Point2D>>();
        foreach (var b in park.BinaDislamalari)
        {
            if (b.AyakIzi.Count < 3) continue;
            if (b.SetbackM > 0)
                dislamaPoligonlari.AddRange(ClipperAdaptor.Tampon(b.AyakIzi, b.SetbackM));
            else
                dislamaPoligonlari.Add(b.AyakIzi);
        }

        // Dışlama yoksa tüm park kullanılabilir.
        if (dislamaPoligonlari.Count == 0)
        {
            var disSinir = parkPoligon.ToList();
            if (ClipperAdaptor.IsaretliAlan(disSinir) < 0) disSinir.Reverse();
            return new KullanilabilirAlanSonucu
            {
                Halkalar = new() { disSinir },
                ToplamParkM2 = toplamPark,
                DislananM2 = 0,
                KullanilabilirM2 = toplamPark
            };
        }

        // Örtüşen tamponları birleştir, sonra parktan çıkar.
        var birlesik = ClipperAdaptor.Birlestir(dislamaPoligonlari);
        var fark = ClipperAdaptor.Fark(parkPoligon, birlesik);
        double kullanilabilir = ClipperAdaptor.NetAlan(fark);

        return new KullanilabilirAlanSonucu
        {
            Halkalar = fark,
            ToplamParkM2 = toplamPark,
            DislananM2 = toplamPark - kullanilabilir,
            KullanilabilirM2 = kullanilabilir
        };
    }

    /// Nokta, kullanılabilir (delikli) park bölgesinin içinde mi? Dış halka (CCW,
    /// işaretli alan ≥ 0) içinde VE hiçbir delik (CW) içinde değilse true.
    public static bool KullanilabilirIcinde(Point2D nokta, IReadOnlyList<IReadOnlyList<Point2D>> halkalar)
    {
        bool disHalkada = false, delikte = false;
        foreach (var halka in halkalar)
        {
            if (!PoligonYardimci.IsPointInside(nokta, halka)) continue;
            if (ClipperAdaptor.IsaretliAlan(halka) >= 0) disHalkada = true;
            else delikte = true;
        }
        return disHalkada && !delikte;
    }
}
