using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Kst.Acoustics.App.BuildingAcoustics.ViewModels;
using Kst.Acoustics.App.Electric.BasitProjeCiz.ViewModels;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Bina.Services;
using Kst.Acoustics.Core.BuildingAcoustics.Calculation;
using Kst.Acoustics.Core.BuildingAcoustics.Geometry;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;
using M50GeometriServisi = Kst.Acoustics.Core.Bina.Services.BinaGeometriServisi;

namespace Kst.Acoustics.Tests;

/// <summary>
/// M50'nin tek geometri kaynağı olduğu M08/M12 köprüleri için regresyonlar.
/// Program.cs kaydı entegrasyon sırasında ana agent tarafından yapılır.
/// </summary>
public static class M50ModuleIntegrationRegressionTests
{
    public static void M08KararliKatIdReorderAkustikAtamayiKorur()
    {
        var geo = M50GeometriServisi.YeniOlustur("KatId");
        var katA = Kat("kat-a", 1, "A", "oda-a");
        var katB = Kat("kat-b", 2, "B", "oda-b");
        geo.Katlar.Add(katA);
        geo.Katlar.Add(katB);

        var proje = new BinaProje();
        BinaGeometriMekanAdapteri.Uygula(proje, geo);
        var mekanA = proje.Katlar.Single(k => k.BinaGeometriKatId == "kat-a").Mekanlar.Single();
        mekanA.RT60 = 0.83;
        mekanA.Sinif = MekanSinifi.Alici;
        mekanA.ZeminYapiElemani = new YapiElemani { Ad = "Döşeme A" };

        geo.Katlar.Move(1, 0);
        katA.KatNo = 2;
        katB.KatNo = 1;
        BinaGeometriMekanAdapteri.Uygula(proje, geo);

        var yeniKatA = proje.Katlar.Single(k => k.BinaGeometriKatId == "kat-a");
        if (yeniKatA.KatNo != 2 || !ReferenceEquals(yeniKatA.Mekanlar.Single(), mekanA))
            throw new InvalidOperationException("M08 kat reorder kararlı KatId/oda kimliğiyle eşleşmedi.");
        if (Math.Abs(mekanA.RT60 - 0.83) > 1e-9 || mekanA.Sinif != MekanSinifi.Alici ||
            mekanA.ZeminYapiElemani?.Ad != "Döşeme A")
            throw new InvalidOperationException("Kat reorder sırasında M08 akustik ataması kayboldu.");

        var tekrar = BinaGeometriM08ExportServisi.GeometriyeCevir(proje);
        if (!tekrar.Katlar.Select(k => k.Id).SequenceEqual(["kat-b", "kat-a"]))
            throw new InvalidOperationException("M08 export kararlı M50 kat kimliklerini korumadı.");

        // Aynı oda kararlı Id ile başka kata taşındığında akustik nesnesi korunmalı,
        // fakat eski ve yeni katta aynı referans/Id birlikte kalmamalı.
        katA.Odalar.Remove(katA.Odalar.Single(o => o.Id == "oda-a"));
        katB.Odalar.Add(new BinaOda
        {
            Id = "oda-a", Ad = "A taşındı", AlanM2 = 16,
            Poligon = [new(5, 0), new(9, 0), new(9, 4), new(5, 4)]
        });
        BinaGeometriMekanAdapteri.Uygula(proje, geo);
        var mekanAId = mekanA.Id;
        var sahipler = proje.Katlar.Where(k => k.Mekanlar.Any(m => m.Id == mekanAId)).ToList();
        if (sahipler.Count != 1 || sahipler[0].BinaGeometriKatId != "kat-b" ||
            !ReferenceEquals(sahipler[0].Mekanlar.Single(m => m.Id == mekanAId), mekanA))
            throw new InvalidOperationException("Kata taşınan M08 mekanı tek hedefte aynı akustik nesneyle korunmadı.");
        if (Math.Abs(mekanA.RT60 - 0.83) > 1e-9 || mekanA.ZeminYapiElemani?.Ad != "Döşeme A")
            throw new InvalidOperationException("Çapraz-kat oda taşımasında M08 akustik verisi kayboldu.");
    }

