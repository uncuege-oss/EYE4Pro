using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using Kst.Acoustics.Core.Electric.Models;
using Kst.Acoustics.Core.Electric.Calculation;

namespace Kst.Acoustics.App.Electric.ViewModels;

public partial class TekHatSemasiViewModel
{
    // === Komutlar ===

    public void YeniPanoEkle(string ad, string kat, PanoTipi tip, bool aydinlatmaSayacli = false,
        bool harmonikFiltreli = false, HarmonikFiltreTanim? harmonikFiltre = null)
    {
        if (tip == PanoTipi.AnaPano)
        {
            if (Panolar.Any(p => p.PanoTipi == PanoTipi.AnaPano))
                throw new InvalidOperationException("Projede yalnızca 1 adet Ana Pano olabilir.");
            ad = "Ana Pano";
        }

        ad = (ad ?? "").Trim();
        if (string.IsNullOrWhiteSpace(ad))
            throw new InvalidOperationException("Pano adı boş olamaz.");
        if (!PanoAdiKullanilabilir(ad))
            throw new InvalidOperationException($"'{ad}' adlı pano zaten var. Pano adları benzersiz olmalıdır.");

        var pano = new Pano
        {
            Ad = ad,
            Kat = kat,
            PanoTipi = tip,
            AydinlatmaSayacli = aydinlatmaSayacli,
            HarmonikFiltreliMi = harmonikFiltreli,
            SeciliHarmonikFiltre = harmonikFiltre
        };

        if (Panolar.Count > 0 && tip != PanoTipi.AnaPano && tip != PanoTipi.KontrolPanosu)
        {
            // Seçili pano varsa ona, yoksa ilk ana panoya bağla
            var ustPano = SeciliPano ?? Panolar[0];
            pano.UstPano = ustPano;
            pano.UstPanoAd = ustPano.Ad;
            ustPano.AltPanolar.Add(pano);
        }

        Panolar.Add(pano);
        Proje.TumPanolar.Add(pano);

        if (Proje.KokPano == null || tip == PanoTipi.AnaPano)
            Proje.KokPano = pano;

        // Ana pano eklendiyse, ana panodan önce oluşturulup üst panosu olmayan (öksüz)
        // panoları ana panonun altına bağla — aksi halde ağaç dışında kalırlar.
        if (tip == PanoTipi.AnaPano)
        {
            foreach (var oksuz in Proje.TumPanolar)
            {
                if (oksuz == pano || oksuz.UstPano != null || oksuz.PanoTipi == PanoTipi.AnaPano
                    || oksuz.PanoTipi == PanoTipi.TransferPanosu
                    || oksuz.PanoTipi == PanoTipi.KontrolPanosu)
                    continue;
                oksuz.UstPano = pano;
                oksuz.UstPanoAd = pano.Ad;
                if (!pano.AltPanolar.Contains(oksuz))
                    pano.AltPanolar.Add(oksuz);
            }
        }

        SeciliPano = pano;
    }

    // === Busbar Yönetimi ===
    public BusbarHatti BusbarEkle(Pano ustPano, BusbarTanim tip, double uzunluk, bool otomatikTipSec = true)
    {
        var bb = new BusbarHatti
        {
            Ad = $"Busbar-{Busbarlar.Count + 1}",
            BusbarTipiAd = tip.Ad,
            OtomatikTipSec = otomatikTipSec,
            ToplamUzunlukM = Math.Max(0, uzunluk),
            UstPanoAd = ustPano.Ad
        };
        ustPano.CikanBusbarlar.Add(bb);
        Busbarlar.Add(bb);
        Proje.TumBusbarlar.Add(bb);
        return bb;
    }

