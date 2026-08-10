using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Salon.Models;

namespace Kst.Acoustics.Core.Salon.Services;

public enum SalonKesitCizgiTipi
{
    Zemin,
    Tavan,
    Sahne,
    Seyirci,
    Balkon,
    Orkestra,
    Yansitici
}

/// <summary>Kesit koordinatında X=hat boyunca mesafe, Y=kot.</summary>
public sealed record SalonKesitCizgisi(
    SalonKesitCizgiTipi Tip,
    string Ad,
    IReadOnlyList<Point2D> Noktalar,
    string? KaynakId = null,
    bool Kapali = false);

public sealed record SalonKesitSonucu(
    bool Boyuna,
    double KesitKotu,
    IReadOnlyList<SalonKesitCizgisi> Cizgiler,
    double MinMesafeM,
    double MaksMesafeM,
    double MaksKotM);

/// <summary>
/// Planla canlı bağlı boyuna/enine kesit üretir. Boyuna kesit seyirci kademelerini,
/// sahneyi, balkonları, tavan profilini ve akustik bulutları aynı görünümde verir.
/// </summary>
public static class SalonKesitServisi
{
    public static SalonKesitSonucu BoyunaKesit(SalonGeometri salon, double x)
    {
        ArgumentNullException.ThrowIfNull(salon);
        var araliklar = KabukAraliklari(salon, x, boyuna: true);
        var cizgiler = new List<SalonKesitCizgisi>();
        foreach (var (a, b) in araliklar)
        {
            cizgiler.Add(new SalonKesitCizgisi(SalonKesitCizgiTipi.Zemin, "Zemin",
                [new Point2D(a, 0), new Point2D(b, 0)]));
            cizgiler.Add(new SalonKesitCizgisi(SalonKesitCizgiTipi.Tavan, "Tavan",
                TavanCizgisi(salon, a, b)));
        }

        if (salon.Sahne is { } sahne && KeserX(sahne.TabanPoligonu(Salon3DGorunumServisi.Faset(salon)), x, out var sahneY))
            foreach (var (a, b) in sahneY)
                cizgiler.Add(Dikdortgen(SalonKesitCizgiTipi.Sahne, "Sahne", a, b,
                    sahne.TabanKotuM, sahne.TabanKotuM + sahne.YukseklikM));

        foreach (var s in salon.SeyirciSekciyonlari)
            if (x >= s.MerkezX - s.GenislikM / 2 && x <= s.MerkezX + s.GenislikM / 2)
                cizgiler.Add(new SalonKesitCizgisi(SalonKesitCizgiTipi.Seyirci, s.Ad,
                    SeyirciProfili(s), s.Id));

        foreach (var b in salon.Balkonlar)
            if (x >= b.MerkezX - b.GenislikM / 2 && x <= b.MerkezX + b.GenislikM / 2)
                cizgiler.Add(Dikdortgen(SalonKesitCizgiTipi.Balkon, b.Ad,
                    b.MerkezY - b.DerinlikM / 2, b.MerkezY + b.DerinlikM / 2,
                    b.TabanKotuM - b.KalinlikM, b.TabanKotuM + b.ParapetM, b.Id));

        if (salon.OrkestraCukuru is { } o &&
            x >= o.MerkezX - o.GenislikM / 2 && x <= o.MerkezX + o.GenislikM / 2)
            cizgiler.Add(Dikdortgen(SalonKesitCizgiTipi.Orkestra, "Orkestra çukuru",
                o.MerkezY - o.DerinlikM / 2, o.MerkezY + o.DerinlikM / 2,
                -o.CukurDerinligiM, 0));

        foreach (var p in salon.YansiticiPaneller.Where(p => PanelGorunur(salon, p)))
        {
            var (noktalar, kapali) = YansiticiKesiti(p, x, boyuna: true);
            if (noktalar.Count >= 2)
                cizgiler.Add(new SalonKesitCizgisi(
                    SalonKesitCizgiTipi.Yansitici, p.Ad, noktalar, p.Id, kapali));
        }

        return Sonuc(true, x, cizgiler, araliklar);
    }

