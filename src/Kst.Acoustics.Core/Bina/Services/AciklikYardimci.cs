using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Bina.Services;

/// Açıklık (kapı/pencere) listesi üzerinde yardımcı işlemler — BinaKat modeli ile çalışır.
public static class AciklikYardimci
{
    /// Açıklık orta noktası herhangi bir oda poligonunun içinde veya tolM mesafesi içindeyse true.
    public static bool AciklikYuzeyDeOlu(BinaAciklik aciklik, BinaKat kat, double tolM = 0.50)
    {
        if (kat.Odalar.Count == 0) return true;
        var orta = new Point2D(
            (aciklik.Bas.X + aciklik.Bit.X) * 0.5,
            (aciklik.Bas.Y + aciklik.Bit.Y) * 0.5);

        foreach (var oda in kat.Odalar)
        {
            var poly = oda.Poligon;
            if (poly.Count < 3) continue;
            if (PoligonYardimci.IsPointInside(orta, poly)) return true;
            for (int i = 0; i < poly.Count; i++)
            {
                if (PoligonYardimci.SegmentMesafesi(orta, poly[i], poly[(i + 1) % poly.Count]) < tolM)
                    return true;
            }
        }
        return false;
    }

    /// Oda poligonlarının dışında kalan otomatik açıklıkları siler.
    public static void DisindaKalanlariSil(BinaKat kat)
    {
        if (kat.Odalar.Count == 0) return;
        kat.Acikliklar.RemoveAll(a =>
            !a.ManuelEklendi && !a.IptalEdildi && !AciklikYuzeyDeOlu(a, kat));
    }

    /// Bir açıklığı komşu oda kenarına yansıtır — kapı iki odaya hizmet eder.
    /// Eklenen açıklığı (veya null) döndürür.
    public static BinaAciklik? KomsuDuvaraYansit(BinaKat kat, BinaAciklik kaynak)
    {
        if (kat.Odalar.Count < 2) return null;

        const double paralelTol = 0.25;
        const double minDuvar   = 0.03;
        const double maxDuvar   = 0.80;
        const double dupTol     = 0.25;
        const double kaynakKenarTol = 0.08;

        var bas = kaynak.Bas;
        var bit = kaynak.Bit;
        double dx = bit.X - bas.X, dy = bit.Y - bas.Y;
        double uzunluk = Math.Sqrt(dx * dx + dy * dy);
        if (uzunluk < 1e-6) return null;
        double udx = dx / uzunluk, udy = dy / uzunluk;
        var orta = new Point2D((bas.X + bit.X) * 0.5, (bas.Y + bit.Y) * 0.5);
        var kaynakOdaIds = KaynakOdalariBul(kat, kaynak, paralelTol, kaynakKenarTol);

        Point2D bestA = default, bestB = default;
        double bestDist = double.MaxValue;
        bool bulundu = false;

        foreach (var oda in kat.Odalar)
        {
            if (kaynakOdaIds.Contains(oda.Id)) continue;

            var poly = oda.Poligon;
            if (poly.Count < 2) continue;
            for (int i = 0; i < poly.Count; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % poly.Count];
                double edx = b.X - a.X, edy = b.Y - a.Y;
                double elen = Math.Sqrt(edx * edx + edy * edy);
                if (elen < 1e-6) continue;
                double uedx = edx / elen, uedy = edy / elen;

                if (Math.Abs(udx * uedy - udy * uedx) > paralelTol) continue;

                double mesafe = PoligonYardimci.SegmentMesafesi(orta, a, b);
                if (mesafe < minDuvar || mesafe > maxDuvar) continue;

                var proj = PoligonYardimci.SegmentProje(orta, a, b);
                if (PoligonYardimci.SegmentMesafesi(proj, a, b) > 1e-6) continue;
                double dpa = Math.Sqrt((proj.X - a.X) * (proj.X - a.X) + (proj.Y - a.Y) * (proj.Y - a.Y));
                double dpb = Math.Sqrt((proj.X - b.X) * (proj.X - b.X) + (proj.Y - b.Y) * (proj.Y - b.Y));
                if (dpa < uzunluk * 0.4 || dpb < uzunluk * 0.4) continue;

                if (mesafe < bestDist) { bestDist = mesafe; bestA = a; bestB = b; bulundu = true; }
            }
        }

        if (!bulundu) return null;

        var yBas  = PoligonYardimci.SegmentProje(bas, bestA, bestB);
        var yBit  = PoligonYardimci.SegmentProje(bit, bestA, bestB);
        var yOrta = new Point2D((yBas.X + yBit.X) * 0.5, (yBas.Y + yBit.Y) * 0.5);

        foreach (var a in kat.Acikliklar)
        {
            if (a.IptalEdildi || ReferenceEquals(a, kaynak)) continue;
            var aOrta = new Point2D((a.Bas.X + a.Bit.X) * 0.5, (a.Bas.Y + a.Bit.Y) * 0.5);
            double d = Math.Sqrt((aOrta.X - yOrta.X) * (aOrta.X - yOrta.X) + (aOrta.Y - yOrta.Y) * (aOrta.Y - yOrta.Y));
            if (d < dupTol) return null;
        }

