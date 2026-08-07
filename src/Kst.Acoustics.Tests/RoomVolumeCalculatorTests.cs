using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Importing;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.Tests;

/// <summary>
/// DXF hacim hesabı artık bounding box değil; kabuk kapalıysa (watertight) gerçek imzalı
/// hacim döner, değilse açıkça "bounding-box tahmini" olarak işaretlenir. L-plan gibi
/// dışbükey olmayan geometrilerde bounding box gerçek hacimden büyük çıkar — bu testler
/// bu ayrımı doğrular (maincodex M01 incelemesi P0/P1).
///
/// Yüzey köşe sırası tüm test geometrilerinde dışa dönük normal verecek şekilde
/// (sağ-el kuralı, taban CCW üstten bakışta = tavan yönü, duvarlar taban sınırının
/// CCW yönünde) tutarlı kurulmuştur — imzalı hacim toplamı yönlendirme tutarsızsa
/// yanlış (küçük/iptal olmuş) çıkabileceğinden bu tutarlılık testin doğruluğu için zorunludur.
/// </summary>
public static class RoomVolumeCalculatorTests
{
    public static void ClosedBoxUsesRealVolumeNotBoundingBox()
    {
        const double lx = 4, ly = 4, lz = 3;
        var estimate = RoomVolumeCalculator.Estimate(BoxSurfaces(lx, ly, lz), SurfaceImportUnit.Meters);

        AssertClose(lx * ly * lz, estimate.VolumeM3, 1e-6, "Kapalı kutu hacmi");
        AssertTrue(estimate.Origin == RoomVolumeOrigin.ClosedMesh,
            $"Kapalı kutu 'ClosedMesh' olarak işaretlenmeli, gerçek: {estimate.Origin}.");
    }

    public static void LShapedClosedShellVolumeIsLessThanBoundingBox()
    {
        // 6x6 taban, 3x3'lük köşe (x:3-6, y:3-6) çıkarılmış L-plan, yükseklik 3.
        // Gerçek hacim = (36-9)*3 = 81 m³; bounding box = 6*6*3 = 108 m³.
        var surfaces = LShapeSurfaces();
        var estimate = RoomVolumeCalculator.Estimate(surfaces, SurfaceImportUnit.Meters);

        AssertTrue(estimate.Origin == RoomVolumeOrigin.ClosedMesh,
            $"L-plan kapalı kabuk 'ClosedMesh' olmalı, gerçek: {estimate.Origin}.");
        AssertClose(81.0, estimate.VolumeM3, 1e-6, "L-plan gerçek hacmi");

        const double bboxVolume = 6.0 * 6.0 * 3.0;
        AssertTrue(estimate.VolumeM3 < bboxVolume * 0.9,
            $"L-plan hacmi ({estimate.VolumeM3}) bounding box'a ({bboxVolume}) çok yakın — ayrım kaybolmuş.");
    }

    public static void OpenShellFallsBackToBoundingBoxAndIsMarkedAsEstimate()
    {
        const double lx = 4, ly = 4, lz = 3;
        // Tavanı çıkar — kabuk artık kapalı değil.
        var surfaces = BoxSurfaces(lx, ly, lz).Where(s => s.Name != "Tavan").ToArray();

        var estimate = RoomVolumeCalculator.Estimate(surfaces, SurfaceImportUnit.Meters);

        AssertTrue(estimate.Origin == RoomVolumeOrigin.BoundingBoxEstimate,
            $"Açık kabuk 'BoundingBoxEstimate' olarak işaretlenmeli, gerçek: {estimate.Origin}.");
        AssertClose(lx * ly * lz, estimate.VolumeM3, 1e-6, "Açık kabuk bounding-box hacmi");
    }

