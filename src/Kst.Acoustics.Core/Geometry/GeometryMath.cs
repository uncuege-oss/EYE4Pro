namespace Kst.Acoustics.Core.Geometry;

public static class GeometryMath
{
    public static Vector3D ToVector(Point3D from, Point3D to) => new(to.X - from.X, to.Y - from.Y, to.Z - from.Z);

    public static Point3D Add(Point3D point, Vector3D vector) => new(point.X + vector.X, point.Y + vector.Y, point.Z + vector.Z);

    public static double Dot(Vector3D left, Vector3D right) => (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z);

    public static Vector3D Cross(Vector3D left, Vector3D right)
    {
        return new Vector3D(
            (left.Y * right.Z) - (left.Z * right.Y),
            (left.Z * right.X) - (left.X * right.Z),
            (left.X * right.Y) - (left.Y * right.X));
    }

    public static Vector3D Reflect(Vector3D direction, Vector3D normal)
    {
        var normalizedDirection = direction.Normalize();
        var normalizedNormal = normal.Normalize();
        return (normalizedDirection - (normalizedNormal * (2d * Dot(normalizedDirection, normalizedNormal)))).Normalize();
    }
}
