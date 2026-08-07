using System;
using System.IO;
using System.Linq;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

namespace Kst.Acoustics.Tests;

/// <summary>
/// M12 mahal listesi üretkenlik paketi (codex12 §20): kattan kopyalama, akıllı
/// doldurma, şablon kaydet/yükle ve yerleşim ön-tahmini regresyonları.
/// </summary>
public static class MahalListesiServisiTests
{
    // ─── Oda adı normalize ──────────────────────────────────────────────────

    public static void OdaAdiNormalizeTurkceKarakterVeSayiyiSadelestirir()
    {
        var a = MahalListesiServisi.OdaAdiNormalize("Yatak Odası 2");
        var b = MahalListesiServisi.OdaAdiNormalize("YATAK ODASI-2");
        if (a != b)
            throw new InvalidOperationException($"Normalize eşleşmeliydi: '{a}' != '{b}'.");
        if (a != "yatakodasi")
            throw new InvalidOperationException($"Beklenen 'yatakodasi', bulunan '{a}'.");
    }

    // ─── 1) Kattan kopyalama ────────────────────────────────────────────────

    public static void KattanKopyalaOdaAdinaGoreEslesirVeAktifHucreyiEzmez()
    {
        var kaynak = new BasitProjeKat { Ad = "Zemin Kat" };
        var yatak = new BasitProjeOda { Ad = "Yatak Odası 1", Poligon = [new(0, 0), new(4, 0), new(4, 4), new(0, 4)] };
        var mutfak = new BasitProjeOda { Ad = "Mutfak", Poligon = [new(5, 0), new(9, 0), new(9, 4), new(5, 4)] };
        kaynak.Odalar.Add(yatak);
        kaynak.Odalar.Add(mutfak);

        var satir = new BpcMahalSatiri { Tip = BpcElemanTipi.Armatür, ArmatürTanimId = "arm-1", YerlesimTipi = "Adet" };
        var ayarYatak = satir.HucreGetirVeyaOlustur(yatak.Id);
        ayarYatak.Aktif = true; ayarYatak.Adet = 2;
        var ayarMutfak = satir.HucreGetirVeyaOlustur(mutfak.Id);
        ayarMutfak.Aktif = true; ayarMutfak.Adet = 3;
        kaynak.MahalListesi.Add(satir);

        // Hedef katta aynı adlı odalar var (farklı Id) + biri zaten elle 5 adet açılmış.
        var hedef = new BasitProjeKat { Ad = "1. Kat" };
        var hedefYatak = new BasitProjeOda { Ad = "yatak odası 1", Poligon = [new(0, 0), new(4, 0), new(4, 4), new(0, 4)] };
        var hedefMutfak = new BasitProjeOda { Ad = "Mutfak", Poligon = [new(5, 0), new(9, 0), new(9, 4), new(5, 4)] };
        var hedefBaska = new BasitProjeOda { Ad = "Balkon", Poligon = [new(10, 0), new(12, 0), new(12, 2), new(10, 2)] };
        hedef.Odalar.Add(hedefYatak);
        hedef.Odalar.Add(hedefMutfak);
        hedef.Odalar.Add(hedefBaska);

        // Hedefte kullanıcı zaten mutfağı elle 5 adet olarak açmış — kopyalama bunu EZMEMELİ.
        var hedefSatir = new BpcMahalSatiri { Tip = BpcElemanTipi.Armatür, ArmatürTanimId = "arm-1", YerlesimTipi = "Adet" };
        var oncedenAcik = hedefSatir.HucreGetirVeyaOlustur(hedefMutfak.Id);
        oncedenAcik.Aktif = true; oncedenAcik.Adet = 5;
        hedef.MahalListesi.Add(hedefSatir);

        var sonuc = MahalListesiServisi.KattanKopyala(kaynak, hedef);

        if (sonuc.EklenenSatir != 0 || sonuc.GuncellenenSatir != 1)
            throw new InvalidOperationException("Aynı kalem hedefte zaten vardı — güncellenmeliydi, yeni eklenmemeliydi.");
        if (sonuc.EslesenOda != 2)
            throw new InvalidOperationException($"2 oda eşleşmeliydi (yatak, mutfak). Bulunan: {sonuc.EslesenOda}.");

        var yatakAyar = hedefSatir.HucreGetirVeyaOlustur(hedefYatak.Id);
        if (!yatakAyar.Aktif || yatakAyar.Adet != 2)
            throw new InvalidOperationException($"Yatak odası kopyalanmalıydı (adet=2). Bulunan: Aktif={yatakAyar.Aktif}, Adet={yatakAyar.Adet}.");

        var mutfakAyar = hedefSatir.HucreGetirVeyaOlustur(hedefMutfak.Id);
        if (mutfakAyar.Adet != 5)
            throw new InvalidOperationException($"Kullanıcının elle açtığı mutfak hücresi (adet=5) EZİLMEMELİYDİ. Bulunan: {mutfakAyar.Adet}.");

        var balkonAyar = hedefSatir.HucreGetirVeyaOlustur(hedefBaska.Id);
        if (balkonAyar.Aktif)
            throw new InvalidOperationException("Kaynakta olmayan oda (Balkon) aktif olmamalıydı.");
    }

