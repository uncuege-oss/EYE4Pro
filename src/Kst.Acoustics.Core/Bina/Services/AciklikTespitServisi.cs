using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
                IptalEdildi = a.IptalEdildi, ManuelEklendi = a.ManuelEklendi, Kaynak = a.Kaynak,
                EkAlanlar = EkAlanlariKopyala(a.EkAlanlar)
            })
            .ToList();
        var mevcutOtomatik = mevcut
            .Where(a => !a.ManuelEklendi && !a.IptalEdildi &&
                        !(a.Kaynak?.Contains("-yansima", StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();
        var eslesenMevcut = new HashSet<BinaAciklik>();
        var yeniOtomatik = new List<BinaAciklik>();
        var cakismaListesi = new List<BinaAciklik>(sonuc);
        var segmentIndeksi = new SegmentUzamsalIndeksi(duvarSegmentleri);

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

            if (segmentIndeksi.MinMesafe(merkez, ayar.MaxKapiR + ayar.MaxMenteseWallDistM) > ayar.MaxMenteseWallDistM)
                continue;

            double aramaYaricapi = ayar.MaxKapiR + ayar.MaxMenteseWallDistM;
            double dBas = segmentIndeksi.MinMesafe(bas, aramaYaricapi);
            double dBit = segmentIndeksi.MinMesafe(bit, aramaYaricapi);
            var aciklikUcu = dBas <= dBit ? bas : bit;

            if (!CakisiyorMu(cakismaListesi, merkez, aciklikUcu, ayar.DedupTolM))
            {
                double genis = Mesafe(merkez, aciklikUcu);
                var yeni = new BinaAciklik { Bas = merkez, Bit = aciklikUcu,
                    Tip = BinaAciklikTipi.Kapi, Kaynak = "arc", GenislikM = genis,
                    YukseklikM = BinaAciklik.VarsayilanYukseklik(BinaAciklikTipi.Kapi) };
                yeniOtomatik.Add(yeni);
                cakismaListesi.Add(yeni);
            }
        }

        // (B) Duvar boşluk analizi
        foreach (var aciklik in DuvarBosluklariBul(duvarSegmentleri, segmentIndeksi, ayar))
        {
            if (!CakisiyorMu(cakismaListesi, aciklik.Bas, aciklik.Bit, ayar.DedupTolM))
            {
                yeniOtomatik.Add(aciklik);
                cakismaListesi.Add(aciklik);
            }
        }

        // Yeniden taramada aynı fiziksel açıklığın kimliği ve kullanıcı
        // tarafından düzenlenmiş tip/yükseklik/eşik alanları kararlı kalır.
        foreach (var yeni in yeniOtomatik)
        {
            var eski = EnIyiMevcutEslesme(yeni, mevcutOtomatik, eslesenMevcut, ayar.DedupTolM);
            if (eski is not null)
            {
                yeni.Id = eski.Id;
                yeni.Tip = eski.Tip;
                if (eski.YukseklikM > 0) yeni.YukseklikM = eski.YukseklikM;
                yeni.EsikYuksekligiM = eski.EsikYuksekligiM;
                yeni.EkAlanlar = EkAlanlariKopyala(eski.EkAlanlar);
                eslesenMevcut.Add(eski);
            }
            sonuc.Add(yeni);
        }

        return sonuc;
    }

    private static Dictionary<string, JsonElement>? EkAlanlariKopyala(
        Dictionary<string, JsonElement>? alanlar)
        => alanlar?.ToDictionary(kv => kv.Key, kv => kv.Value.Clone(), StringComparer.Ordinal);

    private static IEnumerable<BinaAciklik> DuvarBosluklariBul(
        IReadOnlyList<(Point2D A, Point2D B)> segs, SegmentUzamsalIndeksi indeks, Ayarlar ayar)
    {
        foreach (var aciklik in EksendeBosluk(segs, indeks, yatay: true, ayar))
            yield return aciklik;
        foreach (var aciklik in EksendeBosluk(segs, indeks, yatay: false, ayar))
            yield return aciklik;
    }

    private static bool SoveVarMi(
        SegmentUzamsalIndeksi indeks, bool yatayAciklik,
        double ucKonum, double eksen, Ayarlar ayar)
    {
        double minX, minY, maxX, maxY;
        if (yatayAciklik)
        {
            minX = ucKonum - ayar.SoveAramaUcM;
            maxX = ucKonum + ayar.SoveAramaUcM;
            minY = eksen - ayar.SoveEksenYakinM;
            maxY = eksen + ayar.SoveEksenYakinM;
        }
        else
        {
            minX = eksen - ayar.SoveEksenYakinM;
            maxX = eksen + ayar.SoveEksenYakinM;
            minY = ucKonum - ayar.SoveAramaUcM;
            maxY = ucKonum + ayar.SoveAramaUcM;
        }

        foreach (var (a, b) in indeks.Sorgula(minX, minY, maxX, maxY))
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
        SegmentUzamsalIndeksi indeks, bool yatayAciklik,
        double ortaKonum, double eksen, Ayarlar ayar)
    {
        const double uzanti = 0.10;
        var adaylar = yatayAciklik
            ? indeks.Sorgula(
                ortaKonum - uzanti, eksen - ayar.MaxKarsiDuvarM,
                ortaKonum + uzanti, eksen + ayar.MaxKarsiDuvarM)
            : indeks.Sorgula(
                eksen - ayar.MaxKarsiDuvarM, ortaKonum - uzanti,
                eksen + ayar.MaxKarsiDuvarM, ortaKonum + uzanti);

        foreach (var (a, b) in adaylar)
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
        IReadOnlyList<(Point2D A, Point2D B)> segs, SegmentUzamsalIndeksi indeks,
        bool yatay, Ayarlar ayar)
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

                if (!SoveVarMi(indeks, yatay, gapBas, eksenDegeri, ayar)) continue;
                if (!SoveVarMi(indeks, yatay, gapBit, eksenDegeri, ayar)) continue;
                double orta = (gapBas + gapBit) * 0.5;
                if (!KarsiDuvarVarMi(indeks, yatay, orta, eksenDegeri, ayar)) continue;

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

    private static BinaAciklik? EnIyiMevcutEslesme(
        BinaAciklik yeni,
        IReadOnlyList<BinaAciklik> mevcut,
        HashSet<BinaAciklik> kullanilan,
        double tolerans)
    {
        double esik = Math.Max(0.15, tolerans);
        var yeniOrta = OrtaNokta(yeni);
        BinaAciklik? enIyi = null;
        double enIyiPuan = double.MaxValue;

        foreach (var eski in mevcut)
        {
            if (kullanilan.Contains(eski)) continue;
            double ortaUzaklik = Mesafe(yeniOrta, OrtaNokta(eski));
            if (ortaUzaklik > esik) continue;

            double ayniYon = Mesafe(yeni.Bas, eski.Bas) + Mesafe(yeni.Bit, eski.Bit);
            double tersYon = Mesafe(yeni.Bas, eski.Bit) + Mesafe(yeni.Bit, eski.Bas);
            double ucOrtalamasi = Math.Min(ayniYon, tersYon) * 0.5;
            if (ucOrtalamasi > esik * 1.5) continue;

            double uzunlukFarki = Math.Abs(
                Mesafe(yeni.Bas, yeni.Bit) - Mesafe(eski.Bas, eski.Bit));
            double puan = ortaUzaklik + ucOrtalamasi * 0.5 + uzunlukFarki * 0.25;
            if (puan >= enIyiPuan) continue;
            enIyiPuan = puan;
            enIyi = eski;
        }

        return enIyi;
    }

    private static Point2D OrtaNokta(BinaAciklik a)
        => new((a.Bas.X + a.Bit.X) * 0.5, (a.Bas.Y + a.Bit.Y) * 0.5);

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

    /// <summary>
    /// Çok sayıda DXF segmentinde nokta/yakın çevre sorgularını tüm listeyi
    /// dolaşmadan yapabilmek için metre tabanlı sabit hücreli uzamsal indeks.
    /// Çok geniş segmentler hücreleri şişirmemek için ayrı listede tutulur.
    /// </summary>
    private sealed class SegmentUzamsalIndeksi
    {
        private const double HucreM = 1.0;
        private const int EnCokHucre = 4096;
        private const long EnCokSorguHucresi = 100_000;
        private readonly IReadOnlyList<(Point2D A, Point2D B)> _segmentler;
        private readonly Dictionary<(int X, int Y), List<int>> _hucreler = new();
        private readonly List<int> _genisSegmentler = new();

        public SegmentUzamsalIndeksi(IReadOnlyList<(Point2D A, Point2D B)> segmentler)
        {
            _segmentler = segmentler;
            for (int i = 0; i < segmentler.Count; i++)
            {
                var (a, b) = segmentler[i];
                if (!Sonlu(a) || !Sonlu(b)) continue;
                int minX = Hucre(Math.Min(a.X, b.X));
                int maxX = Hucre(Math.Max(a.X, b.X));
                int minY = Hucre(Math.Min(a.Y, b.Y));
                int maxY = Hucre(Math.Max(a.Y, b.Y));
                long xSayisi = (long)maxX - minX + 1;
                long ySayisi = (long)maxY - minY + 1;
                if (xSayisi > EnCokHucre || ySayisi > EnCokHucre ||
                    xSayisi > EnCokHucre / Math.Max(1, ySayisi))
                {
                    _genisSegmentler.Add(i);
                    continue;
                }

                for (int x = minX; x <= maxX; x++)
                for (int y = minY; y <= maxY; y++)
                {
                    var anahtar = (x, y);
                    if (!_hucreler.TryGetValue(anahtar, out var liste))
                        _hucreler[anahtar] = liste = new List<int>();
                    liste.Add(i);
                }
            }
        }

        public double MinMesafe(Point2D p, double aramaYaricapi)
        {
            if (!Sonlu(p) || !double.IsFinite(aramaYaricapi) || aramaYaricapi < 0)
                return double.MaxValue;

            double min = double.MaxValue;
            foreach (var (a, b) in Sorgula(
                         p.X - aramaYaricapi, p.Y - aramaYaricapi,
                         p.X + aramaYaricapi, p.Y + aramaYaricapi))
            {
                double d = PoligonYardimci.SegmentMesafesi(p, a, b);
                if (d < min) min = d;
                if (min < 1e-9) break;
            }
            return min;
        }

        public IEnumerable<(Point2D A, Point2D B)> Sorgula(
            double minX, double minY, double maxX, double maxY)
        {
            if (!double.IsFinite(minX) || !double.IsFinite(minY) ||
                !double.IsFinite(maxX) || !double.IsFinite(maxY))
                yield break;

            if (minX > maxX) (minX, maxX) = (maxX, minX);
            if (minY > maxY) (minY, maxY) = (maxY, minY);

            int ilkX = Hucre(minX), sonX = Hucre(maxX);
            int ilkY = Hucre(minY), sonY = Hucre(maxY);
            long xSayisi = (long)sonX - ilkX + 1;
            long ySayisi = (long)sonY - ilkY + 1;
            if (xSayisi > EnCokSorguHucresi || ySayisi > EnCokSorguHucresi ||
                xSayisi > EnCokSorguHucresi / Math.Max(1, ySayisi))
            {
                // Aşırı büyük fakat sonlu koordinat/sorguda milyarlarca hücreyi
                // dolaşmak yerine güvenli O(n) geri dönüşü kullan.
                for (int i = 0; i < _segmentler.Count; i++)
                {
                    var s = _segmentler[i];
                    if (Sonlu(s.A) && Sonlu(s.B)) yield return s;
                }
                yield break;
            }

            var gorulen = new HashSet<int>();
            for (int x = ilkX; x <= sonX; x++)
            for (int y = ilkY; y <= sonY; y++)
            {
                if (!_hucreler.TryGetValue((x, y), out var liste)) continue;
                foreach (int i in liste)
                    if (gorulen.Add(i)) yield return _segmentler[i];
            }

            foreach (int i in _genisSegmentler)
                if (gorulen.Add(i)) yield return _segmentler[i];
        }

        private static int Hucre(double deger)
        {
            double hucre = Math.Floor(deger / HucreM);
            if (hucre <= int.MinValue + 1.0) return int.MinValue + 1;
            if (hucre >= int.MaxValue - 1.0) return int.MaxValue - 1;
            return (int)hucre;
        }

        private static bool Sonlu(Point2D p)
            => double.IsFinite(p.X) && double.IsFinite(p.Y);
    }
}
