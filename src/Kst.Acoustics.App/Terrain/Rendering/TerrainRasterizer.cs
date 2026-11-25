using System.Numerics;

namespace Kst.Acoustics.App.Terrain.Rendering;

internal readonly record struct TerrainCamera(double Azimuth, double Elevation, double Zoom, double PanX, double PanY)
{
    internal static TerrainCamera Home => new(.8,.5,1,0,0);
}

// One worker owns the scratch buffers. Orthographic depth is linear across each triangle.
internal sealed class TerrainRasterizer
{
    internal const int Background = unchecked((int)0xFF0C1624);
    internal int[] Pixels { get; private set; } = [];
    private float[] _depth = [];
    private Vector3[] _screen = [];

    internal double Draw(TerrainMesh mesh, int[]? colors, TerrainCamera camera, int width, int height,
        double logicalWidth, double logicalHeight, CancellationToken cancel = default)
    {
        int size = checked(width * height);
        if (Pixels.Length != size) { Pixels = new int[size]; _depth = new float[size]; }
        Array.Fill(Pixels, Background); Array.Fill(_depth, float.NegativeInfinity);
        var points = mesh.Points;
        if (points.Length == 0) return 1;
        if (_screen.Length != points.Length) _screen = new Vector3[points.Length];
        float ca=(float)Math.Cos(camera.Azimuth), sa=(float)Math.Sin(camera.Azimuth);
        float ce=(float)Math.Cos(camera.Elevation), se=(float)Math.Sin(camera.Elevation);
        float minX=float.PositiveInfinity, minY=minX, maxX=float.NegativeInfinity, maxY=maxX;
        for (int i=0;i<points.Length;i++)
        {
            var p=points[i]; float u=p.X*sa+p.Y*ca;
            float x=p.X*ca-p.Y*sa, y=-(u*se+p.Z*ce);
            _screen[i]=new(x,y,p.Z*se-u*ce);
            minX=Math.Min(minX,x); maxX=Math.Max(maxX,x); minY=Math.Min(minY,y); maxY=Math.Max(maxY,y);
        }
        // Both comparison panes must use the same surveyed origin and camera extent,
        // including when one surface is entirely higher than the other.
        if (mesh.SharedFrame is { } frame)
        {
            minX=minY=float.PositiveInfinity;
            maxX=maxY=float.NegativeInfinity;
            foreach (var p in frame)
            {
                float x=p.X*ca-p.Y*sa, y=-((p.X*sa+p.Y*ca)*se+p.Z*ce);
                minX=Math.Min(minX,x); maxX=Math.Max(maxX,x); minY=Math.Min(minY,y); maxY=Math.Max(maxY,y);
            }
        }
        double scale=Math.Max(.001,Math.Min(Math.Max(1,logicalWidth-52)/Math.Max(.01,maxX-minX),
            Math.Max(1,logicalHeight-52)/Math.Max(.01,maxY-minY)))*camera.Zoom;
        float sx=(float)(scale*width/logicalWidth), sy=(float)(scale*height/logicalHeight);
        float ox=(float)((logicalWidth*.5+camera.PanX)*width/logicalWidth)-(maxX+minX)*.5f*sx;
        float oy=(float)((logicalHeight*.5+camera.PanY)*height/logicalHeight)-(maxY+minY)*.5f*sy;
        for (int i=0;i<points.Length;i++) { ref var p=ref _screen[i]; p.X=p.X*sx+ox; p.Y=p.Y*sy+oy; }
        var faces=mesh.Faces;
        for (int i=0;i<faces.Length;i++)
        {
            if ((i&1023)==0) cancel.ThrowIfCancellationRequested();
            ref readonly var f=ref faces[i];
            Triangle(_screen[f.A],_screen[f.B],_screen[f.C], colors==null?f.Color:colors[i],width,height);
        }
        return scale;
    }
    private void Triangle(Vector3 a, Vector3 b, Vector3 c, int color, int width, int height)
    {
        if (Math.Max(a.X,Math.Max(b.X,c.X))<.5f || Math.Min(a.X,Math.Min(b.X,c.X))>=width ||
            Math.Max(a.Y,Math.Max(b.Y,c.Y))<.5f || Math.Min(a.Y,Math.Min(b.Y,c.Y))>=height) return;
        if (a.Y>b.Y) (a,b)=(b,a);
        if (b.Y>c.Y) (b,c)=(c,b);
        if (a.Y>b.Y) (a,b)=(b,a);
        if (c.Y<=a.Y) return;
        float lx=(c.X-a.X)/(c.Y-a.Y), lz=(c.Z-a.Z)/(c.Y-a.Y);
        SpanRows(a,b,a,lx,lz,color,width,height);
        SpanRows(b,c,a,lx,lz,color,width,height);
    }
    private void SpanRows(Vector3 s, Vector3 e, Vector3 a, float lx, float lz, int color,int width,int height)
    {
        // Half-open pixel-centre coverage: shared edges neither double-fill nor leave gaps.
        int start=Edge(s.Y,height), end=Edge(e.Y,height);
        if (start>=end) return;
        float rx=(e.X-s.X)/(e.Y-s.Y), rz=(e.Z-s.Z)/(e.Y-s.Y);
        for(int y=start;y<end;y++)
        {
            float v=y+.5f;
            float x1=a.X+(v-a.Y)*lx, z1=a.Z+(v-a.Y)*lz;
            float x2=s.X+(v-s.Y)*rx, z2=s.Z+(v-s.Y)*rz;
            if(x1>x2) { (x1,x2)=(x2,x1); (z1,z2)=(z2,z1); }
            int lo=Edge(x1,width), hi=Edge(x2,width);
            if(lo>=hi) continue;
            float dz=(z2-z1)/(x2-x1), z=z1+(lo+.5f-x1)*dz;
            for(int k=y*width+lo, stop=y*width+hi;k<stop;k++,z+=dz)
                if(z>_depth[k]) { _depth[k]=z; Pixels[k]=color; }
        }
    }
    private static int Edge(float position,int limit) => (int)Math.Clamp(MathF.Ceiling(position-.5f),0,limit);
}