    public static SalonKesitSonucu EnineKesit(SalonGeometri salon, double y)
    {
        ArgumentNullException.ThrowIfNull(salon);
        var araliklar = KabukAraliklari(salon, y, boyuna: false);
        var cizgiler = new List<SalonKesitCizgisi>();
        foreach (var (a, b) in araliklar)
        {
            cizgiler.Add(new SalonKesitCizgisi(SalonKesitCizgiTipi.Zemin, "Zemin",
                [new Point2D(a, 0), new Point2D(b, 0)]));
            double z = SalonTavanServisi.Kot(salon, y);
            cizgiler.Add(new SalonKesitCizgisi(SalonKesitCizgiTipi.Tavan, "Tavan",
                [new Point2D(a, z), new Point2D(b, z)]));
        }

        if (salon.Sahne is { } sahne && KeserY(sahne.TabanPoligonu(Salon3DGorunumServisi.Faset(salon)), y, out var sx))
            foreach (var (a, b) in sx)
                cizgiler.Add(Dikdortgen(SalonKesitCizgiTipi.Sahne, "Sahne", a, b,
                    sahne.TabanKotuM, sahne.TabanKotuM + sahne.YukseklikM));

        foreach (var s in salon.SeyirciSekciyonlari)
        {
            double yMin = s.MerkezY - s.DerinlikM / 2, yMax = s.MerkezY + s.DerinlikM / 2;
            if (y < yMin || y > yMax) continue;
            int kademe = KademeIndeksi(s, y);
            double z = s.TabanKotuM + kademe * Math.Max(0, s.KademeYuksekligiM);
            cizgiler.Add(new SalonKesitCizgisi(SalonKesitCizgiTipi.Seyirci, s.Ad,
                [new Point2D(s.MerkezX - s.GenislikM / 2, z),
                 new Point2D(s.MerkezX + s.GenislikM / 2, z)], s.Id));
        }

        foreach (var b in salon.Balkonlar)
            if (y >= b.MerkezY - b.DerinlikM / 2 && y <= b.MerkezY + b.DerinlikM / 2)
                cizgiler.Add(Dikdortgen(SalonKesitCizgiTipi.Balkon, b.Ad,
                    b.MerkezX - b.GenislikM / 2, b.MerkezX + b.GenislikM / 2,
                    b.TabanKotuM - b.KalinlikM, b.TabanKotuM + b.ParapetM, b.Id));

        foreach (var p in salon.YansiticiPaneller.Where(p => PanelGorunur(salon, p)))
        {
            var (noktalar, kapali) = YansiticiKesiti(p, y, boyuna: false);
            if (noktalar.Count >= 2)
                cizgiler.Add(new SalonKesitCizgisi(
                    SalonKesitCizgiTipi.Yansitici, p.Ad, noktalar, p.Id, kapali));
        }

        return Sonuc(false, y, cizgiler, araliklar);
    }

    /// Kesit editöründe sürüklenen tavan kontrol noktalarını ana modele uygular.
    public static void TavanProfiliniGuncelle(SalonGeometri salon, IEnumerable<Point2D> mesafeKot)
    {
        ArgumentNullException.ThrowIfNull(salon);
        ArgumentNullException.ThrowIfNull(mesafeKot);
        var temiz = mesafeKot
            .Where(p => double.IsFinite(p.X) && double.IsFinite(p.Y) && p.Y > 0)
            .OrderBy(p => p.X)
            .GroupBy(p => Math.Round(p.X, 6))
            .Select(g => g.Last())
            .ToList();
        if (temiz.Count == 1)
            throw new ArgumentException("Tavan profili düz tavan için boş, profilli tavan için en az iki nokta olmalıdır.", nameof(mesafeKot));
        salon.TavanProfili.Clear();
        foreach (var p in temiz) salon.TavanProfili.Add(new TavanKirilimNoktasi { YM = p.X, KotM = p.Y });
    }

