using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Salon.Models;

namespace Kst.Acoustics.Core.Salon.Services;

public sealed record SalonYansimaHatti(
    Point3D Kaynak,
    Point3D YansimaNoktasi,
    Point3D Hedef,
    double ToplamYolM,
    bool PanelIcinde);

/// <summary>
/// Akustik bulut/reflektör yüzeylerini üretir, sahne→panel→seyirci birinci yansıma
/// yolunu çözer ve paneli geliş/gidiş açıortayına otomatik yönlendirir.
/// </summary>
public static class SalonYansiticiServisi
{
    public static ProjectSurface YuzeyOlustur(
        SalonYansiticiPanel panel, int id, string? kategori = null)
    {
        ArgumentNullException.ThrowIfNull(panel);
        if (!double.IsFinite(panel.GenislikM) || !double.IsFinite(panel.DerinlikM) ||
            panel.GenislikM <= 0 || panel.DerinlikM <= 0)
            throw new ArgumentOutOfRangeException(nameof(panel), "Panel ölçüleri pozitif olmalıdır.");
        if (!Sonlu(panel.MerkezX, panel.MerkezY, panel.MerkezZ,
                panel.XEgimDerece, panel.YEgimDerece))
            throw new ArgumentOutOfRangeException(nameof(panel), "Panel konumu ve açıları sonlu olmalıdır.");
        var (u, v, _) = Eksenler(panel);
        var c = new Point3D(panel.MerkezX, panel.MerkezY, panel.MerkezZ);
        double w = panel.GenislikM / 2, d = panel.DerinlikM / 2;
        var pts = new[]
        {
            Topla(c, Topla(Carp(u, -w), Carp(v, -d))),
            Topla(c, Topla(Carp(u, -w), Carp(v,  d))),
            Topla(c, Topla(Carp(u,  w), Carp(v,  d))),
            Topla(c, Topla(Carp(u,  w), Carp(v, -d)))
        };
        kategori = string.IsNullOrWhiteSpace(kategori)
            ? (string.IsNullOrWhiteSpace(panel.Kategori) ? "Yansıtıcı" : panel.Kategori)
            : kategori;
        return new ProjectSurface(id, panel.Ad, kategori, pts, panel.Id);
    }

    public static IReadOnlyList<ProjectSurface> YuzeyleriOlustur(
        IEnumerable<SalonYansiticiPanel> paneller, int ilkId = 1)
    {
        ArgumentNullException.ThrowIfNull(paneller);
        var r = new List<ProjectSurface>();
        int id = ilkId;
        foreach (var p in paneller.Where(x => x.Gorunur)) r.Add(YuzeyOlustur(p, id++));
        return r;
    }

    public static SalonYansimaHatti? YansimaHatti(
        SalonYansiticiPanel panel, Point3D kaynak, Point3D hedef)
    {
        ArgumentNullException.ThrowIfNull(panel);
        if (!Sonlu(kaynak) || !Sonlu(hedef))
            throw new ArgumentOutOfRangeException(nameof(kaynak), "Kaynak ve hedef sonlu olmalıdır.");
        _ = YuzeyOlustur(panel, 0); // panel ölçü/konum sözleşmesini tek yerde doğrula
        var (u, v, n) = Eksenler(panel);
        var c = new Point3D(panel.MerkezX, panel.MerkezY, panel.MerkezZ);
        double uzaklik = Dot(Cikar(kaynak, c), n);
        var aynaKaynak = Cikar(kaynak, Carp(n, 2 * uzaklik));
        var hat = Cikar(hedef, aynaKaynak);
        double payda = Dot(hat, n);
        if (Math.Abs(payda) < 1e-9) return null;
        double t = Dot(Cikar(c, aynaKaynak), n) / payda;
        if (t < 0 || t > 1) return null;
        var q = Topla(aynaKaynak, Carp(hat, t));
        var yerel = Cikar(q, c);
        bool icinde = Math.Abs(Dot(yerel, u)) <= panel.GenislikM / 2 + 1e-8 &&
                      Math.Abs(Dot(yerel, v)) <= panel.DerinlikM / 2 + 1e-8;
        return new SalonYansimaHatti(kaynak, q, hedef,
            Uzunluk(Cikar(q, kaynak)) + Uzunluk(Cikar(hedef, q)), icinde);
    }

    public static void HedefeYonlendir(
        SalonYansiticiPanel panel, Point3D kaynak, Point3D hedef)
    {
        ArgumentNullException.ThrowIfNull(panel);
        var c = new Point3D(panel.MerkezX, panel.MerkezY, panel.MerkezZ);
        if (!Sonlu(kaynak) || !Sonlu(hedef) || !Sonlu(c))
            throw new ArgumentOutOfRangeException(nameof(kaynak), "Panel, kaynak ve hedef konumları sonlu olmalıdır.");
        var ca = Cikar(kaynak, c);
        var cb = Cikar(hedef, c);
        if (Uzunluk(ca) < 1e-9 || Uzunluk(cb) < 1e-9)
            throw new ArgumentException("Kaynak ve hedef panel merkezinden farklı olmalıdır.");
        var a = Birim(ca);
        var b = Birim(cb);
        var toplam = Topla(a, b);
        if (Uzunluk(toplam) < 1e-9)
            throw new ArgumentException("Kaynak ve hedef panel merkezine göre zıt doğrultuda; yön tanımsızdır.");
        var n = Birim(toplam);
        if (n.Z > 0) n = Carp(n, -1); // salon içine bakan alt yüz normalini kullan

        double xAci = Math.Asin(Math.Clamp(n.Y, -1, 1));
        double cosX = Math.Max(1e-9, Math.Cos(xAci));
        double yAci = Math.Atan2(-n.X / cosX, -n.Z / cosX);
        panel.XEgimDerece = xAci * 180.0 / Math.PI;
        panel.YEgimDerece = yAci * 180.0 / Math.PI;
    }

