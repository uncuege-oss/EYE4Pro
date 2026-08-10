using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Salon.Models;

namespace Kst.Acoustics.Core.Salon.Services;

public sealed record SalonOlcuDegeri(double Deger, string Metin, Point2D Baslangic, Point2D Bitis);

public static class SalonOlculendirmeServisi
{
    public static SalonOlcuDegeri Hesapla(SalonGeometri salon, SalonOlculendirme olcu)
    {
        ArgumentNullException.ThrowIfNull(salon);
        ArgumentNullException.ThrowIfNull(olcu);
        var (a, b) = NoktalariCoz(salon, olcu);
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double deger = olcu.Tip switch
        {
            SalonOlcuTipi.Yatay => Math.Abs(dx),
            SalonOlcuTipi.Dikey => Math.Abs(dy),
            SalonOlcuTipi.Aci => NormAci(Math.Atan2(dy, dx) * 180.0 / Math.PI),
            SalonOlcuTipi.Yaricap => Yaricap(salon, olcu),
            _ => Math.Sqrt(dx * dx + dy * dy)
        };
        string ek = olcu.Tip == SalonOlcuTipi.Aci ? "°" : olcu.SonEk;
        string metin = $"{deger:0.###} {ek}".TrimEnd();
        return new SalonOlcuDegeri(deger, metin, a, b);
    }

    /// <summary>
    /// İlişkili doğrusal ölçünün değerini değiştirerek öğenin ikinci noktasını taşır.
    /// Daire/yay yarıçap ölçüsü doğrudan yarıçapı günceller.
    /// </summary>
    public static void DegerUygula(SalonGeometri salon, SalonOlculendirme olcu, double yeniDeger)
    {
        if (!double.IsFinite(yeniDeger) || yeniDeger < 0)
            throw new ArgumentOutOfRangeException(nameof(yeniDeger));
        var oge = salon.CizimOgeleri.FirstOrDefault(x => x.Id == olcu.OgeId)
                  ?? throw new InvalidOperationException("Ölçünün bağlı olduğu çizim öğesi bulunamadı.");
        if (olcu.Tip == SalonOlcuTipi.Yaricap)
        {
            if (oge.Tip is not (SalonCizimTipi.Daire or SalonCizimTipi.Yay))
                throw new InvalidOperationException("Yarıçap ölçüsü daire veya yaya bağlanmalıdır.");
            if (yeniDeger <= 0) throw new ArgumentOutOfRangeException(nameof(yeniDeger));
            oge.YariCapM = yeniDeger;
            return;
        }

        int ia = olcu.BaslangicNoktaIndeksi, ib = olcu.BitisNoktaIndeksi;
        if (ia < 0 || ib < 0 || ia >= oge.Noktalar.Count || ib >= oge.Noktalar.Count || ia == ib)
            throw new InvalidOperationException("Ölçü için geçerli iki bağlı köşe gerekir.");
        var a = oge.Noktalar[ia]; var b = oge.Noktalar[ib];
        double dx = b.X - a.X, dy = b.Y - a.Y;
        switch (olcu.Tip)
        {
            case SalonOlcuTipi.Yatay:
                oge.Noktalar[ib] = new Point2D(a.X + Math.CopySign(yeniDeger, Math.Abs(dx) < 1e-12 ? 1 : dx), b.Y);
                break;
            case SalonOlcuTipi.Dikey:
                oge.Noktalar[ib] = new Point2D(b.X, a.Y + Math.CopySign(yeniDeger, Math.Abs(dy) < 1e-12 ? 1 : dy));
                break;
            case SalonOlcuTipi.Aci:
            {
                double l = Math.Sqrt(dx * dx + dy * dy);
                double r = yeniDeger * Math.PI / 180.0;
                oge.Noktalar[ib] = new Point2D(a.X + l * Math.Cos(r), a.Y + l * Math.Sin(r));
                break;
            }
            default:
            {
                double l = Math.Sqrt(dx * dx + dy * dy);
                if (l < 1e-12) throw new InvalidOperationException("Sıfır uzunluklu ölçü düzenlenemez.");
                oge.Noktalar[ib] = new Point2D(a.X + dx / l * yeniDeger, a.Y + dy / l * yeniDeger);
                break;
            }
        }
        olcu.Baslangic = oge.Noktalar[ia];
        olcu.Bitis = oge.Noktalar[ib];
    }

