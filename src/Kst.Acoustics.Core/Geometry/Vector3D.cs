namespace Kst.Acoustics.Core.Geometry;

public readonly record struct Vector3D(double X, double Y, double Z)
{
    public double Length => Math.Sqrt((X * X) + (Y * Y) + (Z * Z));

    public Vector3D Normalize()
    {
        var length = Length;
        return length > 1e-12
            ? new Vector3D(X / length, Y / length, Z / length)
            : new Vector3D(0, 0, 0);
    }

    public static Vector3D operator +(Vector3D left, Vector3D right) => new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

    public static Vector3D operator -(Vector3D left, Vector3D right) => new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

    public static Vector3D operator -(Vector3D value) => new(-value.X, -value.Y, -value.Z);

    public static Vector3D operator *(Vector3D value, double scalar) => new(value.X * scalar, value.Y * scalar, value.Z * scalar);

    public static Vector3D operator /(Vector3D value, double scalar) => new(value.X / scalar, value.Y / scalar, value.Z / scalar);
}
