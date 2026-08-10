using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Salon.Models;

namespace Kst.Acoustics.Core.Salon.Services;

public enum SalonHizalamaTipi
{
    Sol,
    YatayMerkez,
    Sag,
    Alt,
    DikeyMerkez,
    Ust
}

public sealed record SalonKoseIslemSonucu(
    SalonCizimOgesi Birinci,
    SalonCizimOgesi Ikinci,
    SalonCizimOgesi? Yay);

public sealed record SalonPanoPaketi(
    IReadOnlyList<SalonCizimOgesi> Ogeler,
    Point2D BazNoktasi);

/// <summary>Fillet/pah, dizi, hizala/dağıt, grup/blok ve kopyala-yapıştır CAD araçları.</summary>
public static class SalonGelismisCadServisi
{
    public static SalonKoseIslemSonucu PahKir(
        Point2D onceki, Point2D kose, Point2D sonraki, double mesafeM, string katmanId = "")
    {
        var (t1, t2, _, _) = Tegetler(onceki, kose, sonraki, mesafeM, yaricapMi: false);
        return new SalonKoseIslemSonucu(
            Cizgi(onceki, t1, katmanId), Cizgi(t2, sonraki, katmanId), Cizgi(t1, t2, katmanId));
    }

    public static SalonKoseIslemSonucu KoseYuvarla(
        Point2D onceki, Point2D kose, Point2D sonraki, double yaricapM, string katmanId = "")
    {
        var (t1, t2, merkez, aciortay) = Tegetler(onceki, kose, sonraki, yaricapM, yaricapMi: true);
        var yayUstu = new Point2D(merkez.X - aciortay.X * yaricapM, merkez.Y - aciortay.Y * yaricapM);
        var yay = SalonCizimOgesi.UcNoktadanYay(t1, t2, yayUstu)
                  ?? throw new InvalidOperationException("Köşe için geçerli teğet yay üretilemedi.");
        yay.KatmanId = katmanId;
        return new SalonKoseIslemSonucu(Cizgi(onceki, t1, katmanId), Cizgi(t2, sonraki, katmanId), yay);
    }

    /// Sayı, özgün öğe dahil toplam örnek sayısıdır.
    public static IReadOnlyList<SalonCizimOgesi> DogrusalDizi(
        IEnumerable<SalonCizimOgesi> kaynak, int sayi, double dx, double dy)
    {
        ArgumentNullException.ThrowIfNull(kaynak);
        sayi = Math.Clamp(sayi, 1, 10_000);
        var temel = kaynak.ToList();
        var r = new List<SalonCizimOgesi>(temel.Count * sayi);
        string? grup = temel.Count > 1 ? Guid.NewGuid().ToString("N") : null;
        for (int i = 0; i < sayi; i++)
            foreach (var o in temel)
            {
                var c = SalonDonusumServisi.Kopyala(o);
                SalonDonusumServisi.Tasi(c, i * dx, i * dy);
                if (grup is not null) c.GrupId = $"{grup}-{i}";
                r.Add(c);
            }
        return r;
    }

    public static IReadOnlyList<SalonCizimOgesi> DaireselDizi(
        IEnumerable<SalonCizimOgesi> kaynak, Point2D merkez, int sayi,
        double toplamAciDerece = 360, bool tamDairedeSonuTekrarlama = false)
    {
        ArgumentNullException.ThrowIfNull(kaynak);
        sayi = Math.Clamp(sayi, 1, 10_000);
        var temel = kaynak.ToList();
        double bolen = tamDairedeSonuTekrarlama || Math.Abs(Math.Abs(toplamAciDerece) - 360) > 1e-9
            ? Math.Max(1, sayi - 1)
            : sayi;
        var r = new List<SalonCizimOgesi>(temel.Count * sayi);
        string? grup = temel.Count > 1 ? Guid.NewGuid().ToString("N") : null;
        for (int i = 0; i < sayi; i++)
            foreach (var o in temel)
            {
                var c = SalonDonusumServisi.Kopyala(o);
                SalonDonusumServisi.Dondur(c, merkez, toplamAciDerece * i / bolen);
                if (grup is not null) c.GrupId = $"{grup}-{i}";
                r.Add(c);
            }
        return r;
    }

