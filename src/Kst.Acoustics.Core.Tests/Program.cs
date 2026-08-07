using Kst.Acoustics.Core.Architecture.Models;
using Kst.Acoustics.Core.Architecture.Circulation;
using Kst.Acoustics.Core.Architecture.Coordination;
using Kst.Acoustics.Core.Architecture.Exporting;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Bina.Services;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;
using Kst.Acoustics.Core.Architecture.Layout;
using Kst.Acoustics.Core.Architecture.Materials;
using Kst.Acoustics.Core.Architecture.Model3D;
using Kst.Acoustics.Core.Architecture.Reports;
using Kst.Acoustics.Core.Architecture.Rules;
using Kst.Acoustics.Core.Architecture.Services;
using Kst.Acoustics.Core.Architecture.Templates;
using Kst.Acoustics.Core.Terrain.Models;
using Kst.Acoustics.Core.Terrain.Services;
using Kst.Acoustics.Core.Landscape.Models;
using Kst.Acoustics.Core.Landscape.Services;
using Kst.Acoustics.Core.Common.Geometry;
using netDxf;
using netDxf.Entities;
using netDxf.Units;
using netDxf.Tables;

var tests = new (string Name, Action Test)[]
{
    ("M30: .e4mimari.json kaydet-aç round-trip", E4MimariRoundTrip),
    ("M30: boş mimari proje kritik sonuç üretir", BosProjeKritikSonucUretir),
    ("M30: eksik bina işlevi ve boş mahal listesi yakalanır", EksikBinaIsleviVeBosMahalListesiYakalanir),
    ("M30: negatif alan ve geçersiz adet yakalanır", NegatifAlanVeGecersizAdetYakalanir),
    ("M30: geçerli minimal proje kritik üretmez", GecerliMinimalProjeKritikUretmez),
    ("M31: bina işlevi tanımları benzersizdir", BinaIsleviTanimlariBenzersizdir),
    ("M31: bina işlevi tanımları enum değerlerini kapsar", BinaIsleviTanimlariEnumDegerleriniKapsar),
    ("M31: bina işlevi oranları ve yükseklikleri geçerlidir", BinaIsleviTanimlariGecerlidir),
    ("M31: mahal şablon kodları benzersizdir", MahalSablonKodlariBenzersizdir),
    ("M31: mahal şablonları zorunlu işlev ailelerini kapsar", MahalSablonlariZorunluIslevAileleriniKapsar),
    ("M31: mahal şablon değerleri geçerlidir", MahalSablonDegerleriGecerlidir),
    ("M31: ofis ihtiyaç programı üretir", OfisIhtiyacProgramiUretir),
    ("M31: konferans ihtiyaç programı kapasiteyle ölçeklenir", KonferansIhtiyacProgramiKapasiteyleOlceklenir),
    ("M31: alan özeti toplamları hesaplar", AlanOzetiToplamlariHesaplar),
    ("M31: alan özeti üretim programıyla tutarlıdır", AlanOzetiUretimProgramiylaTutarlidir),
    ("M31: ihtiyaç programı HTML ve CSV raporu üretir", IhtiyacProgramiHtmlVeCsvRaporuUretir),
    ("M34: kural modelleri güvenli varsayılanlarla oluşur", KuralModelleriGuvenliVarsayilanlarlaOlusur),
    ("M34: kural çalıştırıcı eksik veriyi güvenli yakalar", KuralCalistiriciEksikVeriyiGuvenliYakalar),
    ("M34: ilk veri tabanlı kontroller uyarı ve kritik üretir", IlkVeriTabanliKontrollerUyariVeKritikUretir),
    ("M34: plan taslağı yerleşim uyarıları üretir", PlanTaslagiYerlesimUyarilariUretir),
    ("M34: kaynak profilleri resmi iz bilgisi taşır", KaynakProfilleriResmiIzBilgisiTasir),
    ("M34: ilk kontrol sonuçları kaynak profiline bağlanır", IlkKontrolSonuclariKaynakProfilineBaglanir),
    ("M32: plan blok modelleri güvenli varsayılanlarla oluşur", PlanBlokModelleriGuvenliVarsayilanlarlaOlusur),
    ("M32: ilişki matrisi ofis ve konut ilişkileri üretir", IliskiMatrisiOfisVeKonutIliskileriUretir),
    ("M32: ilk yerleşim algoritması blokları katlara çakışmadan dağıtır", IlkYerlesimAlgoritmasiBloklariKatlaraCakismadanDagitir),
    ("M32: koridor modeli ana aks ve bağlantısız mahal tespiti üretir", KoridorModeliAnaAksVeBaglantisizMahalTespitiUretir),
    ("M32: plan taslağı e4mimari dosyasında korunur", PlanTaslagiE4MimariDosyasindaKorunur),
    ("M33: çekirdek ve şaft modelleri güvenli varsayılanlarla oluşur", CekirdekVeSaftModelleriGuvenliVarsayilanlarlaOlusur),
    ("M33: çekirdek ve şaft e4mimari dosyasında korunur", CekirdekVeSaftE4MimariDosyasindaKorunur),
    ("M33: katlar arası süreklilik uyarıları üretir", KatlarArasiSureklilikUyarilariUretir),
    ("M34: çekirdek süreklilik uyarılarını plan kontrolüne taşır", CekirdekSureklilikUyarilariniPlanKontroluneTasir),
    ("M33: ıslak hacim şaft ilişkisi uyarıları üretir", IslakHacimSaftIliskisiUyarilariUretir),
    ("M34: ıslak hacim şaft uyarılarını plan kontrolüne taşır", IslakHacimSaftUyarilariniPlanKontroluneTasir),
    ("M33: kaçış grafı mahal-koridor-merdiven bağlantısı üretir", KacisGrafiMahalKoridorMerdivenBaglantisiUretir),
    ("M34: kaçış grafı merdivene ulaşamayan mahal uyarısı üretir", KacisGrafiMerdiveneUlasamayanMahalUyarisiUretir),
    ("M35: 3D geometri modelleri güvenli varsayılanlarla oluşur", Model3DGeometriModelleriGuvenliVarsayilanlarlaOlusur),
    ("M35: 3D model e4mimari dosyasında korunur", Model3DE4MimariDosyasindaKorunur),
    ("M35: 2D plan taslağından 3D model üretir", Model3DUretimServisiPlanTaslagindan3DModelUretir),
    ("M35: 3D ihracat sözleşmesi glb varsayılanıyla oluşur", Model3DIhracatSozlesmesiGlbVarsayilaniylaOlusur),
    ("M35: 3D geometri kapanış regresyonu alan ve hacimleri korur", Model3DGeometriKapanisRegresyonuAlanVeHacimleriKorur),
    ("M36: malzeme modelleri güvenli varsayılanlarla oluşur", MalzemeModelleriGuvenliVarsayilanlarlaOlusur),
    ("M36: varsayılan mimari malzeme kataloğu geçerlidir", VarsayilanMimariMalzemeKataloguGecerlidir),
    ("M36: malzemeler e4mimari dosyasında korunur", MalzemelerE4MimariDosyasindaKorunur),
    ("M36: yapı elemanı tipleri katman ve performans özeti üretir", YapiElemaniTipleriKatmanVePerformansOzetiUretir),
    ("M36: varsayılan yapı elemanı tipleri M35 sözleşmesiyle uyumludur", VarsayilanYapiElemaniTipleriM35SozlesmesiyleUyumludur),
    ("M36: yapı elemanı tipleri ve atamaları e4mimari dosyasında korunur", YapiElemaniTipleriVeAtamalariE4MimariDosyasindaKorunur),
    ("M36: mahal finisaj önerisi mahal tipine göre üretir", MahalFinisajOnerisiMahalTipineGoreUretir),
    ("M36: finisaj ön kontrolü uygunsuz kararları yakalar", FinisajOnKontroluUygunsuzKararlariYakalar),
    ("M36: finisaj profilleri e4mimari dosyasında korunur", FinisajProfilleriE4MimariDosyasindaKorunur),
    ("M36: disiplin veri özeti güvenli ve tutarlı üretir", DisiplinVeriOzetiGuvenliVeTutarliUretir),
    ("M36: kapanış regresyonu atama ve disiplin özetini korur", M36KapanisRegresyonuAtamaVeDisiplinOzetiniKorur),
    ("M37: mahal listesi tablosu 3D model ve programdan üretir", M37MahalListesiTablosuUretir),
    ("M37: alan tablosu alan özetiyle tutarlı üretir", M37AlanTablosuTutarliUretir),
    ("M37: kapı-pencere çizelgesi açıklıkları gruplar", M37KapiPencereCizelgesiUretir),
    ("M37: finisaj çizelgesi mahal bazında üretir", M37FinisajCizelgesiUretir),
    ("M37: ön kontrol raporu uyarıları kaynak koduyla üretir", M37OnKontrolRaporuUretir),
    ("M37: HTML ve CSV çıktı formatları tüm tabloları üretir", M37CiktiFormatlariUretir),
    ("M37: XLSX çıktısı geçerli çok sayfalı paket üretir", M37XlsxCiktisiUretir),
    ("M30 Faz9: bina geometri adaptörü mimari modeli M50'ye çevirir", Faz9BinaGeometriAdaptoruDonusturur),
    ("M30 Faz9: .e4bina dışa aktarımı kaydeder, bağlar ve üzerine yazmayı korur", Faz9BinaGeometriDisaAktarimi),
    ("M30 Faz9: oda/kat Id'leri yeniden üretim ve round-trip'te kararlı", Faz9IdKararliligi),
    ("M30 Faz9: üretilen .e4bina M12 ve M08 köprülerince okunur", Faz9KopruDogrulamasi),
    ("M39: revizyon farkı eklenen/silinen/değişen mahalleri raporlar", Faz10RevizyonFarki),
    ("M39: OBJ ihracatı mahal ve duvarları kutu mesh olarak yazar", Faz10ObjIhracati),
    ("M39: mutabakat askıda atama ve kırık tip referansını yakalar", Faz10Mutabakat),
    ("M60 F1: DXF spot kot (POINT) arazi noktasına çevrilir", M60SpotKotOkunur),
    ("M60 F1: 3D polyline kontur köşe Z'sinden kot alır", M60Kontur3DVertexZ),
    ("M60 F1: Polyline2D kontur düzlem kotundan (Elevation) kot alır", M60Kontur2DElevation),
    ("M60 F1: kotsuz kontur katman adından kot çözer", M60KonturKatmanAdindanKot),
    ("M60 F1: kotsuz kontur en yakın TEXT etiketinden kot çözer", M60KonturMetinEtiketinden),
    ("M60 F1: mm DXF metreye ölçeklenir", M60BirimOlcekleme),
    ("M60 F2: TIN sabit düzlemde kotu korur", M60YuzeyDuzlemSabit),
    ("M60 F2: TIN eğik düzlemi tam interpolasyonla verir", M60YuzeyEgikDuzlem),
    ("M60 F2: yüzey dışı sorgu null döner", M60YuzeyDisNokta),
    ("M60 F2: konturlar arası kot interpolasyonu", M60YuzeyKonturInterpolasyon),
    ("M60 F2: 3'ten az nokta boş yüzey üretir", M60YuzeyYetersizNokta),
    ("M60 F5: düz zemin + alçak platform → saf yarma", M60HafriyatSafYarma),
    ("M60 F5: düz zemin + yüksek platform → saf dolgu", M60HafriyatSafDolgu),
    ("M60 F5: eğik zemin + orta platform → dengeli yarma/dolgu", M60HafriyatDengeli),
    ("M60 F5: tasarım mevcut yüzey dışındaysa kapsam-dışı sayılır", M60HafriyatKapsamDisi),
    ("M60 F5: tasarım bölgesi yoksa sıfır hacim", M60HafriyatTasarimYok),
    ("M60 F7: .e4arazi.json round-trip (nokta/kontur/tasarım korunur)", M60AraziRoundTrip),
    ("M60 F7: her kayıtta GeometriRevizyon artar", M60AraziRevizyonArtar),
    ("M60 F7: olmayan dosya FileNotFoundException", M60AraziDosyaYok),
    ("M60 F7: yükleme sonrası TIN+hafriyat yeniden hesaplanır", M60AraziYuklemeSonrasiHesap),
    ("M60 F6: iki platform bölge bazlı yarma/dolgu dökümü üretir", M60BolgeDokumu),
    ("M60 F6: kabarma/sıkışma katsayıları ve bank denge", M60KabarmaSikismaDenge),
    ("M60 F6: katsayısız raporda gevşek=yarma, denge=net", M60RaporKatsayisiz),
    ("M60 F6: özet metni başlık ve bölge adı içerir", M60RaporOzetMetni),
    ("M60 F3: TIN üçgenleri ProjectSurface'e çevrilir", M60Yuzey3DGorunum),
    ("M60 F8: tüketici .e4arazi'yi salt-okunur yükler ve kot sorgular", M60AraziTuketim),
    ("M60 F6: yarma/dolgu renk haritası diff'i [0..1] yoğunluğa eşler", M60RenkHaritasi),
    ("M61 F2: düz zeminde ±0.00, bodrum, kazı derinliği doğru", M61DuzZeminOturtma),
    ("M61 F2: ±0.00 override serbest yükseklik ve kazıyı değiştirir", M61SifirKotOverride),
    ("M61 F2: eğik arazide iki blok kademe sırası alır", M61KademeSirasi),
    ("M61 F2: arazi dışı ayak izi uyarı verir, oturtmaz", M61AyakIziDisinda),
    ("M61 F2: ±0.00 zemin altıysa gömülü uyarısı", M61GomuluUyari),
    ("M61 F1: .e4bina adaptörü ayak izi + kat sayılarını türetir", M61BinaAdaptoru),
    ("M61 F1: çok odalı kat ayak izi sınır kutusu olur", M61AdaptorCokOda),
    ("M61 F5: kesit arazi profili + blok dikdörtgenleri üretir", M61Kesit),
    ("M61 F1: mimari M35 Bim3DModel ayak izine çevrilir", M61MimariAdaptoru),
    ("M70 F1: park sınırı arazi TIN düğümlerini konveks kabukla sarar", M70ParkSiniriAraziden),
    ("M70 F1: araziden zemin kotu sorgulanır", M70AraziKotSorgu),
    ("M70 F1: .e4bina ayak izi + setback BinaDislama'ya çevrilir", M70BinaDislamaYukle),
    ("M70 F1: arazi/bina revizyon damgası ve eskime durumu", M70AltlikDurumu),
    ("M70 F2: setback offset (Tampon) kareyi dışa şişirir", M70TamponOffset),
    ("M70 F2: difference park içinde delik açar", M70FarkDelik),
    ("M70 F2: union örtüşen tamponları birleştirir", M70BirlestirUnion),
    ("M70 F2: kullanılabilir alan = park − (bina + setback)", M70KullanilabilirAlan),
    ("M70 F3: öğe ekle kararlı Id verir, sil çalışır", M70OgeEkleSil),
    ("M70 F3: araziden öğe zemin kotu atanır", M70OgeZeminKotu),
    ("M70 F3: kullanılabilir alan içi/dışı/bina üstü doğrulanır", M70YerlesimDogrula),
    ("M70 F3: öğe taşıma geometriyi öteler", M70OgeTasi),
    ("M70 F4: alan/oran özeti (sert/yeşil) doğru", M70AlanOran),
    ("M70 F4: binalı parkta oranlar kullanılabilir alandan", M70AlanOranBinali),
    ("M70 F4: dar yürüyüş yolu genişlik uyarısı verir", M70YolGenislikUyari),
    ("M70 F4: dik yürüyüş yolu eğim uyarısı verir", M70YolEgimUyari),
    ("M70 F5: .e4p70 round-trip (öğe tipleri + değerler korunur)", M70ParkRoundTrip),
    ("M70 F5: her kayıtta GeometriRevizyon artar", M70ParkRevizyonArtar),
    ("M70 F5: olmayan dosya FileNotFoundException", M70ParkDosyaYok),
    ("M70 F5: .e4park artifact yazımı revizyon artırmaz", M70ParkArtifact),
};

var failed = 0;
foreach (var (name, test) in tests)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL {name}");
        Console.WriteLine(ex);
    }
}

if (failed > 0)
{
    throw new InvalidOperationException($"{failed} Core testi başarısız oldu.");
}

Console.WriteLine($"Core testleri tamamlandı: {tests.Length} geçti.");

static void E4MimariRoundTrip()
{
    var yol = Path.Combine(Path.GetTempPath(), $"e4mimari-test-{Guid.NewGuid():N}.e4mimari.json");
    try
    {
        var proje = MimariProjeServisi.YeniOlustur("Mimari Test");
        proje.IhtiyacProgrami.BinaIslevi = BinaIslevi.KonferansVeToplanti;
        proje.IhtiyacProgrami.ProjeTipi = "Konferans merkezi";
        proje.IhtiyacProgrami.KullaniciKapasitesi = 250;
        proje.IhtiyacProgrami.KatSayisi = 2;
        proje.IhtiyacProgrami.SirkulasyonOrani = 0.22;
        proje.IhtiyacProgrami.TeknikAlanOrani = 0.08;
        proje.IhtiyacProgrami.Mahaller.Add(new MahalGereksinimi
        {
            Id = "mah-01",
            MahalTipi = MahalTipi.KonferansSalonu,
            KullanimSinifi = KullanimSinifi.AnaKullanim,
            Ad = "Ana Konferans Salonu",
            Adet = 1,
            KisiKapasitesi = 250,
            MinimumAlanM2 = 250,
            IdealAlanM2 = 320,
            HedefAlanM2 = 340,
            MinimumNetYukseklikM = 4.5,
            HedefNetYukseklikM = 5.0
        });
        proje.MahalSablonlari.Add(new MahalSablonu
        {
            Kod = "KONF-ANA",
            VarsayilanAd = "Konferans Salonu",
            MahalTipi = MahalTipi.KonferansSalonu,
            KullanimSinifi = KullanimSinifi.AnaKullanim,
            UygunBinaIslevleri = [BinaIslevi.Kulturel, BinaIslevi.KonferansVeToplanti],
            VarsayilanAdet = 1,
            MinimumAlanM2 = 200,
            IdealAlanM2 = 320,
            MinimumNetYukseklikM = 4.5,
            KisiBasinaAlanM2 = 1.2
        });

        MimariProjeServisi.Kaydet(proje, yol);
        Assert(File.Exists(yol), "Mimari proje dosyası oluşturulmalı.");

        var acilan = MimariProjeServisi.Ac(yol);
        Assert(acilan.ProjeAdi == "Mimari Test", "Proje adı korunmalı.");
        Assert(acilan.DosyaYolu == yol, "DosyaYolu yükleme sonrası set edilmeli.");
        Assert(acilan.SemaSurum == MimariProjeServisi.GuncelSemaSurum, "SemaSurum güncel olmalı.");
        Assert(acilan.IhtiyacProgrami.BinaIslevi == BinaIslevi.KonferansVeToplanti, "Bina işlevi korunmalı.");
        Assert(acilan.IhtiyacProgrami.Mahaller.Count == 1, "Mahal gereksinimi korunmalı.");
        Assert(acilan.MahalSablonlari.Count == 1, "Mahal şablonu korunmalı.");
        Assert(Math.Abs(acilan.IhtiyacProgrami.Mahaller[0].ToplamHedefHacimM3 - 1700.0) < 1e-9, "Mahal hacmi doğru hesaplanmalı.");
        Assert(acilan.MahalSablonlari[0].UygunBinaIslevleri.Contains(BinaIslevi.Kulturel), "Şablon işlev listesi korunmalı.");
    }
    finally
    {
        if (File.Exists(yol))
            File.Delete(yol);
    }
}

static void BosProjeKritikSonucUretir()
{
    var sonuclar = MimariProjeDogrulamaServisi.Dogrula(null);
    Assert(sonuclar.Any(s => s.Kod == "MIMARI_PROJE_BOS" && s.Seviye == KontrolSeviyesi.Kritik),
        "Boş mimari proje kritik sonuç üretmeli.");
}

static void EksikBinaIsleviVeBosMahalListesiYakalanir()
{
    var proje = YeniDogrulamaProjesi();
    var sonuclar = MimariProjeDogrulamaServisi.Dogrula(proje);
    Assert(sonuclar.Any(s => s.Kod == "BINA_ISLEVI_EKSIK" && s.Seviye == KontrolSeviyesi.Kritik),
        "Eksik bina işlevi kritik sonuç üretmeli.");
    Assert(sonuclar.Any(s => s.Kod == "MAHAL_LISTESI_BOS" && s.Seviye == KontrolSeviyesi.Uyari),
        "Boş mahal listesi uyarı üretmeli.");
}

static void NegatifAlanVeGecersizAdetYakalanir()
{
    var proje = YeniDogrulamaProjesi();
    proje.IhtiyacProgrami.BinaIslevi = BinaIslevi.Ofis;
    proje.IhtiyacProgrami.Mahaller.Add(new MahalGereksinimi
    {
        Id = "mah-01",
        Ad = "Toplantı Odası",
        MahalTipi = MahalTipi.ToplantiOdasi,
        KullanimSinifi = KullanimSinifi.AnaKullanim,
        Adet = 0,
        MinimumAlanM2 = 12,
        IdealAlanM2 = 18,
        HedefAlanM2 = -5,
        HedefNetYukseklikM = 3.0
    });

    var sonuclar = MimariProjeDogrulamaServisi.Dogrula(proje);
    Assert(sonuclar.Any(s => s.Kod == "MAHAL_ADEDI_GECERSIZ" && s.Seviye == KontrolSeviyesi.Kritik),
        "Geçersiz mahal adedi kritik sonuç üretmeli.");
    Assert(sonuclar.Any(s => s.Kod == "MAHAL_ALANI_NEGATIF" && s.Seviye == KontrolSeviyesi.Kritik),
        "Negatif mahal alanı kritik sonuç üretmeli.");
}

static void GecerliMinimalProjeKritikUretmez()
{
    var proje = YeniDogrulamaProjesi();
    proje.IhtiyacProgrami.BinaIslevi = BinaIslevi.Ofis;
    proje.IhtiyacProgrami.Mahaller.Add(new MahalGereksinimi
    {
        Id = "mah-01",
        Ad = "Açık Ofis",
        MahalTipi = MahalTipi.AcikOfis,
        KullanimSinifi = KullanimSinifi.AnaKullanim,
        Adet = 1,
        MinimumAlanM2 = 50,
        IdealAlanM2 = 75,
        HedefAlanM2 = 80,
        MinimumNetYukseklikM = 2.6,
        HedefNetYukseklikM = 3.0
    });

    var sonuclar = MimariProjeDogrulamaServisi.Dogrula(proje);
    Assert(!sonuclar.Any(s => s.Seviye == KontrolSeviyesi.Kritik),
        $"Geçerli minimal proje kritik sonuç üretmemeli: {string.Join(", ", sonuclar.Select(s => s.Kod))}");
}

static void BinaIsleviTanimlariBenzersizdir()
{
    var tanimlar = BinaIsleviTanimlari.Tum;
    Assert(tanimlar.Count > 0, "Bina işlevi tanım kataloğu boş olmamalı.");

    var tekrarliIslev = tanimlar
        .GroupBy(t => t.Islev)
        .Where(g => g.Count() > 1)
        .Select(g => g.Key.ToString())
        .ToList();
    Assert(tekrarliIslev.Count == 0, $"Tekrarlı bina işlevi tanımı var: {string.Join(", ", tekrarliIslev)}");

    var tekrarliAd = tanimlar
        .GroupBy(t => t.Ad.Trim(), StringComparer.OrdinalIgnoreCase)
        .Where(g => g.Count() > 1)
        .Select(g => g.Key)
        .ToList();
    Assert(tekrarliAd.Count == 0, $"Tekrarlı bina işlevi adı var: {string.Join(", ", tekrarliAd)}");
}

static void BinaIsleviTanimlariEnumDegerleriniKapsar()
{
    var eksikler = Enum.GetValues<BinaIslevi>()
        .Where(islev => islev != BinaIslevi.Tanimsiz)
        .Where(islev => BinaIsleviTanimlari.Bul(islev) is null)
        .Select(islev => islev.ToString())
        .ToList();

    Assert(eksikler.Count == 0, $"Tanımı eksik bina işlevleri var: {string.Join(", ", eksikler)}");
    Assert(BinaIsleviTanimlari.Varsayilan.Islev == BinaIslevi.Konut, "Varsayılan bina işlevi Konut olmalı.");
}

static void BinaIsleviTanimlariGecerlidir()
{
    foreach (var tanim in BinaIsleviTanimlari.Tum)
    {
        Assert(tanim.Islev != BinaIslevi.Tanimsiz, "Tanımsız bina işlevi katalogda yer almamalı.");
        Assert(!string.IsNullOrWhiteSpace(tanim.Ad), $"{tanim.Islev} için ad boş olmamalı.");
        Assert(!string.IsNullOrWhiteSpace(tanim.Aciklama), $"{tanim.Islev} için açıklama boş olmamalı.");
        Assert(!string.IsNullOrWhiteSpace(tanim.KapasiteEtiketi), $"{tanim.Islev} için kapasite etiketi boş olmamalı.");
        Assert(tanim.VarsayilanSirkulasyonOrani >= 0, $"{tanim.Islev} sirkülasyon oranı negatif olmamalı.");
        Assert(tanim.VarsayilanTeknikAlanOrani >= 0, $"{tanim.Islev} teknik alan oranı negatif olmamalı.");
        Assert(tanim.VarsayilanKatYuksekligiM > 0, $"{tanim.Islev} kat yüksekliği pozitif olmalı.");
    }
}

static void MahalSablonKodlariBenzersizdir()
{
    var sablonlar = VarsayilanMahalSablonlari.Tum;
    Assert(sablonlar.Count > 0, "Varsayılan mahal şablon kataloğu boş olmamalı.");

    var tekrarliKodlar = sablonlar
        .GroupBy(s => s.Kod.Trim(), StringComparer.OrdinalIgnoreCase)
        .Where(g => g.Count() > 1)
        .Select(g => g.Key)
        .ToList();

    Assert(tekrarliKodlar.Count == 0, $"Tekrarlı mahal şablon kodu var: {string.Join(", ", tekrarliKodlar)}");
}

static void MahalSablonlariZorunluIslevAileleriniKapsar()
{
    var zorunluIslevler = new[]
    {
        BinaIslevi.Konut,
        BinaIslevi.Villa,
        BinaIslevi.Ofis,
        BinaIslevi.Egitim,
        BinaIslevi.Saglik,
        BinaIslevi.Konaklama,
        BinaIslevi.Kulturel,
        BinaIslevi.KonferansVeToplanti,
        BinaIslevi.Ticari,
        BinaIslevi.RestoranVeYemeIcme,
        BinaIslevi.Endustri,
        BinaIslevi.DepoVeLojistik,
    };

    foreach (var islev in zorunluIslevler)
    {
        var sablonlar = VarsayilanMahalSablonlari.BinaIslevineGore(islev);
        Assert(sablonlar.Count >= 3, $"{islev} için en az 3 mahal şablonu beklenir.");
        Assert(sablonlar.Any(s => s.Zorunlu), $"{islev} için en az bir zorunlu mahal şablonu beklenir.");
    }

    Assert(VarsayilanMahalSablonlari.BinaIslevineGore(BinaIslevi.Konut).Any(s => s.MahalTipi == MahalTipi.Salon),
        "Konut şablonlarında Salon bulunmalı.");
    Assert(VarsayilanMahalSablonlari.BinaIslevineGore(BinaIslevi.Ofis).Any(s => s.MahalTipi == MahalTipi.AcikOfis),
        "Ofis şablonlarında Açık Ofis bulunmalı.");
    Assert(VarsayilanMahalSablonlari.BinaIslevineGore(BinaIslevi.KonferansVeToplanti).Any(s => s.MahalTipi == MahalTipi.KonferansSalonu),
        "Konferans şablonlarında Konferans Salonu bulunmalı.");
    Assert(VarsayilanMahalSablonlari.BinaIslevineGore(BinaIslevi.DepoVeLojistik).Any(s => s.MahalTipi == MahalTipi.Depo),
        "Depo/Lojistik şablonlarında Depo bulunmalı.");
}

static void MahalSablonDegerleriGecerlidir()
{
    foreach (var sablon in VarsayilanMahalSablonlari.Tum)
    {
        Assert(!string.IsNullOrWhiteSpace(sablon.Kod), "Şablon kodu boş olmamalı.");
        Assert(!string.IsNullOrWhiteSpace(sablon.VarsayilanAd), $"{sablon.Kod} varsayılan adı boş olmamalı.");
        Assert(sablon.MahalTipi != MahalTipi.Tanimsiz, $"{sablon.Kod} mahal tipi tanımsız olmamalı.");
        Assert(sablon.KullanimSinifi != KullanimSinifi.Tanimsiz, $"{sablon.Kod} kullanım sınıfı tanımsız olmamalı.");
        Assert(sablon.UygunBinaIslevleri.Count > 0, $"{sablon.Kod} en az bir bina işlevine bağlı olmalı.");
        Assert(!sablon.UygunBinaIslevleri.Contains(BinaIslevi.Tanimsiz), $"{sablon.Kod} tanımsız bina işlevine bağlı olmamalı.");
        Assert(sablon.VarsayilanAdet > 0, $"{sablon.Kod} varsayılan adedi pozitif olmalı.");
        Assert(sablon.MinimumAlanM2 >= 0, $"{sablon.Kod} minimum alanı negatif olmamalı.");
        Assert(sablon.IdealAlanM2 >= sablon.MinimumAlanM2, $"{sablon.Kod} ideal alanı minimum alandan küçük olmamalı.");
        Assert(sablon.MinimumNetYukseklikM > 0, $"{sablon.Kod} minimum net yüksekliği pozitif olmalı.");
        Assert(sablon.KisiBasinaAlanM2 >= 0, $"{sablon.Kod} kişi başına alanı negatif olmamalı.");
    }
}

static void OfisIhtiyacProgramiUretir()
{
    var program = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.Ofis, kapasite: 50, katSayisi: 3);

    Assert(program.BinaIslevi == BinaIslevi.Ofis, "Program bina işlevi Ofis olmalı.");
    Assert(program.ProjeTipi == "Ofis", "Proje tipi bina işlevi tanımından gelmeli.");
    Assert(program.KullaniciKapasitesi == 50, "Kullanıcı kapasitesi korunmalı.");
    Assert(program.KatSayisi == 3, "Kat sayısı korunmalı.");
    Assert(Math.Abs(program.SirkulasyonOrani - 0.22) < 1e-9, "Ofis sirkülasyon oranı tanımdan gelmeli.");
    Assert(Math.Abs(program.TeknikAlanOrani - 0.07) < 1e-9, "Ofis teknik alan oranı tanımdan gelmeli.");
    Assert(Math.Abs(program.VarsayilanKatYuksekligiM - 3.2) < 1e-9, "Ofis kat yüksekliği tanımdan gelmeli.");
    Assert(program.Mahaller.Any(m => m.MahalTipi == MahalTipi.AcikOfis), "Ofis programında açık ofis olmalı.");
    Assert(program.Mahaller.Any(m => m.MahalTipi == MahalTipi.ToplantiOdasi), "Ofis programında toplantı odası olmalı.");
    Assert(program.Mahaller.Any(m => m.MahalTipi == MahalTipi.Wc), "Ofis programında WC olmalı.");
    Assert(program.Mahaller.Any(m => m.MahalTipi == MahalTipi.TeknikOda), "Ofis programında teknik oda olmalı.");

    var acikOfis = program.Mahaller.First(m => m.MahalTipi == MahalTipi.AcikOfis);
    Assert(acikOfis.HedefAlanM2 >= 300, "Açık ofis kapasiteye göre en az 300 m² olmalı.");
    Assert(program.HedefToplamAlanM2 >= program.HedefNetAlanM2, "Hedef toplam alan net alandan küçük olmamalı.");

    var proje = new MimariProje
    {
        ProjeAdi = "Ofis İhtiyaç Programı",
        IhtiyacProgrami = program
    };
    var sonuclar = MimariProjeDogrulamaServisi.Dogrula(proje);
    Assert(!sonuclar.Any(s => s.Seviye == KontrolSeviyesi.Kritik),
        $"Üretilen ofis programı kritik sonuç üretmemeli: {string.Join(", ", sonuclar.Select(s => s.Kod))}");
}

static void KonferansIhtiyacProgramiKapasiteyleOlceklenir()
{
    var program = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.KonferansVeToplanti, kapasite: 300, katSayisi: 1);

    Assert(program.BinaIslevi == BinaIslevi.KonferansVeToplanti, "Program bina işlevi konferans olmalı.");
    Assert(Math.Abs(program.SirkulasyonOrani - 0.30) < 1e-9, "Konferans sirkülasyon oranı tanımdan gelmeli.");
    Assert(Math.Abs(program.TeknikAlanOrani - 0.10) < 1e-9, "Konferans teknik alan oranı tanımdan gelmeli.");
    Assert(program.Mahaller.Any(m => m.MahalTipi == MahalTipi.KonferansSalonu), "Konferans programında salon olmalı.");
    Assert(program.Mahaller.Any(m => m.MahalTipi == MahalTipi.Fuaye), "Konferans programında fuaye olmalı.");
    Assert(program.Mahaller.Any(m => m.MahalTipi == MahalTipi.Wc), "Konferans programında WC olmalı.");

    var salon = program.Mahaller.First(m => m.MahalTipi == MahalTipi.KonferansSalonu);
    var fuaye = program.Mahaller.First(m => m.MahalTipi == MahalTipi.Fuaye);
    Assert(salon.HedefAlanM2 >= 360, "Konferans salonu kapasiteye göre en az 360 m² olmalı.");
    Assert(fuaye.HedefAlanM2 >= 180, "Fuaye kapasiteye göre en az 180 m² olmalı.");
    Assert(program.HedefToplamAlanM2 >= program.HedefNetAlanM2, "Hedef toplam alan net alandan küçük olmamalı.");
}

static void AlanOzetiToplamlariHesaplar()
{
    var program = new IhtiyacProgrami
    {
        BinaIslevi = BinaIslevi.Ofis,
        SirkulasyonOrani = 0.20,
        TeknikAlanOrani = 0.05,
        HedefToplamAlanM2 = 120
    };
    program.Mahaller.Add(new MahalGereksinimi
    {
        Ad = "Açık Ofis",
        MahalTipi = MahalTipi.AcikOfis,
        KullanimSinifi = KullanimSinifi.AnaKullanim,
        Adet = 2,
        HedefAlanM2 = 30,
        HedefNetYukseklikM = 3.0
    });
    program.Mahaller.Add(new MahalGereksinimi
    {
        Ad = "Koridor",
        MahalTipi = MahalTipi.Koridor,
        KullanimSinifi = KullanimSinifi.Sirkulasyon,
        Adet = 1,
        HedefAlanM2 = 20,
        HedefNetYukseklikM = 2.8
    });
    program.Mahaller.Add(new MahalGereksinimi
    {
        Ad = "Teknik Oda",
        MahalTipi = MahalTipi.TeknikOda,
        KullanimSinifi = KullanimSinifi.TeknikHacim,
        Adet = 1,
        HedefAlanM2 = 20,
        HedefNetYukseklikM = 2.6
    });

    var ozet = AlanOzetiServisi.Hesapla(program);

    AssertClose(ozet.NetAlanM2, 100, "Net alan toplamı doğru olmalı.");
    AssertClose(ozet.SirkulasyonAlanM2, 20, "Sirkülasyon alanı oranla hesaplanmalı.");
    AssertClose(ozet.TeknikAlanM2, 5, "Teknik alan oranla hesaplanmalı.");
    AssertClose(ozet.BrutAlanM2, 125, "Brüt alan net + sirkülasyon + teknik alan olmalı.");
    AssertClose(ozet.HedefToplamAlanM2, 120, "Hedef toplam alan korunmalı.");
    AssertClose(ozet.HedefFarkM2, 5, "Hedef farkı brüt alan ile hedef toplam alan farkı olmalı.");
    AssertClose(ozet.HedefFarkOrani, 5.0 / 120.0, "Hedef fark oranı hedef toplam alana göre hesaplanmalı.");
    AssertClose(ozet.NetHacimM3, 288, "Net hacim mahal hacimlerinden hesaplanmalı.");
    Assert(ozet.MahalSayisi == 3, "Mahal satırı sayısı doğru olmalı.");
    Assert(ozet.ToplamMahalAdedi == 4, "Toplam mahal adedi doğru olmalı.");

    var anaKullanim = ozet.KullanimSinifiOzetleri.First(o => o.KullanimSinifi == KullanimSinifi.AnaKullanim);
    Assert(anaKullanim.MahalSayisi == 1, "Ana kullanım satır sayısı doğru olmalı.");
    Assert(anaKullanim.ToplamAdet == 2, "Ana kullanım adedi doğru olmalı.");
    AssertClose(anaKullanim.AlanM2, 60, "Ana kullanım alanı doğru olmalı.");
    AssertClose(anaKullanim.HacimM3, 180, "Ana kullanım hacmi doğru olmalı.");
    AssertClose(anaKullanim.AlanOrani, 0.60, "Ana kullanım alan oranı doğru olmalı.");

    var sirkulasyon = ozet.KullanimSinifiOzetleri.First(o => o.KullanimSinifi == KullanimSinifi.Sirkulasyon);
    AssertClose(sirkulasyon.AlanM2, 20, "Sirkülasyon kırılım alanı doğru olmalı.");

    var teknik = ozet.KullanimSinifiOzetleri.First(o => o.KullanimSinifi == KullanimSinifi.TeknikHacim);
    AssertClose(teknik.AlanM2, 20, "Teknik hacim kırılım alanı doğru olmalı.");
}

