using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kst.Acoustics.App.Electric.CevreAydinlatma;
using Kst.Acoustics.Core.Bina.Services;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Electric.Calculation;
using Kst.Acoustics.Core.Electric.CevreAydinlatma;
using Kst.Acoustics.Core.Electric.CevreAydinlatma.Fotometri;
using Kst.Acoustics.Core.Electric.Models;
using Kst.Acoustics.Core.Electric.Ortak;
using netDxf;
using netDxf.Entities;
using netDxf.Tables;

namespace Kst.Acoustics.Tests;

public static class CevreAydinlatmaTests
{
    // EULUMDAT (.ldt): Isym=0, Mc=4 (C 0/90/180/270), Ng=4 (γ 0/30/60/90).
    // 1 lamba, 12000 lm, 100 W. Işık şiddeti cd/1000 lm. Her değer kendi satırında.
    private const string OrnekLdt = """
EYE4PRO TEST
1
0
4
90
4
30
RPT-001
EYE4PRO Park Armaturu
PRK-100
prk100
2026-05-28
700
300
150
600
250
0
0
0
0
100
95
1.0
0
1
1
LED
12000
4000K
80
100
0
0
0
0
0
0
0
0
0
0
0
90
180
270
0
30
60
90
300
250
150
0
300
240
140
0
300
250
150
0
300
240
140
0
""";

    // IESNA LM-63: 1 lamba × 10000 lm, çarpan 1, 4 dikey (γ) × 4 yatay (C), 120 W.
    // Kandela mutlak; parser cd/1000 lm'ye çevirir (raw/10).
    private const string OrnekIes = """
IESNA:LM-63-2002
[TEST] RPT-IES-001
[MANUFAC] EYE4PRO
[LUMCAT] PRK-200
[LUMINAIRE] EYE4PRO Park IES Armaturu
TILT=NONE
1 10000 1 4 4 1 2 0.3 0.7 0.15
1 1 120
0 30 60 90
0 90 180 270
500 400 250 0
500 390 240 0
500 400 250 0
500 390 240 0
""";

    public static void LdtParkArmaturunuOkur()
    {
        var f = LdtParser.Ayristir(OrnekLdt);

        AssertEqual("EYE4PRO Park Armaturu", f.ArmaturAdi, "LDT armatür adı");
        AssertClose(100, f.ToplamGucW, "LDT sistem gücü (W)");
        AssertClose(12000, f.ToplamAkiLm, "LDT toplam akı (lm)");
        if (f.LambaSayisi != 1)
            throw new InvalidOperationException($"LDT lamba sayısı: beklenen 1, bulunan {f.LambaSayisi}.");
        if (f.CDuzlemAcilari.Length != 4 || f.GamaAcilari.Length != 4)
            throw new InvalidOperationException("LDT açı sayıları (Mc/Ng) yanlış okundu.");
        AssertClose(90, f.CDuzlemAcilari[1], "LDT C düzlemi açısı");
        AssertClose(90, f.GamaAcilari[3], "LDT gama açısı");
        AssertClose(300, f.IsikSiddeti_cd_klm[0][0], "LDT C0/γ0 ışık şiddeti (cd/klm)");
        AssertClose(0, f.IsikSiddeti_cd_klm[0][3], "LDT C0/γ90 ışık şiddeti (cd/klm)");
        AssertClose(3600, f.KandelaMaks(), "LDT en yüksek kandela (cd)");
    }