    public static void Hizala(IReadOnlyList<SalonCizimOgesi> ogeler, SalonHizalamaTipi tip)
    {
        ArgumentNullException.ThrowIfNull(ogeler);
        if (ogeler.Count < 2) return;
        var kutular = ogeler.Select(Sinir).ToList();
        double hedef = tip switch
        {
            SalonHizalamaTipi.Sol => kutular.Min(x => x.MinX),
            SalonHizalamaTipi.Sag => kutular.Max(x => x.MaxX),
            SalonHizalamaTipi.YatayMerkez => (kutular.Min(x => x.MinX) + kutular.Max(x => x.MaxX)) / 2,
            SalonHizalamaTipi.Alt => kutular.Min(x => x.MinY),
            SalonHizalamaTipi.Ust => kutular.Max(x => x.MaxY),
            _ => (kutular.Min(x => x.MinY) + kutular.Max(x => x.MaxY)) / 2
        };
        for (int i = 0; i < ogeler.Count; i++)
        {
            var b = kutular[i];
            double dx = tip switch
            {
                SalonHizalamaTipi.Sol => hedef - b.MinX,
                SalonHizalamaTipi.Sag => hedef - b.MaxX,
                SalonHizalamaTipi.YatayMerkez => hedef - (b.MinX + b.MaxX) / 2,
                _ => 0
            };
            double dy = tip switch
            {
                SalonHizalamaTipi.Alt => hedef - b.MinY,
                SalonHizalamaTipi.Ust => hedef - b.MaxY,
                SalonHizalamaTipi.DikeyMerkez => hedef - (b.MinY + b.MaxY) / 2,
                _ => 0
            };
            SalonDonusumServisi.Tasi(ogeler[i], dx, dy);
        }
    }

    public static void EsitDagit(IReadOnlyList<SalonCizimOgesi> ogeler, bool yatay)
    {
        ArgumentNullException.ThrowIfNull(ogeler);
        if (ogeler.Count < 3) return;
        var sirali = ogeler.Select(o => (Oge: o, B: Sinir(o)))
            .OrderBy(x => yatay ? (x.B.MinX + x.B.MaxX) / 2 : (x.B.MinY + x.B.MaxY) / 2)
            .ToList();
        double ilk = yatay ? MerkezX(sirali[0].B) : MerkezY(sirali[0].B);
        double son = yatay ? MerkezX(sirali[^1].B) : MerkezY(sirali[^1].B);
        for (int i = 1; i + 1 < sirali.Count; i++)
        {
            double hedef = ilk + (son - ilk) * i / (sirali.Count - 1);
            double mevcut = yatay ? MerkezX(sirali[i].B) : MerkezY(sirali[i].B);
            SalonDonusumServisi.Tasi(sirali[i].Oge, yatay ? hedef - mevcut : 0, yatay ? 0 : hedef - mevcut);
        }
    }

    public static string Grupla(IEnumerable<SalonCizimOgesi> ogeler)
    {
        ArgumentNullException.ThrowIfNull(ogeler);
        string id = Guid.NewGuid().ToString("N");
        foreach (var o in ogeler) o.GrupId = id;
        return id;
    }

    public static void GrubuCoz(IEnumerable<SalonCizimOgesi> ogeler)
    {
        ArgumentNullException.ThrowIfNull(ogeler);
        foreach (var o in ogeler) o.GrupId = null;
    }

    public static void BlokTanimla(IEnumerable<SalonCizimOgesi> ogeler, string blokAdi)
    {
        ArgumentNullException.ThrowIfNull(ogeler);
        ArgumentException.ThrowIfNullOrWhiteSpace(blokAdi);
        string grup = Grupla(ogeler);
        foreach (var o in ogeler.Where(x => x.GrupId == grup)) o.BlokAdi = blokAdi.Trim();
    }

    public static SalonPanoPaketi PanoyaKopyala(IEnumerable<SalonCizimOgesi> ogeler)
    {
        ArgumentNullException.ThrowIfNull(ogeler);
        var kopyalar = ogeler.Select(SalonDonusumServisi.Kopyala).ToList();
        if (kopyalar.Count == 0) return new SalonPanoPaketi([], default);
        var tum = kopyalar.SelectMany(Noktalar).ToList();
        var baz = new Point2D(tum.Min(p => p.X), tum.Min(p => p.Y));
        return new SalonPanoPaketi(kopyalar, baz);
    }

    public static IReadOnlyList<SalonCizimOgesi> Yapistir(SalonPanoPaketi paket, Point2D hedef)
    {
        ArgumentNullException.ThrowIfNull(paket);
        double dx = hedef.X - paket.BazNoktasi.X, dy = hedef.Y - paket.BazNoktasi.Y;
        var r = paket.Ogeler.Select(SalonDonusumServisi.Kopyala).ToList();
        foreach (var o in r) SalonDonusumServisi.Tasi(o, dx, dy);
        return r;
    }

