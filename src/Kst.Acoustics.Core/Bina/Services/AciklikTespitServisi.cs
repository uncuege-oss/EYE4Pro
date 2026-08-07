using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Bina.Services;

/// Bir katın DXF verisinden açıklıkları (kapı + pencere) iki yöntemle tespit eder:
/// (A) ARC parametrelerinden kapı yayları, (B) duvar koordinat eksenlerinde boşluk analizi.
/// Manuel eklenen veya iptal edilen kayıtlar yeniden taramada korunur.
public static class AciklikTespitServisi
{
    public sealed class Ayarlar
    {
        public double MinKapiR = 0.50;
        public double MaxKapiR = 1.50;
        public double MinKapiSweepDeg = 60;
        public double MaxKapiSweepDeg = 120;
        public double MaxMenteseWallDistM = 0.15;
        public double DuvarOrtTol = 0.12;
        public double DuvarOrthTol = 0.08;
        public double MinDuvarUzunluk = 0.4;
        public double SegmentBirlestirme = 0.05;
        public double MinAciklikGenisM = 0.60;
        public double MaxAciklikGenisM = 2.50;
        public double KenarTamponM = 0.20;
        public double SoveAramaUcM = 0.30;
        public double MinSoveBoyuM = 0.10;
        public double SoveEksenYakinM = 0.20;
        public double MaxKarsiDuvarM = 1.50;
        public double MinKarsiDuvarM = 0.05;
        public double DedupTolM = 0.30;
    }

    /// Açıklık tespitini çalıştırır. Mevcut listede ManuelEklendi=true veya IptalEdildi=true
    /// olan kayıtlar korunur; otomatik olanlar yenilenir.
    public static List<BinaAciklik> Bul(
        IReadOnlyList<(Point2D A, Point2D B)> duvarSegmentleri,
        IReadOnlyList<(Point2D Merkez, double R, double BasDeg, double BitDeg)> arclar,
        IReadOnlyList<BinaAciklik> mevcut,
        Ayarlar? ayar = null)
    {
        ayar ??= new Ayarlar();

        var sonuc = mevcut
            .Where(a => a.ManuelEklendi || a.IptalEdildi)
            .Select(a => new BinaAciklik
            {
                Id = a.Id, Bas = a.Bas, Bit = a.Bit,
                Tip = a.Tip,
                GenislikM = a.GenislikM,
                YukseklikM = a.YukseklikM,
                EsikYuksekligiM = a.EsikYuksekligiM,
                IptalEdildi = a.IptalEdildi, ManuelEklendi = a.ManuelEklendi, Kaynak = a.Kaynak
            })
            .ToList();

        // (A) ARC tabanlı — yüksek güven
        foreach (var (merkez, r, basDeg, bitDeg) in arclar)
        {
            if (r < ayar.MinKapiR || r > ayar.MaxKapiR) continue;
            double sweep = NormSweep(basDeg, bitDeg);
            if (sweep < ayar.MinKapiSweepDeg || sweep > ayar.MaxKapiSweepDeg) continue;

            var bas = new Point2D(
                merkez.X + r * Math.Cos(basDeg * Math.PI / 180),
                merkez.Y + r * Math.Sin(basDeg * Math.PI / 180));
            var bit = new Point2D(
                merkez.X + r * Math.Cos(bitDeg * Math.PI / 180),
                merkez.Y + r * Math.Sin(bitDeg * Math.PI / 180));

            if (MinSegMesafesi(merkez, duvarSegmentleri) > ayar.MaxMenteseWallDistM) continue;

            double dBas = MinSegMesafesi(bas, duvarSegmentleri);
            double dBit = MinSegMesafesi(bit, duvarSegmentleri);
            var aciklikUcu = dBas <= dBit ? bas : bit;

            if (!CakisiyorMu(sonuc, merkez, aciklikUcu, ayar.DedupTolM))
            {
                double genis = Mesafe(merkez, aciklikUcu);
                sonuc.Add(new BinaAciklik { Bas = merkez, Bit = aciklikUcu,
                    Tip = BinaAciklikTipi.Kapi, Kaynak = "arc", GenislikM = genis,
                    YukseklikM = BinaAciklik.VarsayilanYukseklik(BinaAciklikTipi.Kapi) });
            }
        }

        // (B) Duvar boşluk analizi
        foreach (var aciklik in DuvarBosluklariBul(duvarSegmentleri, ayar))
        {
            if (!CakisiyorMu(sonuc, aciklik.Bas, aciklik.Bit, ayar.DedupTolM))
                sonuc.Add(aciklik);
        }

        return sonuc;
    }

