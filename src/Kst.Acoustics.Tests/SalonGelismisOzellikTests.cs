using System;
using System.IO;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Salon.Models;
using Kst.Acoustics.Core.Salon.Services;

namespace Kst.Acoustics.Tests;

public static class SalonGelismisOzellikTests
{
    public static void GercekKoltukYerlesimiKoridorVeErisilebilirYerUretir()
    {
        var s = new SeyirciSekciyonu
        {
            GenislikM = 12, DerinlikM = 9, MerkezY = 7,
            KademeSayisi = 10, KademeYuksekligiM = 0.30,
            KoltukGenisligiM = 0.55, SiraAraligiM = 0.90,
            YanKoridorGenisligiM = 1.20, OrtaKoridorSayisi = 1,
            OrtaKoridorGenisligiM = 1.20, TekerlekliSandalyeYeri = 2
        };
        var y = SalonKoltukYerlesimServisi.Uret(s);
        if (y.Siralar.Count != 10 || y.NormalKoltukSayisi <= 0 || y.TekerlekliSandalyeYeriSayisi != 2)
            throw new InvalidOperationException("Koridorlu ve erişilebilir gerçek koltuk yerleşimi üretilemedi.");
        if (y.Koltuklar.Any(k => Math.Abs(k.Konum.X - s.MerkezX) > s.GenislikM / 2 + 1e-9 ||
                                Math.Abs(k.Konum.Y - s.MerkezY) > s.DerinlikM / 2 + 1e-9))
            throw new InvalidOperationException("Bir koltuk seyirci alanının dışına taştı.");
    }

    public static void GorusHattiCegeriniVeIsiPuaniUretir()
    {
        var salon = OrnekSalon();
        var s = salon.SeyirciSekciyonlari[0];
        var y = SalonKoltukYerlesimServisi.Uret(s, salon.Sahne);
        var g = SalonGorusHattiServisi.AnalizEt(salon, s);
        if (g.Koltuklar.Count != y.Koltuklar.Count || g.Koltuklar.Count == 0)
            throw new InvalidOperationException("Görüş raporu tüm koltukları kapsamadı.");
        if (g.Koltuklar.Any(x => x.GorunurlukPuani is < 0 or > 1) ||
            g.Koltuklar.Where(x => double.IsFinite(x.CDegeriMm)).Any(x => !double.IsFinite(x.CDegeriMm)))
            throw new InvalidOperationException("C-değeri veya ısı haritası puanı geçersiz.");
    }

    public static void KesitVeAkustikOnKontrolTumBilesenleriUretir()
    {
        var salon = OrnekSalon();
        salon.YansiticiPaneller.Add(SalonYansiticiServisi.OtomatikDizi(salon, 1, 1)[0]);
        var kesit = SalonKesitServisi.BoyunaKesit(salon, 0);
        var tipler = kesit.Cizgiler.Select(x => x.Tip).ToHashSet();
        foreach (var tip in new[] { SalonKesitCizgiTipi.Zemin, SalonKesitCizgiTipi.Tavan,
                                    SalonKesitCizgiTipi.Sahne, SalonKesitCizgiTipi.Seyirci,
                                    SalonKesitCizgiTipi.Yansitici })
            if (!tipler.Contains(tip)) throw new InvalidOperationException($"Kesitte {tip} eksik.");

        var a = SalonAkustikOnKontrolServisi.Hesapla(salon);
        if (a.HavaHacmiM3 <= 0 || a.ToplamYuzeyAlaniM2 <= 0 || a.KoltukSayisi <= 0 || a.RtBantlari.Count != 6 ||
            a.RtBantlari.Any(x => !double.IsFinite(x.TahminiRt60S) || x.TahminiRt60S <= 0))
            throw new InvalidOperationException("Akustik ön kontrol hacim, koltuk veya RT bantlarını üretemedi.");
    }