    public void BusbarSil(BusbarHatti bb)
    {
        var ustPano = Proje.TumPanolar.FirstOrDefault(p => p.Ad == bb.UstPanoAd);
        ustPano?.CikanBusbarlar.Remove(bb);

        // Busbar'a bağlı tali panoları doğrudan kablo'ya çevir
        foreach (var pano in Proje.TumPanolar.Where(p => p.BaglandigiBusbarId == bb.Id))
        {
            pano.BaglantiTipi = BaglantiTipi.Kablo;
            pano.BaglandigiBusbarId = null;
            pano.CikisKutusuSigortaA = 0;
        }
        Busbarlar.Remove(bb);
        Proje.TumBusbarlar.Remove(bb);
    }

    public void BusbarDuzenle(BusbarHatti bb, string ad, Pano ustPano, BusbarTanim? tip,
        bool otomatikTipSec, double uzunlukM)
    {
        var eskiUst = Proje.TumPanolar.FirstOrDefault(p => p.Ad == bb.UstPanoAd);
        if (eskiUst != ustPano)
        {
            eskiUst?.CikanBusbarlar.Remove(bb);
            if (!ustPano.CikanBusbarlar.Contains(bb))
                ustPano.CikanBusbarlar.Add(bb);
            bb.UstPanoAd = ustPano.Ad;

            foreach (var pano in Proje.TumPanolar.Where(p => p.BaglandigiBusbarId == bb.Id))
            {
                if (pano.UstPano != null) pano.UstPano.AltPanolar.Remove(pano);
                pano.UstPano = ustPano;
                pano.UstPanoAd = ustPano.Ad;
                if (!ustPano.AltPanolar.Contains(pano))
                    ustPano.AltPanolar.Add(pano);
            }
        }

        ad = (ad ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(ad) && ad != bb.Ad)
        {
            bb.Ad = ad;
            BusbarYenidenDuzenle(bb);
        }

        bb.OtomatikTipSec = otomatikTipSec;
        if (!otomatikTipSec && tip != null)
            bb.BusbarTipiAd = tip.Ad;

        if (bb.KatBolumleri.Count > 0)
            BusbarYerlesimServisi.Uygula(Proje, bb, bb.KatBolumleri);
        else
        {
            var minUzunluk = BusbarMinUzunluk(bb);
            bb.ToplamUzunlukM = Math.Max(Math.Max(0, uzunlukM), minUzunluk);
        }
        BusbarYenidenDuzenle(bb);
    }

    public void PanoyuBusbarBagla(Pano pano, BusbarHatti bb, double pozisyon, int cikisSigortaA, double kabloKutuMesafeM)
        => PanoyuBusbarBaglaTemel(pano, bb, pozisyon, null, cikisSigortaA, kabloKutuMesafeM);

    public void PanoyuBusbarBaglaKatKonumuyla(Pano pano, BusbarHatti bb, double katPozisyonM,
        int cikisSigortaA, double kabloKutuMesafeM)
    {
        double katUzunlugu = BusbarYerlesimServisi.KatUzunlugu(bb, pano.Kat);
        if (bb.KatBolumleri.Count > 0 && katUzunlugu <= 0)
            throw new InvalidOperationException($"{pano.Kat} katında busbar uzunluğu tanımlı değil. Önce busbar adına tıklayıp kat yerleşimini girin.");
        if (katUzunlugu > 0 && katPozisyonM > katUzunlugu)
            throw new InvalidOperationException($"Çıkış kutusu konumu ({katPozisyonM:0.##} m), {pano.Kat} katındaki busbar uzunluğunu ({katUzunlugu:0.##} m) aşamaz.");

        double mutlakPozisyon = bb.KatBolumleri.Count == 0
            ? Math.Max(0, katPozisyonM)
            : BusbarYerlesimServisi.MutlakPozisyonHesapla(Proje, bb, pano.Kat, katPozisyonM);
        PanoyuBusbarBaglaTemel(pano, bb, mutlakPozisyon, Math.Max(0, katPozisyonM),
            cikisSigortaA, kabloKutuMesafeM);
    }

