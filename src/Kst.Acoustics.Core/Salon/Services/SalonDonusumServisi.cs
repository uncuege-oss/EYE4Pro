using System;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Salon.Models;

namespace Kst.Acoustics.Core.Salon.Services;

/// M51 çizim öğesi dönüşümleri: taşı / döndür / aynala / kopyala.
/// Yay açıları dönüşümlerde düzeltilir (döndürmede öteleme, aynalamada CCW yönünün
/// korunması için uçların takası). Editörün Taşı/Döndür/Aynala araçları bunu kullanır.
public static class SalonDonusumServisi
{
    /// Öğeyi (dx, dy) kadar öteler.
    public static void Tasi(SalonCizimOgesi oge, double dx, double dy)
    {
        for (int i = 0; i < oge.Noktalar.Count; i++)
            oge.Noktalar[i] = new Point2D(oge.Noktalar[i].X + dx, oge.Noktalar[i].Y + dy);
    }

    /// Öğeyi merkez etrafında CCW döndürür (derece).
    public static void Dondur(SalonCizimOgesi oge, Point2D merkez, double aciDeg)
    {
        double a = aciDeg * Math.PI / 180.0;
        double cos = Math.Cos(a), sin = Math.Sin(a);
        for (int i = 0; i < oge.Noktalar.Count; i++)
        {
            double px = oge.Noktalar[i].X - merkez.X, py = oge.Noktalar[i].Y - merkez.Y;
            oge.Noktalar[i] = new Point2D(
                merkez.X + px * cos - py * sin,
                merkez.Y + px * sin + py * cos);
        }
        if (oge.Tip == SalonCizimTipi.Yay)
        {
            oge.AciBasDeg += aciDeg;
            oge.AciBitisDeg += aciDeg;
        }
    }

    /// Öğeyi (e1→e2) doğrusu etrafında yansıtır. Yayda açı φ → 2θ−φ dönüşümü CCW
    /// yönünü tersine çevirdiğinden başlangıç/bitiş takas edilir (geometri aynı kalır).
    public static void Aynala(SalonCizimOgesi oge, Point2D e1, Point2D e2)
    {
        double dx = e2.X - e1.X, dy = e2.Y - e1.Y;
        double len2 = dx * dx + dy * dy;
        if (len2 < 1e-12) return;

        Point2D Yansit(Point2D p)
        {
            double t = ((p.X - e1.X) * dx + (p.Y - e1.Y) * dy) / len2;
            double ax = e1.X + t * dx, ay = e1.Y + t * dy; // dik ayak
            return new Point2D(2 * ax - p.X, 2 * ay - p.Y);
        }

        for (int i = 0; i < oge.Noktalar.Count; i++)
            oge.Noktalar[i] = Yansit(oge.Noktalar[i]);

        if (oge.Tip == SalonCizimTipi.Yay)
        {
            double eksenDeg = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            double yeniBas = 2 * eksenDeg - oge.AciBitisDeg;
            double yeniBitis = 2 * eksenDeg - oge.AciBasDeg;
            oge.AciBasDeg = yeniBas;
            oge.AciBitisDeg = yeniBitis;
        }
    }

    /// Derin kopya (yeni Id; nokta listesi bağımsız). Katman bağı korunur.
    public static SalonCizimOgesi Kopyala(SalonCizimOgesi oge) => new()
    {
        Tip = oge.Tip,
        Noktalar = oge.Noktalar.ToList(),
        YariCapM = oge.YariCapM,
        AciBasDeg = oge.AciBasDeg,
        AciBitisDeg = oge.AciBitisDeg,
        Kapali = oge.Kapali,
        KatmanId = oge.KatmanId
    };
}