    public static IReadOnlyDictionary<string, bool> KatmaniYalit(
        SalonGeometri salon, string katmanId)
    {
        ArgumentNullException.ThrowIfNull(salon);
        var onceki = salon.Katmanlar.ToDictionary(k => k.Id, k => k.Gorunur);
        foreach (var k in salon.Katmanlar) k.Gorunur = k.Id == katmanId;
        return onceki;
    }

    public static void KatmanGorunurlugunuGeriYukle(
        SalonGeometri salon, IReadOnlyDictionary<string, bool> durum)
    {
        foreach (var k in salon.Katmanlar)
            if (durum.TryGetValue(k.Id, out bool v)) k.Gorunur = v;
    }

    private static (Point2D T1, Point2D T2, Point2D Merkez, Point2D Aciortay) Tegetler(
        Point2D onceki, Point2D kose, Point2D sonraki, double deger, bool yaricapMi)
    {
        if (!double.IsFinite(deger) || deger <= 0) throw new ArgumentOutOfRangeException(nameof(deger));
        var u1 = Birim(Cikar(onceki, kose)); var u2 = Birim(Cikar(sonraki, kose));
        double dot = Math.Clamp(Dot(u1, u2), -1, 1);
        double aci = Math.Acos(dot);
        if (aci < 1e-4 || Math.PI - aci < 1e-4) throw new InvalidOperationException("Doğrusal veya sıfır açılı köşe işlenemez.");
        double d = yaricapMi ? deger / Math.Tan(aci / 2) : deger;
        double l1 = Uzunluk(Cikar(onceki, kose)), l2 = Uzunluk(Cikar(sonraki, kose));
        if (d >= l1 || d >= l2) throw new InvalidOperationException("Köşe mesafesi komşu segmentten kısa olmalıdır.");
        var t1 = Topla(kose, Carp(u1, d)); var t2 = Topla(kose, Carp(u2, d));
        var bis = Birim(Topla(u1, u2));
        var merkez = yaricapMi ? Topla(kose, Carp(bis, deger / Math.Sin(aci / 2))) : kose;
        return (t1, t2, merkez, bis);
    }

    private static SalonCizimOgesi Cizgi(Point2D a, Point2D b, string katmanId) => new()
    {
        Tip = SalonCizimTipi.Cizgi, Noktalar = [a, b], KatmanId = katmanId
    };

    private static IEnumerable<Point2D> Noktalar(SalonCizimOgesi o)
    {
        if (o.Tip is (SalonCizimTipi.Daire or SalonCizimTipi.Yay) && o.Noktalar.Count > 0)
        {
            var c = o.Noktalar[0]; double r = o.YariCapM;
            return [new Point2D(c.X - r, c.Y - r), new Point2D(c.X + r, c.Y + r)];
        }
        return o.Noktalar;
    }

    private static (double MinX, double MinY, double MaxX, double MaxY) Sinir(SalonCizimOgesi o)
    {
        var p = Noktalar(o).ToList();
        if (p.Count == 0) return (0, 0, 0, 0);
        return (p.Min(x => x.X), p.Min(x => x.Y), p.Max(x => x.X), p.Max(x => x.Y));
    }
    private static double MerkezX((double MinX, double MinY, double MaxX, double MaxY) b) => (b.MinX + b.MaxX) / 2;
    private static double MerkezY((double MinX, double MinY, double MaxX, double MaxY) b) => (b.MinY + b.MaxY) / 2;
    private static Point2D Topla(Point2D a, Point2D b) => new(a.X + b.X, a.Y + b.Y);
    private static Point2D Cikar(Point2D a, Point2D b) => new(a.X - b.X, a.Y - b.Y);
    private static Point2D Carp(Point2D a, double k) => new(a.X * k, a.Y * k);
    private static double Dot(Point2D a, Point2D b) => a.X * b.X + a.Y * b.Y;
    private static double Uzunluk(Point2D a) => Math.Sqrt(Dot(a, a));
    private static Point2D Birim(Point2D a)
    {
        double l = Uzunluk(a);
        if (l < 1e-12) throw new InvalidOperationException("Sıfır uzunluklu segment işlenemez.");
        return Carp(a, 1 / l);
    }
}