    public static void KattanKopyalaEslesmeyenOdaPasifKalir()
    {
        var kaynak = new BasitProjeKat { Ad = "Kaynak" };
        var oda = new BasitProjeOda { Ad = "Depo", Poligon = [new(0, 0), new(3, 0), new(3, 3), new(0, 3)] };
        kaynak.Odalar.Add(oda);
        var satir = new BpcMahalSatiri { Tip = BpcElemanTipi.Priz, ArmatürTanimId = "priz-1" };
        var ayar = satir.HucreGetirVeyaOlustur(oda.Id);
        ayar.Aktif = true; ayar.Adet = 4;
        kaynak.MahalListesi.Add(satir);

        var hedef = new BasitProjeKat { Ad = "Hedef" };
        var farkliOda = new BasitProjeOda { Ad = "Ofis", Poligon = [new(0, 0), new(3, 0), new(3, 3), new(0, 3)] };
        hedef.Odalar.Add(farkliOda);

        var sonuc = MahalListesiServisi.KattanKopyala(kaynak, hedef);
        if (sonuc.EslesmeyenOda != 1)
            throw new InvalidOperationException("Ad eşleşmeyen oda sayılmalıydı.");

        var hedefSatir = hedef.MahalListesi.Single();
        if (hedefSatir.HucreGetirVeyaOlustur(farkliOda.Id).Aktif)
            throw new InvalidOperationException("Adı eşleşmeyen odada hücre aktif OLMAMALI.");
    }

    // ─── 2) Akıllı doldurma ─────────────────────────────────────────────────

    public static void AkilliDoldurOdaAdindanKalemUretirVeAktifHucreyiEzmez()
    {
        var kutuphane = new BasitProjeCizKutuphaneServisi();
        var kat = new BasitProjeKat();
        var yatak = new BasitProjeOda { Ad = "Yatak Odası 1", Poligon = [new(0, 0), new(4, 0), new(4, 4), new(0, 4)] };
        var tanimsizOda = new BasitProjeOda { Ad = "Sürpriz Mahal", Poligon = [new(5, 0), new(8, 0), new(8, 3), new(5, 3)] };
        kat.Odalar.Add(yatak);
        kat.Odalar.Add(tanimsizOda);

        var sonuc = MahalListesiServisi.AkilliDoldur(kat, kutuphane);

        if (sonuc.EklenenSatir == 0 || sonuc.AktifEdilenHucre == 0)
            throw new InvalidOperationException("Yatak odası için kalem/hücre üretilmeliydi.");
        if (!sonuc.TaninmayanOdalar.Contains("Sürpriz Mahal"))
            throw new InvalidOperationException("Tanınmayan oda raporda görünmeliydi.");

        var armaturSatir = kat.MahalListesi.FirstOrDefault(s =>
            BasitProjeCizKabloServisi.ElemanArmaturMi(s.Tip));
        if (armaturSatir == null)
            throw new InvalidOperationException("Armatür satırı üretilmeliydi.");
        if (!armaturSatir.HucreGetirVeyaOlustur(yatak.Id).Aktif)
            throw new InvalidOperationException("Yatak odasındaki armatür hücresi aktif olmalıydı.");

        // İkinci çalıştırma: kullanıcının elle değiştirdiği adet EZİLMEMELİ.
        var ayar = armaturSatir.HucreGetirVeyaOlustur(yatak.Id);
        ayar.Adet = 99; // kullanıcı elle değiştirdi
        MahalListesiServisi.AkilliDoldur(kat, kutuphane);
        if (ayar.Adet != 99)
            throw new InvalidOperationException($"Zaten aktif hücrenin adedi korunmalıydı. Bulunan: {ayar.Adet}.");
    }

    public static void AkilliDoldurMahalListesineDahilOlmayanOdayiAtlar()
    {
        var kutuphane = new BasitProjeCizKutuphaneServisi();
        var kat = new BasitProjeKat();
        var oda = new BasitProjeOda
        {
            Ad = "Mutfak", MahalListesineDahil = false,
            Poligon = [new(0, 0), new(4, 0), new(4, 4), new(0, 4)]
        };
        kat.Odalar.Add(oda);

        var sonuc = MahalListesiServisi.AkilliDoldur(kat, kutuphane);
        if (sonuc.AktifEdilenHucre != 0)
            throw new InvalidOperationException("Dahil olmayan oda için hücre aktif edilmemeliydi.");
    }

