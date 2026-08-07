using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;
using BinaServices = Kst.Acoustics.Core.Bina.Services;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;
using netDxf;
using netDxf.Blocks;
using netDxf.Entities;
using netDxf.Units;

namespace Kst.Acoustics.Tests;

public static class BosluktanOdaTespitTests
{
    private static (Point2D, Point2D) S(double ax, double ay, double bx, double by)
        => (new Point2D(ax, ay), new Point2D(bx, by));

    // Tek dikdörtgen oda: 6x4 = 24 m². Tek poligon, alan ~24 bulunmalı.
    public static void TekDikdortgenOdaBulunur()
    {
        var duvarlar = new List<(Point2D, Point2D)>
        {
            S(0, 0, 6, 0), S(6, 0, 6, 4), S(6, 4, 0, 4), S(0, 4, 0, 0)
        };

        var odalar = BinaServices.BosluktanOdaTespitServisi.OdalariBul(duvarlar);

        if (odalar.Count != 1)
            throw new InvalidOperationException($"Tek oda beklenirken {odalar.Count} oda bulundu.");

        double alan = PoligonYardimci.Alan(odalar[0]);
        if (alan < 20 || alan > 26)
            throw new InvalidOperationException($"Oda alanı ~24 m² olmalı, bulunan: {alan:F1}.");
    }

    // İki oda + aralarında 1 m kapı boşluğu. Kapı kapatma ile 2 ayrı oda bulunmalı.
    public static void KapiBosluguMuhurlenipIkiOdaBulunur()
    {
        var duvarlar = new List<(Point2D, Point2D)>
        {
            // Dış çerçeve 10x4
            S(0, 0, 10, 0), S(10, 0, 10, 4), S(10, 4, 0, 4), S(0, 4, 0, 0),
            // Orta duvar x=5, y=1.5..2.5 arası 1 m kapı boşluğu
            S(5, 0, 5, 1.5), S(5, 2.5, 5, 4)
        };

        var odalar = BinaServices.BosluktanOdaTespitServisi.OdalariBul(duvarlar);

        if (odalar.Count != 2)
            throw new InvalidOperationException(
                $"Kapı boşluğu mühürlenip 2 oda beklenirken {odalar.Count} oda bulundu.");

        foreach (var o in odalar)
        {
            double alan = PoligonYardimci.Alan(o);
            if (alan < 14 || alan > 22)
                throw new InvalidOperationException($"Her oda ~20 m² olmalı, bulunan: {alan:F1}.");
        }
    }

    // Kapı kapatma kapalı (0) iken iki oda kapıdan birleşir → tek bölge.
    public static void KapiKapatmaKapaliIkenOdalarBirlesir()
    {
        var duvarlar = new List<(Point2D, Point2D)>
        {
            S(0, 0, 10, 0), S(10, 0, 10, 4), S(10, 4, 0, 4), S(0, 4, 0, 0),
            S(5, 0, 5, 1.5), S(5, 2.5, 5, 4)
        };

        var ayar = new BinaServices.BosluktanOdaTespitServisi.Ayarlar { KapiKapatmaM = 0.0 };
        var odalar = BinaServices.BosluktanOdaTespitServisi.OdalariBul(duvarlar, ayar);

        // Mühürleme yokken kapıdan sızıp tek büyük bölge olmalı
        if (odalar.Count != 1)
            throw new InvalidOperationException(
                $"Mühürleme kapalıyken odalar birleşip 1 bölge olmalı, bulunan: {odalar.Count}.");
    }

    // Oda içindeki küçük mobilya (kapalı kutu) odayı bölmemeli; tek oda kalmalı.
    public static void OdaIcindekiMobilyaOdayiBolmez()
    {
        var duvarlar = new List<(Point2D, Point2D)>
        {
            // Dış oda 8x6
            S(0, 0, 8, 0), S(8, 0, 8, 6), S(8, 6, 0, 6), S(0, 6, 0, 0),
            // Ortada 1x1 mobilya kutusu
            S(3, 2.5, 4, 2.5), S(4, 2.5, 4, 3.5), S(4, 3.5, 3, 3.5), S(3, 3.5, 3, 2.5)
        };

        var odalar = BinaServices.BosluktanOdaTespitServisi.OdalariBul(duvarlar);

        if (odalar.Count != 1)
            throw new InvalidOperationException(
                $"Mobilya odayı bölmemeli; 1 oda beklenirken {odalar.Count} bulundu.");

        double alan = PoligonYardimci.Alan(odalar[0]);
        // Dış kontur alanı ~48 m² (mobilya dış konturu etkilemez, delik sayılmaz)
        if (alan < 42 || alan > 50)
            throw new InvalidOperationException($"Dış kontur alanı ~48 m² olmalı, bulunan: {alan:F1}.");
    }

