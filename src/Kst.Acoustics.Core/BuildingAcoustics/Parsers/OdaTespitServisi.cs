using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.BuildingAcoustics.Parsers;

public class OdaTespitServisi
{
    private const double MinWallGap = 0.00;       // aynı çizgide çizilmiş eski oda sınırlarını da destekle
    private const double MaxWallGap = 0.80;       // bodrum/strüktürel duvarlar 60–80 cm olabilir; koridor (>=1 m) hariç tutulur
    private const double ParallelDotTolerance = 0.985;
    private const double MinSharedLength = 0.10;  // M6 — 10 cm: dar geçiş/sövelerin gerçek bölmelerini kaybetme
    private const double OverlapRatio = 0.10;     // örtüşme, kısa duvarın en az %10'u kadar olmalı
    private const double SplitGapEps = 0.05;      // 5 cm altındaki bölünme boşluklarını yutma

    /// <summary>
    /// Mekanlar arasındaki ortak duvarları (komşulukları) tespit eder.
    /// Her poligon kenarı, birden fazla komşuyla buluşabilirse ayrı bölme parçalarına ayrılır.
    /// Önceki çağrıdan kalan YapiElemani/Acikliklar geometrik yakınlık ile aktarılır.
    /// </summary>
    public void KomsuluklariTespitEt(IList<Mekan> mekanlar)
    {
        // 1. State snapshot — YapiElemani ve Acikliklar'ı koru
        var preserved = new Dictionary<Mekan, List<KorunanBolmeDurumu>>();
        foreach (var m in mekanlar)
        {
            preserved[m] = m.Bolmeler.Select(b => new KorunanBolmeDurumu
            {
                Original = b,
                Merkez = OrtaNokta(b.Baslangic, b.Bitis),
                Baslangic = b.Baslangic,
                Bitis = b.Bitis,
                AliciMekanId = b.AliciMekan?.Id,
                YapiElemani = b.YapiElemani,
                Acikliklar = b.Acikliklar.ToList(),
                Menfezler = b.Menfezler.ToList(),
                Junctions = b.Junctions.ToList()
            }).ToList();
        }

        // 2. Her Mekan için poligondan ham kenar listesi üret
        var kenarlar = new Dictionary<Mekan, List<PoligonKenari>>();
        foreach (var m in mekanlar)
            kenarlar[m] = PoligondanKenarUret(m);

        // 3. Her kenar için eşleşme listesi
        var eslesmelerKenara = new Dictionary<PoligonKenari, List<KenarEslesmesi>>();
        foreach (var liste in kenarlar.Values)
            foreach (var e in liste)
                eslesmelerKenara[e] = new List<KenarEslesmesi>();

        // 4. Çift yönlü kenar-kenar eşleştirme
        for (int i = 0; i < mekanlar.Count; i++)
        {
            for (int j = i + 1; j < mekanlar.Count; j++)
            {
                var m1 = mekanlar[i];
                var m2 = mekanlar[j];

                foreach (var e1 in kenarlar[m1])
                {
                    foreach (var e2 in kenarlar[m2])
                    {
                        if (TryEslestir(e1, e2, out var es1, out var es2))
                        {
                            es1.Komsu = m2;
                            es2.Komsu = m1;
                            eslesmelerKenara[e1].Add(es1);
                            eslesmelerKenara[e2].Add(es2);
                        }
                    }
                }
            }
        }

        // 5. Her Mekan için Bolmeler'i yeniden inşa et
        foreach (var m in mekanlar)
        {
            var yeniBolmeler = new List<Bolme>();
            foreach (var kenar in kenarlar[m])
            {
                yeniBolmeler.AddRange(KenardanBolmeUret(m, kenar, eslesmelerKenara[kenar]));
            }

            // Eski state'i geometrik en yakın eşleşmeyle aktar
            DurumuAktar(yeniBolmeler, preserved[m]);

            m.Bolmeler.Clear();
            foreach (var b in yeniBolmeler)
                m.Bolmeler.Add(b);
        }
    }

    private static List<PoligonKenari> PoligondanKenarUret(Mekan m)
    {
        var liste = new List<PoligonKenari>();
        if (m.Poligon == null || m.Poligon.Count < 2) return liste;
        for (int i = 0; i < m.Poligon.Count; i++)
        {
            var a = m.Poligon[i];
            var b = m.Poligon[(i + 1) % m.Poligon.Count];
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            if (dx * dx + dy * dy < 1e-12) continue; // sıfır uzunluklu kenarları atla
            liste.Add(new PoligonKenari(m, i, a, b));
        }
        return liste;
    }

