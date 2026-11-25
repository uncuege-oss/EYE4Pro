using System;
using System.Collections.Generic;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Salon.Models;

namespace Kst.Acoustics.Core.Salon.Services;

/// Kademeli seyirci alanı geometrisi: footprint, 2D kademe sınırları ve 3D yüzeyler
/// (basamak döşemeleri "Seyirci" + düşey korniş yüzeyleri "Korniş").
public static class SalonSeyirciServisi
{
    /// Footprint dikdörtgeni (4 köşe).
    public static List<Point2D> Footprint(SeyirciSekciyonu s)
    {
        double xL = s.MerkezX - s.GenislikM / 2, xR = s.MerkezX + s.GenislikM / 2;
        double yA = s.MerkezY - s.DerinlikM / 2, yB = s.MerkezY + s.DerinlikM / 2;
        return new List<Point2D>
        {
            new(xL, yA), new(xR, yA), new(xR, yB), new(xL, yB)
        };
    }

    /// Kademe sınır çizgileri (2D gösterim için) — i=1..N-1.
    public static List<(Point2D A, Point2D B)> KademeSinirlari(SeyirciSekciyonu s)
    {
        var sonuc = new List<(Point2D, Point2D)>();
        int n = Math.Max(1, s.KademeSayisi);
        double xL = s.MerkezX - s.GenislikM / 2, xR = s.MerkezX + s.GenislikM / 2;
        for (int i = 1; i < n; i++)
        {
            double y = KademeY(s, i, n);
            sonuc.Add((new Point2D(xL, y), new Point2D(xR, y)));
        }
        return sonuc;
    }

    /// 3D yüzeyler: N basamak döşemesi ("Seyirci") + (N-1) düşey korniş ("Korniş").
    /// <paramref name="kabuk"/> verilirse basamaklar ve kornişler salon dış hattının
    /// içine kırpılır — dışarı taşan yerleşim kabuk-dışı hayalet yüzey üretmez.
    public static List<ProjectSurface> Yuzeyler(
        SeyirciSekciyonu s, ref int id, List<List<Point2D>>? kabuk = null)
    {
        var surfaces = new List<ProjectSurface>();
        int n = Math.Max(1, s.KademeSayisi);
        double xL = s.MerkezX - s.GenislikM / 2, xR = s.MerkezX + s.GenislikM / 2;
        double taban = s.TabanKotuM, rise = s.KademeYuksekligiM;

        for (int i = 0; i < n; i++)
        {
            double yA = KademeY(s, i, n);
            double yB = KademeY(s, i + 1, n);
            double z = taban + i * rise;
            var basamak = new List<Point2D> { new(xL, yA), new(xR, yA), new(xR, yB), new(xL, yB) };
            foreach (var p in SalonPoligonServisi.KabugaKirp(kabuk, basamak))
                surfaces.Add(new ProjectSurface(id++, $"{s.Ad} Kademe {i + 1}", "Seyirci",
                    p.Select(q => new Point3D(q.X, q.Y, z)).ToList()));
        }

        if (rise > 1e-6)
            for (int i = 1; i < n; i++)
            {
                double yb = KademeY(s, i, n);
                double zLow = taban + (i - 1) * rise, zHigh = taban + i * rise;
                foreach (var (px1, px2) in SalonPoligonServisi.YatayParcalar(kabuk, yb, xL, xR))
                    surfaces.Add(new ProjectSurface(id++, $"{s.Ad} Korniş {i}", "Korniş", new List<Point3D>
                    {
                        new(px1, yb, zLow), new(px2, yb, zLow), new(px2, yb, zHigh), new(px1, yb, zHigh)
                    }));
            }

        // Close exposed sides/back and raised undersides. Treads and risers retain
        // their categories; closure surfaces are separate for material assignment.
        var treads = surfaces.Where(p => p.Category == "Seyirci").ToList();
        foreach (var tread in treads)
        {
            var p = tread.Vertices.Select(v => new Point2D(v.X, v.Y)).ToList();
            double z = tread.Vertices[0].Z;
            double sign = ClipperAdaptor.IsaretliAlan(p) > 0 ? 1 : -1;
            if (taban > 1e-6)
                surfaces.Add(new ProjectSurface(id++, s.Ad + " Alt", "Seyirci-Alt", p.Select(v=>new Point3D(v.X,v.Y,taban)).ToList()));
            if (z <= taban + 1e-6) continue;
            for (int i=0;i<p.Count;i++)
            {
                var a=p[i];var b=p[(i+1)%p.Count];double dx=b.X-a.X,dy=b.Y-a.Y,len=Math.Sqrt(dx*dx+dy*dy);
                if(len<1e-9)continue;
                var outside=new Point2D((a.X+b.X)/2+sign*dy/len*.0002,(a.Y+b.Y)/2-sign*dx/len*.0002);
                if(treads.Any(other=>!ReferenceEquals(other,tread)&&SalonTasarimServisi.Icinde(outside,other.Vertices.Select(v=>new Point2D(v.X,v.Y)).ToList())))continue;
                surfaces.Add(new ProjectSurface(id++,s.Ad+" Yan","Seyirci-Yan",new List<Point3D>{new(a.X,a.Y,taban),new(b.X,b.Y,taban),new(b.X,b.Y,z),new(a.X,a.Y,z)}));
            }
        }
        return surfaces;
    }

    /// Koltuk başına brüt alan (m²) — sıra aralığı ~0,90 m × koltuk genişliği ~0,55 m
    /// (pratik salon planlaması ortalaması; koridor payı hariç brüt).
    public const double KoltukBasinaM2 = 0.5;

    /// Seyirci alanının tahmini koltuk kapasitesi (brüt alan / koltuk başına alan).
    public static int KoltukKapasitesi(SeyirciSekciyonu s, double m2PerKoltuk = KoltukBasinaM2)
    {
        if (m2PerKoltuk <= 0) return 0;
        return (int)Math.Floor(s.GenislikM * s.DerinlikM / m2PerKoltuk);
    }

    /// i. kademe sınırının Y koordinatı (0 = ön/alçak, N = arka/yüksek).
    private static double KademeY(SeyirciSekciyonu s, int i, int n)
    {
        double sd = s.DerinlikM / n;
        double yFront = s.TersYon ? s.MerkezY + s.DerinlikM / 2 : s.MerkezY - s.DerinlikM / 2;
        double sgn = s.TersYon ? -1.0 : 1.0;
        return yFront + sgn * i * sd;
    }
}