    // Kolon/kiriş gibi küçük girinti-çıkıntılar M08 için fazladan duvar üretmemeli.
    public static void KucukKolonCikintilariDuzlesir()
    {
        var duvarlar = new List<(Point2D, Point2D)>
        {
            // 6x4 oda; alt duvarda 0.30 m derinlik, 0.60 m genişlik çıkıntı var.
            S(0, 0, 2, 0),
            S(2, 0, 2, -0.30),
            S(2, -0.30, 2.60, -0.30),
            S(2.60, -0.30, 2.60, 0),
            S(2.60, 0, 6, 0),
            S(6, 0, 6, 4),
            S(6, 4, 0, 4),
            S(0, 4, 0, 0)
        };

        var ayar = new BinaServices.BosluktanOdaTespitServisi.Ayarlar
        {
            HucreBoyuM = 0.05,
            KapiKapatmaM = 0.10,
            SnapToleransM = 0.12,
            CikintiDerinlikEsigiM = 0.35,
            CikintiGenislikEsigiM = 0.80
        };
        var odalar = BinaServices.BosluktanOdaTespitServisi.OdalariBul(duvarlar, ayar);
        if (odalar.Count != 1)
            throw new InvalidOperationException($"1 oda beklenirken {odalar.Count} oda bulundu.");
        if (odalar[0].Count != 4)
            throw new InvalidOperationException($"Küçük çıkıntı düzleşip 4 köşe kalmalı; bulunan köşe: {odalar[0].Count}.");

        ayar.CikintiDerinlikEsigiM = 0;
        ayar.CikintiGenislikEsigiM = 0;
        var hamOdalar = BinaServices.BosluktanOdaTespitServisi.OdalariBul(duvarlar, ayar);
        if (hamOdalar.Count != 1)
            throw new InvalidOperationException($"Çıkıntı filtresi kapalıyken de 1 oda beklenir; bulunan: {hamOdalar.Count}.");
        if (hamOdalar[0].Count <= 4)
            throw new InvalidOperationException("Çıkıntı filtresi kapalıyken kolon çıkıntısı köşeleri korunmalı.");
    }

    public static void M12KucukKolonCikintilariDuzlesir()
    {
        var kat = new BasitProjeKat { KatNo = 0, Ad = "Zemin", KatYuksekligiM = 3.0 };
        foreach (var (a, b) in new List<(Point2D, Point2D)>
        {
            S(0, 0, 2, 0),
            S(2, 0, 2, -0.30),
            S(2, -0.30, 2.60, -0.30),
            S(2.60, -0.30, 2.60, 0),
            S(2.60, 0, 6, 0),
            S(6, 0, 6, 4),
            S(6, 4, 0, 4),
            S(0, 4, 0, 0)
        })
        {
            kat.DxfSegmentleri.Add(new BpcDxfSegment { A = a, B = b });
        }

        int bulunan = BasitProjeCizDxfServisi.OdalariYenidenTara(kat);
        if (bulunan != 1 || kat.Odalar.Count != 1)
            throw new InvalidOperationException($"M12 yeniden taramada 1 oda beklenirken {kat.Odalar.Count} bulundu.");
        if (kat.Odalar[0].Poligon.Count != 4)
            throw new InvalidOperationException(
                $"M12 oda poligonu küçük çıkıntıyı düzleştirip 4 köşe üretmeli; bulunan: {kat.Odalar[0].Poligon.Count}.");
    }

    // DXF üzerinden sembol şablonu round-trip: export → değiştir → import.
    // Bir sembolün çizgilerini DXF'te override edip yeniden okuduğumuzda kütüphaneye
    // doğru BlokAdi ile uygulandığını doğrular.
    public static void SembolDxfRoundTrip()
    {
        // Geçici dosya yolu
        var tmpDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "eye4pro_sembol_test_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(tmpDir);
        var sablonPath = System.IO.Path.Combine(tmpDir, "sablon.dxf");

        try
        {
            var lib = new BasitProjeCizKutuphaneServisi();
            var servis = new BasitProjeCizSembolDxfServisi();

            // Export et — DXF dosyasında tüm blocklar olacak
            servis.SablonDisariAktar(lib, sablonPath);
            if (!System.IO.File.Exists(sablonPath))
                throw new InvalidOperationException("Şablon DXF üretilemedi.");

            // Geri yükle — eşleşen blocklar güncellenmeli
            var sonuc = servis.SembolleriYukle(sablonPath, lib);
            if (sonuc.Guncellendi == 0)
                throw new InvalidOperationException(
                    $"Round-trip: hiç sembol güncellenmedi. Eşleşmeyen: {sonuc.Eslesemedi.Count}, Hata: {sonuc.Hatalar.Count}");
            if (sonuc.Hatalar.Count > 0)
                throw new InvalidOperationException("Round-trip hatası: " + string.Join("; ", sonuc.Hatalar));

            // Kütüphanedeki bir bilinen blok hâlâ sembol çizgilerine sahip olmalı
            var anahtar = lib.Anahtarlar.FirstOrDefault(a => a.BlokAdi == "E4P_ANAHTAR");
            if (anahtar == null || anahtar.SembolCizgiler.Count == 0)
                throw new InvalidOperationException("Round-trip sonrası E4P_ANAHTAR sembol çizgileri kayboldu.");
        }
        finally
        {
            try { System.IO.Directory.Delete(tmpDir, recursive: true); } catch { }
        }
    }

