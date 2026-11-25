using System;
using System.IO;
using System.Linq;
using System.Globalization;
using ClosedXML.Excel;
using Kst.Acoustics.App.Electric.ViewModels;
using Kst.Acoustics.Core.Electric;
using Kst.Acoustics.Core.Electric.Calculation;
using Kst.Acoustics.Core.Electric.Models;
using netDxf;

namespace Kst.Acoustics.Tests;

/// <summary>
/// M11 Tek Hat Şeması hesap motorunun izole birim testleri (codex §14 regresyon alanları):
/// kablo kesiti, faz dağılımı, linye kablo hesabı, kompanzasyon, kısa devre (Ik3),
/// jeneratör seçimi ve selektivite.
/// </summary>
public static class ElektrikHesapTests
{
    private static int Derinlik(Pano p)
    {
        int d = 0; var x = p;
        while (x.UstPano != null) { d++; x = x.UstPano; }
        return d;
    }

    /// <summary>ViewModel.TumunuHesapla zincirinin Core eşdeğeri (kısa devre dahil).</summary>
    private static void HesaplaTumu(ElektrikProje proje, ElektrikKutuphaneServisi kut)
    {
        foreach (var pano in proje.TumPanolar.OrderBy(Derinlik).Reverse())
        {
            PanoHesapServisi.PanoHesapla(pano, proje, kut);
            KabloHesapServisi.PanoKabloHesapla(pano, proje);
        }
        foreach (var bb in proje.TumBusbarlar)
            BusbarHesapServisi.Hesapla(bb, proje, kut);
        if (proje.OrtaGerilim != null && proje.KokPano != null)
            OrtaGerilimHesapServisi.Hesapla(proje.OrtaGerilim, proje.KokPano, proje, kut);
        KisaDevreHesapServisi.Hesapla(proje, kut);
        JeneratorHesapServisi.HesaplaTumu(proje, kut);
        GerilimDususKontrolServisi.Hesapla(proje);
    }

    private static Pano AnaPanoYap(ElektrikProje proje, params Linye[] linyeler)
    {
        var ana = new Pano { Ad = "Ana Pano", Kat = "Z", PanoTipi = PanoTipi.AnaPano };
        foreach (var l in linyeler) ana.Linyeler.Add(l);
        proje.TumPanolar.Add(ana);
        proje.KokPano = ana;
        return ana;
    }

    private static Pano TaliPanoYap(ElektrikProje proje, Pano ust, string ad, double kabloM, params Linye[] linyeler)
    {
        var tali = new Pano
        {
            Ad = ad, Kat = "1", PanoTipi = PanoTipi.TaliPano,
            UstPano = ust, UstPanoAd = ust.Ad, KabloMesafeM = kabloM
        };
        foreach (var l in linyeler) tali.Linyeler.Add(l);
        ust.AltPanolar.Add(tali);
        proje.TumPanolar.Add(tali);
        return tali;
    }

    private static Linye Priz(int sayi = 5, double birim = 300, FazTipi faz = FazTipi.Monofaze, double mesafe = 15)
        => new() { LinyeNo = "P1", YukTipiAd = "Priz", FazTipi = faz, SortiSayisi = sayi, BirimGucW = birim, KabloMesafeM = mesafe };

    // ── Kablo kesiti ──

    public static void KabloKesiti_DusukAkimMinKesit()
    {
        var (kesit, ad, _) = KabloHesapServisi.KesitHesapla(10, 5, FazTipi.Monofaze, 230, 3.0, 0.85);
        if (Math.Abs(kesit - 2.5) > 0.001)
            throw new InvalidOperationException($"10A/5m için minimum 2.5 mm² beklenirdi, bulunan: {ad}.");
    }

    public static void KabloKesiti_YuksekAkimTermalBuyur()
    {
        var (kesit, ad, _) = KabloHesapServisi.KesitHesapla(100, 5, FazTipi.Trifaze, 400, 3.0, 0.85);
        // 100A için B2 kapasitesi ≥100 olan ilk kesit 35 mm² (110A)
        if (kesit < 25)
            throw new InvalidOperationException($"100A için termal kriter ≥25 mm² beklenirdi, bulunan: {ad}.");
    }

    public static void KabloKesiti_UzunMesafeGerilimKriteriBuyutur()
    {
        // Kısa mesafe: termal belirler
        var kisa = KabloHesapServisi.KesitHesapla(20, 5, FazTipi.Monofaze, 230, 3.0, 0.85);
        // Uzun mesafe: gerilim düşümü kriteri daha büyük kesit zorlar
        var uzun = KabloHesapServisi.KesitHesapla(20, 120, FazTipi.Monofaze, 230, 3.0, 0.85);
        if (uzun.KesitMm2 <= kisa.KesitMm2)
            throw new InvalidOperationException(
                $"Uzun mesafe gerilim düşümü kriteri kesiti büyütmeliydi (kısa={kisa.KesitAd}, uzun={uzun.KesitAd}).");
        if (uzun.GercekDusumYuzde > 3.01)
            throw new InvalidOperationException($"Seçilen kesit %3 düşüm sınırını aşmamalı, bulunan: %{uzun.GercekDusumYuzde:N2}.");
    }

    public static void KabloKesiti_ReaktansCozumuUzunluguCifteCarpmaz()
    {
        var r = KabloHesapServisi.KesitHesapla(30, 150, FazTipi.Trifaze, 400, 3.0, 0.85);
        if (r.KesitMm2 > 16)
            throw new InvalidOperationException($"Reaktans ters çözümü uzunluğu iki kez çarpmamalı; 30A/150m için makul kesit <=16 mm² beklenirdi, bulunan: {r.KesitAd}.");
        if (r.GercekDusumYuzde > 3.01)
            throw new InvalidOperationException($"Seçilen kesit reaktans dahil %3 sınırını aşmamalı: %{r.GercekDusumYuzde:N2}.");
    }

    public static void KabloEtiketi_MonoVeTrifazeOrtakFormatta()
    {
        var proje = new ElektrikProje();
        var kut = new ElektrikKutuphaneServisi();
        var ana = AnaPanoYap(proje);
        var tali = TaliPanoYap(proje, ana, "Kat Panosu", 20,
            new Linye
            {
                LinyeNo = "P1", YukTipiAd = "Priz", FazTipi = FazTipi.Monofaze,
                SortiSayisi = 8, BirimGucW = 300, KabloMesafeM = 25
            });

        HesaplaTumu(proje, kut);

        if (!tali.KabloKesiti.StartsWith("4x", StringComparison.Ordinal) ||
            !tali.KabloKesiti.EndsWith(" mm²", StringComparison.Ordinal))
            throw new InvalidOperationException($"Trifaze pano kablosu '4xS mm²' formatında olmalı: {tali.KabloKesiti}.");
        if (!tali.Linyeler[0].KabloKesiti.StartsWith("2x", StringComparison.Ordinal) ||
            !tali.Linyeler[0].KabloKesiti.EndsWith(" mm²", StringComparison.Ordinal))
            throw new InvalidOperationException($"Monofaze linye kablosu '2xS mm²' formatında olmalı: {tali.Linyeler[0].KabloKesiti}.");
    }

    // ── Faz dağılımı ──

    public static void FazDagilimi_UcEsitYukDengeli()
    {
        var linyeler = new System.Collections.Generic.List<Linye>
        {
            new() { FazTipi = FazTipi.Monofaze, SortiSayisi = 1, BirimGucW = 1000 },
            new() { FazTipi = FazTipi.Monofaze, SortiSayisi = 1, BirimGucW = 1000 },
            new() { FazTipi = FazTipi.Monofaze, SortiSayisi = 1, BirimGucW = 1000 }
        };
        var fd = FazDagilimServisi.OtomatikFazAta(linyeler);
        if (fd.DengesizlikYuzde > 0.001)
            throw new InvalidOperationException($"Üç eşit yük R/S/T'ye dengeli dağılmalı, dengesizlik: %{fd.DengesizlikYuzde:N2}.");
        var fazlar = linyeler.Select(l => l.FazAtamasi).Distinct().Count();
        if (fazlar != 3)
            throw new InvalidOperationException("Üç yük üç ayrı faza atanmalıydı.");
    }

    public static void FazDagilimi_FazAkimlariCosFIleHesaplanir()
    {
        var linyeler = new System.Collections.Generic.List<Linye>
        {
            new() { FazTipi = FazTipi.Monofaze, SortiSayisi = 1, BirimGucW = 1000 }
        };
        var fd = FazDagilimServisi.OtomatikFazAta(linyeler, fazGerilimV: 230, cosF: 0.5);
        double beklenen = 1000.0 / (230.0 * 0.5);
        if (Math.Abs(fd.R_A - beklenen) > 0.001)
            throw new InvalidOperationException($"Faz akımı aktif güç / (V*cosφ) olmalıydı (beklenen={beklenen:N3}, bulunan={fd.R_A:N3}).");
    }

    public static void LinyeSigortasi_YukTipiMinimumlariniUygular()
    {
        var proje = new ElektrikProje();
        var ana = AnaPanoYap(
            proje,
            new Linye { YukTipiAd = "Aydınlatma", FazTipi = FazTipi.Monofaze, SortiSayisi = 1, BirimGucW = 40, SigortaAmper = 6 },
            new Linye { YukTipiAd = "Priz", FazTipi = FazTipi.Monofaze, SortiSayisi = 1, BirimGucW = 300, SigortaAmper = 6 },
            new Linye { YukTipiAd = "Mekanik", FazTipi = FazTipi.Monofaze, SortiSayisi = 1, BirimGucW = 200, SigortaAmper = 0 },
            new Linye { YukTipiAd = "Motor", FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 1000, SigortaCinsi = SigortaCinsi.C, SigortaAmper = 6 });

        HesaplaTumu(proje, new ElektrikKutuphaneServisi());

        var bulunan = string.Join(",", ana.Linyeler.Select(l => $"{l.YukTipiAd}:{l.SigortaAmper}"));
        if (ana.Linyeler[0].SigortaAmper != 10 ||
            ana.Linyeler[1].SigortaAmper != 16 ||
            ana.Linyeler[2].SigortaAmper != 6 ||
            ana.Linyeler[3].SigortaAmper != 20)
            throw new InvalidOperationException($"Yük tipi minimum sigortaları uygulanmalıydı. Bulunan: {bulunan}.");
    }

    public static void LinyeNo_AyniPanodaYukTipineGoreSiralanir()
    {
        var pano = new Pano { Ad = "Kat Panosu" };
        pano.Linyeler.Add(new Linye { YukTipiAd = "Aydınlatma" });
        pano.Linyeler.Add(new Linye { YukTipiAd = "Priz" });
        pano.Linyeler.Add(new Linye { YukTipiAd = "Aydınlatma" });
        pano.Linyeler.Add(new Linye { YukTipiAd = "Priz" });

        TekHatSemasiViewModel.LinyeleriYenidenNumaralandir(pano);

        var bulunan = string.Join(",", pano.Linyeler.Select(l => l.LinyeNo));
        if (bulunan != "A1,P1,A2,P2")
            throw new InvalidOperationException($"Linye numaraları yük tipine göre ayrı ilerlemeli. Bulunan: {bulunan}.");
    }

    public static void LinyeNo_SilmeSonrasiBoslukKapatir()
    {
        var pano = new Pano { Ad = "Kat Panosu" };
        pano.Linyeler.Add(new Linye { YukTipiAd = "Aydınlatma" });
        pano.Linyeler.Add(new Linye { YukTipiAd = "Aydınlatma" });
        pano.Linyeler.Add(new Linye { YukTipiAd = "Priz" });
        pano.Linyeler.Add(new Linye { YukTipiAd = "Priz" });
        TekHatSemasiViewModel.LinyeleriYenidenNumaralandir(pano);

        pano.Linyeler.RemoveAt(0);
        pano.Linyeler.RemoveAt(1);
        TekHatSemasiViewModel.LinyeleriYenidenNumaralandir(pano);

        var bulunan = string.Join(",", pano.Linyeler.Select(l => l.LinyeNo));
        if (bulunan != "A1,P1")
            throw new InvalidOperationException($"Silmeden sonra linye numaraları boşluk bırakmamalı. Bulunan: {bulunan}.");
    }

    public static void LinyeNo_MekanikMotorIleAyniMSerisiniKullanir()
    {
        var pano = new Pano { Ad = "Kat Panosu" };
        pano.Linyeler.Add(new Linye { YukTipiAd = "Motor (3 Faz)" });
        pano.Linyeler.Add(new Linye { YukTipiAd = "Mekanik" });
        pano.Linyeler.Add(new Linye { YukTipiAd = "Motor" });

        TekHatSemasiViewModel.LinyeleriYenidenNumaralandir(pano);

        var bulunan = string.Join(",", pano.Linyeler.Select(l => l.LinyeNo));
        if (bulunan != "M1,M2,M3")
            throw new InvalidOperationException($"Mekanik yükler motorlarla aynı M serisinde ilerlemeli. Bulunan: {bulunan}.");
    }

    public static void LinyeTekrarlaEkle_AyniLinyeyiSiradakiNoIleKopyalar()
    {
        var vm = new TekHatSemasiViewModel();
        var pano = new Pano { Ad = "Kat Panosu", Kat = "1", PanoTipi = PanoTipi.TaliPano };
        var kaynak = new Linye
        {
            LinyeNo = "P1",
            YukTipiAd = "Priz",
            FazTipi = FazTipi.Monofaze,
            SortiSayisi = 4,
            BirimGucW = 300,
            TalepFaktoru = 0.8,
            Aciklama = "Ofis prizleri",
            SigortaCinsi = SigortaCinsi.C,
            KabloMesafeM = 18
        };
        pano.Linyeler.Add(new Linye { LinyeNo = "A1", YukTipiAd = "Aydınlatma", SortiSayisi = 2, BirimGucW = 40 });
        pano.Linyeler.Add(kaynak);
        vm.Proje.TumPanolar.Add(pano);
        vm.SeciliPano = pano;

        var kopya = vm.LinyeTekrarlaEkle(kaynak);

        if (kopya == null)
            throw new InvalidOperationException("Tekrarla Ekle yeni linye üretmeliydi.");
        if (kopya.LinyeNo != "P2")
            throw new InvalidOperationException($"Kopya priz için sıradaki numara P2 olmalıydı. Bulunan: {kopya.LinyeNo}.");
        if (kopya.YukTipiAd != kaynak.YukTipiAd ||
            kopya.FazTipi != kaynak.FazTipi ||
            kopya.SortiSayisi != kaynak.SortiSayisi ||
            Math.Abs(kopya.BirimGucW - kaynak.BirimGucW) > 0.001 ||
            Math.Abs(kopya.TalepFaktoru - kaynak.TalepFaktoru) > 0.001 ||
            kopya.Aciklama != kaynak.Aciklama ||
            kopya.SigortaCinsi != kaynak.SigortaCinsi ||
            Math.Abs(kopya.KabloMesafeM - kaynak.KabloMesafeM) > 0.001)
            throw new InvalidOperationException("Tekrarla Ekle kaynak linyenin kullanıcı girdilerini korumalı.");
    }

    public static void LinyeKopyalaYapistir_CokluLinyeyiBaskaPanoyaTasir()
    {
        var vm = new TekHatSemasiViewModel();
        var kaynakPano = new Pano { Ad = "K", Kat = "1", PanoTipi = PanoTipi.TaliPano };
        var hedefPano = new Pano { Ad = "H", Kat = "2", PanoTipi = PanoTipi.TaliPano };
        var a1 = new Linye { LinyeNo = "A1", YukTipiAd = "Aydınlatma", SortiSayisi = 3, BirimGucW = 40, Aciklama = "Koridor" };
        var p1 = new Linye { LinyeNo = "P1", YukTipiAd = "Priz", SortiSayisi = 4, BirimGucW = 300, KontaktorluMu = true };
        kaynakPano.Linyeler.Add(a1);
        kaynakPano.Linyeler.Add(p1);
        hedefPano.Linyeler.Add(new Linye { LinyeNo = "P1", YukTipiAd = "Priz", BirimGucW = 300 });
        vm.Proje.TumPanolar.Add(kaynakPano);
        vm.Proje.TumPanolar.Add(hedefPano);

        vm.SeciliPano = kaynakPano;
        if (vm.LinyeleriKopyala(new[] { a1, p1 }) != 2)
            throw new InvalidOperationException("İki linye panoya kopyalanmalıydı.");
        a1.Aciklama = "Sonradan değişti"; // kopyalanan içerik kopyalama anındaki hali kalmalı

        vm.SeciliPano = hedefPano;
        var eklenen = vm.LinyeleriYapistir();

        if (eklenen.Count != 2 || hedefPano.Linyeler.Count != 3)
            throw new InvalidOperationException($"Hedef panoya 2 linye eklenmeliydi. Eklenen: {eklenen.Count}.");
        var numaralar = string.Join(",", hedefPano.Linyeler.Select(l => l.LinyeNo).OrderBy(n => n));
        if (numaralar != "A1,P1,P2")
            throw new InvalidOperationException($"Yapıştırılan linyeler hedef panoda sıradaki numaraları almalı. Bulunan: {numaralar}.");
        var aKopya = eklenen.Single(l => l.YukTipiAd == "Aydınlatma");
        var pKopya = eklenen.Single(l => l.YukTipiAd == "Priz");
        if (aKopya.Aciklama != "Koridor" || aKopya.SortiSayisi != 3 || !pKopya.KontaktorluMu)
            throw new InvalidOperationException("Yapıştırılan linyeler kopyalama anındaki girdileri korumalı.");
        if (eklenen.Any(l => l.Id == a1.Id || l.Id == p1.Id))
            throw new InvalidOperationException("Yapıştırılan linyeler yeni kimlik almalı.");
        if (kaynakPano.Linyeler.Count != 2)
            throw new InvalidOperationException("Kopyalama kaynak panoyu değiştirmemeli.");

        // Aynı pano içeriği ikinci kez yapıştırılabilir.
        if (vm.LinyeleriYapistir().Count != 2 || hedefPano.Linyeler.Count != 5)
            throw new InvalidOperationException("Pano içeriği tekrar yapıştırılabilmeli.");
    }