    private void PanoyuBusbarBaglaTemel(Pano pano, BusbarHatti bb, double pozisyon,
        double? katPozisyonM, int cikisSigortaA, double kabloKutuMesafeM)
    {
        var bbUstPano = Proje.TumPanolar.FirstOrDefault(p => p.Ad == bb.UstPanoAd);
        if (bbUstPano == null)
            throw new InvalidOperationException("Busbarı besleyen pano bulunamadı.");
        if (bbUstPano == pano || AltAgactaMi(pano, bbUstPano))
            throw new InvalidOperationException("Pano kendi busbarına veya alt ağacındaki bir busbara bağlanamaz.");

        // Önce eski bağlantıyı kaldır
        if (pano.BaglandigiBusbarId != null)
        {
            var eski = Proje.TumBusbarlar.FirstOrDefault(b => b.Id == pano.BaglandigiBusbarId);
            eski?.CikisKutulari.RemoveAll(k => k.BagliPanoAd == pano.Ad);
            if (eski != null) BusbarYenidenDuzenle(eski);
        }

        // Üst pano referansını da busbar'ın üst panosuna ayarla
        if (bbUstPano != null && pano.UstPano != bbUstPano)
        {
            if (pano.UstPano != null) pano.UstPano.AltPanolar.Remove(pano);
            pano.UstPano = bbUstPano;
            pano.UstPanoAd = bbUstPano.Ad;
            if (!bbUstPano.AltPanolar.Contains(pano))
                bbUstPano.AltPanolar.Add(pano);
        }

        pano.BaglantiTipi = BaglantiTipi.Busbar;
        pano.BaglandigiBusbarId = bb.Id;
        pano.CikisKutusuSigortaA = cikisSigortaA;
        pano.KabloMesafeM = kabloKutuMesafeM;

        bb.CikisKutulari.Add(new BusbarCikisKutusu
        {
            Ad = $"{bb.Ad}-K{bb.CikisKutulari.Count + 1}",
            PozisyonM = pozisyon,
            KatPozisyonM = katPozisyonM,
            SigortaA = cikisSigortaA,
            KabloMesafeM = kabloKutuMesafeM,
            BagliPanoAd = pano.Ad
        });

        // Pozisyona göre sırala, kutu adlarını yeniden numaralandır, hat uzunluğunu gerekirse büyüt
        BusbarYenidenDuzenle(bb);
    }

    public void BusbarKatYerlesiminiUygula(BusbarHatti bb, IEnumerable<BusbarKatBolumu> katBolumleri)
    {
        BusbarYerlesimServisi.Uygula(Proje, bb, katBolumleri);
        BusbarYenidenDuzenle(bb);
        TumunuHesapla();
    }