    private static IEnumerable<BinaAciklik> DuvarBosluklariBul(
        IReadOnlyList<(Point2D A, Point2D B)> segs, Ayarlar ayar)
    {
        foreach (var aciklik in EksendeBosluk(segs, yatay: true, ayar))
            yield return aciklik;
        foreach (var aciklik in EksendeBosluk(segs, yatay: false, ayar))
            yield return aciklik;
    }

    private static bool SoveVarMi(
        IReadOnlyList<(Point2D A, Point2D B)> segs, bool yatayAciklik,
        double ucKonum, double eksen, Ayarlar ayar)
    {
        foreach (var (a, b) in segs)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < ayar.MinSoveBoyuM) continue;

            if (yatayAciklik)
            {
                if (Math.Abs(dx) / len > ayar.DuvarOrthTol) continue;
                double sx = (a.X + b.X) * 0.5;
                if (Math.Abs(sx - ucKonum) > ayar.SoveAramaUcM) continue;
                double y0 = Math.Min(a.Y, b.Y), y1 = Math.Max(a.Y, b.Y);
                if (Math.Abs(y0 - eksen) <= ayar.SoveEksenYakinM ||
                    Math.Abs(y1 - eksen) <= ayar.SoveEksenYakinM)
                    return true;
            }
            else
            {
                if (Math.Abs(dy) / len > ayar.DuvarOrthTol) continue;
                double sy = (a.Y + b.Y) * 0.5;
                if (Math.Abs(sy - ucKonum) > ayar.SoveAramaUcM) continue;
                double x0 = Math.Min(a.X, b.X), x1 = Math.Max(a.X, b.X);
                if (Math.Abs(x0 - eksen) <= ayar.SoveEksenYakinM ||
                    Math.Abs(x1 - eksen) <= ayar.SoveEksenYakinM)
                    return true;
            }
        }
        return false;
    }

    private static bool KarsiDuvarVarMi(
        IReadOnlyList<(Point2D A, Point2D B)> segs, bool yatayAciklik,
        double ortaKonum, double eksen, Ayarlar ayar)
    {
        foreach (var (a, b) in segs)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < ayar.MinDuvarUzunluk) continue;

            if (yatayAciklik)
            {
                if (Math.Abs(dy) / len > ayar.DuvarOrthTol) continue;
                double y = (a.Y + b.Y) * 0.5;
                double dist = Math.Abs(y - eksen);
                if (dist < ayar.MinKarsiDuvarM || dist > ayar.MaxKarsiDuvarM) continue;
                double x0 = Math.Min(a.X, b.X), x1 = Math.Max(a.X, b.X);
                if (ortaKonum < x0 - 0.10 || ortaKonum > x1 + 0.10) continue;
                return true;
            }
            else
            {
                if (Math.Abs(dx) / len > ayar.DuvarOrthTol) continue;
                double x = (a.X + b.X) * 0.5;
                double dist = Math.Abs(x - eksen);
                if (dist < ayar.MinKarsiDuvarM || dist > ayar.MaxKarsiDuvarM) continue;
                double y0 = Math.Min(a.Y, b.Y), y1 = Math.Max(a.Y, b.Y);
                if (ortaKonum < y0 - 0.10 || ortaKonum > y1 + 0.10) continue;
                return true;
            }
        }
        return false;
    }

    private static IEnumerable<BinaAciklik> EksendeBosluk(
        IReadOnlyList<(Point2D A, Point2D B)> segs, bool yatay, Ayarlar ayar)
    {
        var aday = new List<(double Eksen, double Bas, double Bit)>();
        foreach (var (a, b) in segs)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < ayar.MinDuvarUzunluk) continue;

            if (yatay)
            {
                if (Math.Abs(dy) / len > ayar.DuvarOrthTol) continue;
                aday.Add(((a.Y + b.Y) * 0.5, Math.Min(a.X, b.X), Math.Max(a.X, b.X)));
            }
            else
            {
                if (Math.Abs(dx) / len > ayar.DuvarOrthTol) continue;
                aday.Add(((a.X + b.X) * 0.5, Math.Min(a.Y, b.Y), Math.Max(a.Y, b.Y)));
            }
        }
        if (aday.Count == 0) yield break;

        aday.Sort((a, b) => a.Eksen.CompareTo(b.Eksen));
        var gruplar = new List<List<(double Eksen, double Bas, double Bit)>>();
        var aktif = new List<(double Eksen, double Bas, double Bit)> { aday[0] };
        double aktifEnSon = aday[0].Eksen;
        for (int i = 1; i < aday.Count; i++)
        {
            if (aday[i].Eksen - aktifEnSon <= ayar.DuvarOrtTol)
            {
                aktif.Add(aday[i]);
                aktifEnSon = aday[i].Eksen;
            }
            else
            {
                gruplar.Add(aktif);
                aktif = new List<(double, double, double)> { aday[i] };
                aktifEnSon = aday[i].Eksen;
            }
        }
        gruplar.Add(aktif);

        foreach (var grup in gruplar)
        {
            var listesi = grup.OrderBy(s => s.Bas).ToList();
            if (listesi.Count == 0) continue;

            var kosumlar = new List<(double Bas, double Bit)>();
            double kosBas = listesi[0].Bas, kosBit = listesi[0].Bit;
            for (int i = 1; i < listesi.Count; i++)
            {
                var s = listesi[i];
                if (s.Bas - kosBit <= ayar.SegmentBirlestirme)
                {
                    if (s.Bit > kosBit) kosBit = s.Bit;
                }
                else
                {
                    kosumlar.Add((kosBas, kosBit));
                    kosBas = s.Bas; kosBit = s.Bit;
                }
            }
            kosumlar.Add((kosBas, kosBit));

            double eksenDegeri = grup.Average(s => s.Eksen);
            for (int i = 0; i < kosumlar.Count - 1; i++)
            {
                double gapBas = kosumlar[i].Bit;
                double gapBit = kosumlar[i + 1].Bas;
                double genislik = gapBit - gapBas;
                if (genislik < ayar.MinAciklikGenisM || genislik > ayar.MaxAciklikGenisM) continue;

                double solUzun = kosumlar[i].Bit - kosumlar[i].Bas;
                double sagUzun = kosumlar[i + 1].Bit - kosumlar[i + 1].Bas;
                if (solUzun < ayar.KenarTamponM || sagUzun < ayar.KenarTamponM) continue;

                if (!SoveVarMi(segs, yatay, gapBas, eksenDegeri, ayar)) continue;
                if (!SoveVarMi(segs, yatay, gapBit, eksenDegeri, ayar)) continue;
                double orta = (gapBas + gapBit) * 0.5;
                if (!KarsiDuvarVarMi(segs, yatay, orta, eksenDegeri, ayar)) continue;

                Point2D bas, bit;
                if (yatay)
                {
                    bas = new Point2D(gapBas, eksenDegeri);
                    bit = new Point2D(gapBit, eksenDegeri);
                }
                else
                {
                    bas = new Point2D(eksenDegeri, gapBas);
                    bit = new Point2D(eksenDegeri, gapBit);
                }
                yield return new BinaAciklik { Bas = bas, Bit = bit,
                    Tip = BinaAciklikTipi.Kapi, Kaynak = "gap", GenislikM = genislik,
                    YukseklikM = BinaAciklik.VarsayilanYukseklik(BinaAciklikTipi.Kapi) };
            }
        }
    }

    private static double NormSweep(double basDeg, double bitDeg)
    {
        double s = bitDeg - basDeg;
        while (s < 0) s += 360;
        while (s > 360) s -= 360;
        return s;
    }

    private static double MinSegMesafesi(Point2D p, IReadOnlyList<(Point2D A, Point2D B)> segs)
    {
        double min = double.MaxValue;
        foreach (var (a, b) in segs)
        {
            double d = PoligonYardimci.SegmentMesafesi(p, a, b);
            if (d < min) min = d;
            if (min < 1e-6) return min;
        }
        return min;
    }

    private static bool CakisiyorMu(List<BinaAciklik> liste, Point2D bas, Point2D bit, double tol)
    {
        var orta = new Point2D((bas.X + bit.X) * 0.5, (bas.Y + bit.Y) * 0.5);
        foreach (var a in liste)
        {
            if (PoligonYardimci.SegmentMesafesi(orta, a.Bas, a.Bit) < tol)
                return true;
        }
        return false;
    }

    private static double Mesafe(Point2D a, Point2D b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