    public static void IesParkArmaturunuOkur()
    {
        var f = IesParser.Ayristir(OrnekIes);

        AssertEqual("EYE4PRO Park IES Armaturu", f.ArmaturAdi, "IES armatür adı");
        AssertClose(120, f.ToplamGucW, "IES sistem gücü (W)");
        AssertClose(10000, f.ToplamAkiLm, "IES toplam akı (lm)");
        if (f.LambaSayisi != 1)
            throw new InvalidOperationException($"IES lamba sayısı: beklenen 1, bulunan {f.LambaSayisi}.");
        if (f.CDuzlemAcilari.Length != 4 || f.GamaAcilari.Length != 4)
            throw new InvalidOperationException("IES açı sayıları yanlış okundu.");
        AssertClose(90, f.CDuzlemAcilari[1], "IES yatay (C) açısı");
        AssertClose(90, f.GamaAcilari[3], "IES dikey (γ) açısı");
        AssertClose(50, f.IsikSiddeti_cd_klm[0][0], "IES C0/γ0 ışık şiddeti (cd/klm)");
        AssertClose(0, f.IsikSiddeti_cd_klm[0][3], "IES C0/γ90 ışık şiddeti (cd/klm)");
        AssertClose(500, f.KandelaMaks(), "IES en yüksek kandela (cd)");
    }

    public static void BosIcerikHataVerir()
    {
        AssertThrows(() => LdtParser.Ayristir(""), "Boş LDT hata vermeli.");
        AssertThrows(() => IesParser.Ayristir("   "), "Boş IES hata vermeli.");
    }

    // Faz 1 — M12 ile paylaşılan ortak yerleşim geometrisi (direk dizilimi temeli).
    public static void YerlesimGeometriKenarVeIzgaraUretir()
    {
        var kare = new List<Point2D> { new(0, 0), new(10, 0), new(10, 10), new(0, 10) };

        var izgara = YerlesimGeometri.IzgaraAdaylariUret(kare, adim: 2, kenarMin: 1);
        if (izgara.Count != 25)
            throw new InvalidOperationException($"Izgara aday sayısı: beklenen 25, bulunan {izgara.Count}.");
        foreach (var n in izgara)
            if (YerlesimGeometri.KenarlardanMinMesafe(n, kare) < 1 - 1e-9)
                throw new InvalidOperationException("Izgara noktası kenara çok yakın.");

        var kenar = YerlesimGeometri.DuvarAdaylariUret(kare, adim: 5, giris: 1);
        if (kenar.Count == 0)
            throw new InvalidOperationException("Kenar adayları boş döndü.");
        bool altKenarIceride = kenar.Any(p => Math.Abs(p.X - 5) < 1e-6 && Math.Abs(p.Y - 1) < 1e-6);
        if (!altKenarIceride)
            throw new InvalidOperationException("Alt kenardan 1 m içeri kaydırılmış (5,1) adayı bulunamadı.");
    }

    // Faz 1 — ortak DXF segment sadeleştirme (büyük park DXF'i için kritik; M15 doğrudan kullanır).
    public static void DxfSegmentSadelestirmeKisalariEler()
    {
        var segmentler = new List<(Point2D A, Point2D B)>
        {
            (new(0, 0), new(0, 0.02)),  // 2 cm  → elenir
            (new(0, 0), new(0, 0.10)),  // 10 cm → kalır
            (new(0, 0), new(5, 0))      // 5 m   → kalır
        };

        var sade = DxfSegmentYardimci.Sadelestir(segmentler).ToList();
        if (sade.Count != 2)
            throw new InvalidOperationException($"5 cm altı elenmeli: beklenen 2, bulunan {sade.Count}.");

        var tavanli = DxfSegmentYardimci.Sadelestir(segmentler, minUzunlukM: 0.0, maxSegment: 1).ToList();
        if (tavanli.Count != 1)
            throw new InvalidOperationException($"Tavan 1 olmalı: bulunan {tavanli.Count}.");
        if (Math.Abs(tavanli[0].B.X - 5) > 1e-9)
            throw new InvalidOperationException("Tavan uygulanınca en uzun segment tutulmalı.");
    }