    public static void UnitConversionScalesEstimatedVolumeByCubeOfFactor()
    {
        const double lx = 4, ly = 4, lz = 3;
        var meters = RoomVolumeCalculator.Estimate(BoxSurfaces(lx, ly, lz), SurfaceImportUnit.Meters);

        var mmSurfaces = BoxSurfaces(lx * 1000, ly * 1000, lz * 1000);
        var millimeters = RoomVolumeCalculator.Estimate(mmSurfaces, SurfaceImportUnit.Millimeters);

        AssertClose(meters.VolumeM3, millimeters.VolumeM3, 1e-6, "mm cinsinden aynı odanın m³ karşılığı");
    }

    public static void MixedSurfaceWindingIsOrientedBeforeVolumeCalculation()
    {
        var surfaces = BoxSurfaces(4, 4, 3);
        // İthal CAD kabuklarında birkaç yüzey ters winding ile gelebilir. Kapalılık
        // bozulmaz; hacim hesabı komşuluk kısıtlarından yönleri tutarlı hale getirmeli.
        surfaces[1] = surfaces[1] with { Vertices = surfaces[1].Vertices.Reverse().ToArray() };
        surfaces[3] = surfaces[3] with { Vertices = surfaces[3].Vertices.Reverse().ToArray() };
        surfaces[5] = surfaces[5] with { Vertices = surfaces[5].Vertices.Reverse().ToArray() };

        var estimate = RoomVolumeCalculator.Estimate(surfaces, SurfaceImportUnit.Meters);
        AssertTrue(estimate.Origin == RoomVolumeOrigin.ClosedMesh,
            $"Karışık winding kapalı kabuk olarak kalmalı, gerçek: {estimate.Origin}.");
        AssertClose(48, estimate.VolumeM3, 1e-6, "Karışık winding kutu hacmi");
    }

    // Tüm yüzeyler dışa dönük normal verecek şekilde: taban CCW plan sırasının tersi
    // (üstten bakışta CW → -Z dışa dönük), tavan CCW sırayla (+Z dışa dönük), duvarlar
    // taban sınırının CCW kenarından (p_i, p_i+1, üst_i+1, üst_i) sırasıyla.
    private static List<ProjectSurface> BoxSurfaces(double lx, double ly, double lz)
    {
        Point3D[] planCcw = [new(0, 0, 0), new(lx, 0, 0), new(lx, ly, 0), new(0, ly, 0)];
        return ExtrudePlan(planCcw, lz, "ZEMIN", "TAVAN", "DUVAR");
    }

    private static List<ProjectSurface> LShapeSurfaces()
    {
        Point3D[] planCcw =
        [
            new(0, 0, 0), new(6, 0, 0), new(6, 3, 0), new(3, 3, 0), new(3, 6, 0), new(0, 6, 0)
        ];
        return ExtrudePlan(planCcw, 3, "ZEMIN", "TAVAN", "DUVAR");
    }

    private static List<ProjectSurface> ExtrudePlan(
        IReadOnlyList<Point3D> planCcw, double height, string floorCategory, string ceilingCategory, string wallCategory)
    {
        var top = planCcw.Select(p => new Point3D(p.X, p.Y, p.Z + height)).ToArray();
        var floor = planCcw.Reverse().ToArray();

        var surfaces = new List<ProjectSurface>
        {
            new(1, "Zemin", floorCategory, floor),
            new(2, "Tavan", ceilingCategory, top)
        };

        var id = 3;
        for (var i = 0; i < planCcw.Count; i++)
        {
            var a = planCcw[i];
            var b = planCcw[(i + 1) % planCcw.Count];
            var aTop = new Point3D(a.X, a.Y, a.Z + height);
            var bTop = new Point3D(b.X, b.Y, b.Z + height);
            surfaces.Add(new ProjectSurface(id++, $"Duvar{i}", wallCategory, [a, b, bTop, aTop]));
        }

        return surfaces;
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void AssertClose(double expected, double actual, double tolerance, string label)
    {
        if (Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException($"{label}: beklenen {expected:F6}, gerçek {actual:F6}, tolerans {tolerance:F6}.");
    }
}
