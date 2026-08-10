using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Importing;

namespace Kst.Acoustics.Core.Projects;

public enum AcousticPointArrayPattern
{
    Linear,
    RectangularGrid,
    Circular,
    BoundaryGrid
}

public sealed record AcousticPointPlacementRequest(
    AcousticPointType PointType,
    AcousticPointArrayPattern Pattern,
    Point3D Origin,
    int StartId = 1,
    string LabelPrefix = "R",
    int CountX = 1,
    int CountY = 1,
    double SpacingMeters = 1.0,
    double RadiusMeters = 2.0,
    double AzimuthDegrees = 0,
    SurfaceImportUnit Unit = SurfaceImportUnit.Meters,
    IReadOnlyList<Point3D>? Boundary = null,
    bool KeepInsideRoom = true,
    int MaximumPointCount = 4_000);

public sealed record AcousticPointPlacementResult(
    IReadOnlyList<AcousticPoint> Points,
    int RejectedOutsideRoom,
    int RejectedOutsideBoundary,
    string Summary);

/// <summary>
/// Kaynak/alıcı dizilerini fiziksel metre girdileriyle üretir. Servis geometriyi
/// değiştirmez; M50/M51 yazarlık sınırını koruyarak yalnız M01 analiz noktaları üretir.
/// </summary>
public static class AcousticPointPlacementService
{
    public static AcousticPointPlacementResult Generate(
        AcousticPointPlacementRequest request,
        IReadOnlyList<ProjectSurface>? roomSurfaces = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(request.PointType) || !Enum.IsDefined(request.Pattern))
            throw new ArgumentOutOfRangeException(nameof(request), "Nokta tipi veya dizi biçimi geçersiz.");
        if (!Enum.IsDefined(request.Unit))
            throw new ArgumentOutOfRangeException(nameof(request.Unit), "Proje birimi geçersiz.");
        if (!IsFinite(request.Origin) || !double.IsFinite(request.SpacingMeters) || request.SpacingMeters <= 0 ||
            !double.IsFinite(request.RadiusMeters) || request.RadiusMeters <= 0 ||
            !double.IsFinite(request.AzimuthDegrees))
            throw new ArgumentOutOfRangeException(nameof(request), "Yerleştirme ölçüleri pozitif ve sonlu olmalıdır.");
        if (request.CountX <= 0 || request.CountY <= 0 || request.MaximumPointCount is <= 0 or > 100_000)
            throw new ArgumentOutOfRangeException(nameof(request), "Nokta adetleri pozitif ve güvenli sınırlar içinde olmalıdır.");
        if (string.IsNullOrWhiteSpace(request.LabelPrefix))
            throw new ArgumentException("Nokta etiketi öneki boş olamaz.", nameof(request));

        var requestedCount = request.Pattern == AcousticPointArrayPattern.RectangularGrid
            ? (long)request.CountX * request.CountY
            : request.Pattern is AcousticPointArrayPattern.Linear or AcousticPointArrayPattern.Circular
                ? request.CountX
                : 0;
        if (requestedCount > request.MaximumPointCount)
            throw new InvalidDataException(
                $"Yerleştirme {requestedCount:N0} nokta üretiyor; sınır {request.MaximumPointCount:N0}.");

        var metersPerUnit = ProjectScalingService.GetMetersPerUnit(request.Unit);
        var spacing = request.SpacingMeters / metersPerUnit;
        var radius = request.RadiusMeters / metersPerUnit;
        var candidates = request.Pattern switch
        {
            AcousticPointArrayPattern.Linear => GenerateLinear(request, spacing),
            AcousticPointArrayPattern.RectangularGrid => GenerateGrid(request, spacing),
            AcousticPointArrayPattern.Circular => GenerateCircle(request, radius),
            AcousticPointArrayPattern.BoundaryGrid => GenerateBoundaryGrid(request, spacing),
            _ => []
        };

        if (candidates.Count > request.MaximumPointCount)
            throw new InvalidDataException(
                $"Yerleştirme {candidates.Count:N0} nokta üretiyor; sınır {request.MaximumPointCount:N0}.");

        var hasBoundary = request.Boundary is { Count: >= 3 };
        var checkClosedRoom = request.KeepInsideRoom && roomSurfaces is { Count: > 0 } &&
            RoomVolumeCalculator.Estimate(roomSurfaces, request.Unit).Origin == RoomVolumeOrigin.ClosedMesh;
        var rejectedBoundary = 0;
        var rejectedRoom = 0;
        var accepted = new List<Point3D>(candidates.Count);
        foreach (var candidate in candidates)
        {
            if (!IsFinite(candidate)) continue;
            if (request.Pattern == AcousticPointArrayPattern.BoundaryGrid &&
                hasBoundary && !IsInsidePolygon2D(candidate, request.Boundary!))
            {
                rejectedBoundary++;
                continue;
            }

            if (checkClosedRoom && roomSurfaces is { Count: > 0 } &&
                !RoomPointLocator.IsInside(candidate, roomSurfaces))
            {
                rejectedRoom++;
                continue;
            }

            accepted.Add(candidate);
        }