    // Faz 2 — katman adından yüzey kategorisi tahmini (Türkçe/İngilizce, aksan/harf duyarsız).
    public static void YuzeyKategoriKatmandanEslesir()
    {
        AssertKategori("YESIL", YuzeyKategori.YesilAlan);
        AssertKategori("Yeşil Alan", YuzeyKategori.YesilAlan);
        AssertKategori("PEYZAJ-01", YuzeyKategori.YesilAlan);
        AssertKategori("YOL", YuzeyKategori.Yol);
        AssertKategori("Yaya Yolu", YuzeyKategori.Yol);
        AssertKategori("Spor Sahası", YuzeyKategori.SporAlani);
        AssertKategori("BINA", YuzeyKategori.Bina);
        AssertKategori("KOTA", YuzeyKategori.Diger);
        AssertKategori("", YuzeyKategori.Diger);
    }

    // Faz 2 — DXF'teki kapalı polyline'lar kategorili yüzeye çevrilir; açık polyline atlanır.
    public static void KapaliPoligonlarYuzeyeCevrilir()
    {
        var doc = new DxfDocument();
        doc.Entities.Add(new Polyline2D(new[]
        {
            new Polyline2DVertex(0, 0), new Polyline2DVertex(10, 0),
            new Polyline2DVertex(10, 10), new Polyline2DVertex(0, 10)
        }, true) { Layer = new Layer("YESIL") });
        doc.Entities.Add(new Polyline2D(new[]
        {
            new Polyline2DVertex(0, 0), new Polyline2DVertex(20, 0),
            new Polyline2DVertex(20, 2), new Polyline2DVertex(0, 2)
        }, true) { Layer = new Layer("YOL") });
        doc.Entities.Add(new Polyline2D(new[]
        {
            new Polyline2DVertex(0, 0), new Polyline2DVertex(5, 0), new Polyline2DVertex(5, 5)
        }, false) { Layer = new Layer("YESIL") });

        var yuzeyler = CevreYuzeyImportServisi.KapaliPoligonlardanYuzeyler(doc, 1.0);
        if (yuzeyler.Count != 2)
            throw new InvalidOperationException($"Açık polyline atlanmalı: beklenen 2 yüzey, bulunan {yuzeyler.Count}.");

        var yesil = yuzeyler.Single(y => y.Kategori == YuzeyKategori.YesilAlan);
        var yol = yuzeyler.Single(y => y.Kategori == YuzeyKategori.Yol);
        AssertClose(100, yesil.AlanM2, "Yeşil alan m²");
        AssertClose(40, yol.AlanM2, "Yol m²");
    }

    // Faz 3 — LDT/IES fotometriden armatür kütüphane tanımı üretimi (güç/akı/ad eşlenir).
    public static void ArmaturFotometridenUretilir()
    {
        var ldt = ArmaturTanimFabrikasi.FotometridenOlustur(LdtParser.Ayristir(OrnekLdt), id: 1);
        AssertEqual("EYE4PRO Park Armaturu", ldt.Ad, "LDT armatür adı");
        AssertClose(100, ldt.GucW, "LDT armatür gücü");
        AssertClose(12000, ldt.AkiLm, "LDT armatür akısı");
        if (ldt.FotometriKaynak != FotometriKaynak.Ldt)
            throw new InvalidOperationException("LDT armatür kaynağı Ldt olmalı.");

        var ies = ArmaturTanimFabrikasi.FotometridenOlustur(IesParser.Ayristir(OrnekIes), id: 2);
        AssertEqual("EYE4PRO Park IES Armaturu", ies.Ad, "IES armatür adı");
        AssertClose(120, ies.GucW, "IES armatür gücü");
        AssertClose(10000, ies.AkiLm, "IES armatür akısı");
        if (ies.FotometriKaynak != FotometriKaynak.Ies)
            throw new InvalidOperationException("IES armatür kaynağı Ies olmalı.");
    }

