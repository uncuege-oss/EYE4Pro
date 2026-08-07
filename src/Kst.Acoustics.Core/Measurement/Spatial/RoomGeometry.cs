using System;
using System.Collections.Generic;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Importing;

namespace Kst.Acoustics.Core.Measurement.Spatial;

public readonly record struct Triangle3D(Point3D A, Point3D B, Point3D C);

public sealed class RoomGeometry
{
    public IReadOnlyList<Triangle3D> Triangles { get; }
    public Point3D Min { get; }
    public Point3D Max { get; }
    public string Source { get; }

    public RoomGeometry(IReadOnlyList<Triangle3D> triangles, string source)
    {
        Triangles = triangles ?? throw new ArgumentNullException(nameof(triangles));
        Source = source;
        (Min, Max) = ComputeBounds(triangles);
    }

    public Point3D Center => new(
        (Min.X + Max.X) * 0.5,
        (Min.Y + Max.Y) * 0.5,
        (Min.Z + Max.Z) * 0.5);

    public double DiagonalLength
    {
        get
        {
            var dx = Max.X - Min.X;
            var dy = Max.Y - Min.Y;
            var dz = Max.Z - Min.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }

    public double Length => Max.X - Min.X;
    public double Width  => Max.Y - Min.Y;
    public double Height => Max.Z - Min.Z;

    public static RoomGeometry FromBox(double length, double width, double height)
    {
        if (length <= 0 || width <= 0 || height <= 0)
            throw new ArgumentException("Oda boyutları sıfırdan büyük olmalı.");

        var p000 = new Point3D(0, 0, 0);
        var p100 = new Point3D(length, 0, 0);
        var p110 = new Point3D(length, width, 0);
        var p010 = new Point3D(0, width, 0);
        var p001 = new Point3D(0, 0, height);
        var p101 = new Point3D(length, 0, height);
        var p111 = new Point3D(length, width, height);
        var p011 = new Point3D(0, width, height);

        var tris = new List<Triangle3D>(12)
        {
            new(p000, p100, p110), new(p000, p110, p010),
            new(p001, p011, p111), new(p001, p111, p101),
            new(p000, p001, p101), new(p000, p101, p100),
            new(p010, p110, p111), new(p010, p111, p011),
            new(p000, p010, p011), new(p000, p011, p001),
            new(p100, p101, p111), new(p100, p111, p110),
        };

        return new RoomGeometry(tris, $"Kutu {length:F2}×{width:F2}×{height:F2} m");
    }

    public static RoomGeometry FromSurfaces(SurfaceImportDocument document, string sourceLabel)
    {
        ArgumentNullException.ThrowIfNull(document);

        var scale = UnitScale(document.Unit);
        var tris = new List<Triangle3D>();

        foreach (var surface in document.Surfaces)
        {
            if (surface.Vertices.Count < 3) continue;

            var v0 = Scale(surface.Vertices[0], scale);
            for (int i = 1; i < surface.Vertices.Count - 1; i++)
            {
                var vi = Scale(surface.Vertices[i], scale);
                var vj = Scale(surface.Vertices[i + 1], scale);
                tris.Add(new Triangle3D(v0, vi, vj));
            }
        }

        if (tris.Count == 0)
            throw new InvalidOperationException("DXF içinden üçgenleştirilebilecek yüzey çıkarılamadı.");

        return new RoomGeometry(tris, sourceLabel);
    }

    private static double UnitScale(SurfaceImportUnit unit) => unit switch
    {
        SurfaceImportUnit.Meters       => 1.0,
        SurfaceImportUnit.Centimeters  => 0.01,
        SurfaceImportUnit.Millimeters  => 0.001,
        SurfaceImportUnit.Inches       => 0.0254,
        SurfaceImportUnit.Feet         => 0.3048,
        _ => 1.0,
    };

    private static Point3D Scale(Point3D p, double s) => new(p.X * s, p.Y * s, p.Z * s);

    private static (Point3D Min, Point3D Max) ComputeBounds(IReadOnlyList<Triangle3D> tris)
    {
        if (tris.Count == 0)
            return (new Point3D(0, 0, 0), new Point3D(1, 1, 1));

        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

        void Update(Point3D p)
        {
            if (p.X < minX) minX = p.X; if (p.X > maxX) maxX = p.X;
            if (p.Y < minY) minY = p.Y; if (p.Y > maxY) maxY = p.Y;
            if (p.Z < minZ) minZ = p.Z; if (p.Z > maxZ) maxZ = p.Z;
        }

        foreach (var t in tris) { Update(t.A); Update(t.B); Update(t.C); }
        return (new Point3D(minX, minY, minZ), new Point3D(maxX, maxY, maxZ));
    }
}
