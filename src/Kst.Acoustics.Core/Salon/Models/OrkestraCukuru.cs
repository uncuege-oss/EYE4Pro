using System.Collections.Generic;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Salon.Models;

/// Orkestra çukuru: sahne önünde, salon zemininden aşağı inen dikdörtgen çukur.
/// Zeminde açıklık açar; çukur tabanı + çevre duvarları akustik hacme katılır
/// (kategori "Orkestra"). Salon başına tek çukur (<see cref="SalonGeometri.OrkestraCukuru"/>).
public class OrkestraCukuru : IOrkestraCukuru
{
    /// Genişlik (X, m).
    public double GenislikM { get; set; } = 12.0;
    /// Plan derinliği (Y, m).
    public double DerinlikM { get; set; } = 3.0;
    /// Çukur derinliği (zeminden aşağı, pozitif m).
    public double CukurDerinligiM { get; set; } = 2.5;

    public double MerkezX { get; set; }
    public double MerkezY { get; set; }

    /// Footprint dikdörtgeni (4 köşe).
    public List<Point2D> Footprint()
    {
        double xL = MerkezX - GenislikM / 2, xR = MerkezX + GenislikM / 2;
        double yA = MerkezY - DerinlikM / 2, yB = MerkezY + DerinlikM / 2;
        return new List<Point2D> { new(xL, yA), new(xR, yA), new(xR, yB), new(xL, yB) };
    }

    public override string ToString()
        => $"Orkestra çukuru ({GenislikM:F0}×{DerinlikM:F0} m, −{CukurDerinligiM:F1} m)";
}