static void AlanOzetiUretimProgramiylaTutarlidir()
{
    var program = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.KonferansVeToplanti, kapasite: 300, katSayisi: 1);
    var ozet = AlanOzetiServisi.Hesapla(program);

    AssertClose(ozet.NetAlanM2, program.HedefNetAlanM2, "Özet net alanı program net alanıyla tutarlı olmalı.");
    AssertClose(ozet.SirkulasyonAlanM2, program.OnerilenSirkulasyonAlanM2, "Özet sirkülasyon alanı programla tutarlı olmalı.");
    AssertClose(ozet.TeknikAlanM2, program.OnerilenTeknikAlanM2, "Özet teknik alanı programla tutarlı olmalı.");
    AssertClose(ozet.BrutAlanM2, program.OnerilenBrutAlanM2, "Özet brüt alanı programla tutarlı olmalı.");
    AssertClose(ozet.HedefFarkM2, 0, "Üretilen programda hedef farkı sıfır olmalı.");
    Assert(ozet.KullanimSinifiOzetleri.Any(o => o.KullanimSinifi == KullanimSinifi.AnaKullanim),
        "Özette ana kullanım kırılımı olmalı.");
    Assert(ozet.KullanimSinifiOzetleri.Any(o => o.KullanimSinifi == KullanimSinifi.Sirkulasyon),
        "Özette sirkülasyon kırılımı olmalı.");
    Assert(ozet.KullanimSinifiOzetleri.Any(o => o.KullanimSinifi == KullanimSinifi.TeknikHacim),
        "Özette teknik hacim kırılımı olmalı.");
}

static void IhtiyacProgramiHtmlVeCsvRaporuUretir()
{
    var proje = MimariProjeServisi.YeniOlustur("Rapor Test");
    proje.IhtiyacProgrami = IhtiyacProgramiUretimServisi.Uret(
        BinaIslevi.Ofis,
        kapasite: 25,
        katSayisi: 2,
        projeTipi: "Ofis Raporu");

    var html = IhtiyacProgramiRaporServisi.HtmlOlustur(proje);
    var csv = IhtiyacProgramiRaporServisi.CsvOlustur(proje);
    var htmlMetin = System.Net.WebUtility.HtmlDecode(html);

    Assert(!string.IsNullOrWhiteSpace(html), "HTML rapor boş olmamalı.");
    Assert(html.Contains("<html", StringComparison.OrdinalIgnoreCase), "HTML rapor kök html etiketi içermeli.");
    Assert(htmlMetin.Contains("Rapor Test", StringComparison.Ordinal), "HTML rapor proje adını içermeli.");
    Assert(htmlMetin.Contains("Açık Ofis", StringComparison.Ordinal), "HTML rapor mahal adını içermeli.");
    Assert(htmlMetin.Contains("Net Alan", StringComparison.Ordinal), "HTML rapor alan özeti içermeli.");

    Assert(!string.IsNullOrWhiteSpace(csv), "CSV rapor boş olmamalı.");
    Assert(csv.StartsWith("Mahal Id;", StringComparison.Ordinal), "CSV rapor başlık satırıyla başlamalı.");
    Assert(csv.Contains("Açık Ofis", StringComparison.Ordinal), "CSV rapor mahal adını içermeli.");
    Assert(csv.Contains("Toplam Hedef Alan", StringComparison.Ordinal), "CSV rapor toplam alan sütununu içermeli.");
}

static void KuralModelleriGuvenliVarsayilanlarlaOlusur()
{
    var set = new KuralSeti
    {
        Kod = "M34-TEMEL",
        Ad = "Temel Ön Kontrol",
        Aciklama = "Mimari ihtiyaç programı veri kontrolleri",
        KaynakKodu = "M34"
    };
    set.Kurallar.Add(new Kural
    {
        Kod = "M34-001",
        Baslik = "Bina işlevi kontrolü",
        Kapsam = "IhtiyacProgrami",
        KaynakKodu = "M34",
        CalismaTuru = "Veri",
        GerekliVeriler = ["IhtiyacProgrami.BinaIslevi"]
    });
    set.Kurallar.Add(new Kural
    {
        Kod = "M34-002",
        Baslik = "Kaçış mesafesi kontrolü",
        Kapsam = "PlanTaslagi",
        KaynakKodu = "BYKHY",
        CalismaTuru = "Geometri",
        VarsayilanSeviye = KontrolSeviyesi.KontrolEdilemedi,
        Aktif = false,
        GerekliVeriler = ["PlanTaslagi", "CekirdekTaslagi"]
    });

    Assert(set.Aktif, "Kural seti varsayılan olarak aktif olmalı.");
    Assert(set.ToplamKuralSayisi == 2, "Kural seti toplam kural sayısını hesaplamalı.");
    Assert(set.AktifKuralSayisi == 1, "Kural seti aktif kural sayısını hesaplamalı.");
    Assert(set.Kurallar[0].VarsayilanSeviye == KontrolSeviyesi.Uyari,
        "Kural varsayılan seviyesi uyarı olmalı.");
    Assert(set.Kurallar[0].GerekliVeriler.Contains("IhtiyacProgrami.BinaIslevi"),
        "Kural gerekli veri listesini taşımalı.");

    var sonuc = new KuralSonucu
    {
        Seviye = KontrolSeviyesi.KontrolEdilemedi,
        KuralKodu = "M34-002",
        KaynakKodu = "BYKHY",
        Baslik = "Kaçış mesafesi kontrolü",
        Mesaj = "Plan taslağı olmadığı için kaçış mesafesi ölçülemedi.",
        CozumOnerisi = "M32/M33 ile plan ve çekirdek taslağı oluşturun.",
        VeriEksigi = "PlanTaslagi"
    };

    Assert(sonuc.KontrolEdilemediMi, "Kontrol edilemedi sonucu ayırt edilmeli.");
    Assert(!sonuc.KritikMi, "Kontrol edilemedi sonucu kritik sayılmamalı.");
    Assert(sonuc.KontrolZamani <= DateTime.Now, "Kural sonucu kontrol zamanı üretmeli.");

    var mimariSonuc = sonuc.MimariSonucaDonustur();
    Assert(mimariSonuc.Kod == "M34-002", "Kural sonucu mimari kontrol sonucuna kural kodunu taşımalı.");
    Assert(mimariSonuc.Kaynak == "BYKHY", "Kural sonucu mimari kontrol sonucuna kaynak kodunu taşımalı.");
    Assert(mimariSonuc.Seviye == KontrolSeviyesi.KontrolEdilemedi,
        "Kural sonucu mimari kontrol sonucuna seviyeyi taşımalı.");

    var proje = MimariProjeServisi.YeniOlustur("M34 Model Test");
    var baglam = KontrolBaglami.Projeden(proje);
    baglam.AktifKaynakProfilleri.Add("M34");
    baglam.Parametreler["Dil"] = "tr-TR";

    Assert(baglam.ProjeVar, "Kontrol bağlamı proje var bilgisini üretmeli.");
    Assert(baglam.IhtiyacProgramiVar, "Kontrol bağlamı ihtiyaç programı var bilgisini üretmeli.");
    Assert(!baglam.GeometriVerisiVar, "Plan/çekirdek yoksa geometri verisi var sayılmamalı.");
    Assert(ReferenceEquals(baglam.IhtiyacProgrami, proje.IhtiyacProgrami),
        "Kontrol bağlamı proje ihtiyaç programına bağlanmalı.");
    Assert(baglam.AktifKaynakProfilleri.Contains("M34"), "Kontrol bağlamı kaynak profil kodlarını taşımalı.");
    Assert(baglam.Parametreler["Dil"] == "tr-TR", "Kontrol bağlamı parametreleri taşımalı.");
}

static void KuralCalistiriciEksikVeriyiGuvenliYakalar()
{
    var kuralSeti = new KuralSeti
    {
        Kod = "M34-RUNNER",
        Ad = "Çalıştırıcı Test Seti",
        KaynakKodu = "M34"
    };
    kuralSeti.Kurallar.Add(new Kural
    {
        Kod = "M34-GEN-001",
        Baslik = "Proje adı kontrolü",
        KaynakKodu = "M34",
        GerekliVeriler = ["MimariProje.ProjeAdi"]
    });
    kuralSeti.Kurallar.Add(new Kural
    {
        Kod = "M34-GEO-001",
        Baslik = "Kaçış mesafesi ön kontrolü",
        KaynakKodu = "BYKHY",
        CalismaTuru = "Geometri",
        GerekliVeriler = ["PlanTaslagi", "CekirdekTaslagi"]
    });
    kuralSeti.Kurallar.Add(new Kural
    {
        Kod = "M34-PASS-001",
        Baslik = "Parametre kontrolü",
        KaynakKodu = "M34",
        GerekliVeriler = ["Dil"]
    });
    kuralSeti.Kurallar.Add(new Kural
    {
        Kod = "M34-PASSIVE-001",
        Baslik = "Pasif kural",
        KaynakKodu = "M34",
        Aktif = false,
        GerekliVeriler = ["MimariProje"]
    });

    var sonuclar = YonetmelikOnKontrolServisi.Calistir(new KontrolBaglami(), [kuralSeti]);
    Assert(sonuclar.Count == 4, "Boş bağlamda dört eksik veri sonucu beklenir.");
    Assert(sonuclar.All(s => s.Seviye == KontrolSeviyesi.KontrolEdilemedi),
        "Eksik veri sonuçları kontrol edilemedi seviyesinde olmalı.");
    Assert(sonuclar.Any(s => s.KuralKodu == "M34-GEN-001" && s.VeriEksigi == "MimariProje.ProjeAdi"),
        "Proje adı eksikliği yakalanmalı.");
    Assert(sonuclar.Any(s => s.KuralKodu == "M34-GEO-001" && s.VeriEksigi == "PlanTaslagi"),
        "Plan taslağı eksikliği yakalanmalı.");
    Assert(sonuclar.Any(s => s.KuralKodu == "M34-GEO-001" && s.VeriEksigi == "CekirdekTaslagi"),
        "Çekirdek taslağı eksikliği yakalanmalı.");
    Assert(sonuclar.Any(s => s.KuralKodu == "M34-PASS-001" && s.VeriEksigi == "Dil"),
        "Parametre eksikliği yakalanmalı.");
    Assert(!sonuclar.Any(s => s.KuralKodu == "M34-PASSIVE-001"),
        "Pasif kural çalıştırılmamalı.");

    var proje = MimariProjeServisi.YeniOlustur("Yönetmelik Testi");
    proje.IhtiyacProgrami = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.Ofis, kapasite: 12, katSayisi: 2);
    var baglam = KontrolBaglami.Projeden(proje);
    baglam.Parametreler["Dil"] = "tr-TR";

    var geometriEksikleri = YonetmelikOnKontrolServisi.Calistir(baglam, [kuralSeti]);
    Assert(geometriEksikleri.Count == 2, "Proje verisi tamamlanınca yalnız geometri eksikleri kalmalı.");
    Assert(geometriEksikleri.All(s => s.KuralKodu == "M34-GEO-001"),
        "Geometri eksikleri doğru kuraldan gelmeli.");
    Assert(geometriEksikleri.All(s => s.CozumOnerisi.Contains("oluşturulduktan sonra", StringComparison.Ordinal)),
        "Geometri eksikleri kullanıcıya sonraki taslak adımını anlatmalı.");

    var sirali = YonetmelikOnKontrolServisi.Sirala([
        new KuralSonucu { Seviye = KontrolSeviyesi.Bilgi, KuralKodu = "BILGI" },
        new KuralSonucu { Seviye = KontrolSeviyesi.KontrolEdilemedi, KuralKodu = "KONTROL" },
        new KuralSonucu { Seviye = KontrolSeviyesi.Kritik, KuralKodu = "KRITIK" },
        new KuralSonucu { Seviye = KontrolSeviyesi.Uyari, KuralKodu = "UYARI" }
    ]);
    Assert(sirali[0].Seviye == KontrolSeviyesi.Kritik, "Sıralamada kritik sonuç önce gelmeli.");
    Assert(sirali[1].Seviye == KontrolSeviyesi.Uyari, "Sıralamada uyarı ikinci gelmeli.");
    Assert(sirali[2].Seviye == KontrolSeviyesi.KontrolEdilemedi,
        "Sıralamada kontrol edilemedi uyarıdan sonra gelmeli.");
    Assert(sirali[3].Seviye == KontrolSeviyesi.Bilgi, "Sıralamada bilgi en sonda gelmeli.");

    Assert(YonetmelikOnKontrolServisi.VeriVarMi(baglam, "IhtiyacProgrami.BinaIslevi"),
        "Bina işlevi doluysa veri var kabul edilmeli.");
    Assert(!YonetmelikOnKontrolServisi.VeriVarMi(baglam, "PlanTaslagi"),
        "Plan taslağı yoksa veri yok kabul edilmeli.");
}

static void IlkVeriTabanliKontrollerUyariVeKritikUretir()
{
    var proje = new MimariProje
    {
        ProjeAdi = "",
        IhtiyacProgrami = new IhtiyacProgrami
        {
            BinaIslevi = BinaIslevi.Ofis,
            ProjeTipi = "",
            KullaniciKapasitesi = 0,
            KatSayisi = 0,
            HedefToplamAlanM2 = 50,
            SirkulasyonOrani = 0.65,
            TeknikAlanOrani = 0.35
        }
    };
    proje.IhtiyacProgrami.Mahaller.Add(new MahalGereksinimi
    {
        Id = "mah-sorunlu",
        Ad = "",
        MahalTipi = MahalTipi.AcikOfis,
        KullanimSinifi = KullanimSinifi.Tanimsiz,
        Adet = 0,
        MinimumAlanM2 = 30,
        IdealAlanM2 = 60,
        HedefAlanM2 = 20,
        MinimumNetYukseklikM = 3.0,
        HedefNetYukseklikM = 2.6
    });

    var sonuclar = YonetmelikOnKontrolServisi.IlkVeriKontrolleriniCalistir(proje);

    Assert(sonuclar.Any(s => s.KuralKodu == "M34-PROJE-ADI-EKSIK" && s.Seviye == KontrolSeviyesi.Uyari),
        "Eksik proje adı uyarı üretmeli.");
    Assert(sonuclar.Any(s => s.KuralKodu == "M34-PROJE-TIPI-EKSIK" && s.Seviye == KontrolSeviyesi.Uyari),
        "Eksik proje tipi uyarı üretmeli.");
    Assert(sonuclar.Any(s => s.KuralKodu == "M34-KAPASITE-EKSIK" && s.Seviye == KontrolSeviyesi.Uyari),
        "Eksik kapasite uyarı üretmeli.");
    Assert(sonuclar.Any(s => s.KuralKodu == "M34-KAT-SAYISI-GECERSIZ" && s.Seviye == KontrolSeviyesi.Kritik),
        "Geçersiz kat sayısı kritik sonuç üretmeli.");
    Assert(sonuclar.Any(s => s.KuralKodu == "M34-MAHAL-ADEDI-GECERSIZ" && s.Seviye == KontrolSeviyesi.Kritik),
        "Geçersiz mahal adedi kritik sonuç üretmeli.");
    Assert(sonuclar.Any(s => s.KuralKodu == "M34-KULLANIM-SINIFI-EKSIK" && s.Seviye == KontrolSeviyesi.Uyari),
        "Eksik kullanım sınıfı uyarı üretmeli.");
    Assert(sonuclar.Any(s => s.KuralKodu == "M34-HEDEF-ALAN-MINIMUMDAN-KUCUK"),
        "Minimumdan küçük hedef alan uyarı üretmeli.");
    Assert(sonuclar.Any(s => s.KuralKodu == "M34-HEDEF-YUKSEKLIK-MINIMUMDAN-KUCUK"),
        "Minimumdan küçük hedef yükseklik uyarı üretmeli.");
    Assert(sonuclar.Any(s => s.KuralKodu == "M34-ZORUNLU-MAHAL-EKSIK"),
        "Eksik zorunlu mahal ailesi uyarı üretmeli.");
    Assert(sonuclar.Any(s => s.KuralKodu == "M34-SIRKULASYON-ORANI-YUKSEK"),
        "Yüksek sirkülasyon oranı uyarı üretmeli.");
    Assert(sonuclar.Any(s => s.KuralKodu == "M34-TEKNIK-ALAN-ORANI-YUKSEK"),
        "Yüksek teknik alan oranı uyarı üretmeli.");
    Assert(sonuclar.Any(s => s.KuralKodu == "M34-HEDEF-TOPLAM-ALAN-FARKI"),
        "Hedef toplam alan farkı uyarı üretmeli.");

    Assert(sonuclar[0].Seviye == KontrolSeviyesi.Kritik,
        "İlk veri kontrollerinde kritik sonuçlar önce sıralanmalı.");

    var gecerliProje = MimariProjeServisi.YeniOlustur("Geçerli Ofis");
    gecerliProje.IhtiyacProgrami = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.Ofis, kapasite: 20, katSayisi: 2);
    var gecerliSonuclar = YonetmelikOnKontrolServisi.IlkVeriKontrolleriniCalistir(gecerliProje);

    Assert(!gecerliSonuclar.Any(s => s.Seviye == KontrolSeviyesi.Kritik),
        $"Üretilmiş ofis programı kritik sonuç üretmemeli: {string.Join(", ", gecerliSonuclar.Select(s => s.KuralKodu))}");
    Assert(!gecerliSonuclar.Any(s => s.KuralKodu == "M34-ZORUNLU-MAHAL-EKSIK"),
        "Üretilmiş ofis programında zorunlu mahal ailesi eksik olmamalı.");
}

static void PlanTaslagiYerlesimUyarilariUretir()
{
    var eksikPlanSonuclari = YonetmelikOnKontrolServisi.PlanTaslagiKontrolleriniCalistir(new KontrolBaglami());
    Assert(eksikPlanSonuclari.Any(s =>
            s.KuralKodu == "M34-PLAN-TASLAGI-YOK" &&
            s.Seviye == KontrolSeviyesi.KontrolEdilemedi &&
            s.VeriEksigi == "PlanTaslagi"),
        "Plan taslağı yoksa kontrol edilemedi sonucu üretilmeli.");

    var program = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.Ofis, kapasite: 40, katSayisi: 1);
    var plan = PlanTaslagiUretimServisi.Uret(program, "plan-kontrol");
    var kat = plan.Katlar.First();
    var baglanacakBloklar = KoridorYerlesimServisi.BaglanmasiGerekenBloklariBul(kat).ToList();
    Assert(baglanacakBloklar.Count >= 2, "Plan kontrol testi için en az iki bağlanacak blok beklenir.");

    var temizSonuclar = YonetmelikOnKontrolServisi.PlanTaslagiKontrolleriniCalistir(plan);
    Assert(!temizSonuclar.Any(s => s.KuralKodu == "M34-PLAN-BAGLANTISIZ-BLOK"),
        "Otomatik üretilen planda bağlantısız blok uyarısı olmamalı.");
    Assert(!temizSonuclar.Any(s => s.KuralKodu == "M34-PLAN-BLOK-CAKISMASI"),
        "Otomatik üretilen planda çakışma uyarısı olmamalı.");

    var blokA = baglanacakBloklar[0];
    var blokB = baglanacakBloklar[1];
    foreach (var koridor in kat.Koridorlar)
    {
        koridor.BagliBlokIdleri.RemoveAll(id => string.Equals(id, blokA.Id, StringComparison.OrdinalIgnoreCase));
    }

    blokB.X = blokA.X;
    blokB.Y = blokA.Y;
    blokA.GenislikM = Math.Max(0.1, blokA.GenislikM / 3.0);

    var sorunluSonuclar = YonetmelikOnKontrolServisi.PlanTaslagiKontrolleriniCalistir(
        new KontrolBaglami { PlanTaslagi = plan });

    Assert(sorunluSonuclar.Any(s =>
            s.KuralKodu == "M34-PLAN-BAGLANTISIZ-BLOK" &&
            s.EtkilenenMahalId == blokA.Id &&
            s.EtkilenenKatId == kat.Id),
        "Bağlantısız blok mahal ve kat Id'siyle uyarı üretmeli.");
    Assert(sorunluSonuclar.Any(s =>
            s.KuralKodu == "M34-PLAN-BLOK-CAKISMASI" &&
            s.EtkilenenMahalId == blokA.Id),
        "Çakışan blok çifti uyarı üretmeli.");
    Assert(sorunluSonuclar.Any(s =>
            s.KuralKodu == "M34-PLAN-ALAN-TOLERANSI" &&
            s.EtkilenenMahalId == blokA.Id),
        "Alan toleransı dışına çıkan blok uyarı üretmeli.");
    Assert(sorunluSonuclar.All(s => s.KaynakKodu == YonetmelikOnKontrolServisi.VarsayilanVeriKaynakKodu),
        "Plan taslağı uyarıları M34 veri kaynak profiline bağlanmalı.");

    var proje = MimariProjeServisi.YeniOlustur("Plan Bağlam Testi");
    proje.IhtiyacProgrami = program;
    var baglam = KontrolBaglami.Projeden(proje);
    baglam.PlanTaslagi = plan;
    Assert(YonetmelikOnKontrolServisi.VeriVarMi(baglam, "PlanTaslagi"),
        "Kontrol bağlamındaki plan taslağı veri var kabul edilmeli.");
    Assert(YonetmelikOnKontrolServisi.IlkVeriKontrolleriniCalistir(baglam)
        .Any(s => s.KuralKodu == "M34-PLAN-BAGLANTISIZ-BLOK"),
        "İlk veri kontrolleri bağlamda plan varsa yerleşim uyarılarını da taşımalı.");
}

static void KaynakProfilleriResmiIzBilgisiTasir()
{
    var profiller = VarsayilanYonetmelikKaynakProfilleri.Tum;
    Assert(profiller.Count >= 7, "Başlangıç kaynak profili kataloğu yeterli sayıda profil taşımalı.");

    var tekrarliKodlar = profiller
        .GroupBy(p => p.Kod.Trim(), StringComparer.OrdinalIgnoreCase)
        .Where(g => g.Count() > 1)
        .Select(g => g.Key)
        .ToList();
    Assert(tekrarliKodlar.Count == 0, $"Tekrarlı kaynak profili kodu var: {string.Join(", ", tekrarliKodlar)}");

    foreach (var kod in new[] { "M34-VERI", "PAIY", "BYKHY", "OTOPARK", "TBDY", "BEPY", "ERISILEBILIRLIK" })
    {
        var profil = VarsayilanYonetmelikKaynakProfilleri.Bul(kod);
        if (profil is null)
            throw new InvalidOperationException($"{kod} kaynak profili bulunmalı.");

        Assert(profil.TemelBilgilerVar, $"{kod} kaynak profili temel bilgileri taşımalı.");
        Assert(profil.SonDogrulamaTarihi != DateOnly.MinValue, $"{kod} son doğrulama tarihi taşımalı.");
    }

    foreach (var profil in profiller.Where(p => p.ResmiKaynakMi))
    {
        Assert(!string.IsNullOrWhiteSpace(profil.ResmiGazeteSayisi), $"{profil.Kod} Resmi Gazete sayısı taşımalı.");
        Assert(profil.ResmiKaynakUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase),
            $"{profil.Kod} resmi kaynak URL'si https olmalı.");
    }

    var veriProfili = VarsayilanYonetmelikKaynakProfilleri.Bul("m34-veri");
    if (veriProfili is null)
        throw new InvalidOperationException("Kaynak profili arama büyük/küçük harfe duyarsız olmalı.");

    Assert(veriProfili.KuralKodlari.Contains("M34-KAPASITE-EKSIK"),
        "M34 veri profili ilk veri kural kodlarını taşımalı.");
    Assert(veriProfili.KuralKodlari.Contains("M34-PLAN-BAGLANTISIZ-BLOK"),
        "M34 veri profili plan taslağı kural kodlarını taşımalı.");
}

static void IlkKontrolSonuclariKaynakProfilineBaglanir()
{
    var proje = new MimariProje
    {
        ProjeAdi = "",
        IhtiyacProgrami = new IhtiyacProgrami
        {
            BinaIslevi = BinaIslevi.Ofis,
            KullaniciKapasitesi = 0,
            KatSayisi = 0
        }
    };

    var sonuclar = YonetmelikOnKontrolServisi.IlkVeriKontrolleriniCalistir(proje);
    Assert(sonuclar.Count > 0, "Sorunlu proje kaynak profiline bağlanacak sonuç üretmeli.");

    foreach (var sonuc in sonuclar)
    {
        var kaynak = VarsayilanYonetmelikKaynakProfilleri.Bul(sonuc.KaynakKodu);
        if (kaynak is null)
            throw new InvalidOperationException($"{sonuc.KuralKodu} için kaynak profili bulunmalı.");

        Assert(kaynak.Kod == YonetmelikOnKontrolServisi.VarsayilanVeriKaynakKodu,
            $"{sonuc.KuralKodu} ilk veri kontrolü M34 veri profiline bağlı olmalı.");

        var kuralKaynagi = VarsayilanYonetmelikKaynakProfilleri.KuralKaynagiBul(sonuc.KuralKodu);
        if (kuralKaynagi is null)
            throw new InvalidOperationException($"{sonuc.KuralKodu} kaynak profili kural listesinde yer almalı.");

        Assert(kuralKaynagi.Kod == sonuc.KaynakKodu,
            $"{sonuc.KuralKodu} sonuç kaynak kodu ile profil kural kaynağı aynı olmalı.");
    }
}

static void PlanBlokModelleriGuvenliVarsayilanlarlaOlusur()
{
    var bosPlan = new PlanTaslagi();
    Assert(!string.IsNullOrWhiteSpace(bosPlan.Id), "Boş plan taslağı Id üretmeli.");
    Assert(bosPlan.Katlar.Count == 0, "Boş plan taslağı boş kat listesiyle başlamalı.");
    Assert(bosPlan.Iliskiler.Count == 0, "Boş plan taslağı boş ilişki listesiyle başlamalı.");
    Assert(bosPlan.KatSayisi == 0, "Boş plan taslağı kat sayısını sıfır hesaplamalı.");
    Assert(bosPlan.ToplamBlokSayisi == 0, "Boş plan taslağı blok sayısını sıfır hesaplamalı.");
    Assert(bosPlan.Parametreler.VarsayilanKoridorGenisligiM > 0,
        "Yerleşim parametreleri güvenli koridor genişliği varsayılanı taşımalı.");
    Assert(bosPlan.Parametreler.AlanToleransiOrani > 0,
        "Yerleşim parametreleri pozitif alan toleransı taşımalı.");

    var gereksinim = new MahalGereksinimi
    {
        Id = "mah-acik-ofis",
        Ad = "Açık Ofis",
        MahalTipi = MahalTipi.AcikOfis,
        KullanimSinifi = KullanimSinifi.AnaKullanim,
        Adet = 2,
        HedefAlanM2 = 30,
        Notlar = "Cephe ister"
    };

    var blok = MahalBlogu.Gereksinimden(gereksinim, katNo: 2);
    Assert(blok.MahalGereksinimiId == "mah-acik-ofis", "Blok mahal gereksinimi Id'sini taşımalı.");
    Assert(blok.Ad == "Açık Ofis", "Blok mahal adını taşımalı.");
    Assert(blok.MahalTipi == MahalTipi.AcikOfis, "Blok mahal tipini taşımalı.");
    Assert(blok.KullanimSinifi == KullanimSinifi.AnaKullanim, "Blok kullanım sınıfını taşımalı.");
    Assert(blok.KatNo == 2, "Blok kat numarasını taşımalı.");
    Assert(blok.YerlesmisMi, "Gereksinimden üretilen blok taslak boyutlarıyla yerleşmiş sayılmalı.");
    AssertClose(blok.HedefAlanM2, 60, "Blok hedef alanı toplam mahal hedef alanından gelmeli.");
    AssertClose(blok.AlanM2, 60, "Kare başlangıç bloğu hedef alanı korumalı.");
    AssertClose(blok.AlanFarkOrani, 0, "Başlangıç bloğunda alan fark oranı sıfır olmalı.");

    var koridor = new KoridorTaslagi
    {
        BaslangicX = 0,
        BaslangicY = 0,
        BitisX = 10,
        BitisY = 0,
        GenislikM = 1.8,
        BagliBlokIdleri = [blok.Id]
    };

    var kat = new KatTaslagi
    {
        KatNo = 2,
        Ad = "Kat 2",
        HedefAlanM2 = 100,
        Sinir = new PlanSiniri
        {
            GenislikM = 20,
            DerinlikM = 10
        },
        Bloklar = [blok],
        Koridorlar = [koridor]
    };

    Assert(kat.BlokSayisi == 1, "Kat taslağı blok sayısını hesaplamalı.");
    Assert(ReferenceEquals(kat.BlokBul(blok.Id), blok), "Kat taslağı blok Id ile arama yapabilmeli.");
    AssertClose(kat.BlokHedefAlanM2, 60, "Kat blok hedef alanını hesaplamalı.");
    AssertClose(kat.BlokYerlesimAlanM2, 60, "Kat blok yerleşim alanını hesaplamalı.");
    AssertClose(kat.Sinir.AlanM2, 200, "Kat sınır alanı hesaplanmalı.");
    AssertClose(kat.Koridorlar[0].UzunlukM, 10, "Koridor uzunluğu hesaplanmalı.");
    AssertClose(kat.KoridorAlanM2, 18, "Kat koridor alanını hesaplamalı.");
    AssertClose(kat.ToplamTaslakAlanM2, 78, "Kat taslak alanı blok ve koridor alanını toplamalı.");

    var plan = new PlanTaslagi
    {
        ProjeId = "proje-01",
        Katlar = [kat],
        Iliskiler =
        [
            new MahalIliskisi
            {
                KaynakMahalId = blok.Id,
                HedefMahalId = "koridor-01",
                IliskiTipi = IliskiTipi.KoridoraBaglanmali,
                Oncelik = IliskiOnceligi.Yuksek,
                Aciklama = "Ana kullanım koridora bağlanmalı"
            }
        ]
    };

    Assert(plan.ProjeId == "proje-01", "Plan taslağı proje Id'sini taşımalı.");
    Assert(plan.KatSayisi == 1, "Plan taslağı kat sayısını hesaplamalı.");
    Assert(plan.ToplamBlokSayisi == 1, "Plan taslağı toplam blok sayısını hesaplamalı.");
    Assert(ReferenceEquals(plan.KatBul(2), kat), "Plan taslağı kat numarasıyla arama yapabilmeli.");
    AssertClose(plan.ToplamHedefAlanM2, 100, "Plan hedef alanı kat hedef alanından hesaplanmalı.");
    AssertClose(plan.ToplamTaslakAlanM2, 78, "Plan taslak alanı katlardan toplanmalı.");
    Assert(plan.Iliskiler.Count == 1, "Plan taslağı ilişki satırlarını taşımalı.");
    Assert(plan.Iliskiler[0].OncelikPuani == 3, "İlişki öncelik puanı enum değerinden hesaplanmalı.");
    Assert(plan.Iliskiler[0].HedefliMi, "Hedef mahal Id'si varsa ilişki hedefli sayılmalı.");
}

static void IliskiMatrisiOfisVeKonutIliskileriUretir()
{
    var acikOfis = Blok("ofis-ana", "Açık Ofis", MahalTipi.AcikOfis, KullanimSinifi.AnaKullanim);
    var toplanti = Blok("ofis-toplanti", "Toplantı", MahalTipi.ToplantiOdasi, KullanimSinifi.YardimciKullanim);
    var wc = Blok("ofis-wc", "WC", MahalTipi.Wc, KullanimSinifi.IslakHacim);
    var saft = Blok("ofis-saft", "Şaft", MahalTipi.Saft, KullanimSinifi.TeknikHacim);
    var teknik = Blok("ofis-teknik", "Teknik Oda", MahalTipi.TeknikOda, KullanimSinifi.TeknikHacim);
    var server = Blok("ofis-server", "Server", MahalTipi.ServerOdasi, KullanimSinifi.TeknikHacim);

    var ofisIliskileri = IliskiMatrisiServisi.VarsayilanIliskileriUret(
        [acikOfis, toplanti, wc, saft, teknik, server]);

    Assert(ofisIliskileri.Count > 0, "Ofis blokları için ilişki matrisi üretilmeli.");
    Assert(ofisIliskileri[0].Oncelik == IliskiOnceligi.Kritik,
        "İlişki matrisi kritik ilişkileri önce sıralamalı.");
    Assert(IliskiVar(ofisIliskileri, acikOfis, null, IliskiTipi.KoridoraBaglanmali),
        "Açık ofis koridora bağlanmalı ilişkisi üretmeli.");
    Assert(IliskiVar(ofisIliskileri, acikOfis, null, IliskiTipi.CepheAlmali),
        "Açık ofis cephe almalı ilişkisi üretmeli.");
    Assert(IliskiVar(ofisIliskileri, acikOfis, toplanti, IliskiTipi.YakinOlmali),
        "Açık ofis ve toplantı odası yakın olmalı ilişkisi üretmeli.");
    Assert(IliskiVar(ofisIliskileri, acikOfis, toplanti, IliskiTipi.AyniKattaOlmali),
        "Açık ofis ve toplantı odası aynı katta olmalı ilişkisi üretmeli.");
    Assert(IliskiVar(ofisIliskileri, wc, saft, IliskiTipi.YakinOlmali),
        "WC ve şaft yakın olmalı ilişkisi üretmeli.");
    Assert(IliskiVar(ofisIliskileri, teknik, server, IliskiTipi.YakinOlmali),
        "Teknik oda ve server odası yakın olmalı ilişkisi üretmeli.");

    var tekrarliCiftler = ofisIliskileri
        .GroupBy(i => $"{i.IliskiTipi}|{MinMax(i.KaynakMahalId, i.HedefMahalId)}")
        .Where(g => g.Count() > 1)
        .ToList();
    Assert(tekrarliCiftler.Count == 0, "İlişki matrisi aynı ilişki çiftini tekrar üretmemeli.");

    var salon = Blok("konut-salon", "Salon", MahalTipi.Salon, KullanimSinifi.AnaKullanim);
    var mutfak = Blok("konut-mutfak", "Mutfak", MahalTipi.Mutfak, KullanimSinifi.Servis);
    var yatak = Blok("konut-yatak", "Yatak Odası", MahalTipi.YatakOdasi, KullanimSinifi.AnaKullanim);
    var banyo = Blok("konut-banyo", "Banyo", MahalTipi.Banyo, KullanimSinifi.IslakHacim);
    var konutSaft = Blok("konut-saft", "Şaft", MahalTipi.Saft, KullanimSinifi.TeknikHacim);

    var konutIliskileri = IliskiMatrisiServisi.VarsayilanIliskileriUret(
        [salon, mutfak, yatak, banyo, konutSaft]);

    Assert(IliskiVar(konutIliskileri, salon, mutfak, IliskiTipi.YakinOlmali),
        "Salon ve mutfak yakın olmalı ilişkisi üretmeli.");
    Assert(IliskiVar(konutIliskileri, salon, mutfak, IliskiTipi.AyniKattaOlmali),
        "Salon ve mutfak aynı katta olmalı ilişkisi üretmeli.");
    Assert(IliskiVar(konutIliskileri, banyo, konutSaft, IliskiTipi.YakinOlmali),
        "Banyo ve şaft yakın olmalı ilişkisi üretmeli.");
    Assert(IliskiVar(konutIliskileri, yatak, null, IliskiTipi.CepheAlmali),
        "Yatak odası cephe almalı ilişkisi üretmeli.");
}