    /// <summary>
    /// Bir panoyu ve tüm alt ağacını siler. Ağaç bağlarının yanı sıra busbar (panoyu besleyen
    /// çıkış kutusu + bu panodan çıkan busbarlar), jeneratör grubu/tekil jeneratör, UPS ve orta
    /// gerilim referanslarını da temizler; modelde dangling busbar/grup/cihaz referansı bırakmaz.
    /// </summary>
    public void PanoSil(Pano pano)
    {
        // Alt ağacı derinden yukarı sil — her çocuk kendi busbar/cihaz referansını da temizler.
        foreach (var alt in pano.AltPanolar.ToList())
            PanoSil(alt);

        // 1) Bu pano bir busbar'dan besleniyorsa: ilgili çıkış kutusunu busbar'dan kaldır.
        if (!string.IsNullOrEmpty(pano.BaglandigiBusbarId))
        {
            var besleyenBb = Proje.TumBusbarlar.FirstOrDefault(b => b.Id == pano.BaglandigiBusbarId);
            if (besleyenBb != null)
            {
                besleyenBb.CikisKutulari.RemoveAll(k => k.BagliPanoAd == pano.Ad);
                BusbarYenidenDuzenle(besleyenBb);
            }
        }

        // 2) Bu panodan çıkan busbarları tamamen sil (bağlı panolar yukarıda zaten silindi).
        foreach (var cikan in Proje.TumBusbarlar.Where(b => b.UstPanoAd == pano.Ad).ToList())
            BusbarSil(cikan);

        // 3) Kontrol panosu buton/kablo referansları.
        KontrolReferanslariniPanoIcinSil(pano.Ad);

        // 4) Ağaç bağları.
        if (pano.UstPano != null)
            pano.UstPano.AltPanolar.Remove(pano);
        Panolar.Remove(pano);
        Proje.TumPanolar.Remove(pano);

        // 5) Jeneratör — bu panoya bağlı grup/tekil jeneratörleri kaldır.
        Proje.JeneratorGruplari.RemoveAll(g => g.AnaPanoAd == pano.Ad);
        if (Proje.Jenerator != null && Proje.Jenerator.AnaPanoAd == pano.Ad)
            Proje.Jenerator = null;

        // 6) UPS — kritik panosu ya da besleyeni bu pano olanları kaldır.
        Proje.Upsler.RemoveAll(u => u.KritikPanoAd == pano.Ad || u.BesleyenPanoAd == pano.Ad);

        // 7) Orta gerilim — ana panosu bu panoysa referansı düşür.
        if (Proje.OrtaGerilim != null &&
            string.Equals(Proje.OrtaGerilim.AnaPanoAd, pano.Ad, StringComparison.OrdinalIgnoreCase))
            Proje.OrtaGerilim.AnaPanoAd = "";

        // 8) Kök/seçili pano.
        if (Proje.KokPano == pano) Proje.KokPano = null;
        if (SeciliPano == pano) SeciliPano = null;
    }

    /// <summary>
    /// Bir panoyu yeniden adlandırır ve panonun adına ad ile bağlı tüm referansları günceller:
    /// çocuk panoların UstPanoAd'ı, busbar üst pano + çıkış kutusu adları, jeneratör/UPS referansları.
    /// Aksi halde kaydedip yükleyince bağlantılar kopar.
    /// </summary>
    public void PanoyuYenidenAdlandir(Pano pano, string yeniAd)
    {
        yeniAd = (yeniAd ?? "").Trim();
        if (pano.PanoTipi == PanoTipi.AnaPano)
            yeniAd = "Ana Pano";
        if (string.IsNullOrWhiteSpace(yeniAd) || yeniAd == pano.Ad) return;
        if (!PanoAdiKullanilabilir(yeniAd, pano))
            throw new InvalidOperationException($"'{yeniAd}' adlı pano zaten var. Pano adları benzersiz olmalıdır.");
        string eski = pano.Ad;
        pano.Ad = yeniAd;

        // Çocuk panoların üst pano referansı (hem nesne hem ad ile eşleşenler)
        foreach (var p in Proje.TumPanolar)
            if (p.UstPano == pano || p.UstPanoAd == eski)
                p.UstPanoAd = yeniAd;

        // Busbar üst pano + çıkış kutusu referansları
        foreach (var b in Proje.TumBusbarlar)
        {
            if (b.UstPanoAd == eski) b.UstPanoAd = yeniAd;
            foreach (var k in b.CikisKutulari)
                if (k.BagliPanoAd == eski) k.BagliPanoAd = yeniAd;
        }

        // Jeneratör referansları
        if (Proje.Jenerator != null)
        {
            if (Proje.Jenerator.AnaPanoAd == eski) Proje.Jenerator.AnaPanoAd = yeniAd;
            if (Proje.Jenerator.TransferPanoAd == eski) Proje.Jenerator.TransferPanoAd = yeniAd;
        }
        foreach (var grup in Proje.JeneratorGruplari)
        {
            if (grup.AnaPanoAd == eski) grup.AnaPanoAd = yeniAd;
            if (grup.SenkronPanoAd == eski) grup.SenkronPanoAd = yeniAd;
            foreach (var jen in grup.Jeneratorler)
            {
                if (jen.AnaPanoAd == eski) jen.AnaPanoAd = yeniAd;
                if (jen.TransferPanoAd == eski) jen.TransferPanoAd = yeniAd;
            }
        }

        // UPS referansları
        foreach (var u in Proje.Upsler)
        {
            if (u.BesleyenPanoAd == eski) u.BesleyenPanoAd = yeniAd;
            if (u.KritikPanoAd == eski) u.KritikPanoAd = yeniAd;
        }

        // Orta gerilim sistemi referansı
        if (Proje.OrtaGerilim != null && Proje.OrtaGerilim.AnaPanoAd == eski)
            Proje.OrtaGerilim.AnaPanoAd = yeniAd;

        // Kontrol panosu buton ve sinyal kablosu kaynak pano referansları
        foreach (var kontrol in Proje.TumPanolar.Where(p => p.PanoTipi == PanoTipi.KontrolPanosu))
        {
            foreach (var atama in kontrol.KontrolButonlari)
                if (string.Equals(atama.KaynakPanoAd, eski, StringComparison.OrdinalIgnoreCase))
                    atama.KaynakPanoAd = yeniAd;
            foreach (var kablo in kontrol.KontrolSinyalKablolari)
                if (string.Equals(kablo.KaynakPanoAd, eski, StringComparison.OrdinalIgnoreCase))
                    kablo.KaynakPanoAd = yeniAd;
        }
    }