    private static (Point2D A, Point2D B) NoktalariCoz(SalonGeometri salon, SalonOlculendirme o)
    {
        var oge = salon.CizimOgeleri.FirstOrDefault(x => x.Id == o.OgeId);
        if (oge is not null && o.BaslangicNoktaIndeksi >= 0 && o.BitisNoktaIndeksi >= 0 &&
            o.BaslangicNoktaIndeksi < oge.Noktalar.Count && o.BitisNoktaIndeksi < oge.Noktalar.Count)
            return (oge.Noktalar[o.BaslangicNoktaIndeksi], oge.Noktalar[o.BitisNoktaIndeksi]);
        return (o.Baslangic, o.Bitis);
    }

    private static double Yaricap(SalonGeometri salon, SalonOlculendirme o)
        => salon.CizimOgeleri.FirstOrDefault(x => x.Id == o.OgeId)?.YariCapM
           ?? Math.Sqrt(Math.Pow(o.Bitis.X - o.Baslangic.X, 2) + Math.Pow(o.Bitis.Y - o.Baslangic.Y, 2));

    private static double NormAci(double a) { while (a < 0) a += 360; while (a >= 360) a -= 360; return a; }
}

public sealed record SalonKisitCozumSonucu(
    bool Basarili,
    int UygulananKisitSayisi,
    IReadOnlyList<string> Hatalar);

/// <summary>
/// Küçük ve deterministik 2B kısıt çözücü. Bütün değişiklikleri geçici noktalar üzerinde
/// çözer; tek bir kısıt geçersizse kısmi geometri uygulamaz.
/// </summary>
public static class SalonKisitServisi
{
    public static SalonKisitCozumSonucu Uygula(SalonGeometri salon, int yineleme = 8)
    {
        ArgumentNullException.ThrowIfNull(salon);
        var noktalar = salon.CizimOgeleri.ToDictionary(
            o => o.Id, o => o.Noktalar.ToList(), StringComparer.Ordinal);
        var ogeler = salon.CizimOgeleri.ToDictionary(o => o.Id, StringComparer.Ordinal);
        var hatalar = new List<string>();
        int uygulanan = 0;

        for (int tur = 0; tur < Math.Clamp(yineleme, 1, 50); tur++)
        {
            foreach (var k in salon.GeometrikKisitlar.Where(x => x.Etkin))
            {
                try
                {
                    if (k.Tip == SalonKisitTipi.Sabit) { uygulanan++; continue; }
                    if (!ogeler.TryGetValue(k.OgeId, out var a) || !noktalar.TryGetValue(k.OgeId, out var ap))
                        throw new InvalidOperationException($"'{k.Ad}': birinci öğe bulunamadı.");
                    UygulaTek(k, a, ap, ogeler, noktalar);
                    uygulanan++;
                }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
                {
                    if (!hatalar.Contains(ex.Message, StringComparer.Ordinal)) hatalar.Add(ex.Message);
                }
            }
            if (hatalar.Count > 0) break;
        }

        if (hatalar.Count > 0) return new SalonKisitCozumSonucu(false, 0, hatalar);
        foreach (var o in salon.CizimOgeleri)
            if (noktalar.TryGetValue(o.Id, out var p)) o.Noktalar = p;
        return new SalonKisitCozumSonucu(true, uygulanan, []);
    }