static void IlkYerlesimAlgoritmasiBloklariKatlaraCakismadanDagitir()
{
    var bosPlan = PlanTaslagiUretimServisi.Uret((IhtiyacProgrami?)null);
    Assert(bosPlan.Katlar.Count == 0, "Boş programdan boş plan taslağı üretilmeli.");

    var program = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.Ofis, kapasite: 60, katSayisi: 3);
    var plan = PlanTaslagiUretimServisi.Uret(program, "proje-ofis");

    Assert(plan.ProjeId == "proje-ofis", "Plan taslağı proje Id'sini taşımalı.");
    Assert(plan.Katlar.Count == 3, "Plan taslağı program kat sayısını taşımalı.");
    Assert(plan.ToplamBlokSayisi == program.Mahaller.Count,
        "Her mahal gereksinimi bir plan bloğuna dönüşmeli.");
    Assert(plan.Katlar.All(k => k.Bloklar.Count > 0), "İlk dağıtım her kata en az bir blok vermeli.");
    Assert(plan.Iliskiler.Count > 0, "Üretilen plan varsayılan ilişki matrisini taşımalı.");
    Assert(plan.Parametreler.VarsayilanKatSayisi == 3, "Yerleşim parametreleri varsayılan kat sayısını taşımalı.");

    var tumBloklar = plan.Katlar.SelectMany(k => k.Bloklar).ToList();
    Assert(tumBloklar.All(b => b.YerlesmisMi), "Üretilen tüm bloklar boyutlandırılmış olmalı.");
    Assert(tumBloklar.All(b => b.KatNo >= 1 && b.KatNo <= 3), "Blok kat numaraları program kat aralığında olmalı.");
    Assert(tumBloklar.All(b => BasitYerlesimServisi.AlanToleransiIcindeMi(b, plan.Parametreler.AlanToleransiOrani)),
        "Üretilen bloklar alan toleransı içinde kalmalı.");

    foreach (var kat in plan.Katlar)
    {
        Assert(kat.Sinir.GenislikM > 0, $"{kat.Ad} taslak sınır genişliği pozitif olmalı.");
        Assert(kat.Sinir.DerinlikM > 0, $"{kat.Ad} taslak sınır derinliği pozitif olmalı.");
        Assert(kat.Bloklar.All(b => b.KatNo == kat.KatNo), $"{kat.Ad} blokları kat numarasını taşımalı.");
        Assert(BasitYerlesimServisi.CakisanBlokCiftleri(kat).Count == 0,
            $"{kat.Ad} içinde çakışan blok olmamalı.");
    }

    var beklenenNetAlan = program.Mahaller.Sum(m => m.ToplamHedefAlanM2);
    var blokHedefToplami = tumBloklar.Sum(b => b.HedefAlanM2);
    AssertClose(blokHedefToplami, beklenenNetAlan, "Blok hedef alanları ihtiyaç programı net alanını korumalı.");

    var elleCakismaA = new MahalBlogu
    {
        Id = "cakisma-a",
        HedefAlanM2 = 25,
        X = 0,
        Y = 0,
        GenislikM = 5,
        DerinlikM = 5
    };
    var elleCakismaB = new MahalBlogu
    {
        Id = "cakisma-b",
        HedefAlanM2 = 25,
        X = 4,
        Y = 4,
        GenislikM = 5,
        DerinlikM = 5
    };
    Assert(BasitYerlesimServisi.CakisiyorMu(elleCakismaA, elleCakismaB),
        "Çakışma yardımcısı üst üste gelen blokları yakalamalı.");
}

static void KoridorModeliAnaAksVeBaglantisizMahalTespitiUretir()
{
    var program = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.Ofis, kapasite: 60, katSayisi: 2);
    var plan = PlanTaslagiUretimServisi.Uret(program, "proje-koridor");

    Assert(plan.Katlar.Count == 2, "Koridor testi iki katlı plan üretmeli.");
    foreach (var kat in plan.Katlar)
    {
        Assert(kat.Koridorlar.Count == 1, $"{kat.Ad} için bir ana koridor üretilmeli.");

        var koridor = kat.Koridorlar[0];
        Assert(koridor.YatayMi, $"{kat.Ad} ana koridoru yatay aks olarak üretilmeli.");
        Assert(koridor.UzunlukM > 0, $"{kat.Ad} ana koridor uzunluğu pozitif olmalı.");
        Assert(koridor.AlanM2 > 0, $"{kat.Ad} ana koridor alanı pozitif olmalı.");
        AssertClose(koridor.GenislikM, plan.Parametreler.VarsayilanKoridorGenisligiM,
            $"{kat.Ad} ana koridor genişliği yerleşim parametresinden gelmeli.");

        var baglanmasiGerekenler = KoridorYerlesimServisi.BaglanmasiGerekenBloklariBul(kat);
        Assert(baglanmasiGerekenler.Count > 0, $"{kat.Ad} içinde koridora bağlanması gereken blok olmalı.");
        Assert(koridor.BagliBlokIdleri.Count == baglanmasiGerekenler.Count,
            $"{kat.Ad} ana koridoru bağlanması gereken blokları taşımalı.");
        Assert(KoridorYerlesimServisi.BaglantisizBloklariBul(kat).Count == 0,
            $"{kat.Ad} içinde otomatik üretimden sonra bağlantısız blok kalmamalı.");
    }

    var elleBlok = Blok("manuel-ofis", "Manuel Ofis", MahalTipi.Ofis, KullanimSinifi.AnaKullanim);
    var sirkulasyon = Blok("manuel-koridor", "Mevcut Koridor", MahalTipi.Koridor, KullanimSinifi.Sirkulasyon);
    var manuelKat = new KatTaslagi
    {
        KatNo = 1,
        Ad = "Manuel Kat",
        Bloklar = [elleBlok, sirkulasyon]
    };

    var ilkBaglantisizlar = KoridorYerlesimServisi.BaglantisizBloklariBul(manuelKat);
    Assert(ilkBaglantisizlar.Count == 1 && ReferenceEquals(ilkBaglantisizlar[0], elleBlok),
        "Koridorsuz ana kullanım bloğu bağlantısız mahal olarak tespit edilmeli.");

    manuelKat.Koridorlar.Add(new KoridorTaslagi
    {
        BaslangicX = 0,
        BaslangicY = 8,
        BitisX = 12,
        BitisY = 8,
        GenislikM = 1.5
    });
    Assert(KoridorYerlesimServisi.BaglantisizBloklariBul(manuelKat).Count == 1,
        "Koridor var ama blok bağlantısı yoksa mahal bağlantısız kalmalı.");

    manuelKat.Koridorlar[0].BagliBlokIdleri.Add(elleBlok.Id);
    Assert(KoridorYerlesimServisi.KoridoraBagliMi(manuelKat, elleBlok),
        "Blok Id koridora eklendiğinde bağlı sayılmalı.");
    Assert(KoridorYerlesimServisi.BaglantisizBloklariBul(manuelKat).Count == 0,
        "Bağlı ana kullanım bloğu bağlantısız listesinde görünmemeli.");
}

static void PlanTaslagiE4MimariDosyasindaKorunur()
{
    var yol = Path.Combine(Path.GetTempPath(), $"e4mimari-plan-test-{Guid.NewGuid():N}.e4mimari.json");
    try
    {
        var proje = MimariProjeServisi.YeniOlustur("Plan Kayıt Testi");
        proje.IhtiyacProgrami = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.Ofis, kapasite: 60, katSayisi: 2);
        proje.MahalSablonlari = VarsayilanMahalSablonlari.Tum.ToList();
        proje.PlanTaslagi = PlanTaslagiUretimServisi.Uret(proje.IhtiyacProgrami, proje.Id);
        var kaynakPlan = proje.PlanTaslagi;

        var kaynakKat = kaynakPlan.Katlar.First();
        var kaynakBlok = kaynakKat.Bloklar.OrderByDescending(b => b.HedefAlanM2).First();
        kaynakBlok.X = 12.34;
        kaynakBlok.Y = 5.67;
        kaynakBlok.Kilitli = true;
        var kaynakKoridor = kaynakKat.Koridorlar.First();

        MimariProjeServisi.Kaydet(proje, yol);
        Assert(File.Exists(yol), "Plan taslaklı mimari proje dosyası oluşturulmalı.");
        Assert(File.ReadAllText(yol).Contains("\"PlanTaslagi\"", StringComparison.Ordinal),
            "Kaydedilen dosya plan taslağı alanını içermeli.");

        var acilan = MimariProjeServisi.Ac(yol);
        var acilanPlan = acilan.PlanTaslagi;
        if (acilanPlan is null)
            throw new InvalidOperationException("Açılan projede plan taslağı korunmalı.");

        Assert(acilanPlan.ProjeId == proje.Id, "Plan taslağı proje Id'sini korumalı.");
        Assert(acilanPlan.Katlar.Count == kaynakPlan.Katlar.Count, "Plan kat sayısı korunmalı.");
        Assert(acilanPlan.Iliskiler.Count == kaynakPlan.Iliskiler.Count, "Plan ilişki listesi korunmalı.");
        Assert(acilanPlan.Parametreler.VarsayilanKoridorGenisligiM > 0,
            "Plan yerleşim parametreleri korunmalı.");

        var acilanKat = acilanPlan.KatBul(kaynakKat.KatNo);
        if (acilanKat is null)
            throw new InvalidOperationException("Açılan planda kaynak kat bulunmalı.");

        var acilanBlok = acilanKat.BlokBul(kaynakBlok.Id);
        if (acilanBlok is null)
            throw new InvalidOperationException("Açılan planda kaynak blok bulunmalı.");

        Assert(acilanBlok.Kilitli, "Blok kilit durumu korunmalı.");
        AssertClose(acilanBlok.X, 12.34, "Blok X konumu korunmalı.");
        AssertClose(acilanBlok.Y, 5.67, "Blok Y konumu korunmalı.");
        Assert(acilanKat.Koridorlar.Any(k =>
                k.Id == kaynakKoridor.Id &&
                k.BagliBlokIdleri.Count == kaynakKoridor.BagliBlokIdleri.Count),
            "Koridor Id'si ve bağlı blok listesi korunmalı.");

        var baglam = KontrolBaglami.Projeden(acilan);
        Assert(baglam.PlanTaslagi is not null, "Kontrol bağlamı açılan proje plan taslağını taşımalı.");
        Assert(YonetmelikOnKontrolServisi.VeriVarMi(baglam, "PlanTaslagi"),
            "Açılan proje plan taslağı M34 için veri var kabul edilmeli.");
    }
    finally
    {
        if (File.Exists(yol))
            File.Delete(yol);
    }
}

static void CekirdekVeSaftModelleriGuvenliVarsayilanlarlaOlusur()
{
    var cekirdek = new CekirdekElemani
    {
        Id = "cek-ana",
        Ad = "Ana Merdiven",
        Tip = CekirdekTipi.Merdiven,
        KatNo = 1,
        BaslangicKatNo = 1,
        BitisKatNo = 3,
        X = 2,
        Y = 4,
        GenislikM = 2.4,
        DerinlikM = 5,
        NetGenislikM = 1.5,
        KapasiteKisi = 80,
        AksKodu = "A-1",
        BagliKoridorIdleri = ["kor-01"],
        BagliMahalIdleri = ["mah-01"]
    };

    Assert(!string.IsNullOrWhiteSpace(cekirdek.Id), "Çekirdek elemanı Id taşımalı.");
    Assert(cekirdek.Tip == CekirdekTipi.Merdiven, "Çekirdek tipi korunmalı.");
    Assert(cekirdek.YerlesmisMi, "Boyutlu çekirdek yerleşmiş sayılmalı.");
    Assert(cekirdek.DuseySurekliMi, "Birden fazla kata yayılan çekirdek düşey sürekli sayılmalı.");
    Assert(cekirdek.KatAraligindaMi(2), "Çekirdek kat aralığı içindeki katı kapsamalı.");
    Assert(!cekirdek.KatAraligindaMi(4), "Çekirdek kat aralığı dışındaki katı kapsamamalı.");
    AssertClose(cekirdek.AlanM2, 12, "Çekirdek alanı hesaplanmalı.");
    AssertClose(cekirdek.MerkezX, 3.2, "Çekirdek merkez X değeri hesaplanmalı.");
    AssertClose(cekirdek.MerkezY, 6.5, "Çekirdek merkez Y değeri hesaplanmalı.");

    var grup = new CekirdekGrubu
    {
        Id = "grp-ana",
        Ad = "Ana Çekirdek",
        AksKodu = "A-1",
        BaslangicKatNo = 1,
        BitisKatNo = 3,
        MerkezX = cekirdek.MerkezX,
        MerkezY = cekirdek.MerkezY,
        ElemanIdleri = [cekirdek.Id]
    };

    Assert(grup.KatAraligindaMi(3), "Çekirdek grubu üst katı kapsamalı.");
    Assert(!grup.KatAraligindaMi(0), "Çekirdek grubu kat aralığı dışını kapsamamalı.");
    Assert(grup.ElemanIdleri.Contains(cekirdek.Id), "Çekirdek grubu eleman Id'sini taşımalı.");

    var saft = new SaftTaslagi
    {
        Id = "saft-wc",
        Ad = "Islak Hacim Şaftı",
        TesisatTuru = SaftTesisatTuru.SihhiTesisat,
        KatNo = 1,
        BaslangicKatNo = 1,
        BitisKatNo = 3,
        X = 8,
        Y = 4,
        GenislikM = 1,
        DerinlikM = 1.2,
        AksKodu = "S-1",
        BagliMahalIdleri = ["wc-01"]
    };

    Assert(saft.YerlesmisMi, "Boyutlu şaft yerleşmiş sayılmalı.");
    Assert(saft.KatAraligindaMi(2), "Şaft kat aralığı içindeki katı kapsamalı.");
    AssertClose(saft.AlanM2, 1.2, "Şaft alanı hesaplanmalı.");
    AssertClose(saft.MerkezX, 8.5, "Şaft merkez X değeri hesaplanmalı.");
    AssertClose(saft.MerkezY, 4.6, "Şaft merkez Y değeri hesaplanmalı.");

    var kat = new KatTaslagi
    {
        KatNo = 1,
        Cekirdekler = [cekirdek],
        CekirdekGruplari = [grup],
        Saftlar = [saft]
    };

    Assert(kat.CekirdekSayisi == 1, "Kat çekirdek sayısını hesaplamalı.");
    Assert(kat.SaftSayisi == 1, "Kat şaft sayısını hesaplamalı.");
    Assert(ReferenceEquals(kat.CekirdekBul(cekirdek.Id), cekirdek), "Kat çekirdeği Id ile bulabilmeli.");
    Assert(ReferenceEquals(kat.CekirdekGrubuBul(grup.Id), grup), "Kat çekirdek grubunu Id ile bulabilmeli.");
    Assert(ReferenceEquals(kat.SaftBul(saft.Id), saft), "Kat şaftı Id ile bulabilmeli.");
    AssertClose(kat.CekirdekAlanM2, 12, "Kat çekirdek alanını hesaplamalı.");
    AssertClose(kat.SaftAlanM2, 1.2, "Kat şaft alanını hesaplamalı.");
    AssertClose(kat.DuseySirkulasyonAlanM2, 13.2, "Kat düşey sirkülasyon alanını hesaplamalı.");
    AssertClose(kat.ToplamTaslakAlanM2, 13.2, "Kat taslak alanı çekirdek ve şaft alanını içermeli.");

    var plan = new PlanTaslagi { Katlar = [kat] };
    Assert(plan.ToplamCekirdekSayisi == 1, "Plan toplam çekirdek sayısını hesaplamalı.");
    Assert(plan.ToplamSaftSayisi == 1, "Plan toplam şaft sayısını hesaplamalı.");
    AssertClose(plan.ToplamTaslakAlanM2, 13.2, "Plan taslak alanı düşey sirkülasyon alanını içermeli.");
}

static void CekirdekVeSaftE4MimariDosyasindaKorunur()
{
    var yol = Path.Combine(Path.GetTempPath(), $"e4mimari-cekirdek-test-{Guid.NewGuid():N}.e4mimari.json");
    try
    {
        var proje = MimariProjeServisi.YeniOlustur("Çekirdek Kayıt Testi");
        proje.IhtiyacProgrami = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.Ofis, kapasite: 40, katSayisi: 2);
        proje.PlanTaslagi = PlanTaslagiUretimServisi.Uret(proje.IhtiyacProgrami, proje.Id);

        var kaynakKat = proje.PlanTaslagi.KatBul(1)
            ?? throw new InvalidOperationException("Kaynak planda Kat 1 bulunmalı.");
        var kaynakKoridor = kaynakKat.Koridorlar.FirstOrDefault();
        var kaynakBlok = kaynakKat.Bloklar.FirstOrDefault();

        var grup = new CekirdekGrubu
        {
            Id = "grp-ana",
            Ad = "Ana Çekirdek",
            AksKodu = "A-1",
            BaslangicKatNo = 1,
            BitisKatNo = 2,
            MerkezX = 4,
            MerkezY = 5,
            ElemanIdleri = ["cek-yangin"]
        };

        var cekirdek = new CekirdekElemani
        {
            Id = "cek-yangin",
            Ad = "Yangın Merdiveni",
            Tip = CekirdekTipi.YanginMerdiveni,
            KatNo = 1,
            BaslangicKatNo = 1,
            BitisKatNo = 2,
            X = 2,
            Y = 3,
            GenislikM = 2.2,
            DerinlikM = 4.5,
            NetGenislikM = 1.4,
            KapasiteKisi = 120,
            AksKodu = "A-1",
            CekirdekGrubuId = grup.Id,
            BagliKoridorIdleri = kaynakKoridor is null ? [] : [kaynakKoridor.Id]
        };

        var saft = new SaftTaslagi
        {
            Id = "saft-sihhi",
            Ad = "Sıhhi Tesisat Şaftı",
            TesisatTuru = SaftTesisatTuru.SihhiTesisat,
            KatNo = 1,
            BaslangicKatNo = 1,
            BitisKatNo = 2,
            X = 6,
            Y = 3,
            GenislikM = 1,
            DerinlikM = 1.1,
            AksKodu = "S-1",
            BagliMahalIdleri = kaynakBlok is null ? [] : [kaynakBlok.Id]
        };

        kaynakKat.CekirdekGruplari.Add(grup);
        kaynakKat.Cekirdekler.Add(cekirdek);
        kaynakKat.Saftlar.Add(saft);

        MimariProjeServisi.Kaydet(proje, yol);
        var json = File.ReadAllText(yol);
        Assert(json.Contains("\"Cekirdekler\"", StringComparison.Ordinal),
            "Kaydedilen dosya çekirdek listesini içermeli.");
        Assert(json.Contains("\"Saftlar\"", StringComparison.Ordinal),
            "Kaydedilen dosya şaft listesini içermeli.");
        Assert(json.Contains("\"YanginMerdiveni\"", StringComparison.Ordinal),
            "Kaydedilen dosya çekirdek tipini string enum olarak saklamalı.");

        var acilan = MimariProjeServisi.Ac(yol);
        var acilanKat = acilan.PlanTaslagi?.KatBul(1)
            ?? throw new InvalidOperationException("Açılan planda Kat 1 bulunmalı.");
        var acilanCekirdek = acilanKat.CekirdekBul(cekirdek.Id)
            ?? throw new InvalidOperationException("Açılan planda kaynak çekirdek bulunmalı.");
        var acilanSaft = acilanKat.SaftBul(saft.Id)
            ?? throw new InvalidOperationException("Açılan planda kaynak şaft bulunmalı.");
        var acilanGrup = acilanKat.CekirdekGrubuBul(grup.Id)
            ?? throw new InvalidOperationException("Açılan planda kaynak çekirdek grubu bulunmalı.");

        Assert(acilan.PlanTaslagi.ToplamCekirdekSayisi == 1, "Açılan plan toplam çekirdek sayısını korumalı.");
        Assert(acilan.PlanTaslagi.ToplamSaftSayisi == 1, "Açılan plan toplam şaft sayısını korumalı.");
        Assert(acilanCekirdek.Tip == CekirdekTipi.YanginMerdiveni, "Çekirdek tipi korunmalı.");
        Assert(acilanCekirdek.BagliKoridorIdleri.Count == cekirdek.BagliKoridorIdleri.Count,
            "Çekirdek koridor bağlantıları korunmalı.");
        Assert(acilanGrup.ElemanIdleri.Contains(cekirdek.Id), "Çekirdek grubu eleman bağlantısını korumalı.");
        Assert(acilanSaft.TesisatTuru == SaftTesisatTuru.SihhiTesisat, "Şaft tesisat türü korunmalı.");
        Assert(acilanSaft.BagliMahalIdleri.Count == saft.BagliMahalIdleri.Count,
            "Şaft mahal bağlantıları korunmalı.");
        Assert(YonetmelikOnKontrolServisi.VeriVarMi(KontrolBaglami.Projeden(acilan), "PlanTaslagi"),
            "Çekirdekli plan M34 için plan taslağı verisi olarak görünmeli.");
    }
    finally
    {
        if (File.Exists(yol))
            File.Delete(yol);
    }
}

static void Model3DGeometriModelleriGuvenliVarsayilanlarlaOlusur()
{
    var mahal = new MahalHacmi3D
    {
        Id = "mh3d-01",
        KaynakMahalBloguId = "blok-01",
        Ad = "Toplantı Salonu",
        Tip = MahalTipi.KonferansSalonu,
        KatNo = 1,
        X = 1,
        Y = 2,
        Z = 0,
        GenislikM = 4,
        DerinlikM = 5,
        YukseklikM = 3
    };

    Assert(mahal.YerlesmisMi, "Boyutlu mahal hacmi yerleşmiş sayılmalı.");
    AssertClose(mahal.AlanM2, 20, "Mahal hacmi alanı hesaplanmalı.");
    AssertClose(mahal.HacimM3, 60, "Mahal hacmi hacmi hesaplanmalı.");
    AssertClose(mahal.MerkezX, 3, "Mahal hacmi merkez X değeri hesaplanmalı.");
    AssertClose(mahal.MerkezY, 4.5, "Mahal hacmi merkez Y değeri hesaplanmalı.");
    AssertClose(mahal.MerkezZ, 1.5, "Mahal hacmi merkez Z değeri hesaplanmalı.");

    var duvar = new Duvar3D
    {
        Id = "duvar-01",
        KaynakMahalBloguId = mahal.KaynakMahalBloguId,
        KatNo = 1,
        BaslangicX = 0,
        BaslangicY = 0,
        BitisX = 3,
        BitisY = 4,
        YukseklikM = 3,
        KalinlikM = 0.2,
        AciklikIdleri = ["kapi-01"]
    };

    Assert(duvar.YerlesmisMi, "Boyutlu duvar yerleşmiş sayılmalı.");
    AssertClose(duvar.UzunlukM, 5, "Duvar uzunluğu hesaplanmalı.");
    AssertClose(duvar.AlanM2, 15, "Duvar alanı hesaplanmalı.");
    AssertClose(duvar.HacimM3, 3, "Duvar hacmi hesaplanmalı.");
    Assert(duvar.AciklikIdleri.Contains("kapi-01"), "Duvar açıklık Id listesini taşımalı.");

    var doseme = new Doseme3D
    {
        Id = "doseme-01",
        KaynakKatTaslagiId = "kat-01",
        KatNo = 1,
        GenislikM = 4,
        DerinlikM = 5,
        KalinlikM = 0.2
    };

    var tavan = new Tavan3D
    {
        Id = "tavan-01",
        KaynakKatTaslagiId = "kat-01",
        KatNo = 1,
        Z = 3,
        GenislikM = 4,
        DerinlikM = 5,
        KalinlikM = 0.1
    };

    Assert(doseme.YerlesmisMi, "Boyutlu döşeme yerleşmiş sayılmalı.");
    Assert(tavan.YerlesmisMi, "Boyutlu tavan yerleşmiş sayılmalı.");
    AssertClose(doseme.AlanM2, 20, "Döşeme alanı hesaplanmalı.");
    AssertClose(doseme.HacimM3, 4, "Döşeme hacmi hesaplanmalı.");
    AssertClose(tavan.AlanM2, 20, "Tavan alanı hesaplanmalı.");
    AssertClose(tavan.HacimM3, 2, "Tavan hacmi hesaplanmalı.");

    var aciklik = new Aciklik3D
    {
        Id = "kapi-01",
        KaynakMahalBloguId = mahal.KaynakMahalBloguId,
        DuvarId = duvar.Id,
        Tip = Aciklik3DTipi.Kapi,
        GenislikM = 0.9,
        YukseklikM = 2.1
    };

    Assert(aciklik.BoyutluMu, "Boyutlu açıklık boyutlu sayılmalı.");
    AssertClose(aciklik.AlanM2, 1.89, "Açıklık alanı hesaplanmalı.");
    AssertClose(aciklik.UstKotuM, 2.1, "Açıklık üst kotu hesaplanmalı.");

    var cekirdekHacmi = new CekirdekHacmi3D
    {
        Id = "cek3d-01",
        KaynakCekirdekId = "cek-01",
        Tip = CekirdekTipi.Merdiven,
        KatNo = 1,
        GenislikM = 2,
        DerinlikM = 4,
        YukseklikM = 3
    };

    var saftHacmi = new SaftHacmi3D
    {
        Id = "saft3d-01",
        KaynakSaftId = "saft-01",
        TesisatTuru = SaftTesisatTuru.SihhiTesisat,
        KatNo = 1,
        GenislikM = 1,
        DerinlikM = 1.2,
        YukseklikM = 3
    };

    Assert(cekirdekHacmi.YerlesmisMi, "Çekirdek 3D hacmi yerleşmiş sayılmalı.");
    Assert(saftHacmi.YerlesmisMi, "Şaft 3D hacmi yerleşmiş sayılmalı.");
    AssertClose(cekirdekHacmi.HacimM3, 24, "Çekirdek 3D hacmi hesaplanmalı.");
    AssertClose(saftHacmi.HacimM3, 3.6, "Şaft 3D hacmi hesaplanmalı.");

    var kat = new Kat3DModel
    {
        Id = "kat3d-01",
        KaynakKatTaslagiId = "kat-01",
        KatNo = 1,
        KatYuksekligiM = 3.2,
        NetYukseklikM = 3,
        Dosemeler = [doseme],
        Tavanlar = [tavan],
        Mahaller = [mahal],
        Duvarlar = [duvar],
        Acikliklar = [aciklik],
        CekirdekHacimleri = [cekirdekHacmi],
        SaftHacimleri = [saftHacmi]
    };

    AssertClose(kat.UstKotuM, 3.2, "Kat üst kotu hesaplanmalı.");
    AssertClose(kat.KullanilanNetYukseklikM, 3, "Kat net yüksekliği kullanılmalı.");
    AssertClose(kat.ToplamMahalAlaniM2, 20, "Kat mahal alanı hesaplanmalı.");
    AssertClose(kat.ToplamMahalHacmiM3, 60, "Kat mahal hacmi hesaplanmalı.");
    AssertClose(kat.ToplamDosemeAlaniM2, 20, "Kat döşeme alanı hesaplanmalı.");
    AssertClose(kat.ToplamDuvarAlaniM2, 15, "Kat duvar alanı hesaplanmalı.");
    Assert(ReferenceEquals(kat.MahalBul(mahal.KaynakMahalBloguId), mahal),
        "Kat kaynak blok Id ile 3D mahal hacmini bulabilmeli.");

    var model = new Bim3DModel
    {
        Id = "model-01",
        KaynakPlanId = "plan-01",
        Katlar = [kat]
    };

    Assert(model.ToplamKatSayisi == 1, "3D model kat sayısını hesaplamalı.");
    Assert(model.ToplamMahalSayisi == 1, "3D model mahal sayısını hesaplamalı.");
    Assert(model.ToplamDuvarSayisi == 1, "3D model duvar sayısını hesaplamalı.");
    Assert(model.ToplamAciklikSayisi == 1, "3D model açıklık sayısını hesaplamalı.");
    Assert(model.ToplamCekirdekHacmiSayisi == 1, "3D model çekirdek hacmi sayısını hesaplamalı.");
    Assert(model.ToplamSaftHacmiSayisi == 1, "3D model şaft hacmi sayısını hesaplamalı.");
    AssertClose(model.ToplamMahalHacmiM3, 60, "3D model mahal hacmini hesaplamalı.");
    AssertClose(model.ToplamDosemeAlaniM2, 20, "3D model döşeme alanını hesaplamalı.");
    Assert(ReferenceEquals(model.KatBul(1), kat), "3D model katı kat no ile bulabilmeli.");

    var plan = new PlanTaslagi { Id = "plan-01", Model3D = model };
    AssertClose(plan.Toplam3DMahalHacmiM3, 60, "Plan 3D mahal hacmini taşımalı.");
    AssertClose(plan.Toplam3DDosemeAlaniM2, 20, "Plan 3D döşeme alanını taşımalı.");
}

static void Model3DE4MimariDosyasindaKorunur()
{
    var yol = Path.Combine(Path.GetTempPath(), $"e4mimari-3d-test-{Guid.NewGuid():N}.e4mimari.json");
    try
    {
        var proje = MimariProjeServisi.YeniOlustur("3D Model Kayıt Testi");
        proje.IhtiyacProgrami = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.Ofis, kapasite: 30, katSayisi: 1);
        proje.PlanTaslagi = PlanTaslagiUretimServisi.Uret(proje.IhtiyacProgrami, proje.Id);

        var kaynakPlan = proje.PlanTaslagi;
        var kaynakKat = kaynakPlan.Katlar.First();
        var kaynakBlok = kaynakKat.Bloklar.First();
        kaynakPlan.Model3D = new Bim3DModel
        {
            Id = "model3d-kayit",
            Ad = "Kayıt Test Modeli",
            KaynakPlanId = kaynakPlan.Id,
            Katlar =
            [
                new Kat3DModel
                {
                    Id = "kat3d-kayit",
                    KaynakKatTaslagiId = kaynakKat.Id,
                    KatNo = kaynakKat.KatNo,
                    Ad = kaynakKat.Ad,
                    TabanKotuM = 0,
                    KatYuksekligiM = 3.2,
                    NetYukseklikM = 3,
                    Dosemeler =
                    [
                        new Doseme3D
                        {
                            Id = "doseme-kayit",
                            KaynakKatTaslagiId = kaynakKat.Id,
                            KatNo = kaynakKat.KatNo,
                            GenislikM = 10,
                            DerinlikM = 8,
                            KalinlikM = 0.2
                        }
                    ],
                    Mahaller =
                    [
                        new MahalHacmi3D
                        {
                            Id = "mahal3d-kayit",
                            KaynakMahalBloguId = kaynakBlok.Id,
                            Ad = kaynakBlok.Ad,
                            Tip = kaynakBlok.MahalTipi,
                            KatNo = kaynakKat.KatNo,
                            X = kaynakBlok.X,
                            Y = kaynakBlok.Y,
                            Z = 0,
                            GenislikM = 4,
                            DerinlikM = 5,
                            YukseklikM = 3
                        }
                    ],
                    Duvarlar =
                    [
                        new Duvar3D
                        {
                            Id = "duvar-kayit",
                            KaynakMahalBloguId = kaynakBlok.Id,
                            KatNo = kaynakKat.KatNo,
                            BaslangicX = 0,
                            BaslangicY = 0,
                            BitisX = 4,
                            BitisY = 0,
                            YukseklikM = 3,
                            KalinlikM = 0.2,
                            AciklikIdleri = ["aciklik-kayit"]
                        }
                    ],
                    Acikliklar =
                    [
                        new Aciklik3D
                        {
                            Id = "aciklik-kayit",
                            KaynakMahalBloguId = kaynakBlok.Id,
                            DuvarId = "duvar-kayit",
                            Tip = Aciklik3DTipi.Kapi,
                            GenislikM = 0.9,
                            YukseklikM = 2.1
                        }
                    ],
                    CekirdekHacimleri =
                    [
                        new CekirdekHacmi3D
                        {
                            Id = "cek3d-kayit",
                            KaynakCekirdekId = "cek-kayit",
                            Tip = CekirdekTipi.Asansor,
                            KatNo = kaynakKat.KatNo,
                            GenislikM = 2,
                            DerinlikM = 2,
                            YukseklikM = 3
                        }
                    ],
                    SaftHacimleri =
                    [
                        new SaftHacmi3D
                        {
                            Id = "saft3d-kayit",
                            KaynakSaftId = "saft-kayit",
                            TesisatTuru = SaftTesisatTuru.Havalandirma,
                            KatNo = kaynakKat.KatNo,
                            GenislikM = 1,
                            DerinlikM = 1,
                            YukseklikM = 3
                        }
                    ]
                }
            ]
        };

        MimariProjeServisi.Kaydet(proje, yol);
        var json = File.ReadAllText(yol);
        Assert(json.Contains("\"Model3D\"", StringComparison.Ordinal),
            "Kaydedilen dosya 3D model alanını içermeli.");
        Assert(json.Contains("\"Katlar\"", StringComparison.Ordinal),
            "Kaydedilen dosya 3D kat listesini içermeli.");
        Assert(json.Contains("\"Kapi\"", StringComparison.Ordinal),
            "Kaydedilen dosya açıklık tipini string enum olarak saklamalı.");

        var acilan = MimariProjeServisi.Ac(yol);
        var acilanModel = acilan.PlanTaslagi?.Model3D
            ?? throw new InvalidOperationException("Açılan planda 3D model korunmalı.");
        var acilanKat = acilanModel.KatBul(kaynakKat.KatNo)
            ?? throw new InvalidOperationException("Açılan 3D modelde kaynak kat bulunmalı.");
        var acilanMahal = acilanKat.MahalBul(kaynakBlok.Id)
            ?? throw new InvalidOperationException("Açılan 3D modelde kaynak mahal hacmi bulunmalı.");
        var acilanDuvar = acilanKat.Duvarlar.FirstOrDefault(d => d.Id == "duvar-kayit")
            ?? throw new InvalidOperationException("Açılan 3D modelde kaynak duvar bulunmalı.");

        Assert(acilanModel.KaynakPlanId == kaynakPlan.Id, "3D model kaynak plan Id'sini korumalı.");
        Assert(acilanModel.ToplamKatSayisi == 1, "3D model kat sayısını korumalı.");
        Assert(acilanKat.Dosemeler.Count == 1, "3D kat döşeme listesini korumalı.");
        Assert(acilanKat.Acikliklar.Single().Tip == Aciklik3DTipi.Kapi, "Açıklık tipi korunmalı.");
        Assert(acilanKat.CekirdekHacimleri.Single().Tip == CekirdekTipi.Asansor,
            "Çekirdek 3D tipi korunmalı.");
        Assert(acilanKat.SaftHacimleri.Single().TesisatTuru == SaftTesisatTuru.Havalandirma,
            "Şaft 3D tesisat türü korunmalı.");
        Assert(acilanDuvar.AciklikIdleri.Contains("aciklik-kayit"), "Duvar açıklık bağlantısı korunmalı.");
        AssertClose(acilanMahal.HacimM3, 60, "Açılan 3D mahal hacmi hesaplanmalı.");
        AssertClose(acilan.PlanTaslagi.Toplam3DMahalHacmiM3, 60,
            "Açılan plan 3D toplam mahal hacmini hesaplamalı.");
    }
    finally
    {
        if (File.Exists(yol))
            File.Delete(yol);
    }
}