    public static void OneriBulEnUzunAnahtariSecer()
    {
        var oneri = MahalListesiServisi.OneriBul("Çocuk Odası");
        if (oneri == null || oneri.Ad != "Yatak Odası")
            throw new InvalidOperationException($"'Çocuk Odası' Yatak Odası önerisine düşmeliydi. Bulunan: {oneri?.Ad}.");
    }

    // ─── 3) Şablon kaydet/yükle ─────────────────────────────────────────────

    public static void SablonaDonusturVeUygulaRoundTrip()
    {
        var kaynakKat = new BasitProjeKat();
        var oda1 = new BasitProjeOda { Ad = "Ofis 1", Poligon = [new(0, 0), new(4, 0), new(4, 4), new(0, 4)] };
        kaynakKat.Odalar.Add(oda1);

        var satir = new BpcMahalSatiri { Tip = BpcElemanTipi.Priz, ArmatürTanimId = "priz-x", ArmatürTanimAd = "Priz X" };
        var ayar = satir.HucreGetirVeyaOlustur(oda1.Id);
        ayar.Aktif = true; ayar.Adet = 6; ayar.YerlesimTipi = "Homojen Duvar";
        kaynakKat.MahalListesi.Add(satir);
        // Pasif hücre şablona GİRMEMELİ (yalnız aktifler saklanır).
        var oda2 = new BasitProjeOda { Ad = "Boş Oda", Poligon = [new(5, 0), new(8, 0), new(8, 3), new(5, 3)] };
        kaynakKat.Odalar.Add(oda2);
        satir.HucreGetirVeyaOlustur(oda2.Id).Aktif = false;

        var sablon = MahalListesiServisi.SablonaDonustur(kaynakKat, "Ofis Standardı");
        if (sablon.Satirlar.Count != 1 || sablon.Satirlar[0].OdaAdiHucreleri.Count != 1)
            throw new InvalidOperationException("Şablon yalnız aktif hücreleri içermeliydi.");

        var hedefKat = new BasitProjeKat();
        var hedefOda = new BasitProjeOda { Ad = "ofis 1", Poligon = [new(0, 0), new(4, 0), new(4, 4), new(0, 4)] };
        hedefKat.Odalar.Add(hedefOda);

        var sonuc = MahalListesiServisi.SablonUygula(sablon, hedefKat);
        if (sonuc.EklenenSatir != 1) throw new InvalidOperationException("Yeni kalem eklenmeliydi.");

        var hedefSatir = hedefKat.MahalListesi.Single();
        var hedefAyar = hedefSatir.HucreGetirVeyaOlustur(hedefOda.Id);
        if (!hedefAyar.Aktif || hedefAyar.Adet != 6 || hedefAyar.YerlesimTipi != "Homojen Duvar")
            throw new InvalidOperationException("Şablon hücre ayarları (adet, yerleşim tipi) korunmalıydı.");
    }

