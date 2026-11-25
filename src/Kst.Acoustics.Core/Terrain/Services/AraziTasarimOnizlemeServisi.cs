using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Terrain.Models;
using TriangleNet.Geometry;
using TriangleNet.Meshing;

namespace Kst.Acoustics.Core.Terrain.Services;

public sealed class AraziTasarimOnizlemesi
{
    public List<ProjectSurface> Yuzeyler { get; } = new();
    public double OrneklemeAraligiM { get; init; }
    public List<string> Uyarilar { get; } = new();
}

/// <summary>
/// Ölçülen TIN'i platform sınırlarına kırpar; düz platformları ve dik kenarları tam sınırda,
/// şevleri belirtilen azami örnekleme aralığında gösterir. Hacim hesabı yerine kullanılmaz.
/// </summary>
public static class AraziTasarimOnizlemeServisi
{
    private const int AzamiYuzey = 600_000;
    private readonly record struct Kutu(double MinX, double MinY, double MaxX, double MaxY)
    {
        public bool Kesisir(Kutu b) => MinX <= b.MaxX && MaxX >= b.MinX && MinY <= b.MaxY && MaxY >= b.MinY;
        public Kutu Genislet(double m) => new(MinX - m, MinY - m, MaxX + m, MaxY + m);
    }

