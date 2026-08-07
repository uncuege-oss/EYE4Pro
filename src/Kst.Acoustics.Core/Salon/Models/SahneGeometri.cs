using System;
using System.Collections.Generic;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Salon.Models;

/// Sahne biçimi.
public enum SahneTipi { Dikdortgen, Dairesel, Oval }

/// Sahnenin salona göre konumu (varsayılan yerleşim + metadata).
public enum SahneKonum { SalonIci, SalonOnu }

/// Parametrik sahne tanımı. Salon başına tek sahne (<see cref="SalonGeometri.Sahne"/>).
/// Yükseltilmiş platform olarak 3D'ye katılır (üst yüzey + çevre etek).
public class SahneGeometri : ISahneGeometri
{
    public SahneTipi Tip { get; set; } = SahneTipi.Dikdortgen;
    public SahneKonum Konum { get; set; } = SahneKonum.SalonOnu;

    /// Genişlik (X, m). Dairesel için çap.
    public double GenislikM { get; set; } = 12.0;
    /// Derinlik (Y, m). Dairesel için yok sayılır (çap = genişlik).
    public double DerinlikM { get; set; } = 8.0;
    /// Platform yüksekliği (zeminden, m).
    public double YukseklikM { get; set; } = 1.0;
    /// Taban kotu (m).
    public double TabanKotuM { get; set; } = 0.0;

    /// Sahne merkezinin dünya koordinatı (m).
    public double MerkezX { get; set; }
    public double MerkezY { get; set; }

    /// Sahnenin taban (footprint) poligonu. Eğriler (dairesel/oval) çokgene fasetlenir.
    public List<Point2D> TabanPoligonu(int daireKenar = 48)
    {
        var pts = new List<Point2D>();
        double cx = MerkezX, cy = MerkezY;

        switch (Tip)
        {
            case SahneTipi.Dikdortgen:
            {
                double w = GenislikM / 2.0, d = DerinlikM / 2.0;
                pts.Add(new Point2D(cx - w, cy - d));
                pts.Add(new Point2D(cx + w, cy - d));
                pts.Add(new Point2D(cx + w, cy + d));
                pts.Add(new Point2D(cx - w, cy + d));
                break;
            }
            case SahneTipi.Dairesel:
            {
                double r = GenislikM / 2.0;
                for (int i = 0; i < daireKenar; i++)
                {
                    double a = 2.0 * Math.PI * i / daireKenar;
                    pts.Add(new Point2D(cx + r * Math.Cos(a), cy + r * Math.Sin(a)));
                }
                break;
            }
            case SahneTipi.Oval:
            {
                double rx = GenislikM / 2.0, ry = DerinlikM / 2.0;
                for (int i = 0; i < daireKenar; i++)
                {
                    double a = 2.0 * Math.PI * i / daireKenar;
                    pts.Add(new Point2D(cx + rx * Math.Cos(a), cy + ry * Math.Sin(a)));
                }
                break;
            }
        }
        return pts;
    }
}
