using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.Core.Geometry;

public enum RotationAxis { X, Y, Z }

public static class SurfaceTransformService
{
    /// <summary>
    /// Yüzeyin tüm köşelerini centroid etrafında verilen eksen boyunca döndürür.
    /// Döndürülmüş yeni <see cref="ProjectSurface"/> döner.
    /// </summary>
    public static ProjectSurface RotateAroundCentroid(
        ProjectSurface surface, RotationAxis axis, double angleDeg)
    {
        var rad      = angleDeg * Math.PI / 180.0;
        var centroid = ComputeCentroid(surface.Vertices);
        var k        = AxisVector(axis);

        var newVertices = surface.Vertices
            // Eski proje dosyalarında bulunabilecek NaN delik ayıracını en azından
            // dönüşüm sırasında çoğaltma/centroid'e yayma; ayıracı olduğu gibi koru.
            .Select(v => IsFinite(v) ? RotatePoint(v, centroid, k, rad) : v)
            .ToArray();

        return surface with { Vertices = newVertices };
    }

    public static Point3D ComputeCentroid(IReadOnlyList<Point3D> vertices)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        var finite = vertices.Where(IsFinite).ToArray();
        if (finite.Length == 0) return default;

        var x = finite.Average(v => v.X);
        var y = finite.Average(v => v.Y);
        var z = finite.Average(v => v.Z);
        return new Point3D(x, y, z);
    }

    /// <summary>
    /// Verilen 3B çokgen köşelerinden alan hesaplar (Stokes teoremi varyasyonu).
    /// </summary>
    public static double ComputeArea(IReadOnlyList<Point3D> vertices)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        var loops = SplitFiniteLoops(vertices);
        if (loops.Count == 0) return 0.0;

        // Legacy NaN-ayraçlı yüzeyde ilk halka dış kontur, devamındakiler deliktir.
        // Yeni import kalıcı modele NaN üretmez; bu yol yalnız geriye dönük güvenliktir.
        var area = ComputeSimpleArea(loops[0]);
        for (var i = 1; i < loops.Count; i++)
        {
            area -= ComputeSimpleArea(loops[i]);
        }

        return Math.Max(0, area);
    }

    private static double ComputeSimpleArea(IReadOnlyList<Point3D> vertices)
    {
        if (vertices.Count < 3) return 0.0;

        double cx = 0, cy = 0, cz = 0;
        for (int i = 0; i < vertices.Count; i++)
        {
            var p1 = vertices[i];
            var p2 = vertices[(i + 1) % vertices.Count];

            cx += (p1.Y * p2.Z) - (p1.Z * p2.Y);
            cy += (p1.Z * p2.X) - (p1.X * p2.Z);
            cz += (p1.X * p2.Y) - (p1.Y * p2.X);
        }

        return 0.5 * Math.Sqrt(cx * cx + cy * cy + cz * cz);
    }

    // ─── Rodrigues' rotation formula ────────────────────────────────────────
    private static Point3D RotatePoint(Point3D p, Point3D origin, Vector3D k, double rad)
    {
        // v = p - origin
        var vx = p.X - origin.X;
        var vy = p.Y - origin.Y;
        var vz = p.Z - origin.Z;

        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        var dot = k.X*vx + k.Y*vy + k.Z*vz; // k · v

        // v_rot = v*cos + (k×v)*sin + k*(k·v)*(1-cos)
        var crossX = k.Y*vz - k.Z*vy;
        var crossY = k.Z*vx - k.X*vz;
        var crossZ = k.X*vy - k.Y*vx;

        var rx = vx*cos + crossX*sin + k.X*dot*(1-cos);
        var ry = vy*cos + crossY*sin + k.Y*dot*(1-cos);
        var rz = vz*cos + crossZ*sin + k.Z*dot*(1-cos);

        return new Point3D(origin.X + rx, origin.Y + ry, origin.Z + rz);
    }

    /// <summary>
    /// Yüzeyin tüm köşelerini verilen delta kadar ötelr.
    /// </summary>
    public static ProjectSurface Translate(
        ProjectSurface surface, double dx, double dy, double dz)
    {
        var newVertices = surface.Vertices
            .Select(v => IsFinite(v) ? new Point3D(v.X + dx, v.Y + dy, v.Z + dz) : v)
            .ToArray();
        return surface with { Vertices = newVertices };
    }

    private static List<IReadOnlyList<Point3D>> SplitFiniteLoops(IReadOnlyList<Point3D> vertices)
    {
        var loops = new List<IReadOnlyList<Point3D>>();
        var current = new List<Point3D>();

        foreach (var point in vertices)
        {
            if (IsFinite(point))
            {
                current.Add(point);
                continue;
            }

            if (current.Count >= 3) loops.Add(current);
            current = [];
        }

        if (current.Count >= 3) loops.Add(current);
        return loops;
    }

    private static bool IsFinite(Point3D point) =>
        double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(point.Z);

    private static Vector3D AxisVector(RotationAxis axis) => axis switch
    {
        RotationAxis.X => new Vector3D(1, 0, 0),
        RotationAxis.Y => new Vector3D(0, 1, 0),
        _              => new Vector3D(0, 0, 1),
    };
}
