using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Salon.Models;

namespace Kst.Acoustics.Core.Salon.Services;

public enum SalonGorusKalitesi
{
    Engelli,
    Zayif,
    KabulEdilebilir,
    Iyi
}

public sealed record SalonKoltukGorusSonucu(
    string KoltukId,
    Point3D GozNoktasi,
    Point3D Hedef,
    double CDegeriMm,
    double GorunurlukPuani,
    SalonGorusKalitesi Kalite,
    string? EngelKoltukId);

public sealed record SalonGorusRaporu(
    IReadOnlyList<SalonKoltukGorusSonucu> Koltuklar,
    double OrtalamaCMm,
    double MinimumCMm,
    double IyiGorusYuzdesi,
    IReadOnlyList<string> Uyarilar);

/// <summary>
/// Koltuk gözünden sahne hedef noktasına görüş çizgisini ve bir önceki sıradaki baş
/// üzerinden düşey açıklığı (C değeri) hesaplar. Sonuçlar 2B/3B ısı haritasında doğrudan
/// kullanılabilecek 0..1 görünürlük puanı taşır.
/// </summary>
public static class SalonGorusHattiServisi
{
    public const double VarsayilanGozYuksekligiM = 1.15;
    public const double IyiCMetre = 0.12;
    public const double KabulEdilebilirCMetre = 0.09;

    public static SalonGorusRaporu AnalizEt(
        SalonKoltukYerlesimi yerlesim,
        Point3D hedef,
        double gozYuksekligiM = VarsayilanGozYuksekligiM)
    {
        ArgumentNullException.ThrowIfNull(yerlesim);
        if (!Sonlu(hedef.X) || !Sonlu(hedef.Y) || !Sonlu(hedef.Z))
            throw new ArgumentOutOfRangeException(nameof(hedef), "Sahne hedef noktası sonlu olmalıdır.");
        if (!Sonlu(gozYuksekligiM) || gozYuksekligiM <= 0)
            throw new ArgumentOutOfRangeException(nameof(gozYuksekligiM));

        var uyarilar = new List<string>(yerlesim.Uyarilar);
        var sonuc = new List<SalonKoltukGorusSonucu>();
        var siralar = yerlesim.Siralar.OrderBy(s => s.Sira).ToList();

        for (int i = 0; i < siralar.Count; i++)
        {
            var sira = siralar[i];
            foreach (var koltuk in sira.Koltuklar)
            {
                var goz = new Point3D(koltuk.Konum.X, koltuk.Konum.Y, koltuk.Konum.Z + gozYuksekligiM);
                if (i == 0)
                {
                    sonuc.Add(new SalonKoltukGorusSonucu(
                        koltuk.Id, goz, hedef, double.PositiveInfinity, 1,
                        SalonGorusKalitesi.Iyi, null));
                    continue;
                }

                var onceki = EnYakinKoltuk(siralar[i - 1].Koltuklar, goz, hedef);
                if (onceki is null)
                {
                    sonuc.Add(new SalonKoltukGorusSonucu(
                        koltuk.Id, goz, hedef, double.PositiveInfinity, 1,
                        SalonGorusKalitesi.Iyi, null));
                    continue;
                }

                var engelGoz = new Point3D(
                    onceki.Konum.X, onceki.Konum.Y, onceki.Konum.Z + gozYuksekligiM);
                double c = DuseyAciklik(goz, hedef, engelGoz);
                double puan = Math.Clamp(c / IyiCMetre, 0, 1);
                var kalite = c switch
                {
                    < 0 => SalonGorusKalitesi.Engelli,
                    < KabulEdilebilirCMetre => SalonGorusKalitesi.Zayif,
                    < IyiCMetre => SalonGorusKalitesi.KabulEdilebilir,
                    _ => SalonGorusKalitesi.Iyi
                };
                sonuc.Add(new SalonKoltukGorusSonucu(
                    koltuk.Id, goz, hedef, c * 1000.0, puan, kalite, onceki.Id));
            }
        }

        var sonlu = sonuc.Where(x => double.IsFinite(x.CDegeriMm)).ToList();
        double ort = sonlu.Count == 0 ? 0 : sonlu.Average(x => x.CDegeriMm);
        double min = sonlu.Count == 0 ? 0 : sonlu.Min(x => x.CDegeriMm);
        double iyi = sonuc.Count == 0 ? 0
            : 100.0 * sonuc.Count(x => x.Kalite is SalonGorusKalitesi.Iyi or SalonGorusKalitesi.KabulEdilebilir)
              / sonuc.Count;
        if (sonlu.Any(x => x.Kalite is SalonGorusKalitesi.Engelli or SalonGorusKalitesi.Zayif))
            uyarilar.Add("Bazı koltuklarda C değeri 90 mm altında; kademe yüksekliği veya sıra aralığını artırın.");

        return new SalonGorusRaporu(sonuc, ort, min, iyi, uyarilar);
    }

    public static SalonGorusRaporu AnalizEt(SalonGeometri salon, SeyirciSekciyonu sekciyon)
    {
        ArgumentNullException.ThrowIfNull(salon);
        ArgumentNullException.ThrowIfNull(sekciyon);
        var yerlesim = SalonKoltukYerlesimServisi.Uret(sekciyon, salon.Sahne);
        var hedef = salon.Sahne is { } s
            ? new Point3D(s.MerkezX, s.MerkezY, s.TabanKotuM + s.YukseklikM + 1.20)
            : new Point3D(sekciyon.MerkezX,
                sekciyon.TersYon
                    ? sekciyon.MerkezY + sekciyon.DerinlikM / 2 + 1
                    : sekciyon.MerkezY - sekciyon.DerinlikM / 2 - 1,
                1.20);
        return AnalizEt(yerlesim, hedef);
    }

    private static SalonKoltuk? EnYakinKoltuk(
        IReadOnlyList<SalonKoltuk> koltuklar, Point3D goz, Point3D hedef)
        => koltuklar.Count == 0 ? null : koltuklar.MinBy(k => YatayHatUzakligi(k.Konum, goz, hedef));

    private static double DuseyAciklik(Point3D goz, Point3D hedef, Point3D engelGoz)
    {
        double dx = hedef.X - goz.X, dy = hedef.Y - goz.Y;
        double l2 = dx * dx + dy * dy;
        if (l2 < 1e-12) return hedef.Z - engelGoz.Z;

        // Önceki başın plan izdüşümünü görüş ışınına dik izdüşür. Eski baskın-eksen
        // hesabı çapraz bakışlarda X ve Y için farklı t seçip C değerini bozuyordu.
        double t = ((engelGoz.X - goz.X) * dx + (engelGoz.Y - goz.Y) * dy) / l2;
        if (t <= 0 || t >= 1) return double.PositiveInfinity;

        double zHat = goz.Z + t * (hedef.Z - goz.Z);
        return zHat - engelGoz.Z;
    }

    private static double YatayHatUzakligi(Point3D p, Point3D a, Point3D b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double l2 = dx * dx + dy * dy;
        if (l2 < 1e-12)
            return Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
        double t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2, 0, 1);
        double qx = a.X + t * dx, qy = a.Y + t * dy;
        return Math.Sqrt((p.X - qx) * (p.X - qx) + (p.Y - qy) * (p.Y - qy));
    }

    private static bool Sonlu(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
}