        double yeniGenis = Math.Sqrt((yBit.X - yBas.X) * (yBit.X - yBas.X) + (yBit.Y - yBas.Y) * (yBit.Y - yBas.Y));
        var yansima = new BinaAciklik
        {
            Bas = yBas, Bit = yBit, Tip = kaynak.Tip,
            GenislikM = yeniGenis,
            YukseklikM = kaynak.YukseklikM,
            EsikYuksekligiM = kaynak.EsikYuksekligiM,
            EsikYuksekligiBelirtildi = kaynak.EsikYuksekligiBelirtildi,
            ManuelEklendi = kaynak.ManuelEklendi,
            Kaynak = (kaynak.Kaynak ?? "") + "-yansima"
        };
        kat.Acikliklar.Add(yansima);
        return yansima;
    }

    /// Seçilen açıklığın komşu oda kenarındaki eşini/eşlerini geometriye göre siler.
    public static int KomsuAcikliklariSil(BinaKat kat, BinaAciklik kaynak)
    {
        var silinecekler = kat.Acikliklar
            .Where(a => !ReferenceEquals(a, kaynak) && a.Id != kaynak.Id && KomsuAciklikEslesir(kaynak, a))
            .ToList();

        foreach (var a in silinecekler)
            kat.Acikliklar.Remove(a);

        return silinecekler.Count;
    }

    /// Kattaki tüm aktif açıklıklar için komşu duvar yansımalarını tamamlar.
    public static int KomsuDuvarlariTamamla(BinaKat kat)
    {
        if (kat.Odalar.Count < 2) return 0;
        var mevcutlar = kat.Acikliklar.Where(a => !a.IptalEdildi).ToList();
        int eklenen = 0;
        foreach (var a in mevcutlar)
            if (KomsuDuvaraYansit(kat, a) != null) eklenen++;
        return eklenen;
    }

    private static HashSet<string> KaynakOdalariBul(
        BinaKat kat,
        BinaAciklik kaynak,
        double paralelTol,
        double kenarTol)
    {
        var sonuc = new HashSet<string>();
        double dx = kaynak.Bit.X - kaynak.Bas.X;
        double dy = kaynak.Bit.Y - kaynak.Bas.Y;
        double uzunluk = Math.Sqrt(dx * dx + dy * dy);
        if (uzunluk < 1e-6) return sonuc;

        double udx = dx / uzunluk, udy = dy / uzunluk;
        var orta = new Point2D((kaynak.Bas.X + kaynak.Bit.X) * 0.5, (kaynak.Bas.Y + kaynak.Bit.Y) * 0.5);

        foreach (var oda in kat.Odalar)
        {
            var poly = oda.Poligon;
            if (poly.Count < 2) continue;
            for (int i = 0; i < poly.Count; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % poly.Count];
                double edx = b.X - a.X, edy = b.Y - a.Y;
                double elen = Math.Sqrt(edx * edx + edy * edy);
                if (elen < 1e-6) continue;
                double uedx = edx / elen, uedy = edy / elen;
                if (Math.Abs(udx * uedy - udy * uedx) > paralelTol) continue;
                if (PoligonYardimci.SegmentMesafesi(orta, a, b) > kenarTol) continue;

                var proj = PoligonYardimci.SegmentProje(orta, a, b);
                if (PoligonYardimci.SegmentMesafesi(proj, a, b) > 1e-6) continue;
                sonuc.Add(oda.Id);
                break;
            }
        }

        return sonuc;
    }

    private static bool KomsuAciklikEslesir(BinaAciklik kaynak, BinaAciklik aday)
    {
        const double paralelTol = 0.25;
        const double minDuvar = 0.03;
        const double maxDuvar = 0.80;

        if (kaynak.Tip != aday.Tip) return false;

        double sdx = kaynak.Bit.X - kaynak.Bas.X;
        double sdy = kaynak.Bit.Y - kaynak.Bas.Y;
        double adx = aday.Bit.X - aday.Bas.X;
        double ady = aday.Bit.Y - aday.Bas.Y;
        double suz = Math.Sqrt(sdx * sdx + sdy * sdy);
        double auz = Math.Sqrt(adx * adx + ady * ady);
        if (suz < 1e-6 || auz < 1e-6) return false;

        double sudx = sdx / suz, sudy = sdy / suz;
        double audx = adx / auz, audy = ady / auz;
        if (Math.Abs(sudx * audy - sudy * audx) > paralelTol) return false;

        double uzunlukTol = Math.Max(0.15, suz * 0.25);
        if (Math.Abs(suz - auz) > uzunlukTol) return false;

        var sOrta = new Point2D((kaynak.Bas.X + kaynak.Bit.X) * 0.5, (kaynak.Bas.Y + kaynak.Bit.Y) * 0.5);
        var aOrta = new Point2D((aday.Bas.X + aday.Bit.X) * 0.5, (aday.Bas.Y + aday.Bit.Y) * 0.5);
        double mdx = aOrta.X - sOrta.X;
        double mdy = aOrta.Y - sOrta.Y;
        double ortaMesafe = Math.Sqrt(mdx * mdx + mdy * mdy);
        if (ortaMesafe < minDuvar || ortaMesafe > maxDuvar) return false;

        double eksenSapma = Math.Abs(mdx * sudx + mdy * sudy);
        double eksenTol = Math.Max(0.25, suz * 0.30);
        return eksenSapma <= eksenTol;
    }
}