    public static void M08SilinenBirlesimiTemizlerVeKatIdExportEder()
    {
        var geo = M50GeometriServisi.YeniOlustur("Birleşim");
        var alt = Kat("alt", 1, "Alt", "alt-oda");
        var ust = Kat("ust", 2, "Üst", "ust-oda");
        geo.Katlar.Add(alt);
        geo.Katlar.Add(ust);
        geo.DuseyBirlesimleri.Add(new BinaDuseyBirlesim
        {
            Id = "birlesim", AltKatNo = 1, UstKatNo = 2,
            Uyeler =
            [
                new() { KatId = "alt", KatNo = 1, OdaId = "alt-oda", TavanAcik = true },
                new() { KatId = "ust", KatNo = 2, OdaId = "ust-oda", DosemeAcik = true }
            ]
        });

        var proje = new BinaProje();
        BinaGeometriMekanAdapteri.Uygula(proje, geo);
        var export = BinaGeometriM08ExportServisi.GeometriyeCevir(proje);
        if (export.DuseyBirlesimleri.Single().Uyeler.Any(u => string.IsNullOrWhiteSpace(u.KatId)))
            throw new InvalidOperationException("M08 export şema-2 birleşim üyelerinde KatId boş kaldı.");

        geo.DuseyBirlesimleri.Clear();
        BinaGeometriMekanAdapteri.Uygula(proje, geo);
        if (proje.DuseyMekanBirlesimleri.Count != 0)
            throw new InvalidOperationException("M50'de silinen birleşim M08'de artık kayıt olarak kaldı.");
    }

    public static void M08DisDuvarUstSagSinirAcikliklariniKacirmaz()
    {
        var geo = M50GeometriServisi.YeniOlustur("Sınır açıklıkları");
        var kat = Kat("kat", 1, "Zemin", "oda");
        kat.Odalar.Add(new BinaOda
        {
            Id = "yakin-oda", Ad = "Yakın oda", AlanM2 = 16,
            // Açıklığın üst kenarına 10 cm yakın; fakat açıklık bu poligonun
            // içinde veya sınırında değildir ve buraya çiftlenmemelidir.
            Poligon = [new(0, 4.1), new(4, 4.1), new(4, 8.1), new(0, 8.1)]
        });
        kat.Acikliklar.Add(new BinaAciklik
        {
            Id = "ust-pencere", Tip = BinaAciklikTipi.Pencere,
            Bas = new Point2D(1, 4), Bit = new Point2D(2, 4), GenislikM = 1, YukseklikM = 1.4
        });
        kat.Acikliklar.Add(new BinaAciklik
        {
            Id = "sag-kapi", Tip = BinaAciklikTipi.Kapi,
            Bas = new Point2D(4, 1), Bit = new Point2D(4, 2), GenislikM = 1, YukseklikM = 2.1
        });
        geo.Katlar.Add(kat);

        var proje = new BinaProje();
        BinaGeometriMekanAdapteri.Uygula(proje, geo);
        var tumAcikliklar = proje.Katlar.Single().Mekanlar
            .SelectMany(m => m.Bolmeler).SelectMany(b => b.Acikliklar).ToList();
        var ids = tumAcikliklar.Select(a => a.AciklikId).ToHashSet();
        if (!ids.SetEquals(["ust-pencere", "sag-kapi"]))
            throw new InvalidOperationException("Üst/sağ dış duvar sınırındaki M50 açıklıkları M08'e yerleşmedi.");
        if (tumAcikliklar.Count(a => a.AciklikId == "ust-pencere") != 1)
            throw new InvalidOperationException("Sınır açıklığı 10 cm yakındaki komşu oda duvarına yanlış çiftlendi.");
    }

