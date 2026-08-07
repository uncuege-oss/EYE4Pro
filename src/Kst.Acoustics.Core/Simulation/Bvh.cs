using System;
using System.Collections.Generic;
using Kst.Acoustics.Core.Geometry;

namespace Kst.Acoustics.Core.Simulation;

internal struct BvhNode
{
    public BoundingBox Bounds;
    public int LeftIndex;   // If < 0, then -(index + 1) is index into leaf triangles
    public int RightIndex;
    public int TriangleCount;
}

internal struct BoundingBox
{
    public Point3D Min;
    public Point3D Max;

    public static BoundingBox FromPoints(params Point3D[] points)
    {
        double xmin = double.MaxValue, xmax = double.MinValue;
        double ymin = double.MaxValue, ymax = double.MinValue;
        double zmin = double.MaxValue, zmax = double.MinValue;
        foreach (var p in points)
        {
            if (p.X < xmin) xmin = p.X; if (p.X > xmax) xmax = p.X;
            if (p.Y < ymin) ymin = p.Y; if (p.Y > ymax) ymax = p.Y;
            if (p.Z < zmin) zmin = p.Z; if (p.Z > zmax) zmax = p.Z;
        }
        return new BoundingBox { Min = new Point3D(xmin, ymin, zmin), Max = new Point3D(xmax, ymax, zmax) };
    }

    public void Encapsulate(BoundingBox other)
    {
        Min = new Point3D(Math.Min(Min.X, other.Min.X), Math.Min(Min.Y, other.Min.Y), Math.Min(Min.Z, other.Min.Z));
        Max = new Point3D(Math.Max(Max.X, other.Max.X), Math.Max(Max.Y, other.Max.Y), Math.Max(Max.Z, other.Max.Z));
    }

    public readonly bool Intersect(Point3D origin, Vector3D dir, out double tMin, out double tMax)
    {
        tMin = 0; tMax = double.MaxValue;
        
        double invX = 1.0 / dir.X;
        double t1 = (Min.X - origin.X) * invX;
        double t2 = (Max.X - origin.X) * invX;
        tMin = Math.Max(tMin, Math.Min(t1, t2));
        tMax = Math.Min(tMax, Math.Max(t1, t2));

        double invY = 1.0 / dir.Y;
        double t3 = (Min.Y - origin.Y) * invY;
        double t4 = (Max.Y - origin.Y) * invY;
        tMin = Math.Max(tMin, Math.Min(t3, t4));
        tMax = Math.Min(tMax, Math.Max(t3, t4));

        double invZ = 1.0 / dir.Z;
        double t5 = (Min.Z - origin.Z) * invZ;
        double t6 = (Max.Z - origin.Z) * invZ;
        tMin = Math.Max(tMin, Math.Min(t5, t6));
        tMax = Math.Min(tMax, Math.Max(t5, t6));

        return tMax >= tMin;
    }
}
