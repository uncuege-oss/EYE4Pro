using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

namespace Kst.Acoustics.Tests;

public static class AciklikTespitTests
{
    private static (Point2D, Point2D) S(double ax, double ay, double bx, double by)
        => (new Point2D(ax, ay), new Point2D(bx, by));

    // Kapı yayı (arc): r=0.8, sweep=90° → 1 açıklık (kiriş segmenti).
    public static void ArcKapiAciklikUretir()
    {
        var segs = new List<(Point2D, Point2D)>
        {
            S(0, 0, 5, 0), S(5, 0, 5, 4), S(5, 4, 0, 4), S(0, 4, 0, 0)
        };
        var arcs = new List<(Point2D Merkez, double R, double BasDeg, double BitDeg)>
        {
            (new Point2D(2, 0), 0.8, 0, 90)
        };

        var sonuc = AciklikTespitServisi.Bul(segs, arcs, Array.Empty<BpcAciklik>());

        var arcKaynakli = sonuc.Where(a => a.Kaynak == "arc").ToList();
        if (arcKaynakli.Count != 1)
            throw new InvalidOperationException($"1 arc açıklığı beklenirken {arcKaynakli.Count} bulundu.");
        if (arcKaynakli[0].Tip != BpcAciklikTipi.Kapı)
            throw new InvalidOperationException("Arc kaynaklı açıklık varsayılan olarak Kapı olmalı.");
    }

    // Sweep 180° arc → kapı değil, sayılmamalı.
    public static void GenisArcKapiSayilmaz()
    {
        var arcs = new List<(Point2D Merkez, double R, double BasDeg, double BitDeg)>
        {
            (new Point2D(2, 0), 2.0, 0, 180)
        };
        var sonuc = AciklikTespitServisi.Bul(
            Array.Empty<(Point2D, Point2D)>(), arcs, Array.Empty<BpcAciklik>());
        if (sonuc.Any(a => a.Kaynak == "arc"))
            throw new InvalidOperationException("Geniş arc kapı sayılmamalı.");
    }

    // Gerçek kapı senaryosu (sıkı filtre geçer):
    //  • y=0'da iki parçalı duvar [0,2] ve [3,5] — kapı boşluğu [2,3] (1 m).
    //  • Kapı söğesi: x=2 ve x=3'te 0.15 m kısa dikey segment.
    //  • Karşı duvar (oda iç yüzü): y=1.2'de paralel yatay duvar (≤1.5 m).
    public static void DuvarBoslugundanAciklik()
    {
        var segs = new List<(Point2D, Point2D)>
        {
            // y=0 duvar parçaları
            S(0, 0, 2, 0), S(3, 0, 5, 0),
            // Dış köşe duvarları
            S(0, 0, 0, 1.2), S(5, 0, 5, 1.2),
            // Kapı söğeleri (eksene dik kısa segmentler)
            S(2, 0, 2, 0.15), S(3, 0, 3, 0.15),
            // Karşı (paralel) duvar — oda iç yüzü, 1.2 m karşıda
            S(0, 1.2, 5, 1.2),
        };
        var sonuc = AciklikTespitServisi.Bul(
            segs, Array.Empty<(Point2D, double, double, double)>(), Array.Empty<BpcAciklik>());

        var gapKaynakli = sonuc.Where(a => a.Kaynak == "gap").ToList();
        if (gapKaynakli.Count < 1)
            throw new InvalidOperationException($"En az 1 gap açıklığı beklenirken {gapKaynakli.Count} bulundu.");
        if (gapKaynakli.Any(a => a.Tip != BpcAciklikTipi.Kapı))
            throw new InvalidOperationException("Gap kaynaklı açıklıklar kullanıcı değiştirene kadar Kapı varsayılmalı.");
    }