    public static void YansiticiYuzeyiVeYansimaHattiDogru()
    {
        var p = new SalonYansiticiPanel
        {
            MerkezX = 0, MerkezY = 0, MerkezZ = 5, GenislikM = 4, DerinlikM = 2
        };
        var yuzey = SalonYansiticiServisi.YuzeyOlustur(p, 7);
        if (Math.Abs(SurfaceTransformService.ComputeArea(yuzey.Vertices) - 8) > 1e-8)
            throw new InvalidOperationException("Yansıtıcı panel alanı yanlış.");
        var hat = SalonYansiticiServisi.YansimaHatti(
            p, new(0, -2, 1), new(0, 2, 1));
        if (hat is null || !hat.PanelIcinde || Math.Abs(hat.YansimaNoktasi.Z - 5) > 1e-8)
            throw new InvalidOperationException("Yansıma hattı panel üzerinde çözülemedi.");
    }

    public static void OlcuVeKisitGeometriyiAtomikGunceller()
    {
        var salon = SalonGeometriServisi.YeniOlustur();
        var oge = new SalonCizimOgesi
        {
            Id = "cizgi", Tip = SalonCizimTipi.Cizgi,
            Noktalar = [new(0, 0), new(3, 4)]
        };
        salon.CizimOgeleri.Add(oge);
        var olcu = new SalonOlculendirme
        {
            OgeId = oge.Id, BaslangicNoktaIndeksi = 0, BitisNoktaIndeksi = 1
        };
        SalonOlculendirmeServisi.DegerUygula(salon, olcu, 10);
        if (Math.Abs(SalonOlculendirmeServisi.Hesapla(salon, olcu).Deger - 10) > 1e-8)
            throw new InvalidOperationException("Bağlı ölçü geometriyi güncellemedi.");
        salon.GeometrikKisitlar.Add(new SalonGeometrikKisit
        {
            Tip = SalonKisitTipi.Yatay, OgeId = oge.Id, NoktaIndeksi = 0, IkinciNoktaIndeksi = 1
        });
        var sonuc = SalonKisitServisi.Uygula(salon);
        if (!sonuc.Basarili || Math.Abs(oge.Noktalar[0].Y - oge.Noktalar[1].Y) > 1e-9)
            throw new InvalidOperationException("Yatay kısıt uygulanamadı.");
    }

    public static void GelismisCadPahDiziGrupVeYapistirUretir()
    {
        var pah = SalonGelismisCadServisi.PahKir(new(0, 4), new(0, 0), new(4, 0), 1);
        if (pah.Yay is null || pah.Birinci.Noktalar.Count != 2 || pah.Ikinci.Noktalar.Count != 2)
            throw new InvalidOperationException("Pah geometrisi üretilemedi.");
        var kaynak = new[] { pah.Birinci, pah.Ikinci };
        var dizi = SalonGelismisCadServisi.DogrusalDizi(kaynak, 3, 5, 0);
        if (dizi.Count != 6) throw new InvalidOperationException("Doğrusal dizi sayısı yanlış.");
        string grup = SalonGelismisCadServisi.Grupla(kaynak);
        if (kaynak.Any(x => x.GrupId != grup)) throw new InvalidOperationException("Gruplama başarısız.");
        var pano = SalonGelismisCadServisi.PanoyaKopyala(kaynak);
        var yapisan = SalonGelismisCadServisi.Yapistir(pano, new(10, 20));
        if (yapisan.Count != 2 || yapisan.SelectMany(x => x.Noktalar).Min(x => x.X) < 9.999)
            throw new InvalidOperationException("Pano yapıştırma baz noktasını korumadı.");
    }

    public static void AltlikKalibrasyonVeDonusumDogru()
    {
        var a = new SalonReferansAltlik { OfsetX = 1, OfsetY = 2, DonusDerece = 90 };
        SalonAltlikServisi.IkiNoktaylaOlceklendir(a, new(0, 0), new(200, 0), 10);
        var q = SalonAltlikServisi.Donustur(a, new(200, 0));
        if (Math.Abs(a.Olcek - 0.05) > 1e-10 || Math.Abs(q.X - 1) > 1e-9 || Math.Abs(q.Y - 12) > 1e-9)
            throw new InvalidOperationException("Altlık iki noktalı kalibrasyon/dönüşümü yanlış.");
    }

