using System;
using System.Collections.Generic;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Electric.CevreAydinlatma;

/// <summary>
/// Güzergâh boyunca otomatik direk yerleşimi. İki mod: açık polyline (yol kenarı) boyunca eşit
/// aralıklı; kapalı poligon (yeşil alan) çevresinde kenara ofsetli eşit aralıklı. M15 Faz 4.
/// </summary>
public static class DirekYerlesimServisi
{
    /// <summary>Açık güzergâh (polyline) boyunca <paramref name="aralikM"/> eşit aralıklı direk konumları.</summary>
    public static List<Point2D> GuzergahaYerlestir(IReadOnlyList<Point2D> rota, double aralikM)
    {
        var sonuc = new List<Point2D>();
        if (rota.Count == 0 || !GecerliYerlesim(rota, aralikM, kapali: false))
            return sonuc;

        sonuc.Add(rota[0]);
        double kalan = aralikM;
        for (int i = 0; i < rota.Count - 1; i++)
            SegmentBoyunca(rota[i], rota[i + 1], aralikM, 0, 0, ref kalan, sonuc);
        return sonuc;
    }

    /// <summary>
    /// Kapalı poligon (ör. yeşil alan) çevresinde eşit aralıklı direk konumları; her nokta o kenarın
    /// iç(+)/dış(−) normaline <paramref name="kenarOfsetM"/> kadar kaydırılır. Dikiş noktasındaki
    /// tekrar ayıklanır.
    /// </summary>
    public static List<Point2D> PoligonCevresineYerlestir(IReadOnlyList<Point2D> poligon, double aralikM, double kenarOfsetM)
    {
        var sonuc = new List<Point2D>();
        if (poligon.Count < 3 || !double.IsFinite(kenarOfsetM) || !GecerliYerlesim(poligon, aralikM, kapali: true))
            return sonuc;

        bool ccw = ImzaliAlan(poligon) > 0;
        int n = poligon.Count;

        var (n0x, n0y) = KenarNormali(poligon[0], poligon[1 % n], ccw);
        sonuc.Add(new Point2D(poligon[0].X + n0x * kenarOfsetM, poligon[0].Y + n0y * kenarOfsetM));

        double kalan = aralikM;
        for (int i = 0; i < n; i++)
        {
            var a = poligon[i];
            var b = poligon[(i + 1) % n];
            var (nx, ny) = KenarNormali(a, b, ccw);
            SegmentBoyunca(a, b, aralikM, nx * kenarOfsetM, ny * kenarOfsetM, ref kalan, sonuc);
        }

        // Dikiş: son nokta ilkine çok yakınsa (kapanış tekrarı) at.
        if (sonuc.Count > 1)
        {
            var ilk = sonuc[0];
            var son = sonuc[^1];
            double d = Math.Sqrt((ilk.X - son.X) * (ilk.X - son.X) + (ilk.Y - son.Y) * (ilk.Y - son.Y));
            if (d < aralikM * 0.5)
                sonuc.RemoveAt(sonuc.Count - 1);
        }
        return sonuc;
    }

    /// <summary>Konumlardan, verilen güzergâh/tanım atamalı yerleşmiş direkler üretir.</summary>
    public static List<AydinlatmaDiregi> DireklerOlustur(
        IEnumerable<Point2D> noktalar, int guzergahId, int? direkTanimId, int? armaturTanimId)
    {
        var direkler = new List<AydinlatmaDiregi>();
        int id = 1;
        foreach (var p in noktalar)
            direkler.Add(new AydinlatmaDiregi
            {
                Id = id++,
                X = p.X,
                Y = p.Y,
                GuzergahId = guzergahId,
                DirekTanimId = direkTanimId,
                ArmaturTanimId = armaturTanimId
            });
        return direkler;
    }

    // a→b segmenti boyunca, (ofsetX,ofsetY) kayma ile eşit aralıklı nokta ekler. kalan: bir
    // sonraki noktaya kalan mesafe (segmentler arası taşınır).
    private static void SegmentBoyunca(Point2D a, Point2D b, double aralik,
        double ofsetX, double ofsetY, ref double kalan, List<Point2D> sonuc)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double L = Math.Sqrt(dx * dx + dy * dy);
        if (L < 1e-9)
            return;

        double ux = dx / L, uy = dy / L;
        double pos = 0;
        while (kalan <= L - pos + 1e-9)
        {
            pos += kalan;
            sonuc.Add(new Point2D(a.X + ux * pos + ofsetX, a.Y + uy * pos + ofsetY));
            kalan = aralik;
        }
        kalan -= (L - pos);
    }

    private static double ImzaliAlan(IReadOnlyList<Point2D> poligon)
    {
        double alan = 0;
        for (int i = 0; i < poligon.Count; i++)
        {
            var a = poligon[i];
            var b = poligon[(i + 1) % poligon.Count];
            alan += a.X * b.Y - b.X * a.Y;
        }
        return alan;
    }

    private static bool GecerliYerlesim(IReadOnlyList<Point2D> points, double spacing, bool kapali)
    {
        if (!double.IsFinite(spacing) || spacing <= 0) return false;
        double length = 0;
        for (int i = 0; i < points.Count; i++)
        {
            if (!double.IsFinite(points[i].X) || !double.IsFinite(points[i].Y)) return false;
            if (!kapali && i == points.Count - 1) break;
            var next = points[(i + 1) % points.Count];
            double dx = next.X - points[i].X, dy = next.Y - points[i].Y;
            length += Math.Sqrt(dx * dx + dy * dy);
        }
        return double.IsFinite(length) && length / spacing <= 100_000;
    }

    // CCW poligonda sol normal (-uy, ux) içeri bakar.
    private static (double nx, double ny) KenarNormali(Point2D a, Point2D b, bool ccw)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double L = Math.Sqrt(dx * dx + dy * dy);
        if (L < 1e-9)
            return (0, 0);
        double ux = dx / L, uy = dy / L;
        return ccw ? (-uy, ux) : (uy, -ux);
    }
}