    // Söğesi olmayan gap reddedilmeli (mobilya ucu, parçalanmış duvar vb.).
    // Aynı setup'tan kapı söğelerini çıkarınca açıklık üretilmemeli.
    public static void SovesizGapReddedilir()
    {
        var segs = new List<(Point2D, Point2D)>
        {
            S(0, 0, 2, 0), S(3, 0, 5, 0),
            S(0, 0, 0, 1.2), S(5, 0, 5, 1.2),
            // SÖĞE YOK
            S(0, 3, 5, 3),
        };
        var sonuc = AciklikTespitServisi.Bul(
            segs, Array.Empty<(Point2D, double, double, double)>(), Array.Empty<BpcAciklik>());

        var gapKaynakli = sonuc.Where(a => a.Kaynak == "gap").ToList();
        if (gapKaynakli.Count != 0)
            throw new InvalidOperationException(
                $"Söğesiz gap reddedilmeliydi; {gapKaynakli.Count} açıklık üretildi.");
    }

    // Karşı paralel duvarı olmayan gap reddedilmeli (oda iç yüzü yok = boşlukta duvar).
    public static void KarsiDuvarsizGapReddedilir()
    {
        var segs = new List<(Point2D, Point2D)>
        {
            S(0, 0, 2, 0), S(3, 0, 5, 0),
            S(0, 0, 0, 1.2), S(5, 0, 5, 1.2),
            S(2, 0, 2, 0.15), S(3, 0, 3, 0.15),
            // KARŞI DUVAR YOK — paralel yatay segment yok
        };
        var sonuc = AciklikTespitServisi.Bul(
            segs, Array.Empty<(Point2D, double, double, double)>(), Array.Empty<BpcAciklik>());

        var gapKaynakli = sonuc.Where(a => a.Kaynak == "gap").ToList();
        if (gapKaynakli.Count != 0)
            throw new InvalidOperationException(
                $"Karşı duvarsız gap reddedilmeliydi; {gapKaynakli.Count} açıklık üretildi.");
    }

    // Karşı duvar MaxKarsiDuvarM (1.5 m) içinde olmalı. 2.5 m karşıdaki duvar reddetmeli.
    public static void UzakKarsiDuvarReddedilir()
    {
        var segs = new List<(Point2D, Point2D)>
        {
            S(0, 0, 2, 0), S(3, 0, 5, 0),
            S(0, 0, 0, 1.2), S(5, 0, 5, 1.2),
            S(2, 0, 2, 0.15), S(3, 0, 3, 0.15),
            // Karşı duvar 2.5 m uzakta — sıkı filtre 1.5 m sınırının dışı
            S(0, 2.5, 5, 2.5),
        };
        var sonuc = AciklikTespitServisi.Bul(
            segs, Array.Empty<(Point2D, double, double, double)>(), Array.Empty<BpcAciklik>());

        var gapKaynakli = sonuc.Where(a => a.Kaynak == "gap").ToList();
        if (gapKaynakli.Count != 0)
            throw new InvalidOperationException(
                $"Uzak (2.5 m) karşı duvar reddedilmeliydi; {gapKaynakli.Count} açıklık üretildi.");
    }

    // İptal edilmiş kayıt yeniden taramada korunmalı.
    public static void IptalEdilmisKayitKorunur()
    {
        var mevcut = new List<BpcAciklik>
        {
            new() { Bas = new(10, 10), Bit = new(11, 10), IptalEdildi = true, Kaynak = "manuel" }
        };
        var sonuc = AciklikTespitServisi.Bul(
            Array.Empty<(Point2D, Point2D)>(),
            Array.Empty<(Point2D, double, double, double)>(),
            mevcut);

        if (!sonuc.Any(a => a.IptalEdildi))
            throw new InvalidOperationException("İptal edilmiş açıklık silinmiş.");
    }

