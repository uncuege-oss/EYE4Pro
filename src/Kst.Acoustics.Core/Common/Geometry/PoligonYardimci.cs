using System;
using System.Collections.Generic;
using System.Linq;

namespace Kst.Acoustics.Core.Common.Geometry;

/// Ortak poligon ve geometri hesaplamaları (M08 + M12 birleştirmesi)
public static class PoligonYardimci
{
    /// Ray-casting algoritması. M12'nin epsilon (+1e-12) versiyonu alındı (sayısal daha sağlam).
    public static bool IsPointInside(Point2D p, IReadOnlyList<Point2D> poly)
    {
        if (poly.Count < 3) return false;

        double x = p.X, y = p.Y;
        bool inside = false;

        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            double xi = poly[i].X, yi = poly[i].Y;
            double xj = poly[j].X, yj = poly[j].Y;

            bool intersect = ((yi > y) != (yj > y)) && (x < (xj - xi) * (y - yi) / (yj - yi + 1e-12) + xi);
            if (intersect) inside = !inside;
        }

        return inside;
    }

    /// Noktanın bir doğru segmentine olan mesafesi (M08 versiyonu)
    public static double SegmentMesafesi(Point2D p, Point2D a, Point2D b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (dx * dx + dy * dy);
        t = Math.Max(0, Math.Min(1, t));

        double projX = a.X + t * dx;
        double projY = a.Y + t * dy;

        return Math.Sqrt((p.X - projX) * (p.X - projX) + (p.Y - projY) * (p.Y - projY));
    }

    /// Noktanın bir doğru segmentine izdüşümü (M12 versiyonu)
    public static Point2D SegmentProje(Point2D p, Point2D a, Point2D b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double denominator = dx * dx + dy * dy;

        if (denominator < 1e-12)
            return a; // Dejenere segment (a == b)

        double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / denominator;
        t = Math.Max(0, Math.Min(1, t));

        return new Point2D(a.X + t * dx, a.Y + t * dy);
    }

    /// Poligon alanı (Shoelace formülü)
    public static double Alan(IReadOnlyList<Point2D> poly)
    {
        if (poly.Count < 3) return 0;

        double area = 0;
        for (int i = 0; i < poly.Count; i++)
        {
            int j = (i + 1) % poly.Count;
            area += poly[i].X * poly[j].Y;
            area -= poly[j].X * poly[i].Y;
        }

        return Math.Abs(area) / 2.0;
    }

    /// Poligonun alan-ağırlıklı ağırlık merkezi (centroid). Köşe ortalamasından
    /// farklı olarak bir kenarda yoğunlaşan köşelere kaymaz. Dejenere (sıfır alanlı /
    /// doğrusal) poligonlarda köşe ortalamasına, boş poligonda (0,0)'a düşer.
    public static Point2D Centroid(IReadOnlyList<Point2D> poly)
    {
        if (poly.Count == 0) return new Point2D(0, 0);
        if (poly.Count < 3) return KoseOrtalama(poly);

        double a2 = 0, cx = 0, cy = 0;
        for (int i = 0; i < poly.Count; i++)
        {
            int j = (i + 1) % poly.Count;
            double cross = poly[i].X * poly[j].Y - poly[j].X * poly[i].Y;
            a2 += cross;
            cx += (poly[i].X + poly[j].X) * cross;
            cy += (poly[i].Y + poly[j].Y) * cross;
        }

        if (Math.Abs(a2) < 1e-12) return KoseOrtalama(poly); // dejenere → köşe ortalaması
        return new Point2D(cx / (3.0 * a2), cy / (3.0 * a2));
    }

    private static Point2D KoseOrtalama(IReadOnlyList<Point2D> poly)
    {
        double sx = 0, sy = 0;
        foreach (var p in poly) { sx += p.X; sy += p.Y; }
        return new Point2D(sx / poly.Count, sy / poly.Count);
    }

    /// Nokta kümesinin konveks kabuğu (Andrew monotone chain), CCW sırada.
    /// 3'ten az benzersiz nokta varsa noktalar olduğu gibi (X,Y sıralı) döner.
    /// M70 park sınırını arazi TIN düğümlerinden türetmek için kullanılır.
    public static List<Point2D> KonveksKabuk(IReadOnlyList<Point2D> noktalar)
    {
        var pts = noktalar.Distinct().OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
        if (pts.Count < 3) return pts;

        static double Capraz(Point2D o, Point2D a, Point2D b)
            => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);

        var alt = new List<Point2D>();
        foreach (var p in pts)
        {
            while (alt.Count >= 2 && Capraz(alt[^2], alt[^1], p) <= 0) alt.RemoveAt(alt.Count - 1);
            alt.Add(p);
        }

        var ust = new List<Point2D>();
        for (int i = pts.Count - 1; i >= 0; i--)
        {
            var p = pts[i];
            while (ust.Count >= 2 && Capraz(ust[^2], ust[^1], p) <= 0) ust.RemoveAt(ust.Count - 1);
            ust.Add(p);
        }

        // Son nokta her zincirin başlangıcıyla tekrarlanır → birini at.
        alt.RemoveAt(alt.Count - 1);
        ust.RemoveAt(ust.Count - 1);
        alt.AddRange(ust);
        return alt;
    }
}