    // Faz 4 — açık güzergâh boyunca eşit aralıklı direk yerleşimi.
    public static void GuzergahaEsitAralikYerlesir()
    {
        var duz = DirekYerlesimServisi.GuzergahaYerlestir(
            new List<Point2D> { new(0, 0), new(10, 0) }, aralikM: 5);
        if (duz.Count != 3)
            throw new InvalidOperationException($"Düz güzergâh: beklenen 3 direk, bulunan {duz.Count}.");
        AssertClose(0, duz[0].X, "Direk 0 X");
        AssertClose(5, duz[1].X, "Direk 1 X");
        AssertClose(10, duz[2].X, "Direk 2 X");

        // L şeklinde güzergâh: (0,0)-(0,10)-(10,10), toplam 20 m, 5 m aralık → 5 direk.
        var l = DirekYerlesimServisi.GuzergahaYerlestir(
            new List<Point2D> { new(0, 0), new(0, 10), new(10, 10) }, aralikM: 5);
        if (l.Count != 5)
            throw new InvalidOperationException($"L güzergâh: beklenen 5 direk, bulunan {l.Count}.");
        AssertClose(0, l[2].X, "Köşe direği X");
        AssertClose(10, l[2].Y, "Köşe direği Y");
        AssertClose(10, l[4].X, "Son direk X");
        AssertClose(10, l[4].Y, "Son direk Y");
    }

    // Faz 4 — kapalı poligon çevresine yerleşim (dikiş tekrarı ayıklanır).
    public static void PoligonCevresineYerlesir()
    {
        var kare = new List<Point2D> { new(0, 0), new(10, 0), new(10, 10), new(0, 10) };
        var direkler = DirekYerlesimServisi.PoligonCevresineYerlestir(kare, aralikM: 10, kenarOfsetM: 0);
        if (direkler.Count != 4)
            throw new InvalidOperationException($"Kare çevresi (çevre 40, aralık 10): beklenen 4 direk, bulunan {direkler.Count}.");

        var nesneler = DirekYerlesimServisi.DireklerOlustur(direkler, guzergahId: 7, direkTanimId: 3, armaturTanimId: 9);
        if (nesneler.Count != 4)
            throw new InvalidOperationException("Direk nesneleri sayısı yanlış.");
        if (nesneler[0].GuzergahId != 7 || nesneler[0].DirekTanimId != 3 || nesneler[0].ArmaturTanimId != 9)
            throw new InvalidOperationException("Direk nesnesi güzergâh/tanım atamaları yanlış.");
    }

    // Faz 5 — lümen metodu aydınlatma hesabı (Kademe A).
    public static void LumenMetoduHesaplar()
    {
        // E = N·Φ·MF·UF / A = 10·10000·0.8·0.5 / 1000 = 40 lüks.
        double e = AydinlatmaHesapServisi.OrtalamaAydinlik(10, 10000, 1000, 0.8, 0.5);
        AssertClose(40, e, "Ortalama aydınlık (lüks)");

        // N = ceil(40·1000 / (10000·0.8·0.5)) = ceil(10) = 10.
        int n = AydinlatmaHesapServisi.GerekliArmaturSayisi(40, 1000, 10000, 0.8, 0.5);
        if (n != 10)
            throw new InvalidOperationException($"Gerekli armatür sayısı: beklenen 10, bulunan {n}.");
        // 45 lüks → 11.25 → yukarı yuvarla → 12.
        int n2 = AydinlatmaHesapServisi.GerekliArmaturSayisi(45, 1000, 10000, 0.8, 0.5);
        if (n2 != 12)
            throw new InvalidOperationException($"Gerekli armatür sayısı (yuvarlama): beklenen 12, bulunan {n2}.");

        // S = Φ·MF·UF / (E·W) = 10000·0.8·0.5 / (20·8) = 25 m.
        double s = AydinlatmaHesapServisi.OnerilenDirekAralik(20, 8, 10000, 0.8, 0.5);
        AssertClose(25, s, "Önerilen direk aralığı (m)");

        AssertThrows(() => AydinlatmaHesapServisi.OrtalamaAydinlik(10, 10000, 0), "Sıfır alan hata vermeli.");
    }