        var prefix = request.LabelPrefix.Trim();
        var points = accepted.Select((position, index) => new AcousticPoint(
            request.StartId + index,
            $"{prefix}{request.StartId + index}",
            request.PointType,
            position)).ToArray();

        var summary = $"{points.Length:N0} {Role(request.PointType)} üretildi" +
            (rejectedRoom + rejectedBoundary > 0
                ? $"; {rejectedRoom + rejectedBoundary:N0} aday sınır dışında bırakıldı."
                : ".");
        return new AcousticPointPlacementResult(points, rejectedRoom, rejectedBoundary, summary);
    }

    private static List<Point3D> GenerateLinear(AcousticPointPlacementRequest request, double spacing)
    {
        var angle = request.AzimuthDegrees * Math.PI / 180.0;
        var dx = Math.Cos(angle) * spacing;
        var dy = Math.Sin(angle) * spacing;
        return Enumerable.Range(0, request.CountX)
            .Select(i => new Point3D(
                request.Origin.X + i * dx,
                request.Origin.Y + i * dy,
                request.Origin.Z))
            .ToList();
    }

    private static List<Point3D> GenerateGrid(AcousticPointPlacementRequest request, double spacing)
    {
        var candidateCount = (long)request.CountX * request.CountY;
        if (candidateCount > request.MaximumPointCount)
            throw new InvalidDataException(
                $"Yerleştirme {candidateCount:N0} nokta üretiyor; sınır {request.MaximumPointCount:N0}.");
        var result = new List<Point3D>((int)candidateCount);
        for (var row = 0; row < request.CountY; row++)
        for (var col = 0; col < request.CountX; col++)
            result.Add(new Point3D(
                request.Origin.X + col * spacing,
                request.Origin.Y + row * spacing,
                request.Origin.Z));
        return result;
    }

    private static List<Point3D> GenerateCircle(AcousticPointPlacementRequest request, double radius)
    {
        var count = request.CountX;
        var start = request.AzimuthDegrees * Math.PI / 180.0;
        return Enumerable.Range(0, count)
            .Select(i =>
            {
                var angle = start + i * Math.Tau / count;
                return new Point3D(
                    request.Origin.X + radius * Math.Cos(angle),
                    request.Origin.Y + radius * Math.Sin(angle),
                    request.Origin.Z);
            })
            .ToList();
    }

    private static List<Point3D> GenerateBoundaryGrid(AcousticPointPlacementRequest request, double spacing)
    {
        if (request.Boundary is not { Count: >= 3 } boundary || boundary.Any(p => !IsFinite(p)))
            throw new InvalidDataException("Sınır içi ızgara için en az üç geçerli sınır köşesi gerekir.");

        var minX = boundary.Min(p => p.X);
        var maxX = boundary.Max(p => p.X);
        var minY = boundary.Min(p => p.Y);
        var maxY = boundary.Max(p => p.Y);
        var cols = SafeCount(minX, maxX, spacing);
        var rows = SafeCount(minY, maxY, spacing);
        if ((long)cols * rows > request.MaximumPointCount * 4L)
            throw new InvalidDataException("Sınır ızgarası güvenli aday sayısını aşıyor; aralığı büyütün.");

        var result = new List<Point3D>(cols * rows);
        for (var row = 0; row < rows; row++)
        for (var col = 0; col < cols; col++)
            result.Add(new Point3D(minX + col * spacing, minY + row * spacing, request.Origin.Z));
        return result;
    }

    private static int SafeCount(double min, double max, double spacing)
    {
        var raw = Math.Floor((max - min) / spacing) + 1;
        if (!double.IsFinite(raw) || raw is < 1 or > int.MaxValue)
            throw new InvalidDataException("Yerleştirme sınırları geçersiz veya aşırı büyük.");
        return (int)raw;
    }

    private static bool IsInsidePolygon2D(Point3D p, IReadOnlyList<Point3D> polygon)
    {
        var inside = false;
        for (var i = 0; i < polygon.Count; i++)
        {
            var j = (i + polygon.Count - 1) % polygon.Count;
            var a = polygon[i];
            var b = polygon[j];
            if ((a.Y > p.Y) == (b.Y > p.Y)) continue;
            var x = (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X;
            if (p.X < x) inside = !inside;
        }
        return inside;
    }

    private static bool IsFinite(Point3D point) =>
        double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(point.Z);

    private static string Role(AcousticPointType type) =>
        type == AcousticPointType.Source ? "kaynak" : "alıcı";
}
