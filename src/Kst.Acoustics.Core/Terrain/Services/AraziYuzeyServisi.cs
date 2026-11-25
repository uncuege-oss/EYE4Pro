using TriangleNet.Geometry;
using TriangleNet.Meshing;
using Kst.Acoustics.Core.Terrain.Models;

namespace Kst.Acoustics.Core.Terrain.Services;

/// Spot kotları ve yoğunlaştırılmış konturlardan Delaunay TIN üretir.
public class AraziYuzeyServisi
{
    /// Kontur örnekleme aralığı (m). Sıfır/negatif değer yalnız köşeleri kullanır.
    public double KonturOrneklemeAraligiM { get; set; } = 5.0;
    /// Plan koordinatları bu mesafeden yakın noktalar tekilleştirilir (m).
    public double TekillestirmeToleransiM { get; set; } = 1e-6;
    /// Aynı plan konumundaki kotların izin verilen farkı (m).
    public double KotUyumToleransiM { get; set; } = 1e-4;
    public int AzamiNoktaSayisi { get; set; } = 250_000;

    public AraziYuzeyi Olustur(AraziModeli arazi)
    {
        ArgumentNullException.ThrowIfNull(arazi);
        AyarlariDogrula();
        if (arazi.KaynakUcgenleri is { } kaynak)
        {
            if (arazi.Noktalar.Count > AzamiNoktaSayisi || kaynak.Count > 2L * AzamiNoktaSayisi)
                throw new ArgumentException("CAD yüzeyi düğüm veya üçgen sınırını aşıyor.");
            var yuzey = new AraziYuzeyi { Ucgenler = kaynak.ToList() };
            foreach (var p in arazi.Noktalar)
            {
                var n = new AraziDugum(p.X, p.Y, p.Z);
                NoktayiDogrula(n);
                yuzey.Dugumler.Add(n);
            }
            AraziKarsilastirmaServisi.KaynakYuzeyiniDogrula(yuzey);
            return yuzey;
        }
        var noktalar = new List<AraziDugum>();
        void Ekle(AraziDugum n)
        {
            NoktayiDogrula(n);
            if (noktalar.Count >= AzamiNoktaSayisi)
                throw new ArgumentException("Arazi nokta sınırı aşıldı. Kontur örnekleme aralığını artırın veya katmanları daraltın.");
            noktalar.Add(n);
        }
        foreach (var n in arazi.Noktalar) Ekle(new(n.X, n.Y, n.Z));
        foreach (var k in arazi.Konturlar)
        {
            if (k.KotKaynagi == AraziKotKaynagi.Bilinmiyor) continue;
            foreach (var p in Yogunlastir(k)) Ekle(p);
        }
        return Ucgenle(noktalar);
    }

    /// Üçten az veya doğrusal noktada boş yüzey; geçersiz/çelişkili veride hata döner.
    public AraziYuzeyi Ucgenle(IReadOnlyList<AraziDugum> noktalar)
    {
        ArgumentNullException.ThrowIfNull(noktalar);
        AyarlariDogrula();
        if (noktalar.Count > AzamiNoktaSayisi)
            throw new ArgumentException("Arazi nokta sınırı aşıldı. Nokta sayısını azaltın.");
        var tekil = Tekillestir(noktalar);
        var yuzey = new AraziYuzeyi();
        if (tekil.Count < 3) return yuzey;
        // Yerel koordinatlar harita koordinatlarının büyük başlangıç değerini giderir.
        double ox = tekil[0].X, oy = tekil[0].Y;
        var verts = new List<Vertex>(tekil.Count);
        var kaynak = new Dictionary<(double, double), AraziDugum>();
        foreach (var n in tekil)
        {
            double x = n.X - ox, y = n.Y - oy;
            verts.Add(new Vertex(x, y));
            kaynak.Add((x, y), n);
        }
        var first = tekil[0]; var last = tekil[^1];
        double dx = last.X - first.X, dy = last.Y - first.Y;
        if (tekil.All(p => Math.Abs(dx * (p.Y - first.Y) - dy * (p.X - first.X)) <= 1e-12)) return yuzey;
        var mesh = new GenericMesher().Triangulate(verts);
        var idToIndex = new Dictionary<int, int>();
        foreach (var v in mesh.Vertices)
        {
            if (!kaynak.TryGetValue((v.X, v.Y), out var n))
                throw new InvalidOperationException("TIN köşesinin kaynak kotu bulunamadı; yüzey oluşturulamadı.");
            idToIndex.Add(v.ID, yuzey.Dugumler.Count);
            yuzey.Dugumler.Add(n);
        }
        foreach (var t in mesh.Triangles)
        {
            int a = idToIndex[t.GetVertexID(0)], b = idToIndex[t.GetVertexID(1)], c = idToIndex[t.GetVertexID(2)];
            var pa = yuzey.Dugumler[a]; var pb = yuzey.Dugumler[b]; var pc = yuzey.Dugumler[c];
            double alan2 = (pb.X - pa.X) * (pc.Y - pa.Y) - (pb.Y - pa.Y) * (pc.X - pa.X);
            if (!double.IsFinite(alan2)) throw new ArgumentException("Arazi koordinat aralığı geçersiz.");
            if (Math.Abs(alan2) <= 1e-12) continue;
            yuzey.Ucgenler.Add(new(a, b, c));
        }
        return yuzey;
    }

