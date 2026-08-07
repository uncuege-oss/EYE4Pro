using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;
using BinaServices = Kst.Acoustics.Core.Bina.Services;
using netDxf;
using netDxf.Entities;
using netDxf.Units;

namespace Kst.Acoustics.Tests;

/// M50 §9 — çoklu kat tek DXF (yan yana planlar) testleri.
public static class CokluKatTests
{
    private static (Point2D, Point2D) S(double ax, double ay, double bx, double by)
        => (new Point2D(ax, ay), new Point2D(bx, by));

    private static List<(Point2D A, Point2D B)> Dikdortgen(double x0, double y0, double x1, double y1) =>
        new()
        {
            S(x0, y0, x1, y0), S(x1, y0, x1, y1), S(x1, y1, x0, y1), S(x0, y1, x0, y0)
        };

    // ── 9.1 KatKumelemeServisi ──────────────────────────────────────────────

    public static void KumelemeUzakPlanlariAyirir()
    {
        var segler = Dikdortgen(0, 0, 10, 8).Concat(Dikdortgen(30, 0, 40, 8)).ToList();

        var kumeler = BinaServices.KatKumelemeServisi.Kumele(segler, [], []);

        if (kumeler.Count != 2)
            throw new InvalidOperationException($"20 m aralıklı iki plan 2 ayrı küme vermeli; bulunan {kumeler.Count}.");
        foreach (var k in kumeler)
            if (k.BboxAlanM2 < 70 || k.BboxAlanM2 > 90)
                throw new InvalidOperationException($"Her küme ~80 m² bbox olmalı; bulunan {k.BboxAlanM2:F1}.");
    }

    public static void KumelemeBitisikPlanlariBirlestirir()
    {
        // 2 m aralık — varsayılan 5 m boşluk eşiğinin altında, TEK küme sayılmalı.
        var segler = Dikdortgen(0, 0, 10, 8).Concat(Dikdortgen(12, 0, 22, 8)).ToList();

        var kumeler = BinaServices.KatKumelemeServisi.Kumele(segler, [], []);

        if (kumeler.Count != 1)
            throw new InvalidOperationException($"2 m aralıklı bitişik planlar tek küme sayılmalı; bulunan {kumeler.Count}.");
    }

    public static void KumelemeTekPlandaTekKumeDoner()
    {
        var segler = Dikdortgen(0, 0, 10, 8).ToList();

        var kumeler = BinaServices.KatKumelemeServisi.Kumele(segler, [], []);

        if (kumeler.Count != 1)
            throw new InvalidOperationException($"Tek plan tek küme dönmeli; bulunan {kumeler.Count}.");
    }

    public static void PlanFiltreKucukKumeyiAyiklar()
    {
        // Büyük plan (80 m²) + küçük antet bloğu (2x2=4 m², eşik 20 m² altında), 30 m arayla.
        var segler = Dikdortgen(0, 0, 10, 8).Concat(Dikdortgen(40, 0, 42, 2)).ToList();
        var kumeler = BinaServices.KatKumelemeServisi.Kumele(segler, [], []);
        if (kumeler.Count != 2)
            throw new InvalidOperationException($"İki ayrık küme beklenir; bulunan {kumeler.Count}.");

        var (planlar, planDegil) = BinaServices.KatKumelemeServisi.PlanFiltrele(kumeler);
        if (planlar.Count != 1 || planDegil.Count != 1)
            throw new InvalidOperationException(
                $"1 plan + 1 plan-dışı beklenir; bulunan plan={planlar.Count}, planDegil={planDegil.Count}.");
        if (planlar[0].BboxAlanM2 < 70)
            throw new InvalidOperationException("Plan olarak işaretlenen küme büyük olan olmalı.");
    }

    // ── 9.2 KatEtiketServisi ────────────────────────────────────────────────

    private static BinaServices.KatKumelemeServisi.KatKumesi KumeYap(
        double x0, double y0, double x1, double y1, params string[] metinler)
    {
        var segler = Dikdortgen(x0, y0, x1, y1);
        var txt = metinler.Select(m => (new Point2D((x0 + x1) / 2, (y0 + y1) / 2), m)).ToList();
        return new BinaServices.KatKumelemeServisi.KatKumesi(x0, y0, x1, y1, segler, txt, []);
    }