    private static List<Bolme> KenardanBolmeUret(Mekan m, PoligonKenari kenar, List<KenarEslesmesi> eslesmeler)
    {
        var sonuc = new List<Bolme>();
        var vx = kenar.B.X - kenar.A.X;
        var vy = kenar.B.Y - kenar.A.Y;
        var len = Math.Sqrt(vx * vx + vy * vy);
        if (len < 1e-9) return sonuc;

        var tx = vx / len;
        var ty = vy / len;

        if (eslesmeler.Count == 0)
        {
            sonuc.Add(YeniDuvarBolmesi(m, kenar.A, kenar.B, len));
            return sonuc;
        }

        // T0'a göre sırala, örtüşen eşleşmeleri tek tane bırak (ilki kalır)
        var sirali = eslesmeler.OrderBy(e => e.T0).ToList();
        var birlestirilmis = new List<KenarEslesmesi>();
        double sonT1 = double.NegativeInfinity;
        foreach (var es in sirali)
        {
            if (es.T0 + SplitGapEps >= sonT1)
            {
                birlestirilmis.Add(es);
                sonT1 = es.T1;
            }
            // örtüşen ek eşleşmeleri sessizce at: aynı kenarda iki komşu tam üst üste düşmemeli
        }

        double imlec = 0.0;
        foreach (var es in birlestirilmis)
        {
            // Eşleşme öncesi dış duvar parçası
            if (es.T0 > imlec + SplitGapEps)
            {
                var a = new Point2D(kenar.A.X + tx * imlec, kenar.A.Y + ty * imlec);
                var b = new Point2D(kenar.A.X + tx * es.T0, kenar.A.Y + ty * es.T0);
                sonuc.Add(YeniDuvarBolmesi(m, a, b, es.T0 - imlec));
            }

            // Ortak duvar (merkez çizgi geometrisi)
            sonuc.Add(new Bolme
            {
                Ad = $"Ortak Duvar ({m.Ad} - {es.Komsu!.Ad})",
                KaynakMekan = m,
                AliciMekan = es.Komsu,
                Baslangic = es.MerkezBas,
                Bitis = es.MerkezBit,
                Alan = es.PaylasimUzunluk * m.Yukseklik,
                HasGeometry = true
            });
            imlec = es.T1;
        }

        // Kalan dış duvar parçası
        if (len > imlec + SplitGapEps)
        {
            var a = new Point2D(kenar.A.X + tx * imlec, kenar.A.Y + ty * imlec);
            sonuc.Add(YeniDuvarBolmesi(m, a, kenar.B, len - imlec));
        }

        return sonuc;
    }

    private static Bolme YeniDuvarBolmesi(Mekan m, Point2D a, Point2D b, double uzunluk)
    {
        return new Bolme
        {
            Ad = "Duvar",
            KaynakMekan = m,
            Baslangic = a,
            Bitis = b,
            Alan = uzunluk * m.Yukseklik,
            HasGeometry = true
        };
    }