    private List<AraziDugum> Tekillestir(IReadOnlyList<AraziDugum> noktalar)
    {
        foreach (var n in noktalar) NoktayiDogrula(n);
        if (noktalar.Count == 0) return [];
        var sirali = noktalar.OrderBy(n => n.X).ThenBy(n => n.Y).ThenBy(n => n.Z).ToList();
        double ox = sirali[0].X, oy = sirali.Min(n => n.Y);
        var hucreler = new Dictionary<(long, long), List<int>>();
        var sonuc = new List<AraziDugum>();
        foreach (var n in sirali)
        {
            double gx = Math.Floor((n.X - ox) / TekillestirmeToleransiM);
            double gy = Math.Floor((n.Y - oy) / TekillestirmeToleransiM);
            if (!double.IsFinite(gx) || !double.IsFinite(gy) || gx > 9e18 || gy > 9e18)
                throw new ArgumentException("Arazi koordinat aralığı tekilleştirme hassasiyetini aşıyor.");
            long ix = (long)gx, iy = (long)gy;
            int yakin = -1;
            for (long x = ix - 1; x <= ix + 1; x++)
            for (long y = iy - 1; y <= iy + 1; y++)
            {
                if (!hucreler.TryGetValue((x, y), out var adaylar)) continue;
                foreach (int i in adaylar)
                {
                    var p = sonuc[i];
                    double dx = (n.X - p.X) / TekillestirmeToleransiM;
                    double dy = (n.Y - p.Y) / TekillestirmeToleransiM;
                    if (dx * dx + dy * dy > 1) continue;
                    if (Math.Abs(p.Z - n.Z) > KotUyumToleransiM)
                        throw new ArgumentException($"Aynı konumda çelişkili kotlar var (X={n.X:0.###}, Y={n.Y:0.###}; {p.Z:0.####} / {n.Z:0.####} m). Kot katmanlarını kontrol edin.");
                    yakin = i;
                }
            }
            if (yakin >= 0) continue;
            if (!hucreler.TryGetValue((ix, iy), out var liste)) hucreler[(ix, iy)] = liste = [];
            liste.Add(sonuc.Count);
            sonuc.Add(n);
        }
        return sonuc;
    }

    private IEnumerable<AraziDugum> Yogunlastir(AraziKonturu kontur)
    {
        var pts = kontur.Noktalar;
        foreach (var p in pts) NoktayiDogrula(new(p.X, p.Y, kontur.KotM));
        if (pts.Count == 0) yield break;
        if (KonturOrneklemeAraligiM <= 0)
        {
            foreach (var p in pts) yield return new(p.X, p.Y, kontur.KotM);
            yield break;
        }
        int son = kontur.Kapali ? pts.Count : pts.Count - 1;
        for (int i = 0; i < son; i++)
        {
            var a = pts[i]; var b = pts[(i + 1) % pts.Count];
            yield return new(a.X, a.Y, kontur.KotM);
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double len = double.Hypot(dx, dy);
            double adim = Math.Floor(len / KonturOrneklemeAraligiM);
            if (!double.IsFinite(adim) || adim > AzamiNoktaSayisi)
                throw new ArgumentException("Kontur örnekleme sayısı sınırı aşıyor. Birimi kontrol edin veya örnekleme aralığını artırın.");
            for (int j = 1; j <= (int)adim; j++)
            {
                double t = j * KonturOrneklemeAraligiM / len;
                if (t >= 1) break;
                yield return new(a.X + dx * t, a.Y + dy * t, kontur.KotM);
            }
        }
        if (!kontur.Kapali) yield return new(pts[^1].X, pts[^1].Y, kontur.KotM);
    }

    private void AyarlariDogrula()
    {
        if (!double.IsFinite(KonturOrneklemeAraligiM) ||
            !double.IsFinite(TekillestirmeToleransiM) || TekillestirmeToleransiM <= 0 ||
            !double.IsFinite(KotUyumToleransiM) || KotUyumToleransiM < 0 || AzamiNoktaSayisi < 3)
            throw new ArgumentException("TIN örnekleme ve tolerans ayarları geçersiz.");
    }
    private static void NoktayiDogrula(AraziDugum n)
    {
        if (!double.IsFinite(n.X) || !double.IsFinite(n.Y) || !double.IsFinite(n.Z))
            throw new ArgumentException("Arazi noktalarının X, Y ve kot değerleri sonlu olmalıdır.");
    }
}
