using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;

namespace Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

public static class ArmaturYerlesimServisi
{
    public static List<BasitProjeEleman> YerlesimOlustur(BasitProjeOda oda, BpcArmatürTanim armatür)
    {
        var points = Grid(oda, armatür);

        return points.Select((p, i) => new BasitProjeEleman
        {
            Tip            = BpcElemanTipi.Armatür,
            Ad             = $"{armatür.SembolAdi}-{i + 1}",
            EkranAdi       = string.IsNullOrWhiteSpace(armatür.EkranAdi) ? armatür.Ad : armatür.EkranAdi,
            OdaId          = oda.Id,
            ArmatürTanimId = armatür.Id,
            BlokAdi        = armatür.BlokAdi,
            X              = p.X,
            Y              = p.Y
        }).ToList();
    }

    // ─── Grid yerleşim ───────────────────────────────────────────────────

    private static List<Point2D> Grid(BasitProjeOda oda, BpcArmatürTanim armatür)
    {
        var (minX, minY, maxX, maxY) = Bounds(oda);
        double stepX = Math.Max(1.0, armatür.GridAraligiM);
        double stepY = Math.Max(1.0, armatür.GridAraligiYM ?? armatür.GridAraligiM);
        var    result = new List<Point2D>();
        for (double x = minX + stepX / 2; x < maxX; x += stepX)
        for (double y = minY + stepY / 2; y < maxY; y += stepY)
        {
            var p = new Point2D(x, y);
            if (PoligonYardimci.IsPointInside(p, oda.Poligon))
                result.Add(p);
        }
        if (result.Count == 0) result.Add(PoligonIcMerkezi(oda));
        return result;
    }

    // ─── Poligon iç merkez ────────────────────────────────────────────────

    /// Shoelace centroid döner; polygon dışındaysa yatay tarama ile en geniş
    /// iç alanın ortasını döner — karmaşık şekiller için de çalışır.
    public static Point2D PoligonIcMerkezi(BasitProjeOda oda)
    {
        if (oda.Poligon.Count == 0) return new Point2D();

        var centroid = CentroidShoelace(oda.Poligon);
        if (PoligonYardimci.IsPointInside(centroid, oda.Poligon))
            return centroid;

        var (minX, minY, maxX, maxY) = Bounds(oda);
        Point2D best     = oda.Poligon[0];
        double   bestSpan = -1;
        for (int i = 1; i <= 20; i++)
        {
            double y = minY + (maxY - minY) * i / 21.0;
            foreach (var (x1, x2) in YatayScan(oda.Poligon, y))
            {
                double span = x2 - x1;
                if (span > bestSpan) { bestSpan = span; best = new Point2D((x1 + x2) / 2, y); }
            }
        }
        return best;
    }

    private static Point2D CentroidShoelace(List<Point2D> poly)
    {
        double cx = 0, cy = 0, area = 0;
        for (int i = 0; i < poly.Count; i++)
        {
            var    a     = poly[i];
            var    b     = poly[(i + 1) % poly.Count];
            double cross = a.X * b.Y - b.X * a.Y;
            area += cross;
            cx   += (a.X + b.X) * cross;
            cy   += (a.Y + b.Y) * cross;
        }
        area /= 2;
        if (Math.Abs(area) < 1e-10) return poly[0];
        return new Point2D(cx / (6 * area), cy / (6 * area));
    }

    // ─── Tarama yardımcıları ─────────────────────────────────────────────

    /// y=sabit yatay çizginin poligonla kesişim X aralıkları (çift-sayı kuralı).
    private static List<(double X1, double X2)> YatayScan(List<Point2D> poly, double y)
    {
        var xs = new List<double>();
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Count];
            if ((a.Y <= y && b.Y > y) || (b.Y <= y && a.Y > y))
            {
                double t = (y - a.Y) / (b.Y - a.Y);
                xs.Add(a.X + t * (b.X - a.X));
            }
        }
        xs.Sort();
        var spans = new List<(double, double)>();
        for (int i = 0; i + 1 < xs.Count; i += 2)
            spans.Add((xs[i], xs[i + 1]));
        return spans;
    }

    private static (double MinX, double MinY, double MaxX, double MaxY) Bounds(BasitProjeOda oda)
    {
        if (oda.Poligon.Count == 0) return (0, 0, 0, 0);
        return (
            oda.Poligon.Min(p => p.X),
            oda.Poligon.Min(p => p.Y),
            oda.Poligon.Max(p => p.X),
            oda.Poligon.Max(p => p.Y));
    }
}
