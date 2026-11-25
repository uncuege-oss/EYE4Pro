using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Terrain.Models;

namespace Kst.Acoustics.Core.Terrain.Services;

public readonly record struct KarsilastirmaNoktasi(double X, double Y, double IlkKot, double SonKot)
{
    public double Fark => IlkKot - SonKot;
}

public readonly record struct MetrajParcasi(KarsilastirmaNoktasi A, KarsilastirmaNoktasi B,
    KarsilastirmaNoktasi C, double AlanM2, int IlkUcgen, int SonUcgen)
{
    public double OrtalamaFark => (A.Fark + B.Fark + C.Fark) / 3;
    public double HacimM3 => AlanM2 * Math.Abs(OrtalamaFark);
}

public sealed class AraziKarsilastirmasi
{
    private readonly List<MetrajParcasi> _parcalar = new();
    public IReadOnlyList<MetrajParcasi> Parcalar => _parcalar;
    public double IlkAlanM2 { get; internal set; }
    public double SonAlanM2 { get; internal set; }
    public double OrtakAlanM2 => YarmaAlaniM2 + DolguAlaniM2 + EsitAlanM2;
    public double YarmaAlaniM2 { get; private set; }
    public double DolguAlaniM2 { get; private set; }
    public double EsitAlanM2 { get; private set; }
    public double YarmaM3 { get; private set; }
    public double DolguM3 { get; private set; }
    public double NetM3 => YarmaM3 - DolguM3;

    internal void Ekle(KarsilastirmaNoktasi a, KarsilastirmaNoktasi b, KarsilastirmaNoktasi c, int ilkUcgen, int sonUcgen)
    {
        double alan = Math.Abs((b.X-a.X)*(c.Y-a.Y) - (b.Y-a.Y)*(c.X-a.X)) / 2;
        if (alan <= 1e-12) return;
        var parca = new MetrajParcasi(a, b, c, alan, ilkUcgen, sonUcgen);
        if (!double.IsFinite(parca.HacimM3)) throw new InvalidDataException("Geçersiz kot veya koordinat aralığı.");
        if (_parcalar.Count >= 2_000_000)
            throw new InvalidOperationException("Ortak yüzey iki milyon parçayı aştı. Daha küçük bir çalışma alanı seçin.");
        _parcalar.Add(parca);
        if (parca.OrtalamaFark > 0) { YarmaAlaniM2 += alan; YarmaM3 += parca.HacimM3; }
        else if (parca.OrtalamaFark < 0) { DolguAlaniM2 += alan; DolguM3 += parca.HacimM3; }
        else EsitAlanM2 += alan;
    }
}

/// Integrate the linear elevation difference on each intersection of two TIN faces.
/// Split at zero before integration so cut and fill in the same face cannot cancel.
public static class AraziKarsilastirmaServisi
{
    private readonly record struct Duzlem(AraziDugum A, AraziDugum B, AraziDugum C,
        double IkiAlan, double EgimX, double EgimY, int Sira)
    {
        public double Kot(double dx, double dy) => A.Z + EgimX * dx + EgimY * dy;
    }

    public static AraziKarsilastirmasi Hesapla(AraziYuzeyi ilk, AraziYuzeyi son, CancellationToken iptal = default)
    {
        var ilkYuzler = Duzlemler(ilk, out double ilkAlan);
        var sonYuzler = Duzlemler(son, out double sonAlan);
        var sonuc = new AraziKarsilastirmasi { IlkAlanM2 = ilkAlan, SonAlanM2 = sonAlan };
        bool ayniAg = AyniBaglantilar(ilk, son);
        var indeks = ayniAg ? null : new AraziKotIndeksi(son);
        var adaylar = new List<int>();
        for (int i = 0; i < ilkYuzler.Length; i++)
        {
            iptal.ThrowIfCancellationRequested();
            var yuz = ilkYuzler[i];
            if (Math.Abs(yuz.IkiAlan) < 1e-9) continue;
            if (ayniAg)
            {
                var diger = sonYuzler[i];
                FarkaGoreBol(Nokta(yuz.A, diger.A.Z), Nokta(yuz.B, diger.B.Z), Nokta(yuz.C, diger.C.Z), i, i, sonuc);
                continue;
            }
            indeks!.KutuAdaylari(Math.Min(yuz.A.X, Math.Min(yuz.B.X, yuz.C.X)), Math.Min(yuz.A.Y, Math.Min(yuz.B.Y, yuz.C.Y)),
                Math.Max(yuz.A.X, Math.Max(yuz.B.X, yuz.C.X)), Math.Max(yuz.A.Y, Math.Max(yuz.B.Y, yuz.C.Y)), adaylar);
            foreach (int j in adaylar) KesisimiEkle(yuz, sonYuzler[j], sonuc);
        }
        if (sonuc.Parcalar.Count == 0)
            throw new InvalidOperationException("İki yüzeyin ortak alanı yok. Dosyaların koordinat sistemlerini ve birimlerini kontrol edin.");
        return sonuc;
    }

