using System;
using System.Collections.Generic;
using System.Linq;
using TriangleNet.Geometry;
using TriangleNet.Meshing;
using Kst.Acoustics.Core.Terrain.Models;

namespace Kst.Acoustics.Core.Terrain.Services;

/// F2 — Arazi sayısal yüzeyi (DTM) üreticisi. Spot kotları ve konturları
/// (eşyükselti eğrilerini noktalara yoğunlaştırarak) toplar ve Delaunay TIN üretir.
/// Üçgenleme Triangle.NET (TriangleNet) ile yapılır.
public class AraziYuzeyServisi
{
    /// Kontur eğrisi üzerindeki ardışık köşeler arası örnekleme aralığı (metre).
    /// Daha küçük = daha yoğun TIN. 0 veya negatifse yalnız köşeler kullanılır.
    public double KonturOrneklemeAraligiM { get; set; } = 5.0;

    /// İki kotsuz nokta birbirine bu mesafeden yakınsa aynı sayılır (tekilleştirme, metre).
    public double TekillestirmeToleransiM { get; set; } = 1e-6;

    /// Arazi modelinden TIN üretir. Kotu çözülemeyen (Bilinmiyor) konturlar atlanır.
    public AraziYuzeyi Olustur(AraziModeli arazi)
    {
        var noktalar = ToplaNoktalar(arazi);
        return Ucgenle(noktalar);
    }

    /// (x,y,z) düğüm listesinden Delaunay TIN üretir. 3'ten az veya tümü kolineer
    /// noktada boş yüzey döner.
    public AraziYuzeyi Ucgenle(IReadOnlyList<AraziDugum> noktalar)
    {
        var yuzey = new AraziYuzeyi();
        if (noktalar.Count < 3) return yuzey;

        // Triangle.NET Vertex Z taşımaz. Saf Delaunay (kalite iyileştirmesi yok) Steiner
        // noktası eklemediğinden çıktı köşeleri girdi köşeleriyle birebir örtüşür; Z'yi
        // koordinat anahtarlı sözlükten geri buluruz.
        var verts = new List<Vertex>(noktalar.Count);
        var zSozluk = new Dictionary<(long, long), double>();
        foreach (var n in noktalar)
        {
            verts.Add(new Vertex(n.X, n.Y));
            zSozluk[KoordAnahtar(n.X, n.Y)] = n.Z;
        }

        IMesh mesh;
        try
        {
            mesh = new GenericMesher().Triangulate(verts);
        }
        catch
        {
            return yuzey; // kolineer/dejenere girdi
        }

        // Düğümleri yeniden indeksle (mesh vertex ID'leri seyrek olabilir).
        var idToIndex = new Dictionary<int, int>();
        foreach (var v in mesh.Vertices)
        {
            idToIndex[v.ID] = yuzey.Dugumler.Count;
            double z = zSozluk.TryGetValue(KoordAnahtar(v.X, v.Y), out var zz) ? zz : 0.0;
            yuzey.Dugumler.Add(new AraziDugum(v.X, v.Y, z));
        }

        foreach (var t in mesh.Triangles)
        {
            int a = idToIndex[t.GetVertexID(0)];
            int b = idToIndex[t.GetVertexID(1)];
            int c = idToIndex[t.GetVertexID(2)];
            yuzey.Ucgenler.Add(new AraziUcgen(a, b, c));
        }

        return yuzey;
    }

    // Koordinatı 1e-6 m çözünürlükte tamsayı anahtara çevirir (Z geri eşleme için).
    private static (long, long) KoordAnahtar(double x, double y)
        => ((long)Math.Round(x * 1_000_000.0), (long)Math.Round(y * 1_000_000.0));

    // ── Nokta toplama + kontur yoğunlaştırma ──────────────────────────────────

    private List<AraziDugum> ToplaNoktalar(AraziModeli arazi)
    {
        var noktalar = new List<AraziDugum>();

        foreach (var n in arazi.Noktalar)
            noktalar.Add(new AraziDugum(n.X, n.Y, n.Z));

        foreach (var k in arazi.Konturlar)
        {
            if (k.KotKaynagi == AraziKotKaynagi.Bilinmiyor) continue;
            foreach (var p in Yogunlastir(k))
                noktalar.Add(new AraziDugum(p.X, p.Y, k.KotM));
        }

        return noktalar;
    }

    private IEnumerable<(double X, double Y)> Yogunlastir(AraziKonturu kontur)
    {
        var pts = kontur.Noktalar;
        if (pts.Count == 0) yield break;

        if (KonturOrneklemeAraligiM <= 0)
        {
            foreach (var p in pts) yield return (p.X, p.Y);
            yield break;
        }

        int son = kontur.Kapali ? pts.Count : pts.Count - 1;
        for (int i = 0; i < son; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % pts.Count];
            yield return (a.X, a.Y);

            double dx = b.X - a.X, dy = b.Y - a.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            int adim = (int)(len / KonturOrneklemeAraligiM);
            for (int j = 1; j <= adim; j++)
            {
                double t = (j * KonturOrneklemeAraligiM) / len;
                if (t >= 1.0) break;
                yield return (a.X + dx * t, a.Y + dy * t);
            }
        }

        if (!kontur.Kapali)
            yield return (pts[^1].X, pts[^1].Y);
    }
}
