using System.Numerics;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Terrain.Models;
using Kst.Acoustics.Core.Terrain.Services;

namespace Kst.Acoustics.App.Terrain.Rendering;

// Display-only, immutable once built. Subtract the survey origin in double precision.
internal sealed partial class TerrainMesh
{
    internal readonly record struct Face(int A, int B, int C, int Color, float Shade);
    internal Vector3[] Points { get; }
    internal Face[] Faces { get; }
    internal double MinZ { get; }
    internal double MaxZ { get; }
    internal Vector3[]? SharedFrame { get; private init; }
    private TerrainMesh(Vector3[] points, Face[] faces, double min, double max)
        => (Points, Faces, MinZ, MaxZ) = (points, faces, min, max);

    internal static TerrainMesh Measured(AraziYuzeyi tin)
    {
        var nodes = tin.Dugumler;
        if (nodes.Count == 0) return new([], [], 0, 0);
        var o = nodes[0];
        double min = nodes.Min(p => p.Z), max = nodes.Max(p => p.Z);
        var points = new Vector3[nodes.Count];
        for (int i = 0; i < points.Length; i++)
            points[i] = Local(nodes[i].X-o.X, nodes[i].Y-o.Y, nodes[i].Z-o.Z);
        var faces = new Face[tin.Ucgenler.Count];
        for (int i = 0; i < faces.Length; i++)
        {
            var t = tin.Ucgenler[i];
            faces[i] = BuildFace(points, t.A, t.B, t.C, min-o.Z, max-o.Z, false);
        }
        return new(points, faces, min, max);
    }

    // The grading preview contains clipped triangles and vertical, four-corner wall strips.
    internal static TerrainMesh Design(IReadOnlyList<ProjectSurface> surfaces, TerrainMesh measured)
    {
        if (surfaces.Count == 0) return new([], [], measured.MinZ, measured.MaxZ);
        var origin = surfaces[0].Vertices[0];
        var lookup = new Dictionary<Point3D,int>();
        var points = new List<Vector3>();
        var faces = new List<Face>(surfaces.Count);
        int Vertex(Point3D p)
        {
            if (lookup.TryGetValue(p, out int i)) return i;
            i = points.Count;
            lookup.Add(p, i);
            points.Add(Local(p.X-origin.X, p.Y-origin.Y, p.Z-origin.Z));
            return i;
        }
        foreach (var surface in surfaces)
        {
            var v = surface.Vertices;
            if (v.Count < 3) continue;
            int a = Vertex(v[0]), b = Vertex(v[1]);
            for (int j = 2; j < v.Count; j++)
            {
                int c = Vertex(v[j]);
                faces.Add(BuildFace(points, a, b, c, measured.MinZ-origin.Z, measured.MaxZ-origin.Z,
                    surface.Category == Arazi3DGorunumServisi.KategoriPlatform));
                b = c;
            }
        }
        return new(points.ToArray(), faces.ToArray(), measured.MinZ, measured.MaxZ);
    }


    // Source TIN face IDs are 1-based; precompute once when a design changes.
    internal int[] HeatColors(IReadOnlyDictionary<int,double> map)
    {
        var colors = new int[Faces.Length];
        for (int i = 0; i < colors.Length; i++) colors[i] = Faces[i].Color;
        foreach (var (id, value) in map)
            if ((uint)(id-1) < (uint)Faces.Length && double.IsFinite(value))
                colors[id-1] = Shade(Heat(value), Faces[id-1].Shade);
        return colors;
    }
    private static Vector3 Local(double x, double y, double z) => new((float)x, (float)y, (float)z);
    private static Face BuildFace(IReadOnlyList<Vector3> p, int a, int b, int c, double min, double max, bool platform)
    {
        var n = Vector3.Cross(p[b]-p[a], p[c]-p[a]);
        if (n.Z < 0) n = -n;
        float len = n.Length();
        float light = len > 0 ? .64f + .36f * Math.Clamp((-.35f*n.X-.45f*n.Y+.82f*n.Z)/len, 0, 1) : 1;
        double h = max > min ? (((double)p[a].Z+p[b].Z+p[c].Z)/3-min)/(max-min) : .35;
        return new(a,b,c,Shade(platform ? 0xBED4AA : Elevation(h), light), light);
    }
    internal static int Elevation(double t) => t < .4 ? Blend(0x37775A,0x779D65,t/.4) :
        t < .75 ? Blend(0x779D65,0xB8A873,(t-.4)/.35) : Blend(0xB8A873,0xE1D6B5,(t-.75)/.25);
    internal static int Heat(double t) => t <= .5 ? Blend(0x2B77BC,0xDCE7DF,t*2) : Blend(0xDCE7DF,0xC54F42,(t-.5)*2);
    private static int Blend(int a, int b, double t)
    {
        t = Math.Clamp(t,0,1);
        int Channel(int shift) => (int)(((a>>shift)&255)*(1-t)+((b>>shift)&255)*t);
        return unchecked((int)0xFF000000) | Channel(16)<<16 | Channel(8)<<8 | Channel(0);
    }
    private static int Shade(int rgb, float s) => unchecked((int)0xFF000000) |
        (int)(((rgb>>16)&255)*s)<<16 | (int)(((rgb>>8)&255)*s)<<8 | (int)((rgb&255)*s);
}