    public static void LinyeExcelSablonu_RoundTripOkunur()
    {
        var path = Path.Combine(Path.GetTempPath(), $"m11_linye_sablon_{Guid.NewGuid():N}.xlsx");
        try
        {
            TekHatLinyeExcelServisi.SablonOlustur(path);
            var (linyeler, hatalar) = TekHatLinyeExcelServisi.IceriAktar(path);

            if (hatalar.Count != 0)
                throw new InvalidOperationException($"Şablon uyarısız okunmalıydı: {string.Join(" | ", hatalar)}");
            if (linyeler.Count != 3)
                throw new InvalidOperationException($"Şablonda 3 örnek linye okunmalıydı. Bulunan: {linyeler.Count}.");
            if (linyeler[0].YukTipiAd != "Aydınlatma" || linyeler[0].FazTipi != FazTipi.Monofaze)
                throw new InvalidOperationException("Şablon aydınlatma örnek satırı doğru okunamadı.");
            if (linyeler[2].YukTipiAd != "Motor" || linyeler[2].FazTipi != FazTipi.Trifaze || linyeler[2].SigortaCinsi != SigortaCinsi.C)
                throw new InvalidOperationException("Şablon motor örnek satırı doğru okunamadı.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public static void LinyeExcelAktarimi_SeciliPanoyuYenilerVeAltPanoCikislariniKorur()
    {
        var path = Path.Combine(Path.GetTempPath(), $"m11_linye_import_{Guid.NewGuid():N}.xlsx");
        try
        {
            TekHatLinyeExcelServisi.SablonOlustur(path);
            var (linyeler, hatalar) = TekHatLinyeExcelServisi.IceriAktar(path);
            if (hatalar.Count != 0)
                throw new InvalidOperationException($"Excel aktarımında uyarı beklenmiyordu: {string.Join(" | ", hatalar)}");

            var vm = new TekHatSemasiViewModel();
            var pano = new Pano { Ad = "Kat Panosu", Kat = "1", PanoTipi = PanoTipi.TaliPano };
            pano.Linyeler.Add(new Linye { LinyeNo = "A1", YukTipiAd = "Aydınlatma", SortiSayisi = 1, BirimGucW = 40 });
            pano.Linyeler.Add(new Linye { LinyeNo = "P1", YukTipiAd = "Priz", SortiSayisi = 1, BirimGucW = 300 });
            var altPano = new Pano
            {
                Ad = "Alt Pano",
                Kat = "1",
                PanoTipi = PanoTipi.TaliPano,
                UstPano = pano,
                UstPanoAd = pano.Ad,
                UstPanoCikisSigortaA = 63,
                KabloMesafeM = 12
            };
            pano.AltPanolar.Add(altPano);
            vm.Proje.TumPanolar.Add(pano);
            vm.Proje.TumPanolar.Add(altPano);
            vm.SeciliPano = pano;

            var eklenen = vm.LinyeleriIceAktar(linyeler);
            var etiketler = string.Join(",", pano.Linyeler.Select(l => l.LinyeNo));

            if (eklenen != 3)
                throw new InvalidOperationException($"3 linye eklenmeliydi. Bulunan: {eklenen}.");
            if (pano.Linyeler.Count != 5 || etiketler != "A1,P1,A2,P2,M1")
                throw new InvalidOperationException($"Excel aktarımı mevcut linyeleri koruyup yenilerini eklemeli ve yeniden sıralamalıydı. Bulunan: {etiketler}.");
            if (pano.AltPanolar.Count != 1 || pano.AltPanolar[0] != altPano || altPano.UstPano != pano)
                throw new InvalidOperationException("Excel aktarımı alt pano bağlantılarına dokunmamalı.");
            if (altPano.UstPanoCikisSigortaA <= 0)
                throw new InvalidOperationException("Excel aktarımı alt pano çıkış sigortası bilgisini silmemeli.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public static void ExcelRaporu_PanolarAyriSayfaVeCikislarDogru()
    {
        var proje = new ElektrikProje { ProjeAdi = "Pano Excel Testi" };
        var ana = AnaPanoYap(proje, new Linye
        {
            LinyeNo = "A1", YukTipiAd = "Aydınlatma", FazTipi = FazTipi.Monofaze,
            SortiSayisi = 8, BirimGucW = 40, KabloMesafeM = 12, Aciklama = "Lobi"
        });
        var tali = TaliPanoYap(proje, ana, "Kat/1:*?[Pano] Çok Uzun Sayfa Adı", 18, new Linye
        {
            LinyeNo = "P1", YukTipiAd = "Priz", FazTipi = FazTipi.Monofaze,
            SortiSayisi = 6, BirimGucW = 300, KabloMesafeM = 24, Aciklama = "Ofis prizleri"
        });
        tali.BaglantiTipi = BaglantiTipi.Busbar;
        tali.CikisKutusuSigortaA = 40;
        tali.CikisKesmeKapasitesi = "10kA";
        var busbar = new BusbarHatti { Ad = "BB-1", UstPanoAd = ana.Ad, ToplamUzunlukM = 20 };
        busbar.CikisKutulari.Add(new BusbarCikisKutusu
        {
            Ad = "BB-1-K1", BagliPanoAd = tali.Ad, SigortaA = 40, KabloMesafeM = 18
        });
        tali.BaglandigiBusbarId = busbar.Id;
        proje.TumBusbarlar.Add(busbar);

        var kontrol = new Pano { Ad = "Kontrol/Panosu", Kat = "Z", PanoTipi = PanoTipi.KontrolPanosu };
        kontrol.KontrolButonlari.Add(new KontrolButonuAtama
        {
            KaynakPanoAd = tali.Ad, LinyeNo = "P1", LinyeAciklama = "Ofis priz kontrolü", SigortaAmper = 16
        });
        kontrol.KontrolSinyalKablolari.Add(new KontrolSinyalKablosu
        {
            KaynakPanoAd = tali.Ad, DamarSayisi = 1, KesitMm2 = 1.5, MetrajM = 35, KabloTipi = "LIYY"
        });
        proje.TumPanolar.Add(kontrol);

        HesaplaTumu(proje, new ElektrikKutuphaneServisi());
        var path = Path.Combine(Path.GetTempPath(), $"m11_pano_raporu_{Guid.NewGuid():N}.xlsx");
        try
        {
            TekHatSemasiExcelServisi.RaporOlustur(proje, path);
            using var workbook = new ClosedXML.Excel.XLWorkbook(path);

            if (workbook.Worksheets.Count != proje.TumPanolar.Count + 1)
                throw new InvalidOperationException("M13 özeti ve her pano için ayrı Excel sayfası oluşturulmalı.");
            if (workbook.Worksheet(1).Name != "M13 Metraj Özeti")
                throw new InvalidOperationException("Excel raporunun ilk sayfası M13 Metraj Özeti olmalı.");
            if (workbook.Worksheets.Any(s => s.Name.Length > 31 || s.Name.IndexOfAny(['[', ']', ':', '*', '?', '/', '\\']) >= 0))
                throw new InvalidOperationException("Excel sayfa adları 31 karakter sınırına ve yasak karakter kuralına uymalı.");

            var anaSayfa = workbook.Worksheet("Ana Pano");
            var beklenenBasliklar = new[]
            {
                "Pano Adı", "Beslendiği Üst Pano", "Besleme Kablosu / Kesiti", "Besleme Mesafesi (m)",
                "Giriş Sigortası (A)", "Giriş Kaçak Akım Rölesi", "Linye No", "Alt Pano / Yük Tipi",
                "Çıkış Sigortası (A)", "Sigorta Tipi", "Kesme Kapasitesi", "Beslediği Kablo Kesiti",
                "Mesafe (m)", "Açıklama"
            };
            for (var col = 1; col <= beklenenBasliklar.Length; col++)
                if (anaSayfa.Cell(3, col).GetString() != beklenenBasliklar[col - 1])
                    throw new InvalidOperationException($"Excel başlığı hatalı: sütun {col}.");

            var aydinlatmaSatiri = anaSayfa.RowsUsed().First(r => r.Cell(7).GetString() == "A1");
            if (aydinlatmaSatiri.Cell(8).GetString() != "Aydınlatma" ||
                aydinlatmaSatiri.Cell(10).GetString() != "Sigorta B" ||
                Math.Abs(aydinlatmaSatiri.Cell(13).GetDouble() - 12) > 0.001)
                throw new InvalidOperationException("Linye çıkış bilgileri Excel'e doğru yazılmalı.");

            var altPanoSatiri = anaSayfa.RowsUsed().First(r => r.Cell(8).GetString() == tali.Ad);
            if (altPanoSatiri.Cell(9).GetDouble() != tali.CikisKorumaAkimA ||
                altPanoSatiri.Cell(11).GetString() != tali.CikisKesmeKapasitesi ||
                !altPanoSatiri.Cell(14).GetString().Contains("BB-1"))
                throw new InvalidOperationException("Alt pano çıkış sigortası ve busbar bilgisi Excel'e doğru yazılmalı.");

            var taliSayfa = workbook.Worksheets.Single(s => s.Cell(4, 1).GetString() == tali.Ad);
            if (taliSayfa.Cell(4, 2).GetString() != ana.Ad ||
                !taliSayfa.Cell(4, 3).GetString().Contains("BB-1") ||
                Math.Abs(taliSayfa.Cell(4, 4).GetDouble() - 18) > 0.001 ||
                taliSayfa.Cell(4, 6).GetString().Length == 0)
                throw new InvalidOperationException("Pano giriş beslemesi, mesafesi ve KAR bilgisi Excel'e yazılmalı.");

            var kontrolSayfa = workbook.Worksheets.Single(s => s.Cell(4, 1).GetString() == kontrol.Ad);
            if (kontrolSayfa.Cell(4, 7).GetString() != "P1" ||
                kontrolSayfa.Cell(4, 10).GetString() != "Kontrol sinyali" ||
                !kontrolSayfa.Cell(4, 12).GetString().Contains("LIYY") ||
                !kontrolSayfa.Cell(4, 12).GetString().Contains("1x") ||
                Math.Abs(kontrolSayfa.Cell(4, 13).GetDouble() - 35) > 0.001)
                throw new InvalidOperationException("Kontrol panosu buton ve sinyal kablosu bilgileri Excel'e yazılmalı.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public static void ExcelRaporu_M13ToplamMetrajTumGruplariYazar()
    {
        var proje = new ElektrikProje { ProjeAdi = "M13 Metraj Testi", VarsayilanKabloTipi = "NHXMH" };
        var ana = new Pano
        {
            Ad = "Ana Pano", Kat = "Z", PanoTipi = PanoTipi.AnaPano,
            GirisSigortaA = 250, GirisSalteriA = 315, KisaDevreAkimiKA = 22,
            KacakAkimRolesi = new KacakAkimRolesiTanim { Ad = "Troid KAR 300mA", AnmaAkimA = 400, SensitiviteMa = 300, KutupSayisi = 4, TroidTipiMi = true },
            KacakAkimAkimTrafosu = new AkimTrafosuTanim { Ad = "AT 400/5", PrimerA = 400, SekonderA = 5, IcCapMm = 100 },
            SayacEkle = true, SayacTipi = "Trifaze sayaç", KlemensSayisi = 12,
            GovdeTipi = PanoGovdeTipi.DikiliTip, PanoDerinlikCm = 90,
            GerekliPanoAlaniM2 = 1.25, PanoGenislikM = 1, PanoYukseklikM = 1
        };
        ana.Linyeler.Add(new Linye
        {
            LinyeNo = "A1", YukTipiAd = "Aydınlatma", FazTipi = FazTipi.Monofaze,
            SigortaAmper = 16, SigortaCinsi = SigortaCinsi.B, KesmeKapasitesi = "6kA",
            KabloKesiti = "2x2.5 mm²", KabloMesafeM = 12,
            Kontaktor = new KontaktorTanim { Ad = "K 25A", AnmaAkimA = 25, KutupSayisi = 4, BobinGerilimV = 230 },
            DarbeKesici = new DarbeKesiciTanim { Ad = "DK 16A", AnmaAkimA = 16, KutupSayisi = 1 }
        });

        var tali = new Pano
        {
            Ad = "Kat Panosu", Kat = "1", PanoTipi = PanoTipi.TaliPano,
            UstPano = ana, UstPanoAd = ana.Ad, KabloTipi = "NHXMH",
            KabloKesiti = "2x(4x25) mm²", KabloParalelKolSayisi = 2, KabloMesafeM = 20,
            GirisSigortaA = 80, UstPanoCikisSigortaA = 100, CikisKesmeKapasitesi = "36kA",
            KisaDevreAkimiKA = 12, GovdeTipi = PanoGovdeTipi.DuvarTipi,
            AstronomikZamanRolesi = new AstronomikZamanRolesiTanim { Ad = "AZR 16A", AnmaAkimA = 16 },
            GerekliPanoAlaniM2 = 0.35, PanoGenislikM = 0.5, PanoYukseklikM = 0.6
        };
        ana.AltPanolar.Add(tali);

        var busbarPano = new Pano
        {
            Ad = "Busbar Panosu", Kat = "2", PanoTipi = PanoTipi.TaliPano,
            UstPano = ana, UstPanoAd = ana.Ad, BaglantiTipi = BaglantiTipi.Busbar,
            KabloTipi = "NHXMH", KabloKesiti = "4x16 mm²", KabloMesafeM = 8,
            GirisSigortaA = 63, CikisKutusuSigortaA = 63, CikisKesmeKapasitesi = "10kA",
            GerekliPanoAlaniM2 = 0.2, PanoGenislikM = 0.4, PanoYukseklikM = 0.5
        };
        ana.AltPanolar.Add(busbarPano);

        var kompPano = new Pano
        {
            Ad = "Kompanzasyon", Kat = "Z", PanoTipi = PanoTipi.Kompanzasyon,
            UstPano = ana, UstPanoAd = ana.Ad, GirisSigortaA = 125, UstPanoCikisSigortaA = 160,
            CikisKesmeKapasitesi = "36kA", KisaDevreAkimiKA = 18,
            KabloTipi = "NHXMH", KabloKesiti = "4x50 mm²", KabloMesafeM = 6,
            GerekliPanoAlaniM2 = 0.45, PanoGenislikM = 0.6, PanoYukseklikM = 0.7,
            HarmonikFiltreliMi = true,
            SeciliHarmonikFiltre = new HarmonikFiltreTanim { Ad = "HF 50", Tipi = "Pasif", GucKVar = 50, AnmaAkimA = 80 },
            GirisBicakliSigorta = new BicakliSigortaTanim
            {
                Ad = "NH1 160A", Boy = "NH1", AnmaAkimA = 160, Karakteristik = "gG", KesmeKapasitesiKA = 120
            },
            SeciliKompanzasyonRolesi = new KompanzasyonRolesiTanim { Ad = "Röle 12 Kademe", KademeSayisi = 12 }
        };
        ana.AltPanolar.Add(kompPano);

        var senkron = new Pano
        {
            Ad = "Senkron Pano", Kat = "Z", PanoTipi = PanoTipi.TransferPanosu,
            GovdeTipi = PanoGovdeTipi.DikiliTip, PanoDerinlikCm = 90,
            GerekliPanoAlaniM2 = 1.1, PanoGenislikM = 1, PanoYukseklikM = 1
        };

        proje.KokPano = ana;
        proje.TumPanolar.AddRange([ana, tali, busbarPano, kompPano, senkron]);

        var busbar = new BusbarHatti
        {
            Ad = "BB-400", BusbarTipiAd = "KXA-400A", UstPanoAd = ana.Ad, ToplamUzunlukM = 25
        };
        busbar.CikisKutulari.Add(new BusbarCikisKutusu
        {
            Ad = "Kutu-63", BagliPanoAd = busbarPano.Ad, SigortaA = 63, KabloMesafeM = 8
        });
        busbarPano.BaglandigiBusbarId = busbar.Id;
        proje.TumBusbarlar.Add(busbar);

        proje.OrtaGerilim = new OrtaGerilimSistemi
        {
            AnaPanoAd = ana.Ad,
            TrafoTanimAd = "Kuru Trafo 1000 kVA", TrafoManuel = true,
            KoskTipi = KoskTipi.DisMekanBetonKosk,
            KoskGenislikMm = 6000, KoskDerinlikMm = 3000, KoskYukseklikMm = 2800,
            SebekeHucreXlpeKablosu = new OgXlpeKablo
            {
                IletkenMalzemesi = OgKabloIletkenMalzemesi.Aluminyum, KesitMm2 = 50, UzunlukM = 10
            },
            HucreTrafoXlpeKablosu = new OgXlpeKablo
            {
                IletkenMalzemesi = OgKabloIletkenMalzemesi.Bakir, KesitMm2 = 70, UzunlukM = 12
            },
            CikisBaglanti = new TrafoCikisBaglanti
            {
                Tip = TrafoCikisBaglantiTipi.Kablo, ReferansAd = "NHXMH",
                KesitMm2 = "2x(4x240) mm²", UzunlukM = 15,
                ParalelKolSayisi = 2, HesaplananParalelKolSayisi = 2
            }
        };
        proje.OrtaGerilim.Hucreler.Add(new OgHucre
        {
            Ad = "Giriş Hücresi", HucreTanimAd = "Giriş YA 36kV 630A", HucreTipi = OgHucreTipi.Giris
        });

        proje.Upsler.Add(new Ups
        {
            UpsTanimAd = "UPS 30 kVA", SeciliUpsGucKVA = 30,
            AkuTanimAd = "12V 100Ah VRLA", ToplamBlokSayisi = 64
        });

        proje.Kompanzasyon = new KompanzasyonSonucu
        {
            ReaktorAktif = true,
            SecilenYukSurucu = new SvcSurucuTanim
            {
                Ad = "SVC 25 kVAr", KademeGucKVar = 25, AnmaAkimA = 60
            },
            KondansatorKademeleri =
            [
                new() { KademeNo = 1, KVar = 10, KondansatorAd = "10 kVAr", FazTipi = "Trifaze" },
                new() { KademeNo = 2, KVar = 10, KondansatorAd = "10 kVAr", FazTipi = "Trifaze" },
                new() { KademeNo = 3, KVar = 20, KondansatorAd = "20 kVAr", FazTipi = "Trifaze" }
            ],
            ReaktorKademeleri =
            [
                new() { KademeNo = 1, KVar = 5, ReaktorAd = "Reaktör 5 kVAr", AkortFrekansHz = 189 },
                new() { KademeNo = 2, KVar = 5, ReaktorAd = "Reaktör 5 kVAr", AkortFrekansHz = 189 }
            ]
        };

        proje.JeneratorGruplari.Add(new JeneratorGrubu
        {
            AnaPanoAd = ana.Ad,
            SenkronPanoAd = senkron.Ad,
            // Eski/ara sürüm dosyalarında grup alanı boş, seçim ünite üzerinde kalabiliyor.
            TransferSalterAd = "",
            SenkronHedefPanoKabloMesafeM = 10,
            SenkronHedefKabloKesiti = "5x(4x240) mm²",
            SenkronHedefKabloParalelKolSayisi = 5,
            Jeneratorler =
            [
                new() { JeneratorTanimAd = "JEN 880 kVA", DisMekandaMi = true, TransferSalterAd = "ATS 3200A", SenkronPanoKabloMesafeM = 5, SenkronKabloKesiti = "3x(4x240) mm²", SenkronKabloParalelKolSayisi = 3 },
                new() { JeneratorTanimAd = "JEN 880 kVA", DisMekandaMi = false, TransferSalterAd = "ATS 3200A", SenkronPanoKabloMesafeM = 5, SenkronKabloKesiti = "3x(4x240) mm²", SenkronKabloParalelKolSayisi = 3 }
            ]
        });

        string? qaPath = Environment.GetEnvironmentVariable("EYE4PRO_M13_QA_PATH");
        bool qaDosyasiniKoru = !string.IsNullOrWhiteSpace(qaPath);
        var path = qaDosyasiniKoru
            ? Path.GetFullPath(qaPath!)
            : Path.Combine(Path.GetTempPath(), $"m11_m13_metraj_{Guid.NewGuid():N}.xlsx");
        if (qaDosyasiniKoru)
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            TekHatSemasiExcelServisi.RaporOlustur(proje, path);
            using var workbook = new ClosedXML.Excel.XLWorkbook(path);
            var ws = workbook.Worksheet(1);
            if (ws.Name != "M13 Metraj Özeti" || ws.Cell(7, 1).GetString() != "Ana Grup")
                throw new InvalidOperationException("M13 metraj özeti ilk sayfada ve beklenen tablo düzeninde olmalı.");

            var satirlar = ws.RowsUsed().Where(r => r.RowNumber() >= 8).ToList();
            double Miktar(string anaGrup, string altGrup, string? teknikParca = null) => satirlar
                .Where(r => r.Cell(1).GetString() == anaGrup && r.Cell(2).GetString() == altGrup &&
                            (teknikParca == null || r.Cell(3).GetString().Contains(teknikParca, StringComparison.OrdinalIgnoreCase)))
                .Sum(r => r.Cell(5).GetDouble());

            // Kablolar tek "Kablo" bölümünde, kablo tipi başına tek satırda birleşir
            // (linye + pano besleme + trafo + jeneratör birlikte). Ayrı "Kablo - ..." bölümü kalmamalı.
            if (satirlar.Any(r => r.Cell(1).GetString().StartsWith("Kablo -", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Kablolar tek 'Kablo' bölümünde birleşmeli; ayrı 'Kablo - Pano/Tesis' / 'Kablo - Linye' bölümü kalmamalı.");

            // Üç güzergâhın aynı tipi (NHXMH 4x240): trafo 30 + jeneratör 30 + senkron 50 = 110 m → TEK satır.
            int adet240 = satirlar.Count(r => r.Cell(1).GetString() == "Kablo" &&
                                              r.Cell(2).GetString() == "Güç kablosu" &&
                                              r.Cell(3).GetString().Contains("240"));
            if (adet240 != 1)
                throw new InvalidOperationException($"Aynı tip (NHXMH 4x240) güç kablosu tek satırda toplanmalı (bulunan satır: {adet240}).");
            if (Miktar("Kablo", "Güç kablosu", "240") < 110 - 0.001)
                throw new InvalidOperationException($"Trafo+jeneratör+senkron 240 kabloları tek satırda ≥110 m olmalı (bulunan: {Miktar("Kablo", "Güç kablosu", "240"):N1}).");

            // Linye kablosu (12 m) artık aynı bölümde 'Güç kablosu' altında; toplam linye dahil olmalı.
            if (Miktar("Kablo", "Güç kablosu") < 122 - 0.001)
                throw new InvalidOperationException("Linye + pano/tesis güç kabloları tek bölümde toplanmalı (linye 12 m dahil ≥122 m).");
            if (Math.Abs(ws.Cell(5, 5).GetDouble() - (ws.Cell(5, 1).GetDouble() + ws.Cell(5, 3).GetDouble())) > 0.001)
                throw new InvalidOperationException("Tüm Kablolar kartı = Güç + (Sinyal/OG) toplamı olmalı.");

            // Koruma cihazları: aynı cihaz (tip+amper+kesme) güzergâhtan bağımsız TEK satırda toplanır.
            var korumaRows = satirlar.Where(r => r.Cell(1).GetString() == "Koruma Cihazı").ToList();
            var teknikler = korumaRows.Select(r => r.Cell(3).GetString()).ToList();
            if (teknikler.Count != teknikler.Distinct(StringComparer.OrdinalIgnoreCase).Count())
                throw new InvalidOperationException("Aynı koruma cihazı (TMŞ/sigorta) tek satırda toplanmalı; tekrar eden teknik özellik olmamalı.");

            // İki adet 25A kondansatör kademe sigortası (10+10 kVAr) aynı tip → tek satırda 2 adet.
            var row25 = korumaRows.SingleOrDefault(r =>
                r.Cell(3).GetString().Contains(" 25A") && r.Cell(2).GetString().Contains("sigorta", StringComparison.OrdinalIgnoreCase));
            if (row25 == null || Math.Abs(row25.Cell(5).GetDouble() - 2) > 0.001)
                throw new InvalidOperationException($"Aynı tip 25A kademe sigortası tek satırda 2 adet toplanmalı (bulunan: {row25?.Cell(5).GetDouble().ToString() ?? "satır yok"}).");

            // Linye 16A sigortası ve en az bir TMŞ cihazı metrajda yer almalı.
            if (!korumaRows.Any(r => r.Cell(3).GetString().Contains(" 16A")) ||
                !korumaRows.Any(r => r.Cell(3).GetString().StartsWith("TMŞ")))
                throw new InvalidOperationException("Linye 16A sigortası ve TMŞ cihazları metrajda yer almalı.");
            if (Math.Abs(Miktar("Busbar", "Busbar hattı", "KXA-400A") - 25) > 0.001 ||
                Miktar("Busbar", "Çıkış kutusu", "63A") != 1)
                throw new InvalidOperationException("Busbar metre ve amperaja göre çıkış kutusu adedi yazılmalı.");
            // Pano metrajı: duvar tipi standart ön-yüz alanına, dikili tip derinliğe göre tek
            // satırda toplanır (adet). Dikili tipte m² yazılmaz; ayrı "toplam m²" satırı yoktur.
            if (Miktar("Pano", "Duvar Tipi", "m² standart") < 1 ||
                Miktar("Pano", "Dikili Tip", "cm derinlik") < 1)
                throw new InvalidOperationException("Duvar (standart alan) ve dikili (derinlik) pano adetleri ayrı yazılmalı.");
            if (Miktar("Kompanzasyon", "Kondansatör", "10 kVAr") != 2 ||
                Miktar("Kompanzasyon", "Reaktör", "5 kVAr") != 2 ||
                Miktar("Kompanzasyon", "Yük sürücü", "SVC 25 kVAr") != 1 ||
                Miktar("Kompanzasyon", "Kompanzasyon rölesi", "12 kademe") != 1)
                throw new InvalidOperationException("Kompanzasyon kondansatör, reaktör, yük sürücü ve röleleri güç/kademe bazında ayrıştırılmalı.");
            if (Miktar("Jeneratör Sistemi", "Jeneratör", "880 kVA") != 2 ||
                Miktar("Jeneratör Sistemi", "Senkron / Transfer panosu") != 1 ||
                Miktar("Jeneratör Sistemi", "Transfer şalteri", "ATS 3200A") != 1)
                throw new InvalidOperationException("Jeneratör, senkron pano ve transfer şalteri ayrı toplamlanmalı.");
            if (Miktar("Jeneratör Aksesuarı", "Jeneratör kabini", "JEN 880 kVA") != 1)
                throw new InvalidOperationException("Yalnız dış mekândaki jeneratör için kabin metrajı yazılmalı.");

            if (Miktar("Koruma ve Kumanda", "Troid kaçak akım rölesi") != 1 ||
                Miktar("Koruma ve Kumanda", "Troid akım trafosu") != 1 ||
                Miktar("Koruma ve Kumanda", "Kontaktör") != 1 ||
                Miktar("Koruma ve Kumanda", "Darbe akım anahtarı") != 1 ||
                Miktar("Koruma ve Kumanda", "Astronomik zaman rölesi") != 1)
                throw new InvalidOperationException("KAR/troid, kontaktör ve darbe akım anahtarı M14 metrajına yazılmalı.");
            if (Miktar("Kompanzasyon", "Harmonik filtre") != 1 ||
                Miktar("UPS Sistemi", "UPS") != 1 || Miktar("UPS Sistemi", "Akü bloğu") != 64 ||
                Miktar("UPS Sistemi", "Bakım bypass şalteri") != 1)
                throw new InvalidOperationException("Harmonik filtre ve UPS/akü/bypass ekipmanları metrajda bulunmalı.");
            if (Miktar("Orta Gerilim", "Trafo") != 1 || Miktar("Orta Gerilim", "OG hücresi") != 1 ||
                Miktar("Orta Gerilim", "Köşk") != 1 || Miktar("Kablo Aksesuarı", "OG kablo başlığı") != 4)
                throw new InvalidOperationException("Trafo, OG hücresi, köşk ve OG başlıkları metrajda bulunmalı.");
            if (Miktar("Pano Aksesuarı", "Klemens") < 12 || Miktar("Pano Aksesuarı", "Topraklama barası") != 5 ||
                Miktar("Busbar Aksesuarı", "Askı/destek") < 1 || Miktar("Kablo Aksesuarı", "AG kablo bağlantı seti") < 1)
                throw new InvalidOperationException("Pano, busbar ve kablo aksesuarları türetilerek metraja yazılmalı.");

            double genelPanoAdedi = satirlar.Where(r => r.Cell(1).GetString() == "Pano").Sum(r => r.Cell(5).GetDouble());
            if (Math.Abs(genelPanoAdedi - 4) > 0.001)
                throw new InvalidOperationException("Senkron/transfer panosu genel pano metrajında ikinci kez sayılmamalı.");
        }
        finally
        {
            if (!qaDosyasiniKoru && File.Exists(path)) File.Delete(path);
        }
    }

    public static void YukTipiKutuphane_EskiDosyayaMekanikVarsayilanEklenir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"m11_yuk_tipleri_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(
                Path.Combine(dir, "YukTipiKutuphanesi.json"),
                """[{ "Ad": "Aydınlatma", "BirimGucW": 40, "SigortaCinsi": "B", "TalepFaktoru": 1.0 }]""");

            var kut = new ElektrikKutuphaneServisi(dir);
            var mekanik = kut.YukTipleri.FirstOrDefault(y => y.Ad == "Mekanik");
            if (mekanik == null)
                throw new InvalidOperationException("Eski yük tipi kütüphanesine Mekanik varsayılanı eklenmeliydi.");
            if (Math.Abs(mekanik.BirimGucW - 200) > 0.001)
                throw new InvalidOperationException($"Mekanik varsayılan gücü 200 W olmalıydı, bulunan: {mekanik.BirimGucW} W.");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    public static void YukTipiKutuphane_VarsayilanFazEskiDosyadaTamamlanirVeKorunur()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"m11_yuk_faz_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(
                Path.Combine(dir, "YukTipiKutuphanesi.json"),
                """
                [{ "Ad": "Aydınlatma", "BirimGucW": 40, "SigortaCinsi": "B", "TalepFaktoru": 1.0 },
                 { "Ad": "Motor (3 Faz)", "BirimGucW": 0, "SigortaCinsi": "C", "TalepFaktoru": 1.0 },
                 { "Ad": "Klima", "BirimGucW": 2500, "SigortaCinsi": "C", "TalepFaktoru": 1.0 }]
                """);

            var kut = new ElektrikKutuphaneServisi(dir);
            FazTipi Faz(ElektrikKutuphaneServisi k, string ad) => k.YukTipleri.First(y => y.Ad == ad).VarsayilanFaz;
            if (Faz(kut, "Aydınlatma") != FazTipi.Monofaze || Faz(kut, "Motor (3 Faz)") != FazTipi.Trifaze)
                throw new InvalidOperationException("Faz alanı olmayan eski kayıtlar addan tamamlanmalı (Motor 3 Faz → trifaze).");

            // Kullanıcı varsayılanı değiştirir → kaydet/aç sonrası korunur.
            kut.YukTipleri.First(y => y.Ad == "Klima").FazSecimIndex = 1;
            kut.Kaydet();
            var tekrar = new ElektrikKutuphaneServisi(dir);
            if (Faz(tekrar, "Klima") != FazTipi.Trifaze || Faz(tekrar, "Aydınlatma") != FazTipi.Monofaze)
                throw new InvalidOperationException("Değiştirilen varsayılan faz kütüphane kaydında korunmalı.");

            // Excel: boş Faz hücresi kütüphane varsayılanını alır, dolu hücre geçerli kalır.
            var xlsx = Path.Combine(dir, "linyeler.xlsx");
            using (var wb = new XLWorkbook())
            {
                var ws = wb.AddWorksheet("Linyeler");
                ws.Cell(1, 1).Value = "Yük Tipi";
                ws.Cell(1, 2).Value = "Faz";
                ws.Cell(2, 1).Value = "Klima";
                ws.Cell(3, 1).Value = "Klima";
                ws.Cell(3, 2).Value = "Monofaze";
                wb.SaveAs(xlsx);
            }
            var (linyeler, _) = TekHatLinyeExcelServisi.IceriAktar(xlsx, tekrar.YukTipleri);
            if (linyeler.Count != 2 || linyeler[0].FazTipi != FazTipi.Trifaze || linyeler[1].FazTipi != FazTipi.Monofaze)
                throw new InvalidOperationException("Excel'de boş Faz kütüphane varsayılanını almalı, dolu Faz korunmalı.");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    public static void KacakAkim_YuzAmperAltiKlasikKarSecer()
    {
        var kut = new ElektrikKutuphaneServisi();
        var proje = new ElektrikProje();
        var ana = AnaPanoYap(proje);
        var tali = TaliPanoYap(proje, ana, "Tali", 10,
            new Linye { LinyeNo = "P1", YukTipiAd = "Priz", FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 10_000 });

        HesaplaTumu(proje, kut);

        if (tali.GirisAkimA >= 100)
            throw new InvalidOperationException($"Test kurgusu: tali pano akımı 100A altında olmalıydı. Bulunan={tali.GirisAkimA:N1}A.");
        if (tali.KacakAkimRolesi == null || tali.KacakAkimRolesi.TroidTipiMi)
            throw new InvalidOperationException("100A altındaki tali panoda klasik 4P KAR seçilmeli.");
        if (tali.KacakAkimAkimTrafosu != null)
            throw new InvalidOperationException("Klasik KAR seçildiğinde akım trafosu atanmamalı.");
    }

    public static void KacakAkim_YuzAmperUstuTroidVeAkimTrafosuSecer()
    {
        var kut = new ElektrikKutuphaneServisi();
        var proje = new ElektrikProje();
        var ana = AnaPanoYap(proje, new Linye { LinyeNo = "M1", YukTipiAd = "Motor", FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 30_000 });
        var tali = TaliPanoYap(proje, ana, "Yüksek Tali", 10,
            new Linye { LinyeNo = "M2", YukTipiAd = "Motor", FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 90_000 });

        HesaplaTumu(proje, kut);

        if (tali.GirisAkimA <= 100)
            throw new InvalidOperationException($"Test kurgusu: tali pano akımı 100A üstünde olmalıydı. Bulunan={tali.GirisAkimA:N1}A.");
        if (tali.KacakAkimRolesi?.TroidTipiMi != true || tali.KacakAkimRolesi.SensitiviteMa != 30)
            throw new InvalidOperationException("100A üstündeki tali panoda 30mA troid KAR seçilmeli.");
        if (tali.KacakAkimAkimTrafosu == null)
            throw new InvalidOperationException("Troid KAR seçilen tali panoya akım trafosu atanmalı.");
        if (tali.KacakAkimAkimTrafosu.PrimerA < tali.GirisAkimA)
            throw new InvalidOperationException($"Seçilen AT primeri pano akımını taşımalı (AT={tali.KacakAkimAkimTrafosu.PrimerA}A, pano={tali.GirisAkimA:N1}A).");
        if (tali.KacakAkimAkimTrafosu.SekonderA != tali.KacakAkimRolesi.AkimTrafosuSekonderA)
            throw new InvalidOperationException("Seçilen AT sekonderi troid röle ihtiyacıyla uyumlu olmalı.");

        if (ana.KacakAkimRolesi?.TroidTipiMi != true || ana.KacakAkimRolesi.SensitiviteMa != 300)
            throw new InvalidOperationException("100A üstündeki ana panoda 300mA troid KAR seçilmeli.");
        if (ana.KacakAkimAkimTrafosu == null || ana.KacakAkimAkimTrafosu.PrimerA < ana.GirisAkimA)
            throw new InvalidOperationException("Ana pano troid KAR için yeterli akım trafosu atanmalı.");
    }

    public static void KacakAkim_PanoBazindaKapatilir()
    {
        var kut = new ElektrikKutuphaneServisi();
        var proje = new ElektrikProje { ProjeAdi = "KAR Kapalı Testi" };
        var ana = AnaPanoYap(proje);
        var tali = TaliPanoYap(proje, ana, "KAR Olmayan Pano", 15,
            new Linye
            {
                LinyeNo = "M1", YukTipiAd = "Mekanik", FazTipi = FazTipi.Trifaze,
                SortiSayisi = 1, BirimGucW = 90_000, KabloMesafeM = 20
            });

        HesaplaTumu(proje, kut);
        if (tali.KacakAkimRolesi == null)
            throw new InvalidOperationException("Test ön koşulu: KAR aktifken röle atanmalıydı.");
        var aktifModul = tali.ModulSayisi;

        tali.KacakAkimRolesiAktif = false;
        HesaplaTumu(proje, kut);
        if (tali.KacakAkimRolesi != null || tali.KacakAkimAkimTrafosu != null)
            throw new InvalidOperationException("KAR kapatılan panoda röle ve troid akım trafosu temizlenmeli.");
        if (tali.ModulSayisi >= aktifModul)
            throw new InvalidOperationException("KAR kapatılınca pano modül sayısı azalmalı.");

        var opts = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
        };
        var json = System.Text.Json.JsonSerializer.Serialize(proje, opts);
        var geri = System.Text.Json.JsonSerializer.Deserialize<ElektrikProje>(json, opts);
        var geriTali = geri?.TumPanolar.FirstOrDefault(p => p.Ad == tali.Ad)
            ?? throw new InvalidOperationException("KAR kapalı pano JSON round-trip'te kayboldu.");
        if (geriTali.KacakAkimRolesiAktif)
            throw new InvalidOperationException("Pano bazlı KAR kapalı tercihi JSON round-trip'te korunmalı.");

        var path = Path.Combine(Path.GetTempPath(), $"m11_kar_kapali_{Guid.NewGuid():N}.xlsx");
        try
        {
            TekHatSemasiExcelServisi.RaporOlustur(proje, path);
            using var workbook = new ClosedXML.Excel.XLWorkbook(path);
            var sayfa = workbook.Worksheet(tali.Ad);
            if (sayfa.Cell(4, 6).GetString() != "Kapalı")
                throw new InvalidOperationException("KAR kapalı pano Excel raporunda 'Kapalı' gösterilmeli.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public static void PanoBoyutlandirma_OtomatikVeManuelSecim()
    {
        var kut = new ElektrikKutuphaneServisi();
        var proje = new ElektrikProje();
        var pano = AnaPanoYap(proje);
        for (int i = 0; i < 20; i++)
        {
            pano.Linyeler.Add(new Linye
            {
                LinyeNo = $"P{i + 1}", YukTipiAd = "Priz", FazTipi = FazTipi.Monofaze,
                SortiSayisi = 1, BirimGucW = 300
            });
        }

        HesaplaTumu(proje, kut);

        if (pano.GovdeTipi != PanoGovdeTipi.DuvarTipi || pano.PanoDerinlikCm != 90)
            throw new InvalidOperationException("Yeni panoda duvar tipi ve 90 cm dikili pano derinliği varsayılan gelmeli.");
        if (pano.ModulAlaniM2 <= 0 || pano.KlemensAlaniM2 <= 0 || pano.GerekliPanoAlaniM2 <= 0)
            throw new InvalidOperationException("Modül, klemens ve gerekli pano alanları m² cinsinden hesaplanmalı.");
        if (pano.KlemensSayisi != 65)
            throw new InvalidOperationException($"20 monofaze linye için 65 klemens beklenirdi, bulunan: {pano.KlemensSayisi}.");
        if (Math.Abs(pano.ModulAlaniM2 - pano.ModulSayisi * 0.01) > 0.000001)
            throw new InvalidOperationException("Duvar tipi panoda her modül 0,01 m² hesaplanmalı.");
        if (Math.Abs(pano.KlemensAlaniM2 - pano.KlemensSayisi * 0.005) > 0.000001)
            throw new InvalidOperationException("Duvar tipi panoda her klemens 0,005 m² hesaplanmalı.");
        if (Math.Abs(pano.GerekliPanoAlaniM2 - (pano.ModulAlaniM2 + pano.KlemensAlaniM2)) > 0.000001)
            throw new InvalidOperationException("Duvar tipi gerekli pano alanı modül ve klemens alanlarının toplamı olmalı.");
        if (pano.PanoOnYuzAlaniM2 + 0.000001 < pano.GerekliPanoAlaniM2)
            throw new InvalidOperationException("Otomatik seçilen pano ölçüsü gerekli yerleşim alanını karşılamalı.");

        var otomatik = PanoBoyutlandirmaServisi.OtomatikOlcuyuSec(pano.GerekliPanoAlaniM2);
        if (Math.Abs(pano.PanoGenislikM - otomatik.GenislikM) > 0.0001 ||
            Math.Abs(pano.PanoYukseklikM - otomatik.YukseklikM) > 0.0001)
            throw new InvalidOperationException("Pano ölçüsü standart katalogdan otomatik seçilmeli.");

        for (int i = 20; i < 40; i++)
        {
            pano.Linyeler.Add(new Linye
            {
                LinyeNo = $"P{i + 1}", YukTipiAd = "Priz", FazTipi = FazTipi.Monofaze,
                SortiSayisi = 1, BirimGucW = 300
            });
        }
        HesaplaTumu(proje, kut);
        if (pano.GerekliPanoAlaniM2 <= 1.0)
            throw new InvalidOperationException("Büyük pano testinde gerekli alan 1 m²'yi aşmalıydı.");
        if (pano.GovdeTipi != PanoGovdeTipi.DikiliTip || pano.PanoDerinlikCm != 90)
            throw new InvalidOperationException("Alanı 1 m²'yi aşan duvar tipi pano otomatik olarak 90 cm derinlikli dikili tipe çevrilmeli.");

        pano.PanoOlcusuOtomatik = false;
        pano.PanoGenislikM = 0.20;
        pano.PanoYukseklikM = 0.30;
        pano.GovdeTipi = PanoGovdeTipi.DikiliTip;
        pano.PanoDerinlikCm = 40;
        HesaplaTumu(proje, kut);
        if (Math.Abs(pano.PanoGenislikM - 0.20) > 0.0001 || Math.Abs(pano.PanoYukseklikM - 0.30) > 0.0001)
            throw new InvalidOperationException("Kullanıcının elle seçtiği pano ölçüsü yeniden hesapta korunmalı.");
        if (pano.PanoDerinlikCm != 40 || pano.GovdeTipi != PanoGovdeTipi.DikiliTip)
            throw new InvalidOperationException("Dikili pano gövde tipi ve seçilen derinlik korunmalı.");

        var json = System.Text.Json.JsonSerializer.Serialize(proje);
        var geri = System.Text.Json.JsonSerializer.Deserialize<ElektrikProje>(json)
            ?? throw new InvalidOperationException("Pano boyutlandırma JSON round-trip başarısız.");
        var geriPano = geri.TumPanolar.Single(p => p.Ad == pano.Ad);
        if (geriPano.PanoOlcusuOtomatik || geriPano.GovdeTipi != PanoGovdeTipi.DikiliTip ||
            geriPano.PanoDerinlikCm != 40 || Math.Abs(geriPano.PanoGenislikM - 0.20) > 0.0001)
            throw new InvalidOperationException("Pano gövde ve manuel ölçü tercihleri proje dosyasında korunmalı.");
    }

    public static void KacakAkimKutuphane_EskiDosyayaTroidVeAkimTrafosuEklenir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"m11_kar_troid_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(
                Path.Combine(dir, "KacakAkimRolesiKutuphanesi.json"),
                """[{ "Ad": "300mA 4P 100A AC", "SensitiviteMa": 300, "AnmaAkimA": 100, "KutupSayisi": 4, "Tip": "AC" }]""");

            var kut = new ElektrikKutuphaneServisi(dir);
            if (!kut.KacakAkimRoleleri.Any(k => k.TroidTipiMi && k.SensitiviteMa == 300 && k.AnmaAkimA >= 630))
                throw new InvalidOperationException("Eski KAR kütüphanesine 300mA troid röle varsayılanı eklenmeli.");
            if (!kut.KacakAkimRoleleri.Any(k => k.TroidTipiMi && k.SensitiviteMa == 30 && k.AnmaAkimA >= 630))
                throw new InvalidOperationException("Eski KAR kütüphanesine 30mA troid röle varsayılanı eklenmeli.");
            if (!kut.AkimTrafolari.Any(a => a.Tip.Contains("Toroid", StringComparison.OrdinalIgnoreCase) && a.PrimerA >= 630 && a.SekonderA == 5))
                throw new InvalidOperationException("Eksik akım trafosu kütüphanesine troid AT varsayılanları eklenmeli.");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    public static void GirisSigortasi_AnaPanoYuksekAltPanoYukundeGuncellenir()
    {
        var kut = new ElektrikKutuphaneServisi();
        var proje = new ElektrikProje();
        var ana = AnaPanoYap(proje);
        TaliPanoYap(proje, ana, "Büyük Yük Panosu", 10,
            new Linye { LinyeNo = "M1", YukTipiAd = "Motor", FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 650_000 });

        HesaplaTumu(proje, kut);

        if (ana.Linyeler.Count != 0)
            throw new InvalidOperationException("Test kurgusu: ana panoda doğrudan linye olmamalı.");
        if (ana.GirisAkimA <= 630)
            throw new InvalidOperationException($"Test kurgusu: ana pano giriş akımı 630A üstünde olmalıydı. Bulunan={ana.GirisAkimA:N1}A.");
        if (ana.GirisSigortaA <= 630)
            throw new InvalidOperationException($"Ana pano giriş sigortası 630A üstüne güncellenmeli. Bulunan={ana.GirisSigortaA}A.");
        if (ana.GirisSigortaA != 1250)
            throw new InvalidOperationException($"650kW alt pano yükünde ana pano giriş sigortası 1250A olmalı. Bulunan={ana.GirisSigortaA}A.");
    }

    // ── Linye kablo hesabı (K1) ──

    public static void LinyeKabloHesabi_KesitVeDusumAtanir()
    {
        var proje = new ElektrikProje();
        var kut = new ElektrikKutuphaneServisi();
        AnaPanoYap(proje, Priz(sayi: 8, birim: 300, faz: FazTipi.Monofaze, mesafe: 25));
        HesaplaTumu(proje, kut);

        var l = proje.KokPano!.Linyeler[0];
        if (string.IsNullOrWhiteSpace(l.KabloKesiti))
            throw new InvalidOperationException("Linye kablo kesiti hesaplanmadı (K1).");
        if (l.KabloGerilimDususYuzde <= 0)
            throw new InvalidOperationException("Linye kablo gerilim düşümü hesaplanmadı (K1).");
        if (l.KabloIsiKaybiW <= 0)
            throw new InvalidOperationException("Linye kablo ısı kaybı hesaplanmadı (K1).");
    }

    // ── Kümülatif gerilim düşümü (A1) ──

    public static void KumulatifGerilimDususu_ZincirBoyuncaToplanirVeSinirAsimiIsaretlenir()
    {
        var proje = new ElektrikProje();
        var kut = new ElektrikKutuphaneServisi();
        var ana = AnaPanoYap(proje);
        var tali1 = TaliPanoYap(proje, ana, "Tali-1", 50);
        var tali2 = TaliPanoYap(proje, tali1, "Tali-2", 50,
            new Linye { LinyeNo = "P1", YukTipiAd = "Priz", FazTipi = FazTipi.Monofaze, SortiSayisi = 10, BirimGucW = 300, KabloMesafeM = 20 });
        HesaplaTumu(proje, kut);

        var linye = tali2.Linyeler[0];

        // Trafosuz kök panonun kaynak tarafı referanstır → kümülatif 0.
        if (Math.Abs(ana.KumulatifGerilimDususYuzde) > 1e-9)
            throw new InvalidOperationException($"Trafosuz kök panonun kümülatif ΔU'su 0 olmalı: %{ana.KumulatifGerilimDususYuzde:N3}.");

        // Zincir additif: her seviye üst kümülatif + kendi segment ΔU'su.
        double beklenenTali2 = tali1.KumulatifGerilimDususYuzde + tali2.KabloGerilimDususYuzde;
        if (Math.Abs(tali2.KumulatifGerilimDususYuzde - beklenenTali2) > 1e-6)
            throw new InvalidOperationException($"Tali-2 kümülatif ΔU additif olmalı (beklenen %{beklenenTali2:N4}, bulunan %{tali2.KumulatifGerilimDususYuzde:N4}).");

        double beklenenLinye = tali2.KumulatifGerilimDususYuzde + linye.KabloGerilimDususYuzde;
        if (Math.Abs(linye.KumulatifGerilimDususYuzde - beklenenLinye) > 1e-6)
            throw new InvalidOperationException($"Linye kümülatif ΔU additif olmalı (beklenen %{beklenenLinye:N4}, bulunan %{linye.KumulatifGerilimDususYuzde:N4}).");

        // Daha derin segment kümülatifi büyütmeli.
        if (!(tali2.KumulatifGerilimDususYuzde > tali1.KumulatifGerilimDususYuzde))
            throw new InvalidOperationException("Daha derin pano kümülatif ΔU'yu büyütmeli.");

        double c = linye.KumulatifGerilimDususYuzde;
        if (c <= 0) throw new InvalidOperationException("Linye kümülatif ΔU pozitif olmalı.");

        // Toplam sınır kümülatifin altına çekilince linye aşım işaretlenir.
        proje.IzinVerilenToplamGerilimDususYuzde = c * 0.5;
        HesaplaTumu(proje, kut);
        if (!linye.GerilimDususuAsiyor)
            throw new InvalidOperationException("Kümülatif ΔU toplam sınırı aşınca linye işaretlenmeli.");

        // Sınır kümülatifin üstüne çıkınca aşım kalkar.
        proje.IzinVerilenToplamGerilimDususYuzde = c * 2 + 1;
        HesaplaTumu(proje, kut);
        if (linye.GerilimDususuAsiyor || tali2.GerilimDususuAsiyor)
            throw new InvalidOperationException("Kümülatif ΔU sınırın altındayken aşım işaretlenmemeli.");
    }

    // ── Kablo kesiti yeterlilik uyarısı (A2) ──

    public static void KabloKesitiYeterli_AsiriYukteIsaretlenir()
    {
        var proje = new ElektrikProje();
        var kut = new ElektrikKutuphaneServisi();
        var ana = AnaPanoYap(proje,
            new Linye { LinyeNo = "P1", YukTipiAd = "Priz", FazTipi = FazTipi.Monofaze, SortiSayisi = 5, BirimGucW = 300, KabloMesafeM = 15 });
        HesaplaTumu(proje, kut);

        // Makul yükte kesit yeterli olmalı.
        if (!ana.Linyeler[0].KabloKesitiYeterli)
            throw new InvalidOperationException("Normal linyede kablo kesiti yeterli işaretlenmeliydi.");

        // Aşırı yüklü trifaze linye: azami paralel kol × 240 mm² bile termal taşıyamaz → yetersiz.
        ana.Linyeler.Add(new Linye
        {
            LinyeNo = "M1", YukTipiAd = "Motor", FazTipi = FazTipi.Trifaze,
            SortiSayisi = 1, BirimGucW = 5_000_000, KabloMesafeM = 30
        });
        HesaplaTumu(proje, kut);

        var agir = ana.Linyeler.First(l => l.LinyeNo == "M1");
        if (agir.KabloKesitiYeterli)
            throw new InvalidOperationException($"Aşırı yüklü linyede kablo kesiti yetersiz işaretlenmeliydi (akım {agir.AkimA:N0} A, kesit {agir.KabloKesiti}).");
    }

    // ── Pano metraj kimliği: standart alan / derinlik bazında toplama ──

    public static void PanoMetraj_StandartAlanVeDerinligeGoreTekSatir()
    {
        // Üç duvar panosu farklı HAM gerekli alanlı ama hepsi 0,50×0,60 standardına oturmuş (0,30 m²);
        // iki dikili pano 90 cm. Metraj kimliği ham alanı değil standart alanı/derinliği esas almalı.
        var d1 = new Pano { GovdeTipi = PanoGovdeTipi.DuvarTipi, PanoGenislikM = 0.50, PanoYukseklikM = 0.60, GerekliPanoAlaniM2 = 0.21 };
        var d2 = new Pano { GovdeTipi = PanoGovdeTipi.DuvarTipi, PanoGenislikM = 0.50, PanoYukseklikM = 0.60, GerekliPanoAlaniM2 = 0.26 };
        var d3 = new Pano { GovdeTipi = PanoGovdeTipi.DuvarTipi, PanoGenislikM = 0.50, PanoYukseklikM = 0.60, GerekliPanoAlaniM2 = 0.29 };
        var k1 = new Pano { GovdeTipi = PanoGovdeTipi.DikiliTip, PanoDerinlikCm = 90 };
        var k2 = new Pano { GovdeTipi = PanoGovdeTipi.DikiliTip, PanoDerinlikCm = 90 };

        // Etiket: ham gerekli alan değil standart alan / derinlik.
        if (PanoBoyutlandirmaServisi.MetrajEtiketi(d2) != "Duvar Tipi 0,30 m²")
            throw new InvalidOperationException($"Duvar etiketi standart alanı göstermeli (bulunan: '{PanoBoyutlandirmaServisi.MetrajEtiketi(d2)}').");
        if (PanoBoyutlandirmaServisi.MetrajEtiketi(k1) != "Dikili Tip 90 cm derinlik")
            throw new InvalidOperationException($"Dikili etiketi derinliği göstermeli, m² içermemeli (bulunan: '{PanoBoyutlandirmaServisi.MetrajEtiketi(k1)}').");

        // Gruplama: 3 duvar (0,30) → tek grup, 2 dikili (90 cm) → tek grup.
        var gruplar = new[] { d1, d2, d3, k1, k2 }
            .GroupBy(PanoBoyutlandirmaServisi.MetrajAnahtari).ToList();
        if (gruplar.Count != 2)
            throw new InvalidOperationException($"Farklı ham alanlı ama aynı standarda oturan panolar tek grupta toplanmalı (grup sayısı: {gruplar.Count}).");
        var duvar = gruplar.Single(g => g.Key.Govde == PanoGovdeTipi.DuvarTipi);
        var dikili = gruplar.Single(g => g.Key.Govde == PanoGovdeTipi.DikiliTip);
        if (duvar.Count() != 3 || dikili.Count() != 2)
            throw new InvalidOperationException($"Duvar 3, dikili 2 adet beklenirdi (duvar={duvar.Count()}, dikili={dikili.Count()}).");
    }

    // ── Linye sigorta üst aralığı (C3) ──

    public static void LinyeSigortasi_BuyukTrifazeLinye250AUstuneCikar()
    {
        var proje = new ElektrikProje();
        var kut = new ElektrikKutuphaneServisi();
        // ~150 kW trifaze yük → I ≈ 255 A → sigorta 250A'da kalmamalı, üst standarda (315A) çıkmalı.
        AnaPanoYap(proje, new Linye
        {
            LinyeNo = "M1", YukTipiAd = "Motor", FazTipi = FazTipi.Trifaze,
            SortiSayisi = 1, BirimGucW = 150_000, KabloMesafeM = 20
        });
        HesaplaTumu(proje, kut);

        var l = proje.KokPano!.Linyeler[0];
        if (l.SigortaAmper <= 250)
            throw new InvalidOperationException($"Büyük trifaze linye sigortası 250A üstüne çıkmalıydı (akım {l.AkimA:N0} A, sigorta {l.SigortaAmper} A).");
    }

    // ── Jeneratör lokal grup hedefi yoksa atlanır (A4) ──

    public static void JeneratorLokalGrup_HedefYoksaAnaPanoyaDusmedenAtlanir()
    {
        var proje = new ElektrikProje();
        var kut = new ElektrikKutuphaneServisi();
        AnaPanoYap(proje, new Linye
        {
            LinyeNo = "P1", YukTipiAd = "Priz", FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 50_000
        });
        HesaplaTumu(proje, kut);

        // Hedef tali panosu bulunmayan LOKAL grup → atlanmalı (ana pano yüküne boyutlanmamalı).
        var lokal = new JeneratorGrubu { AnaPanoAd = "Yok-Olan-Pano", BeslemeKapsami = JeneratorBeslemeKapsami.LokalBolge };
        proje.JeneratorGruplari.Add(lokal);
        JeneratorHesapServisi.HesaplaTumu(proje, kut);

        if (lokal.Jeneratorler.Count != 0 || lokal.HesaplananYukKW != 0)
            throw new InvalidOperationException("Hedefi bulunmayan LOKAL grup atlanmalı; ana panoya boyutlanmamalı.");

        // Hedef adı bulunmayan TESİS GENELİ grup → ana panoya düşer ve boyutlanır.
        var tesis = new JeneratorGrubu { AnaPanoAd = "Yine-Yok", BeslemeKapsami = JeneratorBeslemeKapsami.TesisGeneli };
        proje.JeneratorGruplari.Add(tesis);
        JeneratorHesapServisi.HesaplaTumu(proje, kut);

        if (tesis.HesaplananYukKW <= 0)
            throw new InvalidOperationException("Tesis geneli grup hedefi yoksa ana panoya düşüp boyutlanmalı.");
    }

    public static void Kontaktor_LinyeBayragiIleOtomatikSecilir()
    {
        var kut = new ElektrikKutuphaneServisi();
        var proje = new ElektrikProje();
        var ana = AnaPanoYap(proje);
        var tali = TaliPanoYap(proje, ana, "Tali", 10,
            new Linye { LinyeNo = "M1", YukTipiAd = "Motor", FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 5000, KontaktorluMu = true },
            new Linye { LinyeNo = "M2", YukTipiAd = "Motor", FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 5000 });

        HesaplaTumu(proje, kut);

        var l1 = tali.Linyeler[0];
        var l2 = tali.Linyeler[1];
        if (l1.Kontaktor == null)
            throw new InvalidOperationException("KontaktorluMu işaretli linyeye (tali panoda) kontaktör atanmalı.");
        if (l1.Kontaktor.AnmaAkimA < l1.AkimA)
            throw new InvalidOperationException($"Seçilen kontaktör linye akımını taşımalı (anma={l1.Kontaktor.AnmaAkimA}A, akım={l1.AkimA:N1}A).");
        if (l2.Kontaktor != null)
            throw new InvalidOperationException("İşaretsiz linyeye kontaktör atanmamalı.");

        // Bayrak kaldırılınca kontaktör temizlenmeli
        l1.KontaktorluMu = false;
        HesaplaTumu(proje, kut);
        if (l1.Kontaktor != null)
            throw new InvalidOperationException("Bayrak kaldırılınca kontaktör temizlenmeli.");
    }

    public static void DarbeKesici_BayrakVe16AKontaktorKurali()
    {
        var kut = new ElektrikKutuphaneServisi();
        var proje = new ElektrikProje();
        var ana = AnaPanoYap(proje);
        var tali = TaliPanoYap(proje, ana, "Tali", 10,
            new Linye { LinyeNo = "A1", YukTipiAd = "Priz", FazTipi = FazTipi.Monofaze, SortiSayisi = 1, BirimGucW = 2000, DarbeKesiciMi = true },  // ~10A ≤16
            new Linye { LinyeNo = "A2", YukTipiAd = "Priz", FazTipi = FazTipi.Monofaze, SortiSayisi = 1, BirimGucW = 6000, DarbeKesiciMi = true },  // ~31A >16
            new Linye { LinyeNo = "A3", YukTipiAd = "Priz", FazTipi = FazTipi.Monofaze, SortiSayisi = 1, BirimGucW = 2000 });                        // işaretsiz

        HesaplaTumu(proje, kut);

        var l1 = tali.Linyeler[0];
        var l2 = tali.Linyeler[1];
        var l3 = tali.Linyeler[2];

        if (l1.AkimA > 16) throw new InvalidOperationException($"Test kurgusu: A1 ≤16A olmalı (akım={l1.AkimA:N1}).");
        if (l2.AkimA <= 16) throw new InvalidOperationException($"Test kurgusu: A2 >16A olmalı (akım={l2.AkimA:N1}).");

        if (l1.DarbeKesici == null)
            throw new InvalidOperationException("DarbeKesiciMi işaretli linyeye darbe kesici atanmalı.");
        if (l1.DarbeKesici.AnmaAkimA < l1.AkimA)
            throw new InvalidOperationException("Seçilen darbe kesici linye akımını taşımalı.");
        if (l1.Kontaktor != null)
            throw new InvalidOperationException("16A altı darbe kesicili linyeye kontaktör eklenmemeli.");

        if (l2.DarbeKesici == null)
            throw new InvalidOperationException("16A üstü darbe kesicili linyeye de darbe kesici atanmalı.");
        if (l2.Kontaktor == null)
            throw new InvalidOperationException("16A üstü darbe kesicili linyeye kontaktör de otomatik eklenmeli.");

        if (l3.DarbeKesici != null)
            throw new InvalidOperationException("İşaretsiz linyeye darbe kesici eklenmemeli.");
    }

    public static void KontrolPanosu_DarbeKesiciAtamaTekilVeKabloGruplu()
    {
        var vm = new TekHatSemasiViewModel();
        vm.YeniPanoEkle("Ana Pano", "Z", PanoTipi.AnaPano);
        var ana = vm.SeciliPano!;
        vm.YeniPanoEkle("Kaynak Pano", "1", PanoTipi.TaliPano);
        var kaynak = vm.SeciliPano!;

        var l1 = new Linye
        {
            LinyeNo = "D1",
            YukTipiAd = "Priz",
            FazTipi = FazTipi.Monofaze,
            SortiSayisi = 1,
            BirimGucW = 2500,
            DarbeKesiciMi = true,
            Aciklama = "Salon darbe"
        };
        var l2 = new Linye
        {
            LinyeNo = "D2",
            YukTipiAd = "Priz",
            FazTipi = FazTipi.Monofaze,
            SortiSayisi = 1,
            BirimGucW = 1800,
            DarbeKesiciMi = true,
            Aciklama = "Koridor darbe"
        };
        var normal = new Linye
        {
            LinyeNo = "P1",
            YukTipiAd = "Priz",
            FazTipi = FazTipi.Monofaze,
            SortiSayisi = 1,
            BirimGucW = 300
        };
        kaynak.Linyeler.Add(l1);
        kaynak.Linyeler.Add(l2);
        kaynak.Linyeler.Add(normal);
        vm.TumunuHesapla();

        vm.YeniPanoEkle("Kontrol-1", "1", PanoTipi.KontrolPanosu);
        var kontrol1 = vm.SeciliPano!;
        vm.YeniPanoEkle("Kontrol-2", "2", PanoTipi.KontrolPanosu);
        var kontrol2 = vm.SeciliPano!;

        if (kontrol1.UstPano != null || ana.AltPanolar.Contains(kontrol1))
            throw new InvalidOperationException("Kontrol panosu güç ağacına otomatik alt pano olarak bağlanmamalı.");

        var atama1 = vm.KontrolButonuEkle(kontrol1, kaynak, l1);
        if (atama1 == null)
            throw new InvalidOperationException("Darbe kesicili linye kontrol panosuna atanmalı.");
        if (kontrol1.KontrolSinyalKablolari.Count != 1 || kontrol1.KontrolSinyalKablolari[0].DamarSayisi != 1)
            throw new InvalidOperationException("İlk kontrol ataması kaynak pano için 1 damarlı tek sinyal kablosu üretmeli.");

        kontrol1.KontrolSinyalKablolari[0].MetrajM = 42;
        kontrol1.KontrolSinyalKablolari[0].KesitMm2 = 1.5;
        kontrol1.KontrolSinyalKablolari[0].KabloTipi = "LIYY";

        var tekrar = vm.KontrolButonuEkle(kontrol2, kaynak, l1);
        if (tekrar != null)
            throw new InvalidOperationException("Bir darbe kesicili linye ikinci kontrol panosuna atanamamalı.");

        int topluEklenen = vm.KontrolButonlarininHepsiniEkle(kontrol1, kaynak);
        if (topluEklenen != 1)
            throw new InvalidOperationException($"Hepsini Ekle seçili panodaki kalan darbe kesiciyi eklemeliydi; bulunan: {topluEklenen}.");
        if (kontrol1.KontrolSinyalKablolari.Count != 1)
            throw new InvalidOperationException("Aynı kaynak panodaki iki darbe kesici için ayrı kablo satırı açılmamalı.");
        if (kontrol1.KontrolSinyalKablolari[0].DamarSayisi != 2)
            throw new InvalidOperationException($"Damar sayısı iki atama için 2 olmalı, bulunan: {kontrol1.KontrolSinyalKablolari[0].DamarSayisi}.");
        if (Math.Abs(kontrol1.KontrolSinyalKablolari[0].MetrajM - 42) > 0.001)
            throw new InvalidOperationException("Sinyal kablosu metrajı damar sayısı güncellenirken korunmalı.");
        if (Math.Abs(kontrol1.KontrolSinyalKablolari[0].KesitMm2 - 1.5) > 0.001 ||
            kontrol1.KontrolSinyalKablolari[0].KabloTipi != "LIYY")
            throw new InvalidOperationException("Sinyal kablosu kesiti ve tipi damar sayısı güncellenirken korunmalı.");
        if (vm.KontrolButonlarininHepsiniEkle(kontrol2, kaynak) != 0)
            throw new InvalidOperationException("Hepsini Ekle başka kontrol panosuna atanmış darbe kesicileri alamamalı.");

        var dxfPath = Path.Combine(Path.GetTempPath(), $"m11_kontrol_sinyal_{Guid.NewGuid():N}.dxf");
        try
        {
            TekHatSemasiDxfServisi.DxfCiktiAl(vm.Proje, dxfPath, vm.Kutuphane);
            var dxf = File.ReadAllText(dxfPath);
            if (!dxf.Contains("M11-KONTROL") || !dxf.Contains("LIYY") ||
                !dxf.Contains("2x1.5 mm") || !dxf.Contains("L=42m"))
                throw new InvalidOperationException("Kontrol panosu sinyal kablosu tip, damar×kesit ve metrajıyla DXF'te görünmeli.");
        }
        finally
        {
            if (File.Exists(dxfPath)) File.Delete(dxfPath);
        }

        if (vm.KontrolIcinUygunDarbeLinyeleri(kaynak, kontrol2).Any(l => l.Id == l2.Id))
            throw new InvalidOperationException("Başka kontrol panosuna atanmış darbe linyesi aday listesinde görünmemeli.");

        l2.DarbeKesiciMi = false;
        vm.TumunuHesapla();
        if (kontrol1.KontrolButonlari.Any(a => a.LinyeId == l2.Id))
            throw new InvalidOperationException("Darbe kesici bayrağı kapanan linye kontrol panosundan temizlenmeli.");
        if (kontrol1.KontrolSinyalKablolari.Count != 1 || kontrol1.KontrolSinyalKablolari[0].DamarSayisi != 1)
            throw new InvalidOperationException("Geçersiz atama temizlenince kaynak pano sinyal kablosu damar sayısı güncellenmeli.");
    }

    // ── Kompanzasyon ──

    public static void Kompanzasyon_GerekliQFormulu()
    {
        // P=20kW, cosφ 0.85 → 0.99 : Q = P(tanφ1 - tanφ2) ≈ 9.54 kVAr
        double q = KompanzasyonHesapServisi.GerekliQHesapla(20000, 0.85, 0.99);
        double beklenen = 20000 * (Math.Tan(Math.Acos(0.85)) - Math.Tan(Math.Acos(0.99)));
        if (Math.Abs(q - beklenen) > 1.0)
            throw new InvalidOperationException($"Gerekli Q formülü tutmuyor: {q:N0} VAr (beklenen {beklenen:N0}).");
    }

    public static void Kompanzasyon_GecersizCosFNaNUretmez()
    {
        double q = KompanzasyonHesapServisi.GerekliQHesapla(20000, 1.2, 1.1);
        if (double.IsNaN(q) || double.IsInfinity(q))
            throw new InvalidOperationException("Geçersiz cosφ değerleri kompanzasyon hesabında NaN/Infinity üretmemeli.");
    }

    public static void Kompanzasyon_KademeSayisiNormalizeEdilir()
    {
        var proje = new ElektrikProje();
        var kut = new ElektrikKutuphaneServisi();
        var ana = AnaPanoYap(proje, new Linye { FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 20000 });
        HesaplaTumu(proje, kut);
        var sonuc = new KompanzasyonSonucu { KondansatorKademeSayisi = 10 }; // standart dışı
        sonuc = KompanzasyonHesapServisi.Hesapla(ana, proje, kut, sonuc);
        if (sonuc.KondansatorKademeSayisi != 6)
            throw new InvalidOperationException($"Standart dışı kademe sayısı 6'ya normalize edilmeliydi, bulunan: {sonuc.KondansatorKademeSayisi}.");
    }

    public static void Kompanzasyon_ElleAyarKademeSayisiniKorur()
    {
        var proje = new ElektrikProje();
        var kut = new ElektrikKutuphaneServisi();
        var ana = AnaPanoYap(proje, new Linye { FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 30000 });
        HesaplaTumu(proje, kut);

        var sonuc = new KompanzasyonSonucu { KondansatorKademeSayisi = 12 };
        sonuc = KompanzasyonHesapServisi.Hesapla(ana, proje, kut, sonuc); // 12 kademe üretir
        int uretilen = sonuc.KondansatorKademeleri.Count;
        sonuc.KademelerElleAyarlandi = true;
        // Yeniden hesap: elle ayar açıkken kademe listesi yeniden üretilmemeli
        sonuc = KompanzasyonHesapServisi.Hesapla(ana, proje, kut, sonuc);
        if (sonuc.KondansatorKademeleri.Count != uretilen || uretilen != 12)
            throw new InvalidOperationException($"Elle ayarlı 12 kademe korunmalıydı (üretilen={uretilen}, son={sonuc.KondansatorKademeleri.Count}).");
    }

    public static void Kompanzasyon_PanoKVarIleBoyutlandirilirVeDxfeYazilir()
    {
        var vm = new TekHatSemasiViewModel();
        vm.YeniPanoEkle("", "Z", PanoTipi.AnaPano);
        var ana = vm.Proje.KokPano!;
        // Büyük yük → kompanzasyon tetiklenir (>15 kW)
        ana.Linyeler.Add(new Linye { YukTipiAd = "Priz", FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 200_000 });

        // Kompanzasyon panosu ana panonun altına eklenir
        vm.SeciliPano = ana;
        vm.YeniPanoEkle("Kompanzasyon", "Z", PanoTipi.Kompanzasyon);
        var komp = vm.Proje.TumPanolar.First(p => p.PanoTipi == PanoTipi.Kompanzasyon);
        komp.KabloMesafeM = 5;
        vm.TumunuHesapla();

        if (vm.Proje.Kompanzasyon == null || vm.Proje.Kompanzasyon.ToplamKondansatorKVar <= 0)
            throw new InvalidOperationException("Kompanzasyon kVAr üretilmedi (test kurulumu).");

        // Yük bağlı olmasa da kondansatör bankı kVAr'ından gerçek akım/sigorta/kablo türemeli
        if (!(komp.GirisAkimA > 0))
            throw new InvalidOperationException("Kompanzasyon panosu akımı kondansatör kVAr'ından türetilmeliydi.");
        if (komp.GirisSigortaA <= 6)
            throw new InvalidOperationException($"Kompanzasyon giriş sigortası minimumda kalmamalı: {komp.GirisSigortaA}A.");
        double kesit = KabloHesapServisi.KesitDegeri(komp.KabloKesiti);
        if (kesit <= 2.5)
            throw new InvalidOperationException($"Kompanzasyon kablosu minimumda kalmamalı: '{komp.KabloKesiti}'.");
        // Aktif güç (ToplamGucW) ≈ 0 kalmalı — üst panonun aktif yük toplamını şişirmemeli
        if (komp.ToplamGucW > 1)
            throw new InvalidOperationException($"Kompanzasyon panosu aktif güç taşımamalı (≈0): {komp.ToplamGucW}W.");
        // Görünür güç ≈ kondansatör kVAr olmalı
        double sVA = vm.Proje.Kompanzasyon.ToplamKondansatorKVar * 1000.0;
        if (Math.Abs(komp.ToplamGorunurVA - sVA) > 1.0)
            throw new InvalidOperationException("Kompanzasyon görünür gücü kondansatör kVAr'ına eşit olmalıydı.");

        // DXF: kondansatör içeriği (kVAr etiketi) yazılmalı
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"m11_komp_dxf_{Guid.NewGuid():N}.dxf");
        try
        {
            TekHatSemasiDxfServisi.DxfCiktiAl(vm.Proje, path);
            var icerik = System.IO.File.ReadAllText(path);
            if (!icerik.Contains("kVAr"))
                throw new InvalidOperationException("DXF'te kompanzasyon kondansatör kVAr etiketi bulunmalı.");
        }
        finally
        {
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
    }

    // ── Eşzamanlılık / Yangın panosu / Giriş şalteri ──

    public static void Diversite_BeslemeSigortasiniRevizeEder()
    {
        var proje = new ElektrikProje();
        var kut = new ElektrikKutuphaneServisi();
        var ana = AnaPanoYap(proje);
        var tali = TaliPanoYap(proje, ana, "Tali", 20,
            new Linye { YukTipiAd = "Priz", FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 100_000 });

        tali.EszamanlilikFaktoru = 1.0;
        HesaplaTumu(proje, kut);
        int sigTam = tali.GirisSigortaA;
        int cikisTam = tali.UstPanoCikisSigortaA;
        double anaGucTam = ana.ToplamGucW;
        double bagliAkim = tali.GirisAkimA;

        tali.EszamanlilikFaktoru = 0.5;
        HesaplaTumu(proje, kut);

        if (Math.Abs(tali.GirisAkimA - bagliAkim) > 0.5)
            throw new InvalidOperationException("Bağlı (connected) akım ks'ten etkilenmemeli; yalnız talep akımı düşer.");
        if (!(tali.TalepAkimA < tali.GirisAkimA))
            throw new InvalidOperationException("Talep akımı ks ile düşmeliydi.");
        if (!(tali.GirisSigortaA < sigTam))
            throw new InvalidOperationException($"ks düşünce giriş sigortası küçülmeliydi ({sigTam}A→{tali.GirisSigortaA}A).");
        if (!(tali.UstPanoCikisSigortaA < cikisTam))
            throw new InvalidOperationException($"ks düşünce üst pano çıkış sigortası küçülmeliydi ({cikisTam}A→{tali.UstPanoCikisSigortaA}A).");
        if (!(ana.ToplamGucW < anaGucTam))
            throw new InvalidOperationException("ks düşünce ana pano toplam gücü azalmalıydı.");
    }

    public static void YanginPanosu_AnaGuceEklenmez()
    {
        var proje = new ElektrikProje();
        var kut = new ElektrikKutuphaneServisi();
        var ana = AnaPanoYap(proje);
        var normal = TaliPanoYap(proje, ana, "Normal", 10,
            new Linye { YukTipiAd = "Priz", FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 40_000 });
        var yangin = TaliPanoYap(proje, ana, "Yangın", 10,
            new Linye { YukTipiAd = "Priz", FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 30_000 });
        yangin.PanoTipi = PanoTipi.YanginPanosu;

        HesaplaTumu(proje, kut);

        // Ana pano gücü yalnız normal panoyu içermeli (yangın ana şalter öncesinden beslenir → hariç)
        if (Math.Abs(ana.ToplamGucW - normal.ToplamGucW) > 1.0)
            throw new InvalidOperationException(
                $"Yangın panosu ana güce katılmamalı. Ana={ana.ToplamGucW:N0}W, beklenen={normal.ToplamGucW:N0}W.");
        // Yangın panosu kendi yükünü/sigortasını yine hesaplamalı
        if (!(yangin.GirisSigortaA > 0))
            throw new InvalidOperationException("Yangın panosu kendi giriş sigortasını hesaplamalı.");
    }

    public static void YanginPanosu_PanoEkleAkisindaOlusturulur()
    {
        var vm = new TekHatSemasiViewModel();
        vm.YeniPanoEkle("", "Z", PanoTipi.AnaPano);
        var ana = vm.Proje.KokPano!;
        vm.SeciliPano = ana;

        vm.YeniPanoEkle("Yangın Panosu", "B1", PanoTipi.YanginPanosu);

        var yangin = vm.Proje.TumPanolar.SingleOrDefault(p => p.Ad == "Yangın Panosu")
            ?? throw new InvalidOperationException("Pano Ekle akışı yangın panosunu oluşturmalı.");
        if (yangin.PanoTipi != PanoTipi.YanginPanosu)
            throw new InvalidOperationException("Pano Ekle akışı yangın panosu tipini korumalı.");
        if (yangin.UstPano != ana || yangin.UstPanoAd != ana.Ad || !ana.AltPanolar.Contains(yangin))
            throw new InvalidOperationException("Yangın panosu seçili üst panoya bağlanmalı.");
    }

    public static void GirisSalteri_TrafosuzOtomatikAtanir()
    {
        var proje = new ElektrikProje();
        var kut = new ElektrikKutuphaneServisi();
        var ana = AnaPanoYap(proje,
            new Linye { YukTipiAd = "Priz", FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 100_000 });
        HesaplaTumu(proje, kut);

        if (!(ana.GirisSalteriA > 0))
            throw new InvalidOperationException("Trafosuz projede ana pano giriş şalteri otomatik atanmalı.");
        if (ana.GirisSalteriA < ana.GirisSigortaA)
            throw new InvalidOperationException(
                $"Giriş şalteri en az giriş sigortası kadar olmalı ({ana.GirisSalteriA}A < {ana.GirisSigortaA}A).");
    }

    // ── Kısa devre (K3) ──

    public static void KisaDevre_TrafoIk3Formulu()
    {
        var proje = new ElektrikProje { TrafoGucKVA = 630, TrafoUkYuzde = 4.0, SistemGerilimV = 400 };
        var kut = new ElektrikKutuphaneServisi();
        AnaPanoYap(proje, new Linye { FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 5000 });
        HesaplaTumu(proje, kut);
        // Z = 0.04·400²/630000 = 0.01016 Ω → Ik3 = 400/(√3·Z) ≈ 22.7 kA
        double ik = proje.KokPano!.KisaDevreAkimiKA;
        if (ik < 21 || ik > 24)
            throw new InvalidOperationException($"630 kVA / uk%4 trafo için Ik3 ≈ 22.7 kA beklenirdi, bulunan: {ik:N1} kA.");
    }

    public static void KisaDevre_AltPanodaAzalir()
    {
        var proje = new ElektrikProje { SebekeBaslangicIkKA = 10.0 };
        var kut = new ElektrikKutuphaneServisi();
        var ana = AnaPanoYap(proje, new Linye { FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 10000 });
        TaliPanoYap(proje, ana, "Tali", kabloM: 50,
            new Linye { FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 5000 });
        HesaplaTumu(proje, kut);

        double ikAna = ana.KisaDevreAkimiKA;
        double ikTali = proje.TumPanolar.Single(p => p.Ad == "Tali").KisaDevreAkimiKA;
        if (ikAna < 9.5 || ikAna > 10.5)
            throw new InvalidOperationException($"Şebeke Ik (10 kA) ana barada korunmalı, bulunan: {ikAna:N1} kA.");
        if (ikTali >= ikAna)
            throw new InvalidOperationException($"Tali panoda Ik kablo empedansıyla azalmalı (ana={ikAna:N1}, tali={ikTali:N1} kA).");
    }

    public static void KisaDevre_KabloReaktansiZincireDahil()
    {
        // Uzun kablolu tali pano: zincir Z = √(R² + X²) ile birleşmeli (kaynak saf reaktans,
        // kablo R + X). Beklenen Ik, motorun sakladığı kesit/kol/mesafe ve kaynak empedansından
        // birebir yeniden kurularak doğrulanır.
        var proje = new ElektrikProje { TrafoGucKVA = 630, TrafoUkYuzde = 4.0, SistemGerilimV = 400 };
        var kut = new ElektrikKutuphaneServisi();
        var ana = AnaPanoYap(proje, new Linye { FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 5000 });
        var tali = TaliPanoYap(proje, ana, "Tali", kabloM: 120,
            new Linye { FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 40000 });
        HesaplaTumu(proje, kut);

        double xKaynak = KisaDevreHesapServisi.KaynakEmpedansiOhm(proje, kut); // saf reaktans (kök X)
        double kesit = KabloHesapServisi.KesitDegeri(tali.KabloKesiti);
        int kol = Math.Max(1, tali.KabloParalelKolSayisi);
        double r = KabloHesapServisi.BakirOzdirenc * tali.KabloMesafeM / kesit / kol;
        double x = KabloHesapServisi.KabloReaktansOhmPerM * tali.KabloMesafeM / kol;

        double zKuadratur = Math.Sqrt(r * r + (xKaynak + x) * (xKaynak + x));
        double ikBeklenen = 400.0 / (Math.Sqrt(3) * zKuadratur) / 1000.0;
        if (Math.Abs(tali.KisaDevreAkimiKA - ikBeklenen) > 0.05)
            throw new InvalidOperationException(
                $"Tali Ik, Z=√(R²+X²) kuadratür birleşimiyle uyuşmalı (beklenen {ikBeklenen:N2}, bulunan {tali.KisaDevreAkimiKA:N2} kA).");

        // Reaktans gerçekten katkı yapmalı: yalnız-R aritmetik toplamından (eski davranış) ayrışmalı.
        double ikSadeceR = 400.0 / (Math.Sqrt(3) * (xKaynak + r)) / 1000.0;
        if (Math.Abs(tali.KisaDevreAkimiKA - ikSadeceR) < 0.02)
            throw new InvalidOperationException(
                $"Reaktans zincire katılmamış görünüyor (sonuç yalnız-R aritmetik toplamla aynı: {ikSadeceR:N2} kA).");
    }

    public static void KorumaCihazi_KesmeKapasitesiVeTip()
    {
        // Cihaz tipi: ≤63A sigorta (MCB), >63A TMŞ
        if (KorumaCihaziServisi.CihazTipi(63) != "Sigorta")
            throw new InvalidOperationException("63A ve altı için cihaz tipi Sigorta olmalı.");
        if (KorumaCihaziServisi.CihazTipi(64) != "TMŞ")
            throw new InvalidOperationException("63A üstü için cihaz tipi TMŞ olmalı.");

        // MCB (≤63A): kesme 6/10 kA, üst sınır 10
        if (KorumaCihaziServisi.KesmeKapasitesiSec(16, 4) != "6kA")
            throw new InvalidOperationException("MCB, Ik=4kA için 6kA seçmeli.");
        if (KorumaCihaziServisi.KesmeKapasitesiSec(16, 8) != "10kA")
            throw new InvalidOperationException("MCB, Ik=8kA için 10kA seçmeli.");
        if (KorumaCihaziServisi.KesmeKapasitesiSec(16, 20) != "10kA")
            throw new InvalidOperationException("MCB üst sınırı 10kA olmalı (Ik=20kA'da 10kA).");

        // TMŞ (>63A): kesme 36/50/100 kA
        if (KorumaCihaziServisi.KesmeKapasitesiSec(100, 20) != "36kA")
            throw new InvalidOperationException("TMŞ, Ik=20kA için 36kA seçmeli.");
        if (KorumaCihaziServisi.KesmeKapasitesiSec(100, 40) != "50kA")
            throw new InvalidOperationException("TMŞ, Ik=40kA için 50kA seçmeli.");
        if (KorumaCihaziServisi.KesmeKapasitesiSec(100, 80) != "100kA")
            throw new InvalidOperationException("TMŞ, Ik=80kA için 100kA seçmeli.");
    }

    public static void KorumaCihazi_LinyeEntegrasyonu()
    {
        var kut = new ElektrikKutuphaneServisi();
        var proje = new ElektrikProje { SebekeBaslangicIkKA = 25 };
        var ana = AnaPanoYap(proje,
            new Linye { LinyeNo = "P1", YukTipiAd = "Priz", FazTipi = FazTipi.Monofaze, SortiSayisi = 1, BirimGucW = 2000 },    // ≤63A
            new Linye { LinyeNo = "M1", YukTipiAd = "Motor", FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 60000 });   // >63A

        HesaplaTumu(proje, kut);

        var kucuk = ana.Linyeler[0];
        var buyuk = ana.Linyeler[1];

        if (kucuk.SigortaAmper > 63) throw new InvalidOperationException($"Test kurgusu: küçük linye ≤63A olmalı (sigorta={kucuk.SigortaAmper}).");
        if (buyuk.SigortaAmper <= 63) throw new InvalidOperationException($"Test kurgusu: büyük linye >63A olmalı (sigorta={buyuk.SigortaAmper}).");

        if (kucuk.KorumaCihaziTipi != "Sigorta")
            throw new InvalidOperationException("≤63A linye Sigorta (MCB) olmalı.");
        if (kucuk.KesmeKapasitesi != "6kA" && kucuk.KesmeKapasitesi != "10kA")
            throw new InvalidOperationException($"MCB kesme kapasitesi 6/10kA olmalı, bulunan: {kucuk.KesmeKapasitesi}.");

        if (buyuk.KorumaCihaziTipi != "TMŞ")
            throw new InvalidOperationException(">63A linye TMŞ olmalı.");
        if (buyuk.KesmeKapasitesi != "36kA" && buyuk.KesmeKapasitesi != "50kA" && buyuk.KesmeKapasitesi != "100kA")
            throw new InvalidOperationException($"TMŞ kesme kapasitesi 36/50/100kA olmalı, bulunan: {buyuk.KesmeKapasitesi}.");
    }

    public static void KorumaCihazi_TmsZorlaVeManuel()
    {
        var kut = new ElektrikKutuphaneServisi();
        var proje = new ElektrikProje { SebekeBaslangicIkKA = 25 };
        var ana = AnaPanoYap(proje,
            new Linye { LinyeNo = "P1", YukTipiAd = "Priz", FazTipi = FazTipi.Monofaze, SortiSayisi = 1, BirimGucW = 2000 }); // ≤63A
        HesaplaTumu(proje, kut);
        var l = ana.Linyeler[0];
        if (l.SigortaAmper > 63) throw new InvalidOperationException($"Test kurgusu: linye ≤63A olmalı (sigorta={l.SigortaAmper}).");
        if (l.KorumaCihaziTipi != "Sigorta")
            throw new InvalidOperationException("≤63A linye varsayılan Sigorta olmalı.");

        // Kullanıcı ≤63A linyeyi TMŞ'ye zorlar → tip + kesme kapasitesi seçenekleri TMŞ setine geçer
        l.TmsZorla = true;
        HesaplaTumu(proje, kut);
        if (l.KorumaCihaziTipi != "TMŞ")
            throw new InvalidOperationException("TmsZorla ile ≤63A linye TMŞ olmalı.");
        if (l.KesmeKapasitesi != "36kA" && l.KesmeKapasitesi != "50kA" && l.KesmeKapasitesi != "100kA")
            throw new InvalidOperationException($"TMŞ'ye zorlanınca kesme kapasitesi 36/50/100kA olmalı, bulunan: {l.KesmeKapasitesi}.");
        if (l.KesmeKapasiteSecenekleri.Length != 3)
            throw new InvalidOperationException("TMŞ kesme kapasitesi seçenek listesi 3 değer içermeli.");

        // Manuel kesme kapasitesi korunur
        l.KesmeKapasitesi = "100kA";
        l.KesmeKapasitesiManuel = true;
        HesaplaTumu(proje, kut);
        if (l.KesmeKapasitesi != "100kA")
            throw new InvalidOperationException($"Manuel kesme kapasitesi (100kA) korunmalı, bulunan: {l.KesmeKapasitesi}.");

        // Sigortaya dönünce manuel TMŞ değeri (100kA) geçersiz → motor 6/10kA'ya düzeltir
        l.TmsZorla = false;
        HesaplaTumu(proje, kut);
        if (l.KorumaCihaziTipi != "Sigorta")
            throw new InvalidOperationException("TmsZorla kaldırılınca ≤63A linye Sigorta'ya dönmeli.");
        if (l.KesmeKapasitesi != "6kA" && l.KesmeKapasitesi != "10kA")
            throw new InvalidOperationException($"Sigortaya dönünce geçersiz TMŞ kesme değeri 6/10kA'ya düzeltilmeli, bulunan: {l.KesmeKapasitesi}.");
    }

    public static void CikisKoruma_TmsZorlaVeKesme()
    {
        var kut = new ElektrikKutuphaneServisi();
        var proje = new ElektrikProje { SebekeBaslangicIkKA = 25 };
        var ana = AnaPanoYap(proje, new Linye { FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 10000 });
        var tali = TaliPanoYap(proje, ana, "Tali", kabloM: 20,
            new Linye { FazTipi = FazTipi.Monofaze, SortiSayisi = 1, BirimGucW = 2000 });
        HesaplaTumu(proje, kut);

        if (tali.CikisKorumaAkimA <= 0)
            throw new InvalidOperationException("Çıkış cihazı anma akımı hesaplanmadı.");
        if (string.IsNullOrEmpty(tali.CikisKesmeKapasitesi))
            throw new InvalidOperationException("Çıkış cihazı kesme kapasitesi otomatik doldurulmadı.");
        if (tali.CikisKorumaAkimA > 63)
            throw new InvalidOperationException($"Test kurgusu: çıkış cihazı ≤63A olmalı (akım={tali.CikisKorumaAkimA}).");
        if (tali.CikisKorumaCihaziTipi != "Sigorta")
            throw new InvalidOperationException("≤63A çıkış cihazı varsayılan Sigorta olmalı.");

        // Çıkış cihazını TMŞ'ye zorla
        tali.CikisTmsZorla = true;
        HesaplaTumu(proje, kut);
        if (tali.CikisKorumaCihaziTipi != "TMŞ")
            throw new InvalidOperationException("CikisTmsZorla ile çıkış cihazı TMŞ olmalı.");
        if (tali.CikisKesmeKapasitesi != "36kA" && tali.CikisKesmeKapasitesi != "50kA" && tali.CikisKesmeKapasitesi != "100kA")
            throw new InvalidOperationException($"Çıkış TMŞ kesme kapasitesi 36/50/100kA olmalı, bulunan: {tali.CikisKesmeKapasitesi}.");

        // Manuel çıkış kesme kapasitesi korunur
        tali.CikisKesmeKapasitesi = "50kA";
        tali.CikisKesmeKapasitesiManuel = true;
        HesaplaTumu(proje, kut);
        if (tali.CikisKesmeKapasitesi != "50kA")
            throw new InvalidOperationException($"Manuel çıkış kesme kapasitesi (50kA) korunmalı, bulunan: {tali.CikisKesmeKapasitesi}.");
    }

    public static void Dxf_LinyeKorumaVeFazYansir()
    {
        var kut = new ElektrikKutuphaneServisi();
        var proje = new ElektrikProje { ProjeAdi = "DXF Koruma", SebekeBaslangicIkKA = 25 };
        var ana = AnaPanoYap(proje,
            new Linye { LinyeNo = "T1", YukTipiAd = "Motor", FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 5000, TmsZorla = true },     // ≤63A TMŞ + trifaze → RST
            new Linye { LinyeNo = "K1", YukTipiAd = "Motor", FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 9000, KontaktorluMu = true },  // kontaktör
            new Linye { LinyeNo = "D1", YukTipiAd = "Priz",  FazTipi = FazTipi.Monofaze, SortiSayisi = 1, BirimGucW = 6000, DarbeKesiciMi = true }); // >16A → darbe + kontaktör
        HesaplaTumu(proje, kut);

        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"m11_koruma_dxf_{Guid.NewGuid():N}.dxf");
        try
        {
            TekHatSemasiDxfServisi.DxfCiktiAl(proje, path, kut);
            var doc = DxfDocument.Load(path);
            var metinler = doc.Entities.Texts.Select(t => t.Value).ToList();
            bool VarMi(string p) => metinler.Any(m => m.Contains(p, StringComparison.Ordinal));

            if (!VarMi("RST"))
                throw new InvalidOperationException("DXF trifaze linyeyi RST olarak yazmalı.");
            if (!VarMi("TMŞ"))
                throw new InvalidOperationException("DXF TMŞ'ye zorlanan linyeyi TMŞ olarak yazmalı.");
            if (!VarMi("DK"))
                throw new InvalidOperationException("DXF darbe kesicili linyeyi DK işaretiyle yazmalı.");

            if (ana.Linyeler[1].Kontaktor == null)
                throw new InvalidOperationException("Test kurgusu: K1 linyesinde kontaktör olmalı.");
        }
        finally
        {
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
    }

    public static void JeneratorTrafo_KatEtiketiDxf()
    {
        var kut = new ElektrikKutuphaneServisi();
        var proje = new ElektrikProje { ProjeAdi = "Kat Test", TrafoGucKVA = 400, TrafoUkYuzde = 4 };
        var ana = AnaPanoYap(proje,
            new Linye { LinyeNo = "M1", FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 5000 });

        // Jeneratör grubu — kat "Bodrum 2"
        var senkron = new Pano { Ad = "Senkron-Ana", Kat = "Z", PanoTipi = PanoTipi.TransferPanosu };
        proje.TumPanolar.Add(senkron);
        proje.JeneratorGruplari.Add(new JeneratorGrubu
        {
            AnaPanoAd = ana.Ad,
            SenkronPanoAd = senkron.Ad,
            Kat = "Bodrum 2",
            Jeneratorler = { new Jenerator { AnaPanoAd = ana.Ad, TransferPanoAd = senkron.Ad, Kat = "Bodrum 2" } }
        });

        // OG sistemi — trafo katı "Bodrum 1"
        proje.OrtaGerilim = new OrtaGerilimSistemi { AnaPanoAd = ana.Ad, TrafoKat = "Bodrum 1" };

        HesaplaTumu(proje, kut);

        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"m11_kat_etiket_{Guid.NewGuid():N}.dxf");
        try
        {
            TekHatSemasiDxfServisi.DxfCiktiAl(proje, path, kut);
            var doc = DxfDocument.Load(path);
            var metinler = doc.Entities.Texts.Select(t => t.Value).ToList();

            if (!metinler.Any(m => m.Contains("Kat: Bodrum 2", StringComparison.Ordinal)))
                throw new InvalidOperationException("DXF jeneratör kat etiketini (Kat: Bodrum 2) yazmalı.");
            if (!metinler.Any(m => m.Contains("Kat: Bodrum 1", StringComparison.Ordinal)))
                throw new InvalidOperationException("DXF trafo kat etiketini (Kat: Bodrum 1) yazmalı.");

            // Alan round-trip: serialize → deserialize, Kat alanları korunmalı
            var json = System.Text.Json.JsonSerializer.Serialize(proje);
            var geri = System.Text.Json.JsonSerializer.Deserialize<ElektrikProje>(json)!;
            if (geri.JeneratorGruplari[0].Kat != "Bodrum 2")
                throw new InvalidOperationException("JeneratorGrubu.Kat round-trip korunmadı.");
            if (geri.OrtaGerilim!.TrafoKat != "Bodrum 1")
                throw new InvalidOperationException("OrtaGerilim.TrafoKat round-trip korunmadı.");
        }
        finally
        {
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
    }

    public static void KisaDevre_BusbarPozisyonuIkDusurur()
    {
        var proje = new ElektrikProje { SebekeBaslangicIkKA = 25.0 };
        var kut = new ElektrikKutuphaneServisi();
        var ana = AnaPanoYap(proje);

        var busbar = new BusbarHatti { Ad = "BB", UstPanoAd = ana.Ad, BusbarTipiAd = kut.BusbarTipleri[0].Ad };
        proje.TumBusbarlar.Add(busbar);
        ana.CikanBusbarlar.Add(busbar);

        Pano BusbarPano(string ad, double pozisyon)
        {
            var p = new Pano
            {
                Ad = ad,
                Kat = "1",
                PanoTipi = PanoTipi.TaliPano,
                UstPano = ana,
                UstPanoAd = ana.Ad,
                BaglantiTipi = BaglantiTipi.Busbar,
                BaglandigiBusbarId = busbar.Id,
                CikisKutusuSigortaA = 32,
                KabloMesafeM = 2
            };
            p.Linyeler.Add(new Linye { FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 5000 });
            ana.AltPanolar.Add(p);
            proje.TumPanolar.Add(p);
            busbar.CikisKutulari.Add(new BusbarCikisKutusu { BagliPanoAd = ad, PozisyonM = pozisyon, SigortaA = 32, KabloMesafeM = 2 });
            return p;
        }

        var yakin = BusbarPano("Yakın", 1);
        var uzak = BusbarPano("Uzak", 30);
        HesaplaTumu(proje, kut);

        if (uzak.KisaDevreAkimiKA >= yakin.KisaDevreAkimiKA)
            throw new InvalidOperationException($"Busbar üzerinde uzak çıkışın Ik değeri daha düşük olmalıydı (yakın={yakin.KisaDevreAkimiKA:N2}, uzak={uzak.KisaDevreAkimiKA:N2}).");
    }

    public static void Busbar_OtomatikTipIhtiyacaGoreSecilir()
    {
        var kut = new ElektrikKutuphaneServisi();
        var proje = new ElektrikProje();
        var ana = new Pano { Ad = "Ana", PanoTipi = PanoTipi.AnaPano };
        var p1 = new Pano { Ad = "P1", GirisAkimA = 180 };
        var p2 = new Pano { Ad = "P2", GirisAkimA = 170 };
        proje.TumPanolar.AddRange([ana, p1, p2]);

        var bb = new BusbarHatti
        {
            Ad = "BB",
            UstPanoAd = ana.Ad,
            BusbarTipiAd = kut.BusbarTipleri.First().Ad,
            OtomatikTipSec = true
        };
        bb.CikisKutulari.Add(new BusbarCikisKutusu { BagliPanoAd = p1.Ad, PozisyonM = 5 });
        bb.CikisKutulari.Add(new BusbarCikisKutusu { BagliPanoAd = p2.Ad, PozisyonM = 10 });

        BusbarHesapServisi.Hesapla(bb, proje, kut);

        var beklenen = kut.BusbarTipleri
            .Where(t => t.NominalAkimA >= 350)
            .OrderBy(t => t.NominalAkimA)
            .ThenBy(t => t.Malzeme, StringComparer.OrdinalIgnoreCase)
            .First();
        if (bb.BusbarTipiAd != beklenen.Ad)
            throw new InvalidOperationException($"Busbar otomatik tipi {beklenen.Ad} olmalıydı. Bulunan: {bb.BusbarTipiAd}.");
    }

    public static void Busbar_KutuPozisyonlariElleKorunur()
    {
        var vm = new TekHatSemasiViewModel();
        var bb = new BusbarHatti { Ad = "BB", ToplamUzunlukM = 30 };
        bb.CikisKutulari.Add(new BusbarCikisKutusu { BagliPanoAd = "P2", PozisyonM = 2 });
        bb.CikisKutulari.Add(new BusbarCikisKutusu { BagliPanoAd = "P1", PozisyonM = 1 });
        bb.CikisKutulari.Add(new BusbarCikisKutusu { BagliPanoAd = "P3", PozisyonM = 3 });

        vm.BusbarYenidenDuzenle(bb);

        var pozisyonlar = string.Join(",", bb.CikisKutulari.Select(k => k.PozisyonM.ToString("0.##")));
        if (pozisyonlar != "1,2,3")
            throw new InvalidOperationException($"Busbar kutuları elle girilen pozisyonları korumalıydı. Bulunan: {pozisyonlar}.");
        if (bb.ToplamUzunlukM < 30)
            throw new InvalidOperationException($"Elle verilen busbar uzunluğu korunmalıydı. Bulunan: {bb.ToplamUzunlukM:N1}m.");
    }

    public static void Busbar_KatYerlesimiYerelKonumlariMutlagaCevirir()
    {
        var proje = new ElektrikProje
        {
            Bina = new BinaKonfigurasyonu { ZeminVar = true, NormalKatSayisi = 2 }
        };
        var ana = new Pano { Ad = "Ana", Kat = "Z", PanoTipi = PanoTipi.AnaPano };
        var p1 = new Pano { Ad = "P1", Kat = "1", BaglantiTipi = BaglantiTipi.Busbar };
        var p2 = new Pano { Ad = "P2", Kat = "2", BaglantiTipi = BaglantiTipi.Busbar };
        var bb = new BusbarHatti { Ad = "BB", UstPanoAd = ana.Ad };
        p1.BaglandigiBusbarId = bb.Id;
        p2.BaglandigiBusbarId = bb.Id;
        proje.TumPanolar.AddRange([ana, p1, p2]);
        bb.CikisKutulari.Add(new BusbarCikisKutusu { BagliPanoAd = p1.Ad, KatPozisyonM = 2 });
        bb.CikisKutulari.Add(new BusbarCikisKutusu { BagliPanoAd = p2.Ad, KatPozisyonM = 1 });

        BusbarYerlesimServisi.Uygula(proje, bb,
        [
            new BusbarKatBolumu { Kat = "Z", UzunlukM = 4 },
            new BusbarKatBolumu { Kat = "1", UzunlukM = 3 },
            new BusbarKatBolumu { Kat = "2", UzunlukM = 5 }
        ]);

        var k1 = bb.CikisKutulari.First(k => k.BagliPanoAd == p1.Ad);
        var k2 = bb.CikisKutulari.First(k => k.BagliPanoAd == p2.Ad);
        if (Math.Abs(bb.ToplamUzunlukM - 12) > 0.001 ||
            Math.Abs(k1.PozisyonM - 6) > 0.001 || Math.Abs(k2.PozisyonM - 8) > 0.001)
            throw new InvalidOperationException(
                $"Kat yerleşimi hatalı: toplam={bb.ToplamUzunlukM}, P1={k1.PozisyonM}, P2={k2.PozisyonM}.");

        proje.TumBusbarlar.Add(bb);
        var json = System.Text.Json.JsonSerializer.Serialize(proje);
        var geri = System.Text.Json.JsonSerializer.Deserialize<ElektrikProje>(json)!;
        var geriBb = geri.TumBusbarlar.Single();
        if (geriBb.KatBolumleri.Count != 3 ||
            Math.Abs(geriBb.CikisKutulari.First().KatPozisyonM.GetValueOrDefault() - 2) > 0.001)
            throw new InvalidOperationException("Busbar kat yerleşimi proje kayıt/açılışında korunmalı.");
    }

    public static void Busbar_CikisKutusuSigortasiYukDegisinceGuncellenir()
    {
        var kut = new ElektrikKutuphaneServisi();
        var proje = new ElektrikProje();
        var ana = AnaPanoYap(proje);
        var bb = new BusbarHatti
        {
            Ad = "BB",
            UstPanoAd = ana.Ad,
            BusbarTipiAd = kut.BusbarTipleri.First().Ad
        };
        proje.TumBusbarlar.Add(bb);
        ana.CikanBusbarlar.Add(bb);

        var linye = new Linye
        {
            LinyeNo = "M1",
            YukTipiAd = "Motor",
            FazTipi = FazTipi.Trifaze,
            SortiSayisi = 1,
            BirimGucW = 2_000
        };
        var tali = TaliPanoYap(proje, ana, "Busbar Tali", kabloM: 2, linye);
        tali.BaglantiTipi = BaglantiTipi.Busbar;
        tali.BaglandigiBusbarId = bb.Id;
        tali.CikisKutusuSigortaA = 16;
        var kutu = new BusbarCikisKutusu
        {
            BagliPanoAd = tali.Ad,
            PozisyonM = 5,
            SigortaA = 16,
            KabloMesafeM = 2
        };
        bb.CikisKutulari.Add(kutu);

        HesaplaTumu(proje, kut);
        int ilkSigorta = tali.CikisKorumaAkimA;
        if (tali.CikisKutusuSigortaA != tali.UstPanoCikisSigortaA || kutu.SigortaA != tali.UstPanoCikisSigortaA)
            throw new InvalidOperationException("Busbar çıkış kutusu sigortası ilk hesapta pano çıkış sigortasıyla senkron değil.");

        linye.BirimGucW = 22_000;
        HesaplaTumu(proje, kut);

        if (tali.CikisKorumaAkimA <= ilkSigorta)
            throw new InvalidOperationException($"Yük artınca busbar çıkış sigortası artmalıydı. İlk={ilkSigorta}A, yeni={tali.CikisKorumaAkimA}A.");
        if (tali.CikisKutusuSigortaA != tali.UstPanoCikisSigortaA)
            throw new InvalidOperationException($"Pano busbar çıkış sigortası güncellenmedi. Kutu={tali.CikisKutusuSigortaA}A, üst çıkış={tali.UstPanoCikisSigortaA}A.");
        if (kutu.SigortaA != tali.UstPanoCikisSigortaA)
            throw new InvalidOperationException($"Busbar kutusu modeli güncellenmedi. Kutu={kutu.SigortaA}A, üst çıkış={tali.UstPanoCikisSigortaA}A.");
    }

    public static void PanoSil_BusbarReferanslariniTemizler()
    {
        var vm = new TekHatSemasiViewModel();
        vm.YeniPanoEkle("", "Z", PanoTipi.AnaPano);
        var ana = vm.Proje.KokPano!;

        // Ana panodan çıkan busbar + ondan beslenen iki tali pano
        var tip = vm.Kutuphane.BusbarTipleri.First();
        var bb = vm.BusbarEkle(ana, tip, 20);

        vm.YeniPanoEkle("Tali-A", "1", PanoTipi.TaliPano);
        var taliA = vm.Proje.TumPanolar.First(p => p.Ad == "Tali-A");
        vm.PanoyuBusbarBagla(taliA, bb, 5, 32, 2);

        vm.YeniPanoEkle("Tali-B", "1", PanoTipi.TaliPano);
        var taliB = vm.Proje.TumPanolar.First(p => p.Ad == "Tali-B");
        vm.PanoyuBusbarBagla(taliB, bb, 10, 32, 2);

        // 1) Busbar-beslemeli tek panoyu sil → yalnız onun çıkış kutusu kalkmalı, busbar durmalı
        vm.PanoSil(taliA);
        if (bb.CikisKutulari.Any(k => k.BagliPanoAd == "Tali-A"))
            throw new InvalidOperationException("Silinen busbar-beslemeli panonun çıkış kutusu busbar'da kaldı.");
        if (!vm.Proje.TumBusbarlar.Contains(bb))
            throw new InvalidOperationException("Hâlâ başka pano besleyen busbar yanlışlıkla silindi.");

        // 2) Busbar'ı besleyen ana panoyu sil → öksüz busbar tamamen kalkmalı, ağaç boşalmalı
        vm.PanoSil(ana);
        if (vm.Proje.TumBusbarlar.Contains(bb))
            throw new InvalidOperationException("Busbar'ı besleyen pano silindiğinde öksüz busbar modelde kaldı.");
        if (vm.Proje.TumPanolar.Count != 0)
            throw new InvalidOperationException($"Tüm ağaç silinmeliydi. Kalan pano sayısı: {vm.Proje.TumPanolar.Count}.");
    }

    public static void PanoSil_JeneratorUpsOgReferanslariniTemizler()
    {
        var vm = new TekHatSemasiViewModel();
        vm.YeniPanoEkle("", "Z", PanoTipi.AnaPano);
        var ana = vm.Proje.KokPano!;
        ana.Linyeler.Add(new Linye { FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 50_000 });

        // Jeneratör grubu + OG sistemi ana panoya bağlı
        vm.Proje.JeneratorGruplari.Add(new JeneratorGrubu { AnaPanoAd = ana.Ad, SenkronPanoAd = "Senkron" });
        vm.Proje.OrtaGerilim = new OrtaGerilimSistemi { AnaPanoAd = ana.Ad };
        vm.Proje.Upsler.Add(new Ups { BesleyenPanoAd = ana.Ad, KritikPanoAd = "Kritik" });

        vm.PanoSil(ana);

        if (vm.Proje.JeneratorGruplari.Any(g => g.AnaPanoAd == "Ana Pano"))
            throw new InvalidOperationException("Ana pano silindiğinde ona bağlı jeneratör grubu öksüz kaldı.");
        if (vm.Proje.Upsler.Any(u => u.BesleyenPanoAd == "Ana Pano"))
            throw new InvalidOperationException("Ana pano silindiğinde ona bağlı UPS öksüz kaldı.");
        if (vm.Proje.OrtaGerilim != null && vm.Proje.OrtaGerilim.AnaPanoAd == "Ana Pano")
            throw new InvalidOperationException("Ana pano silindiğinde OG ana pano referansı temizlenmedi.");
    }

    public static void Jenerator_LokalBolgeVeTesisGeneliBirlikte()
    {
        var vm = new TekHatSemasiViewModel();
        vm.YeniPanoEkle("", "Z", PanoTipi.AnaPano);
        var ana = vm.Proje.KokPano!;
        ana.Linyeler.Add(new Linye { YukTipiAd = "Priz", FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 50_000 });

        vm.YeniPanoEkle("Bolge-1", "1", PanoTipi.TaliPano);
        var bolge = vm.Proje.TumPanolar.First(p => p.Ad == "Bolge-1");
        bolge.Linyeler.Add(new Linye { YukTipiAd = "Priz", FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 10_000 });

        // Tesis geneli (ana pano) + lokal bölge (tali pano) jeneratörleri aynı projede birlikte
        vm.JeneratorGrubuUygula(ana, JeneratorMod.Standby, 1.25, Array.Empty<JeneratorTanim>());
        vm.JeneratorGrubuUygula(bolge, JeneratorMod.Standby, 1.25, Array.Empty<JeneratorTanim>());

        if (vm.Proje.JeneratorGruplari.Count != 2)
            throw new InvalidOperationException($"İki jeneratör grubu beklenirdi. Bulunan: {vm.Proje.JeneratorGruplari.Count}.");

        var tesis = vm.Proje.JeneratorGruplari.First(g => g.AnaPanoAd == ana.Ad);
        var lokal = vm.Proje.JeneratorGruplari.First(g => g.AnaPanoAd == bolge.Ad);

        if (tesis.BeslemeKapsami != JeneratorBeslemeKapsami.TesisGeneli)
            throw new InvalidOperationException("Ana panoya bağlı grup tesis geneli olmalıydı.");
        if (lokal.BeslemeKapsami != JeneratorBeslemeKapsami.LokalBolge)
            throw new InvalidOperationException("Tali panoya bağlı grup lokal bölge olmalıydı.");

        // Lokal jeneratör yalnız kendi bölgesinin yüküne göre boyutlanmalı (tesis genelinden küçük)
        if (!(lokal.HesaplananYukKW < tesis.HesaplananYukKW))
            throw new InvalidOperationException(
                $"Lokal yük ({lokal.HesaplananYukKW:N1} kW) tesis geneli yükünden ({tesis.HesaplananYukKW:N1} kW) küçük olmalıydı.");
        if (Math.Abs(lokal.HesaplananYukKW - bolge.ToplamGucW / 1000.0) > 0.5)
            throw new InvalidOperationException(
                $"Lokal jeneratör bölge yüküne ({bolge.ToplamGucW / 1000.0:N1} kW) göre boyutlanmalıydı. Bulunan: {lokal.HesaplananYukKW:N1} kW.");
    }

    public static void Jenerator_BeslemeKapsamiRoundTrip()
    {
        var vm = new TekHatSemasiViewModel();
        vm.YeniPanoEkle("", "Z", PanoTipi.AnaPano);
        vm.YeniPanoEkle("Bolge-1", "1", PanoTipi.TaliPano);
        var bolge = vm.Proje.TumPanolar.First(p => p.Ad == "Bolge-1");
        bolge.Linyeler.Add(new Linye { FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 8_000 });
        vm.JeneratorGrubuUygula(bolge, JeneratorMod.Standby, 1.25, Array.Empty<JeneratorTanim>());

        var opts = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
        };
        var json = System.Text.Json.JsonSerializer.Serialize(vm.Proje, opts);
        var geri = System.Text.Json.JsonSerializer.Deserialize<ElektrikProje>(json, opts);

        var grup = geri?.JeneratorGruplari.FirstOrDefault(g => g.AnaPanoAd == "Bolge-1");
        if (grup == null) throw new InvalidOperationException("Lokal jeneratör grubu round-trip'te kayboldu.");
        if (grup.BeslemeKapsami != JeneratorBeslemeKapsami.LokalBolge)
            throw new InvalidOperationException("BeslemeKapsami round-trip'te korunmadı.");
    }

    public static void Jenerator_KabloMesafeleriRoundTripDxfVeExcel()
    {
        var vm = new TekHatSemasiViewModel();
        vm.YeniPanoEkle("", "Z", PanoTipi.AnaPano);
        var ana = vm.Proje.KokPano!;
        ana.Linyeler.Add(new Linye
        {
            LinyeNo = "M1", YukTipiAd = "Mekanik", FazTipi = FazTipi.Trifaze,
            SortiSayisi = 1, BirimGucW = 80_000, KabloMesafeM = 20
        });
        var jeneratörler = vm.JeneratorTipleri.Take(2).ToList();
        if (jeneratörler.Count < 2)
            throw new InvalidOperationException("Kablo mesafesi testi için en az iki jeneratör tanımı gerekli.");

        vm.JeneratorGrubuUygula(
            ana, JeneratorMod.Standby, 1.25, jeneratörler,
            jeneratorSenkronKabloMesafeleri: new[] { 12.5, 27.0 },
            senkronHedefPanoKabloMesafeM: 42.0);

        var grup = vm.JeneratorGrubuBul(ana)
            ?? throw new InvalidOperationException("Jeneratör grubu oluşturulamadı.");
        if (Math.Abs(grup.Jeneratorler[0].SenkronPanoKabloMesafeM - 12.5) > 0.001 ||
            Math.Abs(grup.Jeneratorler[1].SenkronPanoKabloMesafeM - 27.0) > 0.001 ||
            Math.Abs(grup.SenkronHedefPanoKabloMesafeM - 42.0) > 0.001)
            throw new InvalidOperationException("Jeneratör kablo mesafeleri grup modeline doğru yazılmalı.");

        var opts = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
        };
        var json = System.Text.Json.JsonSerializer.Serialize(vm.Proje, opts);
        var geri = System.Text.Json.JsonSerializer.Deserialize<ElektrikProje>(json, opts);
        var geriGrup = geri?.JeneratorGruplari.FirstOrDefault()
            ?? throw new InvalidOperationException("Jeneratör grubu round-trip'te kayboldu.");
        if (Math.Abs(geriGrup.Jeneratorler[0].SenkronPanoKabloMesafeM - 12.5) > 0.001 ||
            Math.Abs(geriGrup.Jeneratorler[1].SenkronPanoKabloMesafeM - 27.0) > 0.001 ||
            Math.Abs(geriGrup.SenkronHedefPanoKabloMesafeM - 42.0) > 0.001)
            throw new InvalidOperationException("Jeneratör kablo mesafeleri JSON round-trip'te korunmalı.");

        var dxfPath = Path.Combine(Path.GetTempPath(), $"m11_jen_mesafe_{Guid.NewGuid():N}.dxf");
        var xlsxPath = Path.Combine(Path.GetTempPath(), $"m11_jen_mesafe_{Guid.NewGuid():N}.xlsx");
        try
        {
            TekHatSemasiDxfServisi.DxfCiktiAl(vm.Proje, dxfPath, vm.Kutuphane);
            var dxf = File.ReadAllText(dxfPath);
            if (!dxf.Contains("L=12.5m") || !dxf.Contains("L=27m") || !dxf.Contains("L=42m"))
                throw new InvalidOperationException("Jeneratör kablo mesafeleri DXF etiketlerine yazılmalı.");

            TekHatSemasiExcelServisi.RaporOlustur(vm.Proje, xlsxPath);
            using var workbook = new ClosedXML.Excel.XLWorkbook(xlsxPath);
            var senkronSayfa = workbook.Worksheet(grup.SenkronPanoAd);
            var jen1 = senkronSayfa.RowsUsed().First(r => r.Cell(7).GetString() == "JEN-1");
            var jen2 = senkronSayfa.RowsUsed().First(r => r.Cell(7).GetString() == "JEN-2");
            var senkron = senkronSayfa.RowsUsed().First(r => r.Cell(7).GetString() == "SENKRON");
            if (Math.Abs(jen1.Cell(13).GetDouble() - 12.5) > 0.001 ||
                Math.Abs(jen2.Cell(13).GetDouble() - 27.0) > 0.001 ||
                Math.Abs(senkron.Cell(13).GetDouble() - 42.0) > 0.001)
                throw new InvalidOperationException("Jeneratör kablo mesafeleri Excel raporuna yazılmalı.");
            if (jen1.Cell(12).GetString() != grup.Jeneratorler[0].SenkronKabloKesiti ||
                jen2.Cell(12).GetString() != grup.Jeneratorler[1].SenkronKabloKesiti ||
                senkron.Cell(12).GetString() != grup.SenkronHedefKabloKesiti)
                throw new InvalidOperationException("Jeneratör kablo kesitleri Excel raporuna yazılmalı.");
        }
        finally
        {
            if (File.Exists(dxfPath)) File.Delete(dxfPath);
            if (File.Exists(xlsxPath)) File.Delete(xlsxPath);
        }
    }

    public static void Jenerator_YuksekAkimdaParalelKabloAtar()
    {
        var kut = new ElektrikKutuphaneServisi();
        kut.Jeneratorler.Clear();
        kut.Jeneratorler.Add(new JeneratorTanim
        {
            Ad = "Test 880 kVA",
            Marka = "Test",
            Model = "880",
            PrimeGucKVA = 800,
            PrimeGucKW = 640,
            StandbyGucKVA = 880,
            StandbyGucKW = 704,
            GerilimV = 400
        });

        var ana = new Pano
        {
            Ad = "Ana Pano",
            PanoTipi = PanoTipi.AnaPano,
            ToplamGucW = 1_000_000,
            ToplamGorunurVA = 1_200_000,
            GirisAkimA = 1_800
        };
        var proje = new ElektrikProje { CosF = 0.85, IzinVerilenGerilimDususYuzde = 3 };
        var grup = new JeneratorGrubu
        {
            AnaPanoAd = ana.Ad,
            SenkronPanoAd = "Senkron Pano",
            Mod = JeneratorMod.Standby,
            EmniyetFaktoru = 1.0,
            SenkronHedefPanoKabloMesafeM = 25,
            Jeneratorler = new List<Jenerator>
            {
                new() { JeneratorTanimAd = "Test 880 kVA", SenkronPanoKabloMesafeM = 75 },
                new() { JeneratorTanimAd = "Test 880 kVA", SenkronPanoKabloMesafeM = 10 }
            }
        };

        JeneratorHesapServisi.Hesapla(grup, ana, proje, kut);

        if (grup.Jeneratorler.Any(j => j.SenkronKabloParalelKolSayisi != 3))
            throw new InvalidOperationException(
                "880 kVA jeneratör hattı tek 240 mm² kabloda kalmamalı; 3 paralel kol beklenirdi.");
        if (grup.Jeneratorler.Any(j => j.SenkronKabloKesiti != "3x(4x240) mm²"))
            throw new InvalidOperationException("880 kVA ünite kablo etiketi paralel kol sayısını göstermeli.");
        if (grup.SenkronHedefKabloParalelKolSayisi != 5 ||
            grup.SenkronHedefKabloKesiti != "5x(4x240) mm²")
            throw new InvalidOperationException(
                $"İki 880 kVA ünitenin ortak hattında 5 paralel kol beklenirdi; bulunan: {grup.SenkronHedefKabloKesiti}.");

        double uniteKolAkimi = grup.Jeneratorler[0].AnmaAkimA /
            grup.Jeneratorler[0].SenkronKabloParalelKolSayisi;
        double ortakKolAkimi = grup.ToplamAnmaAkimA / grup.SenkronHedefKabloParalelKolSayisi;
        if (uniteKolAkimi > JeneratorHesapServisi.Jenerator240Mm2AkimKapasitesiA + 0.001 ||
            ortakKolAkimi > JeneratorHesapServisi.Jenerator240Mm2AkimKapasitesiA + 0.001)
            throw new InvalidOperationException("Paralel kollardan birindeki akım 240 mm² için 564 A sınırını aşmamalı.");
    }

    // ── Jeneratör ──

    public static void Jenerator_StandbyModUygunSecer()
    {
        var kut = new ElektrikKutuphaneServisi();
        if (kut.Jeneratorler.Count == 0) return; // kütüphane boşsa atla
        double yukKW = 50, emniyet = 1.25;
        var jen = JeneratorHesapServisi.UygunJeneratorSec(yukKW, JeneratorMod.Standby, emniyet, kut);
        if (jen == null)
            throw new InvalidOperationException("Standby modunda jeneratör seçilemedi.");
        double gerekli = yukKW * emniyet;
        bool yeterli = jen.StandbyGucKW >= gerekli;
        bool enBuyuk = jen == kut.Jeneratorler.OrderByDescending(j => j.StandbyGucKW).First();
        if (!yeterli && !enBuyuk)
            throw new InvalidOperationException($"Seçilen jeneratör yükü karşılamalı ya da en büyüğü olmalı (StandbyKW={jen.StandbyGucKW}, gerekli={gerekli}).");
    }

    public static void Jenerator_KvaYetersizTipiUygunSaymaz()
    {
        var kut = new ElektrikKutuphaneServisi();
        kut.Jeneratorler.Clear();
        kut.Jeneratorler.Add(new JeneratorTanim { Ad = "KW yeter KVA yetersiz", StandbyGucKW = 100, StandbyGucKVA = 100, PrimeGucKW = 100, PrimeGucKVA = 100, GerilimV = 400 });
        kut.Jeneratorler.Add(new JeneratorTanim { Ad = "KW KVA yeterli", StandbyGucKW = 110, StandbyGucKVA = 200, PrimeGucKW = 110, PrimeGucKVA = 200, GerilimV = 400 });

        var ana = new Pano { Ad = "Ana", PanoTipi = PanoTipi.AnaPano, ToplamGucW = 80000, ToplamGorunurVA = 160000, GirisAkimA = 120 };
        var jen = new Jenerator { Mod = JeneratorMod.Standby, EmniyetFaktoru = 1.0 };
        JeneratorHesapServisi.Hesapla(jen, ana, new ElektrikProje(), kut);

        if (jen.JeneratorTanimAd != "KW KVA yeterli")
            throw new InvalidOperationException($"Jeneratör seçiminde kVA kriteri de aranmalıydı. Seçilen: {jen.JeneratorTanimAd}");
        if (!jen.YukYeterliMi)
            throw new InvalidOperationException("kW ve kVA kriterlerini sağlayan jeneratör yeterli işaretlenmeliydi.");
    }

    public static void TransferSalter_YuksekAkimda1250UstuSecer()
    {
        var kut = new ElektrikKutuphaneServisi();
        var salter = JeneratorHesapServisi.UygunTransferSalterSec(anaPanoAkimA: 2100, jeneratorAnaAkimA: 1900, kut);

        if (salter == null)
            throw new InvalidOperationException("Yüksek akım için transfer şalteri seçilmeli.");
        if (salter.AnmaAkimA < 2100)
            throw new InvalidOperationException($"2100A hedef için 1250A'da kalmamalı; seçilen: {salter.AnmaAkimA}A.");
        if (salter.AnmaAkimA != 2500)
            throw new InvalidOperationException($"2100A hedef için en küçük yeterli standart 2500A beklenirdi, seçilen: {salter.AnmaAkimA}A.");
    }

    public static void TransferSalter_OtomatikYukleGuncellenirManuelKorunur()
    {
        var vm = new TekHatSemasiViewModel();
        vm.YeniPanoEkle("", "Z", PanoTipi.AnaPano);
        var ana = vm.Proje.KokPano!;
        ana.Linyeler.Add(new Linye { YukTipiAd = "Priz", FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 20_000 });

        // Otomatik transfer şalteri (salter parametresi geçilmedi → "(otomatik tayin)").
        vm.JeneratorGrubuUygula(ana, JeneratorMod.Standby, 1.25, Array.Empty<JeneratorTanim>());
        var grup = vm.Proje.JeneratorGruplari.First();
        if (grup.TransferSalterManuel)
            throw new InvalidOperationException("Otomatik seçimde manuel bayrağı false olmalıydı.");
        string ilkSalter = grup.TransferSalterAd;
        double ilkAkim = ana.GirisAkimA;
        var ilkTanim = vm.TransferSalterTipleri.FirstOrDefault(t => t.Ad == ilkSalter);

        // Yük büyük ölçüde artar → ana pano akımı büyür → otomatik transfer şalteri de güncellenmeli.
        ana.Linyeler.Add(new Linye { YukTipiAd = "Priz", FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 400_000 });
        vm.TumunuHesapla();

        if (!(ana.GirisAkimA > ilkAkim))
            throw new InvalidOperationException("Yük artınca ana pano akımı büyümeliydi (test kurulumu hatalı).");
        if (grup.TransferSalterAd == ilkSalter)
            throw new InvalidOperationException("Yük büyük ölçüde artınca otomatik transfer şalteri güncellenmeliydi.");
        var yeniTanim = vm.TransferSalterTipleri.FirstOrDefault(t => t.Ad == grup.TransferSalterAd);
        if (yeniTanim == null)
            throw new InvalidOperationException("Yük artışı sonrası transfer şalteri seçilememiş.");
        if (ilkTanim != null && yeniTanim.AnmaAkimA < ilkTanim.AnmaAkimA)
            throw new InvalidOperationException("Yük artınca transfer şalteri küçülmemeliydi.");
        // Ünitelere de yansımalı.
        if (grup.Jeneratorler.Any(j => j.TransferSalterAd != grup.TransferSalterAd))
            throw new InvalidOperationException("Güncellenen şalter jeneratör ünitelerine yansımadı.");

        // Elle seçim yük artsa da korunmalı.
        var manuelTanim = vm.TransferSalterTipleri.OrderBy(t => t.AnmaAkimA).First();
        vm.JeneratorTransferSalteriAyarla(manuelTanim);
        if (!grup.TransferSalterManuel)
            throw new InvalidOperationException("Elle seçimde manuel bayrağı true olmalıydı.");
        ana.Linyeler.Add(new Linye { YukTipiAd = "Priz", FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 200_000 });
        vm.TumunuHesapla();
        if (grup.TransferSalterAd != manuelTanim.Ad)
            throw new InvalidOperationException("Manuel transfer şalteri yük artsa da korunmalıydı.");

        // "(otomatik tayin)"e dönülünce yeniden otomatik seçilir.
        vm.JeneratorTransferSalteriAyarla(null);
        if (grup.TransferSalterManuel)
            throw new InvalidOperationException("Otomatik tayine dönünce manuel bayrağı false olmalıydı.");
        if (grup.TransferSalterAd == manuelTanim.Ad && vm.TransferSalterTipleri.Count > 1)
            throw new InvalidOperationException("Otomatik tayine dönünce şalter yeniden seçilmeliydi.");
    }

    public static void TransferSalterKutuphane_EskiDosyayaYuksekAkimVarsayilanlariEklenir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "e4p-transfer-kut-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "TransferSalterKutuphanesi.json"),
                """
                [
                  { "Ad": "Kullanıcı ATS 1250", "Marka": "Özel", "Model": "ATS", "AnmaAkimA": 1250, "KutupSayisi": 4, "GerilimV": 400, "Tipi": "ATS", "TransferSuresiMs": 250, "KontrolTipi": "Mikroişlemci" }
                ]
                """);

            var kut = new ElektrikKutuphaneServisi(dir);
            if (!kut.TransferSalterleri.Any(t => t.Ad == "Kullanıcı ATS 1250"))
                throw new InvalidOperationException("Kullanıcı transfer şalteri korunmalı.");
            if (!kut.TransferSalterleri.Any(t => t.Tipi == "ATS" && t.AnmaAkimA >= 6300))
                throw new InvalidOperationException("Eski transfer şalteri kütüphanesine 6300A ATS varsayılanı eklenmeli.");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    public static void JeneratorGrubu_FarkliGucleriSenkronPanodaToplar()
    {
        var kut = new ElektrikKutuphaneServisi();
        kut.Jeneratorler.Clear();
        kut.Jeneratorler.Add(new JeneratorTanim { Ad = "Küçük 100kVA", StandbyGucKW = 80, StandbyGucKVA = 100, PrimeGucKW = 72, PrimeGucKVA = 90, GerilimV = 400, YakitTuketimLh100 = 20 });
        kut.Jeneratorler.Add(new JeneratorTanim { Ad = "Büyük 200kVA", StandbyGucKW = 160, StandbyGucKVA = 200, PrimeGucKW = 145, PrimeGucKVA = 180, GerilimV = 400, YakitTuketimLh100 = 40 });

        var ana = new Pano
        {
            Ad = "Ana",
            PanoTipi = PanoTipi.AnaPano,
            ToplamGucW = 180_000,
            ToplamGorunurVA = 250_000,
            GirisAkimA = 360
        };
        var proje = new ElektrikProje { KokPano = ana };
        proje.TumPanolar.Add(ana);
        var grup = new JeneratorGrubu
        {
            AnaPanoAd = ana.Ad,
            SenkronPanoAd = "Senkron-Ana",
            Mod = JeneratorMod.Standby,
            EmniyetFaktoru = 1.0,
            Jeneratorler = new()
            {
                new Jenerator { JeneratorTanimAd = "Küçük 100kVA" },
                new Jenerator { JeneratorTanimAd = "Büyük 200kVA" }
            }
        };
        proje.JeneratorGruplari.Add(grup);

        JeneratorHesapServisi.Hesapla(grup, ana, proje, kut);

        if (Math.Abs(grup.ToplamGucKVA - 300) > 0.01 || Math.Abs(grup.ToplamGucKW - 240) > 0.01)
            throw new InvalidOperationException($"Senkron grup farklı güçte jeneratörleri toplamalıydı (bulunan {grup.ToplamGucKW:N0}kW/{grup.ToplamGucKVA:N0}kVA).");
        if (!grup.YukYeterliMi)
            throw new InvalidOperationException("80kW + 160kW grup 180kW/250kVA yükü karşılamalıydı.");
        if (grup.Jeneratorler[1].HesaplananYukKW <= grup.Jeneratorler[0].HesaplananYukKW)
            throw new InvalidOperationException("Büyük jeneratör yük paylaşımında küçük jeneratörden daha fazla pay almalıydı.");
        if (grup.YakitTuketimLh <= 0)
            throw new InvalidOperationException("Senkron grup toplam yakıt tüketimi hesaplanmalıydı.");
    }

    public static void JeneratorGrubu_EskiTekJeneratorGrubaTasinir()
    {
        var proje = new ElektrikProje();
        var ana = AnaPanoYap(proje, new Linye { FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 50_000 });
        var transfer = new Pano { Ad = "Transfer-Ana Pano", Kat = "Z", PanoTipi = PanoTipi.TransferPanosu };
        proje.TumPanolar.Add(transfer);
        proje.Jenerator = new Jenerator
        {
            AnaPanoAd = ana.Ad,
            TransferPanoAd = transfer.Ad,
            JeneratorTanimAd = "Aksa APD-110",
            Mod = JeneratorMod.Standby,
            EmniyetFaktoru = 1.25
        };

        var vm = new TekHatSemasiViewModel();
        vm.ProjeyiYukle(proje);

        var grup = vm.AktifJeneratorGrubu;
        if (grup == null)
            throw new InvalidOperationException("Eski tek jeneratör alanı açılışta JeneratorGrubu'na taşınmalıydı.");
        if (grup.Jeneratorler.Count != 1 || grup.Jeneratorler[0].JeneratorTanimAd != "Aksa APD-110")
            throw new InvalidOperationException("Eski jeneratör seçimi grup içinde tek ünite olarak korunmalıydı.");
        if (grup.SenkronPanoAd != transfer.Ad)
            throw new InvalidOperationException("Eski transfer panosu senkron pano olarak korunmalıydı.");
    }

    public static void JeneratorKutuphane_AksaEmsaArkenAlimarVarsayilanlariGelir()
    {
        var kut = new ElektrikKutuphaneServisi();
        JeneratorMarkalariniDogrula(kut);

        if (kut.Jeneratorler.Max(j => j.StandbyGucKVA) < 2200)
            throw new InvalidOperationException("Jeneratör kütüphanesi büyük projeler için en az 2200 kVA standby kademesine kadar uzanmalı.");
    }

    public static void JeneratorKutuphane_EskiDosyayaYeniMarkalarBirlestirilir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "e4p-jenerator-kut-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "JeneratorKutuphanesi.json"),
                "[{\"Ad\":\"Kullanıcı Özel Jen\",\"Marka\":\"Özel\",\"Model\":\"X1\",\"PrimeGucKVA\":75,\"PrimeGucKW\":60,\"StandbyGucKVA\":82.5,\"StandbyGucKW\":66,\"GerilimV\":400}]");

            var kut = new ElektrikKutuphaneServisi(dir);
            if (!kut.Jeneratorler.Any(j => j.Ad == "Kullanıcı Özel Jen"))
                throw new InvalidOperationException("Eski/kullanıcı jeneratör kaydı korunmalıydı.");

            JeneratorMarkalariniDogrula(kut);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    private static void JeneratorMarkalariniDogrula(ElektrikKutuphaneServisi kut)
    {
        foreach (var marka in new[] { "Aksa", "Emsa", "Arken", "Alimar" })
        {
            if (!kut.Jeneratorler.Any(j => string.Equals(j.Marka, marka, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"{marka} jeneratörleri kütüphanede bulunmalı.");
        }
    }

    // ── Reaktans (O3) ──

    public static void Kablo_ReaktansGerilimDususuneEklenir()
    {
        // Aynı kesitte reaktans dahil düşüm, yalnız-R düşümünden büyük olmalı (endüktif yük)
        double rOnly = KabloHesapServisi.BakirOzdirenc * 100 / 50 * 0.85 * Math.Sqrt(3) * 200; // ~R·cosφ terimi
        double tam = KabloHesapServisi.GerilimDususV(200, 100, 50, FazTipi.Trifaze, 0.85);
        // tam = √3·I·(R·cosφ + X·sinφ) ; X·sinφ > 0 olduğundan tam > sadece-R bileşeni
        double sadeceR = Math.Sqrt(3) * 200 * (KabloHesapServisi.BakirOzdirenc * 100 / 50 * 0.85);
        if (tam <= sadeceR)
            throw new InvalidOperationException($"Reaktans gerilim düşümünü artırmalı (tam={tam:N2}V, R-only={sadeceR:N2}V).");
    }

    public static void Kablo_GerilimDususuSinifIcinde()
    {
        // Reaktans dahil seçilen kesit yine de izin sınırını aşmamalı
        var r = KabloHesapServisi.KesitHesapla(30, 150, FazTipi.Trifaze, 400, 3.0, 0.85);
        if (r.GercekDusumYuzde > 3.01)
            throw new InvalidOperationException($"Seçilen kesit reaktans dahil %3 sınırını aşmamalı: %{r.GercekDusumYuzde:N2}.");
    }

    // ── Reaktör akort (S4) ──

    public static void Kompanzasyon_AkortYuzdesiReaktorSecer()
    {
        var proje = new ElektrikProje();
        var kut = new ElektrikKutuphaneServisi();
        var ana = AnaPanoYap(proje, new Linye { FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 40000 });
        HesaplaTumu(proje, kut);

        var sonuc = new KompanzasyonSonucu { ReaktorAktif = true, ReaktorAkortYuzde = 14.0 };
        sonuc = KompanzasyonHesapServisi.Hesapla(ana, proje, kut, sonuc);
        if (sonuc.ReaktorKademeleri.Count != 3)
            throw new InvalidOperationException($"Reaktör aktifken 3 kademe beklenir, bulunan: {sonuc.ReaktorKademeleri.Count}.");

        // Kütüphanede 14'e en yakın akort varsa seçilen reaktörün akortu 7'den çok 14'e yakın olmalı
        var secilenAd = sonuc.ReaktorKademeleri[0].ReaktorAd;
        var reaktor = kut.Reaktorler.FirstOrDefault(r => r.Ad == secilenAd);
        if (reaktor == null) return; // kütüphane verisi yoksa atla
        bool varyans14 = kut.Reaktorler.Any(r => Math.Abs(r.AkortYuzde - 14.0) < Math.Abs(r.AkortYuzde - 7.0));
        if (varyans14 && Math.Abs(reaktor.AkortYuzde - 14.0) > Math.Abs(reaktor.AkortYuzde - 7.0))
            throw new InvalidOperationException($"Akort %14 hedeflenirken 14'e yakın reaktör seçilmeliydi (seçilen akort: %{reaktor.AkortYuzde}).");
    }

    // ── Eşzamanlılık / diversite faktörü ──

    public static void Diversite_AltPanoGucunuAzaltir()
    {
        var kut = new ElektrikKutuphaneServisi();

        // ks = 1.0 (tam): ana toplam = kendi + tali tam
        var projeTam = new ElektrikProje();
        var anaTam = AnaPanoYap(projeTam, new Linye { FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 5000 });
        TaliPanoYap(projeTam, anaTam, "Tali", 20, new Linye { FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 10000 });
        HesaplaTumu(projeTam, kut);
        double tam = anaTam.ToplamGucW;

        // ks = 0.6: tali katkısı %60'a düşer → ana toplam azalır
        var projeKs = new ElektrikProje();
        var anaKs = AnaPanoYap(projeKs, new Linye { FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 5000 });
        var taliKs = TaliPanoYap(projeKs, anaKs, "Tali", 20, new Linye { FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 10000 });
        taliKs.EszamanlilikFaktoru = 0.6;
        HesaplaTumu(projeKs, kut);
        double azaltilmis = anaKs.ToplamGucW;

        // tam = 5000 + 10000 = 15000 ; azaltılmış = 5000 + 6000 = 11000
        if (azaltilmis >= tam)
            throw new InvalidOperationException($"Diversite (ks=0.6) ana pano gücünü azaltmalı (tam={tam:N0}, ks={azaltilmis:N0}).");
        if (Math.Abs(azaltilmis - 11000) > 1.0)
            throw new InvalidOperationException($"ks=0.6 ile ana toplam 11000 W beklenir, bulunan: {azaltilmis:N0}.");
    }

    // ── DXF çıktısı (K2) ──

    public static void DxfCikti_GecerliDosyaUretir()
    {
        var proje = new ElektrikProje { ProjeAdi = "DXF Test", TrafoGucKVA = 400, TrafoUkYuzde = 4 };
        var kut = new ElektrikKutuphaneServisi();
        var ana = AnaPanoYap(proje,
            new Linye { LinyeNo = "P1", FazTipi = FazTipi.Monofaze, SortiSayisi = 6, BirimGucW = 300, KabloMesafeM = 15, Aciklama = "Priz aciklamasi" },
            new Linye { LinyeNo = "M1", FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 4000, KabloMesafeM = 20 });
        TaliPanoYap(proje, ana, "Kat 1 Panosu", kabloM: 30,
            new Linye { LinyeNo = "A1", FazTipi = FazTipi.Monofaze, SortiSayisi = 8, BirimGucW = 40, KabloMesafeM = 12 });
        // Jeneratör + transfer panosu
        var transfer = new Pano { Ad = "Transfer-Ana Pano", Kat = "Z", PanoTipi = PanoTipi.TransferPanosu };
        proje.TumPanolar.Add(transfer);
        proje.Jenerator = new Jenerator { AnaPanoAd = ana.Ad, TransferPanoAd = transfer.Ad, Mod = JeneratorMod.Standby };
        HesaplaTumu(proje, kut);
        if (proje.Jenerator != null)
            JeneratorHesapServisi.Hesapla(proje.Jenerator, ana, proje, kut);

        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"m11_dxf_test_{Guid.NewGuid():N}.dxf");
        try
        {
            TekHatSemasiDxfServisi.DxfCiktiAl(proje, path);
            if (!System.IO.File.Exists(path))
                throw new InvalidOperationException("DXF dosyası oluşturulmadı.");
            var icerik = System.IO.File.ReadAllText(path);
            if (icerik.Length < 100 || !icerik.Contains("SECTION") || !icerik.Contains("EOF"))
                throw new InvalidOperationException("Üretilen DXF geçerli bir AutoCAD belgesi değil.");
            if (!icerik.Contains("M11-PANO") || !icerik.Contains("M11-BUSBAR"))
                throw new InvalidOperationException("DXF katmanları yazılmadı.");
            if (!icerik.Contains("Priz aciklamasi"))
                throw new InvalidOperationException("DXF linye açıklamasını yazmadı.");
        }
        finally
        {
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
    }

    public static void DxfCikti_JeneratorleriGorunurYazar()
    {
        var proje = new ElektrikProje { ProjeAdi = "DXF Jenerator Test", TrafoGucKVA = 400, TrafoUkYuzde = 4 };
        var kut = new ElektrikKutuphaneServisi();
        var ana = AnaPanoYap(proje,
            new Linye { LinyeNo = "M1", FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 45000, KabloMesafeM = 20 });

        proje.JeneratorGruplari.Add(new JeneratorGrubu
        {
            // Eski/kopuk proje referansı: hesap fallback yapabiliyor; DXF de sessizce atlamamalı.
            AnaPanoAd = "Eski Ana Pano Adi",
            SenkronPanoAd = "Senkron-Ana",
            Mod = JeneratorMod.Standby,
            EmniyetFaktoru = 1.25
        });

        HesaplaTumu(proje, kut);

        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"m11_dxf_jenerator_{Guid.NewGuid():N}.dxf");
        try
        {
            TekHatSemasiDxfServisi.DxfCiktiAl(proje, path, kut);
            var doc = DxfDocument.Load(path);

            var jenCizgiler = doc.Entities.Lines.Count(l => l.Layer.Name == "M11-JEN");
            var jenDaireler = doc.Entities.Circles.Count(c => c.Layer.Name == "M11-JEN");
            var jenMetinler = doc.Entities.Texts
                .Where(t => t.Layer.Name == "M11-JEN")
                .Select(t => t.Value)
                .ToList();

            if (jenCizgiler == 0 || jenDaireler == 0)
                throw new InvalidOperationException($"DXF jeneratör geometrisini M11-JEN katmanına yazmalı (çizgi={jenCizgiler}, daire={jenDaireler}).");
            if (!jenMetinler.Any(t => t.Contains("JENERATOR", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("DXF jeneratör başlığını yazmalı.");
            if (!jenMetinler.Any(t => t.Contains("Senkron", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("DXF senkron pano etiketini yazmalı.");
            if (!jenMetinler.Any(t => t.Contains("ESP", StringComparison.OrdinalIgnoreCase) ||
                                      t.Contains("PRP", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("DXF jeneratör çalışma modu etiketini yazmalı.");
        }
        finally
        {
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
    }

    public static void DxfCikti_CokluBusbarlariAyriYazar()
    {
        var proje = new ElektrikProje { ProjeAdi = "DXF Busbar Test" };
        var kut = new ElektrikKutuphaneServisi();
        var ana = AnaPanoYap(proje);

        for (int i = 1; i <= 3; i++)
        {
            var busbar = new BusbarHatti
            {
                Ad = $"Busbar-{i}",
                UstPanoAd = ana.Ad,
                BusbarTipiAd = kut.BusbarTipleri[0].Ad,
                ToplamUzunlukM = 10 + i
            };
            proje.TumBusbarlar.Add(busbar);
            ana.CikanBusbarlar.Add(busbar);

            var pano = new Pano
            {
                Ad = $"BB Pano {i}",
                Kat = "Z",
                PanoTipi = PanoTipi.TaliPano,
                UstPano = ana,
                UstPanoAd = ana.Ad,
                BaglantiTipi = BaglantiTipi.Busbar,
                BaglandigiBusbarId = busbar.Id,
                CikisKutusuSigortaA = 40,
                KabloMesafeM = 2
            };
            pano.Linyeler.Add(new Linye { LinyeNo = "P1", FazTipi = FazTipi.Monofaze, SortiSayisi = 4, BirimGucW = 300 });
            ana.AltPanolar.Add(pano);
            proje.TumPanolar.Add(pano);
            busbar.CikisKutulari.Add(new BusbarCikisKutusu { BagliPanoAd = pano.Ad, PozisyonM = i * 5, SigortaA = 40, KabloMesafeM = 2 });
        }

        HesaplaTumu(proje, kut);

        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"m11_dxf_busbar_test_{Guid.NewGuid():N}.dxf");
        try
        {
            TekHatSemasiDxfServisi.DxfCiktiAl(proje, path);
            var icerik = System.IO.File.ReadAllText(path);
            for (int i = 1; i <= 3; i++)
            {
                if (!icerik.Contains($"Busbar-{i}"))
                    throw new InvalidOperationException($"DXF Busbar-{i} etiketini yazmadı.");
            }

            var doc = DxfDocument.Load(path);
            var busbarDikeyAkslari = doc.Entities.Lines
                .Where(l => l.Layer.Name == "M11-BUSBAR")
                .Where(l => Math.Abs(l.StartPoint.X - l.EndPoint.X) < 0.01)
                .Where(l => Math.Abs(l.StartPoint.Y - l.EndPoint.Y) > 1)
                .Select(l => Math.Round(l.StartPoint.X, 1))
                .Distinct()
                .Count();
            if (busbarDikeyAkslari < 6)
                throw new InvalidOperationException($"Çoklu busbarlar ana pano çıkışında ayrı akslardan inmeli; bulunan dikey busbar aksı: {busbarDikeyAkslari}.");
        }
        finally
        {
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
    }

    // ── Selektivite ──

    public static void Selektivite_UstCikisBirUstDegere()
    {
        var proje = new ElektrikProje { SelektiviteAktif = true };
        var kut = new ElektrikKutuphaneServisi();
        var ana = AnaPanoYap(proje, new Linye { FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 8000 });
        var tali = TaliPanoYap(proje, ana, "Tali", kabloM: 20,
            new Linye { FazTipi = FazTipi.Monofaze, SortiSayisi = 6, BirimGucW = 300 });
        HesaplaTumu(proje, kut);
        if (tali.UstPanoCikisSigortaA <= tali.GirisSigortaA)
            throw new InvalidOperationException(
                $"Selektivite açıkken üst çıkış sigortası bir üst değere çıkmalı (giriş={tali.GirisSigortaA}, üst çıkış={tali.UstPanoCikisSigortaA}).");
    }

    // ── UPS (KGK) ──

    /// <summary>Kritik panoyu besleyen UPS+akülü standart kurulum kurar.</summary>
    private static (ElektrikProje proje, Ups ups, Pano kritik) UpsliProjeYap(
        double kritikGucW, int otonomiDk, ElektrikKutuphaneServisi kut)
    {
        var proje = new ElektrikProje();
        var ana = AnaPanoYap(proje, new Linye { FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 8000 });
        var kritik = new Pano
        {
            Ad = "Kritik Pano", Kat = "Z", PanoTipi = PanoTipi.UpsPanosu,
            UstPano = ana, UstPanoAd = ana.Ad, KabloMesafeM = 10
        };
        kritik.Linyeler.Add(new Linye { FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = kritikGucW });
        ana.AltPanolar.Add(kritik);
        proje.TumPanolar.Add(kritik);

        var ups = new Ups { BesleyenPanoAd = ana.Ad, KritikPanoAd = kritik.Ad, OtonomiDk = otonomiDk };
        proje.Upsler.Add(ups);

        HesaplaTumu(proje, kut);
        UpsHesapServisi.HesaplaTumu(proje, kut);
        return (proje, ups, kritik);
    }

    public static void Ups_GerekliKVAEmniyetFaktorlu()
    {
        var kut = new ElektrikKutuphaneServisi();
        var (_, ups, _) = UpsliProjeYap(kritikGucW: 8000, otonomiDk: 10, kut);

        // GerekliKVA = kritik kVA × emniyet (1.25)
        double beklenen = ups.KritikYukKVA * ups.EmniyetFaktoru;
        if (Math.Abs(ups.GerekliKVA - beklenen) > 0.01)
            throw new InvalidOperationException($"GerekliKVA emniyet faktörlü olmalı (beklenen={beklenen:N2}, bulunan={ups.GerekliKVA:N2}).");
        if (ups.KritikYukKW <= 0)
            throw new InvalidOperationException("Kritik yük kW pozitif olmalı.");
    }

    public static void Ups_UygunTipOtomatikSecilir()
    {
        var kut = new ElektrikKutuphaneServisi();
        var (_, ups, _) = UpsliProjeYap(kritikGucW: 8000, otonomiDk: 10, kut);

        if (string.IsNullOrEmpty(ups.UpsTanimAd))
            throw new InvalidOperationException("UPS tipi otomatik seçilmeli.");
        var tanim = kut.Upsler.First(u => u.Ad == ups.UpsTanimAd);
        if (tanim.GucKVA < ups.GerekliKVA)
            throw new InvalidOperationException($"Seçilen UPS gerekli kVA'yı karşılamalı (gerekli={ups.GerekliKVA:N1}, seçilen={tanim.GucKVA}).");
        if (Math.Abs(ups.SeciliUpsGucKVA - tanim.GucKVA) > 0.01)
            throw new InvalidOperationException("Seçilen UPS anma gücü ortak gösterim alanına yazılmalı.");
        if (!ups.YukYeterliMi)
            throw new InvalidOperationException("Otomatik seçilen UPS yük yeterli olmalı.");
    }

    public static void Ups_GirisAkimiCikistanBuyuk()
    {
        var kut = new ElektrikKutuphaneServisi();
        var (_, ups, _) = UpsliProjeYap(kritikGucW: 15000, otonomiDk: 10, kut);

        // Giriş akımı = çıkış/η + şarj payı → çıkıştan büyük olmalı
        if (ups.GirisAkimA <= ups.CikisAkimA)
            throw new InvalidOperationException($"UPS giriş akımı çıkıştan büyük olmalı (giriş={ups.GirisAkimA:N1}A, çıkış={ups.CikisAkimA:N1}A).");
    }

    public static void Aku_SeriBlokDcBaradanTurer()
    {
        var kut = new ElektrikKutuphaneServisi();
        var (_, ups, _) = UpsliProjeYap(kritikGucW: 10000, otonomiDk: 10, kut);

        var upsTanim = kut.Upsler.First(u => u.Ad == ups.UpsTanimAd);
        var akuTanim = kut.Akuler.First(a => a.Ad == ups.AkuTanimAd);
        int beklenenSeri = (int)Math.Round(upsTanim.DcBaraGerilimV / akuTanim.BlokGerilimV, MidpointRounding.AwayFromZero);
        if (ups.SeriBlokSayisi != beklenenSeri)
            throw new InvalidOperationException($"Seri blok sayısı DC bara / blok gerilimi olmalı (beklenen={beklenenSeri}, bulunan={ups.SeriBlokSayisi}).");
        if (ups.ToplamBlokSayisi != ups.SeriBlokSayisi * ups.ParalelStringSayisi)
            throw new InvalidOperationException("Toplam blok = seri × paralel olmalı.");
    }

    public static void Aku_TabloDisiUzunOtonomiYetersizdir()
    {
        var aku = new AkuTanim
        {
            DesarjTablosu =
            {
                new AkuDesarjNoktasi { Dakika = 10, BlokGucW = 900 },
                new AkuDesarjNoktasi { Dakika = 30, BlokGucW = 500 }
            }
        };

        if (aku.BlokGucW(60) != 0)
            throw new InvalidOperationException("Akü deşarj tablosunun dışındaki uzun otonomi için kapasite bilinmiyor kabul edilmeli.");
    }

    public static void Ups_DxfUpsKatmaniUretir()
    {
        var kut = new ElektrikKutuphaneServisi();
        // 15,6kW / cosφ 0,85 × 1,25 ≈ 23kVA ihtiyaç; standart seçim 30kVA olmalı.
        // DXF ihtiyaç değerini (~23) değil, kanvasla aynı seçilen anma gücünü (30) yazmalı.
        var (proje, ups, _) = UpsliProjeYap(kritikGucW: 15600, otonomiDk: 15, kut);
        if (Math.Abs(ups.SeciliUpsGucKVA - 30) > 0.01 || ups.GerekliKVA < 22 || ups.GerekliKVA > 24)
            throw new InvalidOperationException("UPS DXF test kurgusunda yaklaşık 23kVA ihtiyaç ve 30kVA seçim beklenirdi.");

        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"m11_ups_dxf_{Guid.NewGuid():N}.dxf");
        try
        {
            // Kütüphane parametresi verilmeden de kayda yazılmış anma gücü kullanılmalı.
            TekHatSemasiDxfServisi.DxfCiktiAl(proje, path);
            var icerik = System.IO.File.ReadAllText(path);
            if (!icerik.Contains("M11-UPS"))
                throw new InvalidOperationException("UPS'li projede DXF M11-UPS katmanı bulunmalı.");
            var doc = DxfDocument.Load(path);
            var upsMetinleri = doc.Entities.Texts
                .Select(t => t.Value)
                .Where(t => t.StartsWith("UPS ", StringComparison.Ordinal))
                .ToList();
            if (!upsMetinleri.Any(t => t.Contains("30kVA", StringComparison.Ordinal)))
                throw new InvalidOperationException($"DXF UPS etiketi seçilen 30kVA anma gücünü göstermeli: {string.Join(" | ", upsMetinleri)}");
            if (upsMetinleri.Any(t => t.Contains("~23kVA", StringComparison.Ordinal)))
                throw new InvalidOperationException("DXF UPS etiketi yaklaşık ihtiyaç değerine düşmemeli.");
        }
        finally
        {
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
    }

    public static void Aku_UzunOtonomiDahaFazlaParalelGerektirir()
    {
        var kut = new ElektrikKutuphaneServisi();
        // Aynı yük, kısa vs uzun otonomi — uzun otonomi en az aynı (genelde daha fazla) paralel string ister
        var (_, kisa, _) = UpsliProjeYap(kritikGucW: 30000, otonomiDk: 5, kut);
        var (_, uzun, _) = UpsliProjeYap(kritikGucW: 30000, otonomiDk: 30, kut);

        if (uzun.ParalelStringSayisi < kisa.ParalelStringSayisi)
            throw new InvalidOperationException(
                $"Uzun otonomi (30dk) en az kısa otonomi (5dk) kadar paralel string istemeli (kısa={kisa.ParalelStringSayisi}, uzun={uzun.ParalelStringSayisi}).");
        if (uzun.ToplamEnerjiKWh <= 0)
            throw new InvalidOperationException("Akü toplam enerjisi pozitif olmalı.");
    }

    public static void Ups_KritikPanoyaCevirVeGeriAl()
    {
        var vm = new TekHatSemasiViewModel();
        vm.YeniPanoEkle("", "Z", PanoTipi.AnaPano);
        var ana = vm.Proje.KokPano!;

        vm.YeniPanoEkle("Sunucu", "1", PanoTipi.TaliPano);
        var sunucu = vm.Proje.TumPanolar.First(p => p.Ad == "Sunucu");
        sunucu.Linyeler.Add(new Linye { YukTipiAd = "Priz", FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 6_000 });

        var ups = vm.KritikPanoyaCevir(sunucu);

        if (sunucu.PanoTipi != PanoTipi.UpsPanosu)
            throw new InvalidOperationException("Pano kritik panoya (UpsPanosu) çevrilmedi.");
        if (ups.KritikPanoAd != "Sunucu" || ups.BesleyenPanoAd != ana.Ad)
            throw new InvalidOperationException("UPS besleyen/kritik pano referansları yanlış.");
        if (sunucu.Linyeler.Count != 1)
            throw new InvalidOperationException("Çevrimde mevcut linyeler korunmadı.");
        if (vm.KritikPanoUps(sunucu) == null)
            throw new InvalidOperationException("KritikPanoUps çevrilen panoyu bulamadı.");
        if (ups.KritikYukKVA <= 0)
            throw new InvalidOperationException("UPS kritik yükü hesaplanmadı.");

        // Geri al → tali panoya döner, linyeler korunur
        vm.UpsSil(ups);
        if (sunucu.PanoTipi != PanoTipi.TaliPano)
            throw new InvalidOperationException("Kritik panodan çıkınca tali panoya dönmeliydi.");
        if (sunucu.Linyeler.Count != 1)
            throw new InvalidOperationException("Geri alınca linyeler korunmadı.");
    }

    public static void Ups_KritikCevirKisitlari()
    {
        var vm = new TekHatSemasiViewModel();
        vm.YeniPanoEkle("", "Z", PanoTipi.AnaPano);
        var ana = vm.Proje.KokPano!;

        vm.YeniPanoEkle("A", "1", PanoTipi.TaliPano);
        var a = vm.Proje.TumPanolar.First(p => p.Ad == "A");
        vm.SeciliPano = ana; // bir sonraki pano da ana altına bağlansın
        vm.YeniPanoEkle("B", "1", PanoTipi.TaliPano);
        var b = vm.Proje.TumPanolar.First(p => p.Ad == "B");

        // Ana pano kritik panoya çevrilemez
        bool anaReddedildi = false;
        try { vm.KritikPanoyaCevir(ana); } catch (InvalidOperationException) { anaReddedildi = true; }
        if (!anaReddedildi) throw new InvalidOperationException("Ana pano kritik panoya çevrilmemeliydi.");

        // A çevrilir (besleyen = ana)
        vm.KritikPanoyaCevir(a);
        // B aynı üst panodan (ana) ikinci bağımsız UPS isteyebilir — artık izin verilir.
        vm.KritikPanoyaCevir(b);
        if (vm.Proje.Upsler.Count != 2)
            throw new InvalidOperationException(
                $"Aynı üst pano altında iki UPS olmalıydı (bulunan: {vm.Proje.Upsler.Count}).");
        if (vm.KritikPanoUps(a) == null || vm.KritikPanoUps(b) == null)
            throw new InvalidOperationException("Her iki kritik pano da kendi UPS'iyle çözülmeliydi.");
        // Aynı pano iki kez çevrilemez (zaten kritik pano).
        bool tekrarReddedildi = false;
        try { vm.KritikPanoyaCevir(a); } catch (InvalidOperationException) { tekrarReddedildi = true; }
        if (!tekrarReddedildi)
            throw new InvalidOperationException("Zaten kritik olan pano tekrar çevrilmemeliydi.");
    }

    public static void Ups_MerkeziUpsKurVeKatKritikPanosu()
    {
        var vm = new TekHatSemasiViewModel();
        vm.YeniPanoEkle("", "Z", PanoTipi.AnaPano);
        var ana = vm.Proje.KokPano!;

        vm.UpsTopolojisiAyarla(UpsTopolojisi.Merkezi);
        var ups = vm.MerkeziUpsKur();
        if (!ups.Merkezi) throw new InvalidOperationException("Merkezi UPS işaretlenmedi.");

        var dagitim = vm.MerkeziUpsDagitimPanosu();
        if (dagitim == null || dagitim.PanoTipi != PanoTipi.UpsPanosu)
            throw new InvalidOperationException("Merkezi UPS dağıtım panosu oluşmadı.");
        if (dagitim.UstPano != ana)
            throw new InvalidOperationException("Dağıtım panosu ana panonun altında olmalı.");

        var kritik = vm.MerkeziKatKritikPanosuEkle("1", "Kat1-Kritik");
        kritik.Linyeler.Add(new Linye { YukTipiAd = "Priz", FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 9_000 });
        vm.TumunuHesapla();

        if (kritik.PanoTipi != PanoTipi.UpsPanosu || kritik.UstPano != dagitim)
            throw new InvalidOperationException("Kat kritik panosu dağıtım altında UpsPanosu olmalı.");
        if (dagitim.ToplamGucW < 9_000 - 1)
            throw new InvalidOperationException($"Dağıtım toplam gücü kat kritik yükünü içermeli (bulunan {dagitim.ToplamGucW:N0} W).");
        if (ups.KritikYukKVA <= 0)
            throw new InvalidOperationException("Merkezi UPS yükü dağıtım toplamından hesaplanmalı.");
    }

    public static void Ups_MerkeziLinyeyiKritikPanoyaTasi()
    {
        var vm = new TekHatSemasiViewModel();
        vm.YeniPanoEkle("", "Z", PanoTipi.AnaPano);

        vm.YeniPanoEkle("Kat1-Pano", "1", PanoTipi.TaliPano);
        var normal = vm.Proje.TumPanolar.First(p => p.Ad == "Kat1-Pano");
        var linye = new Linye { YukTipiAd = "Priz", FazTipi = FazTipi.Monofaze, SortiSayisi = 1, BirimGucW = 1_200 };
        normal.Linyeler.Add(linye);

        vm.UpsTopolojisiAyarla(UpsTopolojisi.Merkezi);
        var ups = vm.MerkeziUpsKur();
        var kritik = vm.MerkeziKatKritikPanosuEkle("1", "Kat1-Kritik");
        vm.TumunuHesapla();
        double oncekiKritikYuk = ups.KritikYukKVA;

        var hedefler = vm.KattakiKritikPanolar("1");
        if (!hedefler.Contains(kritik))
            throw new InvalidOperationException("Kat 1 kritik panosu hedef listesinde olmalı.");
        if (hedefler.Contains(vm.MerkeziUpsDagitimPanosu()!))
            throw new InvalidOperationException("Dağıtım panosu taşıma hedefi olmamalı.");

        vm.LinyeyiKritikPanoyaTasi(linye, normal, kritik);
        if (normal.Linyeler.Contains(linye))
            throw new InvalidOperationException("Linye kaynak panodan kalkmadı.");
        if (!kritik.Linyeler.Contains(linye))
            throw new InvalidOperationException("Linye kritik panoya taşınmadı.");
        if (ups.KritikYukKVA <= oncekiKritikYuk)
            throw new InvalidOperationException("Linye taşınınca merkezi UPS yükü artmalıydı.");
    }

    public static void Ups_MerkeziDxfUpsKolonu()
    {
        var vm = new TekHatSemasiViewModel();
        vm.YeniPanoEkle("", "Z", PanoTipi.AnaPano);
        vm.UpsTopolojisiAyarla(UpsTopolojisi.Merkezi);
        vm.MerkeziUpsKur();
        var kritik = vm.MerkeziKatKritikPanosuEkle("1", "Kat1-Kritik");
        kritik.Linyeler.Add(new Linye { FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 8_000 });
        vm.TumunuHesapla();

        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"m11_merkezi_dxf_{Guid.NewGuid():N}.dxf");
        try
        {
            TekHatSemasiDxfServisi.DxfCiktiAl(vm.Proje, path);
            var icerik = System.IO.File.ReadAllText(path);
            if (!icerik.Contains("M11-UPS"))
                throw new InvalidOperationException("Merkezi UPS DXF'inde M11-UPS katmanı bulunmalı.");
            if (!icerik.Contains("UPS Kolonu"))
                throw new InvalidOperationException("Merkezi UPS DXF'inde 'UPS Kolonu' ayrı kolon etiketi bulunmalı.");
        }
        finally
        {
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
    }

    public static void Dxf_KatBazliAsagidanYukari()
    {
        var proje = new ElektrikProje { ProjeAdi = "Kat DXF" };
        var kut = new ElektrikKutuphaneServisi();
        var ana = AnaPanoYap(proje, new Linye { LinyeNo = "P1", FazTipi = FazTipi.Monofaze, SortiSayisi = 2, BirimGucW = 300 });
        var ust = TaliPanoYap(proje, ana, "Kat1 Pano", 20,
            new Linye { LinyeNo = "A1", FazTipi = FazTipi.Monofaze, SortiSayisi = 2, BirimGucW = 100 });
        ust.Kat = "1"; // ana "Z" katında, tali 1. katta

        HesaplaTumu(proje, kut);

        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"m11_kat_dxf_{Guid.NewGuid():N}.dxf");
        try
        {
            TekHatSemasiDxfServisi.DxfCiktiAl(proje, path);
            var satirlar = System.IO.File.ReadAllLines(path);
            var icerik = string.Join("\n", satirlar);

            if (!icerik.Contains("Kat: Z") || !icerik.Contains("Kat: 1"))
                throw new InvalidOperationException("DXF'te kat ayrım etiketleri (Kat: Z / Kat: 1) bulunmalı.");

            double? anaY = DxfTextY(satirlar, "Ana Pano");
            double? ustY = DxfTextY(satirlar, "Kat1 Pano");
            if (anaY == null || ustY == null)
                throw new InvalidOperationException("Pano ad metinleri DXF'te bulunamadı.");
            // Aşağıdan yukarı: zemin (Z) panosu üst kattan DAHA AŞAĞIDA (CAD Y küçük) olmalı.
            if (!(anaY.Value < ustY.Value))
                throw new InvalidOperationException(
                    $"Zemin panosu üst kattan aşağıda olmalıydı (ana Y={anaY:N0}, üst Y={ustY:N0}).");
        }
        finally
        {
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
    }

    /// <summary>ASCII DXF'te verilen değere sahip TEXT varlığının Y (kod 20) konumunu bulur.</summary>
    private static double? DxfTextY(string[] satirlar, string deger)
    {
        double? sonY = null;
        for (int i = 0; i + 1 < satirlar.Length; i++)
        {
            var kod = satirlar[i].Trim();
            if (kod == "20" && double.TryParse(satirlar[i + 1].Trim(),
                    System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y))
                sonY = y;
            else if (kod == "1" && satirlar[i + 1].Trim() == deger)
                return sonY;
        }
        return null;
    }

    // ── Orta Gerilim (OG) / Trafo / Köşk ──

    public static void OrtaGerilim_TrafoOtomatikSecimi()
    {
        var kut = new ElektrikKutuphaneServisi();
        var t = OrtaGerilimHesapServisi.UygunTrafoSec(500, 1.0, null, kut);
        if (t == null || t.GucKVA < 500)
            throw new InvalidOperationException($"500 kVA yük için ≥500 kVA trafo beklenirdi, bulunan: {t?.GucKVA.ToString() ?? "yok"}.");
        // En küçük yeterli olmalı: seçilenden küçük ama ≥500 olan başka trafo bulunmamalı
        bool dahaKucukYeterli = kut.Trafolar.Any(x => x.GucKVA >= 500 && x.GucKVA < t.GucKVA);
        if (dahaKucukYeterli)
            throw new InvalidOperationException($"En küçük yeterli trafo seçilmeliydi, {t.GucKVA} kVA gereksiz büyük.");
    }

    public static void OrtaGerilim_TrafoAkimlari()
    {
        var trafo = new TrafoTanim { GucKVA = 630, PrimerGerilimKV = 34.5, SekonderGerilimV = 400 };
        double ag = OrtaGerilimHesapServisi.TrafoAgAkimA(trafo);
        double og = OrtaGerilimHesapServisi.TrafoOgAkimA(trafo);
        // AG = 630000/(√3·400) ≈ 909 A ; OG = 630000/(√3·34500) ≈ 10.5 A
        if (Math.Abs(ag - 909) > 10)
            throw new InvalidOperationException($"630 kVA trafo AG akımı ≈909 A beklenirdi, bulunan: {ag:N1} A.");
        if (Math.Abs(og - 10.54) > 1)
            throw new InvalidOperationException($"630 kVA trafo OG akımı ≈10.5 A beklenirdi, bulunan: {og:N2} A.");
    }

    public static void OrtaGerilim_KaynakIk3ZqZt()
    {
        var kut = new ElektrikKutuphaneServisi();
        var trafo = kut.Trafolar.First(t => Math.Abs(t.GucKVA - 630) < 1 && t.Tip == TrafoTipi.Yagli);
        var proje = new ElektrikProje { SistemGerilimV = 400 };
        var ana = AnaPanoYap(proje, new Linye { FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 5000 });
        proje.OrtaGerilim = new OrtaGerilimSistemi
        {
            AnaPanoAd = ana.Ad, OgGerilimKV = 34.5, SebekeKisaDevreMVA = 350, TrafoTanimAd = trafo.Ad, TrafoManuel = true
        };
        HesaplaTumu(proje, kut);

        // Zt(630/uk4)=0.01016, Zq(350MVA→AG)=0.000457 → Ztot=0.01062 → kaynak Ik ≈ 21.75 kA
        double kaynakIk = proje.OrtaGerilim.AgBaraIkKA;
        if (kaynakIk < 20.5 || kaynakIk > 22.5)
            throw new InvalidOperationException($"OG (630kVA + 350MVA şebeke) için trafo AG bara Ik3 ≈ 21.8 kA beklenirdi, bulunan: {kaynakIk:N1} kA.");
        // Şebeke empedansı (Zq) eklendiği için yalnız-trafo (≈22.7 kA) değerinin altında olmalı
        if (kaynakIk >= 22.7)
            throw new InvalidOperationException($"Zq eklendiğinde kaynak Ik, yalnız-trafo değerinin (22.7 kA) altında olmalıydı: {kaynakIk:N1} kA.");
        if (ana.KisaDevreAkimiKA >= kaynakIk)
            throw new InvalidOperationException($"Ana pano Ik, trafo-ana pano bağlantı empedansı sonrası kaynak Ik'dan düşük olmalıydı (ana={ana.KisaDevreAkimiKA:N1}, kaynak={kaynakIk:N1}).");
    }

    public static void OrtaGerilim_OgYokeskiDavranisKorunur()
    {
        // OG tanımlı değilken kısa devre eski TrafoGucKVA alanından türemeli (regresyon)
        var proje = new ElektrikProje { TrafoGucKVA = 630, TrafoUkYuzde = 4.0, SistemGerilimV = 400 };
        var kut = new ElektrikKutuphaneServisi();
        AnaPanoYap(proje, new Linye { FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 5000 });
        HesaplaTumu(proje, kut);
        double ik = proje.KokPano!.KisaDevreAkimiKA;
        if (ik < 21 || ik > 24)
            throw new InvalidOperationException($"OG yokken 630kVA/uk4 için Ik3 ≈ 22.7 kA korunmalı, bulunan: {ik:N1} kA.");
    }

    public static void OrtaGerilim_HucreAnmaUyarisi()
    {
        var kut = new ElektrikKutuphaneServisi();
        kut.OgHucreleri.Add(new OgHucreTanim { Ad = "Test Düşük 5A", HucreTipi = OgHucreTipi.Giris, AnmaAkimA = 5 });

        var proje = new ElektrikProje { SistemGerilimV = 400 };
        var ana = AnaPanoYap(proje, new Linye { FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 2_000_000 });
        proje.OrtaGerilim = new OrtaGerilimSistemi { AnaPanoAd = ana.Ad, OgGerilimKV = 34.5 };
        proje.OrtaGerilim.Hucreler.Add(new OgHucre { HucreTanimAd = "Test Düşük 5A", HucreTipi = OgHucreTipi.Giris });
        proje.OrtaGerilim.Hucreler.Add(new OgHucre { HucreTanimAd = "Giriş YA 36kV 630A", HucreTipi = OgHucreTipi.Giris });
        HesaplaTumu(proje, kut);

        // ~2.35 MVA → 2500 kVA trafo → OG akım ≈ 41.8 A
        var dusuk = proje.OrtaGerilim.Hucreler[0];
        var normal = proje.OrtaGerilim.Hucreler[1];
        if (dusuk.AnmaAkimYeterliMi)
            throw new InvalidOperationException($"5 A anma akımlı hücre {dusuk.AkimA:N0} A altında yetersiz olmalıydı.");
        if (!normal.AnmaAkimYeterliMi)
            throw new InvalidOperationException("630 A hücre OG akımı için yeterli olmalıydı.");
    }

    public static void OrtaGerilim_KabloBaglantiBoyutlandirma()
    {
        var kut = new ElektrikKutuphaneServisi();
        var proje = new ElektrikProje { SistemGerilimV = 400, IzinVerilenGerilimDususYuzde = 1.0 };
        var ana = AnaPanoYap(proje, new Linye { FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 400_000 });
        proje.OrtaGerilim = new OrtaGerilimSistemi
        {
            AnaPanoAd = ana.Ad,
            CikisBaglanti = new TrafoCikisBaglanti { Tip = TrafoCikisBaglantiTipi.Kablo, ReferansAd = "NYY", UzunlukM = 15, ParalelKolSayisi = 2 }
        };
        HesaplaTumu(proje, kut);

        var b = proje.OrtaGerilim.CikisBaglanti;
        if (b.AkimA <= 0)
            throw new InvalidOperationException("Kablo bağlantısında trafo AG akımı hesaplanmalı.");
        if (string.IsNullOrEmpty(b.KesitMm2))
            throw new InvalidOperationException("Kablo bağlantısında kesit atanmalı.");
        if (b.GerilimDususYuzde <= 0)
            throw new InvalidOperationException("Kablo bağlantısında gerilim düşümü hesaplanmalı.");
    }

    public static void OrtaGerilim_YuksekAkimdaParalelKabloyuOtomatikArtirir()
    {
        var kut = new ElektrikKutuphaneServisi();
        var trafo = kut.Trafolar.First(t => Math.Abs(t.GucKVA - 2000) < 1);
        var proje = new ElektrikProje
        {
            SistemGerilimV = 400,
            CosF = 0.85,
            IzinVerilenGerilimDususYuzde = 3
        };
        var ana = new Pano
        {
            Ad = "Ana Pano",
            PanoTipi = PanoTipi.AnaPano,
            ToplamGucW = 1_300_000,
            ToplamGorunurVA = 1_600_000,
            GirisAkimA = 2_400
        };
        var og = new OrtaGerilimSistemi
        {
            AnaPanoAd = ana.Ad,
            TrafoTanimAd = trafo.Ad, TrafoManuel = true,
            CikisBaglanti = new TrafoCikisBaglanti
            {
                Tip = TrafoCikisBaglantiTipi.Kablo,
                ReferansAd = "NYY",
                UzunlukM = 25,
                ParalelKolSayisi = 1
            }
        };

        OrtaGerilimHesapServisi.Hesapla(og, ana, proje, kut);

        if (og.CikisBaglanti.HesaplananParalelKolSayisi != 6)
            throw new InvalidOperationException(
                $"2000 kVA trafo AG hattında 240 mm²/564 A sınırı için 6 paralel kol beklenirdi; bulunan: {og.CikisBaglanti.HesaplananParalelKolSayisi}.");
        if (og.CikisBaglanti.KesitMm2 != "6x(4x240) mm²")
            throw new InvalidOperationException(
                $"Trafo-ana pano kablosu ortak paralel formatta olmalı: {og.CikisBaglanti.KesitMm2}.");
        if (ana.KabloKesiti != og.CikisBaglanti.KesitMm2 || ana.KabloParalelKolSayisi != 6)
            throw new InvalidOperationException("Hesaplanan trafo kablosu ve paralel kol sayısı ana panoya aynen yansımalı.");

        double kolAkimi = og.TrafoAgAkimA / og.CikisBaglanti.HesaplananParalelKolSayisi;
        if (kolAkimi > KabloHesapServisi.Maksimum240Mm2AkimKapasitesiA + 0.001)
            throw new InvalidOperationException("Trafo kablosunun bir paralel kolundaki akım 564 A sınırını aşmamalı.");

        var kucukTrafo = kut.Trafolar.First(t => Math.Abs(t.GucKVA - 630) < 1);
        og.TrafoTanimAd = kucukTrafo.Ad;
        OrtaGerilimHesapServisi.Hesapla(og, ana, proje, kut);

        if (og.CikisBaglanti.ParalelKolSayisi != 1)
            throw new InvalidOperationException("Kullanıcının minimum paralel kol girdisi hesap tarafından değiştirilmemeli.");
        if (og.CikisBaglanti.HesaplananParalelKolSayisi != 2 ||
            og.CikisBaglanti.KesitMm2 != "2x(4x240) mm²")
            throw new InvalidOperationException(
                $"Trafo 2000 kVA'dan 630 kVA'ya değişince kablo yeniden 2 kola düşmeliydi; bulunan: {og.CikisBaglanti.KesitMm2}.");
        if (ana.KabloKesiti != "2x(4x240) mm²" || ana.KabloParalelKolSayisi != 2)
            throw new InvalidOperationException("Trafo değişiminden sonraki yeni kablo hesabı ana panoya yansımalı.");
    }

    public static void OrtaGerilim_TrafoAnaPanoKablosuAnaPanoyaYansir()
    {
        var kut = new ElektrikKutuphaneServisi();
        var proje = new ElektrikProje { SistemGerilimV = 400, IzinVerilenGerilimDususYuzde = 3.0 };
        var ana = AnaPanoYap(proje, new Linye { FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 400_000 });
        proje.OrtaGerilim = new OrtaGerilimSistemi
        {
            AnaPanoAd = ana.Ad,
            CikisBaglanti = new TrafoCikisBaglanti
            {
                Tip = TrafoCikisBaglantiTipi.Kablo,
                ReferansAd = "NYY",
                UzunlukM = 5,
                ParalelKolSayisi = 2
            }
        };

        HesaplaTumu(proje, kut);
        double yakinIk = ana.KisaDevreAkimiKA;
        double yakinDusum = ana.KabloGerilimDususYuzde;

        proje.OrtaGerilim.CikisBaglanti.UzunlukM = 80;
        HesaplaTumu(proje, kut);

        var b = proje.OrtaGerilim.CikisBaglanti;
        if (Math.Abs(ana.KabloMesafeM - 80) > 0.001)
            throw new InvalidOperationException($"Ana pano kablo mesafesi OG bağlantı uzunluğuna güncellenmeliydi, bulunan: {ana.KabloMesafeM:N1} m.");
        if (!ana.KabloKesiti.Contains(b.KesitMm2, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Ana pano kablo kesiti OG bağlantı kesitini göstermeli (ana={ana.KabloKesiti}, OG={b.KesitMm2}).");
        if (ana.KabloGerilimDususYuzde <= yakinDusum)
            throw new InvalidOperationException($"Trafo-ana pano kablosu uzayınca gerilim düşümü artmalıydı (5m={yakinDusum:N3}, 80m={ana.KabloGerilimDususYuzde:N3}).");
        if (ana.KisaDevreAkimiKA >= yakinIk)
            throw new InvalidOperationException($"Trafo-ana pano kablosu uzayınca ana pano Ik düşmeli (5m={yakinIk:N2}, 80m={ana.KisaDevreAkimiKA:N2}).");
    }

    public static void OrtaGerilim_Bar24Baglanti()
    {
        var kut = new ElektrikKutuphaneServisi();
        var bb = kut.BusbarTipleri.OrderByDescending(x => x.NominalAkimA).First();
        var proje = new ElektrikProje { SistemGerilimV = 400 };
        var ana = AnaPanoYap(proje, new Linye { FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 300_000 });
        proje.OrtaGerilim = new OrtaGerilimSistemi
        {
            AnaPanoAd = ana.Ad,
            CikisBaglanti = new TrafoCikisBaglanti { Tip = TrafoCikisBaglantiTipi.Bar24, ReferansAd = bb.Ad, UzunlukM = 12 }
        };
        HesaplaTumu(proje, kut);

        var b = proje.OrtaGerilim.CikisBaglanti;
        if (b.KesitMm2 != bb.KesitMm2)
            throw new InvalidOperationException("Bar-24 bağlantısı seçilen busbar kesitini almalı.");
        if (b.GerilimDususYuzde <= 0)
            throw new InvalidOperationException("Bar-24 bağlantısında gerilim düşümü hesaplanmalı.");
    }

    public static void KoskYerlesim_Geometri()
    {
        var kut = new ElektrikKutuphaneServisi();
        var og = new OrtaGerilimSistemi { TrafoTanimAd = kut.Trafolar.First().Ad };
        og.Hucreler.Add(new OgHucre { HucreTanimAd = "Giriş YA 36kV 630A" });
        og.Hucreler.Add(new OgHucre { HucreTanimAd = "Ölçü 36kV" });
        og.Hucreler.Add(new OgHucre { HucreTanimAd = "Çıkış YA 36kV 630A" });

        var y = KoskYerlesimServisi.Hesapla(og, kut);
        var hucreEl = y.Elemanlar.Where(e => e.Tur == "Hucre").OrderBy(e => e.X).ToList();
        if (hucreEl.Count != 3)
            throw new InvalidOperationException($"3 hücre yerleştirilmeliydi, bulunan: {hucreEl.Count}.");
        for (int i = 0; i < hucreEl.Count - 1; i++)
            if (hucreEl[i + 1].X < hucreEl[i].X + hucreEl[i].Genislik - 0.01)
                throw new InvalidOperationException("Hücreler köşk planında üst üste binmemeli.");
        if (!y.Elemanlar.Any(e => e.Tur == "Trafo"))
            throw new InvalidOperationException("Köşk yerleşiminde trafo bölmesi bulunmalı.");
        double lineup = hucreEl.Sum(e => e.Genislik);
        if (y.GenislikMm < lineup)
            throw new InvalidOperationException($"Köşk genişliği hücre dizilim toplamından (≥{lineup:N0}mm) küçük olamaz: {y.GenislikMm:N0}mm.");
    }

    public static void OrtaGerilim_RoundTrip()
    {
        var kut = new ElektrikKutuphaneServisi();
        var proje = new ElektrikProje { ProjeAdi = "OG RoundTrip" };
        var ana = AnaPanoYap(proje, new Linye { FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 300_000 });
        proje.OrtaGerilim = new OrtaGerilimSistemi
        {
            AnaPanoAd = ana.Ad, KoskTipi = KoskTipi.BinaAlti, OgGerilimKV = 31.5, SebekeKisaDevreMVA = 400,
            TrafoTanimAd = kut.Trafolar.First().Ad, TrafoManuel = true,
            SebekeHucreXlpeKablosu = new OgXlpeKablo
            {
                IletkenMalzemesi = OgKabloIletkenMalzemesi.Aluminyum, KesitMm2 = 95, UzunlukM = 73
            },
            HucreTrafoXlpeKablosu = new OgXlpeKablo
            {
                IletkenMalzemesi = OgKabloIletkenMalzemesi.Bakir, KesitMm2 = 70, UzunlukM = 18
            },
            CikisBaglanti = new TrafoCikisBaglanti { Tip = TrafoCikisBaglantiTipi.Bar24, ReferansAd = "KX-2000", UzunlukM = 8 }
        };
        proje.OrtaGerilim.Hucreler.Add(new OgHucre { HucreTanimAd = "Giriş YA 36kV 630A", HucreTipi = OgHucreTipi.Giris });
        proje.OrtaGerilim.Hucreler.Add(new OgHucre { HucreTanimAd = "Trafo Koruma Kesicili 36kV", HucreTipi = OgHucreTipi.TrafoKoruma });

        var opts = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
        };
        var json = System.Text.Json.JsonSerializer.Serialize(proje, opts);
        var geri = System.Text.Json.JsonSerializer.Deserialize<ElektrikProje>(json, opts);

        var og = geri?.OrtaGerilim;
        if (og == null) throw new InvalidOperationException("OG round-trip sonrası OrtaGerilim kayboldu.");
        if (og.KoskTipi != KoskTipi.BinaAlti) throw new InvalidOperationException("Köşk tipi round-trip'te korunmadı.");
        if (Math.Abs(og.OgGerilimKV - 31.5) > 0.001) throw new InvalidOperationException("OG gerilim round-trip'te korunmadı.");
        if (og.Hucreler.Count != 2) throw new InvalidOperationException("Hücreler round-trip'te korunmadı.");
        if (og.SebekeHucreXlpeKablosu.IletkenMalzemesi != OgKabloIletkenMalzemesi.Aluminyum ||
            Math.Abs(og.SebekeHucreXlpeKablosu.KesitMm2 - 95) > 0.001 ||
            Math.Abs(og.SebekeHucreXlpeKablosu.UzunlukM - 73) > 0.001)
            throw new InvalidOperationException("Şebeke-hücre XLPE kablo bilgileri round-trip'te korunmadı.");
        if (og.HucreTrafoXlpeKablosu.IletkenMalzemesi != OgKabloIletkenMalzemesi.Bakir ||
            Math.Abs(og.HucreTrafoXlpeKablosu.KesitMm2 - 70) > 0.001 ||
            Math.Abs(og.HucreTrafoXlpeKablosu.UzunlukM - 18) > 0.001)
            throw new InvalidOperationException("Hücre-trafo XLPE kablo bilgileri round-trip'te korunmadı.");
        if (og.CikisBaglanti.Tip != TrafoCikisBaglantiTipi.Bar24 || og.CikisBaglanti.ReferansAd != "KX-2000")
            throw new InvalidOperationException("Çıkış bağlantısı round-trip'te korunmadı.");
    }

    public static void OrtaGerilim_DxfKatmanlari()
    {
        var kut = new ElektrikKutuphaneServisi();
        var proje = new ElektrikProje { ProjeAdi = "OG DXF" };
        var ana = AnaPanoYap(proje, new Linye { FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 300_000 });
        proje.OrtaGerilim = new OrtaGerilimSistemi
        {
            AnaPanoAd = ana.Ad,
            TrafoTanimAd = kut.Trafolar.First().Ad, TrafoManuel = true,
            SebekeHucreXlpeKablosu = new OgXlpeKablo
            {
                IletkenMalzemesi = OgKabloIletkenMalzemesi.Aluminyum, KesitMm2 = 95, UzunlukM = 73
            },
            HucreTrafoXlpeKablosu = new OgXlpeKablo
            {
                IletkenMalzemesi = OgKabloIletkenMalzemesi.Bakir, KesitMm2 = 70, UzunlukM = 18
            }
        };
        proje.OrtaGerilim.Hucreler.Add(new OgHucre { HucreTanimAd = "Giriş YA 36kV 630A", HucreTipi = OgHucreTipi.Giris });
        proje.OrtaGerilim.Hucreler.Add(new OgHucre { HucreTanimAd = "Trafo Koruma Kesicili 36kV", HucreTipi = OgHucreTipi.TrafoKoruma });
        HesaplaTumu(proje, kut);

        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"m11_og_dxf_{Guid.NewGuid():N}.dxf");
        try
        {
            TekHatSemasiDxfServisi.DxfCiktiAl(proje, path, kut);
            var icerik = System.IO.File.ReadAllText(path);
            if (!icerik.Contains("M11-OG") || !icerik.Contains("M11-TRAFO") || !icerik.Contains("M11-HUCRE"))
                throw new InvalidOperationException("OG'li projede DXF M11-OG/TRAFO/HUCRE katmanları bulunmalı.");
            // netDxf üst simge ² karakterini DXF Unicode kaçışıyla yazabilir; tip, kesit ve metrajı
            // ayrı ASCII parçalar üzerinden doğrula.
            if (!icerik.Contains("NA2XSY XLPE Al") || !icerik.Contains("3x(1x95 mm") || !icerik.Contains("73m"))
                throw new InvalidOperationException("Şebeke-hücre XLPE kablosu DXF etiketinde görünmeli.");
            if (!icerik.Contains("N2XSY XLPE Cu") || !icerik.Contains("3x(1x70 mm") || !icerik.Contains("18m"))
                throw new InvalidOperationException("Hücre-trafo XLPE kablosu DXF etiketinde görünmeli.");
        }
        finally
        {
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
    }

    public static void OrtaGerilim_YenidenAdlandirmaKaskadi()
    {
        var vm = new TekHatSemasiViewModel();
        vm.YeniPanoEkle("Ana Pano", "Z", PanoTipi.AnaPano);
        var ana = vm.Proje.KokPano!;
        vm.OrtaGerilimEkle(ana);
        vm.PanoyuYenidenAdlandir(ana, "Ana Pano 2");
        if (ana.Ad != "Ana Pano" || vm.Proje.OrtaGerilim!.AnaPanoAd != "Ana Pano")
            throw new InvalidOperationException("Ana pano adı her zaman 'Ana Pano' olarak korunmalı.");
    }

    public static void M11Duzeltme_MonofazePanoEnYukluFazaGoreBoyutlanir()
    {
        var vm = new TekHatSemasiViewModel();
        vm.YeniPanoEkle("Ana Pano", "Z", PanoTipi.AnaPano);
        vm.YeniPanoEkle("Mono", "1", PanoTipi.TaliPano);
        var pano = vm.SeciliPano!;
        pano.Linyeler.Add(new Linye { FazTipi = FazTipi.Monofaze, BirimGucW = 10_000 });
        vm.TumunuHesapla();
        double enYuklu = Math.Max(pano.FazDagilimi.R_A, Math.Max(pano.FazDagilimi.S_A, pano.FazDagilimi.T_A));
        if (pano.GirisAkimA + 0.001 < enYuklu || pano.GirisSigortaA < enYuklu)
            throw new InvalidOperationException("Monofaze pano beslemesi en yüklü faz akımını taşımalı.");
    }

    public static void M11Duzeltme_UpsGirisiBeslemeZincirineYansirVeOtomatikBuyur()
    {
        var vm = new TekHatSemasiViewModel();
        vm.Proje.CosF = 0.99;
        vm.YeniPanoEkle("Ana Pano", "Z", PanoTipi.AnaPano);
        var ana = vm.Proje.KokPano!;
        var ups = vm.MerkeziUpsKur();
        var kritik = vm.MerkeziUpsDagitimPanosu()!;
        string ilkTip = ups.UpsTanimAd;
        kritik.Linyeler.Add(new Linye { FazTipi = FazTipi.Trifaze, BirimGucW = 20_000 });
        vm.TumunuHesapla();
        if (!ups.YukYeterliMi || ups.UpsTanimAd == ilkTip)
            throw new InvalidOperationException("Otomatik UPS yük büyüyünce yeniden seçilmeli.");
        if (kritik.TalepAkimA + 0.001 < ups.GirisAkimA ||
            kritik.GirisSigortaA < PanoHesapServisi.GirisSigortaHesapla(ups.GirisAkimA) ||
            ana.GirisAkimA + 0.001 < ups.GirisAkimA)
            throw new InvalidOperationException("UPS giriş akımı pano koruma ve üst besleme zincirine yansımalı.");
    }

    public static void M11Duzeltme_KompanzasyonBusbariSonAkimlaHesaplanir()
    {
        var vm = new TekHatSemasiViewModel();
        vm.YeniPanoEkle("Ana Pano", "Z", PanoTipi.AnaPano);
        var ana = vm.Proje.KokPano!;
        ana.Linyeler.Add(new Linye { FazTipi = FazTipi.Trifaze, BirimGucW = 300_000 });
        var bb = vm.BusbarEkle(ana, vm.BusbarTipleri[0], 20);
        vm.YeniPanoEkle("Komp", "Z", PanoTipi.Kompanzasyon);
        var komp = vm.SeciliPano!;
        vm.PanoyuBusbarBagla(komp, bb, 20, 400, 2);
        vm.TumunuHesapla();
        if (komp.GirisAkimA <= 0 || Math.Abs(bb.ToplamAkimA - komp.TalepAkimA) > 0.01)
            throw new InvalidOperationException("Busbar kompanzasyon panosunun kesinleşen banka akımını taşımalı.");
    }

    public static void M11Duzeltme_AsiriTalepKabloyuYetersizIsaretler()
    {
        var vm = new TekHatSemasiViewModel();
        vm.YeniPanoEkle("Ana Pano", "Z", PanoTipi.AnaPano);
        vm.YeniPanoEkle("Büyük Yük", "1", PanoTipi.TaliPano);
        var pano = vm.SeciliPano!;
        pano.Linyeler.Add(new Linye { FazTipi = FazTipi.Trifaze, BirimGucW = 4_000_000 });
        vm.TumunuHesapla();
        if (pano.KabloKesitiYeterli)
            throw new InvalidOperationException("Gerçek talebi taşımayan azami paralel kablo düzeni yetersiz işaretlenmeli.");
    }

    public static void M11Duzeltme_BusbarBaglantisiDonguyuReddeder()
    {
        var vm = new TekHatSemasiViewModel();
        vm.YeniPanoEkle("Ana Pano", "Z", PanoTipi.AnaPano);
        vm.YeniPanoEkle("Tali", "1", PanoTipi.TaliPano);
        var tali = vm.SeciliPano!;
        var bb = vm.BusbarEkle(tali, vm.BusbarTipleri[0], 10);
        bool reddedildi = false;
        try { vm.PanoyuBusbarBagla(tali, bb, 5, 32, 2); }
        catch (InvalidOperationException) { reddedildi = true; }
        if (!reddedildi || tali.UstPano == tali || tali.AltPanolar.Contains(tali))
            throw new InvalidOperationException("Pano kendi busbarına bağlanamamalı.");
    }

    public static void M11Duzeltme_ExcelAktarimiBoskenMevcuduKorur()
    {
        var vm = new TekHatSemasiViewModel();
        vm.YeniPanoEkle("Ana Pano", "Z", PanoTipi.AnaPano);
        var pano = vm.SeciliPano!;
        var mevcut = new Linye { Aciklama = "Korunacak" };
        pano.Linyeler.Add(mevcut);
        if (vm.LinyeleriIceAktar(Array.Empty<Linye>()) != 0 || !pano.Linyeler.Contains(mevcut))
            throw new InvalidOperationException("Boş Excel aktarımı mevcut linyeleri silmemeli.");
    }

    public static void M11Duzeltme_PanoAdlariVeAnaPanoTipiKorunur()
    {
        var vm = new TekHatSemasiViewModel();
        vm.YeniPanoEkle("Ana Pano", "Z", PanoTipi.AnaPano);
        vm.YeniPanoEkle("Tali", "1", PanoTipi.TaliPano);
        var tali = vm.SeciliPano!;
        bool adReddedildi = false, anaReddedildi = false;
        try { vm.YeniPanoEkle("Tali", "2", PanoTipi.TaliPano); }
        catch (InvalidOperationException) { adReddedildi = true; }
        try { vm.PanoTipiniDegistir(tali, PanoTipi.AnaPano); }
        catch (InvalidOperationException) { anaReddedildi = true; }
        var ana = vm.Proje.KokPano!;
        vm.PanoyuYenidenAdlandir(ana, "Başka Ad");
        if (!adReddedildi || !anaReddedildi || ana.Ad != "Ana Pano")
            throw new InvalidOperationException("Pano adları benzersiz, ana pano tekil ve adı sabit olmalı.");
    }

    public static void M11Duzeltme_ExcelMetinOndalikAyiraciDogruOkunur()
    {
        var eskiKultur = CultureInfo.CurrentCulture;
        var path = Path.Combine(Path.GetTempPath(), $"m11_decimal_{Guid.NewGuid():N}.xlsx");
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            using (var wb = new XLWorkbook())
            {
                var ws = wb.AddWorksheet("Linyeler");
                ws.Cell(1, 1).Value = "Yük Tipi";
                ws.Cell(1, 2).Value = "Birim Güç (W)";
                ws.Cell(1, 3).Value = "Talep Faktörü";
                ws.Cell(1, 4).Value = "Kablo Mesafesi (m)";
                ws.Cell(2, 1).Value = "Priz";
                ws.Cell(2, 2).Value = "300,5";
                ws.Cell(2, 3).Value = "0,5";
                ws.Cell(2, 4).Value = "10,5";
                wb.SaveAs(path);
            }
            var (linyeler, _) = TekHatLinyeExcelServisi.IceriAktar(path);
            var l = linyeler.Single();
            if (Math.Abs(l.BirimGucW - 300.5) > 0.001 || Math.Abs(l.TalepFaktoru - 0.5) > 0.001 ||
                Math.Abs(l.KabloMesafeM - 10.5) > 0.001)
                throw new InvalidOperationException("Excel metin hücrelerindeki virgüllü ondalıklar doğru okunmalı.");
        }
        finally
        {
            CultureInfo.CurrentCulture = eskiKultur;
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public static void M11Duzeltme_UpsVeTransferPanoTipleriKorunur()
    {
        var vm = new TekHatSemasiViewModel();
        var upsPano = new Pano { Ad = "UPS Kritik", PanoTipi = PanoTipi.UpsPanosu };
        var transferPano = new Pano { Ad = "Transfer", PanoTipi = PanoTipi.TransferPanosu };
        vm.Proje.TumPanolar.Add(upsPano);
        vm.Proje.TumPanolar.Add(transferPano);

        bool upsReddedildi = false, transferReddedildi = false;
        try { vm.PanoTipiniDegistir(upsPano, PanoTipi.TaliPano); }
        catch (InvalidOperationException) { upsReddedildi = true; }
        try { vm.PanoTipiniDegistir(transferPano, PanoTipi.TaliPano); }
        catch (InvalidOperationException) { transferReddedildi = true; }

        if (!upsReddedildi || !transferReddedildi ||
            upsPano.PanoTipi != PanoTipi.UpsPanosu || transferPano.PanoTipi != PanoTipi.TransferPanosu)
            throw new InvalidOperationException("UPS ve transfer pano tipleri genel pano düzenlemesinde korunmalı.");
    }

    public static void M11Duzeltme_OgOnizlemeAnaPanoyuDegistirmez()
    {
        var kut = new ElektrikKutuphaneServisi();
        var proje = new ElektrikProje();
        var ana = AnaPanoYap(proje, new Linye { FazTipi = FazTipi.Trifaze, BirimGucW = 20_000 });
        HesaplaTumu(proje, kut);
        ana.KabloMesafeM = 3; ana.KabloKesiti = "4x16"; ana.KabloTipi = "NHXMH";
        var og = new OrtaGerilimSistemi
        {
            AnaPanoAd = ana.Ad,
            CikisBaglanti = new TrafoCikisBaglanti { UzunlukM = 60, ReferansAd = "NYY" }
        };
        OrtaGerilimHesapServisi.Hesapla(og, ana, proje, kut, anaPanoyaYansit: false);
        if (ana.KabloMesafeM != 3 || ana.KabloKesiti != "4x16" || ana.KabloTipi != "NHXMH")
            throw new InvalidOperationException("OG önizlemesi canlı ana panoyu değiştirmemeli.");
    }

    public static void OrtaGerilim_OtomatikTrafoYukArttikcaBuyurManuelKorunur()
    {
        var kut = new ElektrikKutuphaneServisi();
        var proje = new ElektrikProje();
        var yuk = new Linye { FazTipi = FazTipi.Trifaze, SortiSayisi = 1, BirimGucW = 100_000 };
        var ana = AnaPanoYap(proje, yuk);
        proje.OrtaGerilim = new OrtaGerilimSistemi { AnaPanoAd = ana.Ad };
        HesaplaTumu(proje, kut);
        double ilkKVA = kut.Trafolar.First(t => t.Ad == proje.OrtaGerilim.TrafoTanimAd).GucKVA;

        yuk.BirimGucW = 900_000; // yük artar → otomatik trafo yeniden seçilmeli
        HesaplaTumu(proje, kut);
        var og = proje.OrtaGerilim;
        double yeniKVA = kut.Trafolar.First(t => t.Ad == og.TrafoTanimAd).GucKVA;
        if (yeniKVA <= ilkKVA || !og.TrafoYeterliMi)
            throw new InvalidOperationException(
                $"Otomatik trafo yük artınca büyümeliydi. İlk {ilkKVA} kVA, yeni {yeniKVA} kVA, yeterli={og.TrafoYeterliMi}.");

        // Elle sabitlenen trafo yük artsa da değişmez (yetersiz işaretlenir).
        var sabit = kut.Trafolar.OrderBy(t => t.GucKVA).First();
        og.TrafoTanimAd = sabit.Ad;
        og.TrafoManuel = true;
        yuk.BirimGucW = 1_500_000;
        HesaplaTumu(proje, kut);
        if (og.TrafoTanimAd != sabit.Ad || og.TrafoYeterliMi)
            throw new InvalidOperationException("Elle seçilen trafo korunmalı ve yetersiz işaretlenmeliydi.");
    }

    /// <summary>
    /// "Kaydedilmedi" göstergesi proje JSON'unu kayıttaki haliyle karşılaştırır; değişiklik yokken
    /// yeniden hesap JSON'u değiştirirse gösterge yanlışlıkla yanar. Hesap tekrarlanabilir olmalı.
    /// </summary>
    public static void TekHat_YenidenHesapProjeJsonunuDegistirmez()
    {
        var opts = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
        };
        var vm = new TekHatSemasiViewModel();
        var yol = Environment.GetEnvironmentVariable("E4P_PARMAKIZI_PROJE");
        if (!string.IsNullOrEmpty(yol))
        {
            vm.ProjeyiYukle(System.Text.Json.JsonSerializer.Deserialize<ElektrikProje>(File.ReadAllText(yol), opts)!);
        }
        else
        {
            vm.YeniPanoEkle("Ana Pano", "Z", PanoTipi.AnaPano);
            var ana = vm.Proje.KokPano!;
            ana.Linyeler.Add(new Linye { FazTipi = FazTipi.Trifaze, BirimGucW = 120_000 });
            ana.Linyeler.Add(new Linye { FazTipi = FazTipi.Monofaze, SortiSayisi = 6, BirimGucW = 300 });
            vm.YeniPanoEkle("T1", "Z", PanoTipi.TaliPano);
            vm.Proje.TumPanolar.First(p => p.Ad == "T1").Linyeler.Add(new Linye { BirimGucW = 2000 });
            vm.OrtaGerilimEkle(ana);
        }
        vm.TumunuHesapla();
        vm.KaydetmeyeHazirla();
        var ilk = System.Text.Json.JsonSerializer.Serialize(vm.Proje, opts);
        vm.TumunuHesapla();
        vm.KaydetmeyeHazirla();
        var ikinci = System.Text.Json.JsonSerializer.Serialize(vm.Proje, opts);
        if (ilk != ikinci)
        {
            int i = 0;
            while (i < Math.Min(ilk.Length, ikinci.Length) && ilk[i] == ikinci[i]) i++;
            var bas = Math.Max(0, i - 200);
            throw new InvalidOperationException(
                $"Değişiklik yokken yeniden hesap proje JSON'unu değiştirdi (konum {i}): …{ilk.Substring(bas, Math.Min(260, ilk.Length - bas))}…");
        }
    }

    public static void M11Duzeltme_LinyeTekrariEkipmanTercihleriniKorur()
    {
        var vm = new TekHatSemasiViewModel();
        var pano = new Pano { Ad = "P" };
        var kaynak = new Linye
        {
            KontaktorluMu = true, DarbeKesiciMi = true, TmsZorla = true,
            KesmeKapasitesiManuel = true, KesmeKapasitesi = "100kA"
        };
        pano.Linyeler.Add(kaynak); vm.Proje.TumPanolar.Add(pano); vm.SeciliPano = pano;
        var kopya = vm.LinyeTekrarlaEkle(kaynak)!;
        if (!kopya.KontaktorluMu || !kopya.DarbeKesiciMi || !kopya.TmsZorla ||
            !kopya.KesmeKapasitesiManuel || kopya.KesmeKapasitesi != "100kA")
            throw new InvalidOperationException("Tekrarlanan linye ekipman ve manuel koruma tercihlerini korumalı.");
    }

    public static void M11Duzeltme_DxfUpsBeslemeyiSeriCizer()
    {
        var vm = new TekHatSemasiViewModel();
        vm.YeniPanoEkle("Ana Pano", "Z", PanoTipi.AnaPano);
        var ana = vm.Proje.KokPano!;
        var ups = vm.UpsEkle(ana);
        var kritik = vm.Proje.TumPanolar.First(p => p.Ad == ups.KritikPanoAd);
        kritik.Linyeler.Add(new Linye { FazTipi = FazTipi.Trifaze, BirimGucW = 10_000 });
        vm.TumunuHesapla();
        var path = Path.Combine(Path.GetTempPath(), $"m11_ups_series_{Guid.NewGuid():N}.dxf");
        try
        {
            TekHatSemasiDxfServisi.DxfCiktiAl(vm.Proje, path, vm.Kutuphane);
            var doc = DxfDocument.Load(path);
            var upsKutuYataylari = doc.Entities.Lines
                .Where(l => l.Layer.Name == "M11-UPS" &&
                    Math.Abs(l.StartPoint.Y - l.EndPoint.Y) < 0.01 &&
                    Math.Abs(Math.Abs(l.StartPoint.X - l.EndPoint.X) - 72) < 0.01)
                .ToList();
            double upsUst = upsKutuYataylari.Max(l => l.StartPoint.Y);
            var ustKenar = upsKutuYataylari.First(l => Math.Abs(l.StartPoint.Y - upsUst) < 0.01);
            double cx = (ustKenar.StartPoint.X + ustKenar.EndPoint.X) / 2.0;
            bool girisVar = doc.Entities.Lines.Any(l => l.Layer.Name == "M11-UPS" &&
                ((Math.Abs(l.StartPoint.X - cx) < 0.01 && Math.Abs(l.StartPoint.Y - upsUst) < 0.01) ||
                 (Math.Abs(l.EndPoint.X - cx) < 0.01 && Math.Abs(l.EndPoint.Y - upsUst) < 0.01)));
            if (!girisVar) throw new InvalidOperationException("Üst pano hattı UPS giriş kenarına bağlanmalı.");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    public static void M11Duzeltme_BusbarDiversiteTalebiniKullanir()
    {
        var kut = new ElektrikKutuphaneServisi();
        var proje = new ElektrikProje();
        var ana = AnaPanoYap(proje);
        var tali = TaliPanoYap(proje, ana, "Tali", 5,
            new Linye { FazTipi = FazTipi.Trifaze, BirimGucW = 200_000 });
        tali.EszamanlilikFaktoru = 0.5;
        HesaplaTumu(proje, kut);
        var bb = new BusbarHatti { BusbarTipiAd = kut.BusbarTipleri[0].Ad };
        bb.CikisKutulari.Add(new BusbarCikisKutusu { BagliPanoAd = tali.Ad, PozisyonM = 5 });
        BusbarHesapServisi.Hesapla(bb, proje, kut);
        if (Math.Abs(bb.ToplamAkimA - tali.TalepAkimA) > 0.01)
            throw new InvalidOperationException("Busbar bağlı akım yerine diversite uygulanmış talep akımını kullanmalı.");
    }
}
