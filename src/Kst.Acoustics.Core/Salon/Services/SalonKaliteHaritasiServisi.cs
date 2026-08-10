using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Importing;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Salon.Models;
using Kst.Acoustics.Core.Validation;

namespace Kst.Acoustics.Core.Salon.Services;

public enum SalonKaliteSeviyesi
{
    Bilgi,
    Uyari,
    Hata
}

public sealed record SalonKaliteBulgusu(
    string Kod,
    SalonKaliteSeviyesi Seviye,
    string Baslik,
    string Aciklama,
    Point2D? OdakNoktasi = null,
    IReadOnlyList<string>? OgeIdleri = null,
    IReadOnlyList<int>? YuzeyIdleri = null)
{
    public IReadOnlyList<string> OgeIdleri { get; init; } = OgeIdleri?.Distinct().ToArray() ?? [];
    public IReadOnlyList<int> YuzeyIdleri { get; init; } = YuzeyIdleri?.Distinct().ToArray() ?? [];
}

public sealed record SalonKaliteRaporu(IReadOnlyList<SalonKaliteBulgusu> Bulgular)
{
    public int HataSayisi => Bulgular.Count(x => x.Seviye == SalonKaliteSeviyesi.Hata);
    public int UyariSayisi => Bulgular.Count(x => x.Seviye == SalonKaliteSeviyesi.Uyari);
    public bool M01IcinHazir => HataSayisi == 0;
    public string Ozet => Bulgular.Count == 0 ? "Geometri kalite kontrolünden geçti."
        : $"{HataSayisi} hata, {UyariSayisi} uyarı, {Bulgular.Count - HataSayisi - UyariSayisi} bilgi";
}

/// <summary>
/// Hataları yalnız metin olarak değil, canvas'ın yakınlaştırabileceği odak noktalarıyla
/// raporlar: sızıntı/açık kenar, ters normal, örtüşme, çift yüzey, bozuk öğe ve açıklık bağı.
/// </summary>
public static class SalonKaliteHaritasiServisi
{
    public static SalonKaliteRaporu AnalizEt(SalonGeometri salon)
    {
        ArgumentNullException.ThrowIfNull(salon);
        var bulgu = new List<SalonKaliteBulgusu>();
        KimlikVeSayisalKontrol(salon, bulgu);
        KatmanKontrol(salon, bulgu);
        AciklikKontrol(salon, bulgu);
        OrtusmeKontrol(salon, bulgu);

        List<ProjectSurface> yuzeyler;
        try { yuzeyler = Salon3DGorunumServisi.YuzeyleriOlustur(salon); }
        catch (Exception ex)
        {
            bulgu.Add(new SalonKaliteBulgusu("3D_URETIM", SalonKaliteSeviyesi.Hata,
                "3B yüzey üretilemedi", ex.Message));
            return Sirala(bulgu);
        }

        var proje = new AcousticsProject(
            salon.SalonAdi, salon.DosyaYolu, DateTime.UtcNow,
            SurfaceImportFormat.Salon, SurfaceImportUnit.Meters, yuzeyler,
            RoomVolume: Math.Max(0, SalonHacimServisi.Hesapla(salon)));
        var model = new ModelHealthService().Analyze(proje);
        foreach (var f in model.Findings)
        {
            var ids = f.AffectedSurfaceIds;
            Point2D? odak = YuzeyOdak(yuzeyler.Where(x => ids.Contains(x.Id)).ToList());
            bulgu.Add(new SalonKaliteBulgusu(
                "M01_" + f.Code.ToString().ToUpperInvariant(),
                f.Severity switch
                {
                    ModelHealthSeverity.Error => SalonKaliteSeviyesi.Hata,
                    ModelHealthSeverity.Warning => SalonKaliteSeviyesi.Uyari,
                    _ => SalonKaliteSeviyesi.Bilgi
                }, f.Title, f.Description, odak, YuzeyIdleri: ids));
        }

        foreach (var u in SalonDogrulamaServisi.Dogrula(salon))
            if (!bulgu.Any(x => x.Aciklama == u))
                bulgu.Add(new SalonKaliteBulgusu("SALON_DOGRULAMA", SalonKaliteSeviyesi.Uyari,
                    "Salon doğrulaması", u));
        return Sirala(bulgu);
    }

