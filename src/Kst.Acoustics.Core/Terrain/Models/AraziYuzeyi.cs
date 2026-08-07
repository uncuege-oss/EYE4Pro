using System;
using System.Collections.Generic;

namespace Kst.Acoustics.Core.Terrain.Models;

/// TIN düğümü (üçgenleme köşesi). Koordinatlar metre cinsindendir.
public readonly record struct AraziDugum(double X, double Y, double Z);

/// TIN üçgeni — `AraziYuzeyi.Dugumler` listesine indeksler.
public readonly record struct AraziUcgen(int A, int B, int C);

/// Arazi sayısal yüzeyi (DTM) — Delaunay TIN. Düğümler + üçgenler tutar ve
/// herhangi bir (x,y) için kot interpolasyonu (`KotSorgula`) sağlar.
/// Üretici: <see cref="Services.AraziYuzeyServisi"/>.
public class AraziYuzeyi
{
    public List<AraziDugum> Dugumler { get; set; } = new();
    public List<AraziUcgen> Ucgenler { get; set; } = new();

    /// (x,y) noktasını içeren üçgeni bulup baricentric interpolasyonla kotu döndürür.
    /// Nokta yüzey dışındaysa (hiçbir üçgende değilse) null döner.
    public double? KotSorgula(double x, double y)
    {
        const double eps = 1e-9;

        foreach (var t in Ucgenler)
        {
            var a = Dugumler[t.A];
            var b = Dugumler[t.B];
            var c = Dugumler[t.C];

            double denom = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
            if (Math.Abs(denom) < eps) continue; // dejenere üçgen

            double wa = ((b.Y - c.Y) * (x - c.X) + (c.X - b.X) * (y - c.Y)) / denom;
            double wb = ((c.Y - a.Y) * (x - c.X) + (a.X - c.X) * (y - c.Y)) / denom;
            double wc = 1.0 - wa - wb;

            if (wa >= -eps && wb >= -eps && wc >= -eps)
                return wa * a.Z + wb * b.Z + wc * c.Z;
        }

        return null;
    }
}