    private static bool TryEslestir(
        PoligonKenari e1,
        PoligonKenari e2,
        out KenarEslesmesi m1,
        out KenarEslesmesi m2)
    {
        m1 = new KenarEslesmesi();
        m2 = new KenarEslesmesi();

        var v1x = e1.B.X - e1.A.X;
        var v1y = e1.B.Y - e1.A.Y;
        var v2x = e2.B.X - e2.A.X;
        var v2y = e2.B.Y - e2.A.Y;
        var len1 = Math.Sqrt(v1x * v1x + v1y * v1y);
        var len2 = Math.Sqrt(v2x * v2x + v2y * v2y);
        if (len1 <= 1e-9 || len2 <= 1e-9) return false;

        var t1x = v1x / len1;
        var t1y = v1y / len1;
        var t2x = v2x / len2;
        var t2y = v2y / len2;

        var parallel = Math.Abs(t1x * t2x + t1y * t2y);
        if (parallel < ParallelDotTolerance) return false;

        var n1x = -t1y;
        var n1y = t1x;
        var dStart = (e2.A.X - e1.A.X) * n1x + (e2.A.Y - e1.A.Y) * n1y;
        var dEnd = (e2.B.X - e1.A.X) * n1x + (e2.B.Y - e1.A.Y) * n1y;

        // |dStart - dEnd| (mutlak değerlerin farkı değil); çapraz duvarları doğru reddeder
        // ve uzun duvardaki küçük çizim açısına orantılı tolerans tanır.
        var parallelGapTol = Math.Max(0.10, 0.02 * len2);
        if (Math.Abs(dStart - dEnd) > parallelGapTol) return false;

        var distance = (Math.Abs(dStart) + Math.Abs(dEnd)) * 0.5;
        if (distance < MinWallGap || distance > MaxWallGap) return false;

        // e2 uç noktalarının e1 ekseni üzerindeki parametrik konumu
        var e2A_t1 = (e2.A.X - e1.A.X) * t1x + (e2.A.Y - e1.A.Y) * t1y;
        var e2B_t1 = (e2.B.X - e1.A.X) * t1x + (e2.B.Y - e1.A.Y) * t1y;
        var t1Min = Math.Min(e2A_t1, e2B_t1);
        var t1Max = Math.Max(e2A_t1, e2B_t1);
        var t1Start = Math.Max(0, t1Min);
        var t1End = Math.Min(len1, t1Max);
        var overlap1 = t1End - t1Start;
        var requiredOverlap = Math.Max(MinSharedLength, Math.Min(len1, len2) * OverlapRatio);
        if (overlap1 < requiredOverlap) return false;

        // Merkez çizgi uçları
        var p1x = e1.A.X + t1x * t1Start;
        var p1y = e1.A.Y + t1y * t1Start;
        var p2x = e1.A.X + t1x * t1End;
        var p2y = e1.A.Y + t1y * t1End;
        var signedMidDistance = (dStart + dEnd) * 0.5;
        var centerOffsetX = n1x * signedMidDistance * 0.5;
        var centerOffsetY = n1y * signedMidDistance * 0.5;
        var clStart = new Point2D(p1x + centerOffsetX, p1y + centerOffsetY);
        var clEnd = new Point2D(p2x + centerOffsetX, p2y + centerOffsetY);
        var clLen = Distance(clStart, clEnd);
        if (clLen < requiredOverlap) return false;

        // e2 üzerinde parametrik aralık
        var cl1_t2 = (clStart.X - e2.A.X) * t2x + (clStart.Y - e2.A.Y) * t2y;
        var cl2_t2 = (clEnd.X - e2.A.X) * t2x + (clEnd.Y - e2.A.Y) * t2y;
        var t2Start = Math.Max(0, Math.Min(len2, Math.Min(cl1_t2, cl2_t2)));
        var t2End = Math.Max(0, Math.Min(len2, Math.Max(cl1_t2, cl2_t2)));

        m1.T0 = t1Start;
        m1.T1 = t1End;
        m1.MerkezBas = clStart;
        m1.MerkezBit = clEnd;
        m1.PaylasimUzunluk = clLen;

        m2.T0 = t2Start;
        m2.T1 = t2End;
        // e2'nin yönüne göre merkez çizgiyi sırala
        if (cl1_t2 <= cl2_t2)
        {
            m2.MerkezBas = clStart;
            m2.MerkezBit = clEnd;
        }
        else
        {
            m2.MerkezBas = clEnd;
            m2.MerkezBit = clStart;
        }
        m2.PaylasimUzunluk = clLen;

        return true;
    }

