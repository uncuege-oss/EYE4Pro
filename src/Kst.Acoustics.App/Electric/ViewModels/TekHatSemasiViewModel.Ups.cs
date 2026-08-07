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
    // === UPS (KGK) Yönetimi ===

    public bool UpsVar => Proje.Upsler.Count > 0;

    /// <summary>
    /// Verilen panonun beslediği ilk UPS (varsa). Bir besleyen pano birden fazla UPS besleyebildiğinden
    /// "tekil" anlamı taşımaz; belirli bir UPS için kritik pano üzerinden <see cref="KritikPanoUps"/> kullanın.
    /// </summary>
    public Ups? UpsBul(Pano besleyenPano)
        => Proje.Upsler.FirstOrDefault(u => u.BesleyenPanoAd == besleyenPano.Ad);

    /// <summary>Verilen panonun bu UPS'in kritik panosu olup olmadığını döner.</summary>
    public Ups? KritikPanoUps(Pano pano)
        => Proje.Upsler.FirstOrDefault(u => u.KritikPanoAd == pano.Ad);

    /// <summary>Verilen UPS'e ait kritik panoyu döner (rozetten besleyen panoya ulaşmak için).</summary>
    public Pano? UpsKritikPanoBul(Ups ups)
        => Proje.TumPanolar.FirstOrDefault(p => p.Ad == ups.KritikPanoAd);

    /// <summary>
    /// Verilen panonun altına UPS ile beslenen bir kritik pano ekler. Kritik devreler
    /// (acil aydınlatma, sunucu, vb.) bu panoya linye/alt pano olarak taşınır.
    /// UPS/akü atanmamışsa hesapta otomatik seçilir.
    /// </summary>
    public Ups UpsEkle(Pano besleyenPano, int otonomiDk = 10, double emniyetFaktoru = 1.25,
        UpsTanim? upsTanim = null, AkuTanim? akuTanim = null)
    {
        // Bir besleyen pano birden fazla bağımsız UPS kritik panosu besleyebilir; kritik pano adı
        // aşağıda otomatik benzersizleştirilir (UPS, besleyen değil kritik panoya göre çözülür).

        // Benzersiz kritik pano adı
        string ad = $"{besleyenPano.Ad}-Kritik";
        int n = 2;
        while (Proje.TumPanolar.Any(p => p.Ad == ad)) ad = $"{besleyenPano.Ad}-Kritik{n++}";

        var kritikPano = new Pano
        {
            Ad = ad,
            Kat = besleyenPano.Kat,
            PanoTipi = PanoTipi.UpsPanosu,
            UstPano = besleyenPano,
            UstPanoAd = besleyenPano.Ad,
            KabloMesafeM = 10
        };
        besleyenPano.AltPanolar.Add(kritikPano);
        Panolar.Add(kritikPano);
        Proje.TumPanolar.Add(kritikPano);

        var ups = new Ups
        {
            BesleyenPanoAd = besleyenPano.Ad,
            KritikPanoAd = kritikPano.Ad,
            OtonomiDk = otonomiDk,
            EmniyetFaktoru = emniyetFaktoru,
            UpsTanimAd = upsTanim?.Ad ?? "",
            AkuTanimAd = akuTanim?.Ad ?? ""
        };
        Proje.Upsler.Add(ups);

        TumunuHesapla();
        OnPropertyChanged(nameof(UpsVar));
        return ups;
    }

    /// <summary>
    /// Mevcut bir panoyu, üst panosundan UPS ile beslenen bir kritik panoya
    /// (<see cref="PanoTipi.UpsPanosu"/>) dönüştürür. Panonun linyeleri ve alt panoları korunur;
    /// yeni kritik pano yaratılmaz. UPS/akü hesapta otomatik seçilir, sonra düzenlenebilir.
    /// </summary>
    public Ups KritikPanoyaCevir(Pano pano, int otonomiDk = 10, double emniyetFaktoru = 1.25)
    {
        if (pano.UstPano == null || pano.PanoTipi == PanoTipi.AnaPano)
            throw new System.InvalidOperationException("Ana/kök pano kritik panoya çevrilemez (UPS bir üst panodan beslenir).");
        if (pano.PanoTipi == PanoTipi.TransferPanosu)
            throw new System.InvalidOperationException("Transfer panosu kritik panoya çevrilemez.");
        if (pano.PanoTipi == PanoTipi.UpsPanosu || KritikPanoUps(pano) != null)
            throw new System.InvalidOperationException("Bu pano zaten bir UPS kritik panosu.");

        // Bir üst pano birden fazla bağımsız kritik panoyu (her biri kendi UPS'iyle) besleyebilir.
        // UPS, besleyen pano değil kritik pano üzerinden tekil çözüldüğünden ek kısıt gerekmez.
        var besleyen = pano.UstPano;

        pano.PanoTipi = PanoTipi.UpsPanosu;
        var ups = new Ups
        {
            BesleyenPanoAd = besleyen.Ad,
            KritikPanoAd = pano.Ad,
            OtonomiDk = otonomiDk,
            EmniyetFaktoru = emniyetFaktoru
        };
        Proje.Upsler.Add(ups);

        TumunuHesapla();
        OnPropertyChanged(nameof(UpsVar));
        return ups;
    }

    /// <summary>
    /// UPS'i kaldırır. Kritik pano silinmez; normal tali panoya dönüştürülür (yükler korunur).
    /// </summary>
    public void UpsSil(Ups ups)
    {
        var kritikPano = Proje.TumPanolar.FirstOrDefault(p => p.Ad == ups.KritikPanoAd);
        if (kritikPano != null && kritikPano.PanoTipi == PanoTipi.UpsPanosu)
            kritikPano.PanoTipi = PanoTipi.TaliPano;

        Proje.Upsler.Remove(ups);
        TumunuHesapla();
        OnPropertyChanged(nameof(UpsVar));
    }

    public void UpsTipiAyarla(Ups ups, UpsTanim? tip)
    {
        ups.UpsTanimAd = tip?.Ad ?? "";
        TumunuHesapla();
    }

    public void UpsAkuAyarla(Ups ups, AkuTanim? aku)
    {
        ups.AkuTanimAd = aku?.Ad ?? "";
        TumunuHesapla();
    }

    public void UpsOtonomiAyarla(Ups ups, int dakika)
    {
        if (dakika <= 0) return;
        ups.OtonomiDk = dakika;
        TumunuHesapla();
    }

    public void UpsEmniyetAyarla(Ups ups, double faktor)
    {
        if (faktor < 1.0) return;
        ups.EmniyetFaktoru = faktor;
        TumunuHesapla();
    }

    // === UPS Topolojisi (proje geneli: Lokal / Merkezi) ===

    public UpsTopolojisi UpsTopolojisi => Proje.UpsTopolojisi;
    public bool MerkeziModda => Proje.UpsTopolojisi == UpsTopolojisi.Merkezi;

    /// <summary>Proje UPS topolojisini ayarlar (mevcut UPS yapısına dokunmaz; yalnız mod değişir).</summary>
    public void UpsTopolojisiAyarla(UpsTopolojisi t)
    {
        if (Proje.UpsTopolojisi == t) return;
        Proje.UpsTopolojisi = t;
        OnPropertyChanged(nameof(UpsTopolojisi));
        OnPropertyChanged(nameof(MerkeziModda));
        OnPropertyChanged(nameof(MerkeziUpsVar));
    }

    /// <summary>Merkezi UPS cihazı (varsa).</summary>
    public Ups? MerkeziUps() => Proje.Upsler.FirstOrDefault(u => u.Merkezi);
    public bool MerkeziUpsVar => MerkeziUps() != null;

    /// <summary>Merkezi UPS dağıtım panosu (merkezi UPS'in kritik panosu).</summary>
    public Pano? MerkeziUpsDagitimPanosu()
    {
        var u = MerkeziUps();
        return u == null ? null : Proje.TumPanolar.FirstOrDefault(p => p.Ad == u.KritikPanoAd);
    }

    /// <summary>Verilen pano merkezi UPS dağıtım panosu mu?</summary>
    public bool MerkeziDagitimPanosuMu(Pano pano) => MerkeziUps()?.KritikPanoAd == pano.Ad;

    /// <summary>
    /// Merkezi UPS yapısını kurar: ana pano altına "UPS Dağıtım" panosu + merkezi UPS cihazı.
    /// Kat kritik panoları sonra dağıtımın altına eklenir.
    /// </summary>
    public Ups MerkeziUpsKur(int otonomiDk = 10, double emniyetFaktoru = 1.25)
    {
        var ana = Proje.KokPano ?? Proje.TumPanolar.FirstOrDefault(p => p.PanoTipi == PanoTipi.AnaPano);
        if (ana == null)
            throw new System.InvalidOperationException("Merkezi UPS için önce bir Ana Pano oluşturun.");

        var mevcut = MerkeziUps();
        if (mevcut != null) return mevcut;
        if (UpsBul(ana) != null)
            throw new System.InvalidOperationException("Ana pano zaten bir UPS besliyor; merkezi UPS kurmadan önce onu kaldırın.");

        string ad = "UPS Dağıtım";
        int n = 2;
        while (Proje.TumPanolar.Any(p => p.Ad == ad)) ad = $"UPS Dağıtım {n++}";

        var dagitim = new Pano
        {
            Ad = ad, Kat = ana.Kat, PanoTipi = PanoTipi.UpsPanosu,
            UstPano = ana, UstPanoAd = ana.Ad, KabloMesafeM = 10
        };
        ana.AltPanolar.Add(dagitim);
        Panolar.Add(dagitim);
        Proje.TumPanolar.Add(dagitim);

        var ups = new Ups
        {
            Merkezi = true,
            BesleyenPanoAd = ana.Ad,
            KritikPanoAd = dagitim.Ad,
            OtonomiDk = otonomiDk,
            EmniyetFaktoru = emniyetFaktoru
        };
        Proje.Upsler.Add(ups);

        TumunuHesapla();
        OnPropertyChanged(nameof(UpsVar));
        OnPropertyChanged(nameof(MerkeziUpsVar));
        return ups;
    }

    /// <summary>
    /// Merkezi UPS yapısını kaldırır. Dağıtım panosu ve altındaki kat kritik panoları silinmez;
    /// normal tali panoya dönüştürülür (yükler/ağaç korunur).
    /// </summary>
    public void MerkeziUpsKaldir()
    {
        var ups = MerkeziUps();
        if (ups == null) return;
        var dagitim = Proje.TumPanolar.FirstOrDefault(p => p.Ad == ups.KritikPanoAd);
        if (dagitim != null)
        {
            dagitim.PanoTipi = PanoTipi.TaliPano;
            foreach (var alt in AltAgaciTopla(dagitim))
                if (alt.PanoTipi == PanoTipi.UpsPanosu) alt.PanoTipi = PanoTipi.TaliPano;
        }
        Proje.Upsler.Remove(ups);
        TumunuHesapla();
        OnPropertyChanged(nameof(UpsVar));
        OnPropertyChanged(nameof(MerkeziUpsVar));
    }

    /// <summary>
    /// Merkezi modda dağıtım panosunun altına belirli bir kata kritik pano (UpsPanosu) ekler.
    /// </summary>
    public Pano MerkeziKatKritikPanosuEkle(string kat, string? ad = null)
    {
        var dagitim = MerkeziUpsDagitimPanosu()
            ?? throw new System.InvalidOperationException("Önce Merkezi UPS kurun.");

        string panoAd = ad ?? "";
        if (string.IsNullOrWhiteSpace(panoAd))
        {
            panoAd = $"{kat}-Kritik";
            int n = 2;
            while (Proje.TumPanolar.Any(p => p.Ad == panoAd)) panoAd = $"{kat}-Kritik{n++}";
        }

        var pano = new Pano
        {
            Ad = panoAd, Kat = kat, PanoTipi = PanoTipi.UpsPanosu,
            UstPano = dagitim, UstPanoAd = dagitim.Ad, KabloMesafeM = 10
        };
        dagitim.AltPanolar.Add(pano);
        Panolar.Add(pano);
        Proje.TumPanolar.Add(pano);
        TumunuHesapla();
        return pano;
    }

    /// <summary>Belirli bir kattaki kritik panolar (merkezi dağıtım panosu hariç).</summary>
    public List<Pano> KattakiKritikPanolar(string kat)
    {
        var dagitim = MerkeziUpsDagitimPanosu();
        return Proje.TumPanolar
            .Where(p => p.PanoTipi == PanoTipi.UpsPanosu && p.Kat == kat && p != dagitim)
            .ToList();
    }

    /// <summary>Bir linyeyi kaynak panodan hedef kritik panoya taşır ve numaralandırmayı tazeler.</summary>
    public void LinyeyiKritikPanoyaTasi(Linye linye, Pano kaynak, Pano hedef)
    {
        if (kaynak == hedef || !kaynak.Linyeler.Contains(linye)) return;
        kaynak.Linyeler.Remove(linye);
        hedef.Linyeler.Add(linye);
        LinyeleriYenidenNumaralandir(kaynak);
        LinyeleriYenidenNumaralandir(hedef);
        TumunuHesapla();
    }

    /// <summary>Bir panonun tüm alt ağacını (kendisi hariç) düz liste olarak toplar.</summary>
    private static List<Pano> AltAgaciTopla(Pano kok)
    {
        var liste = new List<Pano>();
        void Gez(Pano p)
        {
            foreach (var alt in p.AltPanolar)
            {
                liste.Add(alt);
                Gez(alt);
            }
        }
        Gez(kok);
        return liste;
    }
}
