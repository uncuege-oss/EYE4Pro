using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Terrain.Models;

namespace Kst.Acoustics.Core.Terrain.Services;

public sealed record AraziTasarimOzeti(
    double PlanAlanM2,
    double MinKotM,
    double MaxKotM,
    double OrtalamaKotM,
    IReadOnlyList<Point2D> SinirPoligonu);

/// TIN'den başlangıç platformu ve kot önerisi üretir. Dışbükey sınır ölçüm kapsamıdır;
/// kadastro/parsel sınırı olarak yorumlanmaz.
public static class AraziTasarimServisi
{
    /// Alan ağırlıklı kot, yüzeyin yatay bir düzlemle geometrik net hacmini dengeler.
    /// Kabarma/sıkışma ve şevler bu öneriye dahil değildir.
    public static AraziTasarimOzeti Ozetle(AraziYuzeyi yuzey)
    {
        ArgumentNullException.ThrowIfNull(yuzey);
        if (yuzey.Ucgenler.Count == 0)
            throw new ArgumentException("Tasarım için en az üç doğrusal olmayan kot noktasıyla yüzey oluşturun.");
        double alan = 0, ortalama = 0, minKot = double.PositiveInfinity, maxKot = double.NegativeInfinity;
        var kullanilan = new HashSet<int>();
        foreach (var t in yuzey.Ucgenler)
        {
            if (t.A < 0 || t.B < 0 || t.C < 0 || t.A >= yuzey.Dugumler.Count ||
                t.B >= yuzey.Dugumler.Count || t.C >= yuzey.Dugumler.Count)
                throw new ArgumentException("Arazi yüzeyinde geçersiz üçgen bağlantısı var.");
            var a = yuzey.Dugumler[t.A]; var b = yuzey.Dugumler[t.B]; var c = yuzey.Dugumler[t.C];
            foreach (var p in new[] { a, b, c })
            {
                if (!double.IsFinite(p.X) || !double.IsFinite(p.Y) || !double.IsFinite(p.Z))
                    throw new ArgumentException("Arazi yüzeyi sonlu koordinatlardan oluşmalıdır.");
            }
            double uAlan = Math.Abs(Capraz(new(a.X, a.Y), new(b.X, b.Y), new(c.X, c.Y))) * 0.5;
            if (!double.IsFinite(uAlan)) throw new ArgumentException("Arazi yüzey alanı geçersiz.");
            if (uAlan <= 1e-12) continue;
            double yeniAlan = alan + uAlan;
            if (!double.IsFinite(yeniAlan)) throw new ArgumentException("Arazi yüzey alanı geçersiz.");
            double kot = a.Z / 3 + b.Z / 3 + c.Z / 3;
            ortalama = ortalama * (alan / yeniAlan) + kot * (uAlan / yeniAlan);
            alan = yeniAlan;
            minKot = Math.Min(minKot, Math.Min(a.Z, Math.Min(b.Z, c.Z)));
            maxKot = Math.Max(maxKot, Math.Max(a.Z, Math.Max(b.Z, c.Z)));
            kullanilan.Add(t.A); kullanilan.Add(t.B); kullanilan.Add(t.C);
        }
        if (alan <= 0 || !double.IsFinite(ortalama)) throw new ArgumentException("Arazi yüzeyinin geçerli plan alanı yok.");
        var sinir = PoligonYardimci.KonveksKabuk(kullanilan.Select(i => new Point2D(yuzey.Dugumler[i].X, yuzey.Dugumler[i].Y)).ToList());
        if (sinir.Count < 3) throw new ArgumentException("Arazi sınırı oluşturulamadı.");
        return new(alan, minKot, maxKot, ortalama, sinir);
    }

    public static TasarimYuzeyi PlatformOlustur(AraziYuzeyi yuzey, double hedefKotM, string ad = "Platform")
        => PlatformOlustur(yuzey, hedefKotM, Ozetle(yuzey).SinirPoligonu, ad);