    // ManuelEklendi=true kayıt yeniden taramada korunmalı.
    public static void ManuelKayitKorunur()
    {
        var mevcut = new List<BpcAciklik>
        {
            new() { Bas = new(20, 20), Bit = new(21, 20), Tip = BpcAciklikTipi.Pencere, ManuelEklendi = true, Kaynak = "manuel" }
        };
        var sonuc = AciklikTespitServisi.Bul(
            Array.Empty<(Point2D, Point2D)>(),
            Array.Empty<(Point2D, double, double, double)>(),
            mevcut);

        if (!sonuc.Any(a => a.ManuelEklendi))
            throw new InvalidOperationException("Manuel açıklık silinmiş.");
        if (!sonuc.Any(a => a.ManuelEklendi && a.Tip == BpcAciklikTipi.Pencere))
            throw new InvalidOperationException("Manuel açıklığın Kapı/Pencere tipi korunmalı.");
    }

    // IsAciklikYakininda: yakın → true, uzak → false, iptal → false.
    public static void IsAciklikYakindaTemelDavranis()
    {
        var kat = new BasitProjeKat();
        kat.Acikliklar.Add(new BpcAciklik
        {
            Bas = new Point2D(0, 0), Bit = new Point2D(1, 0), Kaynak = "manuel"
        });

        if (!AciklikYardimci.IsAciklikYakininda(new Point2D(0.5, 0.1), kat, 0.30))
            throw new InvalidOperationException("Açıklığa 0.1 m'de yakın olmalı (true bekleniyor).");
        if (AciklikYardimci.IsAciklikYakininda(new Point2D(0.5, 5.0), kat, 0.30))
            throw new InvalidOperationException("Açıklığa 5 m'de uzak olmalı (false bekleniyor).");

        // İptal edilmiş açıklık → false
        kat.Acikliklar[0].IptalEdildi = true;
        if (AciklikYardimci.IsAciklikYakininda(new Point2D(0.5, 0.1), kat, 0.30))
            throw new InvalidOperationException("İptal edilmiş açıklık 'yakın' sayılmamalı.");
    }

    // Flood-fill ekDuvarlar: açıklık sanal duvarı eklenince iki oda ayırılır.
    public static void FloodFillEkDuvarlarOdayiBolerYap()
    {
        // 10x4 dış çerçeve + ortada parçalı duvar — kapı boşluğu y=1..3 = 2.0 m (geniş)
        var duvarlar = new List<(Point2D, Point2D)>
        {
            S(0, 0, 10, 0), S(10, 0, 10, 4), S(10, 4, 0, 4), S(0, 4, 0, 0),
            S(5, 0, 5, 1), S(5, 3, 5, 4)
        };
        // Mühürleme tamamen kapalı — boşluk doğal olarak kapanmaz
        var ayar = new Kst.Acoustics.Core.Bina.Services.BosluktanOdaTespitServisi.Ayarlar { KapiKapatmaM = 0.0 };

        // Ek duvar olmadan: 2 m boşluk açık → odalar birleşir → 1 oda
        var tek = Kst.Acoustics.Core.Bina.Services.BosluktanOdaTespitServisi.OdalariBul(duvarlar, ayar);
        if (tek.Count != 1)
            throw new InvalidOperationException($"Ek duvarsız 1 oda beklenirken {tek.Count} bulundu.");

        // Ek duvar (açıklığı kapatan sanal segment) ile: 2 oda
        var ekDuvarlar = new List<(Point2D, Point2D)> { S(5, 1, 5, 3) };
        var iki = Kst.Acoustics.Core.Bina.Services.BosluktanOdaTespitServisi.OdalariBul(duvarlar, ayar, ekDuvarlar);
        if (iki.Count != 2)
            throw new InvalidOperationException($"Ek duvarla 2 oda beklenirken {iki.Count} bulundu.");
    }

