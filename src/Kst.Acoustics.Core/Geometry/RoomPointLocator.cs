using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Simulation;

namespace Kst.Acoustics.Core.Geometry;

/// <summary>Kapalı yüzey kabuğunda bir noktanın içeride olup olmadığını belirler.</summary>
public static class RoomPointLocator
{
    // Eksenle tam çakışan kenar/köşe kesişimlerini azaltan sabit, normalize yön.
    private static readonly Vector3D RayDirection = Normalize(new Vector3D(1, 0.371390676, 0.21787311));

    public static bool IsInside(Point3D point, IReadOnlyList<ProjectSurface> surfaces)
    {
        ArgumentNullException.ThrowIfNull(surfaces);
        if (!IsFinite(point) || surfaces.Count == 0) return false;

        var distances = new List<double>();
        foreach (var surface in surfaces)
        {
            if (surface.Vertices.Count < 3 || surface.Vertices.Any(v => !IsFinite(v)))
                continue;

            foreach (var (a, b, c) in RayTracingService.TriangulatePolygon(surface.Vertices))
            {
                if (TryRayTriangleDistance(point, RayDirection, a, b, c, out var distance))
                    distances.Add(distance);
            }
        }

        if (distances.Count == 0) return false;
        distances.Sort();

        // Aynı düzlemsel yüzeyin iki üçgeni ya da ortak kenar, aynı ışın mesafesinde iki
        // isabet üretebilir. Tek geometrik geçiş olarak sayılmazsa parity bozulur.
        var uniqueCount = 0;
        var last = double.NegativeInfinity;
        foreach (var distance in distances)
        {
            var tolerance = 1e-8 * Math.Max(1.0, Math.Abs(distance));
            if (uniqueCount > 0 && Math.Abs(distance - last) <= tolerance) continue;
            uniqueCount++;
            last = distance;
        }

        return uniqueCount % 2 == 1;
    }

    private static bool TryRayTriangleDistance(
        Point3D origin,
        Vector3D direction,
        Point3D a,
        Point3D b,
        Point3D c,
        out double distance)
    {
        const double epsilon = 1e-10;
        distance = 0;

        var edge1 = GeometryMath.ToVector(a, b);
        var edge2 = GeometryMath.ToVector(a, c);
        var h = GeometryMath.Cross(direction, edge2);
        var determinant = GeometryMath.Dot(edge1, h);
        if (Math.Abs(determinant) <= epsilon) return false;

        var inverse = 1.0 / determinant;
        var s = GeometryMath.ToVector(a, origin);
        var u = inverse * GeometryMath.Dot(s, h);
        if (u < -epsilon || u > 1.0 + epsilon) return false;

        var q = GeometryMath.Cross(s, edge1);
        var v = inverse * GeometryMath.Dot(direction, q);
        if (v < -epsilon || u + v > 1.0 + epsilon) return false;

        var t = inverse * GeometryMath.Dot(edge2, q);
        if (t <= epsilon || !double.IsFinite(t)) return false;

        distance = t;
        return true;
    }

    private static Vector3D Normalize(Vector3D value)
    {
        var length = Math.Sqrt(GeometryMath.Dot(value, value));
        return new Vector3D(value.X / length, value.Y / length, value.Z / length);
    }

    private static bool IsFinite(Point3D point) =>
        double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(point.Z);
}
