using System.Collections.Generic;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Terrain.Models;

/// Kesitte bir bina bloğu (yatay X aralığı + düşey kotlar).
public class KesitBlok
{
    public string BlokId { get; set; } = "";
    public int KademeNo { get; set; }
    public double XMin { get; set; }
    public double XMax { get; set; }
    public double TabanKotM { get; set; }   // bodrum taban
    public double CatiKotM { get; set; }
    public double SifirKotM { get; set; }    // ±0.00
}

/// M61 F5 — bina-arazi kotlu kesit çizim modeli. Bir kesit hattı boyunca arazi profili
/// (X = yatay mesafe, Y = kot) + bina blokları (kesit dikdörtgenleri) tutar. Çizim App'te.
public class KesitModeli
{
    /// Arazi profili: her nokta (X = yatay mesafe m, Y = kot m).
    public List<Point2D> ZeminProfili { get; set; } = new();
    public List<KesitBlok> Bloklar { get; set; } = new();

    public double MinX { get; set; }
    public double MaxX { get; set; }
    public double MinKot { get; set; }
    public double MaxKot { get; set; }
}
