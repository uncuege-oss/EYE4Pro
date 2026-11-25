using System.Numerics;
using Kst.Acoustics.Core.Terrain.Models;
using Kst.Acoustics.Core.Terrain.Services;

namespace Kst.Acoustics.App.Terrain.Rendering;

internal sealed partial class TerrainMesh
{
    private const byte Cut = 1, Fill = 2, Level = 4;
    private static byte DifferenceKind(double difference) => difference > 0 ? Cut : difference < 0 ? Fill : Level;

    internal static (TerrainMesh First, TerrainMesh Last) Compare(AraziKarsilastirmasi result,
        AraziYuzeyi firstTin, AraziYuzeyi lastTin, CancellationToken token)
    {
        var origin = result.Parcalar[0].A;
        double minZ = double.PositiveInfinity, maxZ = double.NegativeInfinity;
        double minX = minZ, minY = minZ, maxX = maxZ, maxY = maxZ;
        void Include(KarsilastirmaNoktasi p)
        {
            minZ = Math.Min(minZ,Math.Min(p.IlkKot,p.SonKot)); maxZ = Math.Max(maxZ,Math.Max(p.IlkKot,p.SonKot));
            minX = Math.Min(minX,p.X); maxX = Math.Max(maxX,p.X);
            minY = Math.Min(minY,p.Y); maxY = Math.Max(maxY,p.Y);
        }
        var firstArea = new double[firstTin.Ucgenler.Count];
        var lastArea = new double[lastTin.Ucgenler.Count];
        var kinds = new byte[lastTin.Ucgenler.Count];
        foreach (var part in result.Parcalar)
        {
            firstArea[part.IlkUcgen] += part.AlanM2;
            lastArea[part.SonUcgen] += part.AlanM2;
            kinds[part.SonUcgen] |= DifferenceKind(part.OrtalamaFark);
            Include(part.A); Include(part.B); Include(part.C);
        }
        var bounds = new Vector3[8];
        for (int i = 0; i < bounds.Length; i++)
            bounds[i] = Local(((i&1)==0 ? minX : maxX)-origin.X, ((i&2)==0 ? minY : maxY)-origin.Y,
                ((i&4)==0 ? minZ : maxZ)-origin.IlkKot);
        return (Layer(firstTin,firstArea,null), Layer(lastTin,lastArea,kinds));

        TerrainMesh Layer(AraziYuzeyi tin, double[] coverage, byte[]? colors)
        {
            var lookup = new Dictionary<AraziDugum,int>();
            var points = new List<Vector3>();
            var faces = new List<Face>(tin.Ucgenler.Count);
            var complete = new bool[coverage.Length];
            int Index(AraziDugum p)
            {
                if (lookup.TryGetValue(p,out int index)) return index;
                index = points.Count;
                lookup.Add(p,index);
                points.Add(Local(p.X-origin.X,p.Y-origin.Y,p.Z-origin.IlkKot));
                return index;
            }
            void Add(AraziDugum a, AraziDugum b, AraziDugum c, byte kind)
            {
                var face = BuildFace(points,Index(a),Index(b),Index(c),minZ-origin.IlkKot,maxZ-origin.IlkKot,false);
                if (colors != null)
                {
                    int rgb = kind == Cut ? 0xDD5545 : kind == Fill ? 0x398FD2 : 0xB0B8B5;
                    face = face with { Color = Shade(rgb,face.Shade) };
                }
                faces.Add(face);
            }
            // A fully covered source face has exactly the same plane as its overlay
            // fragments. Restore that triangle when the display color is uniform.
            for (int i = 0; i < coverage.Length; i++)
            {
                if ((i & 4095)==0) token.ThrowIfCancellationRequested();
                if (coverage[i] == 0) continue;
                byte kind = colors?[i] ?? Level;
                if (kind is not (Cut or Fill or Level)) continue;
                var t = tin.Ucgenler[i];
                var a = tin.Dugumler[t.A]; var b = tin.Dugumler[t.B]; var c = tin.Dugumler[t.C];
                double area = Math.Abs((b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X))/2;
                if (Math.Abs(area-coverage[i]) > Math.Max(1e-10,area*1e-9)) continue;
                complete[i] = true;
                Add(a,b,c,kind);
            }
            AraziDugum Node(KarsilastirmaNoktasi p) => new(p.X,p.Y,colors==null ? p.IlkKot : p.SonKot);
            foreach (var part in result.Parcalar)
            {
                if (complete[colors==null ? part.IlkUcgen : part.SonUcgen]) continue;
                Add(Node(part.A),Node(part.B),Node(part.C),DifferenceKind(part.OrtalamaFark));
            }
            return new TerrainMesh(points.ToArray(),faces.ToArray(),minZ,maxZ) { SharedFrame = bounds };
        }
    }
}