    // Diagnostic: import raporu DXF'te bulunan ama tanınmayan + beklenen ama
    // bulunamayan + boş block'ları doğru şekilde listelemeli.
    public static void SembolImportRaporuDiagnosticVerir()
    {
        var tmpDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "eye4pro_sembol_diag_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(tmpDir);
        var dxfPath = System.IO.Path.Combine(tmpDir, "diag.dxf");

        try
        {
            // Manuel minimal DXF: 3 block oluştur
            //   • E4P_ANAHTAR — geçerli LINE (eşleşmeli, kütüphane günceller)
            //   • CUSTOM_FOO — geçerli LINE ama kütüphane tanımaz (Eslesemedi'de olmalı)
            //   • BOS_BLOK   — desteklenmeyen entity yok, hiç entity yok (BosBloklar'da olmalı)
            var dxf = new DxfDocument();

            var b1 = new Block("E4P_ANAHTAR");
            b1.Entities.Add(new Line(new Vector3(-1, 0, 0), new Vector3(1, 0, 0)));
            b1.Entities.Add(new Line(new Vector3(0, -1, 0), new Vector3(0, 1, 0)));
            dxf.Blocks.Add(b1);

            var b2 = new Block("CUSTOM_FOO");
            b2.Entities.Add(new Line(new Vector3(0, 0, 0), new Vector3(2, 2, 0)));
            dxf.Blocks.Add(b2);

            var b3 = new Block("BOS_BLOK");
            // Hiç entity yok
            dxf.Blocks.Add(b3);

            dxf.Save(dxfPath);

            var lib = new BasitProjeCizKutuphaneServisi();
            var servis = new BasitProjeCizSembolDxfServisi();
            var sonuc = servis.SembolleriYukle(dxfPath, lib);

            // E4P_ANAHTAR eşleşmeli — Guncellendi >= 1
            if (sonuc.Guncellendi < 1)
                throw new InvalidOperationException(
                    $"E4P_ANAHTAR eşleşmeli; Guncellendi={sonuc.Guncellendi}.");

            // CUSTOM_FOO kütüphanede yok → Eslesemedi'de olmalı
            if (!sonuc.Eslesemedi.Contains("CUSTOM_FOO"))
                throw new InvalidOperationException(
                    "CUSTOM_FOO Eslesemedi listesinde olmalı.");

            // BOS_BLOK desteklenen entity üretmedi → BosBloklar veya hiç okunmadı.
            // (netDxf bos block'u hiç döndürmüyorsa BeklenenAmaBulunamayan'da olabilir;
            //  her iki durumda da kullanıcı raporda görür.)
            // E4P_PRIZ DXF'te yok → BeklenenAmaBulunamayan'da olmalı
            if (!sonuc.BeklenenAmaBulunamayan.Contains("E4P_PRIZ"))
                throw new InvalidOperationException(
                    "E4P_PRIZ BeklenenAmaBulunamayan listesinde olmalı (kullanıcının priz block'unu " +
                    "şablonda silmiş/farklı isimle kaydetmiş olabileceğini bildirir).");

            // OkunanBloklar'da en az E4P_ANAHTAR ve CUSTOM_FOO olmalı
            if (sonuc.OkunanBloklar.Count < 2)
                throw new InvalidOperationException(
                    $"OkunanBloklar en az 2 (E4P_ANAHTAR + CUSTOM_FOO) içermeli; bulunan {sonuc.OkunanBloklar.Count}.");
        }
        finally
        {
            try { System.IO.Directory.Delete(tmpDir, recursive: true); } catch { }
        }
    }

    // Case-insensitive block adı eşleşmeli — AutoCAD bazen block adlarını
    // küçük/karışık harfe çevirir; bunu yakalamamız gerekir.
    public static void SembolImportCaseInsensitiveEslesir()
    {
        var tmpDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "eye4pro_sembol_case_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(tmpDir);
        var dxfPath = System.IO.Path.Combine(tmpDir, "case.dxf");

        try
        {
            var dxf = new DxfDocument();
            var b = new Block("e4p_anahtar"); // küçük harf
            b.Entities.Add(new Line(new Vector3(-1, 0, 0), new Vector3(1, 0, 0)));
            b.Entities.Add(new Line(new Vector3(0, -1, 0), new Vector3(0, 1, 0)));
            dxf.Blocks.Add(b);
            dxf.Save(dxfPath);

            var lib = new BasitProjeCizKutuphaneServisi();
            var servis = new BasitProjeCizSembolDxfServisi();
            var sonuc = servis.SembolleriYukle(dxfPath, lib);

            if (sonuc.Guncellendi < 1)
                throw new InvalidOperationException(
                    "Küçük harf block adı (e4p_anahtar) E4P_ANAHTAR ile eşleşmeli.");
        }
        finally
        {
            try { System.IO.Directory.Delete(tmpDir, recursive: true); } catch { }
        }
    }

