using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Landscape.Models;
using Kst.Acoustics.Core.Terrain.Services;

namespace Kst.Acoustics.Core.Landscape.Services;

/// Bir öğe yerleşiminin geçerlilik sonucu.
public sealed class YerlesimDogrulama
{
    public bool Gecerli => Uyarilar.Count == 0;
    public List<string> Uyarilar { get; } = new();
}

/// M70 F3 — sert zemin öğelerinin yerleşimi: ekle/sil/taşı (kararlı Id),
/// araziden zemin kotu atama ve kullanılabilir alana göre sınır-içi/çakışma
/// kontrolü. Bina/arazi salt-okunurdur; yalnız park öğeleri düzenlenir.
public static class ParkYerlesimServisi
{
    /// Öğeyi modele ekler; kararlı `Id` atar (`SonrakiOgeId` sayacı — mutabakat için).
    public static ParkOgesi OgeEkle(ParkModeli park, ParkOgesi oge)
    {
        oge.Id = park.SonrakiOgeId++;
        park.Ogeler.Add(oge);
        return oge;
    }

    /// Id ile öğeyi siler; bulunursa true.
    public static bool OgeSil(ParkModeli park, int id)
    {
        var oge = park.Ogeler.FirstOrDefault(o => o.Id == id);
        return oge is not null && park.Ogeler.Remove(oge);
    }

    /// Öğenin tüm geometrisini (dx, dy) kadar öteler.
    public static void OgeTasi(ParkOgesi oge, double dx, double dy)
    {
        switch (oge)
        {
            case Bank b:
                b.Konum = new Point2D(b.Konum.X + dx, b.Konum.Y + dy);
                break;
            case YuruyusYolu y:
                y.Guzergah = Otele(y.Guzergah, dx, dy);
                break;
            case DosemeAlani d:
                d.Poligon = Otele(d.Poligon, dx, dy);
                break;
            case OturmaAlani o:
                o.Poligon = Otele(o.Poligon, dx, dy);
                break;
            case SusHavuzu s:
                s.Poligon = Otele(s.Poligon, dx, dy);
                break;
        }
    }

    /// Öğenin temsil (merkez) noktasından araziden zemin kotu atar. Arazi yoksa
    /// veya nokta yüzey dışındaysa kot null kalır.
    public static void ZeminKotunuAta(ParkOgesi oge, AraziTuketim? arazi)
    {
        if (arazi is null) { oge.ZeminKotuM = null; return; }
        var m = Merkez(oge);
        oge.ZeminKotuM = arazi.KotSorgula(m.X, m.Y);
    }

    /// Tüm öğelere araziden kot atar.
    public static void ZeminKotlariniAta(ParkModeli park, AraziTuketim? arazi)
    {
        foreach (var oge in park.Ogeler)
            ZeminKotunuAta(oge, arazi);
    }

    /// Öğenin yerleşimini kullanılabilir alana göre doğrular. `kullanilabilir`
    /// <see cref="ParkAlanServisi.KullanilabilirAlan"/> sonucunun halkalarıdır.
    /// `arazi` verilirse ve merkez kotu çözülemiyorsa uyarı eklenir.
    public static YerlesimDogrulama Dogrula(
        ParkOgesi oge, IReadOnlyList<IReadOnlyList<Point2D>> kullanilabilir, AraziTuketim? arazi = null)
    {
        var d = new YerlesimDogrulama();

        foreach (var n in KontrolNoktalari(oge))
        {
            if (!ParkAlanServisi.KullanilabilirIcinde(n, kullanilabilir))
            {
                d.Uyarilar.Add($"Öğe '{Ad(oge)}' kullanılabilir park alanı dışına taşıyor " +
                               "(park sınırı dışı veya bina+setback üstünde).");
                break;
            }
        }

        if (arazi is not null)
        {
            var m = Merkez(oge);
            if (arazi.KotSorgula(m.X, m.Y) is null)
                d.Uyarilar.Add($"Öğe '{Ad(oge)}' merkezinde arazi kotu çözülemedi (yüzey dışı).");
        }

        return d;
    }

    // ── Yardımcılar ──────────────────────────────────────────────────────────

    private static List<Point2D> Otele(IReadOnlyList<Point2D> p, double dx, double dy)
        => p.Select(q => new Point2D(q.X + dx, q.Y + dy)).ToList();

    /// Öğenin sınır-içi kontrolünde kullanılan noktalar (poligon köşeleri / yol
    /// güzergâhı / bank konumu).
    private static IReadOnlyList<Point2D> KontrolNoktalari(ParkOgesi oge) => oge switch
    {
        Bank b        => new List<Point2D> { b.Konum },
        YuruyusYolu y => y.Guzergah,
        DosemeAlani d => d.Poligon,
        OturmaAlani o => o.Poligon,
        SusHavuzu s   => s.Poligon,
        _             => new List<Point2D>()
    };

    /// Kot ataması ve merkez tabanlı kontroller için temsil noktası (ağırlık merkezi).
    private static Point2D Merkez(ParkOgesi oge)
    {
        if (oge is Bank b) return b.Konum;
        var pts = KontrolNoktalari(oge);
        if (pts.Count == 0) return new Point2D(0, 0);
        return new Point2D(pts.Average(p => p.X), pts.Average(p => p.Y));
    }

    private static string Ad(ParkOgesi oge)
        => string.IsNullOrWhiteSpace(oge.Ad) ? oge.Tip.ToString() : oge.Ad;
}