static void Model3DUretimServisiPlanTaslagindan3DModelUretir()
{
    var zeminBlok = new MahalBlogu
    {
        Id = "blok-zemin-ofis",
        Ad = "Zemin Ofis",
        MahalTipi = MahalTipi.Ofis,
        KullanimSinifi = KullanimSinifi.AnaKullanim,
        KatNo = 1,
        X = 1,
        Y = 1,
        GenislikM = 4,
        DerinlikM = 5
    };

    var ustBlok = new MahalBlogu
    {
        Id = "blok-ust-toplanti",
        Ad = "Toplantı Odası",
        MahalTipi = MahalTipi.ToplantiOdasi,
        KullanimSinifi = KullanimSinifi.AnaKullanim,
        KatNo = 2,
        X = 2,
        Y = 1,
        GenislikM = 3,
        DerinlikM = 4
    };

    var cekirdek = new CekirdekElemani
    {
        Id = "cek-ana",
        Ad = "Ana Merdiven",
        Tip = CekirdekTipi.Merdiven,
        KatNo = 1,
        BaslangicKatNo = 1,
        BitisKatNo = 2,
        X = 7,
        Y = 1,
        GenislikM = 2,
        DerinlikM = 4,
        AksKodu = "A-1"
    };

    var saft = new SaftTaslagi
    {
        Id = "saft-ana",
        Ad = "Ana Şaft",
        TesisatTuru = SaftTesisatTuru.Havalandirma,
        KatNo = 1,
        BaslangicKatNo = 1,
        BitisKatNo = 2,
        X = 7,
        Y = 5.5,
        GenislikM = 1,
        DerinlikM = 1.2,
        AksKodu = "S-1"
    };

    var plan = new PlanTaslagi
    {
        Id = "plan-3d-uretim",
        Katlar =
        [
            new KatTaslagi
            {
                Id = "kat-zemin",
                KatNo = 1,
                Ad = "Zemin Kat",
                Sinir = new PlanSiniri { X = 0, Y = 0, GenislikM = 10, DerinlikM = 8 },
                Bloklar = [zeminBlok],
                Cekirdekler = [cekirdek],
                Saftlar = [saft]
            },
            new KatTaslagi
            {
                Id = "kat-ust",
                KatNo = 2,
                Ad = "1. Kat",
                Sinir = new PlanSiniri { X = 0, Y = 0, GenislikM = 8, DerinlikM = 6 },
                Bloklar = [ustBlok]
            }
        ]
    };

    var secenekler = new Model3DUretimSecenekleri
    {
        VarsayilanKatYuksekligiM = 3.2,
        VarsayilanNetYukseklikM = 3,
        VarsayilanDuvarKalinligiM = 0.2,
        VarsayilanDosemeKalinligiM = 0.2,
        VarsayilanTavanKalinligiM = 0.1
    };

    var model = Model3DUretimServisi.Uret(plan, secenekler);

    Assert(ReferenceEquals(plan.Model3D, model), "Üretim servisi varsayılan olarak modeli plan taslağına yazmalı.");
    Assert(model.Id == "model3d-plan-3d-uretim", "3D model Id'si kaynak plan Id'sinden türetilmeli.");
    Assert(model.KaynakPlanId == plan.Id, "3D model kaynak plan Id'sini taşımalı.");
    Assert(model.ToplamKatSayisi == 2, "3D model iki kat üretmeli.");
    Assert(model.ToplamMahalSayisi == 2, "3D model bloklardan iki mahal hacmi üretmeli.");
    Assert(model.ToplamDuvarSayisi == 8, "Her dikdörtgen mahal için dört duvar üretilmeli.");
    Assert(model.ToplamCekirdekHacmiSayisi == 2, "İki kata yayılan çekirdek her katta hacim üretmeli.");
    Assert(model.ToplamSaftHacmiSayisi == 2, "İki kata yayılan şaft her katta hacim üretmeli.");

    var zemin = model.KatBul(1)
        ?? throw new InvalidOperationException("Zemin kat 3D modeli üretilmeli.");
    var ust = model.KatBul(2)
        ?? throw new InvalidOperationException("Üst kat 3D modeli üretilmeli.");

    AssertClose(zemin.TabanKotuM, 0, "Zemin kat taban kotu 0 olmalı.");
    AssertClose(ust.TabanKotuM, 3.2, "Üst kat taban kotu kat yüksekliğiyle hesaplanmalı.");
    AssertClose(zemin.UstKotuM, 3.2, "Zemin kat üst kotu hesaplanmalı.");
    AssertClose(ust.UstKotuM, 6.4, "Üst kat üst kotu hesaplanmalı.");

    var zeminMahal = zemin.MahalBul(zeminBlok.Id)
        ?? throw new InvalidOperationException("Zemin bloktan 3D mahal hacmi üretilmeli.");
    var ustMahal = ust.MahalBul(ustBlok.Id)
        ?? throw new InvalidOperationException("Üst bloktan 3D mahal hacmi üretilmeli.");

    Assert(zeminMahal.Id == $"mahal3d-{zeminBlok.Id}", "Mahal hacmi Id'si kaynak blok Id'sinden türetilmeli.");
    AssertClose(zeminMahal.AlanM2, 20, "Zemin mahal alanı blok boyutundan hesaplanmalı.");
    AssertClose(zeminMahal.HacimM3, 60, "Zemin mahal hacmi net yükseklikten hesaplanmalı.");
    AssertClose(ustMahal.HacimM3, 36, "Üst mahal hacmi net yükseklikten hesaplanmalı.");
    AssertClose(model.ToplamMahalHacmiM3, 96, "3D model toplam mahal hacmini hesaplamalı.");

    Assert(zemin.Dosemeler.Count == 1, "Zemin katta döşeme üretilmeli.");
    Assert(zemin.Tavanlar.Count == 1, "Zemin katta tavan üretilmeli.");
    AssertClose(zemin.Dosemeler.Single().AlanM2, 80, "Döşeme alanı kat sınırından hesaplanmalı.");
    AssertClose(zemin.Tavanlar.Single().Z, 3, "Tavan Z kotu net yüksekliğe göre yazılmalı.");
    AssertClose(model.ToplamDosemeAlaniM2, 128, "Toplam döşeme alanı kat sınırlarından hesaplanmalı.");

    Assert(zemin.Duvarlar.Count(d => d.KaynakMahalBloguId == zeminBlok.Id) == 4,
        "Zemin mahal için dört duvar segmenti üretilmeli.");
    AssertClose(zemin.Duvarlar.Where(d => d.KaynakMahalBloguId == zeminBlok.Id).Sum(d => d.AlanM2),
        57.6,
        "Zemin mahal duvar alanı çevre ve kat yüksekliğinden hesaplanmalı.");
    AssertClose(model.ToplamDuvarAlaniM2, 102.4, "Toplam duvar alanı iki mahalden hesaplanmalı.");

    var zeminCekirdek = zemin.CekirdekHacimleri.Single();
    var ustCekirdek = ust.CekirdekHacimleri.Single();
    Assert(zeminCekirdek.KaynakCekirdekId == cekirdek.Id, "Çekirdek hacmi kaynak çekirdek Id'sini taşımalı.");
    Assert(ustCekirdek.Id == $"cekirdek3d-{cekirdek.Id}-kat2", "Üst kat çekirdek hacmi kat bazlı Id taşımalı.");
    AssertClose(zeminCekirdek.HacimM3, 25.6, "Çekirdek hacmi kat yüksekliğiyle hesaplanmalı.");
    AssertClose(ustCekirdek.Z, 3.2, "Üst kat çekirdek hacmi üst kat kotuna yerleşmeli.");

    var ustSaft = ust.SaftHacimleri.Single();
    Assert(ustSaft.KaynakSaftId == saft.Id, "Şaft hacmi kaynak şaft Id'sini taşımalı.");
    AssertClose(ustSaft.HacimM3, 3.84, "Şaft hacmi kat yüksekliğiyle hesaplanmalı.");

    var bosModel = Model3DUretimServisi.Uret(null);
    Assert(bosModel.Katlar.Count == 0, "Plan yoksa boş 3D model güvenli oluşmalı.");
}

static void Model3DIhracatSozlesmesiGlbVarsayilaniylaOlusur()
{
    var secenekler = new Model3DIhracatSecenekleri();

    Assert(secenekler.Format == Model3DIhracatFormati.Glb, "M35 ilk dışa aktarım formatı GLB olmalı.");
    Assert(secenekler.Uzanti == ".glb", "GLB için dosya uzantısı .glb olmalı.");
    Assert(secenekler.MimeType == "model/gltf-binary", "GLB MIME türü korunmalı.");
    Assert(secenekler.Birim == "m", "M35 dışa aktarım birimi metre olmalı.");
    Assert(secenekler.MahalleriAyriMeshOlarakYaz, "Mahaller ayrı mesh olarak yazılmalı.");
    Assert(secenekler.CekirdekVeSaftlariAyriMeshOlarakYaz, "Çekirdek ve şaftlar ayrı mesh olarak yazılmalı.");
    Assert(secenekler.ExtrasMetadataYaz, "Kimlik/kat/mahal metadata'sı extras alanına yazılmalı.");
    Assert(secenekler.DosyaAdiOner("Mimari/Test") == "Mimari-Test.glb",
        "Önerilen dosya adı geçersiz karakterleri temizleyip GLB uzantısı eklemeli.");

    secenekler.Format = Model3DIhracatFormati.Obj;
    Assert(secenekler.Uzanti == ".obj", "OBJ yedek format uzantısı .obj olmalı.");
    Assert(secenekler.MimeType == "model/obj", "OBJ MIME türü korunmalı.");
}

static void Model3DGeometriKapanisRegresyonuAlanVeHacimleriKorur()
{
    var yol = Path.Combine(Path.GetTempPath(), $"e4mimari-3d-regresyon-{Guid.NewGuid():N}.e4mimari.json");
    try
    {
        var proje = MimariProjeServisi.YeniOlustur("M35 Kapanış Regresyonu");
        proje.IhtiyacProgrami = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.Ofis, kapasite: 80, katSayisi: 3);
        proje.PlanTaslagi = PlanTaslagiUretimServisi.Uret(proje.IhtiyacProgrami, proje.Id);

        var plan = proje.PlanTaslagi;
        var secenekler = new Model3DUretimSecenekleri
        {
            VarsayilanKatYuksekligiM = 3.2,
            VarsayilanNetYukseklikM = 3.0,
            VarsayilanDuvarKalinligiM = 0.2,
            VarsayilanDosemeKalinligiM = 0.2,
            VarsayilanTavanKalinligiM = 0.1
        };

        var model = Model3DUretimServisi.Uret(plan, secenekler);
        var yerlesmisBlokSayisi = plan.Katlar.Sum(k => k.Bloklar.Count(b => b.YerlesmisMi));
        var toplamYerlesimAlani = plan.Katlar.Sum(k => k.BlokYerlesimAlanM2);
        var toplamDosemeAlani = plan.Katlar.Sum(k => Model3DGeometriServisi.KatSiniriBul(k).AlanM2);
        var beklenenHacim = toplamYerlesimAlani * secenekler.VarsayilanNetYukseklikM;

        Assert(ReferenceEquals(plan.Model3D, model), "Kapanış regresyonunda üretilen 3D model plan taslağına yazılmalı.");
        Assert(plan.ToplamBlokSayisi == yerlesmisBlokSayisi, "Üretilen planın tüm blokları yerleşmiş olmalı.");
        Assert(model.ToplamKatSayisi == plan.KatSayisi, "3D model kat sayısı planla tutarlı olmalı.");
        Assert(model.ToplamMahalSayisi == yerlesmisBlokSayisi, "3D model yerleşmiş blok sayısı kadar mahal üretmeli.");
        Assert(model.ToplamDuvarSayisi == yerlesmisBlokSayisi * 4, "Her yerleşmiş mahal için dört duvar üretilmeli.");
        AssertClose(model.ToplamMahalHacmiM3, beklenenHacim, "3D model toplam hacmi 2D alan ve net yükseklikten hesaplanmalı.");
        AssertClose(model.ToplamDosemeAlaniM2, toplamDosemeAlani, "3D döşeme alanı kat sınırları toplamıyla tutarlı olmalı.");
        AssertClose(plan.Toplam3DMahalHacmiM3, beklenenHacim, "Plan 3D toplam hacmini yansıtmalı.");
        AssertClose(plan.Toplam3DDosemeAlaniM2, toplamDosemeAlani, "Plan 3D toplam döşeme alanını yansıtmalı.");

        foreach (var kat in plan.Katlar)
        {
            var kat3D = model.KatBul(kat.KatNo)
                ?? throw new InvalidOperationException($"3D modelde Kat {kat.KatNo} bulunmalı.");
            var katYerlesmisBlokSayisi = kat.Bloklar.Count(b => b.YerlesmisMi);
            var katSiniri = Model3DGeometriServisi.KatSiniriBul(kat);

            Assert(kat3D.Mahaller.Count == katYerlesmisBlokSayisi, $"Kat {kat.KatNo} mahal sayısı 2D bloklarla tutarlı olmalı.");
            Assert(kat3D.Duvarlar.Count == katYerlesmisBlokSayisi * 4, $"Kat {kat.KatNo} duvar sayısı 2D bloklarla tutarlı olmalı.");
            Assert(kat3D.Dosemeler.Count == (katSiniri.AlanM2 > 0 ? 1 : 0), $"Kat {kat.KatNo} döşeme üretimi kat sınırıyla tutarlı olmalı.");
            Assert(kat3D.Tavanlar.Count == kat3D.Dosemeler.Count, $"Kat {kat.KatNo} tavan üretimi döşeme üretimiyle tutarlı olmalı.");
            AssertClose(kat3D.TabanKotuM, (kat.KatNo - 1) * secenekler.VarsayilanKatYuksekligiM, $"Kat {kat.KatNo} taban kotu kat yüksekliğiyle hesaplanmalı.");
            AssertClose(kat3D.ToplamMahalAlaniM2, kat.BlokYerlesimAlanM2, $"Kat {kat.KatNo} 3D mahal alanı 2D blok alanını korumalı.");
            AssertClose(kat3D.ToplamMahalHacmiM3, kat.BlokYerlesimAlanM2 * secenekler.VarsayilanNetYukseklikM, $"Kat {kat.KatNo} 3D mahal hacmi alan ve net yükseklikten hesaplanmalı.");
            AssertClose(kat3D.ToplamDosemeAlaniM2, katSiniri.AlanM2, $"Kat {kat.KatNo} 3D döşeme alanı kat sınırıyla tutarlı olmalı.");
        }

        MimariProjeServisi.Kaydet(proje, yol);
        var json = File.ReadAllText(yol);
        Assert(json.Contains("\"Model3D\"", StringComparison.Ordinal),
            "Kapanış regresyon dosyası 3D model alanını içermeli.");

        var acilan = MimariProjeServisi.Ac(yol);
        var acilanModel = acilan.PlanTaslagi?.Model3D
            ?? throw new InvalidOperationException("Kapanış regresyonunda açılan planda 3D model korunmalı.");

        Assert(acilanModel.ToplamKatSayisi == model.ToplamKatSayisi, "Açılan 3D model kat sayısını korumalı.");
        Assert(acilanModel.ToplamMahalSayisi == model.ToplamMahalSayisi, "Açılan 3D model mahal sayısını korumalı.");
        AssertClose(acilanModel.ToplamMahalHacmiM3, model.ToplamMahalHacmiM3, "Açılan 3D model toplam hacmi korumalı.");
        AssertClose(acilanModel.ToplamDosemeAlaniM2, model.ToplamDosemeAlaniM2, "Açılan 3D model toplam döşeme alanını korumalı.");
        AssertClose(acilan.PlanTaslagi.Toplam3DMahalHacmiM3, beklenenHacim,
            "Açılan plan 3D toplam hacmini hesaplamalı.");
    }
    finally
    {
        if (File.Exists(yol))
            File.Delete(yol);
    }
}

static void MalzemeModelleriGuvenliVarsayilanlarlaOlusur()
{
    var performans = new MalzemePerformansi
    {
        YanginTepkiSinifi = "A2-s1,d0",
        YanginDayanimDakika = 30,
        AkustikNot = "Yutucu tavan paneli",
        SesAzaltimRw = 42,
        YutuculukAw = 0.65,
        IsiIletkenlikWmK = 0.04,
        UDegeriWm2K = 1.1,
        SuyaDayanikli = true,
        IslakHacimUygun = true,
        KaymazlikSinifi = "R10"
    };

    Assert(performans.YanginBilgisiVar, "Yangın tepki/dayanım verisi olan malzeme yangın bilgisi taşımalı.");
    Assert(performans.AkustikBilgisiVar, "Akustik not/Rw/aw verisi olan malzeme akustik bilgisi taşımalı.");
    Assert(performans.IsiBilgisiVar, "Isı iletkenlik veya U değeri olan malzeme ısı bilgisi taşımalı.");
    Assert(performans.KaymazlikBilgisiVar, "Kaymazlık sınıfı olan malzeme kaymazlık bilgisi taşımalı.");

    var metraj = new MetrajBilgisi
    {
        VarsayilanBirim = MetrajBirimi.Metrekare,
        BirimAgirlikKg = 12.5,
        YogunlukKgM3 = 800
    };

    Assert(metraj.MetrajlanabilirMi, "Metraj birimi tanımlı malzeme metrajlanabilir olmalı.");
    AssertClose(metraj.KullanilanBirimAgirlikKg, 12.5, "Birim ağırlık pozitif korunmalı.");
    AssertClose(metraj.KullanilanYogunlukKgM3, 800, "Yoğunluk pozitif korunmalı.");

    var malzeme = new MimariMalzeme
    {
        Kod = "TEST-SERAMIK",
        Ad = "Test Seramik",
        Kategori = MalzemeKategorisi.ZeminKaplamasi,
        TeknikSiniflar = [MalzemeTeknikSinifi.IslakHacim, MalzemeTeknikSinifi.KaymazYuzey],
        UygunMahalTipleri = [MahalTipi.Banyo, MahalTipi.Wc],
        UygunKullanimSiniflari = [KullanimSinifi.IslakHacim],
        Performans = performans,
        Metraj = metraj
    };

    Assert(malzeme.KodluMu, "Kod dolu malzeme kodlu sayılmalı.");
    Assert(malzeme.AdliMi, "Ad dolu malzeme adlı sayılmalı.");
    Assert(malzeme.IslakHacimUygun, "Suya dayanıklı/ıslak hacim uygun malzeme ıslak hacim uygun sayılmalı.");
    Assert(malzeme.MetrajlanabilirMi, "Metraj bilgisi olan malzeme metrajlanabilir sayılmalı.");
    Assert(malzeme.TeknikSinifVar(MalzemeTeknikSinifi.KaymazYuzey), "Teknik sınıf listesi sorgulanabilmeli.");
    Assert(malzeme.MahalIcinUygunMu(MahalTipi.Banyo, KullanimSinifi.IslakHacim),
        "Uygun mahal ve kullanım sınıfı eşleşmeli.");
    Assert(!malzeme.MahalIcinUygunMu(MahalTipi.AcikOfis, KullanimSinifi.AnaKullanim),
        "Uygun olmayan mahal/kullanım sınıfı eşleşmemeli.");

    var genelMalzeme = new MimariMalzeme { Kod = "GENEL", Ad = "Genel Malzeme" };
    Assert(genelMalzeme.MahalIcinUygunMu(MahalTipi.AcikOfis, KullanimSinifi.AnaKullanim),
        "Boş uygunluk listeleri genel kullanıma izin vermeli.");
}

static void VarsayilanMimariMalzemeKataloguGecerlidir()
{
    var malzemeler = VarsayilanMimariMalzemeler.Tum;
    Assert(malzemeler.Count >= 8, "Varsayılan mimari malzeme kataloğu başlangıç için yeterli çeşit içermeli.");

    var tekrarliKodlar = malzemeler
        .GroupBy(m => m.Kod.Trim(), StringComparer.OrdinalIgnoreCase)
        .Where(g => g.Count() > 1)
        .Select(g => g.Key)
        .ToList();
    Assert(tekrarliKodlar.Count == 0, $"Tekrarlı malzeme kodu var: {string.Join(", ", tekrarliKodlar)}");

    foreach (var malzeme in malzemeler)
    {
        Assert(!string.IsNullOrWhiteSpace(malzeme.Id), $"{malzeme.Kod} malzeme Id boş olmamalı.");
        Assert(!string.IsNullOrWhiteSpace(malzeme.Kod), "Malzeme kodu boş olmamalı.");
        Assert(!string.IsNullOrWhiteSpace(malzeme.Ad), $"{malzeme.Kod} malzeme adı boş olmamalı.");
        Assert(malzeme.Kategori != MalzemeKategorisi.Tanimsiz, $"{malzeme.Kod} kategorisi tanımsız olmamalı.");
        Assert(malzeme.TeknikSiniflar is not null, $"{malzeme.Kod} teknik sınıf listesi null olmamalı.");
        Assert(malzeme.UygunMahalTipleri is not null, $"{malzeme.Kod} uygun mahal listesi null olmamalı.");
        Assert(malzeme.UygunKullanimSiniflari is not null, $"{malzeme.Kod} uygun kullanım listesi null olmamalı.");
        var performans = malzeme.Performans
            ?? throw new InvalidOperationException($"{malzeme.Kod} performans nesnesi null olmamalı.");
        var metraj = malzeme.Metraj
            ?? throw new InvalidOperationException($"{malzeme.Kod} metraj nesnesi null olmamalı.");

        Assert(performans.YanginDayanimDakika >= 0, $"{malzeme.Kod} yangın dayanımı negatif olmamalı.");
        Assert(performans.SesAzaltimRw >= 0, $"{malzeme.Kod} Rw negatif olmamalı.");
        Assert(performans.YutuculukAw >= 0 && performans.YutuculukAw <= 1,
            $"{malzeme.Kod} yutuculuk aw değeri 0-1 aralığında olmalı.");
        Assert(performans.IsiIletkenlikWmK >= 0, $"{malzeme.Kod} ısı iletkenliği negatif olmamalı.");
        Assert(performans.UDegeriWm2K >= 0, $"{malzeme.Kod} U değeri negatif olmamalı.");
        Assert(metraj.VarsayilanBirim != MetrajBirimi.Tanimsiz,
            $"{malzeme.Kod} varsayılan metraj birimi tanımlı olmalı.");
    }

    Assert(malzemeler.Any(m =>
            m.Kategori == MalzemeKategorisi.ZeminKaplamasi &&
            m.IslakHacimUygun &&
            m.Performans.KaymazlikBilgisiVar),
        "Katalogda ıslak hacim ve kaymazlık bilgisi olan zemin malzemesi bulunmalı.");
    Assert(malzemeler.Any(m =>
            m.Kategori == MalzemeKategorisi.TavanKaplamasi &&
            m.Performans.AkustikBilgisiVar),
        "Katalogda akustik bilgisi olan tavan malzemesi bulunmalı.");
    Assert(malzemeler.Any(m =>
            m.Kategori == MalzemeKategorisi.Kapi &&
            m.Performans.YanginDayanimDakika >= 60),
        "Katalogda yangın dayanımı olan kapı malzemesi bulunmalı.");
    Assert(VarsayilanMimariMalzemeler.KategoriyeGore(MalzemeKategorisi.Yalitim)
            .Any(m => m.TeknikSinifVar(MalzemeTeknikSinifi.IsilYalitim)),
        "Kategori filtresi yalıtım malzemelerini döndürmeli.");

    var kopya = VarsayilanMimariMalzemeler.KodlaBul("ZEM-SER-R10")
        ?? throw new InvalidOperationException("Varsayılan seramik malzeme bulunmalı.");
    kopya.Ad = "Değiştirildi";
    var yenidenOkunan = VarsayilanMimariMalzemeler.KodlaBul("ZEM-SER-R10")
        ?? throw new InvalidOperationException("Varsayılan seramik malzeme yeniden bulunmalı.");
    Assert(yenidenOkunan.Ad != "Değiştirildi", "Varsayılan katalog dışarıya kopya malzeme vermeli.");
}

static void MalzemelerE4MimariDosyasindaKorunur()
{
    var yol = Path.Combine(Path.GetTempPath(), $"e4mimari-malzeme-test-{Guid.NewGuid():N}.e4mimari.json");
    try
    {
        var proje = MimariProjeServisi.YeniOlustur("M36 Malzeme Kayıt Testi");
        var seramik = VarsayilanMimariMalzemeler.KodlaBul("ZEM-SER-R10")
            ?? throw new InvalidOperationException("Varsayılan seramik malzeme bulunmalı.");
        var akustikTavan = VarsayilanMimariMalzemeler.KodlaBul("TAV-AKUSTIK-PANEL")
            ?? throw new InvalidOperationException("Varsayılan akustik tavan malzemesi bulunmalı.");

        proje.Malzemeler = [seramik, akustikTavan];

        MimariProjeServisi.Kaydet(proje, yol);
        var json = File.ReadAllText(yol);
        Assert(json.Contains("\"Malzemeler\"", StringComparison.Ordinal),
            "Kaydedilen dosya malzeme listesini içermeli.");
        Assert(json.Contains("\"ZeminKaplamasi\"", StringComparison.Ordinal),
            "Kaydedilen dosya malzeme kategorisini string enum olarak saklamalı.");
        Assert(json.Contains("\"IslakHacimUygun\": true", StringComparison.Ordinal),
            "Kaydedilen dosya ıslak hacim uygunluğunu saklamalı.");
        Assert(json.Contains("\"KaymazYuzey\"", StringComparison.Ordinal),
            "Kaydedilen dosya teknik sınıf listesini saklamalı.");

        var acilan = MimariProjeServisi.Ac(yol);
        Assert(acilan.Malzemeler.Count == 2, "Açılan proje malzeme listesini korumalı.");

        var acilanSeramik = acilan.Malzemeler.FirstOrDefault(m => m.Kod == "ZEM-SER-R10")
            ?? throw new InvalidOperationException("Açılan projede seramik malzeme bulunmalı.");
        Assert(acilanSeramik.Kategori == MalzemeKategorisi.ZeminKaplamasi, "Malzeme kategorisi korunmalı.");
        Assert(acilanSeramik.TeknikSinifVar(MalzemeTeknikSinifi.IslakHacim), "Teknik sınıf korunmalı.");
        Assert(acilanSeramik.UygunMahalTipleri.Contains(MahalTipi.Banyo), "Uygun mahal tipleri korunmalı.");
        Assert(acilanSeramik.Performans.IslakHacimUygun, "Islak hacim uygunluğu korunmalı.");
        Assert(acilanSeramik.Performans.KaymazlikBilgisiVar, "Kaymazlık bilgisi korunmalı.");
        Assert(acilanSeramik.Metraj.VarsayilanBirim == MetrajBirimi.Metrekare, "Metraj birimi korunmalı.");

        var acilanTavan = acilan.Malzemeler.FirstOrDefault(m => m.Kod == "TAV-AKUSTIK-PANEL")
            ?? throw new InvalidOperationException("Açılan projede akustik tavan malzemesi bulunmalı.");
        Assert(acilanTavan.Performans.AkustikBilgisiVar, "Akustik performans bilgisi korunmalı.");
        AssertClose(acilanTavan.Performans.YutuculukAw, 0.75, "Akustik tavan yutuculuk değeri korunmalı.");
    }
    finally
    {
        if (File.Exists(yol))
            File.Delete(yol);
    }
}

static void YapiElemaniTipleriKatmanVePerformansOzetiUretir()
{
    var alci = new MimariMalzeme
    {
        Id = "mat-alci",
        Kod = "MAT-ALCI",
        Ad = "Test Alçı",
        Performans = new MalzemePerformansi
        {
            YanginTepkiSinifi = "A2-s1,d0",
            YanginDayanimDakika = 30,
            SesAzaltimRw = 35
        },
        Metraj = new MetrajBilgisi
        {
            VarsayilanBirim = MetrajBirimi.Metrekare,
            BirimAgirlikKg = 12
        }
    };
    var yalim = new MimariMalzeme
    {
        Id = "mat-yun",
        Kod = "MAT-YUN",
        Ad = "Test Yalıtım",
        Performans = new MalzemePerformansi
        {
            YanginTepkiSinifi = "A1",
            YutuculukAw = 0.25,
            UDegeriWm2K = 0.45
        },
        Metraj = new MetrajBilgisi
        {
            VarsayilanBirim = MetrajBirimi.Metrekup,
            YogunlukKgM3 = 50
        }
    };

    var tip = new YapiElemaniTipi
    {
        Id = "tip-test-duvar",
        Kod = "TEST-DUV",
        Ad = "Test Duvar Tipi",
        Sinif = YapiElemaniSinifi.Duvar,
        Katmanlar =
        [
            new YapiElemaniKatmani { SiraNo = 1, Ad = "İç alçı", MalzemeId = alci.Id, KalinlikM = 0.0125, Fonksiyon = MalzemeKatmanFonksiyonu.Siva },
            new YapiElemaniKatmani { SiraNo = 2, Ad = "Yalıtım", MalzemeId = yalim.Id, KalinlikM = 0.05, Fonksiyon = MalzemeKatmanFonksiyonu.Yalitim },
            new YapiElemaniKatmani { SiraNo = 3, Ad = "Dış alçı", MalzemeId = alci.Id, KalinlikM = 0.0125, Fonksiyon = MalzemeKatmanFonksiyonu.Siva }
        ]
    };

    Assert(tip.KatmanliMi, "Katmanı olan yapı elemanı tipi katmanlı sayılmalı.");
    Assert(tip.KalinlikliMi, "Pozitif katman kalınlığı olan tip kalınlıklı sayılmalı.");
    Assert(tip.KatmanSayisi == 3, "Tip katman sayısını hesaplamalı.");
    AssertClose(tip.ToplamKalinlikM, 0.075, "Tip toplam katman kalınlığını hesaplamalı.");
    Assert(tip.MalzemeIdleri.Count == 2, "Tip tekrar eden malzemeleri tekil Id listesine indirmeli.");
    Assert(tip.HedefTuruIleUyumluMu(MalzemeAtamaHedefTuru.Duvar), "Duvar tipi duvar hedefiyle uyumlu olmalı.");
    Assert(!tip.HedefTuruIleUyumluMu(MalzemeAtamaHedefTuru.Tavan), "Duvar tipi tavan hedefiyle uyumlu olmamalı.");

    var ozet = tip.PerformansOzetiHesapla([alci, yalim]);
    AssertClose(ozet.ToplamKalinlikM, 0.075, "Performans özeti toplam kalınlığı taşımalı.");
    AssertClose(ozet.TahminiAgirlikKgM2, 26.5, "Performans özeti katman ağırlığını hesaplamalı.");
    Assert(ozet.YanginBilgisiVar, "Performans özeti yangın bilgisini taşımalı.");
    Assert(ozet.AkustikBilgisiVar, "Performans özeti akustik bilgisini taşımalı.");
    Assert(ozet.IsiBilgisiVar, "Performans özeti ısı bilgisini taşımalı.");
    Assert(ozet.MalzemeSayisi == 3, "Performans özeti katman malzeme eşleşmelerini saymalı.");
    Assert(ozet.YanginDayanimDakika == 30, "Performans özeti yangın dayanımını hesaplamalı.");
    AssertClose(ozet.AkustikRw, 35, "Performans özeti Rw değerini hesaplamalı.");
    AssertClose(ozet.YutuculukAw, 0.25, "Performans özeti yutuculuk değerini hesaplamalı.");
    AssertClose(ozet.UDegeriWm2K, 0.45, "Performans özeti U değerini hesaplamalı.");

    var atama = new MalzemeAtamasi
    {
        KaynakElemanTuru = MalzemeAtamaHedefTuru.Duvar,
        KaynakElemanId = "duvar3d-test",
        YapiElemaniTipiId = tip.Id,
        MalzemeIdleri = [alci.Id, yalim.Id]
    };
    Assert(atama.GecerliMi, "Kaynak eleman ve tip bağlantısı olan atama geçerli sayılmalı.");
    Assert(atama.MalzemeBaglantisiVar, "Malzeme Id listesi olan atama malzeme bağlantısı taşımalı.");
}

static void VarsayilanYapiElemaniTipleriM35SozlesmesiyleUyumludur()
{
    var tipler = VarsayilanYapiElemaniTipleri.Tum;
    var malzemeler = VarsayilanMimariMalzemeler.Tum;
    Assert(tipler.Count >= 5, "Varsayılan yapı elemanı tipi kataloğu temel tipleri içermeli.");

    var tekrarliKodlar = tipler
        .GroupBy(t => t.Kod.Trim(), StringComparer.OrdinalIgnoreCase)
        .Where(g => g.Count() > 1)
        .Select(g => g.Key)
        .ToList();
    Assert(tekrarliKodlar.Count == 0, $"Tekrarlı yapı elemanı tipi kodu var: {string.Join(", ", tekrarliKodlar)}");

    var tekrarliIdler = tipler
        .GroupBy(t => t.Id.Trim(), StringComparer.OrdinalIgnoreCase)
        .Where(g => g.Count() > 1)
        .Select(g => g.Key)
        .ToList();
    Assert(tekrarliIdler.Count == 0, $"Tekrarlı yapı elemanı tipi Id'si var: {string.Join(", ", tekrarliIdler)}");

    foreach (var tip in tipler)
    {
        Assert(!string.IsNullOrWhiteSpace(tip.Id), $"{tip.Kod} tip Id boş olmamalı.");
        Assert(!string.IsNullOrWhiteSpace(tip.Kod), "Tip kodu boş olmamalı.");
        Assert(!string.IsNullOrWhiteSpace(tip.Ad), $"{tip.Kod} tip adı boş olmamalı.");
        Assert(tip.Sinif != YapiElemaniSinifi.Tanimsiz, $"{tip.Kod} sınıfı tanımsız olmamalı.");
        Assert(tip.Katmanlar.Count > 0, $"{tip.Kod} en az bir katman içermeli.");
        Assert(tip.ToplamKalinlikM > 0, $"{tip.Kod} toplam kalınlığı pozitif olmalı.");
        Assert(tip.VarsayilanMetrajBirimi != MetrajBirimi.Tanimsiz, $"{tip.Kod} metraj birimi tanımlı olmalı.");

        foreach (var katman in tip.Katmanlar)
        {
            Assert(katman.SiraNo > 0, $"{tip.Kod} katman sıra no pozitif olmalı.");
            Assert(!string.IsNullOrWhiteSpace(katman.Ad), $"{tip.Kod} katman adı boş olmamalı.");
            Assert(!string.IsNullOrWhiteSpace(katman.MalzemeId), $"{tip.Kod} katman malzeme Id boş olmamalı.");
            Assert(katman.Fonksiyon != MalzemeKatmanFonksiyonu.Tanimsiz, $"{tip.Kod} katman fonksiyonu tanımsız olmamalı.");
            Assert(katman.KalinlikM >= 0, $"{tip.Kod} katman kalınlığı negatif olmamalı.");
        }

        var ozet = tip.PerformansOzetiHesapla(malzemeler);
        AssertClose(ozet.ToplamKalinlikM, tip.ToplamKalinlikM, $"{tip.Kod} performans özeti kalınlığı tip ile tutarlı olmalı.");
        Assert(ozet.MalzemeSayisi == tip.KatmanSayisi, $"{tip.Kod} tüm katman malzemeleri katalogda bulunmalı.");
    }

    var secenekler = new Model3DUretimSecenekleri();
    Assert(VarsayilanYapiElemaniTipleri.IdIleBul(secenekler.VarsayilanDuvarTipiId)?.Sinif == YapiElemaniSinifi.Duvar,
        "M35 varsayılan duvar tipi M36 katalogunda bulunmalı.");
    Assert(VarsayilanYapiElemaniTipleri.IdIleBul(secenekler.VarsayilanDosemeTipiId)?.Sinif == YapiElemaniSinifi.Doseme,
        "M35 varsayılan döşeme tipi M36 katalogunda bulunmalı.");
    Assert(VarsayilanYapiElemaniTipleri.IdIleBul(secenekler.VarsayilanTavanTipiId)?.Sinif == YapiElemaniSinifi.Tavan,
        "M35 varsayılan tavan tipi M36 katalogunda bulunmalı.");
    Assert(VarsayilanYapiElemaniTipleri.SinifaGore(YapiElemaniSinifi.Kapi).Any(),
        "Katalogda kapı tipi bulunmalı.");
    Assert(VarsayilanYapiElemaniTipleri.SinifaGore(YapiElemaniSinifi.Pencere).Any(),
        "Katalogda pencere tipi bulunmalı.");

    var tavan = VarsayilanYapiElemaniTipleri.IdIleBul(VarsayilanYapiElemaniTipleri.VarsayilanTavanTipiId)
        ?? throw new InvalidOperationException("Varsayılan tavan tipi bulunmalı.");
    Assert(tavan.PerformansOzetiHesapla(malzemeler).AkustikBilgisiVar,
        "Varsayılan tavan tipi akustik bilgi üretmeli.");

    var kapi = VarsayilanYapiElemaniTipleri.IdIleBul(VarsayilanYapiElemaniTipleri.VarsayilanKapiTipiId)
        ?? throw new InvalidOperationException("Varsayılan kapı tipi bulunmalı.");
    Assert(kapi.PerformansOzetiHesapla(malzemeler).YanginBilgisiVar,
        "Varsayılan kapı tipi yangın bilgi üretmeli.");

    var pencere = VarsayilanYapiElemaniTipleri.IdIleBul(VarsayilanYapiElemaniTipleri.VarsayilanPencereTipiId)
        ?? throw new InvalidOperationException("Varsayılan pencere tipi bulunmalı.");
    Assert(pencere.PerformansOzetiHesapla(malzemeler).IsiBilgisiVar,
        "Varsayılan pencere tipi ısı bilgi üretmeli.");
}