    public static void M08DuseyAkustikBaskiYuzeyBayraklarinaUyar()
    {
        var altOda = Guid.NewGuid();
        var ustOda = Guid.NewGuid();
        var proje = new BinaProje();
        proje.Katlar.Add(new Kst.Acoustics.Core.BuildingAcoustics.Models.Kat
            { BinaGeometriKatId = "alt", KatNo = 1 });
        proje.Katlar.Add(new Kst.Acoustics.Core.BuildingAcoustics.Models.Kat
            { BinaGeometriKatId = "ust", KatNo = 2 });
        var birlesim = new DuseyMekanBirlesimi
        {
            Uyeler =
            [
                new() { KatId = "alt", KatNo = 1, MekanId = altOda, TavanAcik = false },
                new() { KatId = "ust", KatNo = 2, MekanId = ustOda, DosemeAcik = false }
            ]
        };
        proje.DuseyMekanBirlesimleri.Add(birlesim);
        if (DuseyMekanBirlesimServisi.AyniBirlesimdeAcik(proje, 1, altOda, 2, ustOda))
            throw new InvalidOperationException("Kapalı yüzey bayrakları akustik yolu bastırmamalı.");
        birlesim.Uyeler[0].TavanAcik = true;
        birlesim.Uyeler[1].DosemeAcik = true;
        if (!DuseyMekanBirlesimServisi.AyniBirlesimdeAcik(proje, 1, altOda, 2, ustOda))
            throw new InvalidOperationException("Karşılıklı açık yüzeyler akustik yolu bastırmalı.");

        var yonGeo = M50GeometriServisi.YeniOlustur("Düşey yön");
        yonGeo.Katlar.Add(Kat("ust-m50", 1, "Üst", "ust-m50-oda"));
        yonGeo.Katlar.Add(Kat("alt-m50", 2, "Alt", "alt-m50-oda"));
        var yonProje = new BinaProje();
        BinaGeometriMekanAdapteri.Uygula(yonProje, yonGeo);
        var ustKat = yonProje.Katlar.Single(k => k.BinaGeometriKatId == "ust-m50");
        var altKat = yonProje.Katlar.Single(k => k.BinaGeometriKatId == "alt-m50");
        var ustMekan = ustKat.Mekanlar.Single();
        ustMekan.ZeminYapiElemani = new YapiElemani { Ad = "Döşeme", Tip = YapiElemaniTipi.Doseme };
        var darbe = new KatlarArasiDarbeYoluServisi()
            .BulAltMekan(yonProje.Katlar, ustKat, ustMekan);
        if (darbe?.AliciKat.BinaGeometriKatId != "alt-m50")
            throw new InvalidOperationException("M50 üst=1/alt=2 sırası M08 darbe yolunda ters çevrildi.");

        var patlat = new DuseyMekanBirlesimServisi()
            .TavanPatlat(yonProje, altKat, altKat.Mekanlar.Single());
        if (!patlat.Basarili ||
            patlat.Birlesim?.Uyeler.Any(u => u.KatId == "ust-m50" && u.DosemeAcik) != true)
            throw new InvalidOperationException("M50 bağlantılı M08 tavan yönü listedeki üst kata gitmeli.");

        var hayaletKat = new Kst.Acoustics.Core.BuildingAcoustics.Models.Kat
        {
            BinaGeometriKatId = "silinmis-m50-kat", KatNo = 3, Ad = "Silinmiş"
        };
        hayaletKat.Mekanlar.Add(new Mekan
        {
            Ad = "Korunan akustik",
            ZeminYapiElemani = new YapiElemani { Ad = "Atama", Tip = YapiElemaniTipi.Doseme },
            Poligon = [new(0, 0), new(4, 0), new(4, 4), new(0, 4)], Alan = 16, Yukseklik = 3
        });
        yonProje.Katlar.Add(hayaletKat);
        BinaGeometriMekanAdapteri.Uygula(yonProje, yonGeo);
        if (!yonProje.Katlar.Contains(hayaletKat) || !string.IsNullOrWhiteSpace(hayaletKat.BinaGeometriKatId))
            throw new InvalidOperationException("Silinmiş fakat atamalı M08 katı korunurken canlı M50 KatId'sinden ayrılmalı.");

        var altMekan = altKat.Mekanlar.Single();
        altMekan.ZeminYapiElemani = new YapiElemani { Ad = "Alt döşeme", Tip = YapiElemaniTipi.Doseme };
        if (new KatlarArasiDarbeYoluServisi().BulAltMekan(yonProje.Katlar, altKat, altMekan) is not null)
            throw new InvalidOperationException("Korunan hayalet kat güncel en alt M50 katının darbe alıcısı olmamalı.");
        if (new DuseyMekanBirlesimServisi().DosemePatlat(yonProje, altKat, altMekan).Basarili)
            throw new InvalidOperationException("Korunan hayalet kat güncel en alt M50 katının alt komşusu olmamalı.");
    }