    private static SalonKesitSonucu Sonuc(bool boyuna, double kot,
        List<SalonKesitCizgisi> cizgiler, List<(double A, double B)> araliklar)
    {
        double min = araliklar.Count == 0 ? 0 : araliklar.Min(x => x.A);
        double max = araliklar.Count == 0 ? 0 : araliklar.Max(x => x.B);
        double zmax = cizgiler.SelectMany(x => x.Noktalar).DefaultIfEmpty().Max(p => p.Y);
        return new SalonKesitSonucu(boyuna, kot, cizgiler, min, max, zmax);
    }

    private static SalonKesitCizgisi Dikdortgen(SalonKesitCizgiTipi tip, string ad,
        double a, double b, double zAlt, double zUst, string? id = null) =>
        new(tip, ad,
            [new Point2D(a, zAlt), new Point2D(b, zAlt), new Point2D(b, zUst), new Point2D(a, zUst)],
            id, true);

    private static List<Point2D> TavanCizgisi(SalonGeometri salon, double a, double b)
    {
        var ys = new List<double> { a, b };
        ys.AddRange(salon.TavanProfili.Where(p => p.YM > a && p.YM < b).Select(p => p.YM));
        return ys.Distinct().Order().Select(y => new Point2D(y, SalonTavanServisi.Kot(salon, y))).ToList();
    }

    private static List<Point2D> SeyirciProfili(SeyirciSekciyonu s)
    {
        int n = Math.Max(1, s.KademeSayisi);
        double yOn = s.TersYon ? s.MerkezY + s.DerinlikM / 2 : s.MerkezY - s.DerinlikM / 2;
        double yon = s.TersYon ? -1 : 1;
        double d = s.DerinlikM / n;
        var p = new List<Point2D>();
        for (int i = 0; i < n; i++)
        {
            double y1 = yOn + yon * i * d, y2 = yOn + yon * (i + 1) * d;
            double z = s.TabanKotuM + i * Math.Max(0, s.KademeYuksekligiM);
            p.Add(new Point2D(y1, z));
            p.Add(new Point2D(y2, z));
        }
        return p;
    }

    private static int KademeIndeksi(SeyirciSekciyonu s, double y)
    {
        int n = Math.Max(1, s.KademeSayisi);
        double on = s.TersYon ? s.MerkezY + s.DerinlikM / 2 : s.MerkezY - s.DerinlikM / 2;
        double d = s.TersYon ? on - y : y - on;
        return Math.Clamp((int)Math.Floor(d / Math.Max(1e-9, s.DerinlikM) * n), 0, n - 1);
    }

    private static bool PanelGorunur(SalonGeometri salon, SalonYansiticiPanel panel)
    {
        if (!panel.Gorunur) return false;
        var katman = salon.Katmanlar.FirstOrDefault(k => k.Id == panel.KatmanId)
                      ?? salon.Katmanlar.FirstOrDefault(k => k.Tip == SalonKatmanTipi.Yansitici);
        return katman is not { Gorunur: false };
    }