static void YapiElemaniTipleriVeAtamalariE4MimariDosyasindaKorunur()
{
    var yol = Path.Combine(Path.GetTempPath(), $"e4mimari-yapi-elemani-test-{Guid.NewGuid():N}.e4mimari.json");
    try
    {
        var proje = MimariProjeServisi.YeniOlustur("M36 Yapı Elemanı Kayıt Testi");
        proje.IhtiyacProgrami = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.Ofis, kapasite: 20, katSayisi: 1);
        proje.PlanTaslagi = PlanTaslagiUretimServisi.Uret(proje.IhtiyacProgrami, proje.Id);
        Model3DUretimServisi.Uret(proje.PlanTaslagi);

        var kat3D = proje.PlanTaslagi.Model3D?.Katlar.FirstOrDefault()
            ?? throw new InvalidOperationException("Test için 3D kat modeli üretilmeli.");
        var duvar = kat3D.Duvarlar.FirstOrDefault()
            ?? throw new InvalidOperationException("Test için 3D duvar üretilmeli.");
        var doseme = kat3D.Dosemeler.FirstOrDefault()
            ?? throw new InvalidOperationException("Test için 3D döşeme üretilmeli.");
        var tavan = kat3D.Tavanlar.FirstOrDefault()
            ?? throw new InvalidOperationException("Test için 3D tavan üretilmeli.");

        var kapiAcikligi = new Aciklik3D
        {
            Id = "aciklik3d-test-kapi",
            KaynakMahalBloguId = duvar.KaynakMahalBloguId,
            DuvarId = duvar.Id,
            Tip = Aciklik3DTipi.Kapi,
            KatNo = duvar.KatNo,
            GenislikM = 0.9,
            YukseklikM = 2.1
        };
        kat3D.Acikliklar.Add(kapiAcikligi);
        duvar.AciklikIdleri.Add(kapiAcikligi.Id);

        proje.Malzemeler = VarsayilanMimariMalzemeler.Tum.ToList();
        proje.YapiElemaniTipleri = VarsayilanYapiElemaniTipleri.Tum.ToList();
        proje.MalzemeAtamalari =
        [
            new MalzemeAtamasi
            {
                KaynakElemanTuru = MalzemeAtamaHedefTuru.Duvar,
                KaynakElemanId = duvar.Id,
                KatNo = duvar.KatNo,
                YapiElemaniTipiId = VarsayilanYapiElemaniTipleri.VarsayilanDuvarTipiId
            },
            new MalzemeAtamasi
            {
                KaynakElemanTuru = MalzemeAtamaHedefTuru.Doseme,
                KaynakElemanId = doseme.Id,
                KatNo = doseme.KatNo,
                YapiElemaniTipiId = VarsayilanYapiElemaniTipleri.VarsayilanDosemeTipiId
            },
            new MalzemeAtamasi
            {
                KaynakElemanTuru = MalzemeAtamaHedefTuru.Tavan,
                KaynakElemanId = tavan.Id,
                KatNo = tavan.KatNo,
                YapiElemaniTipiId = VarsayilanYapiElemaniTipleri.VarsayilanTavanTipiId
            },
            new MalzemeAtamasi
            {
                KaynakElemanTuru = MalzemeAtamaHedefTuru.Kapi,
                KaynakElemanId = kapiAcikligi.Id,
                KatNo = kapiAcikligi.KatNo,
                YapiElemaniTipiId = VarsayilanYapiElemaniTipleri.VarsayilanKapiTipiId,
                MalzemeIdleri = ["malzeme-kapi-yangin-60"]
            }
        ];

        MimariProjeServisi.Kaydet(proje, yol);
        var json = File.ReadAllText(yol);
        Assert(json.Contains("\"YapiElemaniTipleri\"", StringComparison.Ordinal),
            "Kaydedilen dosya yapı elemanı tipi listesini içermeli.");
        Assert(json.Contains("\"MalzemeAtamalari\"", StringComparison.Ordinal),
            "Kaydedilen dosya malzeme ataması listesini içermeli.");
        Assert(json.Contains("\"Duvar\"", StringComparison.Ordinal),
            "Kaydedilen dosya atama hedef türünü string enum olarak saklamalı.");
        Assert(json.Contains(VarsayilanYapiElemaniTipleri.VarsayilanDuvarTipiId, StringComparison.Ordinal),
            "Kaydedilen dosya M35 uyumlu duvar tipi Id'sini saklamalı.");

        var acilan = MimariProjeServisi.Ac(yol);
        Assert(acilan.YapiElemaniTipleri.Count == proje.YapiElemaniTipleri.Count,
            "Açılan proje yapı elemanı tipi listesini korumalı.");
        Assert(acilan.MalzemeAtamalari.Count == proje.MalzemeAtamalari.Count,
            "Açılan proje malzeme atama listesini korumalı.");

        var acilanDuvarTipi = acilan.YapiElemaniTipleri.FirstOrDefault(t => t.Id == VarsayilanYapiElemaniTipleri.VarsayilanDuvarTipiId)
            ?? throw new InvalidOperationException("Açılan projede varsayılan duvar tipi bulunmalı.");
        Assert(acilanDuvarTipi.Sinif == YapiElemaniSinifi.Duvar, "Yapı elemanı sınıfı korunmalı.");
        Assert(acilanDuvarTipi.Katmanlar.Count == 3, "Yapı elemanı katmanları korunmalı.");
        AssertClose(acilanDuvarTipi.ToplamKalinlikM, 0.08, "Açılan duvar tipi toplam kalınlığı hesaplamalı.");

        var acilanKapiAtamasi = acilan.MalzemeAtamalari.FirstOrDefault(a => a.KaynakElemanId == kapiAcikligi.Id)
            ?? throw new InvalidOperationException("Açılan projede kapı açıklığı ataması bulunmalı.");
        Assert(acilanKapiAtamasi.GecerliMi, "Açılan atama geçerli kalmalı.");
        Assert(acilanKapiAtamasi.KaynakElemanTuru == MalzemeAtamaHedefTuru.Kapi, "Atama hedef türü korunmalı.");
        Assert(acilanKapiAtamasi.MalzemeIdleri.Contains("malzeme-kapi-yangin-60"), "Atama malzeme bağlantısı korunmalı.");

        var kapiTipi = acilan.YapiElemaniTipleri.First(t => t.Id == acilanKapiAtamasi.YapiElemaniTipiId);
        Assert(kapiTipi.HedefTuruIleUyumluMu(acilanKapiAtamasi.KaynakElemanTuru),
            "Kapı tipi kapı açıklığı atamasıyla uyumlu olmalı.");
        Assert(kapiTipi.PerformansOzetiHesapla(acilan.Malzemeler).YanginBilgisiVar,
            "Açılan kapı tipi malzeme kataloğundan yangın bilgisi üretmeli.");
    }
    finally
    {
        if (File.Exists(yol))
            File.Delete(yol);
    }
}

static void MahalFinisajOnerisiMahalTipineGoreUretir()
{
    var banyo = MahalFinisajOneriServisi.Oner(MahalTipi.Banyo, KullanimSinifi.IslakHacim);
    Assert(banyo.ZeminTipiId == VarsayilanYapiElemaniTipleri.VarsayilanIslakHacimDosemeTipiId,
        "Banyo finisaj önerisi ıslak hacim döşeme tipini seçmeli.");
    Assert(banyo.UyariNotlari.Count > 0, "Islak hacim profili su yalıtım/kaymazlık notu taşımalı.");

    var konferans = MahalFinisajOneriServisi.Oner(MahalTipi.KonferansSalonu, KullanimSinifi.AnaKullanim);
    Assert(konferans.TavanTipiId == VarsayilanYapiElemaniTipleri.VarsayilanTavanTipiId,
        "Konferans finisaj önerisi akustik tavan tipini seçmeli.");

    var salon = MahalFinisajOneriServisi.Oner(MahalTipi.Salon, KullanimSinifi.AnaKullanim);
    Assert(salon.TavanTipiId == VarsayilanYapiElemaniTipleri.VarsayilanBoyaliTavanTipiId,
        "Konut salonu finisaj önerisi boyalı tavan tipini seçmeli.");

    var bilinmeyen = MahalFinisajOneriServisi.Oner(MahalTipi.Otopark, KullanimSinifi.Otopark);
    Assert(bilinmeyen.OnerisiVar, "Eşleşmeyen mahal tipi için genel finisaj önerisi üretilmeli.");
    Assert(bilinmeyen.Kod == "FIN-GENEL", "Eşleşmeyen mahal tipi genel profile düşmeli.");

    var tipIdleri = VarsayilanYapiElemaniTipleri.Tum
        .Select(t => t.Id)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    foreach (var profil in VarsayilanMahalFinisajProfilleri.Tum)
    {
        Assert(profil.TamMi, $"{profil.Kod} profili zemin/duvar/tavan önerisini eksiksiz taşımalı.");
        foreach (var tipId in profil.OnerilenTipIdleri)
        {
            Assert(tipIdleri.Contains(tipId),
                $"{profil.Kod} profili çözülebilir bir yapı elemanı tip Id'sine işaret etmeli: {tipId}");
        }
    }
}

static void FinisajOnKontroluUygunsuzKararlariYakalar()
{
    var proje = MimariProjeServisi.YeniOlustur("M36 Finisaj Ön Kontrol");
    proje.IhtiyacProgrami.BinaIslevi = BinaIslevi.Konut;

    var banyo = new MahalGereksinimi
    {
        Id = "mah-banyo",
        Ad = "Banyo",
        MahalTipi = MahalTipi.Banyo,
        KullanimSinifi = KullanimSinifi.IslakHacim,
        Adet = 1,
        HedefAlanM2 = 6,
        HedefNetYukseklikM = 2.6
    };
    var konferans = new MahalGereksinimi
    {
        Id = "mah-konferans",
        Ad = "Konferans Salonu",
        MahalTipi = MahalTipi.KonferansSalonu,
        KullanimSinifi = KullanimSinifi.AnaKullanim,
        Adet = 1,
        HedefAlanM2 = 200,
        HedefNetYukseklikM = 4.5
    };
    var merdiven = new MahalGereksinimi
    {
        Id = "mah-merdiven",
        Ad = "Kaçış Merdiveni",
        MahalTipi = MahalTipi.YanginMerdiveni,
        KullanimSinifi = KullanimSinifi.DuseySirkulasyon,
        Adet = 1,
        HedefAlanM2 = 18,
        HedefNetYukseklikM = 3.0
    };
    proje.IhtiyacProgrami.Mahaller.AddRange([banyo, konferans, merdiven]);

    // Yangın/akustik bilgisi taşımayan özel tip (yalnız cam katmanı).
    proje.YapiElemaniTipleri.Add(new YapiElemaniTipi
    {
        Id = "tip-yangin-yok",
        Kod = "TEST-YANGIN-YOK",
        Ad = "Test yangın bilgisi olmayan tip",
        Sinif = YapiElemaniSinifi.Tavan,
        Katmanlar =
        [
            new YapiElemaniKatmani
            {
                Id = "katman-cam",
                SiraNo = 1,
                Ad = "Cam katman",
                MalzemeId = "malzeme-cam-lowe-cift",
                KalinlikM = 0.024,
                Fonksiyon = MalzemeKatmanFonksiyonu.Bitis
            }
        ]
    });

    // Mahal tiplerini uygunsuz finisaja yönlendiren özel profiller.
    proje.FinisajProfilleri.AddRange(
    [
        new MahalFinisajProfili
        {
            Kod = "OZEL-BANYO",
            MahalTipi = MahalTipi.Banyo,
            KullanimSinifi = KullanimSinifi.IslakHacim,
            ZeminTipiId = VarsayilanYapiElemaniTipleri.VarsayilanDosemeTipiId, // ıslak değil
            DuvarTipiId = VarsayilanYapiElemaniTipleri.VarsayilanDuvarTipiId,
            TavanTipiId = VarsayilanYapiElemaniTipleri.VarsayilanBoyaliTavanTipiId
        },
        new MahalFinisajProfili
        {
            Kod = "OZEL-KONFERANS",
            MahalTipi = MahalTipi.KonferansSalonu,
            KullanimSinifi = KullanimSinifi.AnaKullanim,
            ZeminTipiId = VarsayilanYapiElemaniTipleri.VarsayilanDosemeTipiId,
            DuvarTipiId = VarsayilanYapiElemaniTipleri.VarsayilanDuvarTipiId,
            TavanTipiId = VarsayilanYapiElemaniTipleri.VarsayilanBoyaliTavanTipiId // akustik değil
        },
        new MahalFinisajProfili
        {
            Kod = "OZEL-MERDIVEN",
            MahalTipi = MahalTipi.YanginMerdiveni,
            KullanimSinifi = KullanimSinifi.DuseySirkulasyon,
            ZeminTipiId = "tip-yangin-yok",
            DuvarTipiId = "tip-yangin-yok",
            TavanTipiId = "tip-yangin-yok"
        }
    ]);

    var uyarilar = MahalFinisajOneriServisi.FinisajUyarilari(proje);
    Assert(uyarilar.Any(u => u.KuralKodu == MahalFinisajOneriServisi.IslakZeminUygunsuzKodu && u.EtkilenenMahalId == "mah-banyo"),
        "Islak hacimde uygunsuz zemin uyarısı banyo mahaliyle üretilmeli.");
    Assert(uyarilar.Any(u => u.KuralKodu == MahalFinisajOneriServisi.AkustikTavanEksikKodu && u.EtkilenenMahalId == "mah-konferans"),
        "Akustik tavan eksikliği konferans mahaliyle üretilmeli.");
    Assert(uyarilar.Any(u => u.KuralKodu == MahalFinisajOneriServisi.KacisYanginEksikKodu && u.EtkilenenMahalId == "mah-merdiven"),
        "Kaçış mahalinde yangın bilgisi eksikliği uyarısı üretilmeli.");

    var m34Sonuclari = YonetmelikOnKontrolServisi.FinisajKontrolleriniCalistir(proje);
    Assert(m34Sonuclari.Any(s => s.KuralKodu == MahalFinisajOneriServisi.IslakZeminUygunsuzKodu &&
            s.Seviye == KontrolSeviyesi.Uyari && s.KaynakKodu == YonetmelikOnKontrolServisi.VarsayilanVeriKaynakKodu),
        "Finisaj uyarıları M34 üzerinden uyarı seviyesinde ve M34 veri kaynak profiline bağlı çıkmalı.");
    Assert(YonetmelikOnKontrolServisi.IlkVeriKontrolleriniCalistir(proje)
            .Any(s => s.KuralKodu == MahalFinisajOneriServisi.KacisYanginEksikKodu),
        "İlk veri kontrolleri finisaj uyarılarını da taşımalı.");

    var temizProje = MimariProjeServisi.YeniOlustur("Temiz Ofis Finisaj");
    temizProje.IhtiyacProgrami = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.Ofis, kapasite: 20, katSayisi: 2);
    var temizUyarilar = MahalFinisajOneriServisi.FinisajUyarilari(temizProje);
    Assert(!temizUyarilar.Any(u =>
            u.KuralKodu == MahalFinisajOneriServisi.IslakZeminUygunsuzKodu ||
            u.KuralKodu == MahalFinisajOneriServisi.AkustikTavanEksikKodu ||
            u.KuralKodu == MahalFinisajOneriServisi.KacisYanginEksikKodu),
        $"Varsayılan önerilerle ofis programı finisaj uyarısı üretmemeli: {string.Join(", ", temizUyarilar.Select(u => u.KuralKodu))}");
}

static void FinisajProfilleriE4MimariDosyasindaKorunur()
{
    var yol = Path.Combine(Path.GetTempPath(), $"e4mimari-finisaj-test-{Guid.NewGuid():N}.e4mimari.json");
    try
    {
        var proje = MimariProjeServisi.YeniOlustur("M36 Finisaj Kayıt Testi");
        proje.FinisajProfilleri = VarsayilanMahalFinisajProfilleri.Tum.ToList();
        var beklenenSayi = proje.FinisajProfilleri.Count;
        Assert(beklenenSayi > 0, "Varsayılan finisaj profili kataloğu boş olmamalı.");

        MimariProjeServisi.Kaydet(proje, yol);
        var json = File.ReadAllText(yol);
        Assert(json.Contains("\"FinisajProfilleri\"", StringComparison.Ordinal),
            "Kaydedilen dosya finisaj profili listesini içermeli.");

        var acilan = MimariProjeServisi.Ac(yol);
        Assert(acilan.FinisajProfilleri.Count == beklenenSayi,
            "Açılan proje finisaj profili listesini korumalı.");

        var banyoProfili = acilan.FinisajProfilleri.FirstOrDefault(p => p.MahalTipi == MahalTipi.Banyo)
            ?? throw new InvalidOperationException("Açılan projede banyo finisaj profili bulunmalı.");
        Assert(banyoProfili.ZeminTipiId == VarsayilanYapiElemaniTipleri.VarsayilanIslakHacimDosemeTipiId,
            "Açılan banyo profili ıslak hacim döşeme önerisini korumalı.");
        Assert(banyoProfili.UyariNotlari.Count > 0, "Açılan banyo profili uyarı notlarını korumalı.");
    }
    finally
    {
        if (File.Exists(yol))
            File.Delete(yol);
    }
}

static void DisiplinVeriOzetiGuvenliVeTutarliUretir()
{
    // Boş / null girdide güvenli çalışmalı (36.6.5).
    var bosOzet = DisiplinVeriOzetiServisi.Hesapla(null);
    Assert(bosOzet.ToplamElemanSayisi == 0, "Null projede disiplin özeti boş eleman listesi üretmeli.");
    Assert(!bosOzet.Akustik.VeriVar && !bosOzet.Enerji.VeriVar && !bosOzet.Metraj.VeriVar,
        "Null projede disiplin özetleri veri taşımamalı.");
    Assert(bosOzet.Elektrik.MahalSayisi == 0, "Null projede elektrik özeti mahal taşımamalı.");

    var bosProjeOzeti = DisiplinVeriOzetiServisi.Hesapla(new MimariProje());
    Assert(bosProjeOzeti.ToplamElemanSayisi == 0, "3D modeli olmayan projede eleman üretilmemeli.");

    // Gerçekçi proje: ofis programı + 3D model + varsayılan malzeme/tip + pencere açıklığı ataması.
    var proje = MimariProjeServisi.YeniOlustur("M36 Disiplin Özet Testi");
    proje.IhtiyacProgrami = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.Ofis, kapasite: 20, katSayisi: 1);
    proje.PlanTaslagi = PlanTaslagiUretimServisi.Uret(proje.IhtiyacProgrami, proje.Id);
    Model3DUretimServisi.Uret(proje.PlanTaslagi);
    proje.Malzemeler = VarsayilanMimariMalzemeler.Tum.ToList();
    proje.YapiElemaniTipleri = VarsayilanYapiElemaniTipleri.Tum.ToList();

    var kat3D = proje.PlanTaslagi.Model3D?.Katlar.FirstOrDefault()
        ?? throw new InvalidOperationException("Test için 3D kat modeli üretilmeli.");
    var duvar = kat3D.Duvarlar.FirstOrDefault()
        ?? throw new InvalidOperationException("Test için 3D duvar üretilmeli.");

    var pencere = new Aciklik3D
    {
        Id = "aciklik3d-test-pencere",
        KaynakMahalBloguId = duvar.KaynakMahalBloguId,
        DuvarId = duvar.Id,
        Tip = Aciklik3DTipi.Pencere,
        KatNo = duvar.KatNo,
        GenislikM = 1.4,
        YukseklikM = 1.2
    };
    kat3D.Acikliklar.Add(pencere);
    proje.MalzemeAtamalari.Add(new MalzemeAtamasi
    {
        KaynakElemanTuru = MalzemeAtamaHedefTuru.Pencere,
        KaynakElemanId = pencere.Id,
        KatNo = pencere.KatNo,
        YapiElemaniTipiId = VarsayilanYapiElemaniTipleri.VarsayilanPencereTipiId
    });

    var ozet = DisiplinVeriOzetiServisi.Hesapla(proje);

    Assert(ozet.ToplamElemanSayisi > 0, "Tip atanmış 3D elemanlardan disiplin satırı üretilmeli.");

    // Akustik: varsayılan akustik tavan tipi yutuculuk verisi taşır.
    Assert(ozet.Akustik.AkustikVeriliElemanSayisi >= 1, "Akustik tavan akustik veri satırı üretmeli.");
    Assert(ozet.Akustik.EnYuksekYutuculukAw > 0, "Akustik özet en yüksek yutuculuğu hesaplamalı.");

    // Enerji: low-e cam pencere U değeri ve dış kabuk verisi taşır.
    Assert(ozet.Enerji.UDegerliElemanSayisi >= 1, "Pencere U değeri taşıyan eleman olarak görünmeli.");
    Assert(ozet.Enerji.DisKabukElemanSayisi >= 1, "Cam pencere dış kabuk elemanı sayılmalı.");
    Assert(ozet.Enerji.EnDusukUDegeri > 0, "Enerji özeti en düşük U değerini hesaplamalı.");

    // Elektrik: her mahal için yüzey/kaplama notu üretilmeli.
    Assert(ozet.Elektrik.MahalSayisi >= 1, "Elektrik özeti mahal yüzey satırı üretmeli.");
    Assert(ozet.Elektrik.Mahaller.All(m => !string.IsNullOrWhiteSpace(m.KaplamaNotu)),
        "Her mahalin kaplama notu dolu olmalı.");

    // Metraj: toplam alan ve tür kırılımı tutarlı olmalı.
    Assert(ozet.Metraj.ToplamAlanM2 > 0, "Metraj özeti pozitif toplam alan üretmeli.");
    Assert(ozet.Metraj.ElemanSayisi == ozet.ToplamElemanSayisi, "Metraj eleman sayısı toplam eleman sayısıyla tutarlı olmalı.");
    Assert(ozet.Metraj.TurOzetleri.Any(t => t.ElemanTuru == MalzemeAtamaHedefTuru.Duvar),
        "Metraj tür kırılımında duvar bulunmalı.");
    var metrajToplam = ozet.Metraj.TurOzetleri.Sum(t => t.ToplamAlanM2);
    AssertClose(metrajToplam, ozet.Metraj.ToplamAlanM2, "Tür kırılımı alan toplamı genel toplamla tutarlı olmalı.");
}

static void M36KapanisRegresyonuAtamaVeDisiplinOzetiniKorur()
{
    var yol = Path.Combine(Path.GetTempPath(), $"e4mimari-m36-kapanis-{Guid.NewGuid():N}.e4mimari.json");
    try
    {
        // Tam M36 projesi: program + 3D model + malzeme + tip + finisaj + kapı/pencere ataması.
        var proje = MimariProjeServisi.YeniOlustur("M36 Kapanış Regresyonu");
        proje.IhtiyacProgrami = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.Ofis, kapasite: 30, katSayisi: 2);
        proje.PlanTaslagi = PlanTaslagiUretimServisi.Uret(proje.IhtiyacProgrami, proje.Id);
        Model3DUretimServisi.Uret(proje.PlanTaslagi);
        proje.Malzemeler = VarsayilanMimariMalzemeler.Tum.ToList();
        proje.YapiElemaniTipleri = VarsayilanYapiElemaniTipleri.Tum.ToList();
        proje.FinisajProfilleri = VarsayilanMahalFinisajProfilleri.Tum.ToList();

        var kat3D = proje.PlanTaslagi.Model3D?.Katlar.FirstOrDefault()
            ?? throw new InvalidOperationException("Kapanış testi için 3D kat modeli üretilmeli.");
        var duvar = kat3D.Duvarlar.FirstOrDefault()
            ?? throw new InvalidOperationException("Kapanış testi için 3D duvar üretilmeli.");

        var kapi = new Aciklik3D
        {
            Id = "aciklik3d-kapanis-kapi",
            KaynakMahalBloguId = duvar.KaynakMahalBloguId,
            DuvarId = duvar.Id,
            Tip = Aciklik3DTipi.Kapi,
            KatNo = duvar.KatNo,
            GenislikM = 0.9,
            YukseklikM = 2.1
        };
        var pencere = new Aciklik3D
        {
            Id = "aciklik3d-kapanis-pencere",
            KaynakMahalBloguId = duvar.KaynakMahalBloguId,
            DuvarId = duvar.Id,
            Tip = Aciklik3DTipi.Pencere,
            KatNo = duvar.KatNo,
            GenislikM = 1.4,
            YukseklikM = 1.2
        };
        kat3D.Acikliklar.Add(kapi);
        kat3D.Acikliklar.Add(pencere);
        proje.MalzemeAtamalari =
        [
            new MalzemeAtamasi
            {
                KaynakElemanTuru = MalzemeAtamaHedefTuru.Kapi,
                KaynakElemanId = kapi.Id,
                KatNo = kapi.KatNo,
                YapiElemaniTipiId = VarsayilanYapiElemaniTipleri.VarsayilanKapiTipiId,
                MalzemeIdleri = ["malzeme-kapi-yangin-60"]
            },
            new MalzemeAtamasi
            {
                KaynakElemanTuru = MalzemeAtamaHedefTuru.Pencere,
                KaynakElemanId = pencere.Id,
                KatNo = pencere.KatNo,
                YapiElemaniTipiId = VarsayilanYapiElemaniTipleri.VarsayilanPencereTipiId
            }
        ];

        var oncekiOzet = DisiplinVeriOzetiServisi.Hesapla(proje);
        var beklenenMalzeme = proje.Malzemeler.Count;
        var beklenenTip = proje.YapiElemaniTipleri.Count;
        var beklenenFinisaj = proje.FinisajProfilleri.Count;
        var beklenenAtama = proje.MalzemeAtamalari.Count;

        // Kaydet -> aç döngüsü hiçbir M36 verisini kaybetmemeli.
        MimariProjeServisi.Kaydet(proje, yol);
        var acilan = MimariProjeServisi.Ac(yol);

        Assert(acilan.Malzemeler.Count == beklenenMalzeme, "Açılan proje malzeme listesini korumalı.");
        Assert(acilan.YapiElemaniTipleri.Count == beklenenTip, "Açılan proje yapı elemanı tiplerini korumalı.");
        Assert(acilan.FinisajProfilleri.Count == beklenenFinisaj, "Açılan proje finisaj profillerini korumalı.");
        Assert(acilan.MalzemeAtamalari.Count == beklenenAtama, "Açılan proje malzeme atamalarını korumalı.");

        var kapiAtamasi = acilan.MalzemeAtamalari.FirstOrDefault(a => a.KaynakElemanId == kapi.Id)
            ?? throw new InvalidOperationException("Açılan projede kapı ataması bulunmalı.");
        Assert(kapiAtamasi.MalzemeIdleri.Contains("malzeme-kapi-yangin-60"), "Kapı atamasının malzeme bağlantısı korunmalı.");

        // Disiplin özeti kayıt-kurtarma sonrası birebir aynı kalmalı.
        var sonrakiOzet = DisiplinVeriOzetiServisi.Hesapla(acilan);
        Assert(sonrakiOzet.ToplamElemanSayisi == oncekiOzet.ToplamElemanSayisi,
            "Disiplin eleman sayısı kayıt-kurtarma sonrası korunmalı.");
        Assert(sonrakiOzet.Akustik.AkustikVeriliElemanSayisi == oncekiOzet.Akustik.AkustikVeriliElemanSayisi,
            "Akustik veri taşıyan eleman sayısı korunmalı.");
        Assert(sonrakiOzet.Enerji.DisKabukElemanSayisi == oncekiOzet.Enerji.DisKabukElemanSayisi,
            "Dış kabuk eleman sayısı korunmalı.");
        Assert(sonrakiOzet.Enerji.UDegerliElemanSayisi >= 1, "Cam pencere U değeri kayıt sonrası da görünmeli.");
        Assert(sonrakiOzet.Elektrik.MahalSayisi == oncekiOzet.Elektrik.MahalSayisi,
            "Elektrik mahal yüzey özeti korunmalı.");
        AssertClose(sonrakiOzet.Metraj.ToplamAlanM2, oncekiOzet.Metraj.ToplamAlanM2,
            "Metraj toplam alanı kayıt-kurtarma sonrası korunmalı.");
        AssertClose(sonrakiOzet.Metraj.ToplamHacimM3, oncekiOzet.Metraj.ToplamHacimM3,
            "Metraj toplam hacmi kayıt-kurtarma sonrası korunmalı.");

        // Kapı tipi yangın bilgisini, pencere tipi dış kabuk bilgisini taşımalı.
        Assert(sonrakiOzet.Elemanlar.Any(e => e.ElemanTuru == MalzemeAtamaHedefTuru.Kapi && e.YanginVeriVar),
            "Kapı elemanı yangın verisi taşımalı.");
        Assert(sonrakiOzet.Elemanlar.Any(e => e.ElemanTuru == MalzemeAtamaHedefTuru.Pencere && e.DisKabukMu),
            "Pencere elemanı dış kabuk olarak işaretlenmeli.");
    }
    finally
    {
        if (File.Exists(yol))
            File.Delete(yol);
    }
}

static void M37MahalListesiTablosuUretir()
{
    // Null girdide güvenli boş tablo.
    var bosTablo = MimariRaporUretimServisi.MahalListesiOlustur(null);
    Assert(!bosTablo.VeriVar && bosTablo.SatirSayisi == 0, "Null projede mahal listesi boş olmalı.");

    // Plan/3D olmayan projede ihtiyaç programından üretim (hedef alan/hacim, kat 0).
    var programProjesi = MimariProjeServisi.YeniOlustur("M37 Program Mahal Listesi");
    programProjesi.IhtiyacProgrami = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.Ofis, kapasite: 24, katSayisi: 2);
    var programTablo = MimariRaporUretimServisi.MahalListesiOlustur(programProjesi);

    Assert(programTablo.SatirSayisi == programProjesi.IhtiyacProgrami.Mahaller.Count,
        "Program tablosu her gereksinim için bir satır üretmeli.");
    Assert(programTablo.Satirlar.All(s => s.KatNo == 0), "Plan yokken satırlar kat 0 ile işaretlenmeli.");
    Assert(programTablo.Satirlar.All(s => !string.IsNullOrWhiteSpace(s.MahalNo)), "Her satır mahal no taşımalı.");
    Assert(programTablo.Satirlar.Any(s => s.KullanimSinifi == KullanimSinifi.AnaKullanim),
        "Program tablosunda ana kullanım mahali bulunmalı.");
    var beklenenProgramAlan = programProjesi.IhtiyacProgrami.Mahaller.Sum(m => m.ToplamHedefAlanM2);
    AssertClose(programTablo.ToplamAlanM2, beklenenProgramAlan, "Program tablosu toplam hedef alanı yansıtmalı.");

    // 3D model olan projede yerleşmiş mahallerden üretim (gerçek alan/hacim, kat bilgisi).
    var modelProjesi = MimariProjeServisi.YeniOlustur("M37 3D Mahal Listesi");
    modelProjesi.IhtiyacProgrami = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.Ofis, kapasite: 24, katSayisi: 2);
    modelProjesi.PlanTaslagi = PlanTaslagiUretimServisi.Uret(modelProjesi.IhtiyacProgrami, modelProjesi.Id);
    var model = Model3DUretimServisi.Uret(modelProjesi.PlanTaslagi);
    var tablo = MimariRaporUretimServisi.MahalListesiOlustur(modelProjesi);

    var beklenenSatir = model.Katlar.Sum(k => k.Mahaller.Count);
    Assert(tablo.SatirSayisi == beklenenSatir, "3D tablo her yerleşmiş mahal için satır üretmeli.");
    Assert(tablo.VeriVar, "3D mahal listesi veri taşımalı.");
    Assert(tablo.Satirlar.All(s => s.KatNo >= 1), "3D satırlar gerçek kat numarası taşımalı.");
    Assert(tablo.Satirlar.All(s => s.Adet == 1), "3D mahal instance'ları birer adet olmalı.");
    Assert(tablo.Satirlar.All(s => s.AlanM2 > 0), "3D mahal satırları pozitif alan taşımalı.");
    Assert(tablo.Satirlar.Any(s => s.KullanimSinifi == KullanimSinifi.AnaKullanim),
        "3D tablo plan bloğundan kullanım sınıfını çözmeli.");

    var beklenenModelAlan = model.Katlar.SelectMany(k => k.Mahaller).Sum(m => m.AlanM2);
    AssertClose(tablo.ToplamAlanM2, beklenenModelAlan, "3D tablo toplam alanı model mahalleriyle tutarlı olmalı.");
    Assert(tablo.ToplamMahalAdedi == tablo.SatirSayisi, "3D tabloda toplam adet satır sayısına eşit olmalı.");
}

static void M37AlanTablosuTutarliUretir()
{
    // Null / boş güvenli.
    Assert(!MimariRaporUretimServisi.AlanTablosuOlustur(null).VeriVar,
        "Null projede alan tablosu boş olmalı.");
    Assert(!MimariRaporUretimServisi.AlanTablosuOlustur(MimariProjeServisi.YeniOlustur("Boş")).VeriVar,
        "Mahalsiz projede alan tablosu boş olmalı.");

    var proje = MimariProjeServisi.YeniOlustur("M37 Alan Tablosu");
    proje.IhtiyacProgrami = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.KonferansVeToplanti, kapasite: 200, katSayisi: 1);

    var ozet = AlanOzetiServisi.Hesapla(proje.IhtiyacProgrami);
    var tablo = MimariRaporUretimServisi.AlanTablosuOlustur(proje);

    Assert(tablo.VeriVar, "Dolu projede alan tablosu veri taşımalı.");
    AssertClose(tablo.NetAlanM2, ozet.NetAlanM2, "Tablo net alanı özetle tutarlı olmalı.");
    AssertClose(tablo.SirkulasyonAlanM2, ozet.SirkulasyonAlanM2, "Tablo sirkülasyon alanı özetle tutarlı olmalı.");
    AssertClose(tablo.TeknikAlanM2, ozet.TeknikAlanM2, "Tablo teknik alanı özetle tutarlı olmalı.");
    AssertClose(tablo.BrutAlanM2, ozet.BrutAlanM2, "Tablo brüt alanı özetle tutarlı olmalı.");
    AssertClose(tablo.NetHacimM3, ozet.NetHacimM3, "Tablo net hacmi özetle tutarlı olmalı.");
    Assert(tablo.MahalSayisi == ozet.MahalSayisi, "Tablo mahal sayısı özetle tutarlı olmalı.");
    Assert(tablo.ToplamMahalAdedi == ozet.ToplamMahalAdedi, "Tablo toplam adet özetle tutarlı olmalı.");
    Assert(tablo.KullanimKirilimi.Count == ozet.KullanimSinifiOzetleri.Count,
        "Tablo kullanım sınıfı kırılımı özetle aynı satır sayısında olmalı.");
    Assert(tablo.KullanimKirilimi.Any(k => k.KullanimSinifi == KullanimSinifi.AnaKullanim && k.AlanM2 > 0),
        "Kırılımda ana kullanım alanı bulunmalı.");

    var kirilimToplam = tablo.KullanimKirilimi.Sum(k => k.AlanM2);
    AssertClose(kirilimToplam, tablo.NetAlanM2, "Kırılım alan toplamı net alanla tutarlı olmalı.");
}

static void M37KapiPencereCizelgesiUretir()
{
    // Açıklık yoksa boş çizelge.
    Assert(!MimariRaporUretimServisi.KapiPencereCizelgesiOlustur(null).VeriVar,
        "Null projede kapı-pencere çizelgesi boş olmalı.");

    var proje = MimariProjeServisi.YeniOlustur("M37 Kapı-Pencere");
    proje.IhtiyacProgrami = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.Ofis, kapasite: 20, katSayisi: 1);
    proje.PlanTaslagi = PlanTaslagiUretimServisi.Uret(proje.IhtiyacProgrami, proje.Id);
    Model3DUretimServisi.Uret(proje.PlanTaslagi);
    proje.Malzemeler = VarsayilanMimariMalzemeler.Tum.ToList();
    proje.YapiElemaniTipleri = VarsayilanYapiElemaniTipleri.Tum.ToList();

    var kat3D = proje.PlanTaslagi.Model3D!.Katlar.First();
    var duvar = kat3D.Duvarlar.First();

    Aciklik3D Kapi(string id, double g, double y) => new()
    {
        Id = id,
        KaynakMahalBloguId = duvar.KaynakMahalBloguId,
        DuvarId = duvar.Id,
        Tip = Aciklik3DTipi.Kapi,
        KatNo = duvar.KatNo,
        GenislikM = g,
        YukseklikM = y
    };

    // İki özdeş yangın kapısı (gruplanmalı), bir farklı ölçüde atanmamış kapı, bir cam pencere.
    var kapi1 = Kapi("ac-kapi-1", 0.9, 2.1);
    var kapi2 = Kapi("ac-kapi-2", 0.9, 2.1);
    var kapi3 = Kapi("ac-kapi-3", 1.0, 2.1);
    var pencere = new Aciklik3D
    {
        Id = "ac-pencere-1",
        KaynakMahalBloguId = duvar.KaynakMahalBloguId,
        DuvarId = duvar.Id,
        Tip = Aciklik3DTipi.Pencere,
        KatNo = duvar.KatNo,
        GenislikM = 1.4,
        YukseklikM = 1.2
    };
    kat3D.Acikliklar.AddRange([kapi1, kapi2, kapi3, pencere]);

    foreach (var (id, tipId) in new[]
             {
                 ("ac-kapi-1", VarsayilanYapiElemaniTipleri.VarsayilanKapiTipiId),
                 ("ac-kapi-2", VarsayilanYapiElemaniTipleri.VarsayilanKapiTipiId),
                 ("ac-pencere-1", VarsayilanYapiElemaniTipleri.VarsayilanPencereTipiId)
             })
    {
        var pencereMi = id.Contains("pencere");
        proje.MalzemeAtamalari.Add(new MalzemeAtamasi
        {
            KaynakElemanTuru = pencereMi ? MalzemeAtamaHedefTuru.Pencere : MalzemeAtamaHedefTuru.Kapi,
            KaynakElemanId = id,
            KatNo = duvar.KatNo,
            YapiElemaniTipiId = tipId
        });
    }

    var cizelge = MimariRaporUretimServisi.KapiPencereCizelgesiOlustur(proje);

    Assert(cizelge.VeriVar, "Açıklıklı projede çizelge veri taşımalı.");
    Assert(cizelge.ToplamKapiAdedi == 3, "Toplam kapı adedi 3 olmalı.");
    Assert(cizelge.ToplamPencereAdedi == 1, "Toplam pencere adedi 1 olmalı.");

    var ozdesKapi = cizelge.Satirlar.FirstOrDefault(s => !s.PencereMi && Math.Abs(s.GenislikM - 0.9) < 1e-6)
        ?? throw new InvalidOperationException("0.9 m kapı satırı bulunmalı.");
    Assert(ozdesKapi.Adet == 2, "İki özdeş kapı tek satırda gruplanmalı (adet 2).");
    Assert(ozdesKapi.Poz.StartsWith('K'), "Kapı pozisyonu K ile başlamalı.");
    Assert(ozdesKapi.Olcu == "90/210", "Kapı ölçüsü santimetre cinsinden 90/210 olmalı.");
    Assert(ozdesKapi.YapiElemaniTipiAdi != "Atanmadı", "Atanan kapı tipi adı çözülmeli.");

    var atanmamisKapi = cizelge.Satirlar.FirstOrDefault(s => !s.PencereMi && Math.Abs(s.GenislikM - 1.0) < 1e-6)
        ?? throw new InvalidOperationException("1.0 m kapı satırı bulunmalı.");
    Assert(atanmamisKapi.Adet == 1 && atanmamisKapi.YapiElemaniTipiAdi == "Atanmadı",
        "Atama yapılmamış kapı 'Atanmadı' olarak görünmeli.");

    var pencereSatiri = cizelge.Satirlar.FirstOrDefault(s => s.PencereMi)
        ?? throw new InvalidOperationException("Pencere satırı bulunmalı.");
    Assert(pencereSatiri.Poz.StartsWith('P'), "Pencere pozisyonu P ile başlamalı.");
    Assert(pencereSatiri.MalzemeAdlari.Count > 0, "Pencere satırı malzeme adı taşımalı (cam tipinden).");
    AssertClose(pencereSatiri.BirimAlanM2, 1.4 * 1.2, "Pencere birim alanı ölçüden hesaplanmalı.");
}