    /// <summary>Sahne ile seyirci arasına eş aralıklı, hedefe yönlendirilmiş bulut dizisi üretir.</summary>
    public static IReadOnlyList<SalonYansiticiPanel> OtomatikDizi(
        SalonGeometri salon,
        int sutun = 3,
        int sira = 2,
        double panelGenisligiM = 2.4,
        double panelDerinligiM = 1.2,
        double tavandanAskiM = 0.6)
    {
        ArgumentNullException.ThrowIfNull(salon);
        sutun = Math.Clamp(sutun, 1, 20);
        sira = Math.Clamp(sira, 1, 20);
        if (!double.IsFinite(panelGenisligiM) || !double.IsFinite(panelDerinligiM) ||
            panelGenisligiM <= 0 || panelDerinligiM <= 0)
            throw new ArgumentOutOfRangeException(nameof(panelGenisligiM));
        if (!double.IsFinite(tavandanAskiM))
            throw new ArgumentOutOfRangeException(nameof(tavandanAskiM));
        tavandanAskiM = Math.Max(0, tavandanAskiM);

        double sx = salon.Sahne?.MerkezX ?? 0;
        double sy = salon.Sahne?.MerkezY ?? 0;
        double sz = salon.Sahne is { } sh ? sh.TabanKotuM + sh.YukseklikM + 1.2 : 1.2;
        double hx = salon.SeyirciSekciyonlari.Count == 0 ? sx : salon.SeyirciSekciyonlari.Average(x => x.MerkezX);
        double hy = salon.SeyirciSekciyonlari.Count == 0 ? sy + 8 : salon.SeyirciSekciyonlari.Average(x => x.MerkezY);
        double hz = salon.SeyirciSekciyonlari.Count == 0 ? 1.2 :
            salon.SeyirciSekciyonlari.Average(x => x.TabanKotuM + 1.15);
        var kaynak = new Point3D(sx, sy, sz);
        var hedef = new Point3D(hx, hy, hz);
        double merkezY = (sy + hy) / 2;
        double toplamW = sutun * panelGenisligiM + (sutun - 1) * 0.4;
        double toplamD = sira * panelDerinligiM + (sira - 1) * 0.6;
        var r = new List<SalonYansiticiPanel>();

        for (int iy = 0; iy < sira; iy++)
            for (int ix = 0; ix < sutun; ix++)
            {
                double x = hx - toplamW / 2 + panelGenisligiM / 2 + ix * (panelGenisligiM + 0.4);
                double y = merkezY - toplamD / 2 + panelDerinligiM / 2 + iy * (panelDerinligiM + 0.6);
                double tavanKotu = SalonTavanServisi.Kot(salon, y);
                var p = new SalonYansiticiPanel
                {
                    Ad = $"Yansıtıcı {iy + 1}.{ix + 1}",
                    MerkezX = x,
                    MerkezY = y,
                    MerkezZ = Math.Min(tavanKotu, Math.Max(2.2, tavanKotu - tavandanAskiM)),
                    GenislikM = panelGenisligiM,
                    DerinlikM = panelDerinligiM
                };
                HedefeYonlendir(p, kaynak, hedef);
                TavanAltinaIndir(salon, p);
                r.Add(p);
            }
        return r;
    }

    public static (Point3D U, Point3D V, Point3D Normal) Eksenler(SalonYansiticiPanel p)
    {
        double ax = p.XEgimDerece * Math.PI / 180.0;
        double ay = p.YEgimDerece * Math.PI / 180.0;
        Point3D RxRy(Point3D q)
        {
            double y1 = q.Y * Math.Cos(ax) - q.Z * Math.Sin(ax);
            double z1 = q.Y * Math.Sin(ax) + q.Z * Math.Cos(ax);
            double x2 = q.X * Math.Cos(ay) + z1 * Math.Sin(ay);
            double z2 = -q.X * Math.Sin(ay) + z1 * Math.Cos(ay);
            return new Point3D(x2, y1, z2);
        }
        return (Birim(RxRy(new Point3D(1, 0, 0))),
                Birim(RxRy(new Point3D(0, 1, 0))),
                Birim(RxRy(new Point3D(0, 0, -1))));
    }

    private static Point3D Topla(Point3D a, Point3D b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    private static Point3D Cikar(Point3D a, Point3D b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    private static Point3D Carp(Point3D a, double k) => new(a.X * k, a.Y * k, a.Z * k);
    private static double Dot(Point3D a, Point3D b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    private static double Uzunluk(Point3D a) => Math.Sqrt(Dot(a, a));
    private static Point3D Birim(Point3D a)
    {
        double l = Uzunluk(a);
        return l < 1e-12 ? new Point3D(0, 0, 0) : Carp(a, 1 / l);
    }

    private static void TavanAltinaIndir(SalonGeometri salon, SalonYansiticiPanel panel)
    {
        var (u, v, _) = Eksenler(panel);
        double w = panel.GenislikM / 2, d = panel.DerinlikM / 2;
        double tasma = 0;
        foreach (var (su, sv) in new[] { (-w, -d), (-w, d), (w, d), (w, -d) })
        {
            var q = Topla(Carp(u, su), Carp(v, sv));
            double tavan = SalonTavanServisi.Kot(salon, panel.MerkezY + q.Y);
            tasma = Math.Max(tasma, panel.MerkezZ + q.Z - tavan);
        }
        if (tasma > 0) panel.MerkezZ -= tasma;
    }

    private static bool Sonlu(Point3D p) => Sonlu(p.X, p.Y, p.Z);
    private static bool Sonlu(params double[] degerler) => degerler.All(double.IsFinite);
}