    public bool PanoAdiKullanilabilir(string? ad, Pano? haric = null)
    {
        ad = (ad ?? "").Trim();
        return !string.IsNullOrWhiteSpace(ad) && !Proje.TumPanolar.Any(p =>
            p != haric && string.Equals(p.Ad, ad, StringComparison.OrdinalIgnoreCase));
    }

    public void PanoTipiniDegistir(Pano pano, PanoTipi yeniTip)
    {
        if (pano.PanoTipi == yeniTip) return;
        if (pano.PanoTipi == PanoTipi.AnaPano)
            throw new InvalidOperationException("Ana pano başka bir pano tipine dönüştürülemez.");
        if (pano.PanoTipi is PanoTipi.UpsPanosu or PanoTipi.TransferPanosu)
            throw new InvalidOperationException("UPS ve transfer panosu tipi bağlı sistem üzerinden değiştirilmelidir.");
        if (yeniTip == PanoTipi.AnaPano && Proje.TumPanolar.Any(p =>
                p != pano && p.PanoTipi == PanoTipi.AnaPano))
            throw new InvalidOperationException("Projede yalnızca 1 adet Ana Pano olabilir.");

        pano.PanoTipi = yeniTip;
        if (yeniTip == PanoTipi.AnaPano)
        {
            if (!string.IsNullOrEmpty(pano.BaglandigiBusbarId))
            {
                var eskiBusbar = Proje.TumBusbarlar.FirstOrDefault(b => b.Id == pano.BaglandigiBusbarId);
                eskiBusbar?.CikisKutulari.RemoveAll(k =>
                    string.Equals(k.BagliPanoAd, pano.Ad, StringComparison.OrdinalIgnoreCase));
                if (eskiBusbar != null) BusbarYenidenDuzenle(eskiBusbar);
            }
            pano.UstPano?.AltPanolar.Remove(pano);
            pano.UstPano = null;
            pano.UstPanoAd = "";
            pano.BaglantiTipi = BaglantiTipi.Kablo;
            pano.BaglandigiBusbarId = null;
            pano.CikisKutusuSigortaA = 0;
            PanoyuYenidenAdlandir(pano, "Ana Pano");
            Proje.KokPano = pano;
        }
    }

    /// <summary><paramref name="hedef"/>, <paramref name="kok"/> panonun alt ağacında mı? (döngü kontrolü)</summary>
    public bool AltAgactaMi(Pano kok, Pano hedef)
    {
        foreach (var c in kok.AltPanolar)
            if (c == hedef || AltAgactaMi(c, hedef)) return true;
        return false;
    }