    public static void EtiketZeminKatiTanir()
    {
        var kume = KumeYap(0, 0, 10, 8, "ZEMİN KAT", "SALON 24 m2");
        var t = BinaServices.KatEtiketServisi.TahminEt(kume);
        if (!t.EtiketBulundu || t.OnerilenAd != "Zemin Kat" || Math.Abs(t.SiraDegeri - 0) > 1e-9)
            throw new InvalidOperationException($"Zemin Kat beklenir; bulunan Ad={t.OnerilenAd}, Sira={t.SiraDegeri}, Bulundu={t.EtiketBulundu}.");
    }

    public static void EtiketNumaraliKatiTanir()
    {
        var kume = KumeYap(0, 0, 10, 8, "2. KAT", "YATAK ODASI");
        var t = BinaServices.KatEtiketServisi.TahminEt(kume);
        if (!t.EtiketBulundu || t.OnerilenAd != "2. Kat" || Math.Abs(t.SiraDegeri - 2) > 1e-9)
            throw new InvalidOperationException($"2. Kat beklenir; bulunan Ad={t.OnerilenAd}, Sira={t.SiraDegeri}.");
    }

    public static void EtiketBodrumuTanirVeNegatifSiralar()
    {
        var kume = KumeYap(0, 0, 10, 8, "BODRUM 2", "OTOPARK");
        var t = BinaServices.KatEtiketServisi.TahminEt(kume);
        if (!t.EtiketBulundu || Math.Abs(t.SiraDegeri - (-2)) > 1e-9)
            throw new InvalidOperationException($"Bodrum 2 için Sira=-2 beklenir; bulunan Ad={t.OnerilenAd}, Sira={t.SiraDegeri}.");
    }

    public static void EtiketCatiEnYuksekSiralanir()
    {
        var kume = KumeYap(0, 0, 10, 8, "ÇATI KATI");
        var t = BinaServices.KatEtiketServisi.TahminEt(kume);
        if (!t.EtiketBulundu || t.SiraDegeri < 1000)
            throw new InvalidOperationException($"Çatı en yüksek sıralanmalı; bulunan Sira={t.SiraDegeri}.");
    }

    public static void EtiketBulunamazsaBulunduFalseDoner()
    {
        var kume = KumeYap(0, 0, 10, 8, "ODA 1", "12 m2");
        var t = BinaServices.KatEtiketServisi.TahminEt(kume);
        if (t.EtiketBulundu)
            throw new InvalidOperationException("Kat etiketi olmayan kümede EtiketBulundu=false olmalı.");
    }

    // ── 9.3 DxfKatYukleyici — çoklu kat DXF ────────────────────────────────

    private static string GecisDxfYaz(Action<DxfDocument> olustur)
    {
        var dxf = new DxfDocument();
        dxf.DrawingVariables.InsUnits = DrawingUnits.Meters;
        olustur(dxf);
        var path = Path.Combine(Path.GetTempPath(), $"eye4pro_coklukat_{Guid.NewGuid():N}.dxf");
        dxf.Save(path);
        return path;
    }

    private static void DikdortgenEkle(DxfDocument dxf, double x0, double y0, double x1, double y1)
    {
        dxf.Entities.Add(new Line(new Vector2(x0, y0), new Vector2(x1, y0)));
        dxf.Entities.Add(new Line(new Vector2(x1, y0), new Vector2(x1, y1)));
        dxf.Entities.Add(new Line(new Vector2(x1, y1), new Vector2(x0, y1)));
        dxf.Entities.Add(new Line(new Vector2(x0, y1), new Vector2(x0, y0)));
    }