    private static void DurumuAktar(List<Bolme> yeniler, List<KorunanBolmeDurumu> eskiler)
    {
        if (eskiler.Count == 0) return;

        // Topoloji gerçekten değişmediyse yeni bir Bolme üretmek yerine mevcut nesneyi
        // yerinde güncelle. Böylece yalnız görünüm/kaydetme amacıyla yapılan bir yeniden
        // tespitte Id, hesap sonuçları ve sonuç tablosunun tuttuğu nesne referansı korunur.
        // Bir eski bölme en fazla bir kez kullanılabilir; split edilen yeni parçalara aynı Id
        // kopyalanmaz.
        var kullanilanEskiler = new HashSet<Guid>();
        for (int i = 0; i < yeniler.Count; i++)
        {
            var yeni = yeniler[i];
            var tamEslesen = eskiler.FirstOrDefault(eski =>
                !kullanilanEskiler.Contains(eski.Original.Id) &&
                eski.AliciMekanId == yeni.AliciMekan?.Id &&
                SegmentAyni(eski.Baslangic, eski.Bitis, yeni.Baslangic, yeni.Bitis));

            if (tamEslesen == null) continue;

            var mevcut = tamEslesen.Original;
            mevcut.Ad = yeni.Ad;
            mevcut.KaynakMekan = yeni.KaynakMekan;
            mevcut.AliciMekan = yeni.AliciMekan;
            mevcut.Baslangic = yeni.Baslangic;
            mevcut.Bitis = yeni.Bitis;
            mevcut.Alan = yeni.Alan;
            mevcut.HasGeometry = yeni.HasGeometry;
            yeniler[i] = mevcut;
            kullanilanEskiler.Add(mevcut.Id);
        }

        // 1. YapiElemani/Menfez/Junction: yeni bölme, eski bölmenin doğrusu üzerinde örtüşüyorsa
        //    tüm kullanıcı atamalarını aktar (yalnız malzeme değil — menfez ve junction ayarları
        //    da kaybolmamalı; eskiden yalnızca YapiElemani/Acikliklar aktarılıyordu, bu fonksiyon
        //    her ekran yenilemesinde çağrıldığından kullanıcının junction/menfez ayarları sessizce
        //    siliniyordu).
        //    Önceki kod yalnızca merkez mesafesini bakıyordu; bir duvar split olduğunda yeni
        //    parçaların merkezi eski merkezden >0.5 m uzaklaşıp malzeme TRANSFER EDİLMİYORDU
        //    → kullanıcı "duvarlar bir süre sonra kayboluyor" diye gördü. Segment-örtüşme tabanlı
        //    eşleşme daha sağlamdır; uzun duvarın iki parçası da doğru malzemeyi alır.
        foreach (var yeni in yeniler)
        {
            if (yeni.YapiElemani != null && yeni.Menfezler.Count > 0 && yeni.Junctions.Count > 0) continue;

            double yeniLen = Math.Sqrt(
                (yeni.Bitis.X - yeni.Baslangic.X) * (yeni.Bitis.X - yeni.Baslangic.X) +
                (yeni.Bitis.Y - yeni.Baslangic.Y) * (yeni.Bitis.Y - yeni.Baslangic.Y));
            // Kısa duvarlar (örn. 0.5 m² ~ 0.18 m) sabit 10 cm eşiğini geçemiyordu → her yeniden
            // üretimde atama düşüyordu. Eşiği segment uzunluğuyla orantılı küçült.
            double esik = Math.Min(0.10, 0.40 * yeniLen);
            if (esik < 0.01) esik = 0.01;

            // Aktarılacak bir şeyi olmayan eski bölmeler aday olmaya değmez.
            static bool TasinacakVeriVar(KorunanBolmeDurumu e) =>
                e.YapiElemani != null || e.Menfezler.Count > 0 || e.Junctions.Count > 0;

            KorunanBolmeDurumu? enIyi = null;
            double enIyiOrtusme = esik;
            foreach (var eski in eskiler)
            {
                if (!TasinacakVeriVar(eski)) continue;
                double ortusme = SegmentOrtusmeUzunlugu(
                    yeni.Baslangic, yeni.Bitis,
                    eski.Baslangic, eski.Bitis);
                if (ortusme > enIyiOrtusme)
                {
                    enIyiOrtusme = ortusme;
                    enIyi = eski;
                }
            }

            // Yedek: örtüşme yakalanmazsa (çok kısa / hafif kaymış segment) merkez yakınlığıyla eşle.
            if (enIyi == null)
            {
                var ym = OrtaNokta(yeni.Baslangic, yeni.Bitis);
                double tol = Math.Max(0.12, 0.5 * yeniLen);
                double enYakinKare = tol * tol;
                foreach (var eski in eskiler)
                {
                    if (!TasinacakVeriVar(eski)) continue;
                    double d = KareMesafe(ym, OrtaNokta(eski.Baslangic, eski.Bitis));
                    if (d < enYakinKare)
                    {
                        enYakinKare = d;
                        enIyi = eski;
                    }
                }
            }

            if (enIyi == null) continue;
            if (yeni.YapiElemani == null) yeni.YapiElemani = enIyi.YapiElemani;
            if (yeni.Menfezler.Count == 0 && enIyi.Menfezler.Count > 0) yeni.Menfezler = enIyi.Menfezler.ToList();
            if (yeni.Junctions.Count == 0 && enIyi.Junctions.Count > 0) yeni.Junctions = enIyi.Junctions.ToList();
        }

        // 2. Açıklıklar: tüm eski açıklıkları topla, her birini merkez yakın yeni bölmeye bağla
        var tumAcikliklar = eskiler.SelectMany(e => e.Acikliklar).Distinct().ToList();
        foreach (var ack in tumAcikliklar)
        {
            var ackMerkez = OrtaNokta(ack.Baslangic, ack.Bitis);
            Bolme? enYakin = null;
            double enYakinKareMesafe = double.MaxValue;
            foreach (var yeni in yeniler)
            {
                var d = KareMesafe(ackMerkez, OrtaNokta(yeni.Baslangic, yeni.Bitis));
                if (d < enYakinKareMesafe)
                {
                    enYakinKareMesafe = d;
                    enYakin = yeni;
                }
            }
            if (enYakin != null && enYakinKareMesafe <= 1.00 * 1.00 && !enYakin.Acikliklar.Contains(ack))
                enYakin.Acikliklar.Add(ack);
        }
    }