    private static void UygulaTek(
        SalonGeometrikKisit k,
        SalonCizimOgesi a,
        List<Point2D> ap,
        IReadOnlyDictionary<string, SalonCizimOgesi> ogeler,
        Dictionary<string, List<Point2D>> noktalar)
    {
        int ia = k.NoktaIndeksi, ib = k.IkinciNoktaIndeksi;
        NoktaDogrula(k, ap, ia, ib);
        switch (k.Tip)
        {
            case SalonKisitTipi.Yatay:
                ap[ib] = new Point2D(ap[ib].X, ap[ia].Y);
                return;
            case SalonKisitTipi.Dikey:
                ap[ib] = new Point2D(ap[ia].X, ap[ib].Y);
                return;
            case SalonKisitTipi.Uzunluk:
                if (k.Deger <= 0 || !double.IsFinite(k.Deger))
                    throw new ArgumentOutOfRangeException(nameof(k.Deger), $"'{k.Ad}': uzunluk pozitif olmalıdır.");
                ap[ib] = BoyUygula(ap[ia], ap[ib], k.Deger, k.Ad);
                return;
        }

        if (string.IsNullOrWhiteSpace(k.IkinciOgeId) || !ogeler.ContainsKey(k.IkinciOgeId) ||
            !noktalar.TryGetValue(k.IkinciOgeId, out var bp))
            throw new InvalidOperationException($"'{k.Ad}': ikinci öğe bulunamadı.");
        int ja = k.NoktaIndeksi, jb = k.IkinciNoktaIndeksi;
        NoktaDogrula(k, bp, ja, jb);

        if (k.Tip == SalonKisitTipi.Cakistir)
        {
            bp[ja] = ap[ia];
            return;
        }

        var av = Cikar(ap[ib], ap[ia]);
        var bv = Cikar(bp[jb], bp[ja]);
        double al = Uzunluk(av), bl = Uzunluk(bv);
        if (al < 1e-12 || bl < 1e-12) throw new InvalidOperationException($"'{k.Ad}': sıfır uzunluklu segment kullanılamaz.");
        var au = Carp(av, 1 / al);
        var hedef = k.Tip switch
        {
            SalonKisitTipi.Paralel => au,
            SalonKisitTipi.Dik => new Point2D(-au.Y, au.X),
            SalonKisitTipi.EsitUzunluk => Carp(bv, al / bl),
            _ => throw new InvalidOperationException($"'{k.Ad}': desteklenmeyen kısıt.")
        };
        if (k.Tip is SalonKisitTipi.Paralel or SalonKisitTipi.Dik)
        {
            if (Dot(hedef, bv) < 0) hedef = Carp(hedef, -1);
            hedef = Carp(hedef, bl);
        }
        bp[jb] = Topla(bp[ja], hedef);
    }

    private static void NoktaDogrula(SalonGeometrikKisit k, List<Point2D> p, int a, int b)
    {
        if (a < 0 || b < 0 || a >= p.Count || b >= p.Count || a == b)
            throw new InvalidOperationException($"'{k.Ad}': köşe indisleri geçersiz.");
    }

    private static Point2D BoyUygula(Point2D a, Point2D b, double l, string ad)
    {
        var v = Cikar(b, a); double eski = Uzunluk(v);
        if (eski < 1e-12) throw new InvalidOperationException($"'{ad}': sıfır uzunluklu segment ölçeklenemez.");
        return Topla(a, Carp(v, l / eski));
    }
    private static Point2D Topla(Point2D a, Point2D b) => new(a.X + b.X, a.Y + b.Y);
    private static Point2D Cikar(Point2D a, Point2D b) => new(a.X - b.X, a.Y - b.Y);
    private static Point2D Carp(Point2D a, double k) => new(a.X * k, a.Y * k);
    private static double Dot(Point2D a, Point2D b) => a.X * b.X + a.Y * b.Y;
    private static double Uzunluk(Point2D a) => Math.Sqrt(Dot(a, a));
}