    /// Panelin gerçek döndürülmüş dörtgenini düşey kesit düzlemiyle keser. Böylece
    /// eğimde hem yatay izdüşüm kısalması hem birleşik X/Y dönüşleri doğru kalır.
    private static (List<Point2D> Noktalar, bool Kapali) YansiticiKesiti(
        SalonYansiticiPanel panel, double sabit, bool boyuna)
    {
        var v = SalonYansiticiServisi.YuzeyOlustur(panel, 0).Vertices;
        const double eps = 1e-8;
        double SabitK(Kst.Acoustics.Core.Geometry.Point3D p) => boyuna ? p.X : p.Y;
        Point2D Harita(Kst.Acoustics.Core.Geometry.Point3D p) =>
            new(boyuna ? p.Y : p.X, p.Z);
        var sonuc = new List<Point2D>();

        void Ekle(Point2D p)
        {
            if (sonuc.All(q => Math.Abs(q.X - p.X) > eps || Math.Abs(q.Y - p.Y) > eps))
                sonuc.Add(p);
        }

        for (int i = 0; i < v.Count; i++)
        {
            var a = v[i]; var b = v[(i + 1) % v.Count];
            double da = SabitK(a) - sabit, db = SabitK(b) - sabit;
            if (Math.Abs(da) <= eps) Ekle(Harita(a));
            if ((da < -eps && db > eps) || (da > eps && db < -eps))
            {
                double t = -da / (db - da);
                Ekle(new Point2D(
                    (boyuna ? a.Y : a.X) + t * ((boyuna ? b.Y : b.X) - (boyuna ? a.Y : a.X)),
                    a.Z + t * (b.Z - a.Z)));
            }
        }

        if (sonuc.Count <= 2)
        {
            sonuc.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
            return (sonuc, false);
        }

        // Panel kesit düzlemiyle çakışıksa dört köşenin açısal sırasını koru.
        double cx = sonuc.Average(p => p.X), cy = sonuc.Average(p => p.Y);
        sonuc = sonuc.OrderBy(p => Math.Atan2(p.Y - cy, p.X - cx)).ToList();
        return (sonuc, true);
    }

    private static List<(double A, double B)> KabukAraliklari(SalonGeometri salon, double sabit, bool boyuna)
    {
        var tum = new List<(double A, double B)>();
        foreach (var oge in salon.CizimOgeleri)
        {
            var katman = salon.Katmanlar.FirstOrDefault(k => k.Id == oge.KatmanId);
            if (katman is { Gorunur: false } || katman is not null && katman.Tip != SalonKatmanTipi.Zemin) continue;
            var p = oge.KapaliPoligon(Salon3DGorunumServisi.Faset(salon));
            if (boyuna)
            {
                if (KeserX(p, sabit, out var a)) tum.AddRange(a);
            }
            else if (KeserY(p, sabit, out var a)) tum.AddRange(a);
        }
        return Birlestir(tum);
    }

    private static bool KeserX(IReadOnlyList<Point2D> p, double x, out List<(double A, double B)> araliklar)
        => Keser(p, x, q => q.X, q => q.Y, out araliklar);

    private static bool KeserY(IReadOnlyList<Point2D> p, double y, out List<(double A, double B)> araliklar)
        => Keser(p, y, q => q.Y, q => q.X, out araliklar);

    private static bool Keser(IReadOnlyList<Point2D> p, double sabit,
        Func<Point2D, double> fixedAxis, Func<Point2D, double> resultAxis,
        out List<(double A, double B)> araliklar)
    {
        var hits = new List<double>();
        for (int i = 0; i < p.Count; i++)
        {
            var a = p[i]; var b = p[(i + 1) % p.Count];
            double fa = fixedAxis(a), fb = fixedAxis(b);
            if ((fa <= sabit && fb > sabit) || (fb <= sabit && fa > sabit))
            {
                double t = (sabit - fa) / (fb - fa);
                hits.Add(resultAxis(a) + t * (resultAxis(b) - resultAxis(a)));
            }
        }
        hits.Sort();
        araliklar = new List<(double, double)>();
        for (int i = 0; i + 1 < hits.Count; i += 2)
            if (hits[i + 1] - hits[i] > 1e-8) araliklar.Add((hits[i], hits[i + 1]));
        return araliklar.Count > 0;
    }

    private static List<(double A, double B)> Birlestir(List<(double A, double B)> a)
    {
        var s = a.OrderBy(x => x.A).ToList();
        var r = new List<(double, double)>();
        foreach (var x in s)
        {
            if (r.Count == 0 || x.A > r[^1].Item2 + 1e-8) r.Add(x);
            else r[^1] = (r[^1].Item1, Math.Max(r[^1].Item2, x.B));
        }
        return r;
    }
}