    public static AraziTasarimOnizlemesi Olustur(AraziYuzeyi mevcut, TasarimYuzeyi tasarim,
        double orneklemeAraligiM = 2)
    {
        ArgumentNullException.ThrowIfNull(mevcut);
        ArgumentNullException.ThrowIfNull(tasarim);
        if (!double.IsFinite(orneklemeAraligiM) || orneklemeAraligiM <= 0)
            throw new ArgumentOutOfRangeException(nameof(orneklemeAraligiM), "Önizleme aralığı pozitif ve sonlu olmalı.");
        if (mevcut.Ucgenler.Count > AzamiYuzey)
            throw new ArgumentException("Bu arazi önizleme yüzey sınırını aşıyor.");
        var servis = new AraziTesviyeServisi(mevcut, tasarim);
        var kotIndeksi = new AraziKotIndeksi(mevcut);
        var bolgeler = tasarim.Bolgeler;
        var platformKutulari = bolgeler.Select(b => Sinir(b.Poligon)).ToArray();
        var etkiKutulari = bolgeler.Select((b, i) => platformKutulari[i].Genislet(
            Enumerable.Range(0, b.Poligon.Count).Any(j => b.KenarTipi(j) == TasarimKenarTipi.Sev) ? b.SevAzamiMesafeM : 0)).ToArray();
        double alanTahmini = etkiKutulari.Sum(b => Math.Max(0, b.MaxX - b.MinX) * Math.Max(0, b.MaxY - b.MinY));
        double aralik = Math.Max(orneklemeAraligiM, Math.Sqrt(alanTahmini / 50_000));
        var sonuc = new AraziTasarimOnizlemesi { OrneklemeAraligiM = aralik };
        if (aralik > orneklemeAraligiM * 1.001)
            sonuc.Uyarilar.Add($"Büyük tasarım kapsamı için şev önizleme aralığı {aralik:0.##} m olarak ayarlandı. Hesap ızgarası değişmedi.");
        int id = 1;
        var platformAlanlari = bolgeler.ToDictionary(b => b.Id, _ => 0d);
        // Ortak TIN köşelerindeki şev ışınlarını her komşu üçgen için yeniden çözme.
        var kotOnbellegi = new Dictionary<(double X, double Y), TesviyeNoktasi?>();
        TesviyeNoktasi? Sorgula(Point2D p)
        {
            var key = (p.X, p.Y);
            if (kotOnbellegi.TryGetValue(key, out var bulunan)) return bulunan;
            var nokta = servis.Sorgula(p.X, p.Y);
            if (kotOnbellegi.Count < 300_000) kotOnbellegi.Add(key, nokta);
            return nokta;
        }
        void Ekle(string ad, string kategori, IReadOnlyList<Point3D> noktalar, string? sahip = null)
        {
            if (sonuc.Yuzeyler.Count >= AzamiYuzey)
                throw new InvalidOperationException("Tasarım önizlemesi çok ayrıntılı. Önizleme aralığını artırın veya bölgeleri daraltın.");
            if (noktalar.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y) || !double.IsFinite(p.Z)))
                throw new InvalidOperationException("Önizleme yüzeyinde geçersiz koordinat oluştu.");
            sonuc.Yuzeyler.Add(new(id++, ad, kategori, noktalar, sahip));
        }

        foreach (var t in mevcut.Ucgenler)
        {
            var a = mevcut.Dugumler[t.A]; var b = mevcut.Dugumler[t.B]; var c = mevcut.Dugumler[t.C];
            var tri = new List<Point2D> { new(a.X, a.Y), new(b.X, b.Y), new(c.X, c.Y) };
            var kutu = Sinir(tri);
            if (!etkiKutulari.Any(k => k.Kesisir(kutu)))
            {
                Ekle("Mevcut arazi", Arazi3DGorunumServisi.KategoriArazi,
                    new[] { new Point3D(a.X, a.Y, a.Z), new Point3D(b.X, b.Y, b.Z), new Point3D(c.X, c.Y, c.Z) });
                continue;
            }
            var yakinBolgeler = bolgeler.Where((_, i) => platformKutulari[i].Kesisir(kutu)).ToList();
            var kesiciler = yakinBolgeler.Select(p => (IReadOnlyList<Point2D>)p.Poligon).ToList();
            foreach (var bolge in yakinBolgeler)
                foreach (var pt in Ucgenle(ClipperAdaptor.Kesisim(new[] { tri }, new[] { bolge.Poligon })))
                {
                    platformAlanlari[bolge.Id] += Math.Abs(Alan2(new[] { pt.A, pt.B, pt.C })) / 2;
                    Ekle(bolge.Ad, Arazi3DGorunumServisi.KategoriPlatform,
                        new[] { new Point3D(pt.A.X, pt.A.Y, bolge.HedefKotM), new Point3D(pt.B.X, pt.B.Y, bolge.HedefKotM),
                            new Point3D(pt.C.X, pt.C.Y, bolge.HedefKotM) }, bolge.Id);
                }
            var kalan = kesiciler.Count == 0 ? new List<List<Point2D>> { tri }
                : ClipperAdaptor.Fark(tri, kesiciler);
            foreach (var dis in Ucgenle(kalan))
                foreach (var parca in Bol(dis.A, dis.B, dis.C, aralik))
                {
                    var merkez = new Point2D((parca.A.X + parca.B.X + parca.C.X) / 3,
                        (parca.A.Y + parca.B.Y + parca.C.Y) / 3);
                    TasarimBolgesi? sahip = null;
                    Point3D Kot(Point2D p)
                    {
                        // Kenarda platform içi yerine bu dış yüzün limitini sorgula: dik kesimde köprü oluşmaz.
                        var hedef = Sorgula(p);
                        for (double pay = 1e-5; hedef?.Tur == TesviyeTur.Platform && pay <= 1.001; pay *= 10)
                        {
                            var q = new Point2D(p.X + (merkez.X - p.X) * pay, p.Y + (merkez.Y - p.Y) * pay);
                            hedef = servis.Sorgula(q.X, q.Y);
                        }
                        if (hedef?.Tur == TesviyeTur.Platform) hedef = null;
                        if (hedef is not null) sahip ??= hedef.Bolge;
                        double z = hedef?.HedefKotM ?? DuzlemKotu(p, a, b, c);
                        return new(p.X, p.Y, z);
                    }
                    var noktalar = new[] { Kot(parca.A), Kot(parca.B), Kot(parca.C) };
                    Ekle(sahip is null ? "Mevcut arazi" : $"{sahip.Ad} — şev",
                        Arazi3DGorunumServisi.KategoriArazi, noktalar, sahip?.Id);
                }
        }

        // Platformlar kendi poligonlarına ve ölçülen TIN'e kırpılır; kapsam dışına zemin uydurulmaz.
        foreach (var bolge in bolgeler)
        {
            double alan = Math.Abs(Alan2(bolge.Poligon)) / 2;
            if (platformAlanlari[bolge.Id] < alan - Math.Max(.02, alan * 1e-5))
                sonuc.Uyarilar.Add($"{bolge.Ad}: platformun bir kısmı ölçülen arazi kapsamı dışında; yalnız TIN ile örtüşen bölüm gösterildi.");
        }

        // Dik kesim bir limit yüzeyidir; zemine doğru eğik bir üçgenle yumuşatılmaz.
        var duvarAnahtarlari = new HashSet<string>();
        var bolgeSirasi = bolgeler.Select((b, i) => (b.Id, i)).ToDictionary(p => p.Id, p => p.i);
        foreach (var bolge in bolgeler)
        {
            double yon = Alan2(bolge.Poligon) >= 0 ? 1 : -1;
            for (int i = 0; i < bolge.Poligon.Count; i++)
            {
                if (bolge.KenarTipi(i) != TasarimKenarTipi.DikKesim) continue;
                var a = bolge.Poligon[i]; var b = bolge.Poligon[(i + 1) % bolge.Poligon.Count];
                double dx = b.X - a.X, dy = b.Y - a.Y, uzunluk = double.Hypot(dx, dy);
                if (uzunluk <= 1e-9) continue;
                double ox = yon * dy / uzunluk, oy = -yon * dx / uzunluk;
                int sayi = Math.Max(1, (int)Math.Ceiling(uzunluk / aralik));
                var parametreler = Enumerable.Range(0, sayi + 1).Select(j => (double)j / sayi).ToList();
                // Birbirine değen bölge köşelerinde de duvarı böl; komşu kot değişimi atlanmasın.
                foreach (var p in bolgeler.SelectMany(r => r.Poligon))
                {
                    double u = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (uzunluk * uzunluk);
                    if (u > 1e-9 && u < 1 - 1e-9 && Math.Abs((p.X - a.X) * dy - (p.Y - a.Y) * dx) / uzunluk < 1e-6)
                        parametreler.Add(u);
                }
                parametreler = parametreler.Distinct().Order().ToList();
                for (int j = 0; j + 1 < parametreler.Count; j++)
                {
                    double u0 = parametreler[j], u1 = parametreler[j + 1];
                    var p = new Point2D(a.X + dx * u0, a.Y + dy * u0);
                    var q = new Point2D(a.X + dx * u1, a.Y + dy * u1);
                    const double eps = .0001;
                    var komsu = servis.Sorgula((p.X + q.X) / 2 + ox * eps, (p.Y + q.Y) / 2 + oy * eps);
                    if (komsu?.Tur == TesviyeTur.Platform && bolgeSirasi[komsu.Bolge.Id] < bolgeSirasi[bolge.Id]) continue;
                    double? DisKot(Point2D n, double u) =>
                        servis.KotSorgula(n.X + ox * eps + dx * u, n.Y + oy * eps + dy * u) ?? kotIndeksi.KotSorgula(n.X, n.Y);
                    double? zp = DisKot(p, (u1 - u0) * 1e-5), zq = DisKot(q, -(u1 - u0) * 1e-5);
                    if (!zp.HasValue || !zq.HasValue) continue;
                    if (Math.Abs(zp.Value - bolge.HedefKotM) < 1e-6 && Math.Abs(zq.Value - bolge.HedefKotM) < 1e-6) continue;
                    var k0 = FormattableString.Invariant($"{p.X:0.#####},{p.Y:0.#####},{Math.Min(zp.Value, bolge.HedefKotM):0.###},{Math.Max(zp.Value, bolge.HedefKotM):0.###}");
                    var k1 = FormattableString.Invariant($"{q.X:0.#####},{q.Y:0.#####},{Math.Min(zq.Value, bolge.HedefKotM):0.###},{Math.Max(zq.Value, bolge.HedefKotM):0.###}");
                    string key = string.CompareOrdinal(k0, k1) <= 0 ? k0 + ";" + k1 : k1 + ";" + k0;
                    if (!duvarAnahtarlari.Add(key)) continue;
                    Ekle($"{bolge.Ad} — dik kesim", Arazi3DGorunumServisi.KategoriPlatform,
                        new[] { new Point3D(p.X, p.Y, bolge.HedefKotM), new Point3D(q.X, q.Y, bolge.HedefKotM),
                            new Point3D(q.X, q.Y, zq.Value), new Point3D(p.X, p.Y, zp.Value) }, bolge.Id);
                }
            }
        }
        sonuc.Uyarilar.AddRange(servis.Uyarilar);
        return sonuc;
    }

    private static Kutu Sinir(IReadOnlyList<Point2D> points) => new(points.Min(p => p.X), points.Min(p => p.Y), points.Max(p => p.X), points.Max(p => p.Y));
    private static double Alan2(IReadOnlyList<Point2D> p)
    {
        if (p.Count < 3) return 0;
        double a = 0; var o = p[0];
        for (int i = 1; i + 1 < p.Count; i++) a += (p[i].X - o.X) * (p[i + 1].Y - o.Y) - (p[i + 1].X - o.X) * (p[i].Y - o.Y);
        return a;
    }
    private static double DuzlemKotu(Point2D p, AraziDugum a, AraziDugum b, AraziDugum c)
    {
        double den = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
        if (Math.Abs(den) < 1e-15) return a.Z;
        double wa = ((b.Y - c.Y) * (p.X - c.X) + (c.X - b.X) * (p.Y - c.Y)) / den;
        double wb = ((c.Y - a.Y) * (p.X - c.X) + (a.X - c.X) * (p.Y - c.Y)) / den;
        return wa * a.Z + wb * b.Z + (1 - wa - wb) * c.Z;
    }

    private static IEnumerable<(Point2D A, Point2D B, Point2D C)> Ucgenle(IEnumerable<IReadOnlyList<Point2D>> halkalar)
    {
        var valid = halkalar.Where(p => p.Count >= 3 && Math.Abs(Alan2(p)) > 1e-9).ToList();
        if (valid.Count == 0) yield break;
        if (valid.Count == 1 && valid[0].Count == 3) { yield return (valid[0][0], valid[0][1], valid[0][2]); yield break; }
        double ox = valid[0][0].X, oy = valid[0][0].Y;
        var polygon = new Polygon();
        foreach (var ring in valid)
            polygon.Add(new Contour(ring.Select(p => new Vertex(p.X - ox, p.Y - oy))), valid.Count > 1 && Alan2(ring) < 0);
        var mesh = new GenericMesher().Triangulate(polygon);
        foreach (var t in mesh.Triangles)
        {
            var a = t.GetVertex(0); var b = t.GetVertex(1); var c = t.GetVertex(2);
            yield return (new(a.X + ox, a.Y + oy), new(b.X + ox, b.Y + oy), new(c.X + ox, c.Y + oy));
        }
    }

    private static IEnumerable<(Point2D A, Point2D B, Point2D C)> Bol(Point2D a, Point2D b, Point2D c, double aralik)
    {
        var yigin = new Stack<(Point2D A, Point2D B, Point2D C, int Derinlik)>();
        yigin.Push((a, b, c, 0));
        while (yigin.Count > 0)
        {
            var t = yigin.Pop();
            double ab = double.Hypot(t.A.X - t.B.X, t.A.Y - t.B.Y), bc = double.Hypot(t.B.X - t.C.X, t.B.Y - t.C.Y), ca = double.Hypot(t.C.X - t.A.X, t.C.Y - t.A.Y);
            if (Math.Max(ab, Math.Max(bc, ca)) <= aralik * 1.001) { yield return (t.A, t.B, t.C); continue; }
            if (t.Derinlik >= 32) throw new InvalidOperationException("Önizleme örnekleme aralığı koordinat aralığına göre çok küçük.");
            if (bc >= ab && bc >= ca) t = (t.B, t.C, t.A, t.Derinlik);
            else if (ca >= ab && ca >= bc) t = (t.C, t.A, t.B, t.Derinlik);
            var m = new Point2D((t.A.X + t.B.X) / 2, (t.A.Y + t.B.Y) / 2);
            yigin.Push((t.A, m, t.C, t.Derinlik + 1)); yigin.Push((m, t.B, t.C, t.Derinlik + 1));
        }
    }
}