    private static bool AyniBaglantilar(AraziYuzeyi ilk, AraziYuzeyi son)
    {
        if (ilk.Dugumler.Count != son.Dugumler.Count || !ilk.Ucgenler.SequenceEqual(son.Ucgenler)) return false;
        for (int i = 0; i < ilk.Dugumler.Count; i++)
            if (ilk.Dugumler[i].X != son.Dugumler[i].X || ilk.Dugumler[i].Y != son.Dugumler[i].Y) return false;
        return true;
    }

    // A CAD layer can contain duplicate or stacked faces. Such a mesh would count the
    // same plan area twice, even when each individual triangle has a valid gradient.
    internal static void KaynakYuzeyiniDogrula(AraziYuzeyi tin)
    {
        var indeks = new AraziKotIndeksi(tin);
        var planes = Duzlemler(tin, out _);
        var adaylar = new List<int>();
        var kontrol = new AraziKarsilastirmasi();
        for (int i = 0; i < planes.Length; i++)
        {
            var t = planes[i];
            if (Math.Abs(t.IkiAlan) < 1e-9) throw new InvalidDataException("Kaynak TIN sıfır alanlı bir yüz içeriyor.");
            indeks.KutuAdaylari(Math.Min(t.A.X,Math.Min(t.B.X,t.C.X)),Math.Min(t.A.Y,Math.Min(t.B.Y,t.C.Y)),
                Math.Max(t.A.X,Math.Max(t.B.X,t.C.X)),Math.Max(t.A.Y,Math.Max(t.B.Y,t.C.Y)),adaylar);
            foreach (int j in adaylar)
            {
                if (j <= i) continue;
                double once = kontrol.OrtakAlanM2;
                KesisimiEkle(t,planes[j],kontrol);
                if (kontrol.OrtakAlanM2-once > 1e-8)
                    throw new InvalidDataException("CAD yüzleri planda üst üste geliyor. Tek bir zemin yüzeyi seçin.");
            }
        }
    }

    private static Duzlem[] Duzlemler(AraziYuzeyi yuzey, out double alan)
    {
        var yuzler = new Duzlem[yuzey.Ucgenler.Count];
        alan = 0;
        for (int i = 0; i < yuzler.Length; i++)
        {
            var t = yuzey.Ucgenler[i];
            var a = yuzey.Dugumler[t.A]; var b = yuzey.Dugumler[t.B]; var c = yuzey.Dugumler[t.C];
            double bx = b.X-a.X, by = b.Y-a.Y, cx = c.X-a.X, cy = c.Y-a.Y;
            double det = bx*cy - by*cx;
            if (!double.IsFinite(det) || !double.IsFinite(a.Z) || !double.IsFinite(b.Z) || !double.IsFinite(c.Z))
                throw new InvalidDataException("Yüzeyde geçersiz koordinat veya kot var.");
            if (Math.Abs(det) < 1e-9) continue;
            yuzler[i] = new(a,b,c,det,((b.Z-a.Z)*cy-(c.Z-a.Z)*by)/det,(bx*(c.Z-a.Z)-cx*(b.Z-a.Z))/det,i);
            alan += Math.Abs(det)/2;
        }
        if (alan == 0) throw new InvalidDataException("Karşılaştırılacak yüzeyde geçerli arazi üçgeni yok.");
        return yuzler;
    }

    private static KarsilastirmaNoktasi Nokta(AraziDugum p, double sonKot)
        => new(p.X, p.Y, p.Z, Math.Abs(p.Z-sonKot) < 1e-8 ? p.Z : sonKot);