    public static void M08SinifVeRt60SilmeOncesiAnlamliVeridir()
    {
        var yordam = typeof(BinaProjesiViewModel).GetMethod(
            "MekandaM08AkustikVerisiVar", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("M08 anlamlı veri yordamı bulunamadı.");
        var yalnizSinif = new Mekan { Sinif = MekanSinifi.Alici };
        var yalnizRt60 = new Mekan { RT60 = 0.7 };
        if (yordam.Invoke(null, [yalnizSinif]) is not true || yordam.Invoke(null, [yalnizRt60]) is not true)
            throw new InvalidOperationException("Sinif veya RT60 tek başına silme onayı gerektiren M08 verisi sayılmalı.");
    }

    public static void M12MutabakatPanoVeBuatGecisOdaIdleriniKapsar()
    {
        var proje = new BasitProjeCizProje();
        var kat = new BasitProjeKat { Id = "kat", Ad = "Kat" };
        kat.Elemanlar.Add(new BasitProjeEleman
        {
            Id = "pano", Ad = "KP", Tip = BpcElemanTipi.KatPanosu,
            BagliOdaIds = ["pano-oda"]
        });
        kat.BuatPanoBaglantilari.Add(new BpcBuatPanoBaglanti
        {
            Id = "bag", LinyePrefix = "A", GecisOdaIds = ["gecis-oda"]
        });
        proje.Katlar.Add(kat);

        var kayitlar = new BasitProjeGeometriBagimliVeri(proje).BagimliKayitlar().ToList();
        if (!kayitlar.Any(k => k.Tur == "Pano oda ataması" && k.OdaId == "pano-oda") ||
            !kayitlar.Any(k => k.Tur == "Buat-pano geçişi" && k.OdaId == "gecis-oda"))
            throw new InvalidOperationException("Pano hizmet/geçiş oda Id'leri mutabakata girmedi.");

        var tasinmisGeo = M50GeometriServisi.YeniOlustur("Taşınmış oda");
        tasinmisGeo.Katlar.Add(Kat("baska-kat", 2, "Başka", "pano-oda"));
        var rapor = GeometriMutabakatServisi.Karsilastir(tasinmisGeo, kayitlar);
        if (!rapor.AskidaKayitlar.Any(k => k.Tur == "Pano oda ataması" && k.OdaId == "pano-oda"))
            throw new InvalidOperationException("Aynı OdaId başka kata taşınınca eski kat bağı askıda görünmeli.");
    }

    public static void M12ImportKilitDirtyVeAtomiklikKurallarinaUyar()
    {
        var vm = new BasitProjeCizViewModel();
        vm.Proje.Katlar.Add(new BasitProjeKat { Id = "eski", Ad = "Eski" });
        var geo = M50GeometriServisi.YeniOlustur("Yeni");
        geo.Katlar.Add(Kat("yeni", 1, "Yeni", "oda"));

        vm.ProjeKilitli = true;
        vm.BinaGeometrisiUygula(geo, "/tmp/yeni.e4bina.json");
        if (vm.Proje.Katlar.Any(k => k.Id == "yeni") || vm.IsDirty)
            throw new InvalidOperationException("Kilitli M12 projesinde geometri import uygulanmamalı/kirletmemeli.");

        vm.ProjeKilitli = false;
        vm.BinaGeometrisiUygula(geo, "/tmp/yeni.e4bina.json");
        if (vm.Proje.Katlar.All(k => k.Id != "yeni") || !vm.IsDirty)
            throw new InvalidOperationException("Başarılı M12 import projeyi kirletmeli.");

        var oncekiProje = vm.Proje;
        var oncekiKatIdleri = vm.Proje.Katlar.Select(k => k.Id).ToList();
        var bozuk = M50GeometriServisi.YeniOlustur("Bozuk");
        bozuk.Katlar.Add(Kat("ayni", 1, "A", "a"));
        bozuk.Katlar.Add(Kat("ayni", 2, "B", "b"));
        try
        {
            vm.BinaGeometrisiUygula(bozuk, "/tmp/bozuk.e4bina.json");
            throw new InvalidOperationException("Bozuk geometri import hata vermeliydi.");
        }
        catch (InvalidOperationException ex) when (!ex.Message.StartsWith("Bozuk geometri import"))
        {
            // Beklenen doğrulama hatası.
        }
        if (!ReferenceEquals(vm.Proje, oncekiProje) ||
            !vm.Proje.Katlar.Select(k => k.Id).SequenceEqual(oncekiKatIdleri))
            throw new InvalidOperationException("Başarısız M12 import canlı projeyi kısmen değiştirdi.");

        var tasimaProjesi = new BasitProjeCizProje();
        var eskiKat = new BasitProjeKat { Id = "eski-kat", Ad = "Eski" };
        eskiKat.Odalar.Add(new BasitProjeOda
        {
            Id = "tasinan", Ad = "Taşınan", GecisSecili = true,
            AsmaTavan = true, MahalListesineDahil = false
        });
        // Kat, oda taşındıktan sonra da bağımlı elektrik verisi nedeniyle korunur.
        eskiKat.Elemanlar.Add(new BasitProjeEleman
        {
            Id = "eski-eleman", Ad = "Eski eleman", Tip = BpcElemanTipi.Priz
        });
        tasimaProjesi.Katlar.Add(eskiKat);
        var tasimaGeo = M50GeometriServisi.YeniOlustur("Taşıma");
        tasimaGeo.Katlar.Add(Kat("yeni-kat", 2, "Yeni", "tasinan"));
        BinaGeometriImportServisi.Uygula(tasimaProjesi, tasimaGeo, null);
        var tasinanOdalar = tasimaProjesi.Katlar.SelectMany(k => k.Odalar)
            .Where(o => o.Id == "tasinan").ToList();
        if (tasinanOdalar.Count != 1 ||
            tasimaProjesi.Katlar.Single(k => k.Id == "yeni-kat").Odalar.Single().Id != "tasinan")
            throw new InvalidOperationException("Kata taşınan M12 odası yalnız yeni hedef katta kalmalı.");
        var tasinan = tasinanOdalar.Single();
        if (!tasinan.GecisSecili || !tasinan.AsmaTavan || tasinan.MahalListesineDahil)
            throw new InvalidOperationException("Çapraz-kat oda taşımasında M12 oda bayrakları kayboldu.");
    }

    public static void M12EfektifMontajOverrideAcikTavanFiltresiniBelirler()
    {
        var kutuphane = new BasitProjeCizKutuphaneServisi();
        var tanim = new BpcArmatürTanim
        {
            Id = "test-armatur", Ad = "Test", BlokAdi = "TEST_ARMATUR",
            ElemanTipi = BpcElemanTipi.Armatür, MontajTipi = "Tavan"
        };
        kutuphane.Armatürler.Add(tanim);
        var kat = new BasitProjeKat { Id = "kat", Ad = "Kat" };
        var oda = new BasitProjeOda
        {
            Id = "oda", Ad = "Oda", AlanM2 = 16,
            Poligon = [new(0, 0), new(4, 0), new(4, 4), new(0, 4)]
        };
        kat.Odalar.Add(oda);
        var satir = new BpcMahalSatiri
        {
            Tip = BpcElemanTipi.Armatür, ArmatürTanimId = tanim.Id,
            YerlesimTipi = "Adet", MontajTipi = "Tavan"
        };
        var ayar = satir.HucreGetirVeyaOlustur(oda.Id);
        ayar.Aktif = true;
        ayar.Adet = 1;
        ayar.GenelAyarKullan = false;
        ayar.YerlesimTipi = "Adet";
        ayar.MontajTipi = "Duvar";
        kat.MahalListesi.Add(satir);

        var servis = new BasitProjeCizOtomatikYerlestirServisi(new BasitProjeCizGeometriServisi());
        var (duvarOverride, _) = servis.TumMahalListesindenYerlestir(
            kat, kutuphane, false, new HashSet<string> { oda.Id });
        if (duvarOverride.Count == 0)
            throw new InvalidOperationException("Tavan varsayılanını Duvar'a override eden hücre açık tavanda yerleşebilmeliydi.");

        tanim.MontajTipi = "Duvar";
        ayar.MontajTipi = "Tavan";
        var (tavanOverride, _) = servis.TumMahalListesindenYerlestir(
            kat, kutuphane, false, new HashSet<string> { oda.Id });
        if (tavanOverride.Count != 0)
            throw new InvalidOperationException("Duvar varsayılanını Tavan'a override eden hücre açık tavanda atlanmalıydı.");

        // Tek satır yenileme yolu aynı efektif montaj semantiğini kullanmalı.
        tanim.MontajTipi = "Tavan";
        ayar.MontajTipi = "Duvar";
        var vm = new BasitProjeCizViewModel();
        vm.Kutuphane.Armatürler.Add(tanim);
        vm.Proje.Katlar.Add(kat);
        vm.SeciliKat = kat;
        var geo = M50GeometriServisi.YeniOlustur("Açık tavan");
        geo.Katlar.Add(Kat(kat.Id, 1, kat.Ad, oda.Id));
        geo.DuseyBirlesimleri.Add(new BinaDuseyBirlesim
        {
            Uyeler = [new BinaBirlesimUyesi
                { KatId = kat.Id, KatNo = 1, OdaId = oda.Id, TavanAcik = true }]
        });
        vm.Proje.BinaGeometriKopya = geo;
        vm.MahalSatiriYenidenYerlestir(kat, satir, oda.Id);
        if (kat.Elemanlar.Count == 0)
            throw new InvalidOperationException("Tek-satır yenileme Duvar override'ını açık tavanda yerleştirmeliydi.");

        kat.Elemanlar.Clear();
        tanim.MontajTipi = "Duvar";
        ayar.MontajTipi = "Tavan";
        vm.MahalSatiriYenidenYerlestir(kat, satir, oda.Id);
        if (kat.Elemanlar.Count != 0)
            throw new InvalidOperationException("Tek-satır yenileme Tavan override'ını açık tavanda atlamalıydı.");
    }

    public static void M12BozukSidecarMigrasyonuGuvenliDosyayaAyrilir()
    {
        var dizin = Path.Combine(Path.GetTempPath(), $"m12-bozuk-sidecar-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dizin);
        try
        {
            var projeYolu = Path.Combine(dizin, "Proje.e4p12");
            var yanYol = Path.Combine(dizin, "Proje.e4bina.json");
            File.WriteAllText(yanYol, "{ bozuk-json");
            var proje = new BasitProjeCizProje();
            var kat = new BasitProjeKat { Id = "kat", Ad = "Kat" };
            kat.Odalar.Add(new BasitProjeOda
            {
                Id = "oda", Ad = "Oda", Poligon = [new(0, 0), new(3, 0), new(3, 3), new(0, 3)]
            });
            proje.Katlar.Add(kat);

            var not = BinaGeometriExportServisi.EskiProjeyiMigrateEt(proje, projeYolu);
            if (not is null || proje.BinaGeometriYolu == yanYol ||
                string.IsNullOrWhiteSpace(proje.BinaGeometriYolu) || !File.Exists(proje.BinaGeometriYolu))
                throw new InvalidOperationException("Bozuk M12 sidecar korunup benzersiz güvenli hedef oluşturulmadı.");
            if (File.ReadAllText(yanYol) != "{ bozuk-json")
                throw new InvalidOperationException("Bozuk mevcut sidecar üzerine yazılmamalıydı.");
        }
        finally
        {
            Directory.Delete(dizin, true);
        }
    }

    public static void M12AcilisGeometriMigrasyonuProjeyiKirletir()
    {
        var dizin = Path.Combine(Path.GetTempPath(), $"m12-dirty-mig-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dizin);
        try
        {
            var projeYolu = Path.Combine(dizin, "Eski.e4p12");
            var proje = new BasitProjeCizProje
            {
                SemaSurum = BasitProjeCizOtomatikYerlestirServisi.GuncelSemaSurum
            };
            var kat = new BasitProjeKat { Id = "kat", Ad = "Kat" };
            kat.Odalar.Add(new BasitProjeOda
            {
                Id = "oda", Ad = "Oda", Poligon = [new(0, 0), new(3, 0), new(3, 3), new(0, 3)]
            });
            proje.Katlar.Add(kat);
            var vm = new BasitProjeCizViewModel();
            vm.ProjeyiYukle(proje, projeYolu);
            if (!vm.IsDirty || vm.Proje.BinaGeometriKopya is null)
                throw new InvalidOperationException("M12 açılış geometri migrasyonu kaydetme gerektiren dirty durum üretmeli.");
        }
        finally
        {
            Directory.Delete(dizin, true);
        }
    }

    public static void M12SidecarYoluGoreliKaydolurVeDurumlariAyirir()
    {
        var dizin = Path.Combine(Path.GetTempPath(), $"m12-goreli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dizin);
        try
        {
            var projeYolu = Path.Combine(dizin, "Proje.e4p12");
            var geoYolu = Path.Combine(dizin, "Proje.e4bina.json");
            var geo = M50GeometriServisi.YeniOlustur("Bina");
            geo.Katlar.Add(Kat("kat", 1, "Kat", "oda"));
            M50GeometriServisi.Kaydet(geo, geoYolu);

            var vm = new BasitProjeCizViewModel { ProjeDosyaYolu = projeYolu };
            vm.BinaGeometrisiUygula(geo, geoYolu);
            if (!vm.BinaGeometriDurumMetni.StartsWith("Güncel"))
                throw new InvalidOperationException("Eşit revizyon sidecar durumu Güncel olmalı.");
            if (!vm.ProjeyiKaydet(projeYolu))
                throw new InvalidOperationException("M12 proje kaydı başarısız.");
            using var belge = JsonDocument.Parse(File.ReadAllText(projeYolu));
            var saklanan = belge.RootElement.GetProperty("BinaGeometriYolu").GetString();
            if (string.IsNullOrWhiteSpace(saklanan) || Path.IsPathRooted(saklanan))
                throw new InvalidOperationException("M12 sidecar yolu proje içinde göreli saklanmalı.");

            vm.Proje.BinaGeometriRevizyon = geo.GeometriRevizyon + 1;
            if (!vm.BinaGeometriDurumMetni.StartsWith("Dosya geri revizyonda"))
                throw new InvalidOperationException("Geri revizyon sidecar durumu ayrı gösterilmeli.");
            File.Delete(geoYolu);
            if (!vm.BinaGeometriDurumMetni.StartsWith("Dosya Yok"))
                throw new InvalidOperationException("Eksik sidecar durumu Dosya Yok olmalı.");
            vm.Proje.BinaGeometriYolu = "\0";
            if (vm.BinaGeometriDosyasiVar ||
                !vm.BinaGeometriDurumMetni.StartsWith("Dosya yolu geçersiz"))
                throw new InvalidOperationException("Bozuk M12 sidecar yolu UI durum getter'ından istisna üretmemeli.");
        }
        finally
        {
            Directory.Delete(dizin, true);
        }
    }

    public static void M08GoreliSidecarRevizyonDurumlariniAyirir()
    {
        var dizin = Path.Combine(Path.GetTempPath(), $"m08-goreli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dizin);
        try
        {
            var projeYolu = Path.Combine(dizin, "Akustik.e4p08");
            var geoYolu = Path.Combine(dizin, "Akustik.e4bina.json");
            var geo = M50GeometriServisi.YeniOlustur("Bina");
            geo.Katlar.Add(Kat("kat", 1, "Kat", "oda"));
            M50GeometriServisi.Kaydet(geo, geoYolu);
            var proje = new BinaProje
            {
                BinaGeometriYolu = Path.GetFileName(geoYolu),
                BinaGeometriRevizyon = geo.GeometriRevizyon,
                BinaGeometriKopya = geo
            };

            if (BinaGeometriM08ExportServisi.GuncellikUyarisi(proje, projeYolu) is not null)
                throw new InvalidOperationException("M08 eşit revizyon bağlı sidecar güncel sayılmalı.");
            proje.BinaGeometriRevizyon++;
            var geri = BinaGeometriM08ExportServisi.GuncellikUyarisi(proje, projeYolu);
            if (geri is null || !geri.Contains("geri revizyonda"))
                throw new InvalidOperationException("M08 geri revizyon durumu ayrı uyarılmalı.");
            File.Delete(geoYolu);
            var eksik = BinaGeometriM08ExportServisi.GuncellikUyarisi(proje, projeYolu);
            if (eksik is null || !eksik.Contains("bulunamadı"))
                throw new InvalidOperationException("M08 göreli sidecar eksikliği çözümlenemedi.");
            proje.BinaGeometriYolu = "\0";
            var vm = new BinaProjesiViewModel { Proje = proje };
            if (vm.BinaGeometriDosyasiVar ||
                !vm.BinaGeometriDurumMetni.StartsWith("Dosya yolu geçersiz"))
                throw new InvalidOperationException("Bozuk M08 sidecar yolu UI durum getter'ından istisna üretmemeli.");
        }
        finally
        {
            Directory.Delete(dizin, true);
        }
    }

    public static void EskiM08M12GeometriRelayKomutlariYayinlanmaz()
    {
        string[] m08Komutlari = ["TavanPatlatCommand", "DosemePatlatCommand", "DuseyBirlesimiKaldirCommand"];
        foreach (var ad in m08Komutlari)
            if (typeof(BinaProjesiViewModel).GetProperty(ad, BindingFlags.Instance | BindingFlags.Public) is not null)
                throw new InvalidOperationException($"M08 yerel geometri komutu hâlâ yayınlanıyor: {ad}");

        string[] m12Komutlari =
        [
            "AcikliklariTespitEtCommand", "ManuelAciklikModuAcKapatCommand",
            "OdalariYenidenTaraCommand", "BenzerKatOlusturCommand", "KatSilCommand",
            "KatYukariCommand", "KatAsagiCommand", "KatlariHizalaCommand"
        ];
        foreach (var ad in m12Komutlari)
            if (typeof(BasitProjeCizViewModel).GetProperty(ad, BindingFlags.Instance | BindingFlags.Public) is not null)
                throw new InvalidOperationException($"M12 eski geometri komutu hâlâ yayınlanıyor: {ad}");
    }

    private static BinaKat Kat(string id, int katNo, string ad, string odaId)
    {
        var kat = new BinaKat { Id = id, KatNo = katNo, Ad = ad, KatYuksekligiM = 3 };
        kat.Odalar.Add(new BinaOda
        {
            Id = odaId, Ad = odaId, AlanM2 = 16,
            Poligon = [new(0, 0), new(4, 0), new(4, 4), new(0, 4)]
        });
        return kat;
    }
}
