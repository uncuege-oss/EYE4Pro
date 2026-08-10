using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Salon.Models;

/// 2D çizim öğesi türü (SketchUp benzeri serbest eskiz).
public enum SalonCizimTipi
{
    Cizgi,       // 2 nokta
    Polyline,    // N nokta (Kapali ise son nokta ilk noktaya bağlanır)
    Dikdortgen,  // 4 köşe, daima kapalı
    Daire,       // Merkez = Noktalar[0], YariCapM
    Yay          // Merkez = Noktalar[0], YariCapM, AciBasDeg → AciBitisDeg (CCW)
}

/// Salon eskizindeki tekil 2D öğe. Metre tabanlı koordinatlar.
/// Daire/oval gibi eğriler 3D üretim ve DXF ihracatı anında çokgene fasetlenir.
public class SalonCizimOgesi : ISalonCizimOgesi
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public SalonCizimTipi Tip { get; set; } = SalonCizimTipi.Cizgi;

    /// Öğenin köşe/uç noktaları (metre). Daire için yalnız merkez (tek nokta) tutulur.
    public List<Point2D> Noktalar { get; set; } = new();

    /// Daire/yay yarıçapı (metre). Yalnız Daire ve Yay için anlamlı.
    public double YariCapM { get; set; }

    /// Yay başlangıç açısı (derece, +X ekseninden CCW). Yalnız <see cref="SalonCizimTipi.Yay"/>.
    public double AciBasDeg { get; set; }

    /// Yay bitiş açısı (derece). Yay her zaman <see cref="AciBasDeg"/>'den CCW yönde
    /// buraya çizilir (bitiş &lt; başlangıç ise +360 kabul edilir).
    public double AciBitisDeg { get; set; }

    /// Polyline'ın kapalı olup olmadığı. Dikdörtgen ve daire her zaman kapalıdır.
    public bool Kapali { get; set; }

    /// Bağlı katman kimliği (<see cref="SalonKatman.Id"/>). Boş = atanmamış.
    public string KatmanId { get; set; } = "";

    /// Birlikte seçilip dönüştürülen isteğe bağlı grup kimliği.
    public string? GrupId { get; set; }

    /// Yeniden kullanılabilir blok örneğinin adı; null = bağımsız öğe.
    public string? BlokAdi { get; set; }

    // ISalonCizimOgesi salt-okunur görünüm
    IReadOnlyList<Point2D> ISalonCizimOgesi.Noktalar => Noktalar;

    /// Daireyi <paramref name="kenarSayisi"/> kenarlı çokgene fasetler.
    public List<Point2D> DaireyiFasetle(int kenarSayisi = 48)
    {
        var sonuc = new List<Point2D>(kenarSayisi);
        if (Noktalar.Count == 0 || YariCapM <= 0) return sonuc;
        var m = Noktalar[0];
        for (int i = 0; i < kenarSayisi; i++)
        {
            double a = 2.0 * Math.PI * i / kenarSayisi;
            sonuc.Add(new Point2D(m.X + YariCapM * Math.Cos(a), m.Y + YariCapM * Math.Sin(a)));
        }
        return sonuc;
    }

    /// Öğeyi kapalı bir poligona dönüştürür (zemin/yüzey üretimi için).
    /// Açık çizgi/polyline boş döner.
    public List<Point2D> KapaliPoligon(int daireKenar = 48)
    {
        return Tip switch
        {
            SalonCizimTipi.Daire => DaireyiFasetle(daireKenar),
            SalonCizimTipi.Dikdortgen => Noktalar.ToList(),
            SalonCizimTipi.Polyline when Kapali => Noktalar.ToList(),
            _ => new List<Point2D>()
        };
    }

    /// Yayı çokgen kırıklarına fasetler (uçlar dahil; kenar sayısı yay açısına orantılı).
    /// <paramref name="daireKenar"/> tam çember için kenar sayısıdır.
    public List<Point2D> YayiFasetle(int daireKenar = 48)
    {
        var sonuc = new List<Point2D>();
        if (Noktalar.Count == 0 || YariCapM <= 0) return sonuc;
        var m = Noktalar[0];
        double bas = AciBasDeg * Math.PI / 180.0;
        double aralik = (AciBitisDeg - AciBasDeg) * Math.PI / 180.0;
        while (aralik <= 0) aralik += 2 * Math.PI;
        int n = Math.Max(2, (int)Math.Ceiling(daireKenar * aralik / (2 * Math.PI)));
        for (int i = 0; i <= n; i++)
        {
            double a = bas + aralik * i / n;
            sonuc.Add(new Point2D(m.X + YariCapM * Math.Cos(a), m.Y + YariCapM * Math.Sin(a)));
        }
        return sonuc;
    }

    /// Yayın uç noktaları (başlangıç, bitiş). Yay değilse boş.
    public (Point2D Bas, Point2D Bitis)? YayUclari()
    {
        if (Tip != SalonCizimTipi.Yay || Noktalar.Count == 0 || YariCapM <= 0) return null;
        var m = Noktalar[0];
        double b = AciBasDeg * Math.PI / 180.0, e = AciBitisDeg * Math.PI / 180.0;
        return (new Point2D(m.X + YariCapM * Math.Cos(b), m.Y + YariCapM * Math.Sin(b)),
                new Point2D(m.X + YariCapM * Math.Cos(e), m.Y + YariCapM * Math.Sin(e)));
    }

    /// Üç noktadan (başlangıç, bitiş, yay üzerinde kavis noktası) yay kurar.
    /// Noktalar doğrusala yakınsa null döner (çizgi çizilmelidir).
    public static SalonCizimOgesi? UcNoktadanYay(Point2D bas, Point2D bitis, Point2D kavis)
    {
        // Çevrel çember merkezi (determinant yöntemi)
        double d = 2 * (bas.X * (bitis.Y - kavis.Y) + bitis.X * (kavis.Y - bas.Y) + kavis.X * (bas.Y - bitis.Y));
        if (Math.Abs(d) < 1e-9) return null; // doğrusal

        double b2 = bas.X * bas.X + bas.Y * bas.Y;
        double e2 = bitis.X * bitis.X + bitis.Y * bitis.Y;
        double k2 = kavis.X * kavis.X + kavis.Y * kavis.Y;
        double cx = (b2 * (bitis.Y - kavis.Y) + e2 * (kavis.Y - bas.Y) + k2 * (bas.Y - bitis.Y)) / d;
        double cy = (b2 * (kavis.X - bitis.X) + e2 * (bas.X - kavis.X) + k2 * (bitis.X - bas.X)) / d;
        double r = Math.Sqrt((bas.X - cx) * (bas.X - cx) + (bas.Y - cy) * (bas.Y - cy));
        if (r < 1e-6 || r > 1e5) return null;

        double aBas = Math.Atan2(bas.Y - cy, bas.X - cx) * 180.0 / Math.PI;
        double aBit = Math.Atan2(bitis.Y - cy, bitis.X - cx) * 180.0 / Math.PI;
        double aKav = Math.Atan2(kavis.Y - cy, kavis.X - cx) * 180.0 / Math.PI;

        // Kavis noktası CCW yönde bas→bitis aralığında mı? Değilse uçları çevir
        // (yay her zaman CCW saklanır; geometri aynıdır).
        static double Norm(double a) { while (a < 0) a += 360; while (a >= 360) a -= 360; return a; }
        double span = Norm(aBit - aBas);
        bool ccwIcinde = Norm(aKav - aBas) <= span + 1e-9;

        return new SalonCizimOgesi
        {
            Tip = SalonCizimTipi.Yay,
            YariCapM = r,
            Noktalar = new() { new Point2D(cx, cy) },
            AciBasDeg = ccwIcinde ? aBas : aBit,
            AciBitisDeg = ccwIcinde ? aBas + span : aBit + Norm(aBas - aBit)
        };
    }

    /// Öğeyi doğru parçalarına (segment) ayırır. Trim kesici kenarları, hit-test,
    /// 3D üretim ve DXF ihracatı bu segment listesini kullanır.
    public IEnumerable<(Point2D A, Point2D B)> Segmentler(int daireKenar = 48)
    {
        switch (Tip)
        {
            case SalonCizimTipi.Cizgi:
                if (Noktalar.Count >= 2)
                    yield return (Noktalar[0], Noktalar[1]);
                break;

            case SalonCizimTipi.Yay:
            {
                var fy = YayiFasetle(daireKenar);
                for (int i = 0; i + 1 < fy.Count; i++)
                    yield return (fy[i], fy[i + 1]);
                break;
            }

            case SalonCizimTipi.Polyline:
                for (int i = 0; i + 1 < Noktalar.Count; i++)
                    yield return (Noktalar[i], Noktalar[i + 1]);
                if (Kapali && Noktalar.Count > 2)
                    yield return (Noktalar[^1], Noktalar[0]);
                break;

            case SalonCizimTipi.Dikdortgen:
                for (int i = 0; i < Noktalar.Count; i++)
                    yield return (Noktalar[i], Noktalar[(i + 1) % Noktalar.Count]);
                break;

            case SalonCizimTipi.Daire:
                var fp = DaireyiFasetle(daireKenar);
                for (int i = 0; i < fp.Count; i++)
                    yield return (fp[i], fp[(i + 1) % fp.Count]);
                break;
        }
    }
}