    private static Point2D OrtaNokta(Point2D a, Point2D b) =>
        new Point2D((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5);

    private static double KareMesafe(Point2D a, Point2D b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }

    private static double Distance(Point2D p1, Point2D p2)
    {
        var dx = p1.X - p2.X;
        var dy = p1.Y - p2.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private sealed record PoligonKenari(Mekan Sahip, int Index, Point2D A, Point2D B);

    private sealed class KenarEslesmesi
    {
        public double T0;
        public double T1;
        public Mekan? Komsu;
        public Point2D MerkezBas;
        public Point2D MerkezBit;
        public double PaylasimUzunluk;
    }

    private sealed class KorunanBolmeDurumu
    {
        public Bolme Original = null!;
        public Point2D Merkez;
        public Point2D Baslangic;
        public Point2D Bitis;
        public Guid? AliciMekanId;
        public YapiElemani? YapiElemani;
        public List<BolmeAcikligi> Acikliklar = new();
        public List<Menfez> Menfezler = new();
        public List<Junction> Junctions = new();
    }

    private static bool SegmentAyni(Point2D a1, Point2D a2, Point2D b1, Point2D b2)
    {
        const double tol = 0.01; // DXF normalizasyonu / merkez çizgi hesabı yuvarlama payı
        static double D2(Point2D p, Point2D q)
        {
            var dx = p.X - q.X;
            var dy = p.Y - q.Y;
            return dx * dx + dy * dy;
        }

        var t2 = tol * tol;
        return (D2(a1, b1) <= t2 && D2(a2, b2) <= t2) ||
               (D2(a1, b2) <= t2 && D2(a2, b1) <= t2);
    }

    /// İki segmentin paylaştığı uzunluğu (m) döndürür.
    /// - Paralel değillerse (yön farkı > ~10°) 0
    /// - Birbirine perpendicular mesafe > 0.25 m ise 0 (farklı duvarlar)
    /// - Aksi halde projeksiyonların kesişim uzunluğu
    private static double SegmentOrtusmeUzunlugu(
        Point2D a1, Point2D a2, Point2D b1, Point2D b2)
    {
        double adx = a2.X - a1.X, ady = a2.Y - a1.Y;
        double aLen = Math.Sqrt(adx * adx + ady * ady);
        double bdx = b2.X - b1.X, bdy = b2.Y - b1.Y;
        double bLen = Math.Sqrt(bdx * bdx + bdy * bdy);
        if (aLen < 1e-9 || bLen < 1e-9) return 0;

        // Yön vektörleri
        double aux = adx / aLen, auy = ady / aLen;
        double bux = bdx / bLen, buy = bdy / bLen;

        // Paralel mi? (yön farkı küçük olmalı, ters yön de paralel sayılır)
        double dot = aux * bux + auy * buy;
        if (Math.Abs(dot) < 0.985) return 0;

        // b'nin a doğrusuna perpendicular mesafesi (b1 üzerinden hesapla)
        double sx = b1.X - a1.X, sy = b1.Y - a1.Y;
        double perp = Math.Abs(sx * (-auy) + sy * aux); // |s × a_unit|
        if (perp > 0.25) return 0; // farklı duvarlar

        // b1 ve b2'nin a ekseni üzerindeki parametrelerini al (0..aLen aralığında a1..a2)
        double tB1 = sx * aux + sy * auy;
        double tx2 = b2.X - a1.X;
        double ty2 = b2.Y - a1.Y;
        double tB2 = tx2 * aux + ty2 * auy;
        double bMin = Math.Min(tB1, tB2);
        double bMax = Math.Max(tB1, tB2);

        // Kesişim: [0, aLen] ∩ [bMin, bMax]
        double kesisimMin = Math.Max(0, bMin);
        double kesisimMax = Math.Min(aLen, bMax);
        return Math.Max(0, kesisimMax - kesisimMin);
    }
}