    /// <summary>
    /// Kablolu bir panonun bağlı olduğu üst panoyu değiştirir. Varsa eski busbar bağlantısı
    /// temizlenir ve bağlantı kabloya çevrilir. Döngü (kendini/alt ağacını üst pano yapma) engellenir.
    /// </summary>
    public bool UstPanoyuDegistir(Pano pano, Pano yeniUst)
    {
        if (pano == yeniUst) return false;
        if (pano.PanoTipi == PanoTipi.KontrolPanosu || yeniUst.PanoTipi == PanoTipi.KontrolPanosu) return false;
        if (AltAgactaMi(pano, yeniUst)) return false; // döngü olur

        // Eski busbar bağlantısını temizle
        if (pano.BaglandigiBusbarId != null)
        {
            var eski = Proje.TumBusbarlar.FirstOrDefault(b => b.Id == pano.BaglandigiBusbarId);
            eski?.CikisKutulari.RemoveAll(k => k.BagliPanoAd == pano.Ad);
            if (eski != null) BusbarYenidenDuzenle(eski);
            pano.BaglandigiBusbarId = null;
            pano.CikisKutusuSigortaA = 0;
        }

        // Eski üst panodan çıkar, yeni üst panoya bağla
        if (pano.UstPano != null) pano.UstPano.AltPanolar.Remove(pano);
        pano.UstPano = yeniUst;
        pano.UstPanoAd = yeniUst.Ad;
        pano.BaglantiTipi = BaglantiTipi.Kablo;
        if (!yeniUst.AltPanolar.Contains(pano)) yeniUst.AltPanolar.Add(pano);
        return true;
    }

    public void BusbarYenidenDuzenle(BusbarHatti bb)
    {
        var sirali = bb.CikisKutulari.OrderBy(k => k.PozisyonM).ToList();
        bb.CikisKutulari.Clear();
        for (int i = 0; i < sirali.Count; i++)
        {
            sirali[i].Ad = $"{bb.Ad}-K{i + 1}";
            bb.CikisKutulari.Add(sirali[i]);
        }
        bb.ToplamUzunlukM = bb.KatBolumleri.Count > 0
            ? bb.KatBolumleri.Sum(k => Math.Max(0, k.UzunlukM))
            : Math.Max(Math.Max(0, bb.ToplamUzunlukM), BusbarMinUzunluk(bb));
    }

    private static double BusbarMinUzunluk(BusbarHatti bb)
    {
        if (bb.CikisKutulari.Count == 0) return 0;
        return bb.CikisKutulari.Max(k => k.PozisyonM) + 0.5;
    }

    public void LinyeEkle()
    {
        if (SeciliPano == null) return;
        if (SeciliPano.PanoTipi == PanoTipi.KontrolPanosu) return;

        var linye = new Linye
        {
            LinyeNo = string.IsNullOrWhiteSpace(YeniLinyeNo)
                ? SiradakiLinyeNoBul(SeciliPano, YeniYukTipi?.Ad)
                : YeniLinyeNo,
            YukTipiAd = YeniYukTipi?.Ad ?? "Priz",
            FazTipi = YeniFazTipi,
            SortiSayisi = YeniSortiSayisi,
            BirimGucW = YeniBirimGuc,
            TalepFaktoru = YeniTalepFaktoru,
            Aciklama = YeniAciklama,
            SigortaCinsi = LinyeHesapServisi.SigortaCinsiTayin(YeniYukTipi?.SigortaCinsi ?? "B")
        };

        SeciliPano.Linyeler.Add(linye);
        LinyeleriYenidenNumaralandir(SeciliPano);
        TumunuHesapla();

        // Giriş sıfırla
        YeniLinyeNo = "";
        YeniSortiSayisi = 1;
        YeniAciklama = "";
    }