static void Faz10Mutabakat()
{
    Assert(MutabakatServisi.Denetle(null).TutarliMi, "Null proje tutarlı sayılmalı (bulgu yok).");

    var proje = MimariProjeServisi.YeniOlustur("Faz10 Mutabakat");
    proje.IhtiyacProgrami = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.Ofis, kapasite: 20, katSayisi: 1);
    proje.PlanTaslagi = PlanTaslagiUretimServisi.Uret(proje.IhtiyacProgrami, proje.Id);
    var model = Model3DUretimServisi.Uret(proje.PlanTaslagi);
    proje.YapiElemaniTipleri = VarsayilanYapiElemaniTipleri.Tum.ToList();

    var gercekDuvar = model.Katlar.First().Duvarlar.First();

    // Geçerli atama (gerçek duvar + katalog tipi) → bulgu üretmemeli.
    proje.MalzemeAtamalari.Add(new MalzemeAtamasi
    {
        KaynakElemanTuru = MalzemeAtamaHedefTuru.Duvar,
        KaynakElemanId = gercekDuvar.Id,
        YapiElemaniTipiId = VarsayilanYapiElemaniTipleri.VarsayilanDuvarTipiId
    });
    Assert(MutabakatServisi.Denetle(proje).TutarliMi, "Yalnız geçerli atama varken proje tutarlı olmalı.");

    // Askıda atama (var olmayan eleman) + geçersiz tip referanslı atama.
    proje.MalzemeAtamalari.Add(new MalzemeAtamasi
    {
        KaynakElemanTuru = MalzemeAtamaHedefTuru.Duvar,
        KaynakElemanId = "yok-eleman",
        YapiElemaniTipiId = VarsayilanYapiElemaniTipleri.VarsayilanDuvarTipiId
    });
    proje.MalzemeAtamalari.Add(new MalzemeAtamasi
    {
        KaynakElemanTuru = MalzemeAtamaHedefTuru.Tavan,
        KaynakElemanId = model.Katlar.First().Tavanlar.First().Id,
        YapiElemaniTipiId = "yok-tip"
    });

    // Kırık finisaj referansı.
    proje.FinisajProfilleri.Add(new MahalFinisajProfili
    {
        Kod = "FIN-BOZUK",
        MahalTipi = MahalTipi.Ofis,
        ZeminTipiId = "yok-zemin-tipi"
    });

    var rapor = MutabakatServisi.Denetle(proje);

    Assert(!rapor.TutarliMi, "Sorunlu projede mutabakat tutarsızlık bildirmeli.");
    Assert(rapor.AskidaAtamaSayisi == 1, "Tam bir askıda atama bulgusu olmalı.");
    Assert(rapor.GecersizTipReferansiSayisi == 2, "Atama + finisaj kırık tip referansı (2) yakalanmalı.");
    Assert(rapor.Bulgular.Any(b => b.Tur == MutabakatBulguTuru.AskidaAtama && b.EtkilenenElemanId == "yok-eleman"),
        "Askıda atama doğru elemanı işaret etmeli.");
    Assert(rapor.Bulgular.Any(b => b.KaynakId == "FIN-BOZUK"), "Kırık finisaj profili bulguda görünmeli.");
    Assert(rapor.Bulgular.All(b => !string.IsNullOrWhiteSpace(b.KuralKodu) && !string.IsNullOrWhiteSpace(b.Aciklama)),
        "Her bulgu kural kodu ve açıklama taşımalı.");
}

static void Faz10ObjIhracati()
{
    // Null → yalnız başlık, geometri yok.
    var bos = MimariObjIhracatServisi.Olustur(null);
    Assert(bos.Contains("# EYE4PRO", StringComparison.Ordinal), "Boş ihracat başlık satırı içermeli.");
    Assert(!bos.Split('\n').Any(l => l.StartsWith("v ", StringComparison.Ordinal)), "Null projede vertex yazılmamalı.");

    var proje = MimariProjeServisi.YeniOlustur("Faz10 OBJ");
    proje.IhtiyacProgrami = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.Ofis, kapasite: 20, katSayisi: 1);
    proje.PlanTaslagi = PlanTaslagiUretimServisi.Uret(proje.IhtiyacProgrami, proje.Id);
    var model = Model3DUretimServisi.Uret(proje.PlanTaslagi);
    var mahalSay = model.Katlar.Sum(k => k.Mahaller.Count);
    Assert(mahalSay > 0, "Test için mahal üretilmeli.");

    // Yalnız mahaller: her mahal 8 vertex + 6 yüz.
    var objMahal = MimariObjIhracatServisi.Olustur(proje, duvarlariEkle: false);
    var mahalSatir = objMahal.Split('\n');
    var vMahal = mahalSatir.Count(l => l.StartsWith("v ", StringComparison.Ordinal));
    var fMahal = mahalSatir.Count(l => l.StartsWith("f ", StringComparison.Ordinal));
    Assert(vMahal == mahalSay * 8, "Mahal başına 8 vertex yazılmalı.");
    Assert(fMahal == mahalSay * 6, "Mahal başına 6 yüz yazılmalı.");
    Assert(objMahal.Contains("g mahal-", StringComparison.Ordinal), "Mahaller grup adıyla yazılmalı.");

    // Duvarlar dahil: daha fazla vertex, hepsi 8'in katı, grup adı var.
    var objTam = MimariObjIhracatServisi.Olustur(proje, duvarlariEkle: true);
    var tamSatir = objTam.Split('\n');
    var vTam = tamSatir.Count(l => l.StartsWith("v ", StringComparison.Ordinal));
    Assert(vTam > vMahal && vTam % 8 == 0, "Duvarlar eklenince vertex sayısı artmalı ve 8'in katı olmalı.");
    Assert(objTam.Contains("g duvar-", StringComparison.Ordinal), "Duvarlar grup adıyla yazılmalı.");

    // Tüm yüz indeksleri geçerli vertex aralığında [1, vTam] olmalı.
    foreach (var yuz in tamSatir.Where(l => l.StartsWith("f ", StringComparison.Ordinal)))
    {
        foreach (var parca in yuz[2..].Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = int.Parse(parca, System.Globalization.CultureInfo.InvariantCulture);
            Assert(idx >= 1 && idx <= vTam, $"Yüz indeksi geçerli vertex aralığında olmalı: {idx}");
        }
    }
}

static void Faz10RevizyonFarki()
{
    static BinaOda Oda(string id, string ad, double alan) => new() { Id = id, Ad = ad, AlanM2 = alan };
    static BinaKat Kat(int katNo, params BinaOda[] odalar)
    {
        var k = new BinaKat { Id = $"kat-{katNo}", KatNo = katNo };
        foreach (var o in odalar) k.Odalar.Add(o);
        return k;
    }

    var eski = new BinaGeometri { BinaAdi = "v1" };
    eski.Katlar.Add(Kat(1, Oda("A", "Ofis A", 20), Oda("B", "Ofis B", 30)));
    eski.Katlar.Add(Kat(2, Oda("C", "Depo", 40)));

    var yeni = new BinaGeometri { BinaAdi = "v2" };
    yeni.Katlar.Add(Kat(1, Oda("A", "Ofis A", 25), Oda("D", "Toplantı", 15))); // A alan değişti, B silindi, D eklendi
    yeni.Katlar.Add(Kat(3, Oda("E", "Arşiv", 10)));                            // kat 2 silindi, kat 3 eklendi, C silindi

    var fark = RevizyonFarkiServisi.Karsilastir(eski, yeni);

    Assert(fark.FarkVar, "Değişiklik içeren revizyonlarda fark olmalı.");
    Assert(fark.EklenenMahaller.Select(m => m.OdaId).OrderBy(x => x).SequenceEqual(["D", "E"]),
        "Eklenen mahaller D ve E olmalı.");
    Assert(fark.SilinenMahaller.Select(m => m.OdaId).OrderBy(x => x).SequenceEqual(["B", "C"]),
        "Silinen mahaller B ve C olmalı.");
    Assert(fark.DegisenMahaller.Count == 1 && fark.DegisenMahaller[0].OdaId == "A",
        "Yalnız A mahali değişmiş olmalı.");
    AssertClose(fark.DegisenMahaller[0].AlanDeltaM2, 5, "A mahali alan deltası 5 olmalı.");
    Assert(fark.EklenenKatlar.SequenceEqual([3]), "Kat 3 eklenmiş olmalı.");
    Assert(fark.SilinenKatlar.SequenceEqual([2]), "Kat 2 silinmiş olmalı.");
    AssertClose(fark.ToplamAlanDeltaM2, 50 - 90, "Toplam alan deltası yeni-eski olmalı.");

    // Aynı geometri ile fark yok.
    Assert(!RevizyonFarkiServisi.Karsilastir(yeni, yeni).FarkVar, "Aynı geometride fark olmamalı.");

    // Proje overload'ı: aynı projeyi karşılaştırınca fark yok (kararlı Id'ler).
    var proje = MimariProjeServisi.YeniOlustur("Faz10 Fark");
    proje.IhtiyacProgrami = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.Ofis, kapasite: 20, katSayisi: 1);
    proje.PlanTaslagi = PlanTaslagiUretimServisi.Uret(proje.IhtiyacProgrami, proje.Id);
    Model3DUretimServisi.Uret(proje.PlanTaslagi);
    Assert(!RevizyonFarkiServisi.Karsilastir(proje, proje).FarkVar,
        "Aynı mimari proje kendisiyle karşılaştırılınca fark üretmemeli.");
}

static void Faz9KopruDogrulamasi()
{
    var yol = Path.Combine(Path.GetTempPath(), $"mimari-kopru-{Guid.NewGuid():N}.e4bina.json");
    try
    {
        // Mimari modelden .e4bina üret ve diskten yükle (gerçek uçtan uca yol).
        var proje = MimariProjeServisi.YeniOlustur("Faz9 Köprü");
        proje.IhtiyacProgrami = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.Ofis, kapasite: 30, katSayisi: 2);
        proje.PlanTaslagi = PlanTaslagiUretimServisi.Uret(proje.IhtiyacProgrami, proje.Id);
        Model3DUretimServisi.Uret(proje.PlanTaslagi);
        BinaGeometriDisaAktarimServisi.DisaAktar(proje, yol);

        var geo = BinaGeometriServisi.Ac(yol);
        var katSayisi = geo.Katlar.Count;
        var odaSayisi = geo.Katlar.Sum(k => k.Odalar.Count);
        Assert(katSayisi > 0 && odaSayisi > 0, "Üretilen geometri kat ve oda içermeli.");

        // ── M12 köprüsü (BinaGeometriImportServisi) ──
        var proje12 = new Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BasitProjeCizProje();
        var sonuc12 = BinaGeometriImportServisi.Uygula(proje12, geo, yol);

        Assert(sonuc12.YeniKat == katSayisi, "M12 tüm katları yeni olarak almalı.");
        Assert(proje12.Katlar.Count == katSayisi, "M12 projesinde kat sayısı geometriyle eşleşmeli.");
        Assert(proje12.Katlar.Sum(k => k.Odalar.Count) == odaSayisi, "M12 oda sayısı geometriyle eşleşmeli.");
        Assert(sonuc12.Uyarilar.Count == 0, "Temiz geometride M12 mutabakat uyarısı olmamalı.");
        Assert(proje12.BinaGeometriRevizyon == geo.GeometriRevizyon, "M12 revizyon damgasını saklamalı.");
        var geoOdaIdleri = geo.Katlar.SelectMany(k => k.Odalar.Select(o => o.Id)).ToHashSet(StringComparer.Ordinal);
        var m12OdaIdleri = proje12.Katlar.SelectMany(k => k.Odalar.Select(o => o.Id)).ToHashSet(StringComparer.Ordinal);
        Assert(geoOdaIdleri.SetEquals(m12OdaIdleri), "M12 oda Id'lerini AYNEN korumalı (eleman bağlama için).");

        // ── M08 köprüsü (BinaGeometriMekanAdapteri) ──
        var proje08 = new Kst.Acoustics.Core.BuildingAcoustics.Models.BinaProje();
        var sonuc08 = Kst.Acoustics.Core.BuildingAcoustics.Geometry.BinaGeometriMekanAdapteri.Uygula(proje08, geo, yol);

        Assert(sonuc08.YeniKat == katSayisi, "M08 tüm katları yeni olarak almalı.");
        Assert(proje08.Katlar.Sum(k => k.Mekanlar.Count) == odaSayisi, "M08 mekan sayısı oda sayısıyla eşleşmeli.");

        // M08 Mekan.Id, BinaOda.Id'den deterministik türetilir → kararlı eşleşme.
        var ornekOdaId = geo.Katlar.First(k => k.Odalar.Count > 0).Odalar.First().Id;
        var beklenenMekanId = Kst.Acoustics.Core.BuildingAcoustics.Geometry.BinaGeometriMekanAdapteri.OdaIdToGuid(ornekOdaId);
        var tumMekanIdleri = proje08.Katlar.SelectMany(k => k.Mekanlar.Select(m => m.Id)).ToHashSet();
        Assert(tumMekanIdleri.Contains(beklenenMekanId),
            "M08 mekan Id'si oda Id'sinden deterministik türetilmeli (kararlı eşleşme).");
    }
    finally
    {
        if (File.Exists(yol))
            File.Delete(yol);
    }
}

static void Faz9IdKararliligi()
{
    var yol = Path.Combine(Path.GetTempPath(), $"mimari-id-{Guid.NewGuid():N}.e4mimari.json");
    try
    {
        var proje = MimariProjeServisi.YeniOlustur("Faz9 Id Kararlılığı");
        proje.IhtiyacProgrami = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.Ofis, kapasite: 30, katSayisi: 2);
        proje.PlanTaslagi = PlanTaslagiUretimServisi.Uret(proje.IhtiyacProgrami, proje.Id);
        Model3DUretimServisi.Uret(proje.PlanTaslagi);

        // Oda Id'leri kaynak mahal bloğu Id'siyle (kararlılık çapası) eşleşmeli ve benzersiz olmalı.
        var ilk = BinaGeometriAdapteri.Donustur(proje);
        var tumOdaIdleri = ilk.Katlar.SelectMany(k => k.Odalar.Select(o => o.Id)).ToList();
        Assert(tumOdaIdleri.Count > 0, "En az bir oda üretilmeli.");
        Assert(tumOdaIdleri.Distinct(StringComparer.Ordinal).Count() == tumOdaIdleri.Count,
            "Oda Id'leri benzersiz olmalı.");

        var kaynakBlokIdleri = proje.PlanTaslagi.Model3D!.Katlar
            .SelectMany(k => k.Mahaller.Select(m => m.KaynakMahalBloguId))
            .ToHashSet(StringComparer.Ordinal);
        Assert(tumOdaIdleri.All(id => kaynakBlokIdleri.Contains(id)),
            "Her oda Id'si kaynak mahal bloğu Id'siyle eşleşmeli.");

        var ilkImza = GeometriImza(ilk);

        // 1) Aynı plandan 3D modeli yeniden üret → Id'ler değişmemeli.
        Model3DUretimServisi.Uret(proje.PlanTaslagi);
        var yeniden = BinaGeometriAdapteri.Donustur(proje);
        Assert(GeometriImza(yeniden) == ilkImza, "3D model yeniden üretiminde oda/kat Id'leri korunmalı.");

        // 2) .e4mimari kaydet → aç → yeniden dışa aktar → Id'ler hâlâ aynı.
        MimariProjeServisi.Kaydet(proje, yol);
        var acilan = MimariProjeServisi.Ac(yol);
        var roundTrip = BinaGeometriAdapteri.Donustur(acilan);
        Assert(GeometriImza(roundTrip) == ilkImza, "Proje round-trip'inde oda/kat Id'leri korunmalı.");

        // Kat Id'leri de kararlı olmalı.
        var ilkKatIdleri = string.Join("|", ilk.Katlar.Select(k => k.Id));
        var rtKatIdleri = string.Join("|", roundTrip.Katlar.Select(k => k.Id));
        Assert(ilkKatIdleri == rtKatIdleri, "Kat Id'leri round-trip'te korunmalı.");
    }
    finally
    {
        if (File.Exists(yol))
            File.Delete(yol);
    }

    static string GeometriImza(Kst.Acoustics.Core.Bina.Models.BinaGeometri geo)
        => string.Join(";", geo.Katlar
            .OrderBy(k => k.KatNo)
            .Select(k => $"{k.Id}:{string.Join(",", k.Odalar.Select(o => o.Id).OrderBy(x => x, StringComparer.Ordinal))}"));
}

static void Faz9BinaGeometriDisaAktarimi()
{
    var yol = Path.Combine(Path.GetTempPath(), $"mimari-{Guid.NewGuid():N}.e4bina.json");
    try
    {
        var proje = MimariProjeServisi.YeniOlustur("Faz9 Dışa Aktarım");
        proje.IhtiyacProgrami = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.Ofis, kapasite: 20, katSayisi: 2);
        proje.PlanTaslagi = PlanTaslagiUretimServisi.Uret(proje.IhtiyacProgrami, proje.Id);
        var model = Model3DUretimServisi.Uret(proje.PlanTaslagi);

        // İlk dışa aktarım: yeni dosya üretilir, projeye bağlanır, revizyon 1.
        var sonuc = BinaGeometriDisaAktarimServisi.DisaAktar(proje, yol);
        Assert(sonuc.Durum == BinaDisaAktarimDurumu.Uretildi, "İlk dışa aktarım yeni dosya üretmeli.");
        Assert(sonuc.Yazildi, "İlk dışa aktarım dosyayı yazmış olmalı.");
        Assert(sonuc.GeometriRevizyon == 1, "İlk kayıtta geometri revizyonu 1 olmalı.");
        Assert(File.Exists(yol), ".e4bina dosyası diskte oluşmalı.");
        Assert(proje.BinaGeometriYolu == yol, "Proje bina geometrisi yoluna bağlanmalı.");

        // Diskten okunabilir ve kat/oda sayıları korunur.
        var acilan = BinaGeometriServisi.Ac(yol);
        Assert(acilan.BinaAdi == "Faz9 Dışa Aktarım", "Açılan geometri bina adını korumalı.");
        Assert(acilan.Katlar.Count == model.Katlar.Count, "Açılan geometri kat sayısını korumalı.");
        var beklenenOda = model.Katlar.Sum(k => k.Mahaller.Count);
        Assert(acilan.Katlar.Sum(k => k.Odalar.Count) == beklenenOda, "Açılan geometri oda sayısını korumalı.");

        // İkinci çağrı onaysız: üzerine yazma, yalnız bağla — dosya değişmemeli (revizyon 1 kalır).
        var bagla = BinaGeometriDisaAktarimServisi.DisaAktar(proje, yol);
        Assert(bagla.Durum == BinaDisaAktarimDurumu.BaglandiUzerineYazilmadi,
            "Onaysız ikinci çağrı üzerine yazmamalı, bağlamalı.");
        Assert(!bagla.Yazildi, "Bağlama çağrısı dosyayı yazmamalı.");
        Assert(BinaGeometriServisi.Ac(yol).GeometriRevizyon == 1,
            "Onaysız çağrıda dosya revizyonu değişmemeli.");

        // Onaylı çağrı: üzerine yazılır, revizyon artar.
        var uzerineYaz = BinaGeometriDisaAktarimServisi.DisaAktar(proje, yol, uzerineYaz: true);
        Assert(uzerineYaz.Durum == BinaDisaAktarimDurumu.UzerineYazildi, "Onaylı çağrı üzerine yazmalı.");
        Assert(uzerineYaz.GeometriRevizyon == 2, "Üzerine yazınca geometri revizyonu artmalı.");
    }
    finally
    {
        if (File.Exists(yol))
            File.Delete(yol);
    }
}

static void Faz9BinaGeometriAdaptoruDonusturur()
{
    // Null güvenli: boş ama adlı geometri.
    var bos = BinaGeometriAdapteri.Donustur(null);
    Assert(bos.Katlar.Count == 0, "Null projede kat üretilmemeli.");
    Assert(!string.IsNullOrWhiteSpace(bos.BinaAdi), "Boş geometride de bina adı dolmalı.");

    var proje = MimariProjeServisi.YeniOlustur("Faz9 Bina");
    proje.IhtiyacProgrami = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.Ofis, kapasite: 24, katSayisi: 2);
    proje.PlanTaslagi = PlanTaslagiUretimServisi.Uret(proje.IhtiyacProgrami, proje.Id);
    var model = Model3DUretimServisi.Uret(proje.PlanTaslagi);

    var kat1 = model.Katlar.OrderBy(k => k.KatNo).First();
    var duvar = kat1.Duvarlar.First();
    Aciklik3D Ac(string id, Aciklik3DTipi tip, double g, double y, double alt) => new()
    {
        Id = id,
        DuvarId = duvar.Id,
        KaynakMahalBloguId = duvar.KaynakMahalBloguId,
        Tip = tip,
        KatNo = duvar.KatNo,
        MerkezX = duvar.MerkezX,
        MerkezY = duvar.MerkezY,
        GenislikM = g,
        YukseklikM = y,
        AltKotuM = alt
    };
    kat1.Acikliklar.Add(Ac("ac-kapi", Aciklik3DTipi.Kapi, 0.9, 2.1, 0.0));
    kat1.Acikliklar.Add(Ac("ac-pencere", Aciklik3DTipi.Pencere, 1.4, 1.2, 0.9));
    kat1.Acikliklar.Add(Ac("ac-gecis", Aciklik3DTipi.Gecis, 1.0, 2.2, 0.0));

    var geometri = BinaGeometriAdapteri.Donustur(proje);

    Assert(geometri.BinaAdi == "Faz9 Bina", "Bina adı proje adından gelmeli.");
    Assert(geometri.Katlar.Count == model.Katlar.Count, "Her 3D kat bir BinaKat üretmeli.");

    var binaKat1 = geometri.Katlar.First(k => k.KatNo == kat1.KatNo);
    Assert(binaKat1.KatYuksekligiM > 0, "Kat yüksekliği pozitif olmalı.");
    Assert(binaKat1.Odalar.Count == kat1.Mahaller.Count, "Her mahal bir BinaOda üretmeli.");

    // Oda: dikdörtgen 4 köşe poligon + alan + kararlı Id (KaynakMahalBloguId).
    var ornekMahal = kat1.Mahaller.First();
    var binaOda = binaKat1.Odalar.FirstOrDefault(o => o.Id == ornekMahal.KaynakMahalBloguId)
        ?? throw new InvalidOperationException("Oda Id kaynak mahal bloğu Id'siyle eşleşmeli (kararlılık).");
    Assert(binaOda.Poligon.Count == 4, "Dikdörtgen mahal 4 köşeli poligona çevrilmeli.");
    AssertClose(binaOda.AlanM2, ornekMahal.AlanM2, "Oda alanı mahal alanıyla tutarlı olmalı.");
    Assert(binaOda.Ad == ornekMahal.Ad, "Oda adı korunmalı.");

    // Açıklıklar: Kapı/Pencere çevrilir, Geçiş atlanır.
    Assert(binaKat1.Acikliklar.Count == 2, "Yalnız kapı ve pencere açıklığı dışa aktarılmalı (geçiş atlanır).");
    var kapi = binaKat1.Acikliklar.First(a => a.Id == "ac-kapi");
    Assert(kapi.Tip == BinaAciklikTipi.Kapi, "Kapı tipi korunmalı.");
    AssertClose(kapi.GenislikM, 0.9, "Kapı genişliği korunmalı.");
    var pencere = binaKat1.Acikliklar.First(a => a.Id == "ac-pencere");
    Assert(pencere.Tip == BinaAciklikTipi.Pencere, "Pencere tipi korunmalı.");
    AssertClose(pencere.EsikYuksekligiM, 0.9, "Pencere eşik yüksekliği AltKotuM'den gelmeli.");

    // Bas/Bit segmenti genişlik kadar uzunlukta olmalı.
    var basBitUzunluk = Math.Sqrt(Math.Pow(kapi.Bit.X - kapi.Bas.X, 2) + Math.Pow(kapi.Bit.Y - kapi.Bas.Y, 2));
    AssertClose(basBitUzunluk, 0.9, "Açıklık Bas-Bit segment uzunluğu genişliğe eşit olmalı.");
}

static void M37XlsxCiktisiUretir()
{
    var proje = MimariProjeServisi.YeniOlustur("M37 XLSX Raporu");
    proje.IhtiyacProgrami = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.Ofis, kapasite: 20, katSayisi: 1);
    proje.PlanTaslagi = PlanTaslagiUretimServisi.Uret(proje.IhtiyacProgrami, proje.Id);
    Model3DUretimServisi.Uret(proje.PlanTaslagi);

    var rapor = MimariRaporUretimServisi.TumRaporuOlustur(proje);
    var xlsx = MimariRaporXlsxBuilder.Olustur(rapor);

    Assert(xlsx.Length > 0, "XLSX çıktısı boş olmamalı.");
    Assert(xlsx[0] == (byte)'P' && xlsx[1] == (byte)'K', "XLSX çıktısı ZIP (PK) imzasıyla başlamalı.");

    using var ms = new MemoryStream(xlsx);
    using var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read);

    string Oku(string yol)
    {
        var giris = zip.GetEntry(yol) ?? throw new InvalidOperationException($"XLSX paketinde eksik parça: {yol}");
        using var okuyucu = new StreamReader(giris.Open());
        return okuyucu.ReadToEnd();
    }

    foreach (var parca in new[] { "[Content_Types].xml", "_rels/.rels", "xl/workbook.xml", "xl/_rels/workbook.xml.rels", "xl/worksheets/sheet1.xml" })
    {
        Assert(zip.GetEntry(parca) is not null, $"XLSX paketi '{parca}' parçasını içermeli.");
    }

    var workbook = Oku("xl/workbook.xml");
    Assert(workbook.Contains("Mahal Listesi", StringComparison.Ordinal), "Workbook mahal listesi sayfasını içermeli.");
    Assert(workbook.Contains("Finisaj", StringComparison.Ordinal), "Workbook finisaj sayfasını içermeli.");

    var sheet1 = Oku("xl/worksheets/sheet1.xml");
    Assert(sheet1.Contains("Mahal No", StringComparison.Ordinal), "İlk sayfa mahal listesi başlığını içermeli.");
    Assert(sheet1.Contains("<v>", StringComparison.Ordinal), "İlk sayfa en az bir sayısal hücre içermeli.");

    // Beş tablo = beş sayfa.
    Assert(zip.GetEntry("xl/worksheets/sheet5.xml") is not null, "XLSX beş sayfa (tablo) içermeli.");

    // Boş projede de geçerli paket üretilmeli.
    var bosXlsx = MimariRaporXlsxBuilder.Olustur(MimariRaporUretimServisi.TumRaporuOlustur(null));
    Assert(bosXlsx.Length > 0 && bosXlsx[0] == (byte)'P', "Boş projede de geçerli XLSX üretilmeli.");
}

static void M37CiktiFormatlariUretir()
{
    var proje = MimariProjeServisi.YeniOlustur("M37 Çıktı Raporu");
    proje.IhtiyacProgrami = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.Ofis, kapasite: 24, katSayisi: 1);
    proje.PlanTaslagi = PlanTaslagiUretimServisi.Uret(proje.IhtiyacProgrami, proje.Id);
    Model3DUretimServisi.Uret(proje.PlanTaslagi);
    proje.Malzemeler = VarsayilanMimariMalzemeler.Tum.ToList();
    proje.YapiElemaniTipleri = VarsayilanYapiElemaniTipleri.Tum.ToList();
    proje.FinisajProfilleri = VarsayilanMahalFinisajProfilleri.Tum.ToList();

    var duvar = proje.PlanTaslagi.Model3D!.Katlar.First().Duvarlar.First();
    var pencere = new Aciklik3D
    {
        Id = "ac-rapor-pencere",
        KaynakMahalBloguId = duvar.KaynakMahalBloguId,
        DuvarId = duvar.Id,
        Tip = Aciklik3DTipi.Pencere,
        KatNo = duvar.KatNo,
        GenislikM = 1.4,
        YukseklikM = 1.2
    };
    proje.PlanTaslagi.Model3D!.Katlar.First().Acikliklar.Add(pencere);
    proje.MalzemeAtamalari.Add(new MalzemeAtamasi
    {
        KaynakElemanTuru = MalzemeAtamaHedefTuru.Pencere,
        KaynakElemanId = pencere.Id,
        KatNo = pencere.KatNo,
        YapiElemaniTipiId = VarsayilanYapiElemaniTipleri.VarsayilanPencereTipiId
    });

    var rapor = MimariRaporUretimServisi.TumRaporuOlustur(proje);
    Assert(rapor.MahalListesi.VeriVar && rapor.AlanTablosu.VeriVar && rapor.FinisajCizelgesi.VeriVar
        && rapor.KapiPencereCizelgesi.VeriVar,
        "Toplu rapor tüm temel tabloları doldurmalı.");

    // HTML çıktısı: tek belgede tüm bölümler.
    var html = MimariRaporHtmlBuilder.Olustur(rapor);
    var htmlMetin = System.Net.WebUtility.HtmlDecode(html);
    Assert(html.Contains("<html", StringComparison.OrdinalIgnoreCase), "HTML rapor kök etiketi içermeli.");
    Assert(htmlMetin.Contains("M37 Çıktı Raporu", StringComparison.Ordinal), "HTML rapor proje adını içermeli.");
    foreach (var baslik in new[] { "Mahal Listesi", "Alan Tablosu", "Kapı-Pencere Çizelgesi", "Finisaj Çizelgesi", "Ön Kontrol Raporu" })
    {
        Assert(htmlMetin.Contains(baslik, StringComparison.Ordinal), $"HTML rapor '{baslik}' bölümünü içermeli.");
    }
    Assert(htmlMetin.Contains("kesin uygunluk beyanı içermez", StringComparison.Ordinal),
        "HTML ön kontrol bölümü 'ön kontrol' dilini taşımalı.");

    // CSV çıktıları: her tablo ayrı, başlık satırıyla.
    var mahalCsv = MimariRaporCsvBuilder.MahalListesiCsv(rapor.MahalListesi);
    var alanCsv = MimariRaporCsvBuilder.AlanTablosuCsv(rapor.AlanTablosu);
    var kapiCsv = MimariRaporCsvBuilder.KapiPencereCsv(rapor.KapiPencereCizelgesi);
    var finisajCsv = MimariRaporCsvBuilder.FinisajCsv(rapor.FinisajCizelgesi);
    var onKontrolCsv = MimariRaporCsvBuilder.OnKontrolCsv(rapor.OnKontrol);

    Assert(mahalCsv.StartsWith("Kat;Mahal No;Ad;", StringComparison.Ordinal), "Mahal listesi CSV başlık satırıyla başlamalı.");
    Assert(kapiCsv.Contains("Poz;Tip;", StringComparison.Ordinal), "Kapı-pencere CSV başlığı olmalı.");
    Assert(finisajCsv.Contains("Zemin;Duvar;Tavan", StringComparison.Ordinal), "Finisaj CSV yüzey sütunları içermeli.");
    Assert(!string.IsNullOrWhiteSpace(alanCsv) && !string.IsNullOrWhiteSpace(onKontrolCsv),
        "Alan ve ön kontrol CSV çıktıları boş olmamalı.");
    Assert(mahalCsv.TrimEnd().Split('\n').Length >= 2, "Mahal listesi CSV en az başlık + bir satır içermeli.");

    // Boş projede de güvenli (en az başlık satırı).
    var bosRapor = MimariRaporUretimServisi.TumRaporuOlustur(null);
    var bosHtml = MimariRaporHtmlBuilder.Olustur(bosRapor);
    Assert(bosHtml.Contains("<html", StringComparison.OrdinalIgnoreCase), "Boş projede de HTML üretilmeli.");
    Assert(!string.IsNullOrWhiteSpace(MimariRaporCsvBuilder.MahalListesiCsv(bosRapor.MahalListesi)),
        "Boş projede CSV başlık satırı üretilmeli.");
}

static void M37OnKontrolRaporuUretir()
{
    Assert(!MimariRaporUretimServisi.OnKontrolRaporuOlustur(null).VeriVar,
        "Null projede ön kontrol raporu boş olmalı.");

    // Sorunlu proje: eksik ad/kapasite, geçersiz kat sayısı -> kritik + uyarı üretir.
    var sorunlu = new MimariProje
    {
        ProjeAdi = "",
        IhtiyacProgrami = new IhtiyacProgrami
        {
            BinaIslevi = BinaIslevi.Ofis,
            KullaniciKapasitesi = 0,
            KatSayisi = 0
        }
    };

    var rapor = MimariRaporUretimServisi.OnKontrolRaporuOlustur(sorunlu);

    Assert(rapor.VeriVar, "Sorunlu projede ön kontrol raporu satır üretmeli.");
    Assert(rapor.KritikVar, "Geçersiz kat sayısı kritik satır üretmeli.");
    Assert(rapor.SatirSayisi == rapor.KritikSayisi + rapor.UyariSayisi + rapor.KontrolEdilemediSayisi + rapor.BilgiSayisi,
        "Seviye sayıları toplamı satır sayısına eşit olmalı.");
    Assert(rapor.Satirlar.All(s => !string.IsNullOrWhiteSpace(s.KuralKodu) && !string.IsNullOrWhiteSpace(s.KaynakKodu)),
        "Her ön kontrol satırı kural ve kaynak kodu taşımalı.");
    Assert(rapor.Satirlar[0].KritikMi, "Rapor kritik satırları önce sıralamalı.");
    Assert(rapor.Satirlar.Any(s => s.KuralKodu == "M34-KAT-SAYISI-GECERSIZ"),
        "Rapor geçersiz kat sayısı kuralını içermeli.");

    // Geçerli üretilmiş ofis programı kritik üretmemeli.
    var temiz = MimariProjeServisi.YeniOlustur("Temiz Ofis Raporu");
    temiz.IhtiyacProgrami = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.Ofis, kapasite: 25, katSayisi: 2);
    var temizRapor = MimariRaporUretimServisi.OnKontrolRaporuOlustur(temiz);
    Assert(!temizRapor.KritikVar, "Geçerli ofis programı kritik ön kontrol satırı üretmemeli.");
}

static void M37FinisajCizelgesiUretir()
{
    Assert(!MimariRaporUretimServisi.FinisajCizelgesiOlustur(null).VeriVar,
        "Null projede finisaj çizelgesi boş olmalı.");

    var proje = MimariProjeServisi.YeniOlustur("M37 Finisaj Çizelgesi");
    proje.IhtiyacProgrami = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.Ofis, kapasite: 24, katSayisi: 1);
    proje.PlanTaslagi = PlanTaslagiUretimServisi.Uret(proje.IhtiyacProgrami, proje.Id);
    Model3DUretimServisi.Uret(proje.PlanTaslagi);
    proje.YapiElemaniTipleri = VarsayilanYapiElemaniTipleri.Tum.ToList();
    proje.FinisajProfilleri = VarsayilanMahalFinisajProfilleri.Tum.ToList();

    var mahalListesi = MimariRaporUretimServisi.MahalListesiOlustur(proje);
    var cizelge = MimariRaporUretimServisi.FinisajCizelgesiOlustur(proje);

    Assert(cizelge.VeriVar, "Dolu projede finisaj çizelgesi veri taşımalı.");
    Assert(cizelge.SatirSayisi == mahalListesi.SatirSayisi,
        "Finisaj çizelgesi her mahal için bir satır üretmeli.");
    Assert(cizelge.Satirlar.All(s => s.TamFinisajli),
        "Her mahal zemin/duvar/tavan finisaj önerisi taşımalı (genel yedek dahil).");

    var islakSatirlar = cizelge.Satirlar.Where(s => s.IslakHacim).ToList();
    Assert(islakSatirlar.Count >= 1, "Ofis programında en az bir ıslak hacim (WC) finisaj satırı olmalı.");
    Assert(islakSatirlar.All(s => !string.IsNullOrWhiteSpace(s.ZeminTipiAdi) && !string.IsNullOrWhiteSpace(s.Not)),
        "Islak hacim satırları zemin tipi ve uyarı notu taşımalı.");

    Assert(cizelge.Satirlar.Any(s => s.TavanTipiAdi.Contains("Akustik", StringComparison.OrdinalIgnoreCase)),
        "Ofis/toplantı mahallerinde akustik tavan önerisi görünmeli.");
}

static void KatlarArasiSureklilikUyarilariUretir()
{
    var plan = SureklilikSorunluPlan();
    var uyarilar = SirkulasyonKontrolServisi.KatlarArasiSureklilikUyarilari(plan, aksToleransiM: 0.10);

    Assert(!uyarilar.Any(u => u.KuralKodu == "M34-CEKIRDEK-YOK"),
        "Çekirdek bulunan planda çekirdek yok uyarısı üretilmemeli.");
    Assert(uyarilar.Any(u =>
            u.KuralKodu == "M34-CEKIRDEK-SUREKLILIK-KOPUK" &&
            u.AksKodu == "A-1" &&
            u.EtkilenenElemanId == "cek-a1-1"),
        "Aynı akstaki çekirdek eksik kat için süreklilik uyarısı üretmeli.");
    Assert(uyarilar.Any(u =>
            u.KuralKodu == "M34-SAFT-AKS-KAYMASI" &&
            u.AksKodu == "S-1" &&
            u.EtkilenenElemanId == "saft-s1-2"),
        "Aynı aks kodundaki şaft merkez kayması uyarı üretmeli.");
    Assert(uyarilar.Any(u =>
            u.KuralKodu == "M34-CEKIRDEK-AKS-KODU-EKSIK" &&
            u.EtkilenenElemanId == "cek-asansor-akssiz"),
        "Çok katlı çekirdekte eksik aks kodu uyarı üretmeli.");
}