    // Faz 6 — devre kablo hesabı; M11 KabloHesapServisi'ne dağıtık-yük eşdeğer uzunlukla bağlanır.
    public static void DevreKabloHesabiM11ileTutarli()
    {
        // Tek direk 1000 W, 100 m, monofaze → M11 doğrudan çağrısıyla birebir aynı olmalı.
        var tek = KabloGuzergahServisi.DevreyiHesapla(
            new List<double> { 1000 }, new List<double> { 100 },
            FazTipi.Monofaze, izinVerilenDusumYuzde: 5.0, cosF: 0.9);

        double akim = 1000.0 / (230.0 * 0.9);
        var m11 = KabloHesapServisi.KesitHesapla(akim, 100, FazTipi.Monofaze, 230, 5.0, 0.9);

        AssertClose(100, tek.EsdegerUzunlukM, "Tek direk eşdeğer uzunluk");
        AssertClose(m11.KesitMm2, tek.KesitMm2, "Kesit M11 ile aynı");
        AssertClose(m11.GercekDusumYuzde, tek.GerilimDususYuzde, "ΔU% M11 ile aynı");

        // İki eşit yük 100 m ve 200 m'de → yük merkezi (eşdeğer uzunluk) 150 m.
        var iki = KabloGuzergahServisi.DevreyiHesapla(
            new List<double> { 100, 100 }, new List<double> { 100, 100 },
            FazTipi.Monofaze, izinVerilenDusumYuzde: 5.0, cosF: 0.9);
        AssertClose(150, iki.EsdegerUzunlukM, "İki direk eşdeğer uzunluk (yük merkezi)");
        AssertClose(200, iki.ToplamUzunlukM, "Toplam güzergâh uzunluğu");

        AssertThrows(() => KabloGuzergahServisi.DevreyiHesapla(
            new List<double> { 100 }, new List<double> { 50, 60 }), "Uyumsuz liste uzunluğu hata vermeli.");
    }

    // Faz 7 — TT topraklama: şerit/kazık yayılma direnci + RCD dokunma gerilimi kriteri.
    public static void TopraklamaTtHesaplar()
    {
        // Güzergâh uzunluğu: (0,0)-(0,100)-(50,100) → 100 + 50 = 150 m.
        double boy = TopraklamaServisi.GuzergahUzunlugu(
            new List<Point2D> { new(0, 0), new(0, 100), new(50, 100) });
        AssertClose(150, boy, "Şerit güzergâh uzunluğu");

        // Tek kazık: ρ=100, L=2.4, d=0.016 → ~35.8 Ω.
        double rKazik = TopraklamaServisi.KazikDirenci(100, 2.4, 0.016);
        AssertBetween(rKazik, 35.0, 36.5, "Tek kazık direnci");

        // Şerit: ρ=100, L=100, w=0.03, h=0.7 → ~4.38 Ω.
        double rSerit = TopraklamaServisi.SeritDirenci(100, 100, 0.03, 0.7);
        AssertBetween(rSerit, 4.2, 4.6, "Şerit direnci");

        // Şerit + 4 kazık paralel → şeritten küçük olmalı.
        double rToplam = TopraklamaServisi.ToplamDirenc(rSerit, rKazik, 4);
        if (rToplam >= rSerit)
            throw new InvalidOperationException("Paralel toplam direnç şerit direncinden küçük olmalı.");

        // TT kriteri: I_Δn=0.3 A → R_A ≤ 50/0.3 = 166.7 Ω.
        AssertBetween(TopraklamaServisi.IzinVerilenMaxDirenc(0.3), 166.6, 166.7, "İzin verilen max direnç");
        if (!TopraklamaServisi.RcdUygunMu(rToplam, 0.3))
            throw new InvalidOperationException("Düşük dirençte RCD kriteri sağlanmalı.");
        if (TopraklamaServisi.RcdUygunMu(200, 0.3))
            throw new InvalidOperationException("200 Ω, 0.3 A RCD için kriteri sağlamamalı.");
    }