    // Gömülü sembol şablonu (Core'a embed edilmiş EYE4PRO_Sembol_Sablonu.dxf) gerçekten
    // okunabilmeli ve kütüphane sembollerini düzgün geometriyle güncellemeli.
    // Bu test kaynak adı / LogicalName uyuşmazlığını ve importer regresyonunu yakalar.
    public static void GomuluSembolSablonuKaynaktanUygulanir()
    {
        var asm = typeof(BasitProjeCizKutuphaneServisi).Assembly;
        using var stream = asm.GetManifestResourceStream("EYE4PRO_Sembol_Sablonu.dxf");
        if (stream == null)
            throw new InvalidOperationException(
                "Gömülü sembol şablonu kaynağı bulunamadı (EYE4PRO_Sembol_Sablonu.dxf). " +
                "Core csproj'daki EmbeddedResource/LogicalName tanımını kontrol et.");

        var lib = new BasitProjeCizKutuphaneServisi();
        var sonuc = new BasitProjeCizSembolDxfServisi().SembolleriYukle(stream, lib);

        // Şablonda ~56 blok kütüphaneyle eşleşiyor; gerileme olursa burada düşer.
        if (sonuc.Guncellendi < 40)
            throw new InvalidOperationException(
                $"Gömülü şablondan en az 40 sembol güncellenmeli; Guncellendi={sonuc.Guncellendi}. " +
                $"({sonuc})");

        // E4P_PRIZ DXF'te elips+yay+çizgi olarak çizili → programatikten (≈4 segment)
        // belirgin daha fazla segment üretmeli (çoğullanmış eğriler).
        var priz = lib.Prizler.FirstOrDefault(p => p.BlokAdi == "E4P_PRIZ");
        if (priz == null || priz.SembolCizgiler.Count < 10)
            throw new InvalidOperationException(
                $"E4P_PRIZ gömülü şablondan düzgün geometri almalı; segment={priz?.SembolCizgiler.Count ?? -1}.");

        // Palet `Malzemeler` listesi de güncellenmeli (palet bu listeden beslenir).
        var palet = lib.Malzemeler.FirstOrDefault(m => m.BlokAdi == "E4P_PRIZ");
        if (palet == null || palet.SembolCizgiler.Count < 10)
            throw new InvalidOperationException(
                "Palet (Malzemeler) E4P_PRIZ girdisi de gömülü şablon geometrisini taşımalı.");
    }

    // Priz ailesi EMO standardı geometri doğrulaması:
    // T çubuğu dome tepesini geçmemeli, tam dome tepesinde olmalı.
    public static void PrizSembolTCubukDomeTepesinde()
    {
        // PrizSembol: dome merkez (0,-0.30), r=0.45 → tepe y=0.15
        var priz = BasitProjeCizKutuphaneServisi.PrizSembol();
        double domeTepe = -0.30 + 0.45; // 0.15
        // T çubuğu yatay segment: her iki y koordinatı dome tepesinde olmalı
        var tCubuk = priz.Where(s => Math.Abs(s.Ay - s.By) < 1e-9 && s.Ay > -0.30).ToList();
        if (tCubuk.Count == 0)
            throw new InvalidOperationException("PrizSembol: T çubuğu (yatay segment) bulunamadı.");
        foreach (var t in tCubuk)
            if (Math.Abs(t.Ay - domeTepe) > 0.01)
                throw new InvalidOperationException(
                    $"PrizSembol: T çubuğu y={t.Ay:F3} dome tepesinden ({domeTepe:F3}) sapıyor.");
        // Dikey toprak pimi dome tepesini geçmemeli
        var dikeyPin = priz.Where(s => Math.Abs(s.Ax - s.Bx) < 1e-9 && s.Ax > -0.10 && s.Ax < 0.10).ToList();
        foreach (var p in dikeyPin)
        {
            double maxY = Math.Max(p.Ay, p.By);
            if (maxY > domeTepe + 0.01)
                throw new InvalidOperationException(
                    $"PrizSembol: toprak pimi y={maxY:F3} dome tepesini ({domeTepe:F3}) aşıyor.");
        }

        // İkiliPrizSembol: sol dome merkez (-0.40,-0.30), r=0.35 → tepe y=0.05
        var ikili = BasitProjeCizKutuphaneServisi.IkiliPrizSembol();
        double ikiliTepe = -0.30 + 0.35; // 0.05
        var ikiliTCubuklari = ikili.Where(s => Math.Abs(s.Ay - s.By) < 1e-9 && s.Ay > -0.30).ToList();
        if (ikiliTCubuklari.Count < 2)
            throw new InvalidOperationException($"İkiliPrizSembol: 2 T çubuğu bekleniyor, bulunan {ikiliTCubuklari.Count}.");
        foreach (var t in ikiliTCubuklari)
            if (Math.Abs(t.Ay - ikiliTepe) > 0.01)
                throw new InvalidOperationException(
                    $"İkiliPrizSembol: T çubuğu y={t.Ay:F3} dome tepesinden ({ikiliTepe:F3}) sapıyor.");

        // KombinePrizKutusuSembol: dome merkez (cx,-0.40), r=0.35 → tepe y=-0.05
        var kombin = BasitProjeCizKutuphaneServisi.KombinePrizKutusuSembol();
        double kombinTepe = -0.40 + 0.35; // -0.05
        var kombinTler = kombin.Where(s => Math.Abs(s.Ay - s.By) < 1e-9 && s.Ay > -0.40 && s.Ay < 0.10).ToList();
        if (kombinTler.Count < 2)
            throw new InvalidOperationException($"KombinePriz: 2 T çubuğu bekleniyor, bulunan {kombinTler.Count}.");
        foreach (var t in kombinTler)
            if (Math.Abs(t.Ay - kombinTepe) > 0.01)
                throw new InvalidOperationException(
                    $"KombinePriz: T çubuğu y={t.Ay:F3} dome tepesinden ({kombinTepe:F3}) sapıyor.");
    }