static void CekirdekSureklilikUyarilariniPlanKontroluneTasir()
{
    var plan = SureklilikSorunluPlan();
    var sonuclar = YonetmelikOnKontrolServisi.PlanTaslagiKontrolleriniCalistir(plan);

    Assert(sonuclar.Any(s =>
            s.KuralKodu == "M34-CEKIRDEK-SUREKLILIK-KOPUK" &&
            s.Seviye == KontrolSeviyesi.Uyari &&
            s.KaynakKodu == YonetmelikOnKontrolServisi.VarsayilanVeriKaynakKodu &&
            s.EtkilenenElemanId == "cek-a1-1"),
        "M34 plan kontrolü çekirdek süreklilik uyarısını taşımalı.");
    Assert(sonuclar.Any(s =>
            s.KuralKodu == "M34-SAFT-AKS-KAYMASI" &&
            s.EtkilenenElemanId == "saft-s1-2"),
        "M34 plan kontrolü şaft aks kayması uyarısını taşımalı.");

    var kaynak = VarsayilanYonetmelikKaynakProfilleri.KuralKaynagiBul("M34-CEKIRDEK-SUREKLILIK-KOPUK");
    if (kaynak is null)
        throw new InvalidOperationException("Çekirdek süreklilik kuralı kaynak profiline bağlı olmalı.");

    Assert(kaynak.Kod == YonetmelikOnKontrolServisi.VarsayilanVeriKaynakKodu,
        "Çekirdek süreklilik uyarısı M34 veri kaynak profiline bağlanmalı.");
}

static PlanTaslagi SureklilikSorunluPlan()
{
    var plan = new PlanTaslagi
    {
        ProjeId = "sureklilik-test",
        Katlar =
        [
            new KatTaslagi { KatNo = 1, Ad = "Kat 1", Sinir = new PlanSiniri { GenislikM = 20, DerinlikM = 12 } },
            new KatTaslagi { KatNo = 2, Ad = "Kat 2", Sinir = new PlanSiniri { GenislikM = 20, DerinlikM = 12 } },
            new KatTaslagi { KatNo = 3, Ad = "Kat 3", Sinir = new PlanSiniri { GenislikM = 20, DerinlikM = 12 } }
        ]
    };

    var kat1 = plan.KatBul(1) ?? throw new InvalidOperationException("Kat 1 bulunmalı.");
    var kat2 = plan.KatBul(2) ?? throw new InvalidOperationException("Kat 2 bulunmalı.");
    var kat3 = plan.KatBul(3) ?? throw new InvalidOperationException("Kat 3 bulunmalı.");

    kat1.Cekirdekler.Add(new CekirdekElemani
    {
        Id = "cek-a1-1",
        Ad = "A1 Alt Merdiven",
        Tip = CekirdekTipi.Merdiven,
        KatNo = 1,
        BaslangicKatNo = 1,
        BitisKatNo = 1,
        X = 2,
        Y = 2,
        GenislikM = 2,
        DerinlikM = 4,
        AksKodu = "A-1"
    });
    kat3.Cekirdekler.Add(new CekirdekElemani
    {
        Id = "cek-a1-3",
        Ad = "A1 Üst Merdiven",
        Tip = CekirdekTipi.Merdiven,
        KatNo = 3,
        BaslangicKatNo = 3,
        BitisKatNo = 3,
        X = 2,
        Y = 2,
        GenislikM = 2,
        DerinlikM = 4,
        AksKodu = "A-1"
    });
    kat1.Cekirdekler.Add(new CekirdekElemani
    {
        Id = "cek-asansor-akssiz",
        Ad = "Aksı Eksik Asansör",
        Tip = CekirdekTipi.Asansor,
        KatNo = 1,
        BaslangicKatNo = 1,
        BitisKatNo = 3,
        X = 6,
        Y = 2,
        GenislikM = 2,
        DerinlikM = 2
    });

    kat1.Saftlar.Add(new SaftTaslagi
    {
        Id = "saft-s1-1",
        Ad = "S1 Alt Şaft",
        TesisatTuru = SaftTesisatTuru.SihhiTesisat,
        KatNo = 1,
        BaslangicKatNo = 1,
        BitisKatNo = 1,
        X = 10,
        Y = 2,
        GenislikM = 1,
        DerinlikM = 1,
        AksKodu = "S-1"
    });
    kat2.Saftlar.Add(new SaftTaslagi
    {
        Id = "saft-s1-2",
        Ad = "S1 Kaymış Şaft",
        TesisatTuru = SaftTesisatTuru.SihhiTesisat,
        KatNo = 2,
        BaslangicKatNo = 2,
        BitisKatNo = 2,
        X = 11,
        Y = 2,
        GenislikM = 1,
        DerinlikM = 1,
        AksKodu = "S-1"
    });

    return plan;
}

static void IslakHacimSaftIliskisiUyarilariUretir()
{
    var plan = IslakHacimSaftSorunluPlan();
    var uyarilar = SirkulasyonKontrolServisi.IslakHacimSaftUyarilari(plan, maksSaftUzaklikM: 3.0);

    Assert(!uyarilar.Any(u => u.EtkilenenMahalId == "wc-yakin"),
        "Yakındaki WC için şaft uyarısı üretilmemeli.");
    Assert(uyarilar.Any(u =>
            u.KuralKodu == "M34-ISLAK-HACIM-SAFT-UZAK" &&
            u.EtkilenenMahalId == "mutfak-uzak" &&
            u.EtkilenenElemanId == "saft-kat1"),
        "Şafttan uzak ıslak hacim mesafe uyarısı üretmeli.");
    Assert(uyarilar.Any(u =>
            u.KuralKodu == "M34-ISLAK-HACIM-SAFT-YOK" &&
            u.EtkilenenMahalId == "banyo-saftsiz"),
        "Şaft olmayan kattaki ıslak hacim şaft yok uyarısı üretmeli.");
}

static void IslakHacimSaftUyarilariniPlanKontroluneTasir()
{
    var plan = IslakHacimSaftSorunluPlan();
    var sonuclar = YonetmelikOnKontrolServisi.PlanTaslagiKontrolleriniCalistir(plan);

    Assert(sonuclar.Any(s =>
            s.KuralKodu == "M34-ISLAK-HACIM-SAFT-UZAK" &&
            s.Seviye == KontrolSeviyesi.Uyari &&
            s.KaynakKodu == YonetmelikOnKontrolServisi.VarsayilanVeriKaynakKodu &&
            s.EtkilenenMahalId == "mutfak-uzak" &&
            s.EtkilenenElemanId == "saft-kat1"),
        "M34 plan kontrolü uzak ıslak hacim/şaft uyarısını taşımalı.");
    Assert(sonuclar.Any(s =>
            s.KuralKodu == "M34-ISLAK-HACIM-SAFT-YOK" &&
            s.EtkilenenMahalId == "banyo-saftsiz"),
        "M34 plan kontrolü şaft yok uyarısını taşımalı.");

    var kaynak = VarsayilanYonetmelikKaynakProfilleri.KuralKaynagiBul("M34-ISLAK-HACIM-SAFT-UZAK");
    if (kaynak is null)
        throw new InvalidOperationException("Islak hacim şaft uyarısı kaynak profiline bağlı olmalı.");

    Assert(kaynak.Kod == YonetmelikOnKontrolServisi.VarsayilanVeriKaynakKodu,
        "Islak hacim şaft uyarısı M34 veri kaynak profiline bağlanmalı.");
}

static PlanTaslagi IslakHacimSaftSorunluPlan()
{
    var plan = new PlanTaslagi
    {
        ProjeId = "islak-saft-test",
        Katlar =
        [
            new KatTaslagi { KatNo = 1, Ad = "Kat 1", Sinir = new PlanSiniri { GenislikM = 30, DerinlikM = 12 } },
            new KatTaslagi { KatNo = 2, Ad = "Kat 2", Sinir = new PlanSiniri { GenislikM = 30, DerinlikM = 12 } }
        ]
    };

    var kat1 = plan.KatBul(1) ?? throw new InvalidOperationException("Kat 1 bulunmalı.");
    var kat2 = plan.KatBul(2) ?? throw new InvalidOperationException("Kat 2 bulunmalı.");

    kat1.Bloklar.Add(new MahalBlogu
    {
        Id = "wc-yakin",
        Ad = "Yakın WC",
        MahalTipi = MahalTipi.Wc,
        KullanimSinifi = KullanimSinifi.IslakHacim,
        X = 0,
        Y = 0,
        GenislikM = 2,
        DerinlikM = 2,
        HedefAlanM2 = 4,
        KatNo = 1
    });
    kat1.Bloklar.Add(new MahalBlogu
    {
        Id = "mutfak-uzak",
        Ad = "Uzak Mutfak",
        MahalTipi = MahalTipi.Mutfak,
        KullanimSinifi = KullanimSinifi.IslakHacim,
        X = 20,
        Y = 0,
        GenislikM = 3,
        DerinlikM = 3,
        HedefAlanM2 = 9,
        KatNo = 1
    });
    kat1.Saftlar.Add(new SaftTaslagi
    {
        Id = "saft-kat1",
        Ad = "Kat 1 Sıhhi Şaft",
        TesisatTuru = SaftTesisatTuru.SihhiTesisat,
        KatNo = 1,
        BaslangicKatNo = 1,
        BitisKatNo = 1,
        X = 2.2,
        Y = 0,
        GenislikM = 1,
        DerinlikM = 1,
        AksKodu = "S-1"
    });

    kat2.Bloklar.Add(new MahalBlogu
    {
        Id = "banyo-saftsiz",
        Ad = "Şaftsız Banyo",
        MahalTipi = MahalTipi.Banyo,
        KullanimSinifi = KullanimSinifi.IslakHacim,
        X = 0,
        Y = 0,
        GenislikM = 2,
        DerinlikM = 2,
        HedefAlanM2 = 4,
        KatNo = 2
    });

    return plan;
}

static void KacisGrafiMahalKoridorMerdivenBaglantisiUretir()
{
    var plan = KacisGrafiBagliPlan();
    var graf = SirkulasyonKontrolServisi.KacisGrafiUret(plan);

    Assert(ReferenceEquals(plan.KacisGrafi, graf), "Üretilen kaçış grafı plan taslağı üzerinde tutulmalı.");
    Assert(graf.PlanTaslagiId == plan.Id, "Kaçış grafı plan taslağı Id'sini taşımalı.");
    Assert(graf.DugumSayisi == 3, "Bağlı test planında mahal, koridor ve merdiven düğümleri oluşmalı.");
    Assert(graf.KenarSayisi == 2, "Bağlı test planında mahal-koridor ve koridor-merdiven kenarları oluşmalı.");
    Assert(graf.MahalDugumleri.Count == 1, "Kaçış grafı mahal düğümünü sınıflamalı.");
    Assert(graf.MerdivenDugumleri.Count == 1, "Kaçış grafı merdiven hedef düğümünü sınıflamalı.");
    Assert(graf.Kenarlar.Any(k => k.Tip == KacisKenariTipi.MahalKoridor && k.MesafeM > 0),
        "Mahal-koridor kenarı mesafe taşımalı.");
    Assert(graf.Kenarlar.Any(k => k.Tip == KacisKenariTipi.KoridorMerdiven && k.MesafeM > 0),
        "Koridor-merdiven kenarı mesafe taşımalı.");
    Assert(!SirkulasyonKontrolServisi.KacisGrafiUyarilari(plan)
            .Any(u => u.KuralKodu == "M34-KACIS-GRAFI-MERDIVENE-ULASAMAYAN-MAHAL"),
        "Merdivene bağlanan mahal için kaçış grafı uyarısı üretilmemeli.");
}

static void KacisGrafiMerdiveneUlasamayanMahalUyarisiUretir()
{
    var plan = KacisGrafiKopukPlan();
    var uyarilar = SirkulasyonKontrolServisi.KacisGrafiUyarilari(plan);

    Assert(uyarilar.Any(u =>
            u.KuralKodu == "M34-KACIS-GRAFI-MERDIVENE-ULASAMAYAN-MAHAL" &&
            u.EtkilenenMahalId == "mah-kopuk"),
        "Koridora bağlı ama merdivene ulaşamayan mahal kaçış grafı uyarısı üretmeli.");
    Assert(!uyarilar.Any(u => u.EtkilenenMahalId == "mah-bagli"),
        "Merdivene ulaşan mahal için kaçış grafı uyarısı üretilmemeli.");

    var sonuclar = YonetmelikOnKontrolServisi.PlanTaslagiKontrolleriniCalistir(plan);
    Assert(sonuclar.Any(s =>
            s.KuralKodu == "M34-KACIS-GRAFI-MERDIVENE-ULASAMAYAN-MAHAL" &&
            s.Seviye == KontrolSeviyesi.Uyari &&
            s.KaynakKodu == YonetmelikOnKontrolServisi.VarsayilanVeriKaynakKodu &&
            s.EtkilenenMahalId == "mah-kopuk"),
        "M34 plan kontrolü kaçış grafı mahal uyarısını taşımalı.");

    var kaynak = VarsayilanYonetmelikKaynakProfilleri.KuralKaynagiBul("M34-KACIS-GRAFI-MERDIVENE-ULASAMAYAN-MAHAL");
    if (kaynak is null)
        throw new InvalidOperationException("Kaçış grafı uyarısı kaynak profiline bağlı olmalı.");

    Assert(kaynak.Kod == YonetmelikOnKontrolServisi.VarsayilanVeriKaynakKodu,
        "Kaçış grafı uyarısı M34 veri kaynak profiline bağlanmalı.");
}

static PlanTaslagi KacisGrafiBagliPlan()
{
    var plan = new PlanTaslagi
    {
        ProjeId = "kacis-grafi-bagli",
        Katlar =
        [
            new KatTaslagi
            {
                KatNo = 1,
                Ad = "Kat 1",
                Sinir = new PlanSiniri { GenislikM = 24, DerinlikM = 12 }
            }
        ]
    };

    var kat = plan.KatBul(1) ?? throw new InvalidOperationException("Kat 1 bulunmalı.");
    kat.Bloklar.Add(new MahalBlogu
    {
        Id = "mah-bagli",
        Ad = "Bağlı Ofis",
        MahalTipi = MahalTipi.Ofis,
        KullanimSinifi = KullanimSinifi.AnaKullanim,
        X = 0,
        Y = 0,
        GenislikM = 4,
        DerinlikM = 3,
        HedefAlanM2 = 12,
        KatNo = 1
    });

    var koridor = new KoridorTaslagi
    {
        Id = "kor-ana",
        Ad = "Ana Koridor",
        BaslangicX = 0,
        BaslangicY = 5,
        BitisX = 12,
        BitisY = 5,
        GenislikM = 1.5,
        BagliBlokIdleri = ["mah-bagli"]
    };
    kat.Koridorlar.Add(koridor);
    kat.Cekirdekler.Add(new CekirdekElemani
    {
        Id = "merdiven-ana",
        Ad = "Ana Merdiven",
        Tip = CekirdekTipi.Merdiven,
        KatNo = 1,
        BaslangicKatNo = 1,
        BitisKatNo = 1,
        X = 13,
        Y = 3,
        GenislikM = 2,
        DerinlikM = 4,
        AksKodu = "M-1",
        BagliKoridorIdleri = [koridor.Id]
    });

    return plan;
}

static PlanTaslagi KacisGrafiKopukPlan()
{
    var plan = KacisGrafiBagliPlan();
    plan.ProjeId = "kacis-grafi-kopuk";
    var kat = plan.KatBul(1) ?? throw new InvalidOperationException("Kat 1 bulunmalı.");

    kat.Bloklar.Add(new MahalBlogu
    {
        Id = "mah-kopuk",
        Ad = "Kopuk Toplantı",
        MahalTipi = MahalTipi.ToplantiOdasi,
        KullanimSinifi = KullanimSinifi.YardimciKullanim,
        X = 18,
        Y = 0,
        GenislikM = 4,
        DerinlikM = 3,
        HedefAlanM2 = 12,
        KatNo = 1
    });
    kat.Koridorlar.Add(new KoridorTaslagi
    {
        Id = "kor-kopuk",
        Ad = "Kopuk Koridor",
        BaslangicX = 18,
        BaslangicY = 5,
        BitisX = 23,
        BitisY = 5,
        GenislikM = 1.5,
        BagliBlokIdleri = ["mah-kopuk"]
    });

    return plan;
}

static MahalBlogu Blok(string id, string ad, MahalTipi tip, KullanimSinifi sinif)
    => new()
    {
        Id = id,
        MahalGereksinimiId = $"{id}-gereksinim",
        Ad = ad,
        MahalTipi = tip,
        KullanimSinifi = sinif,
        HedefAlanM2 = 20,
        GenislikM = 4,
        DerinlikM = 5
    };

static bool IliskiVar(
    IEnumerable<MahalIliskisi> iliskiler,
    MahalBlogu kaynak,
    MahalBlogu? hedef,
    IliskiTipi tip)
{
    return iliskiler.Any(i =>
        i.IliskiTipi == tip &&
        (
            hedef is null
                ? i.KaynakMahalId == kaynak.Id && string.IsNullOrWhiteSpace(i.HedefMahalId)
                : (i.KaynakMahalId == kaynak.Id && i.HedefMahalId == hedef.Id) ||
                  (i.KaynakMahalId == hedef.Id && i.HedefMahalId == kaynak.Id)
        ));
}

static string MinMax(string kaynakId, string hedefId)
{
    if (string.Compare(kaynakId, hedefId, StringComparison.OrdinalIgnoreCase) <= 0)
        return $"{kaynakId}>{hedefId}";

    return $"{hedefId}>{kaynakId}";
}

static MimariProje YeniDogrulamaProjesi()
    => new()
    {
        ProjeAdi = "Doğrulama Testi",
        IhtiyacProgrami = new IhtiyacProgrami()
    };

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static void AssertClose(double actual, double expected, string message)
{
    if (Math.Abs(actual - expected) > 1e-9)
        throw new InvalidOperationException($"{message} Beklenen: {expected}, Gerçek: {actual}");
}

// ── M60 Terrain — DXF arazi parser (F1) ──────────────────────────────────────

static DxfDocument YeniAraziDoc(DrawingUnits birim)
{
    var doc = new DxfDocument();
    doc.DrawingVariables.InsUnits = birim;
    return doc;
}

static void M60SpotKotOkunur()
{
    var doc = YeniAraziDoc(DrawingUnits.Meters);
    doc.Entities.Add(new Point(new Vector3(10, 20, 5.5)));

    var sonuc = new DxfAraziParser().Parse(doc);

    Assert(sonuc.Arazi.Noktalar.Count == 1, "Bir spot kot beklenir.");
    var n = sonuc.Arazi.Noktalar[0];
    AssertClose(n.X, 10, "Spot kot X.");
    AssertClose(n.Y, 20, "Spot kot Y.");
    AssertClose(n.Z, 5.5, "Spot kot Z.");
}

static void M60Kontur3DVertexZ()
{
    var doc = YeniAraziDoc(DrawingUnits.Meters);
    doc.Entities.Add(new Polyline3D(new[]
    {
        new Vector3(0, 0, 540), new Vector3(10, 0, 540), new Vector3(10, 10, 540)
    }));

    var sonuc = new DxfAraziParser().Parse(doc);

    Assert(sonuc.Arazi.Konturlar.Count == 1, "Bir kontur beklenir.");
    var k = sonuc.Arazi.Konturlar[0];
    Assert(k.KotKaynagi == AraziKotKaynagi.VertexZ, $"Kot kaynağı VertexZ olmalı, geldi: {k.KotKaynagi}");
    AssertClose(k.KotM, 540, "Kontur kotu (vertex Z).");
    Assert(k.Noktalar.Count == 3, "Kontur 3 noktalı olmalı.");
}

static void M60Kontur2DElevation()
{
    var doc = YeniAraziDoc(DrawingUnits.Meters);
    var p = new Polyline2D(new[] { new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10) })
    {
        Elevation = 560
    };
    doc.Entities.Add(p);

    var sonuc = new DxfAraziParser().Parse(doc);

    Assert(sonuc.Arazi.Konturlar.Count == 1, "Bir kontur beklenir.");
    var k = sonuc.Arazi.Konturlar[0];
    Assert(k.KotKaynagi == AraziKotKaynagi.DuzlemKotu, $"Kot kaynağı DuzlemKotu olmalı, geldi: {k.KotKaynagi}");
    AssertClose(k.KotM, 560, "Kontur kotu (Elevation).");
}

static void M60KonturKatmanAdindanKot()
{
    var doc = YeniAraziDoc(DrawingUnits.Meters);
    var p = new Polyline2D(new[] { new Vector2(0, 0), new Vector2(10, 0) })
    {
        Layer = new Layer("KONTUR_520")
    };
    doc.Entities.Add(p);

    var sonuc = new DxfAraziParser().Parse(doc);

    var k = sonuc.Arazi.Konturlar[0];
    Assert(k.KotKaynagi == AraziKotKaynagi.KatmanAdi, $"Kot kaynağı KatmanAdi olmalı, geldi: {k.KotKaynagi}");
    AssertClose(k.KotM, 520, "Kontur kotu (katman adı).");
}

static void M60KonturMetinEtiketinden()
{
    var doc = YeniAraziDoc(DrawingUnits.Meters);
    // Kotsuz polyline (varsayılan "0" katmanı), yakınında "580" metni.
    doc.Entities.Add(new Polyline2D(new[] { new Vector2(0, 0), new Vector2(10, 0) }));
    doc.Entities.Add(new Text("580", new Vector2(1, 1), 2.0));

    var sonuc = new DxfAraziParser().Parse(doc);

    var k = sonuc.Arazi.Konturlar[0];
    Assert(k.KotKaynagi == AraziKotKaynagi.MetinEtiketi, $"Kot kaynağı MetinEtiketi olmalı, geldi: {k.KotKaynagi}");
    AssertClose(k.KotM, 580, "Kontur kotu (metin etiketi).");
}

static void M60BirimOlcekleme()
{
    var doc = YeniAraziDoc(DrawingUnits.Millimeters);
    doc.Entities.Add(new Point(new Vector3(10000, 20000, 540000)));

    var sonuc = new DxfAraziParser().Parse(doc);

    var n = sonuc.Arazi.Noktalar[0];
    AssertClose(n.X, 10, "mm→m X.");
    AssertClose(n.Y, 20, "mm→m Y.");
    AssertClose(n.Z, 540, "mm→m Z.");
}

// ── M60 Terrain — TIN / DTM (F2) ─────────────────────────────────────────────

static AraziModeli AraziNoktalarla(params (double x, double y, double z)[] noktalar)
{
    var a = new AraziModeli();
    foreach (var (x, y, z) in noktalar)
        a.Noktalar.Add(new AraziNoktasi(x, y, z));
    return a;
}

static void M60YuzeyDuzlemSabit()
{
    var arazi = AraziNoktalarla((0, 0, 100), (100, 0, 100), (100, 100, 100), (0, 100, 100), (50, 50, 100));
    var yuzey = new AraziYuzeyServisi().Olustur(arazi);

    Assert(yuzey.Ucgenler.Count > 0, "TIN üçgen üretmeli.");
    var z = yuzey.KotSorgula(30, 40);
    Assert(z.HasValue, "İç nokta yüzeyde olmalı.");
    AssertClose(z!.Value, 100, "Sabit düzlemde kot 100 olmalı.");
}

static void M60YuzeyEgikDuzlem()
{
    // z = 2x + 3y + 10 — TIN bir düzlemi tam (hatasız) interpole eder.
    double Z(double x, double y) => 2 * x + 3 * y + 10;
    var arazi = AraziNoktalarla(
        (0, 0, Z(0, 0)), (100, 0, Z(100, 0)), (100, 100, Z(100, 100)),
        (0, 100, Z(0, 100)), (60, 20, Z(60, 20)));
    var yuzey = new AraziYuzeyServisi().Olustur(arazi);

    var z = yuzey.KotSorgula(37, 44);
    Assert(z.HasValue, "İç nokta yüzeyde olmalı.");
    AssertClose(z!.Value, Z(37, 44), "Eğik düzlem interpolasyonu tam olmalı.");
}

static void M60YuzeyDisNokta()
{
    var arazi = AraziNoktalarla((0, 0, 10), (10, 0, 10), (10, 10, 10), (0, 10, 10));
    var yuzey = new AraziYuzeyServisi().Olustur(arazi);

    Assert(yuzey.KotSorgula(500, 500) is null, "Yüzey dışı sorgu null dönmeli.");
}

static void M60YuzeyKonturInterpolasyon()
{
    // İki yatay kontur: y=0 kot 500, y=100 kot 510. Orta nokta ~505 olmalı.
    var arazi = new AraziModeli();
    arazi.Konturlar.Add(new AraziKonturu
    {
        KotM = 500, KotKaynagi = AraziKotKaynagi.VertexZ,
        Noktalar = { new(0, 0), new(100, 0) }
    });
    arazi.Konturlar.Add(new AraziKonturu
    {
        KotM = 510, KotKaynagi = AraziKotKaynagi.VertexZ,
        Noktalar = { new(0, 100), new(100, 100) }
    });

    var yuzey = new AraziYuzeyServisi { KonturOrneklemeAraligiM = 10 }.Olustur(arazi);
    var z = yuzey.KotSorgula(50, 50);
    Assert(z.HasValue, "Konturlar arası nokta yüzeyde olmalı.");
    AssertClose(z!.Value, 505, "Konturlar arası lineer interpolasyon 505 olmalı.");
}

static void M60YuzeyYetersizNokta()
{
    var arazi = AraziNoktalarla((0, 0, 5), (10, 0, 5));
    var yuzey = new AraziYuzeyServisi().Olustur(arazi);
    Assert(yuzey.Ucgenler.Count == 0 && yuzey.Dugumler.Count == 0, "3'ten az noktada boş yüzey beklenir.");
}

// ── M60 Terrain — Hafriyat / grid yarma-dolgu (F4-F5) ────────────────────────

// Kenarı 0..size olan kare TIN; kot z = zFunc(x,y).
static AraziYuzeyi KareYuzey(double size, Func<double, double, double> zFunc)
{
    var dugumler = new List<AraziDugum>
    {
        new(0, 0, zFunc(0, 0)),
        new(size, 0, zFunc(size, 0)),
        new(size, size, zFunc(size, size)),
        new(0, size, zFunc(0, size)),
    };
    return new AraziYuzeyServisi().Ucgenle(dugumler);
}

// Köşeleri (x0,y0)-(x1,y1) olan dikdörtgen platform.
static TasarimYuzeyi DikdortgenPlatform(double x0, double y0, double x1, double y1, double hedefKot)
{
    var t = new TasarimYuzeyi();
    t.Bolgeler.Add(new TasarimBolgesi
    {
        Tip = TasarimBolgeTipi.Platform,
        HedefKotM = hedefKot,
        Poligon = { new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1) }
    });
    return t;
}

static void M60HafriyatSafYarma()
{
    var mevcut = KareYuzey(10, (_, _) => 100);          // düz zemin 100
    var tasarim = DikdortgenPlatform(0, 0, 10, 10, 90); // hedef 90 → 10 m kazı
    var s = new HafriyatHesabiServisi { GridAraligiM = 1.0 }.Hesapla(mevcut, tasarim);

    AssertClose(s.YarmaHacmiM3, 1000, "Yarma 10×10×10 = 1000 m³ olmalı.");
    AssertClose(s.DolguHacmiM3, 0, "Dolgu olmamalı.");
    AssertClose(s.NetHacimM3, 1000, "Net = yarma.");
    Assert(s.YarmaHucreSayisi == 100 && s.DolguHucreSayisi == 0, "100 yarma hücresi beklenir.");
}

static void M60HafriyatSafDolgu()
{
    var mevcut = KareYuzey(10, (_, _) => 100);
    var tasarim = DikdortgenPlatform(0, 0, 10, 10, 110); // hedef 110 → 10 m dolgu
    var s = new HafriyatHesabiServisi { GridAraligiM = 1.0 }.Hesapla(mevcut, tasarim);

    AssertClose(s.DolguHacmiM3, 1000, "Dolgu 1000 m³ olmalı.");
    AssertClose(s.YarmaHacmiM3, 0, "Yarma olmamalı.");
    AssertClose(s.NetHacimM3, -1000, "Net negatif (dış dolgu).");
}

static void M60HafriyatDengeli()
{
    // Eğik zemin z = x (0..10). Hedef düz 5 → x<5 dolgu, x>5 yarma, simetrik.
    var mevcut = KareYuzey(10, (x, _) => x);
    var tasarim = DikdortgenPlatform(0, 0, 10, 10, 5);
    var s = new HafriyatHesabiServisi { GridAraligiM = 1.0 }.Hesapla(mevcut, tasarim);

    AssertClose(s.YarmaHacmiM3, 125, "Yarma 125 m³ (Σ0.5..4.5 × 10).");
    AssertClose(s.DolguHacmiM3, 125, "Dolgu 125 m³ (simetrik).");
    AssertClose(s.NetHacimM3, 0, "Dengeli → net 0.");
}

static void M60HafriyatKapsamDisi()
{
    var mevcut = KareYuzey(10, (_, _) => 100);                 // zemin 0..10
    var tasarim = DikdortgenPlatform(100, 100, 110, 110, 90);  // çok uzakta
    var s = new HafriyatHesabiServisi { GridAraligiM = 1.0 }.Hesapla(mevcut, tasarim);

    AssertClose(s.YarmaHacmiM3, 0, "Kapsam dışı → yarma 0.");
    AssertClose(s.DolguHacmiM3, 0, "Kapsam dışı → dolgu 0.");
    Assert(s.KapsamDisiHucreSayisi > 0, "Kapsam dışı hücreler sayılmalı.");
}

static void M60HafriyatTasarimYok()
{
    var mevcut = KareYuzey(10, (_, _) => 100);
    var s = new HafriyatHesabiServisi { GridAraligiM = 1.0 }.Hesapla(mevcut, new TasarimYuzeyi());

    AssertClose(s.YarmaHacmiM3, 0, "Tasarım yok → yarma 0.");
    AssertClose(s.DolguHacmiM3, 0, "Tasarım yok → dolgu 0.");
    Assert(s.DegerlendirilenHucreSayisi == 0, "Değerlendirilen hücre olmamalı.");
}

// ── M60 Terrain — .e4arazi.json kaydet/aç (F7) ───────────────────────────────

static AraziModeli OrnekArazi()
{
    var a = AraziGeometriServisi.YeniOlustur("Test Arazi");
    a.Noktalar.Add(new AraziNoktasi(0, 0, 100));
    a.Noktalar.Add(new AraziNoktasi(10, 10, 110, AraziVeriKaynagi.SpotKot));
    a.Konturlar.Add(new AraziKonturu
    {
        KotM = 540, KotKaynagi = AraziKotKaynagi.KatmanAdi, Kapali = false,
        Noktalar = { new(1, 2), new(3, 4), new(5, 6) }
    });
    a.Tasarim.Bolgeler.Add(new TasarimBolgesi
    {
        Ad = "Platform A", Tip = TasarimBolgeTipi.Platform, HedefKotM = 95,
        Poligon = { new(0, 0), new(10, 0), new(10, 10), new(0, 10) }
    });
    return a;
}

static void M60AraziRoundTrip()
{
    var yol = Path.Combine(Path.GetTempPath(), $"arazi-{Guid.NewGuid():N}.e4arazi.json");
    try
    {
        var arazi = OrnekArazi();
        AraziGeometriServisi.Kaydet(arazi, yol);
        var geri = AraziGeometriServisi.Ac(yol);

        Assert(geri.Noktalar.Count == 2, "2 nokta korunmalı.");
        AssertClose(geri.Noktalar[1].Z, 110, "Nokta Z korunmalı.");

        Assert(geri.Konturlar.Count == 1, "1 kontur korunmalı.");
        AssertClose(geri.Konturlar[0].KotM, 540, "Kontur kotu korunmalı.");
        Assert(geri.Konturlar[0].KotKaynagi == AraziKotKaynagi.KatmanAdi, "Kot kaynağı korunmalı.");
        Assert(geri.Konturlar[0].Noktalar.Count == 3, "Kontur 3 noktalı olmalı.");
        AssertClose(geri.Konturlar[0].Noktalar[2].X, 5, "Point2D X korunmalı.");
        AssertClose(geri.Konturlar[0].Noktalar[2].Y, 6, "Point2D Y korunmalı.");

        Assert(geri.Tasarim.Bolgeler.Count == 1, "Tasarım bölgesi korunmalı.");
        AssertClose(geri.Tasarim.Bolgeler[0].HedefKotM, 95, "Hedef kot korunmalı.");
        Assert(geri.Tasarim.Bolgeler[0].Poligon.Count == 4, "Platform poligonu korunmalı.");
    }
    finally { if (File.Exists(yol)) File.Delete(yol); }
}

static void M60AraziRevizyonArtar()
{
    var yol = Path.Combine(Path.GetTempPath(), $"arazi-{Guid.NewGuid():N}.e4arazi.json");
    try
    {
        var arazi = AraziGeometriServisi.YeniOlustur("Rev");
        Assert(arazi.GeometriRevizyon == 0, "Yeni arazi revizyon 0.");
        AraziGeometriServisi.Kaydet(arazi, yol);
        Assert(arazi.GeometriRevizyon == 1, "İlk kayıt → revizyon 1.");
        AraziGeometriServisi.Kaydet(arazi, yol);
        Assert(arazi.GeometriRevizyon == 2, "İkinci kayıt → revizyon 2.");

        var geri = AraziGeometriServisi.Ac(yol);
        Assert(geri.GeometriRevizyon == 2, "Yüklenen revizyon 2 olmalı.");
    }
    finally { if (File.Exists(yol)) File.Delete(yol); }
}

static void M60AraziDosyaYok()
{
    var yol = Path.Combine(Path.GetTempPath(), $"yok-{Guid.NewGuid():N}.e4arazi.json");
    bool atildi = false;
    try { AraziGeometriServisi.Ac(yol); }
    catch (FileNotFoundException) { atildi = true; }
    Assert(atildi, "Olmayan dosya FileNotFoundException atmalı.");
}

static void M60AraziYuklemeSonrasiHesap()
{
    // Self-contained artifact: yüklenen noktalar+tasarımdan TIN ve hafriyat yeniden üretilebilmeli.
    var yol = Path.Combine(Path.GetTempPath(), $"arazi-{Guid.NewGuid():N}.e4arazi.json");
    try
    {
        var arazi = AraziGeometriServisi.YeniOlustur("Hesap");
        arazi.Noktalar.Add(new AraziNoktasi(0, 0, 100));
        arazi.Noktalar.Add(new AraziNoktasi(10, 0, 100));
        arazi.Noktalar.Add(new AraziNoktasi(10, 10, 100));
        arazi.Noktalar.Add(new AraziNoktasi(0, 10, 100));
        arazi.Tasarim.Bolgeler.Add(new TasarimBolgesi
        {
            Tip = TasarimBolgeTipi.Platform, HedefKotM = 90,
            Poligon = { new(0, 0), new(10, 0), new(10, 10), new(0, 10) }
        });
        AraziGeometriServisi.Kaydet(arazi, yol);

        var geri = AraziGeometriServisi.Ac(yol);
        var yuzey = new AraziYuzeyServisi().Olustur(geri);
        var s = new HafriyatHesabiServisi { GridAraligiM = 1.0 }.Hesapla(yuzey, geri.Tasarim);

        AssertClose(s.YarmaHacmiM3, 1000, "Yüklemeden sonra yarma 1000 m³ yeniden hesaplanmalı.");
    }
    finally { if (File.Exists(yol)) File.Delete(yol); }
}

// ── M60 Terrain — Rapor / kabarma-sıkışma / bölge dökümü (F6) ─────────────────

static HafriyatSonucu IkiPlatformSonucu()
{
    // Düz zemin 100, iki ayrı platform: A (0..10) hedef 90 → yarma, B (20..30) hedef 110 → dolgu.
    var dugumler = new List<AraziDugum>
    {
        new(0, 0, 100), new(30, 0, 100), new(30, 10, 100), new(0, 10, 100)
    };
    var yuzey = new AraziYuzeyServisi().Ucgenle(dugumler);

    var tasarim = new TasarimYuzeyi();
    tasarim.Bolgeler.Add(new TasarimBolgesi
    {
        Ad = "A", Tip = TasarimBolgeTipi.Platform, HedefKotM = 90,
        Poligon = { new(0, 0), new(10, 0), new(10, 10), new(0, 10) }
    });
    tasarim.Bolgeler.Add(new TasarimBolgesi
    {
        Ad = "B", Tip = TasarimBolgeTipi.Platform, HedefKotM = 110,
        Poligon = { new(20, 0), new(30, 0), new(30, 10), new(20, 10) }
    });

    return new HafriyatHesabiServisi { GridAraligiM = 1.0 }.Hesapla(yuzey, tasarim);
}

static void M60BolgeDokumu()
{
    var s = IkiPlatformSonucu();

    Assert(s.BolgeSonuclari.Count == 2, "İki bölge dökümü beklenir.");
    var a = s.BolgeSonuclari.First(b => b.BolgeAdi == "A");
    var b2 = s.BolgeSonuclari.First(b => b.BolgeAdi == "B");

    AssertClose(a.YarmaHacmiM3, 1000, "A bölgesi yarma 1000.");
    AssertClose(a.DolguHacmiM3, 0, "A bölgesi dolgu 0.");
    AssertClose(b2.DolguHacmiM3, 1000, "B bölgesi dolgu 1000.");
    AssertClose(b2.YarmaHacmiM3, 0, "B bölgesi yarma 0.");

    AssertClose(s.YarmaHacmiM3, 1000, "Toplam yarma A ile tutarlı.");
    AssertClose(s.DolguHacmiM3, 1000, "Toplam dolgu B ile tutarlı.");
}