    // Kapı iki odaya hizmet eder: bir odanın duvarına eklenen açıklık, duvar
    // kalınlığı kadar ötedeki komşu oda kenarına yansıtılmalı.
    public static void KomsuDuvaraAciklikYansir()
    {
        var kat = new BasitProjeKat();
        // İki oda, ortak duvarda 0.20 m kalınlık: Oda A y∈[0,3], Oda B y∈[3.2,6.2]
        kat.Odalar.Add(new BasitProjeOda { Ad = "A", Poligon = [new(0, 0), new(4, 0), new(4, 3), new(0, 3)] });
        kat.Odalar.Add(new BasitProjeOda { Ad = "B", Poligon = [new(0, 3.2), new(4, 3.2), new(4, 6.2), new(0, 6.2)] });

        // Kapı Oda A'nın üst kenarında (y=3), x=1.5..2.5
        var kapi = new BpcAciklik { Bas = new(1.5, 3), Bit = new(2.5, 3), Tip = BpcAciklikTipi.Kapı, ManuelEklendi = true, Kaynak = "manuel" };
        kat.Acikliklar.Add(kapi);

        var yansima = AciklikYardimci.KomsuDuvaraYansit(kat, kapi);
        if (yansima == null)
            throw new InvalidOperationException("Komşu duvara yansıma açıklığı oluşmalı.");

        // Yansıma komşu kenarda (y≈3.2) ve aynı x aralığında olmalı
        if (Math.Abs(yansima.Bas.Y - 3.2) > 1e-6 || Math.Abs(yansima.Bit.Y - 3.2) > 1e-6)
            throw new InvalidOperationException($"Yansıma komşu kenara (y=3.2) düşmeli; bulunan ({yansima.Bas.Y:F2},{yansima.Bit.Y:F2}).");
        double minX = Math.Min(yansima.Bas.X, yansima.Bit.X), maxX = Math.Max(yansima.Bas.X, yansima.Bit.X);
        if (Math.Abs(minX - 1.5) > 0.01 || Math.Abs(maxX - 2.5) > 0.01)
            throw new InvalidOperationException($"Yansıma x aralığı 1.5..2.5 olmalı; bulunan {minX:F2}..{maxX:F2}.");
        if (kat.Acikliklar.Count != 2)
            throw new InvalidOperationException($"Yansıma sonrası 2 açıklık beklenir; bulunan {kat.Acikliklar.Count}.");

        // İkinci kez çağrı duplicate üretmemeli (komşu kenarda zaten açıklık var)
        var tekrar = AciklikYardimci.KomsuDuvaraYansit(kat, kapi);
        if (tekrar != null || kat.Acikliklar.Count != 2)
            throw new InvalidOperationException("Tekrarlı yansıtma duplicate açıklık üretmemeli.");
    }

    // Komşu oda yoksa (tek oda) yansıma oluşmamalı.
    public static void KomsuDuvarYoksaYansimaOlmaz()
    {
        var kat = new BasitProjeKat();
        kat.Odalar.Add(new BasitProjeOda { Ad = "A", Poligon = [new(0, 0), new(4, 0), new(4, 3), new(0, 3)] });
        var kapi = new BpcAciklik { Bas = new(1.5, 3), Bit = new(2.5, 3), Tip = BpcAciklikTipi.Kapı, ManuelEklendi = true, Kaynak = "manuel" };
        kat.Acikliklar.Add(kapi);

        var yansima = AciklikYardimci.KomsuDuvaraYansit(kat, kapi);
        if (yansima != null || kat.Acikliklar.Count != 1)
            throw new InvalidOperationException("Komşu duvar yokken yansıma oluşmamalı.");
    }