    // Faz 8 — M11 tek hat aktarımı: Aydınlatma Panosu + her güzergâh bir linye.
    public static void TekHatAktarimDevreleriUretir()
    {
        var proje = OrnekProje();
        var sonuc = CevreAydinlatmaTekHatAktarimServisi.Donustur(proje);

        if (!sonuc.Basarili || sonuc.Proje is null)
            throw new InvalidOperationException($"Aktarım başarısız: {sonuc.Hata}");
        if (sonuc.Proje.KokPano is null ||
            sonuc.Proje.KokPano.PanoTipi != Kst.Acoustics.Core.Electric.Models.PanoTipi.AydinlatmaPanosu)
            throw new InvalidOperationException("Kök pano Aydınlatma Panosu olmalı.");

        var pano = sonuc.Proje.KokPano;
        if (pano.Linyeler.Count != 2)
            throw new InvalidOperationException($"Boş güzergâh atlanıp 2 linye olmalı; bulunan {pano.Linyeler.Count}.");

        var a1 = pano.Linyeler[0];
        if (a1.SortiSayisi != 3)
            throw new InvalidOperationException($"A1 sorti: beklenen 3, bulunan {a1.SortiSayisi}.");
        AssertClose(300, a1.ToplamGucW, "A1 toplam güç");
        AssertClose(100, a1.KabloMesafeM, "A1 kablo boyu (gerçek güzergâh)");

        var a2 = pano.Linyeler[1];
        AssertClose(200, a2.ToplamGucW, "A2 toplam güç");
        AssertClose(50, a2.KabloMesafeM, "A2 kablo boyu");

        AssertClose(500, pano.ToplamGucW, "Pano toplam güç");
        if (sonuc.Uyarilar.Count == 0)
            throw new InvalidOperationException("Boş güzergâh için uyarı beklenir.");
    }

    // Faz 8 — .e4p11 serileştirme: M11 JSON ayarlarıyla round-trip panoları/linyeleri korur.
    public static void TekHatAktarimE4p11RoundTrip()
    {
        var sonuc = CevreAydinlatmaTekHatAktarimServisi.Donustur(OrnekProje());
        var opts = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };

        string json = JsonSerializer.Serialize(sonuc.Proje, opts);
        var geri = JsonSerializer.Deserialize<Kst.Acoustics.Core.Electric.Models.ElektrikProje>(json, opts)
            ?? throw new InvalidOperationException("Deserialize null döndü.");