    private static void KesisimiEkle(Duzlem ilk, Duzlem son, AraziKarsilastirmasi sonuc)
    {
        // Clip in local coordinates to retain precision on national survey grids.
        Point2D Local(AraziDugum p) => new(p.X-ilk.A.X, p.Y-ilk.A.Y);
        Span<Point2D> polygon = stackalloc Point2D[10];
        Span<Point2D> scratch = stackalloc Point2D[10];
        polygon[0] = default; polygon[1] = Local(ilk.B); polygon[2] = Local(ilk.C);
        Span<Point2D> edges = stackalloc Point2D[] { Local(son.A), Local(son.B), Local(son.C) };
        int count = 3;
        int yon = Math.Sign(son.IkiAlan);
        for (int k = 0; k < 3 && count >= 3; k++)
        {
            var a = edges[k]; var b = edges[(k+1)%3];
            double dx = b.X-a.X, dy = b.Y-a.Y;
            double Side(Point2D p) => yon * (dx*(p.Y-a.Y)-dy*(p.X-a.X));
            var prev = polygon[count-1];
            double prevSide = Side(prev);
            int output = 0;
            for (int j = 0; j < count; j++)
            {
                var next = polygon[j];
                double side = Side(next);
                if ((side > 0 && prevSide < 0) || (side < 0 && prevSide > 0))
                {
                    double u = prevSide/(prevSide-side);
                    scratch[output++] = new(prev.X+u*(next.X-prev.X), prev.Y+u*(next.Y-prev.Y));
                }
                if (side >= 0) scratch[output++] = next;
                prev = next; prevSide = side;
            }
            scratch[..output].CopyTo(polygon);
            count = output;
        }
        if (count < 3) return;
        var sonBas = edges[0];
        KarsilastirmaNoktasi Kotlar(Point2D p) => Nokta(new(ilk.A.X+p.X, ilk.A.Y+p.Y, ilk.Kot(p.X,p.Y)),
            son.Kot(p.X-sonBas.X,p.Y-sonBas.Y));
        var first = Kotlar(polygon[0]); var previous = Kotlar(polygon[1]);
        for (int j = 2; j < count; j++)
        {
            var next = Kotlar(polygon[j]);
            FarkaGoreBol(first, previous, next, ilk.Sira, son.Sira, sonuc);
            previous = next;
        }
    }

    private static void FarkaGoreBol(KarsilastirmaNoktasi a, KarsilastirmaNoktasi b, KarsilastirmaNoktasi c,
        int ilkUcgen, int sonUcgen, AraziKarsilastirmasi sonuc)
    {
        if (Math.Min(a.Fark, Math.Min(b.Fark,c.Fark)) >= 0 || Math.Max(a.Fark, Math.Max(b.Fark,c.Fark)) <= 0)
        {
            sonuc.Ekle(a,b,c,ilkUcgen,sonUcgen);
            return;
        }
        Span<KarsilastirmaNoktasi> vertices = stackalloc[] { a,b,c };
        Span<KarsilastirmaNoktasi> cut = stackalloc KarsilastirmaNoktasi[4];
        Span<KarsilastirmaNoktasi> fill = stackalloc KarsilastirmaNoktasi[4];
        int cuts = 0, fills = 0;
        var prev = c;
        foreach (var next in vertices)
        {
            if (prev.Fark * next.Fark < 0)
            {
                double u = prev.Fark/(prev.Fark-next.Fark);
                double z = prev.IlkKot + u*(next.IlkKot-prev.IlkKot);
                var zero = new KarsilastirmaNoktasi(prev.X+u*(next.X-prev.X),prev.Y+u*(next.Y-prev.Y),z,z);
                cut[cuts++] = zero; fill[fills++] = zero;
            }
            if (next.Fark >= 0) cut[cuts++] = next;
            if (next.Fark <= 0) fill[fills++] = next;
            prev = next;
        }
        for (int i = 2; i < cuts; i++) sonuc.Ekle(cut[0],cut[i-1],cut[i],ilkUcgen,sonUcgen);
        for (int i = 2; i < fills; i++) sonuc.Ekle(fill[0],fill[i-1],fill[i],ilkUcgen,sonUcgen);
    }
}