    public void LinyeSil(Linye linye)
    {
        if (SeciliPano == null) return;
        SeciliPano.Linyeler.Remove(linye);
        KontrolReferanslariniLinyeIcinSil(linye.Id);
        LinyeleriYenidenNumaralandir(SeciliPano);
        TumunuHesapla();
    }

    public Linye? LinyeTekrarlaEkle(Linye kaynak)
    {
        if (SeciliPano == null) return null;

        var kopya = LinyeKopyasiOlustur(kaynak, SiradakiLinyeNoBul(SeciliPano, kaynak.YukTipiAd));
        SeciliPano.Linyeler.Add(kopya);
        LinyeleriYenidenNumaralandir(SeciliPano);
        TumunuHesapla();
        return kopya;
    }

    // Linye panosu (Ctrl+C / Ctrl+V). Kopyalama anındaki hali saklanır: kaynak linye sonradan
    // düzenlense ya da silinse de yapıştırılan içerik değişmez. Başka panoya da yapıştırılabilir.
    private readonly List<Linye> _linyePanosu = new();

    public int PanodakiLinyeSayisi => _linyePanosu.Count;

    public int LinyeleriKopyala(IEnumerable<Linye> linyeler)
    {
        _linyePanosu.Clear();
        foreach (var l in linyeler)
            _linyePanosu.Add(LinyeKopyasiOlustur(l, l.LinyeNo));
        return _linyePanosu.Count;
    }

    /// <summary>Panodaki linyeleri seçili panoya ekler; eklenen yeni linyeleri döner.</summary>
    public List<Linye> LinyeleriYapistir()
    {
        var eklenen = new List<Linye>();
        if (SeciliPano == null || _linyePanosu.Count == 0) return eklenen;
        if (SeciliPano.PanoTipi is PanoTipi.KontrolPanosu or PanoTipi.Kompanzasyon) return eklenen;

        foreach (var kaynak in _linyePanosu)
        {
            var yeni = LinyeKopyasiOlustur(kaynak, SiradakiLinyeNoBul(SeciliPano, kaynak.YukTipiAd));
            SeciliPano.Linyeler.Add(yeni);
            eklenen.Add(yeni);
        }
        LinyeleriYenidenNumaralandir(SeciliPano);
        TumunuHesapla();
        return eklenen;
    }

    // Kontrol panosu buton atamaları kopyalanmaz: kopya yeni kimlikli bağımsız bir devredir.
    private static Linye LinyeKopyasiOlustur(Linye kaynak, string linyeNo)
    {
        return new Linye
        {
            LinyeNo = linyeNo,
            YukTipiAd = kaynak.YukTipiAd,
            FazTipi = kaynak.FazTipi,
            SortiSayisi = kaynak.SortiSayisi,
            BirimGucW = kaynak.BirimGucW,
            TalepFaktoru = kaynak.TalepFaktoru,
            Aciklama = kaynak.Aciklama,
            SigortaCinsi = kaynak.SigortaCinsi,
            BireyselKar = kaynak.BireyselKar,
            KabloMesafeM = kaynak.KabloMesafeM,
            KontaktorluMu = kaynak.KontaktorluMu,
            DarbeKesiciMi = kaynak.DarbeKesiciMi,
            TmsZorla = kaynak.TmsZorla,
            KesmeKapasitesi = kaynak.KesmeKapasitesi,
            KesmeKapasitesiManuel = kaynak.KesmeKapasitesiManuel
        };
    }

    public int LinyeleriIceAktar(IEnumerable<Linye> linyeler)
    {
        if (SeciliPano == null) return 0;
        if (SeciliPano.PanoTipi == PanoTipi.KontrolPanosu) return 0;

        var adet = 0;
        foreach (var linye in linyeler)
        {
            SeciliPano.Linyeler.Add(linye);
            adet++;
        }

        LinyeleriYenidenNumaralandir(SeciliPano);
        TumunuHesapla();
        return adet;
    }

}
