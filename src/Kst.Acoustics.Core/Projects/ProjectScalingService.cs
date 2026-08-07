using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Importing;

namespace Kst.Acoustics.Core.Projects;

public static class ProjectScalingService
{
    // Oda akustiği iş akışında bundan daha uç tek-adımlı fiziksel ölçekler anlamlı
    // değildir ve factor³ hacim hesabı ile geometrik toleransları sayısal olarak
    // güvensiz hale getirir. Birim dönüşüm katsayılarının tamamı bu aralıktadır.
    public const double MinimumSupportedScaleFactor = 1e-12;
    public const double MaximumSupportedScaleFactor = 1e12;

    public static bool IsSupportedScaleFactor(double factor) =>
        double.IsFinite(factor) &&
        factor >= MinimumSupportedScaleFactor &&
        factor <= MaximumSupportedScaleFactor;

    public static IReadOnlyList<ProjectSurface> ScaleSurfaces(IReadOnlyList<ProjectSurface> surfaces, double factor)
    {
        ArgumentNullException.ThrowIfNull(surfaces);

        ValidateScaleFactor(factor);

        return surfaces
            .Select(surface => surface with
            {
                Vertices = surface.Vertices.Select(point => ScalePoint(point, factor)).ToArray()
            })
            .ToArray();
    }

    public static IReadOnlyList<ProjectSurface> ConvertUnits(
        IReadOnlyList<ProjectSurface> surfaces,
        SurfaceImportUnit sourceUnit,
        SurfaceImportUnit targetUnit)
    {
        ArgumentNullException.ThrowIfNull(surfaces);

        var factor = GetConversionFactor(sourceUnit, targetUnit);
        return ScaleSurfaces(surfaces, factor);
    }

    public static double GetConversionFactor(SurfaceImportUnit sourceUnit, SurfaceImportUnit targetUnit)
    {
        if (sourceUnit == targetUnit)
        {
            return 1d;
        }

        return GetMetersPerUnit(sourceUnit) / GetMetersPerUnit(targetUnit);
    }

    /// <summary>
    /// Proje birimi değişirken <paramref name="volumeM3"/> fiziksel SI hacmidir ve
    /// değişmez. Geometri/nokta koordinatları dönüştürülür; m³ değeri birim³ değildir.
    /// </summary>
    public static double ConvertRoomVolumeM3(
        double volumeM3,
        SurfaceImportUnit sourceUnit,
        SurfaceImportUnit targetUnit)
    {
        _ = GetMetersPerUnit(sourceUnit);
        _ = GetMetersPerUnit(targetUnit);
        return volumeM3;
    }

    /// <summary>Kaynak/alıcı konumlarını yüzeylerle aynı katsayıyla ölçekler; yön açıları değişmez.</summary>
    public static IReadOnlyList<AcousticPoint> ScalePoints(IReadOnlyList<AcousticPoint> points, double factor)
    {
        ArgumentNullException.ThrowIfNull(points);

        ValidateScaleFactor(factor);

        return points
            .Select(p => p with { Position = ScalePoint(p.Position, factor) })
            .ToArray();
    }

    /// <summary>
    /// Odanın kendisi fiziksel olarak büyütülüp/küçültüldüğünde SI hacmi uzunluk
    /// katsayısının küpüyle ölçeklenir. Sırf proje birimi değişiminde kullanılmaz.
    /// </summary>
    public static double ScalePhysicalVolumeM3(double volumeM3, double lengthFactor)
    {
        ValidateScaleFactor(lengthFactor);
        if (!double.IsFinite(volumeM3) || volumeM3 < 0d)
            throw new ArgumentOutOfRangeException(nameof(volumeM3), "Hacim sonlu ve negatif olmayan bir sayı olmalıdır.");

        var scaled = ScaleFiniteValue(volumeM3, lengthFactor, "hacim");
        scaled = ScaleFiniteValue(scaled, lengthFactor, "hacim");
        return ScaleFiniteValue(scaled, lengthFactor, "hacim");
    }

    /// <summary>Geriye uyumlu ad; yalnız fiziksel ölçekleme için kullanılır.</summary>
    public static double ScaleVolume(double volume, double factor) =>
        ScalePhysicalVolumeM3(volume, factor);

    private static Point3D ScalePoint(Point3D point, double factor)
    {
        return new Point3D(
            ScaleFiniteValue(point.X, factor, "X koordinatı"),
            ScaleFiniteValue(point.Y, factor, "Y koordinatı"),
            ScaleFiniteValue(point.Z, factor, "Z koordinatı"));
    }

    private static void ValidateScaleFactor(double factor)
    {
        if (!IsSupportedScaleFactor(factor))
        {
            throw new ArgumentOutOfRangeException(
                nameof(factor),
                $"Ölçek katsayısı {MinimumSupportedScaleFactor:E0} ile {MaximumSupportedScaleFactor:E0} arasında olmalıdır.");
        }
    }

    private static double ScaleFiniteValue(double value, double factor, string label)
    {
        if (!double.IsFinite(value))
            throw new InvalidDataException($"Sonlu olmayan {label} ölçeklenemez.");

        var scaled = value * factor;
        if (!double.IsFinite(scaled) || (value != 0d && scaled == 0d))
            throw new OverflowException($"Ölçekleme geçersiz bir {label} üretti.");

        return scaled;
    }

    public static double GetMetersPerUnit(SurfaceImportUnit unit)
    {
        return unit switch
        {
            SurfaceImportUnit.Meters => 1d,
            SurfaceImportUnit.Centimeters => 0.01d,
            SurfaceImportUnit.Millimeters => 0.001d,
            SurfaceImportUnit.Inches => 0.0254d,
            SurfaceImportUnit.Feet => 0.3048d,
            _ => 1d
        };
    }
}