    public static void SablonlariKaydetVeYukleAtomikVeKalici()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"m12-sablon-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmpDir);
        var yol = Path.Combine(tmpDir, "mahal-sablonlari.json");
        try
        {
            var kat = new BasitProjeKat();
            var oda = new BasitProjeOda { Ad = "Depo", Poligon = [new(0, 0), new(3, 0), new(3, 3), new(0, 3)] };
            kat.Odalar.Add(oda);
            var satir = new BpcMahalSatiri { Tip = BpcElemanTipi.Anahtar, ArmatürTanimId = "anahtar-1" };
            satir.HucreGetirVeyaOlustur(oda.Id).Aktif = true;
            kat.MahalListesi.Add(satir);

            var dosya = new MahalListesiServisi.SablonDosyasi();
            dosya.Sablonlar.Add(MahalListesiServisi.SablonaDonustur(kat, "Depo Standardı"));

            if (!MahalListesiServisi.SablonlariKaydet(dosya, yol))
                throw new InvalidOperationException("Kaydetme başarısız olmamalıydı.");
            if (!File.Exists(yol))
                throw new InvalidOperationException("Dosya diskte olmalıydı.");

            var yeniden = MahalListesiServisi.SablonlariYukle(yol);
            if (yeniden.Sablonlar.Count != 1 || yeniden.Sablonlar[0].Ad != "Depo Standardı")
                throw new InvalidOperationException("Kaydedilen şablon geri okunmalıydı.");
        }
        finally
        {
            try { Directory.Delete(tmpDir, recursive: true); } catch { }
        }
    }

    public static void SablonlariYukleOlmayanDosyadaBosDonerCokmez()
    {
        var yol = Path.Combine(Path.GetTempPath(), $"olmayan-{Guid.NewGuid():N}.json");
        var dosya = MahalListesiServisi.SablonlariYukle(yol);
        if (dosya.Sablonlar.Count != 0)
            throw new InvalidOperationException("Olmayan dosyada boş şablon listesi dönmeliydi.");
    }

    // ─── 4) Yerleşim ön-tahmini ─────────────────────────────────────────────

    public static void YerlesimTahminiAdetModundaKullaniciAdediniDoner()
    {
        var geometri = new BasitProjeCizGeometriServisi();
        var oda = new BasitProjeOda { Ad = "Oda", Poligon = [new(0, 0), new(4, 0), new(4, 4), new(0, 4)] };
        var satir = new BpcMahalSatiri { Tip = BpcElemanTipi.Priz, YerlesimTipi = "Adet" };
        var ayar = satir.HucreGetirVeyaOlustur(oda.Id);
        ayar.Aktif = true; ayar.Adet = 7;

        int tahmin = MahalListesiServisi.YerlesimTahmini(satir, oda, geometri);
        if (tahmin != 7)
            throw new InvalidOperationException($"Adet modunda tahmin kullanıcı adedi olmalı. Bulunan: {tahmin}.");
    }

    public static void YerlesimTahminiGridModundaAdayUreticidenHesaplar()
    {
        var geometri = new BasitProjeCizGeometriServisi();
        // 8x8 oda, 3m grid → beklenen aday sayısı sıfırdan büyük olmalı (yerleştirmenin ürettiğiyle aynı motor).
        var oda = new BasitProjeOda { Ad = "Salon", Poligon = [new(0, 0), new(8, 0), new(8, 8), new(0, 8)] };
        var satir = new BpcMahalSatiri { Tip = BpcElemanTipi.Armatür, YerlesimTipi = "Grid", GridAraligiM = 3.0 };
        satir.HucreGetirVeyaOlustur(oda.Id).Aktif = true;

        int tahmin = MahalListesiServisi.YerlesimTahmini(satir, oda, geometri);
        if (tahmin <= 0)
            throw new InvalidOperationException("Grid modunda tahmin pozitif olmalı (aday üretici çalıştırılmalı).");
    }

    public static void YerlesimTahminiPasifVeyaHaricOdadaSifirDoner()
    {
        var geometri = new BasitProjeCizGeometriServisi();
        var odaDahil = new BasitProjeOda { Ad = "A", Poligon = [new(0, 0), new(4, 0), new(4, 4), new(0, 4)] };
        var odaHaric = new BasitProjeOda { Ad = "B", MahalListesineDahil = false, Poligon = [new(0, 0), new(4, 0), new(4, 4), new(0, 4)] };
        var satir = new BpcMahalSatiri { Tip = BpcElemanTipi.Armatür, YerlesimTipi = "Grid", GridAraligiM = 3.0 };
        // odaDahil: hücre hiç aktif edilmedi (pasif) → 0
        // odaHaric: hücre aktif ama oda mahal listesine dahil değil → 0
        satir.HucreGetirVeyaOlustur(odaHaric.Id).Aktif = true;

        if (MahalListesiServisi.YerlesimTahmini(satir, odaDahil, geometri) != 0)
            throw new InvalidOperationException("Pasif hücrede tahmin 0 olmalı.");
        if (MahalListesiServisi.YerlesimTahmini(satir, odaHaric, geometri) != 0)
            throw new InvalidOperationException("Mahal listesine dahil olmayan odada tahmin 0 olmalı.");
    }

    public static void KatTahminiGucWSembolGucunuVeAdediÇarpar()
    {
        var kutuphane = new BasitProjeCizKutuphaneServisi();
        var geometri = new BasitProjeCizGeometriServisi();
        var kat = new BasitProjeKat();
        var oda = new BasitProjeOda { Ad = "Oda", Poligon = [new(0, 0), new(4, 0), new(4, 4), new(0, 4)] };
        kat.Odalar.Add(oda);

        var tanim = kutuphane.Armatürler.First();
        var satir = new BpcMahalSatiri
        {
            Tip = BasitProjeCizKutuphaneServisi.TipBlokAdindan(tanim.BlokAdi) ?? BpcElemanTipi.Armatür,
            ArmatürTanimId = tanim.Id, YerlesimTipi = "Adet"
        };
        var ayar = satir.HucreGetirVeyaOlustur(oda.Id);
        ayar.Aktif = true; ayar.Adet = 4;
        kat.MahalListesi.Add(satir);

        double toplam = MahalListesiServisi.KatTahminiGucW(kat, kutuphane, geometri);
        double beklenen = tanim.GucW * 4;
        if (Math.Abs(toplam - beklenen) > 0.01)
            throw new InvalidOperationException($"Beklenen {beklenen} W, bulunan {toplam} W.");
    }
}
