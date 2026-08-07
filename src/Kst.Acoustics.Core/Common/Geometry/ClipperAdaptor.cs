using System;
using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;

namespace Kst.Acoustics.Core.Common.Geometry;

/// Clipper2 (Angus Johnson) ile 2D poligon boole işlemleri için ince adaptör.
/// **Tek sorumluluk:** metre↔integer ölçek dönüşümünü TEK noktada yapmak. Clipper
/// tamsayı koordinatla çalışır; girişte metre × <see cref="Olcek"/> ile ölçeklenir,
/// çıkışta geri bölünür. Böylece birim duyarlılığı tuzağı (krş. M01 dersi) izole
/// edilir. M70 park sınırı − (bina + setback) hesabı bunu kullanır (codex70 §6).
public static class ClipperAdaptor
{
    /// metre → integer çarpanı. 1000 = mm hassasiyeti.
    private const double Olcek = 1000.0;

    /// Poligonu `mesafeM` kadar dışa şişirir (yapı yaklaşma / setback offset).
    /// Köşeler dik tutulur (Miter). Negatif mesafe içe büzer. Sonuç birden çok
    /// halka olabilir; CCW dış yönelimi garanti edilir (offset doğru yönde olur).
    public static List<List<Point2D>> Tampon(IReadOnlyList<Point2D> poligon, double mesafeM)
    {
        var path = YoneAyarli(poligon);
        var girdi = new Paths64 { ToPath(path) };
        var sonuc = Clipper.InflatePaths(girdi, mesafeM * Olcek, JoinType.Miter, EndType.Polygon);
        return FromPaths(sonuc);
    }

    /// Poligonları birleştirir (union) — örtüşen bina+setback tamponları için.
    public static List<List<Point2D>> Birlestir(IEnumerable<IReadOnlyList<Point2D>> poligonlar)
    {
        var girdi = ToPaths(poligonlar);
        if (girdi.Count == 0) return new();
        var sonuc = Clipper.Union(girdi, FillRule.NonZero);
        return FromPaths(sonuc);
    }

    /// `ana` poligondan `cikarilanlar`'ı çıkarır (difference). Sonuç delikli/parçalı
    /// olabilir — dış halkalar + delikler ayrı halkalar olarak döner.
    public static List<List<Point2D>> Fark(
        IReadOnlyList<Point2D> ana, IEnumerable<IReadOnlyList<Point2D>> cikarilanlar)
        => Fark(new[] { ana }, cikarilanlar);

    /// Çoklu-özne fark: `analar` halka kümesinden (delikli olabilir, NonZero)
    /// `cikarilanlar`'ı çıkarır. M51 zemin kırpma ve keyhole ayrıştırma kullanır.
    public static List<List<Point2D>> Fark(
        IEnumerable<IReadOnlyList<Point2D>> analar, IEnumerable<IReadOnlyList<Point2D>> cikarilanlar)
    {
        var subj = ToPaths(analar);
        if (subj.Count == 0) return new();
        var clip = ToPaths(cikarilanlar);
        if (clip.Count == 0) return FromPaths(subj);
        var sonuc = Clipper.Difference(subj, clip, FillRule.NonZero);
        return FromPaths(sonuc);
    }

    /// İki halka kümesinin kesişimi (intersection). M51 hacim düşümü ve
    /// "footprint dış hat içinde mi" doğrulaması kullanır.
    public static List<List<Point2D>> Kesisim(
        IEnumerable<IReadOnlyList<Point2D>> a, IEnumerable<IReadOnlyList<Point2D>> b)
    {
        var subj = ToPaths(a);
        var clip = ToPaths(b);
        if (subj.Count == 0 || clip.Count == 0) return new();
        var sonuc = Clipper.Intersect(subj, clip, FillRule.NonZero);
        return FromPaths(sonuc);
    }

    /// Halka kümesinin net alanı (m²). Dış halkalar artı, delikler eksi yönelimli
    /// olduğundan işaretli alanların toplamının mutlak değeri doğru dolu alanı verir.
    public static double NetAlan(IEnumerable<IReadOnlyList<Point2D>> halkalar)
        => Math.Abs(halkalar.Sum(IsaretliAlan));

    /// İşaretli poligon alanı (Shoelace, mutlak değer ALMADAN). CCW > 0, CW < 0.
    public static double IsaretliAlan(IReadOnlyList<Point2D> poly)
    {
        if (poly.Count < 3) return 0;
        double a = 0;
        for (int i = 0; i < poly.Count; i++)
        {
            int j = (i + 1) % poly.Count;
            a += poly[i].X * poly[j].Y - poly[j].X * poly[i].Y;
        }
        return a / 2.0;
    }

    // CCW (pozitif alan) yönelimi sağla — offset'in dışa olması için.
    private static IReadOnlyList<Point2D> YoneAyarli(IReadOnlyList<Point2D> poly)
        => IsaretliAlan(poly) < 0 ? poly.Reverse().ToList() : poly;

    private static Path64 ToPath(IReadOnlyList<Point2D> p)
        => new(p.Select(q => new Point64(
            (long)Math.Round(q.X * Olcek), (long)Math.Round(q.Y * Olcek))));

    private static Paths64 ToPaths(IEnumerable<IReadOnlyList<Point2D>> ps)
        => new(ps.Select(ToPath));

    private static List<Point2D> FromPath(Path64 path)
        => path.Select(pt => new Point2D(pt.X / Olcek, pt.Y / Olcek)).ToList();

    private static List<List<Point2D>> FromPaths(Paths64 paths)
        => paths.Select(FromPath).ToList();
}