    // Gömülü DXF şablonunu güncel programatik sembollerden yeniden üretir.
    // Bu test, şablonu kaynak dizinine yazar. Normalde geliştirici elle çalıştırır.
    // Program.cs'e kayıt yok — sadece gerektiğinde manuel çağrılır:
    //   dotnet test --filter "FullyQualifiedName~SembolSablonunuYeniden"
    public static void SembolSablonunuYeniden_Uret_MANUEL()
    {
        // Kaynak dizinindeki DXF şablonunu bul
        var baseDir = AppContext.BaseDirectory;
        // bin/Debug/net10.0 → proje kökü → Core projesindeki Sablonlar klasörü
        var sablonDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..",
            "Kst.Acoustics.Core", "Electric", "BasitProjeCiz", "Sablonlar"));
        var hedefPath = Path.Combine(sablonDir, "EYE4PRO_Sembol_Sablonu.dxf");

        if (!Directory.Exists(sablonDir))
            throw new InvalidOperationException(
                $"Sablonlar klasörü bulunamadı: {sablonDir}. Kaynak yapısı değişti mi?");

        var lib = new BasitProjeCizKutuphaneServisi();
        new BasitProjeCizSembolDxfServisi().SablonDisariAktar(lib, hedefPath);

        if (!File.Exists(hedefPath))
            throw new InvalidOperationException("DXF şablonu üretilemedi.");

        var info = new FileInfo(hedefPath);
        if (info.Length < 10_000)
            throw new InvalidOperationException($"Üretilen DXF çok küçük ({info.Length} byte). İçerik eksik olabilir.");
    }

    // Donma regresyonu: çok odalı ızgara + gürültü + dev başıboş segment (MaxHücre
    // büyütmesini tetikler). Makul sürede (saniyeler) bitmeli ve oda bulmalı.
    public static void BuyukKarmasikPlanMakulSuredeBiter()
    {
        var rnd = new Random(42);
        var duvarlar = new List<(Point2D, Point2D)>();

        // 8x8 oda ızgarası, her hücre 5x5 m (40x40 m bina)
        const int n = 8;
        const double a = 5.0;
        for (int i = 0; i <= n; i++)
        {
            duvarlar.Add(S(0, i * a, n * a, i * a));   // yatay duvarlar
            duvarlar.Add(S(i * a, 0, i * a, n * a));   // dikey duvarlar
        }
        // Her odaya rastgele gürültü segmentleri (mobilya/kot çizgileri)
        for (int gx = 0; gx < n; gx++)
        for (int gy = 0; gy < n; gy++)
        {
            double cx = gx * a + a / 2, cy = gy * a + a / 2;
            for (int s = 0; s < 4; s++)
            {
                double x1 = cx + (rnd.NextDouble() - 0.5) * 2;
                double y1 = cy + (rnd.NextDouble() - 0.5) * 2;
                double x2 = x1 + (rnd.NextDouble() - 0.5);
                double y2 = y1 + (rnd.NextDouble() - 0.5);
                duvarlar.Add(S(x1, y1, x2, y2));
            }
        }
        // Bounding box'u patlatan başıboş segment → MaxHücre auto-grow tetiklenir
        duvarlar.Add(S(-500, -500, -499, -499));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var odalar = BinaServices.BosluktanOdaTespitServisi.OdalariBul(duvarlar);
        sw.Stop();

        if (sw.Elapsed.TotalSeconds > 20)
            throw new InvalidOperationException(
                $"Boşluk tarama çok yavaş ({sw.Elapsed.TotalSeconds:F1} s) — donma riski.");
        if (odalar.Count == 0)
            throw new InvalidOperationException("Karmaşık planda hiç oda bulunamadı.");
    }

    // Taşma/donma regresyonu: çok uzakta başıboş bir segment (CAD'de sık) bounding
    // box'ı patlatmamalı. Grid int taşması → devasa tahsis → makine donması olmamalı;
    // gerçek bina yine bulunmalı.
    public static void BasibosUzakSegmentDonmazVeOdaBulur()
    {
        var duvarlar = new List<(Point2D, Point2D)>
        {
            // 6x4 normal oda
            S(0, 0, 6, 0), S(6, 0, 6, 4), S(6, 4, 0, 4), S(0, 4, 0, 0),
            // ~100.000 km uzakta başıboş segment (int taşmasını tetiklerdi)
            S(1e8, 1e8, 1e8 + 1, 1e8 + 1)
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var odalar = BinaServices.BosluktanOdaTespitServisi.OdalariBul(duvarlar);
        sw.Stop();

        if (sw.Elapsed.TotalSeconds > 20)
            throw new InvalidOperationException(
                $"Başıboş uzak segmentte donma riski ({sw.Elapsed.TotalSeconds:F1} s).");
        if (odalar.Count != 1)
            throw new InvalidOperationException(
                $"Başıboş segment kırpılıp gerçek oda bulunmalı; bulunan: {odalar.Count}.");
    }

    // Segment sadeleştirme: çok kısa mikro parçalar elenir, kalan tavanla sınırlanır.
    // Eski .e4p12 migrasyonu: 60K ham segmentli kat sadeleştirilir; eşik altı
    // kat (1K segment) dokunulmaz.
    public static void EskiSegmentMigrasyonuEsikUstuKatlariSadelestirir()
    {
        var proje = new Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BasitProjeCizProje();

        // Kat 1: 60.000 ham segment (eşik 50K aşılır) — %95'i 1 cm mikro parça
        var k1 = new Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BasitProjeKat
        { KatNo = 0, Ad = "Zemin" };
        for (int i = 0; i < 57_000; i++)
            k1.DxfSegmentleri.Add(new Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BpcDxfSegment
            { A = new Point2D(i * 0.01, 0), B = new Point2D(i * 0.01 + 0.01, 0) });
        for (int i = 0; i < 3_000; i++)
            k1.DxfSegmentleri.Add(new Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BpcDxfSegment
            { A = new Point2D(i, 5), B = new Point2D(i + 2.0, 5) });
        proje.Katlar.Add(k1);

        // Kat 2: 1.000 segment — eşik altı, dokunulmamalı
        var k2 = new Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BasitProjeKat
        { KatNo = 1, Ad = "1. Kat" };
        for (int i = 0; i < 1_000; i++)
            k2.DxfSegmentleri.Add(new Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BpcDxfSegment
            { A = new Point2D(i, 0), B = new Point2D(i + 1.0, 0) });
        proje.Katlar.Add(k2);

        int kat2OnceCount = k2.DxfSegmentleri.Count;

        var (etkilenen, once, sonra) = BasitProjeCizDxfServisi.EskiSegmentMigrasyonuUygula(proje);

        if (etkilenen != 1)
            throw new InvalidOperationException(
                $"Yalnız Kat 1 etkilenmeli; etkilenen = {etkilenen}.");
        if (once != 60_000)
            throw new InvalidOperationException(
                $"Önce toplam 60.000 olmalı; bulunan {once}.");
        if (sonra >= 10_000)
            throw new InvalidOperationException(
                $"Sonra 10K'nın altında olmalı (mikro parçalar elenmeli); bulunan {sonra}.");

        // Kat 2 hiç değişmemiş olmalı
        if (k2.DxfSegmentleri.Count != kat2OnceCount)
            throw new InvalidOperationException(
                $"Eşik altı kat dokunulmamalı; kat 2 önce {kat2OnceCount}, sonra {k2.DxfSegmentleri.Count}.");

        // Boş projede no-op
        var bos = new Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BasitProjeCizProje();
        var (e, o, s) = BasitProjeCizDxfServisi.EskiSegmentMigrasyonuUygula(bos);
        if (e != 0 || o != 0 || s != 0)
            throw new InvalidOperationException("Boş projede migrasyon no-op olmalı.");
    }

    public static void SegmentSadelestirmeKisalariElerVeTavaniUygular()
    {
        var segs = new List<(Point2D, Point2D)>();
        // 1000 adet 1 cm'lik mikro parça (görünmez gürültü)
        for (int i = 0; i < 1000; i++)
            segs.Add(S(i, 0, i + 0.01, 0));
        // 50 adet 2 m'lik gerçek duvar
        for (int i = 0; i < 50; i++)
            segs.Add(S(i, 5, i + 2.0, 5));

        var sade = BasitProjeCizDxfServisi.SadelestirSegmentler(segs, minUzunlukM: 0.05).ToList();
        if (sade.Count != 50)
            throw new InvalidOperationException(
                $"5 cm altı mikro parçalar elenmeli; beklenen 50, bulunan {sade.Count}.");

        // Tavan testi: 100 uzun segment, tavan 30 → en uzun 30 tutulur
        var cok = new List<(Point2D, Point2D)>();
        for (int i = 0; i < 100; i++)
            cok.Add(S(0, i, i + 1.0, i)); // uzunluk i+1, artan
        var capli = BasitProjeCizDxfServisi.SadelestirSegmentler(cok, minUzunlukM: 0.05, maxSegment: 30).ToList();
        if (capli.Count != 30)
            throw new InvalidOperationException($"Tavan 30 uygulanmalı; bulunan {capli.Count}.");
    }

    // Oda adı seçimi: poligon içindeki isim-benzeri metin atanır; alan/ölçü elenir.
    public static void OdaAdiPoligonIcindekiMetindenSecilir()
    {
        var poly = new List<Point2D> { new(0, 0), new(10, 0), new(10, 10), new(0, 10) };

        // İçeride "SALON" + alan etiketi "12.5 m2"; dışarıda "MUTFAK"
        var metinler = new List<(Point2D, string)>
        {
            (new Point2D(5, 5), "SALON"),
            (new Point2D(7, 7), "12.5 m2"),
            (new Point2D(20, 20), "MUTFAK")
        };

        var ad = BasitProjeCizDxfServisi.OdaAdiSec(poly, metinler);
        if (ad != "SALON")
            throw new InvalidOperationException($"Poligon içindeki isim 'SALON' seçilmeli; bulunan: '{ad}'.");

        // Yalnız alan etiketi varsa isim seçilmemeli (null)
        var sadeceAlan = new List<(Point2D, string)> { (new Point2D(5, 5), "250 m²") };
        var ad2 = BasitProjeCizDxfServisi.OdaAdiSec(poly, sadeceAlan);
        if (ad2 != null)
            throw new InvalidOperationException($"Alan etiketi isim sayılmamalı; bulunan: '{ad2}'.");

        // Dışarıdaki metin atanmamalı
        var disarda = new List<(Point2D, string)> { (new Point2D(50, 50), "DEPO") };
        var ad3 = BasitProjeCizDxfServisi.OdaAdiSec(poly, disarda);
        if (ad3 != null)
            throw new InvalidOperationException($"Poligon dışındaki metin atanmamalı; bulunan: '{ad3}'.");
    }

    // M12 importu CAD'deki ortak DXF koordinat çerçevesini korumalı. Parser her katı
    // bağımsız min köşeye normalize ederse CAD'de hizalanmış katlar uygulamada kayar.
    public static void M12DxfYuklemeOrtakCadKoordinatiniKorur()
    {
        var path1 = Path.Combine(Path.GetTempPath(), $"e4p-m12-origin-a-{Guid.NewGuid():N}.dxf");
        var path2 = Path.Combine(Path.GetTempPath(), $"e4p-m12-origin-b-{Guid.NewGuid():N}.dxf");
        try
        {
            DikdortgenDxfYaz(path1, 0, 0);
            DikdortgenDxfYaz(path2, 100, 25);

            var servis = new BasitProjeCizDxfServisi();
            var kat1 = servis.KatYukle(path1, 0, "Zemin", 3.0);
            var kat2 = servis.KatYukle(path2, 1, "Kat 1", 3.0);

            double min1X = kat1.DxfSegmentleri.Min(s => Math.Min(s.A.X, s.B.X));
            double min1Y = kat1.DxfSegmentleri.Min(s => Math.Min(s.A.Y, s.B.Y));
            double min2X = kat2.DxfSegmentleri.Min(s => Math.Min(s.A.X, s.B.X));
            double min2Y = kat2.DxfSegmentleri.Min(s => Math.Min(s.A.Y, s.B.Y));

            if (Math.Abs((min2X - min1X) - 100) > 0.1 || Math.Abs((min2Y - min1Y) - 25) > 0.1)
                throw new InvalidOperationException(
                    $"M12 DXF importu ortak CAD koordinatını korumalı; fark ({min2X - min1X:F2}, {min2Y - min1Y:F2}).");
        }
        finally
        {
            if (File.Exists(path1)) File.Delete(path1);
            if (File.Exists(path2)) File.Delete(path2);
        }
    }

    private static void DikdortgenDxfYaz(string path, double ox, double oy)
    {
        var doc = new DxfDocument();
        doc.DrawingVariables.InsUnits = DrawingUnits.Meters;

        var p1 = new Vector3(ox, oy, 0);
        var p2 = new Vector3(ox + 8, oy, 0);
        var p3 = new Vector3(ox + 8, oy + 5, 0);
        var p4 = new Vector3(ox, oy + 5, 0);

        doc.Entities.Add(new Line(p1, p2));
        doc.Entities.Add(new Line(p2, p3));
        doc.Entities.Add(new Line(p3, p4));
        doc.Entities.Add(new Line(p4, p1));
        doc.Save(path);
    }

    private static void IkiOdaliSantimetreCizimMmBaslikliDxfYaz(string path)
    {
        var doc = new DxfDocument();
        doc.DrawingVariables.InsUnits = DrawingUnits.Millimeters;

        void L(double ax, double ay, double bx, double by)
            => doc.Entities.Add(new Line(new Vector3(ax, ay, 0), new Vector3(bx, by, 0)));

        // Koordinatlar pratikte santimetre: 20 m x 8 m bina, ortadan iki oda.
        // Başlık ise milimetre dediği için ilk okuma 2 m x 0.8 m gibi zayıf kalır.
        L(0, 0, 2000, 0);
        L(2000, 0, 2000, 800);
        L(2000, 800, 0, 800);
        L(0, 800, 0, 0);
        L(1000, 0, 1000, 800);

        doc.Entities.Add(new Text("ODA A", new Vector3(500, 400, 0), 80));
        doc.Entities.Add(new Text("ODA B", new Vector3(1500, 400, 0), 80));
        doc.Entities.Add(new Text("HOL", new Vector3(1000, 100, 0), 80));
        doc.Save(path);
    }

    // Küçük kapalı alanlar (min alan altı) oda sayılmaz.
    // Adaptif ikinci geçiş: ilk geçişte tek dev oda (iç duvar uçlarındaki 1.5 m
    // boşluklar varsayılan 0.50 m kapatmayla mühürlenemez) → ikinci geçiş daha
    // agresif kapatma (0.80) ile odayı ikiye böler.
    public static void AdaptifIkinciGecisDevOdayiBoler()
    {
        // 20x10 bina + tam ortada y=5'te iç duvar; iki ucunda 1.5 m kapı boşluğu
        var duvarlar = new List<(Point2D, Point2D)>
        {
            // Dış çerçeve
            S(0, 0, 20, 0), S(20, 0, 20, 10), S(20, 10, 0, 10), S(0, 10, 0, 0),
            // İç bölme duvarı (uçlarda 1.5 m kapı boşluğu — varsayılan kapatma yetmez)
            S(1.5, 5, 18.5, 5),
        };

        // 1) Normal tarama: 1.5 m boşluk varsayılan KapiKapatma=0.5 ile kapanmaz → tek dev oda
        var ilk = BinaServices.BosluktanOdaTespitServisi.OdalariBul(duvarlar);
        if (ilk.Count != 1)
            throw new InvalidOperationException(
                $"İlk geçişte tek dev oda beklenirken {ilk.Count} bulundu.");

        // 2) Adaptif tarama: dev oda ikinci geçişte (KapiKapatma=0.80) ikiye bölünür
        var adaptif = BinaServices.BosluktanOdaTespitServisi.OdalariAdaptifBul(duvarlar);
        if (adaptif.Count != 2)
            throw new InvalidOperationException(
                $"Adaptif ikinci geçiş 2 alt oda beklerken {adaptif.Count} bulundu.");

        // Her alt oda yaklaşık 100 m² (yarım bina)
        foreach (var oda in adaptif)
        {
            double alan = PoligonYardimci.Alan(oda);
            if (alan < 70 || alan > 130)
                throw new InvalidOperationException(
                    $"Alt oda alanı ~100 m² beklenirken {alan:F1} bulundu.");
        }
    }

    // Adaptif ikinci geçiş, makul boyuttaki normal planları bozmamalı —
    // dev eşiği aşılmıyorsa ikinci geçiş çağrılmaz, sonuç değişmez.
    public static void AdaptifIkinciGecisKucukOdalariEtkilemez()
    {
        var duvarlar = new List<(Point2D, Point2D)>
        {
            // 6x4 oda — toplam bbox = 24, dev eşik = 6. Tek oda alanı 24, 24 > 6 ama
            // ikinci geçişte ek bölme bulunmaz → dev oda korunur.
            S(0, 0, 6, 0), S(6, 0, 6, 4), S(6, 4, 0, 4), S(0, 4, 0, 0)
        };

        var sonuc = BinaServices.BosluktanOdaTespitServisi.OdalariAdaptifBul(duvarlar);
        if (sonuc.Count != 1)
            throw new InvalidOperationException(
                $"Tek oda korunmalı; {sonuc.Count} oda bulundu.");
    }

    public static void KucukAlanOdaSayilmaz()
    {
        var duvarlar = new List<(Point2D, Point2D)>
        {
            // 1x1 = 1 m² (min 2 m² altında)
            S(0, 0, 1, 0), S(1, 0, 1, 1), S(1, 1, 0, 1), S(0, 1, 0, 0)
        };

        var odalar = BinaServices.BosluktanOdaTespitServisi.OdalariBul(duvarlar);

        if (odalar.Count != 0)
            throw new InvalidOperationException(
                $"1 m² alan oda sayılmamalı; {odalar.Count} oda bulundu.");
    }

    // DxfKatYukleyici ile BasitProjeCizDxfServisi.KatYukle aynı DXF'ten aynı
    // oda sayısını ve segment sayısını üretmeli.
    public static void DxfKatYukleyiciM12IleAyniSonucu()
    {
        var path = Path.Combine(Path.GetTempPath(), $"e4p-yukleyici-{Guid.NewGuid():N}.dxf");
        try
        {
            DikdortgenDxfYaz(path, 0, 0);

            var m12Kat = new BasitProjeCizDxfServisi().KatYukle(path, 1, "Test", 3.0);
            var binaKat = new BinaServices.DxfKatYukleyici().Yukle(path, 1, "Test", 3.0,
                new BinaServices.DxfKatYukleyici.Ayarlar { NormalizeOrigin = false });

            if (m12Kat.Odalar.Count != binaKat.Odalar.Count)
                throw new InvalidOperationException(
                    $"Oda sayısı uyuşmuyor: M12={m12Kat.Odalar.Count}, Yukleyici={binaKat.Odalar.Count}.");

            if (m12Kat.DxfSegmentleri.Count == 0)
                throw new InvalidOperationException("M12 segmentsiz döndü.");
            if (binaKat.DxfSegmentler.Count == 0)
                throw new InvalidOperationException("DxfKatYukleyici segmentsiz döndü.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public static void DxfKatYukleyiciZayifTaramadaOlcegiDuzeltir()
    {
        var path = Path.Combine(Path.GetTempPath(), $"e4p-yukleyici-scale-{Guid.NewGuid():N}.dxf");
        try
        {
            IkiOdaliSantimetreCizimMmBaslikliDxfYaz(path);

            var binaKat = new BinaServices.DxfKatYukleyici().Yukle(path, 1, "Test", 3.0,
                new BinaServices.DxfKatYukleyici.Ayarlar { NormalizeOrigin = false });

            if (binaKat.Odalar.Count == 0)
                throw new InvalidOperationException("Ölçek düzeltme sonrası en az bir oda bekleniyordu.");

            var toplamAlan = binaKat.Odalar.Sum(o => o.AlanM2);
            if (toplamAlan < 140 || toplamAlan > 180)
                throw new InvalidOperationException($"Toplam oda alanı ~160 m² beklenir; bulunan: {toplamAlan:F1}.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