static void M60KabarmaSikismaDenge()
{
    var s = new HafriyatSonucu { YarmaHacmiM3 = 1000, DolguHacmiM3 = 500 };
    var r = HafriyatRaporServisi.Olustur(s, new HafriyatKatsayilari { KabarmaYuzdesi = 25, SikismaYuzdesi = 10 });

    AssertClose(r.KazilanGevsekHacimM3, 1250, "Kabarma %25 → 1250.");
    AssertClose(r.DolguIcinGerekliBankHacimM3, 550, "Sıkışma %10 → 550.");
    AssertClose(r.DengeBankHacimM3, 450, "Bank denge 1000−550 = 450.");
    Assert(r.FazlaMalzemeVar, "Fazla malzeme var (depoya).");
}

static void M60RaporKatsayisiz()
{
    var s = new HafriyatSonucu { YarmaHacmiM3 = 800, DolguHacmiM3 = 300 };
    var r = HafriyatRaporServisi.Olustur(s);

    AssertClose(r.KazilanGevsekHacimM3, 800, "Katsayısız: gevşek = yarma.");
    AssertClose(r.DolguIcinGerekliBankHacimM3, 300, "Katsayısız: gereken bank = dolgu.");
    AssertClose(r.DengeBankHacimM3, 500, "Katsayısız denge = net.");
}

static void M60RaporOzetMetni()
{
    var s = IkiPlatformSonucu();
    var r = HafriyatRaporServisi.Olustur(s, new HafriyatKatsayilari { KabarmaYuzdesi = 20 });

    Assert(r.Ozet.Contains("HAFRİYAT ÖZETİ"), "Özet başlık içermeli.");
    Assert(r.Ozet.Contains("Bölge dökümü"), "Özet bölge dökümü içermeli.");
    Assert(r.Ozet.Contains("A") && r.Ozet.Contains("B"), "Özet bölge adlarını içermeli.");
}

// ── M60 Terrain — 3D önizleme dönüşümü (F3 Core) ─────────────────────────────

static void M60Yuzey3DGorunum()
{
    var dugumler = new List<AraziDugum>
    {
        new(0, 0, 100), new(10, 0, 101), new(10, 10, 102), new(0, 10, 103)
    };
    var yuzey = new AraziYuzeyServisi().Ucgenle(dugumler);
    var surfaces = Arazi3DGorunumServisi.YuzeyleriOlustur(yuzey);

    Assert(surfaces.Count == yuzey.Ucgenler.Count, "Her üçgen bir yüzey olmalı.");
    Assert(surfaces.Count > 0, "Yüzey üretilmeli.");
    foreach (var s in surfaces)
    {
        Assert(s.Vertices.Count == 3, "Üçgen yüzey 3 köşeli olmalı.");
        Assert(s.Category == Arazi3DGorunumServisi.KategoriArazi, "Kategori AraziYuzey olmalı.");
    }
}

// ── M60 Terrain — Tüketici köprüsü (F8) ──────────────────────────────────────

static void M60AraziTuketim()
{
    var yol = Path.Combine(Path.GetTempPath(), $"arazi-{Guid.NewGuid():N}.e4arazi.json");
    try
    {
        var arazi = AraziGeometriServisi.YeniOlustur("Köprü Test");
        arazi.Noktalar.Add(new AraziNoktasi(0, 0, 100));
        arazi.Noktalar.Add(new AraziNoktasi(10, 0, 100));
        arazi.Noktalar.Add(new AraziNoktasi(10, 10, 110));
        arazi.Noktalar.Add(new AraziNoktasi(0, 10, 110));
        AraziGeometriServisi.Kaydet(arazi, yol);

        var t = AraziTuketimServisi.Yukle(yol);
        Assert(t.Revizyon == 1, "Tüketici revizyonu görmeli.");
        Assert(t.UcgenSayisi > 0, "TIN kurulmalı.");

        var kot = t.KotSorgula(5, 5);
        Assert(kot.HasValue, "İç nokta yüzeyde olmalı.");
        AssertClose(kot!.Value, 105, "y=5 ortada kot 105 (100↔110).");

        Assert(t.KotSorgula(500, 500) is null, "Arazi dışı null.");

        var aralik = t.KotAraligi();
        Assert(aralik is { Min: 100, Max: 110 }, "Kot aralığı 100–110 olmalı.");
    }
    finally { if (File.Exists(yol)) File.Delete(yol); }
}

// ── M60 Terrain — Yarma/dolgu renk haritası (F6 görsel) ──────────────────────

static void M60RenkHaritasi()
{
    // Düz zemin 100, hedef 90 (tümü yarma) → tüm yoğunluklar 1.0 (kırmızı uç).
    var duz = new AraziYuzeyServisi().Ucgenle(new List<AraziDugum>
    {
        new(0, 0, 100), new(10, 0, 100), new(10, 10, 100), new(0, 10, 100)
    });
    var tasDuz = new TasarimYuzeyi();
    tasDuz.Bolgeler.Add(new TasarimBolgesi
    {
        Tip = TasarimBolgeTipi.Platform, HedefKotM = 90,
        Poligon = { new(0, 0), new(10, 0), new(10, 10), new(0, 10) }
    });
    var hDuz = Arazi3DGorunumServisi.HafriyatRenkHaritasi(duz, tasDuz);
    Assert(hDuz.Count == duz.Ucgenler.Count, "Her üçgen haritada olmalı.");
    Assert(hDuz.Values.All(v => Math.Abs(v - 1.0) < 1e-9), "Tümü yarma → yoğunluk 1.0.");

    // Eğik zemin z=x, hedef 5 → hem yarma (>0.5) hem dolgu (<0.5) olmalı.
    var egik = new AraziYuzeyServisi().Ucgenle(new List<AraziDugum>
    {
        new(0, 0, 0), new(10, 0, 10), new(10, 10, 10), new(0, 10, 0)
    });
    var tasEgik = new TasarimYuzeyi();
    tasEgik.Bolgeler.Add(new TasarimBolgesi
    {
        Tip = TasarimBolgeTipi.Platform, HedefKotM = 5,
        Poligon = { new(0, 0), new(10, 0), new(10, 10), new(0, 10) }
    });
    var hEgik = Arazi3DGorunumServisi.HafriyatRenkHaritasi(egik, tasEgik);
    Assert(hEgik.Values.Any(v => v < 0.5), "Dolgu tarafı (<0.5) olmalı.");
    Assert(hEgik.Values.Any(v => v > 0.5), "Yarma tarafı (>0.5) olmalı.");
    Assert(hEgik.Values.All(v => v >= 0 && v <= 1), "Yoğunluk [0..1] içinde olmalı.");
}

// ── M61 — Bina Temeli Kota Oturtma (F2) ──────────────────────────────────────

static AraziTuketim TuketimDuz(double z)
{
    var a = new AraziModeli();
    a.Noktalar.Add(new AraziNoktasi(0, 0, z));
    a.Noktalar.Add(new AraziNoktasi(100, 0, z));
    a.Noktalar.Add(new AraziNoktasi(100, 100, z));
    a.Noktalar.Add(new AraziNoktasi(0, 100, z));
    var y = new AraziYuzeyServisi().Olustur(a);
    return new AraziTuketim { Arazi = a, Yuzey = y };
}

static AraziTuketim TuketimEgik() // z = x
{
    var a = new AraziModeli();
    a.Noktalar.Add(new AraziNoktasi(0, 0, 0));
    a.Noktalar.Add(new AraziNoktasi(100, 0, 100));
    a.Noktalar.Add(new AraziNoktasi(100, 100, 100));
    a.Noktalar.Add(new AraziNoktasi(0, 100, 0));
    var y = new AraziYuzeyServisi().Olustur(a);
    return new AraziTuketim { Arazi = a, Yuzey = y };
}

static BinaAyakIzi M61Blok(string id, double x0, double y0, double x1, double y1,
    int zeminUstu = 2, int bodrum = 1, double katY = 3.0) => new()
{
    BlokId = id,
    ZeminUstuKatSayisi = zeminUstu,
    BodrumKatSayisi = bodrum,
    KatYuksekligiM = katY,
    Poligon = { new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1) }
};

static void M61DuzZeminOturtma()
{
    var arazi = TuketimDuz(100);
    var b = new TemelOturtmaServisi().OturtBlok(arazi, M61Blok("A", 40, 40, 60, 60));

    AssertClose(b.OrtalamaZeminKotuM, 100, "Ortalama zemin 100.");
    AssertClose(b.SifirKotM, 100, "±0.00 = zemin 100.");
    AssertClose(b.BodrumTabanKotuM, 97, "Bodrum taban 100−3.");
    AssertClose(b.CatiKotuM, 106, "Çatı 100+2×3.");
    AssertClose(b.KaziDerinligiM, 3, "Kazı derinliği 3 m.");
    AssertClose(b.SerbestYukseklikM, 0, "Serbest yükseklik 0.");
    Assert(b.Kotlar.Any(k => k.Tip == KotTipi.Bodrum && Math.Abs(k.KotM - 97) < 1e-9), "Bodrum kotu 97.");
    Assert(b.Kotlar.Any(k => k.Tip == KotTipi.Giris && Math.Abs(k.KotM - 100) < 1e-9), "Giriş kotu 100.");
    Assert(b.Uyarilar.Count == 0, "Uyarı olmamalı.");
}

static void M61SifirKotOverride()
{
    var arazi = TuketimDuz(100);
    var b = new TemelOturtmaServisi().OturtBlok(arazi, M61Blok("A", 40, 40, 60, 60), sifirKotOverride: 101);

    AssertClose(b.SifirKotM, 101, "±0.00 override 101.");
    AssertClose(b.BodrumTabanKotuM, 98, "Bodrum taban 101−3.");
    AssertClose(b.KaziDerinligiM, 2, "Kazı 100−98 = 2.");
    AssertClose(b.SerbestYukseklikM, 1, "Serbest yükseklik +1.");
}

static void M61KademeSirasi()
{
    var arazi = TuketimEgik();
    var model = new TemelOturtmaServisi().Oturt(arazi, new[]
    {
        M61Blok("Ust", 70, 10, 90, 30),    // zemin ~80
        M61Blok("Alt", 10, 10, 30, 30)     // zemin ~20
    });

    var alt = model.Bloklar.First(b => b.BlokId == "Alt");
    var ust = model.Bloklar.First(b => b.BlokId == "Ust");
    Assert(alt.OrtalamaZeminKotuM < ust.OrtalamaZeminKotuM, "Alt blok daha düşük zeminde.");
    Assert(alt.KademeNo == 0 && ust.KademeNo == 1, "Kademe sırası zemine göre.");
    Assert(Math.Abs(alt.SifirKotM - ust.SifirKotM) > 1, "Kademeli bina farklı ±0.00.");
    AssertClose(arazi.Revizyon, model.AraziRevizyon, "Arazi revizyonu modele taşınmalı.");
}

static void M61AyakIziDisinda()
{
    var arazi = TuketimDuz(100);
    var b = new TemelOturtmaServisi().OturtBlok(arazi, M61Blok("X", 500, 500, 520, 520));

    Assert(b.Uyarilar.Any(u => u.Contains("arazi dışında")), "Arazi dışı uyarısı olmalı.");
    Assert(b.Kotlar.Count == 0, "Oturtulamayan blokta kot üretilmez.");
}

static void M61GomuluUyari()
{
    var arazi = TuketimDuz(100);
    var b = new TemelOturtmaServisi().OturtBlok(arazi, M61Blok("A", 40, 40, 60, 60), sifirKotOverride: 90);

    Assert(b.SerbestYukseklikM < 0, "±0.00 zemin altında.");
    Assert(b.Uyarilar.Any(u => u.Contains("gömülü")), "Gömülü uyarısı olmalı.");
}

// ── M61 F1 — .e4bina → BinaAyakIzi adaptörü ──────────────────────────────────

static BinaOda M61Oda(params (double x, double y)[] kose)
{
    var o = new BinaOda();
    foreach (var (x, y) in kose) o.Poligon.Add(new Point2D(x, y));
    return o;
}

static void M61BinaAdaptoru()
{
    var geo = new BinaGeometri { BinaAdi = "Blok-A" };
    // Bodrum (KatNo -1): tek oda 0..10 kare → ayak izi bu poligon.
    var bodrum = new BinaKat { KatNo = -1, KatYuksekligiM = 3.0 };
    bodrum.Odalar.Add(M61Oda((0, 0), (10, 0), (10, 10), (0, 10)));
    geo.Katlar.Add(bodrum);
    // Zemin + 1. kat (KatNo 0, 1).
    var zemin = new BinaKat { KatNo = 0, KatYuksekligiM = 3.0 };
    zemin.Odalar.Add(M61Oda((0, 0), (10, 0), (10, 10), (0, 10)));
    geo.Katlar.Add(zemin);
    var ust = new BinaKat { KatNo = 1, KatYuksekligiM = 3.0 };
    ust.Odalar.Add(M61Oda((0, 0), (10, 0), (10, 10), (0, 10)));
    geo.Katlar.Add(ust);

    var ayak = BinaAyakIziAdaptoru.Donustur(geo);
    Assert(ayak.BlokId == "Blok-A", "Blok adı taşınmalı.");
    Assert(ayak.ZeminUstuKatSayisi == 2, "Zemin üstü 2 kat (KatNo 0,1).");
    Assert(ayak.BodrumKatSayisi == 1, "1 bodrum (KatNo -1).");
    AssertClose(ayak.KatYuksekligiM, 3.0, "Kat yüksekliği 3.");
    Assert(ayak.Poligon.Count == 4, "Tek oda → 4 köşeli ayak izi.");

    // Uçtan uca: düz zemin 100'e oturt.
    var arazi = TuketimDuz(100);
    var b = new TemelOturtmaServisi().OturtBlok(arazi, ayak);
    AssertClose(b.SifirKotM, 100, "±0.00 = 100.");
    AssertClose(b.BodrumTabanKotuM, 97, "Bodrum taban 97.");
    AssertClose(b.CatiKotuM, 106, "Çatı 100+2×3.");
}

static void M61AdaptorCokOda()
{
    var geo = new BinaGeometri { BinaAdi = "Çok" };
    var zemin = new BinaKat { KatNo = 0, KatYuksekligiM = 3.0 };
    zemin.Odalar.Add(M61Oda((0, 0), (4, 0), (4, 4), (0, 4)));     // sol oda
    zemin.Odalar.Add(M61Oda((6, 6), (10, 6), (10, 10), (6, 10))); // sağ üst oda
    geo.Katlar.Add(zemin);

    var ayak = BinaAyakIziAdaptoru.Donustur(geo);
    Assert(ayak.Poligon.Count == 4, "Çok oda → sınır kutusu (4 köşe).");
    Assert(ayak.Poligon.Min(p => p.X) == 0 && ayak.Poligon.Min(p => p.Y) == 0, "Bbox min 0,0.");
    Assert(ayak.Poligon.Max(p => p.X) == 10 && ayak.Poligon.Max(p => p.Y) == 10, "Bbox max 10,10.");
}

// ── M61 F5 — Kotlu kesit ─────────────────────────────────────────────────────

static void M61Kesit()
{
    var arazi = TuketimEgik(); // z = x, 0..100
    var ayaklar = new[]
    {
        M61Blok("Alt", 10, 40, 30, 60),
        M61Blok("Ust", 70, 40, 90, 60)
    };
    var model = new TemelOturtmaServisi().Oturt(arazi, ayaklar);
    var kesit = KesitServisi.Olustur(arazi, model, ayaklar, ornekSayisi: 50);

    Assert(kesit.ZeminProfili.Count > 0, "Arazi profili boş olmamalı.");
    // z=x → profil X ile artar (ilk < son).
    Assert(kesit.ZeminProfili.First().Y < kesit.ZeminProfili.Last().Y, "Profil eğimi artmalı.");

    Assert(kesit.Bloklar.Count == 2, "İki blok kesitte olmalı.");
    var alt = kesit.Bloklar.First(b => b.BlokId == "Alt");
    AssertClose(alt.XMin, 10, "Alt blok XMin.");
    AssertClose(alt.XMax, 30, "Alt blok XMax.");
    Assert(alt.CatiKotM > alt.TabanKotM, "Çatı tabandan yüksek.");

    var ust = kesit.Bloklar.First(b => b.BlokId == "Ust");
    Assert(ust.TabanKotM > alt.TabanKotM, "Üst blok daha yüksek kotta (kademe).");
    Assert(kesit.MaxKot >= ust.CatiKotM, "MaxKot en üst çatıyı kapsamalı.");
}

// ── M61 F1 — Mimari M35 (Bim3DModel) → BinaAyakIzi ───────────────────────────

static Kat3DModel M35Kat(int katNo, double x, double y, double w, double d)
{
    var k = new Kat3DModel { KatNo = katNo, KatYuksekligiM = 3.0 };
    k.Mahaller.Add(new MahalHacmi3D { KatNo = katNo, X = x, Y = y, GenislikM = w, DerinlikM = d, YukseklikM = 3 });
    return k;
}

static void M61MimariAdaptoru()
{
    var model = new Bim3DModel { Ad = "Kule" };
    model.Katlar.Add(M35Kat(0, 40, 40, 20, 20));   // bodrum
    model.Katlar.Add(M35Kat(1, 40, 40, 20, 20));   // zemin
    model.Katlar.Add(M35Kat(2, 40, 40, 20, 20));   // 1. kat

    var ayak = BinaAyakIziAdaptoru.Donustur(model);
    Assert(ayak.BlokId == "Kule", "Model adı taşınmalı.");
    Assert(ayak.ZeminUstuKatSayisi == 2, "KatNo≥1 → 2 üst kat.");
    Assert(ayak.BodrumKatSayisi == 1, "KatNo≤0 → 1 bodrum.");
    Assert(ayak.Poligon.Count == 4, "Mahal kutusu → 4 köşeli ayak izi.");
    Assert(ayak.Poligon.Min(p => p.X) == 40 && ayak.Poligon.Max(p => p.X) == 60, "Ayak izi X 40..60.");

    var arazi = TuketimDuz(100);
    var b = new TemelOturtmaServisi().OturtBlok(arazi, ayak);
    AssertClose(b.SifirKotM, 100, "±0.00 = 100.");
    AssertClose(b.BodrumTabanKotuM, 97, "Bodrum taban 97.");
}

// ── M70 Landscape — Park altlık tüketimi + sınır (F1) ────────────────────────

// (x,y,z) düğümlü + kare iki üçgenli arazi tüketicisi: z = z0 + egim*y.
static AraziTuketim AraziTuketici(double z0, double egim)
{
    var yuzey = new AraziYuzeyi
    {
        Dugumler =
        {
            new(0, 0, z0), new(10, 0, z0),
            new(10, 10, z0 + egim * 10), new(0, 10, z0 + egim * 10)
        },
        Ucgenler = { new(0, 1, 2), new(0, 2, 3) }
    };
    return new AraziTuketim { Arazi = new AraziModeli { AraziAdi = "Test" }, Yuzey = yuzey };
}

static void M70ParkSiniriAraziden()
{
    var park = new ParkModeli();
    ParkAltlikServisi.ParkSiniriAraziden(park, AraziTuketici(10, 1));

    Assert(park.Sinir.Poligon.Count == 4, "Kare arazi → 4 köşeli park sınırı.");
    AssertClose(PoligonYardimci.Alan(park.Sinir.Poligon), 100, "Park sınırı alanı 100 m².");
    // Tüm köşeler kabukta olmalı.
    foreach (var k in new[] { new Point2D(0, 0), new(10, 0), new(10, 10), new(0, 10) })
        Assert(park.Sinir.Poligon.Contains(k), $"Köşe {k} sınırda olmalı.");
}

static void M70AraziKotSorgu()
{
    var tuketim = AraziTuketici(10, 1); // z = 10 + y
    var z = tuketim.KotSorgula(5, 5);
    Assert(z.HasValue, "İç nokta yüzeyde olmalı.");
    AssertClose(z!.Value, 15, "z = 10 + y → (5,5) kotu 15.");
}

static void M70BinaDislamaYukle()
{
    var yol = Path.Combine(Path.GetTempPath(), $"park-bina-{Guid.NewGuid():N}.e4bina.json");
    try
    {
        var bina = BinaGeometriServisi.YeniOlustur("Blok A");
        var kat = new BinaKat { KatNo = 0 };
        kat.Odalar.Add(new BinaOda { Poligon = new() { new(0, 0), new(4, 0), new(4, 3), new(0, 3) } });
        bina.Katlar.Add(kat);
        BinaGeometriServisi.Kaydet(bina, yol);

        var park = new ParkModeli();
        var dislama = ParkAltlikServisi.BinaYukle(park, yol, setbackM: 3);

        Assert(park.BinaDislamalari.Count == 1, "Bir bina dışlaması eklenmeli.");
        Assert(dislama.AyakIzi.Count == 4, "Tek oda → 4 köşeli ayak izi.");
        AssertClose(PoligonYardimci.Alan(dislama.AyakIzi), 12, "Ayak izi alanı 4×3 = 12 m².");
        AssertClose(dislama.SetbackM, 3, "Setback 3 m korunmalı.");
        Assert(dislama.BinaAdi == "Blok A", "Bina adı ayak izine taşınmalı.");
        var yuklu = BinaGeometriServisi.Ac(yol);
        Assert(park.BinaRevizyon == yuklu.GeometriRevizyon, "Bina revizyon damgası saklanmalı.");
    }
    finally { if (File.Exists(yol)) File.Delete(yol); }
}

static void M70AltlikDurumu()
{
    var park = new ParkModeli();
    var tuketim = AraziTuketici(10, 0); // arazi.GeometriRevizyon = 1 (varsayılan)

    Assert(ParkAltlikServisi.AraziDurumu(park, null) == AltlikDurumu.Yok, "Arazisiz → Yok.");

    park.AraziDosyaYolu = "x.e4arazi.json";
    park.AraziRevizyon = tuketim.Revizyon;
    Assert(ParkAltlikServisi.AraziDurumu(park, tuketim) == AltlikDurumu.Bagli, "Eşit revizyon → Bağlı.");

    park.AraziRevizyon = tuketim.Revizyon - 1;
    Assert(ParkAltlikServisi.AraziDurumu(park, tuketim) == AltlikDurumu.Eskimis, "Farklı revizyon → Eskimiş.");

    Assert(ParkAltlikServisi.BinaDurumu(park, null) == AltlikDurumu.Yok, "Binasız → Yok.");
    park.BinaDosyaYolu = "y.e4bina.json";
    park.BinaRevizyon = 2;
    Assert(ParkAltlikServisi.BinaDurumu(park, 2) == AltlikDurumu.Bagli, "Eşit bina revizyon → Bağlı.");
    Assert(ParkAltlikServisi.BinaDurumu(park, 5) == AltlikDurumu.Eskimis, "Farklı bina revizyon → Eskimiş.");
}

// ── M70 Landscape — Clipper2 dışlama + kullanılabilir alan (F2) ───────────────

static List<Point2D> Kare(double x0, double y0, double kenar) => new()
{
    new(x0, y0), new(x0 + kenar, y0), new(x0 + kenar, y0 + kenar), new(x0, y0 + kenar)
};

static void M70TamponOffset()
{
    // 4×4 kare +1 m → 6×6 = 36 m².
    var tampon = ClipperAdaptor.Tampon(Kare(0, 0, 4), 1);
    AssertClose(ClipperAdaptor.NetAlan(tampon), 36, "4×4 +1 m offset → 36 m².");
}

static void M70FarkDelik()
{
    // park 10×10 (100) − merkezde 2×2 (4) → 96.
    var fark = ClipperAdaptor.Fark(Kare(0, 0, 10), new[] { Kare(4, 4, 2) });
    AssertClose(ClipperAdaptor.NetAlan(fark), 96, "10×10 − 2×2 → 96 m².");
}

static void M70BirlestirUnion()
{
    // İki 4×4 kare 2×2 örtüşür → 16 + 16 − 4 = 28.
    var birlesik = ClipperAdaptor.Birlestir(new[] { Kare(0, 0, 4), Kare(2, 2, 4) });
    AssertClose(ClipperAdaptor.NetAlan(birlesik), 28, "İki örtüşen 4×4 → 28 m².");
}

static void M70KullanilabilirAlan()
{
    var park = new ParkModeli();
    park.Sinir.Poligon = Kare(0, 0, 20); // 400 m²
    park.BinaDislamalari.Add(new BinaDislama
    {
        BinaAdi = "Blok", AyakIzi = Kare(8, 8, 4), SetbackM = 1 // 4×4 + 1 m → 6×6 = 36
    });

    var sonuc = ParkAlanServisi.KullanilabilirAlan(park);
    AssertClose(sonuc.ToplamParkM2, 400, "Toplam park 400 m².");
    AssertClose(sonuc.DislananM2, 36, "Dışlanan = 6×6 = 36 m².");
    AssertClose(sonuc.KullanilabilirM2, 364, "Kullanılabilir = 400 − 36 = 364 m².");
}

// ── M70 Landscape — öğe yerleşimi + kot + doğrulama (F3) ──────────────────────

static ParkModeli ParkBinali()
{
    var park = new ParkModeli();
    park.Sinir.Poligon = Kare(0, 0, 20);
    park.BinaDislamalari.Add(new BinaDislama { BinaAdi = "Blok", AyakIzi = Kare(8, 8, 4), SetbackM = 1 });
    return park;
}

static void M70OgeEkleSil()
{
    var park = new ParkModeli();
    var b1 = ParkYerlesimServisi.OgeEkle(park, new Bank { Konum = new(1, 1) });
    var d1 = ParkYerlesimServisi.OgeEkle(park, new DosemeAlani { Poligon = Kare(2, 2, 3) });

    Assert(b1.Id == 1 && d1.Id == 2, "Kararlı artan Id atanmalı.");
    Assert(park.SonrakiOgeId == 3, "SonrakiOgeId 3 olmalı.");
    Assert(park.Ogeler.Count == 2, "İki öğe eklenmeli.");

    Assert(ParkYerlesimServisi.OgeSil(park, 1), "Var olan öğe silinmeli.");
    Assert(park.Ogeler.Count == 1 && park.Ogeler[0].Id == 2, "Doğru öğe kalmalı.");
    Assert(!ParkYerlesimServisi.OgeSil(park, 99), "Olmayan öğe silinmemeli.");
}

static void M70OgeZeminKotu()
{
    var arazi = AraziTuketici(10, 1); // z = 10 + y, (0..10)
    var bank = new Bank { Konum = new(5, 5) };
    ParkYerlesimServisi.ZeminKotunuAta(bank, arazi);
    AssertClose(bank.ZeminKotuM!.Value, 15, "Bank (5,5) kotu 15.");

    var doseme = new DosemeAlani { Poligon = Kare(2, 2, 4) }; // merkez (4,4)
    ParkYerlesimServisi.ZeminKotunuAta(doseme, arazi);
    AssertClose(doseme.ZeminKotuM!.Value, 14, "Döşeme merkezi (4,4) kotu 14.");

    ParkYerlesimServisi.ZeminKotunuAta(bank, null);
    Assert(bank.ZeminKotuM is null, "Arazi yoksa kot null.");
}

static void M70YerlesimDogrula()
{
    var park = ParkBinali();
    var halkalar = ParkAlanServisi.KullanilabilirAlan(park).Halkalar;

    Assert(ParkYerlesimServisi.Dogrula(new Bank { Konum = new(2, 2) }, halkalar).Gecerli,
        "Park içi boş alandaki öğe geçerli.");
    Assert(!ParkYerlesimServisi.Dogrula(new Bank { Konum = new(10, 10) }, halkalar).Gecerli,
        "Bina+setback üstündeki öğe geçersiz.");
    Assert(!ParkYerlesimServisi.Dogrula(new Bank { Konum = new(25, 25) }, halkalar).Gecerli,
        "Park sınırı dışındaki öğe geçersiz.");
}

static void M70OgeTasi()
{
    var bank = new Bank { Konum = new(1, 1) };
    ParkYerlesimServisi.OgeTasi(bank, 5, 5);
    Assert(bank.Konum == new Point2D(6, 6), "Bank konumu ötelenmeli.");

    var doseme = new DosemeAlani { Poligon = Kare(0, 0, 2) };
    ParkYerlesimServisi.OgeTasi(doseme, 10, 0);
    Assert(doseme.Poligon[0] == new Point2D(10, 0), "Poligon köşeleri ötelenmeli.");
    AssertClose(PoligonYardimci.Alan(doseme.Poligon), 4, "Öteleme alanı korur.");
}

// ── M70 Landscape — alan/oran + erişilebilirlik (F4) ─────────────────────────

static void M70AlanOran()
{
    var park = new ParkModeli { Sinir = { Poligon = Kare(0, 0, 20) } }; // 400
    ParkYerlesimServisi.OgeEkle(park, new DosemeAlani { Poligon = Kare(5, 5, 10) }); // 100

    var ozet = ParkOranServisi.Hesapla(park);
    AssertClose(ozet.SertZeminM2, 100, "Sert zemin 100 m².");
    AssertClose(ozet.YesilAlanM2, 300, "Yeşil alan 400 − 100 = 300 m².");
    AssertClose(ozet.SertZeminOran, 0.25, "Sert oran 0.25.");
    AssertClose(ozet.YesilOran, 0.75, "Yeşil oran 0.75.");
}

static void M70AlanOranBinali()
{
    var park = ParkBinali(); // 20×20, bina 4×4 + setback1 → kullanılabilir 364
    ParkYerlesimServisi.OgeEkle(park, new DosemeAlani { Poligon = Kare(0, 0, 6) }); // 36

    var ozet = ParkOranServisi.Hesapla(park);
    AssertClose(ozet.ToplamParkM2, 400, "Toplam 400.");
    AssertClose(ozet.KullanilabilirM2, 364, "Kullanılabilir 364.");
    AssertClose(ozet.SertZeminM2, 36, "Sert zemin 36.");
    AssertClose(ozet.YesilAlanM2, 328, "Yeşil = 364 − 36 = 328.");
}

static void M70YolGenislikUyari()
{
    var park = new ParkModeli { Sinir = { Poligon = Kare(0, 0, 20) } };
    ParkYerlesimServisi.OgeEkle(park, new YuruyusYolu
    {
        Ad = "Dar yol", Guzergah = new() { new(0, 0), new(10, 0) }, GenislikM = 1.0
    });

    var ozet = ParkOranServisi.Hesapla(park); // varsayılan min 1.5 m
    AssertClose(ozet.SertZeminM2, 10, "Yol alanı = 10 × 1.0 = 10 m².");
    Assert(ozet.Uyarilar.Any(u => u.Contains("genişliği")), "Dar yol genişlik uyarısı vermeli.");
}

static void M70YolEgimUyari()
{
    var arazi = AraziTuketici(10, 1); // z = 10 + y → y yönünde %100 eğim

    var dik = new ParkModeli { Sinir = { Poligon = Kare(0, 0, 10) } };
    ParkYerlesimServisi.OgeEkle(dik, new YuruyusYolu
    {
        Ad = "Dik yol", Guzergah = new() { new(0, 0), new(0, 10) }, GenislikM = 2.0
    });
    var ozetDik = ParkOranServisi.Hesapla(dik, arazi);
    Assert(ozetDik.Uyarilar.Any(u => u.Contains("eğimi")), "Dik yol eğim uyarısı vermeli.");

    var yatay = new ParkModeli { Sinir = { Poligon = Kare(0, 0, 10) } };
    ParkYerlesimServisi.OgeEkle(yatay, new YuruyusYolu
    {
        Ad = "Düz yol", Guzergah = new() { new(0, 0), new(10, 0) }, GenislikM = 2.0
    });
    var ozetYatay = ParkOranServisi.Hesapla(yatay, arazi); // x yönünde eğim 0
    Assert(!ozetYatay.Uyarilar.Any(u => u.Contains("eğimi")), "Düz yol eğim uyarısı vermemeli.");
}

// ── M70 Landscape — kaydet/aç + .e4park.json (F5) ────────────────────────────

static ParkModeli ParkDoluFixture()
{
    var park = ParkGeometriServisi.YeniOlustur("Merkez Park");
    park.Sinir.Poligon = Kare(0, 0, 30);
    park.BinaDislamalari.Add(new BinaDislama { BinaAdi = "Blok", AyakIzi = Kare(10, 10, 4), SetbackM = 2 });
    ParkYerlesimServisi.OgeEkle(park, new Bank { Ad = "Bank-1", Konum = new(3, 3), YonDeg = 90, Model = "Ahşap" });
    ParkYerlesimServisi.OgeEkle(park, new YuruyusYolu
    {
        Ad = "Ana yol", Guzergah = new() { new(0, 0), new(20, 0) }, GenislikM = 2.5, Kaplama = KaplamaTipi.Granit
    });
    ParkYerlesimServisi.OgeEkle(park, new DosemeAlani { Poligon = Kare(5, 20, 4), Kaplama = KaplamaTipi.DogalTas });
    ParkYerlesimServisi.OgeEkle(park, new OturmaAlani { Poligon = Kare(22, 22, 3) });
    ParkYerlesimServisi.OgeEkle(park, new SusHavuzu { Poligon = Kare(15, 2, 5), DerinlikM = 0.6, KenarKaplama = KaplamaTipi.Granit });
    return park;
}

static void M70ParkRoundTrip()
{
    var yol = Path.Combine(Path.GetTempPath(), $"park-{Guid.NewGuid():N}.e4p70");
    try
    {
        var park = ParkDoluFixture();
        int sonrakiId = park.SonrakiOgeId;
        ParkGeometriServisi.Kaydet(park, yol);

        var ac = ParkGeometriServisi.Ac(yol);
        Assert(ac.ParkAdi == "Merkez Park", "Park adı korunmalı.");
        Assert(ac.Sinir.Poligon.Count == 4, "Sınır korunmalı.");
        Assert(ac.BinaDislamalari.Count == 1 && ac.BinaDislamalari[0].SetbackM == 2, "Bina dışlama + setback korunmalı.");
        Assert(ac.Ogeler.Count == 5, "5 öğe korunmalı.");
        Assert(ac.SonrakiOgeId == sonrakiId, "SonrakiOgeId korunmalı.");

        // Polimorfik tipler + değerler.
        var bank = ac.Ogeler.OfType<Bank>().Single();
        Assert(bank.Konum == new Point2D(3, 3) && bank.YonDeg == 90 && bank.Model == "Ahşap", "Bank değerleri korunmalı.");
        var yolO = ac.Ogeler.OfType<YuruyusYolu>().Single();
        Assert(yolO.GenislikM == 2.5 && yolO.Kaplama == KaplamaTipi.Granit && yolO.Guzergah.Count == 2, "Yol değerleri korunmalı.");
        var havuz = ac.Ogeler.OfType<SusHavuzu>().Single();
        Assert(havuz.DerinlikM == 0.6 && havuz.KenarKaplama == KaplamaTipi.Granit, "Havuz değerleri korunmalı.");
        Assert(ac.Ogeler.OfType<DosemeAlani>().Count() == 1 && ac.Ogeler.OfType<OturmaAlani>().Count() == 1, "Döşeme + oturma tipleri korunmalı.");
    }
    finally { if (File.Exists(yol)) File.Delete(yol); }
}

static void M70ParkRevizyonArtar()
{
    var yol = Path.Combine(Path.GetTempPath(), $"park-{Guid.NewGuid():N}.e4p70");
    try
    {
        var park = ParkGeometriServisi.YeniOlustur("R");
        Assert(park.GeometriRevizyon == 0, "Yeni park revizyon 0.");
        ParkGeometriServisi.Kaydet(park, yol);
        Assert(park.GeometriRevizyon == 1, "İlk kayıt → 1.");
        ParkGeometriServisi.Kaydet(park, yol);
        Assert(park.GeometriRevizyon == 2, "İkinci kayıt → 2.");
        Assert(ParkGeometriServisi.Ac(yol).GeometriRevizyon == 2, "Yüklenen revizyon 2.");
    }
    finally { if (File.Exists(yol)) File.Delete(yol); }
}

static void M70ParkDosyaYok()
{
    var yol = Path.Combine(Path.GetTempPath(), $"yok-{Guid.NewGuid():N}.e4p70");
    bool atti = false;
    try { ParkGeometriServisi.Ac(yol); }
    catch (FileNotFoundException) { atti = true; }
    Assert(atti, "Olmayan dosya FileNotFoundException atmalı.");
}

static void M70ParkArtifact()
{
    var yol = Path.Combine(Path.GetTempPath(), $"park-{Guid.NewGuid():N}.e4park.json");
    try
    {
        var park = ParkDoluFixture();
        park.GeometriRevizyon = 7;
        ParkGeometriServisi.ArtifactYaz(park, yol);
        Assert(park.GeometriRevizyon == 7, "Artifact yazımı revizyonu artırmamalı.");

        var ac = ParkGeometriServisi.Ac(yol);
        Assert(ac.Ogeler.Count == 5 && ac.GeometriRevizyon == 7, "Artifact öğeleri + revizyon korunmalı.");
    }
    finally { if (File.Exists(yol)) File.Delete(yol); }
}
