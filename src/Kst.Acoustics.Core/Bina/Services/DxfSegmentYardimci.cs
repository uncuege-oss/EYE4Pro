using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Bina.Services;

/// Ham DXF arka plan segmentleri üzerinde ortak işlemler.
/// M12, M15 ve M50 tarafından paylaşılır; yalnızca Point2D kullanır.
public static class DxfSegmentYardimci
{
    /// Ham DXF segmentlerini sadeleştirir: çok kısa (varsayılan 5 cm altı) mikro parçaları eler,
    /// kalan sayı tavanını aşarsa en uzunları tutar. Büyük DXF'leri (20-40 MB / ~1M segment)
    /// görüntü/snap için ~on binlere indirir.
    public static IEnumerable<(Point2D A, Point2D B)> Sadelestir(
        IReadOnlyList<(Point2D A, Point2D B)> segmentler,
        double minUzunlukM = 0.05, int maxSegment = 200_000)
    {
        double min2 = minUzunlukM * minUzunlukM;
        var filtreli = new List<(Point2D A, Point2D B, double L2)>(segmentler.Count);
        foreach (var (a, b) in segmentler)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            double l2 = dx * dx + dy * dy;
            if (l2 >= min2)
                filtreli.Add((a, b, l2));
        }

        IEnumerable<(Point2D A, Point2D B, double L2)> kaynak = filtreli;
        if (filtreli.Count > maxSegment)
            kaynak = filtreli.OrderByDescending(s => s.L2).Take(maxSegment);

        foreach (var s in kaynak)
            yield return (s.A, s.B);
    }
}