        if (geri.TumPanolar.Count != 1 || geri.TumPanolar[0].Linyeler.Count != 2)
            throw new InvalidOperationException(".e4p11 round-trip pano/linye sayısını korumadı.");
        AssertClose(300, geri.TumPanolar[0].Linyeler[0].ToplamGucW, "Round-trip A1 güç");
    }

    // UI (App) — ViewModel iş akışı: yüzey → otomatik direk → tek hat aktarımı (GUI'siz testable).
    public static void ViewModelAkisiCalisir()
    {
        var vm = new CevreAydinlatmaViewModel();

        var doc = new DxfDocument();
        doc.Entities.Add(new Polyline2D(new[]
        {
            new Polyline2DVertex(0, 0), new Polyline2DVertex(10, 0),
            new Polyline2DVertex(10, 10), new Polyline2DVertex(0, 10)
        }, true) { Layer = new Layer("YESIL") });
        vm.YuzeyleriAyarla(CevreYuzeyImportServisi.KapaliPoligonlardanYuzeyler(doc, 1.0));
        if (vm.Yuzeyler.Count != 1 || vm.Proje.Yuzeyler.Count != 1)
            throw new InvalidOperationException("VM yüzeyleri yüklemedi.");

        vm.Proje.ArmaturKutuphanesi.Add(new CevreArmaturTanim { Id = 1, GucW = 100 });
        vm.Proje.Guzergahlar.Add(new AydinlatmaGuzergahi
        {
            Id = 1, Ad = "Yol", ArmaturTanimId = 1, DirekAralikM = 5,
            Rota = new List<Point2D> { new(0, 0), new(10, 0) }
        });

        int n = vm.TumDirekleriYerlestir();
        if (n != 3 || vm.Direkler.Count != 3)
            throw new InvalidOperationException($"VM direk yerleşimi: beklenen 3, bulunan {n}.");

        var sonuc = vm.TekHataAktarimiOlustur();
        if (!sonuc.Basarili || sonuc.Proje?.KokPano is null || sonuc.Proje.KokPano.Linyeler.Count != 1)
            throw new InvalidOperationException("VM tek hat aktarımı başarısız.");
    }

    // UI (App) — seçili yüzeyden çevre güzergâhı + otomatik direk (serbest çizim gerektirmez).
    public static void YuzeydenGuzergahVeDirek()
    {
        var vm = new CevreAydinlatmaViewModel();
        vm.Proje.ArmaturKutuphanesi.Add(new CevreArmaturTanim { Id = 1, GucW = 100 });

        var kare = new CevreYuzey
        {
            Id = 1, Ad = "Yeşil", Kategori = YuzeyKategori.YesilAlan,
            Poligon = new List<Point2D> { new(0, 0), new(10, 0), new(10, 10), new(0, 10) }
        };
        vm.YuzeyleriAyarla(new List<CevreYuzey> { kare });
        vm.SeciliYuzey = kare;

        var g = vm.SecilenYuzeydenGuzergahEkle(aralikM: 10, kenarOfsetM: 0, direkTanimId: null, armaturTanimId: 1);
        if (g is null || !g.Kapali || g.Rota.Count != 4 || vm.Proje.Guzergahlar.Count != 1)
            throw new InvalidOperationException("Yüzeyden kapalı güzergâh eklenmedi.");

        int n = vm.TumDirekleriYerlestir();
        if (n != 4 || vm.Direkler.Count != 4)
            throw new InvalidOperationException($"Çevre direkleri: beklenen 4, bulunan {n}.");

        // Yüzey seçili değilken güzergâh eklenmemeli.
        vm.SeciliYuzey = null;
        if (vm.SecilenYuzeydenGuzergahEkle(10, 0, null, 1) is not null)
            throw new InvalidOperationException("Yüzey seçili değilken güzergâh eklenmemeli.");
    }

    // UI/Persistence — .e4p15 JSON round-trip yüzey/kütüphane/güzergâh/direk/topraklama korur.
    public static void ProjeE4p15RoundTrip()
    {
        var proje = OrnekProje();
        proje.Yuzeyler.Add(new CevreYuzey
        {
            Id = 1, Ad = "Yeşil", Kategori = YuzeyKategori.YesilAlan, KaynakKatman = "YESIL",
            AlanM2 = 250, Poligon = new List<Point2D> { new(0, 0), new(10, 0), new(10, 25), new(0, 25) }
        });
        proje.Direkler.Add(new AydinlatmaDiregi { Id = 1, X = 5, Y = 5, GuzergahId = 1, ArmaturTanimId = 1 });
        proje.Topraklama.Kaziklar.Add(new TopraklamaKazigi { X = 1, Y = 2, BoyM = 2.4 });
        proje.Topraklama.SeritGuzergahi.Add(new Point2D(0, 0));
        proje.Topraklama.SeritGuzergahi.Add(new Point2D(10, 0));

        var geri = CevreAydinlatmaJsonServisi.Deserialize(CevreAydinlatmaJsonServisi.Serialize(proje));

        if (geri.Yuzeyler.Count != 1 || geri.Yuzeyler[0].Kategori != YuzeyKategori.YesilAlan)
            throw new InvalidOperationException("Yüzey/kategori korunmadı.");
        if (geri.Yuzeyler[0].Poligon.Count != 4)
            throw new InvalidOperationException("Poligon (Point2D) korunmadı.");
        AssertClose(10, geri.Yuzeyler[0].Poligon[1].X, "Poligon X round-trip");
        // OrnekProje 5 direk ekler; test 1 direk daha ekledi → 6.
        if (geri.ArmaturKutuphanesi.Count != 1 || geri.Guzergahlar.Count != 3 || geri.Direkler.Count != 6)
            throw new InvalidOperationException("Kütüphane/güzergâh/direk korunmadı.");
        if (geri.Topraklama.Sistem != TopraklamaSistemi.TT || geri.Topraklama.Kaziklar.Count != 1
            || geri.Topraklama.SeritGuzergahi.Count != 2)
            throw new InvalidOperationException("Topraklama korunmadı.");
    }

    private static CevreAydinlatmaProje OrnekProje()
    {
        var proje = new CevreAydinlatmaProje { Ad = "Park" };
        proje.ArmaturKutuphanesi.Add(new CevreArmaturTanim { Id = 1, Ad = "LED 100W", GucW = 100 });

        proje.Guzergahlar.Add(new AydinlatmaGuzergahi
        {
            Id = 1, Ad = "Kuzey Yol", ArmaturTanimId = 1,
            Rota = new List<Point2D> { new(0, 0), new(100, 0) }
        });
        proje.Guzergahlar.Add(new AydinlatmaGuzergahi
        {
            Id = 2, Ad = "Güney Yol", ArmaturTanimId = 1,
            Rota = new List<Point2D> { new(0, 0), new(50, 0) }
        });
        proje.Guzergahlar.Add(new AydinlatmaGuzergahi { Id = 3, Ad = "Boş Güzergâh", ArmaturTanimId = 1 });

        for (int i = 0; i < 3; i++)
            proje.Direkler.Add(new AydinlatmaDiregi { Id = i + 1, GuzergahId = 1, ArmaturTanimId = 1 });
        for (int i = 0; i < 2; i++)
            proje.Direkler.Add(new AydinlatmaDiregi { Id = 10 + i, GuzergahId = 2, ArmaturTanimId = 1 });

        return proje;
    }

    // Faz 2 — DXF arka plan çizgileri: LINE entity'leri çıkarılır + kısa segmentler sadeleştirilir.
    public static void ArkaPlanCizgileriCikarilir()
    {
        var doc = new DxfDocument();
        doc.Entities.Add(new Line(new Vector2(0, 0), new Vector2(5, 0)));      // 5 m → kalır
        doc.Entities.Add(new Line(new Vector2(0, 0), new Vector2(20, 0)));     // 20 m → kalır
        doc.Entities.Add(new Line(new Vector2(0, 0), new Vector2(0, 0.02)));   // 2 cm → elenir

        var cizgiler = CevreYuzeyImportServisi.ArkaPlanCizgileri(doc, 1.0);
        if (cizgiler.Count != 2)
            throw new InvalidOperationException($"Arka plan: 2 cm elenip 2 çizgi kalmalı; bulunan {cizgiler.Count}.");
    }

    private static void AssertBetween(double deger, double min, double max, string etiket)
    {
        if (deger < min || deger > max)
            throw new InvalidOperationException($"{etiket}: [{min}, {max}] beklenirken {deger} bulundu.");
    }

    private static void AssertKategori(string katman, YuzeyKategori beklenen)
    {
        var bulunan = YuzeyKategoriEsleyici.Esle(katman);
        if (bulunan != beklenen)
            throw new InvalidOperationException($"Katman \"{katman}\": beklenen {beklenen}, bulunan {bulunan}.");
    }

    private static void AssertEqual(string beklenen, string bulunan, string etiket)
    {
        if (!string.Equals(beklenen, bulunan, StringComparison.Ordinal))
            throw new InvalidOperationException($"{etiket}: beklenen \"{beklenen}\", bulunan \"{bulunan}\".");
    }

    private static void AssertClose(double beklenen, double bulunan, string etiket)
    {
        if (Math.Abs(beklenen - bulunan) > 1e-6)
            throw new InvalidOperationException($"{etiket}: beklenen {beklenen}, bulunan {bulunan}.");
    }

    private static void AssertThrows(Action eylem, string mesaj)
    {
        try
        {
            eylem();
        }
        catch
        {
            return;
        }
        throw new InvalidOperationException(mesaj);
    }
}
