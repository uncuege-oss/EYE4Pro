using Kst.Acoustics.App.ViewModels;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Importing;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.Tests;

/// <summary>
/// Yüzeye snap offset'i artık proje birimine göre çevriliyor; hedef her zaman ~10cm
/// (0.1m) fiziksel mesafedir. Eskiden sabit 0.1 "proje birimi" kullanılıyordu — mm
/// projesinde bu 0.1mm'ye, feet projesinde ~3cm'ye denk gelirdi (maincodex M01 P2).
/// </summary>
public static class SnapOffsetUnitAwarenessTests
{
    public static void SnapOffsetIsPhysicallyConsistentAcrossUnits()
    {
        var meters = BuildAndSnap(SurfaceImportUnit.Meters, 1.0);
        var millimeters = BuildAndSnap(SurfaceImportUnit.Millimeters, 1000.0);

        // Her iki projede de gerçek dünya karşılığı ~0.1m (metre projesinde 0.1 birim,
        // mm projesinde 100 birim) olmalı.
        AssertClose(0.1, meters, 1e-6, "Metre projesinde snap offset");
        AssertClose(100.0, millimeters, 1e-3, "Milimetre projesinde snap offset (0.1m karşılığı)");
    }

    /// <returns>Snap edilen kaynağın merkezden uzaklığı (proje birimi cinsinden).</returns>
    private static double BuildAndSnap(SurfaceImportUnit unit, double sideLength)
    {
        var surface = new ProjectSurface(1, "Zemin", "ZEMIN",
        [
            new Point3D(0, 0, 0),
            new Point3D(sideLength, 0, 0),
            new Point3D(sideLength, sideLength, 0),
            new Point3D(0, sideLength, 0)
        ]);

        var project = new AcousticsProject(
            "SnapOffsetTest", null, DateTime.UtcNow,
            SurfaceImportFormat.Dxf, unit,
            [surface]);

        var vm = new ProjectWindowViewModel(project);
        vm.HighlightedSurfaceId = 1;
        vm.SnapSourceToSelectedSurface();

        if (vm.SourcePoints.Count == 0)
            throw new InvalidOperationException("Snap sonrası bir kaynak eklenmeliydi.");

        var p = vm.SourcePoints[0].Position;
        var centroid = new Point3D(sideLength / 2, sideLength / 2, 0);
        var dx = p.X - centroid.X;
        var dy = p.Y - centroid.Y;
        var dz = p.Z - centroid.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private static void AssertClose(double expected, double actual, double tolerance, string label)
    {
        if (Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException($"{label}: beklenen {expected:F6}, gerçek {actual:F6}, tolerans {tolerance:F6}.");
    }
}