    public static void KaliteHaritasiOdakliHataUretir()
    {
        var salon = OrnekSalon();
        salon.Acikliklar.Add(new DuvarAcikligi
        {
            Ad = "Bağsız kapı", MerkezX = 100, MerkezY = 100,
            GenislikM = 1, YukseklikM = 2, AltKotM = 0
        });
        var rapor = SalonKaliteHaritasiServisi.AnalizEt(salon);
        var bulgu = rapor.Bulgular.FirstOrDefault(x => x.Kod == "BAGSIZ_ACIKLIK");
        if (bulgu?.OdakNoktasi is null || rapor.HataSayisi == 0)
            throw new InvalidOperationException("Kalite haritası odaklı açıklık hatası üretmedi.");
    }

    public static void SurumGecmisiRevizyonVeYoluDegistirmedenKurtarir()
    {
        string klasor = Path.Combine(Path.GetTempPath(), "e4-m51-history-" + Guid.NewGuid().ToString("N"));
        try
        {
            var salon = OrnekSalon();
            salon.DosyaYolu = "/tmp/original.e4salon.json";
            salon.GeometriRevizyon = 9;
            var surum = SalonSurumGecmisiServisi.OtomatikKaydet(salon, klasor);
            if (salon.DosyaYolu != "/tmp/original.e4salon.json" || salon.GeometriRevizyon != 9)
                throw new InvalidOperationException("Otomatik kayıt ana belgenin metadata'sını değiştirdi.");
            var geri = SalonSurumGecmisiServisi.GeriYukle(surum);
            if (geri.SalonAdi != salon.SalonAdi || geri.GeometriRevizyon != 9 ||
                SalonSurumGecmisiServisi.Listele(klasor).Count != 1)
                throw new InvalidOperationException("Sürüm geçmişi listelenemedi veya geri yüklenemedi.");
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        }
    }

    public static void HizliTasarimVeKomutPaletiKullanimaHazirSonucUretir()
    {
        var sonuc = SalonHizliTasarimServisi.Olustur(new SalonHizliTasarimIstegi(
            120, SalonKullanimTipi.Konusma, SalonTipi.Yelpaze, YansiticiEkle: false));
        if (sonuc.UretilenKoltuk < 100 || sonuc.GorusRaporu is null ||
            sonuc.AkustikOnKontrol.RtBantlari.Count != 6 || sonuc.Salon.CizimOgeleri.Count == 0)
            throw new InvalidOperationException("Hızlı tasarım kullanılabilir salon/analiz üretmedi.");
        var komut = SalonKomutPaletiServisi.Ara("rt60").FirstOrDefault();
        if (komut?.Id != "tools.acoustic")
            throw new InvalidOperationException("Komut paleti akustik aramasını bulamadı.");
    }

    public static void EditorAyarlariGuvenliRoundTripYapar()
    {
        string yol = Path.Combine(Path.GetTempPath(), "e4-m51-settings-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var ayar = new SalonEditorAyarlari
            {
                GridAcik = true, GridAraligiM = 0.25, OrtoAcik = true,
                PolarAcik = true, OtomatikKayitDakika = 4
            };
            SalonEditorAyarServisi.Kaydet(ayar, yol);
            var geri = SalonEditorAyarServisi.Yukle(yol);
            if (!geri.GridAcik || Math.Abs(geri.GridAraligiM - 0.25) > 1e-9 ||
                !geri.OrtoAcik || geri.PolarAcik || geri.OtomatikKayitDakika != 4)
                throw new InvalidOperationException("Editör ayarları normalize edilerek korunmadı.");
        }
        finally
        {
            if (File.Exists(yol)) File.Delete(yol);
        }
    }

    private static SalonGeometri OrnekSalon() => SalonParametrikServisi.Olustur(new SalonSablonu
    {
        SalonTip = SalonTipi.Dikdortgen,
        SalonGenislikM = 18,
        SalonDerinlikM = 24,
        SalonYuksekligiM = 9,
        SahneTip = SahneTipi.Dikdortgen,
        SahneKonum = SahneKonum.SalonIci,
        SahneGenislikM = 10,
        SahneDerinlikM = 5,
        SahneYuksekligiM = 0.8,
        SeyirciKademe = 12,
        SeyirciKademeYukM = 0.30
    });
}