    public static void KomsuAciklikBirlikteSilinir()
    {
        var kat = new Kst.Acoustics.Core.Bina.Models.BinaKat();
        kat.Odalar.Add(new Kst.Acoustics.Core.Bina.Models.BinaOda
        {
            Ad = "A",
            Poligon = [new(0, 0), new(4, 0), new(4, 3), new(0, 3)]
        });
        kat.Odalar.Add(new Kst.Acoustics.Core.Bina.Models.BinaOda
        {
            Ad = "B",
            Poligon = [new(0, 3.2), new(4, 3.2), new(4, 6.2), new(0, 6.2)]
        });

        var kapi = new Kst.Acoustics.Core.Bina.Models.BinaAciklik
        {
            Bas = new(1.5, 3),
            Bit = new(2.5, 3),
            Tip = Kst.Acoustics.Core.Bina.Models.BinaAciklikTipi.Kapi,
            GenislikM = 1.0,
            YukseklikM = 2.1,
            EsikYuksekligiM = 0,
            ManuelEklendi = true,
            Kaynak = "manuel"
        };
        kat.Acikliklar.Add(kapi);

        var yansima = Kst.Acoustics.Core.Bina.Services.AciklikYardimci.KomsuDuvaraYansit(kat, kapi);
        if (yansima is null || kat.Acikliklar.Count != 2)
            throw new InvalidOperationException("Komşu açıklık yansıması test önkoşulu oluşmadı.");
        if (Math.Abs(yansima.YukseklikM - 2.1) > 1e-6)
            throw new InvalidOperationException("Yansıyan açıklık yükseklik bilgisini korumalı.");

        int silinen = Kst.Acoustics.Core.Bina.Services.AciklikYardimci.KomsuAcikliklariSil(kat, kapi);
        kat.Acikliklar.Remove(kapi);
        if (silinen != 1 || kat.Acikliklar.Count != 0)
            throw new InvalidOperationException($"Kaynak açıklık silinince komşu açıklık da silinmeli; silinen={silinen}, kalan={kat.Acikliklar.Count}.");

        kat.Acikliklar.Add(kapi);
        yansima = Kst.Acoustics.Core.Bina.Services.AciklikYardimci.KomsuDuvaraYansit(kat, kapi);
        if (yansima is null)
            throw new InvalidOperationException("Ters silme test önkoşulu için yansıma oluşmadı.");

        silinen = Kst.Acoustics.Core.Bina.Services.AciklikYardimci.KomsuAcikliklariSil(kat, yansima);
        kat.Acikliklar.Remove(yansima);
        if (silinen != 1 || kat.Acikliklar.Count != 0)
            throw new InvalidOperationException($"Komşu açıklık silinince kaynak açıklık da silinmeli; silinen={silinen}, kalan={kat.Acikliklar.Count}.");
    }

    public static void KomsuYansimaAyniOdaKenarinaAtlamaz()
    {
        var kat = new Kst.Acoustics.Core.Bina.Models.BinaKat();
        kat.Odalar.Add(new Kst.Acoustics.Core.Bina.Models.BinaOda
        {
            Id = "oda-a",
            Ad = "A",
            Poligon = [new(0, 0), new(4, 0), new(4, 0.20), new(0, 0.20)]
        });
        kat.Odalar.Add(new Kst.Acoustics.Core.Bina.Models.BinaOda
        {
            Id = "oda-b",
            Ad = "B",
            Poligon = [new(0, -0.20), new(4, -0.20), new(4, -3.2), new(0, -3.2)]
        });

        var kapi = new Kst.Acoustics.Core.Bina.Models.BinaAciklik
        {
            Bas = new(1.5, 0),
            Bit = new(2.5, 0),
            Tip = Kst.Acoustics.Core.Bina.Models.BinaAciklikTipi.Kapi,
            GenislikM = 1.0,
            ManuelEklendi = true,
            Kaynak = "manuel"
        };
        kat.Acikliklar.Add(kapi);

        var yansima = Kst.Acoustics.Core.Bina.Services.AciklikYardimci.KomsuDuvaraYansit(kat, kapi);
        if (yansima is null)
            throw new InvalidOperationException("Komşu oda kenarına yansıma oluşmalı.");
        if (Math.Abs(yansima.Bas.Y + 0.20) > 1e-6 || Math.Abs(yansima.Bit.Y + 0.20) > 1e-6)
            throw new InvalidOperationException($"Yansıma aynı odanın üst kenarına değil komşu oda kenarına düşmeli; bulunan Y=({yansima.Bas.Y:F2},{yansima.Bit.Y:F2}).");
    }
}
