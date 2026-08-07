using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.Core.Geometry;

public sealed class HedefYuzey
{
    public required ProjectSurface Surface { get; init; }
    public required Vector3D Normal { get; init; }
    public required Point3D Centroid { get; init; }
    public required double Area { get; init; }

    // Düzlem denklemi: n·X = D
    public double PlaneD => Normal.X * Centroid.X + Normal.Y * Centroid.Y + Normal.Z * Centroid.Z;
}

public sealed record PanelHedefSec(HedefYuzey? Tavan, HedefYuzey? SolDuvar, HedefYuzey? SagDuvar);

public static class PanelHedefDetector
{
    private const double HorizontalNzThreshold = 0.6;   // |nz| > 0.6 → yatay yüzey
    private const double SideNxThreshold = 0.2;          // |nx| > 0.2 → yan duvarda yatay bileşen var
    private const double FrontBackNyThreshold = 0.85;   // |ny| > 0.85 ve |nx| küçük → ön/arka duvar

    public static PanelHedefSec Bul(
        IReadOnlyList<ProjectSurface> surfaces,
        AcousticPanelKonfigurasyon konfig)
    {
        var hesaplananlar = surfaces
            .Select(HesaplaYuzeyOzeti)
            .Where(h => h is not null)
            .Cast<HedefYuzey>()
            .ToList();

        return new PanelHedefSec(
            SecHedef(hesaplananlar, konfig.TavanYuzeyId, TavanAdayi),
            SecHedef(hesaplananlar, konfig.SolDuvarYuzeyId, SolDuvarAdayi(surfaces, konfig.SahneOnuYuzeyId)),
            SecHedef(hesaplananlar, konfig.SagDuvarYuzeyId, SagDuvarAdayi(surfaces, konfig.SahneOnuYuzeyId)));
    }

    private static HedefYuzey? SecHedef(
        IReadOnlyList<HedefYuzey> tumYuzeyler,
        string? manuelId,
        Func<HedefYuzey, bool> filtre)
    {
        if (!string.IsNullOrWhiteSpace(manuelId))
        {
            var manuel = tumYuzeyler.FirstOrDefault(h =>
                h.Surface.Name.Equals(manuelId, StringComparison.OrdinalIgnoreCase) ||
                h.Surface.Id.ToString() == manuelId);
            if (manuel is not null) return manuel;
        }

        return tumYuzeyler
            .Where(filtre)
            .OrderByDescending(h => h.Area)
            .FirstOrDefault();
    }

    private static bool TavanAdayi(HedefYuzey h) =>
        h.Normal.Z > HorizontalNzThreshold;

    // Sol/Sağ duvar adayı — kullanıcı bakış açısına göre değil, X eksenine göre sınıflandırılır:
    // centroidi X<0 bölgesinde olan = SOL, X>0 = SAĞ. Ön/arka duvar (|ny| baskın) elenir.
    private static Func<HedefYuzey, bool> SolDuvarAdayi(
        IReadOnlyList<ProjectSurface> surfaces,
        string? sahneOnuId)
    {
        var (minX, maxX) = GetXRange(surfaces);
        var midX = 0.5 * (minX + maxX);
        return h => YanDuvarMi(h) && h.Centroid.X < midX;
    }

    private static Func<HedefYuzey, bool> SagDuvarAdayi(
        IReadOnlyList<ProjectSurface> surfaces,
        string? sahneOnuId)
    {
        var (minX, maxX) = GetXRange(surfaces);
        var midX = 0.5 * (minX + maxX);
        return h => YanDuvarMi(h) && h.Centroid.X >= midX;
    }

    private static bool YanDuvarMi(HedefYuzey h)
    {
        if (Math.Abs(h.Normal.Z) > HorizontalNzThreshold) return false; // yatay yüzey
        if (Math.Abs(h.Normal.X) < SideNxThreshold) return false;        // ön/arka duvar (X bileşeni yok)
        if (Math.Abs(h.Normal.Y) > FrontBackNyThreshold &&
            Math.Abs(h.Normal.X) < FrontBackNyThreshold * 0.6) return false;
        return true;
    }

    private static (double Min, double Max) GetXRange(IReadOnlyList<ProjectSurface> surfaces)
    {
        var pts = surfaces.SelectMany(s => s.Vertices).ToList();
        if (pts.Count == 0) return (0, 0);
        return (pts.Min(p => p.X), pts.Max(p => p.X));
    }

    private static HedefYuzey? HesaplaYuzeyOzeti(ProjectSurface surface)
    {
        var verts = surface.Vertices;
        if (verts.Count < 3) return null;

        double cx = verts.Average(p => p.X);
        double cy = verts.Average(p => p.Y);
        double cz = verts.Average(p => p.Z);

        double nx = 0, ny = 0, nz = 0;
        for (int i = 1; i < verts.Count - 1; i++)
        {
            var ax = verts[i].X - verts[0].X;
            var ay = verts[i].Y - verts[0].Y;
            var az = verts[i].Z - verts[0].Z;
            var bx = verts[i + 1].X - verts[0].X;
            var by = verts[i + 1].Y - verts[0].Y;
            var bz = verts[i + 1].Z - verts[0].Z;
            nx += ay * bz - az * by;
            ny += az * bx - ax * bz;
            nz += ax * by - ay * bx;
        }

        var len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
        if (len < 1e-9) return null;

        double area = len * 0.5;
        nx /= len; ny /= len; nz /= len;

        return new HedefYuzey
        {
            Surface = surface,
            Normal = new Vector3D(nx, ny, nz),
            Centroid = new Point3D(cx, cy, cz),
            Area = area
        };
    }
}