    public static void CokluKatTespitIkiPlanliDxfBulur()
    {
        var path = GecisDxfYaz(dxf =>
        {
            DikdortgenEkle(dxf, 0, 0, 10, 8);
            dxf.Entities.Add(new Text("ZEMİN KAT", new Vector2(5, 4), 0.5));

            DikdortgenEkle(dxf, 30, 0, 40, 8);
            dxf.Entities.Add(new Text("1. KAT", new Vector2(35, 4), 0.5));
        });

        try
        {
            var yukleyici = new BinaServices.DxfKatYukleyici();
            var sonuc = yukleyici.CokluKatTespitEt(path);

            if (sonuc is null)
                throw new InvalidOperationException("İki ayrık plan içeren DXF'te çoklu-kat tespiti null dönmemeli.");
            if (sonuc.Adaylar.Count != 2)
                throw new InvalidOperationException($"2 aday beklenir; bulunan {sonuc.Adaylar.Count}.");

            var zemin = sonuc.Adaylar.FirstOrDefault(a => a.OnerilenAd == "Zemin Kat");
            var birinci = sonuc.Adaylar.FirstOrDefault(a => a.OnerilenAd == "1. Kat");
            if (zemin is null || birinci is null)
                throw new InvalidOperationException(
                    $"Zemin Kat + 1. Kat adayları beklenir; bulunan adlar: {string.Join(", ", sonuc.Adaylar.Select(a => a.OnerilenAd))}.");
            if (!zemin.OneriliDahil || !birinci.OneriliDahil)
                throw new InvalidOperationException("Her iki aday da (80 m² > 20 m² eşik) plan olarak önerilmeli.");
            if (birinci.SiraDegeri <= zemin.SiraDegeri)
                throw new InvalidOperationException("1. Kat sıra değeri Zemin Kat'tan büyük olmalı.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public static void CokluKatTespitTekPlandaNullDoner()
    {
        var path = GecisDxfYaz(dxf =>
        {
            DikdortgenEkle(dxf, 0, 0, 10, 8);
            dxf.Entities.Add(new Text("ZEMİN KAT", new Vector2(5, 4), 0.5));
        });

        try
        {
            var yukleyici = new BinaServices.DxfKatYukleyici();
            var sonuc = yukleyici.CokluKatTespitEt(path);
            if (sonuc is not null)
                throw new InvalidOperationException("Tek plan içeren DXF'te çoklu-kat tespiti null dönmeli.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public static void CokluKatUretOdaVeSegmentUretir()
    {
        var path = GecisDxfYaz(dxf =>
        {
            DikdortgenEkle(dxf, 0, 0, 10, 8);
            dxf.Entities.Add(new Text("ZEMİN KAT", new Vector2(5, 4), 0.5));

            DikdortgenEkle(dxf, 30, 0, 40, 8);
            dxf.Entities.Add(new Text("1. KAT", new Vector2(35, 4), 0.5));
        });

        try
        {
            var yukleyici = new BinaServices.DxfKatYukleyici();
            var sonuc = yukleyici.CokluKatTespitEt(path)
                ?? throw new InvalidOperationException("Çoklu kat tespiti null döndü.");

            int katNo = 1;
            var uretilenler = new List<Kst.Acoustics.Core.Bina.Models.BinaKat>();
            foreach (var aday in sonuc.Adaylar.Where(a => a.OneriliDahil).OrderByDescending(a => a.SiraDegeri))
                uretilenler.Add(yukleyici.CokluKatUret(aday, katNo++, aday.OnerilenAd, 3.0));

            if (uretilenler.Count != 2)
                throw new InvalidOperationException($"2 kat üretilmeli; bulunan {uretilenler.Count}.");

            foreach (var kat in uretilenler)
            {
                if (kat.Odalar.Count != 1)
                    throw new InvalidOperationException($"'{kat.Ad}' için 1 oda beklenir; bulunan {kat.Odalar.Count}.");
                double alan = kat.Odalar[0].AlanM2;
                if (alan < 70 || alan > 90)
                    throw new InvalidOperationException($"'{kat.Ad}' oda alanı ~80 m² olmalı; bulunan {alan:F1}.");
                if (kat.DxfSegmentler.Count == 0)
                    throw new InvalidOperationException($"'{kat.Ad}' için sadeleştirilmiş DXF segmenti üretilmedi.");

                // Bbox-min normalize edilmiş olmalı: köşe koordinatları küçük (yerel çerçeve),
                // ikinci kümenin dünya X=30 ofseti sızmamış olmalı.
                double minX = kat.Odalar[0].Poligon.Min(p => p.X);
                if (Math.Abs(minX) > 1.0)
                    throw new InvalidOperationException($"'{kat.Ad}' yerel çerçeveye normalize edilmemiş (minX={minX:F1}).");
            }
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
