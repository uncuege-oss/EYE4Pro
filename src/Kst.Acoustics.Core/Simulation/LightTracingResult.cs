using Kst.Acoustics.Core.Geometry;

namespace Kst.Acoustics.Core.Simulation;

public readonly record struct RaySegment(Point3D From, Point3D To, double Weight = 1.0);

public readonly record struct LightHitPoint(Point3D Position, double Weight, int SurfaceId);

public sealed class LightTracingResult
{
    /// <summary>Alıcı yüzeylerin isabet yoğunluğu (0..1 normalize) — yüzey başına.</summary>
    public IReadOnlyDictionary<int, double> SurfaceIntensity { get; }

    /// <summary>Alıcı yüzeyde ışınların düştüğü 3D noktalar (ısı haritası için).</summary>
    public IReadOnlyList<LightHitPoint> ReceiverHitPoints { get; }

    /// <summary>Yalnızca ayna→alıcı yollarından oluşan ışın segmentleri (görselleştirme).</summary>
    public IReadOnlyList<RaySegment> RayPaths { get; }

    /// <summary>Aktif ayna yüzey ID'leri (renderer için).</summary>
    public IReadOnlySet<int> MirrorSurfaceIds { get; }

    public int TotalRays { get; }
    public int TotalHits { get; }

    public LightTracingResult(
        IReadOnlyDictionary<int, double> surfaceIntensity,
        IReadOnlyList<LightHitPoint>     receiverHitPoints,
        IReadOnlyList<RaySegment>        rayPaths,
        IReadOnlySet<int>                mirrorSurfaceIds,
        int totalRays,
        int totalHits)
    {
        SurfaceIntensity  = surfaceIntensity;
        ReceiverHitPoints = receiverHitPoints;
        RayPaths          = rayPaths;
        MirrorSurfaceIds  = mirrorSurfaceIds;
        TotalRays         = totalRays;
        TotalHits         = totalHits;
    }
}