    private static void KimlikVeSayisalKontrol(SalonGeometri s, List<SalonKaliteBulgusu> r)
    {
        foreach (var g in s.CizimOgeleri.GroupBy(x => x.Id).Where(g => string.IsNullOrWhiteSpace(g.Key) || g.Count() > 1))
            r.Add(new SalonKaliteBulgusu("CIFT_OGE_ID", SalonKaliteSeviyesi.Hata,
                "Çizim öğesi kimliği yineleniyor", $"'{g.Key}' kimliği {g.Count()} öğede kullanılıyor.",
                OgeIdleri: g.Select(x => x.Id).ToArray()));
        foreach (var g in s.Katmanlar.GroupBy(x => x.Id).Where(g => string.IsNullOrWhiteSpace(g.Key) || g.Count() > 1))
            r.Add(new SalonKaliteBulgusu("CIFT_KATMAN_ID", SalonKaliteSeviyesi.Hata,
                "Katman kimliği yineleniyor", $"'{g.Key}' kimliği {g.Count()} katmanda kullanılıyor."));

        foreach (var o in s.CizimOgeleri)
        {
            if (!Enum.IsDefined(o.Tip) || o.Noktalar.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y)) ||
                !double.IsFinite(o.YariCapM))
                r.Add(new SalonKaliteBulgusu("GECERSIZ_OGE", SalonKaliteSeviyesi.Hata,
                    "Geçersiz çizim öğesi", "Öğe sonlu olmayan koordinat veya tanımsız tür içeriyor.",
                    o.Noktalar.Count > 0 ? o.Noktalar[0] : null, [o.Id]));
            else if (!YeterliNokta(o))
                r.Add(new SalonKaliteBulgusu("DEJENERE_OGE", SalonKaliteSeviyesi.Hata,
                    "Eksik/dejenere çizim öğesi", $"{o.Tip} için yeterli farklı köşe veya yarıçap yok.",
                    o.Noktalar.Count > 0 ? o.Noktalar[0] : null, [o.Id]));
        }
        if (!double.IsFinite(s.SalonYuksekligiM) || s.SalonYuksekligiM <= 0)
            r.Add(new SalonKaliteBulgusu("YUKSEKLIK", SalonKaliteSeviyesi.Hata,
                "Salon yüksekliği geçersiz", "Salon yüksekliği sonlu ve pozitif olmalıdır."));
    }

    private static void KatmanKontrol(SalonGeometri s, List<SalonKaliteBulgusu> r)
    {
        var gerekli = new[] { SalonKatmanTipi.Zemin, SalonKatmanTipi.Tavan, SalonKatmanTipi.DuvarSeyirci };
        foreach (var tip in gerekli.Where(t => s.Katmanlar.All(k => k.Tip != t)))
            r.Add(new SalonKaliteBulgusu("EKSIK_KATMAN", SalonKaliteSeviyesi.Hata,
                "Anlamsal katman eksik", $"{tip} katmanı bulunmuyor."));
        foreach (var o in s.CizimOgeleri.Where(o => !string.IsNullOrWhiteSpace(o.KatmanId) && s.Katmanlar.All(k => k.Id != o.KatmanId)))
            r.Add(new SalonKaliteBulgusu("KIRIK_KATMAN", SalonKaliteSeviyesi.Hata,
                "Kırık katman bağı", "Çizim öğesinin bağlı olduğu katman bulunmuyor.",
                o.Noktalar.FirstOrDefault(), [o.Id]));
    }

    private static void AciklikKontrol(SalonGeometri s, List<SalonKaliteBulgusu> r)
    {
        var seg = DuvarSegmentleri(s);
        foreach (var a in s.Acikliklar)
        {
            var aday = seg.Select(x => (Seg: x, D: UzaklikVeKonum(x.A, x.B, new Point2D(a.MerkezX, a.MerkezY))))
                .Where(x => x.D.Uzaklik <= Salon3DGorunumServisi.AciklikToleransM)
                .OrderBy(x => x.D.Uzaklik).ToList();
            var p = new Point2D(a.MerkezX, a.MerkezY);
            if (aday.Count == 0)
                r.Add(new SalonKaliteBulgusu("BAGSIZ_ACIKLIK", SalonKaliteSeviyesi.Hata,
                    "Duvara bağlanmayan açıklık", $"'{a.Ad}' hiçbir duvar segmentine bağlanmıyor.", p, [a.Id]));
            else
            {
                if (aday.Count > 1 && Math.Abs(aday[0].D.Uzaklik - aday[1].D.Uzaklik) < 1e-6)
                    r.Add(new SalonKaliteBulgusu("BELIRSIZ_ACIKLIK", SalonKaliteSeviyesi.Uyari,
                        "Açıklık köşede/belirsiz", $"'{a.Ad}' eş uzaklıkta birden fazla duvara aday.", p, [a.Id]));
                double l = Uzaklik(aday[0].Seg.A, aday[0].Seg.B);
                double sAlong = aday[0].D.T * l;
                if (sAlong - a.GenislikM / 2 < -1e-8 || sAlong + a.GenislikM / 2 > l + 1e-8)
                    r.Add(new SalonKaliteBulgusu("TASAN_ACIKLIK", SalonKaliteSeviyesi.Hata,
                        "Açıklık duvar ucundan taşıyor", $"'{a.Ad}' genişliği duvar segmentine bütünüyle sığmıyor.", p, [a.Id]));
            }
            if (a.GenislikM <= 0 || a.YukseklikM <= 0 || a.AltKotM < 0 ||
                !double.IsFinite(a.GenislikM + a.YukseklikM + a.AltKotM))
                r.Add(new SalonKaliteBulgusu("ACIKLIK_OLCU", SalonKaliteSeviyesi.Hata,
                    "Açıklık ölçüsü geçersiz", $"'{a.Ad}' ölçü/kot değerlerini kontrol edin.", p, [a.Id]));
        }
    }

    private static void OrtusmeKontrol(SalonGeometri s, List<SalonKaliteBulgusu> r)
    {
        var bolgeler = s.CizimOgeleri.Select(o =>
        {
            var k = s.Katmanlar.FirstOrDefault(x => x.Id == o.KatmanId);
            return (O: o, K: k, P: o.KapaliPoligon(Salon3DGorunumServisi.Faset(s)));
        }).Where(x => x.P.Count >= 3 && x.K is not null && x.K.Tip != SalonKatmanTipi.Zemin).ToList();
        for (int i = 0; i < bolgeler.Count; i++)
            for (int j = i + 1; j < bolgeler.Count; j++)
            {
                bool ayniDuzlem = bolgeler[i].K!.Tip == SalonKatmanTipi.Tavan ==
                                  (bolgeler[j].K!.Tip == SalonKatmanTipi.Tavan);
                if (!ayniDuzlem) continue;
                double a = ClipperAdaptor.NetAlan(ClipperAdaptor.Kesisim(
                    new[] { (IReadOnlyList<Point2D>)bolgeler[i].P },
                    new[] { (IReadOnlyList<Point2D>)bolgeler[j].P }));
                if (a <= 1e-5) continue;
                var odak = new Point2D(
                    bolgeler[i].P.Average(x => x.X), bolgeler[i].P.Average(x => x.Y));
                r.Add(new SalonKaliteBulgusu("ORTUSEN_BOLGE", SalonKaliteSeviyesi.Uyari,
                    "Örtüşen coplanar bölgeler", $"İki bölge {a:F2} m² örtüşüyor; son çizilen bölge önceliklidir.",
                    odak, [bolgeler[i].O.Id, bolgeler[j].O.Id]));
            }
    }

    private static List<(Point2D A, Point2D B)> DuvarSegmentleri(SalonGeometri s)
    {
        var r = new List<(Point2D, Point2D)>();
        foreach (var o in s.CizimOgeleri)
        {
            var k = s.Katmanlar.FirstOrDefault(x => x.Id == o.KatmanId);
            if (k is { Gorunur: false }) continue;
            var p = o.KapaliPoligon(Salon3DGorunumServisi.Faset(s));
            if (p.Count >= 3)
            {
                if (k is null || k.Tip == SalonKatmanTipi.Zemin)
                    for (int i = 0; i < p.Count; i++) r.Add((p[i], p[(i + 1) % p.Count]));
            }
            else r.AddRange(o.Segmentler(Salon3DGorunumServisi.Faset(s)));
        }
        return r;
    }

    private static (double Uzaklik, double T) UzaklikVeKonum(Point2D a, Point2D b, Point2D p)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy;
        if (l2 < 1e-12) return (Uzaklik(a, p), 0);
        double t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2, 0, 1);
        return (Uzaklik(p, new Point2D(a.X + t * dx, a.Y + t * dy)), t);
    }

    private static bool YeterliNokta(SalonCizimOgesi o) => o.Tip switch
    {
        SalonCizimTipi.Cizgi => o.Noktalar.Count >= 2 && Uzaklik(o.Noktalar[0], o.Noktalar[1]) > 1e-8,
        SalonCizimTipi.Polyline => o.Noktalar.Distinct().Count() >= (o.Kapali ? 3 : 2),
        SalonCizimTipi.Dikdortgen => o.Noktalar.Distinct().Count() >= 3,
        SalonCizimTipi.Daire or SalonCizimTipi.Yay => o.Noktalar.Count > 0 && o.YariCapM > 1e-8,
        _ => false
    };

    private static Point2D? YuzeyOdak(IReadOnlyList<ProjectSurface> s)
    {
        var p = s.SelectMany(x => x.Vertices).ToList();
        return p.Count == 0 ? null : new Point2D(p.Average(x => x.X), p.Average(x => x.Y));
    }

    private static SalonKaliteRaporu Sirala(List<SalonKaliteBulgusu> b) => new(
        b.OrderByDescending(x => x.Seviye).ThenBy(x => x.Baslik).ToList());

    private static double Uzaklik(Point2D a, Point2D b)
        => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
}