    /// Çizilen platformu mevcut TIN kapsamına karşı doğrular. İlk köşe sona tekrar eklenmez.
    public static TasarimYuzeyi PlatformOlustur(AraziYuzeyi yuzey, double hedefKotM,
        IReadOnlyList<Point2D> sinir, string ad = "Platform")
    {
        ArgumentNullException.ThrowIfNull(yuzey);
        ArgumentNullException.ThrowIfNull(sinir);
        if (!double.IsFinite(hedefKotM)) throw new ArgumentException("Hedef kot sonlu bir sayı olmalıdır.");
        SiniriDogrula(sinir);
        foreach (var p in sinir)
            if (yuzey.KotSorgula(p.X, p.Y) is not double kot || !double.IsFinite(kot))
                throw new ArgumentException("Platform sınırı ölçülen arazi yüzeyinin dışında kalıyor.");
        return new TasarimYuzeyi
        {
            Bolgeler = [new TasarimBolgesi
            {
                Ad = string.IsNullOrWhiteSpace(ad) ? "Platform" : ad.Trim(),
                Tip = TasarimBolgeTipi.Platform,
                HedefKotM = hedefKotM,
                Poligon = sinir.ToList()
            }]
        };
    }

    /// Yalnız geometrik geçerlilik; TIN kapsamı ayrı kontrol edilir.
    public static void SiniriDogrula(IReadOnlyList<Point2D> sinir)
    {
        ArgumentNullException.ThrowIfNull(sinir);
        if (sinir.Count < 3 || sinir.Count > 10_000)
            throw new ArgumentException("Platform sınırı 3 ile 10000 köşe içermelidir.");
        const double mesafeToleransi = 1e-8;
        foreach (var p in sinir)
            if (!double.IsFinite(p.X) || !double.IsFinite(p.Y))
                throw new ArgumentException("Platform köşeleri sonlu koordinatlardan oluşmalıdır.");
        for (int i = 0; i < sinir.Count; i++)
        {
            for (int j = i + 1; j < sinir.Count; j++)
                if (double.Hypot(sinir[i].X - sinir[j].X, sinir[i].Y - sinir[j].Y) <= mesafeToleransi)
                    throw new ArgumentException("Platformda yinelenen köşe veya sıfır uzunluklu kenar var.");
            int sonraki = (i + 1) % sinir.Count;
            for (int j = i + 1; j < sinir.Count; j++)
            {
                int jSonraki = (j + 1) % sinir.Count;
                if (j == sonraki || jSonraki == i) continue;
                if (Kesisiyor(sinir[i], sinir[sonraki], sinir[j], sinir[jSonraki]))
                    throw new ArgumentException("Platform sınırı kendi kendini kesiyor veya kenarları çakışıyor.");
            }
            // Ardışık, geri katlanan aynı doğrultulu kenarlar da geçersizdir.
            var onceki = sinir[(i + sinir.Count - 1) % sinir.Count];
            var simdi = sinir[i]; var sonra = sinir[sonraki];
            if (Math.Abs(Capraz(onceki, simdi, sonra)) <= 1e-12 &&
                (onceki.X - simdi.X) * (sonra.X - simdi.X) + (onceki.Y - simdi.Y) * (sonra.Y - simdi.Y) > 0)
                throw new ArgumentException("Platform sınırında geri katlanan kenar var.");
        }
        double alan2 = 0;
        for (int i = 1; i + 1 < sinir.Count; i++) alan2 += Capraz(sinir[0], sinir[i], sinir[i + 1]);
        if (!double.IsFinite(alan2) || Math.Abs(alan2) <= 1e-8)
            throw new ArgumentException("Platform sınırının alanı sıfır veya geçersiz.");
    }

    private static double Capraz(Point2D a, Point2D b, Point2D c)
        => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    private static bool Kesisiyor(Point2D a, Point2D b, Point2D c, Point2D d)
    {
        const double eps = 1e-10;
        double ac = Capraz(a, b, c), ad = Capraz(a, b, d), ca = Capraz(c, d, a), cb = Capraz(c, d, b);
        static bool Arada(Point2D p, Point2D x, Point2D y)
            => p.X >= Math.Min(x.X, y.X) - eps && p.X <= Math.Max(x.X, y.X) + eps &&
               p.Y >= Math.Min(x.Y, y.Y) - eps && p.Y <= Math.Max(x.Y, y.Y) + eps;
        if (Math.Abs(ac) <= eps && Arada(c, a, b) || Math.Abs(ad) <= eps && Arada(d, a, b) ||
            Math.Abs(ca) <= eps && Arada(a, c, d) || Math.Abs(cb) <= eps && Arada(b, c, d)) return true;
        return (ac > 0) != (ad > 0) && (ca > 0) != (cb > 0);
    }
}
