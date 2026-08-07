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
    // === Orta Gerilim (OG) Yönetimi ===

    public OrtaGerilimSistemi? AktifOrtaGerilim => Proje.OrtaGerilim;
    public bool OrtaGerilimVar => Proje.OrtaGerilim != null;

    /// <summary>
    /// Ana panonun üstüne OG besleme sistemi (şebeke + hücreler + trafo) ekler. Trafo
    /// atanmamışsa hesapta otomatik seçilir. Projede tek OG sistemi bulunur.
    /// </summary>
    public OrtaGerilimSistemi OrtaGerilimEkle(Pano anaPano, KoskTipi koskTipi = KoskTipi.DisMekanBetonKosk,
        double emniyetFaktoru = 1.0, TrafoTanim? trafoTanim = null)
    {
        var og = new OrtaGerilimSistemi
        {
            AnaPanoAd = anaPano.Ad,
            KoskTipi = koskTipi,
            EmniyetFaktoru = emniyetFaktoru,
            TrafoTanimAd = trafoTanim?.Ad ?? ""
        };
        Proje.OrtaGerilim = og;
        TumunuHesapla();
        OnPropertyChanged(nameof(AktifOrtaGerilim));
        OnPropertyChanged(nameof(OrtaGerilimVar));
        return og;
    }

    /// <summary>Hazır bir OG sistemini projeye uygular (ekle veya düzenle) ve yeniden hesaplar.</summary>
    public void OrtaGerilimUygula(OrtaGerilimSistemi og)
    {
        Proje.OrtaGerilim = og;
        TumunuHesapla();
        OnPropertyChanged(nameof(AktifOrtaGerilim));
        OnPropertyChanged(nameof(OrtaGerilimVar));
    }

    public void OrtaGerilimSil()
    {
        if (Proje.OrtaGerilim == null) return;
        Proje.OrtaGerilim = null;
        TumunuHesapla();
        OnPropertyChanged(nameof(AktifOrtaGerilim));
        OnPropertyChanged(nameof(OrtaGerilimVar));
    }

    public void OrtaGerilimTrafoAyarla(TrafoTanim? trafo)
    {
        if (Proje.OrtaGerilim == null) return;
        Proje.OrtaGerilim.TrafoTanimAd = trafo?.Ad ?? "";
        TumunuHesapla();
        OnPropertyChanged(nameof(AktifOrtaGerilim));
    }

    public void OrtaGerilimKoskTipiAyarla(KoskTipi tip)
    {
        if (Proje.OrtaGerilim == null) return;
        Proje.OrtaGerilim.KoskTipi = tip;
        OnPropertyChanged(nameof(AktifOrtaGerilim));
    }

    /// <summary>OG sistemine sıralı listenin sonuna bir hücre ekler.</summary>
    public OgHucre OgHucreEkle(OgHucreTanim tanim)
    {
        if (Proje.OrtaGerilim == null)
            throw new System.InvalidOperationException("Önce OG sistemi eklenmeli.");
        var h = new OgHucre
        {
            Ad = tanim.Ad,
            HucreTanimAd = tanim.Ad,
            HucreTipi = tanim.HucreTipi
        };
        Proje.OrtaGerilim.Hucreler.Add(h);
        TumunuHesapla();
        OnPropertyChanged(nameof(AktifOrtaGerilim));
        return h;
    }

    public void OgHucreSil(OgHucre hucre)
    {
        if (Proje.OrtaGerilim == null) return;
        Proje.OrtaGerilim.Hucreler.Remove(hucre);
        TumunuHesapla();
        OnPropertyChanged(nameof(AktifOrtaGerilim));
    }

    /// <summary>Hücreyi listede bir adım sola (yon=-1) veya sağa (yon=+1) taşır.</summary>
    public void OgHucreSiraDegistir(OgHucre hucre, int yon)
    {
        if (Proje.OrtaGerilim == null) return;
        var list = Proje.OrtaGerilim.Hucreler;
        int i = list.IndexOf(hucre);
        int j = i + (yon < 0 ? -1 : 1);
        if (i < 0 || j < 0 || j >= list.Count) return;
        (list[i], list[j]) = (list[j], list[i]);
        TumunuHesapla();
        OnPropertyChanged(nameof(AktifOrtaGerilim));
    }

    /// <summary>OG şebeke parametrelerini ayarlar (gerilim kV, kısa devre MVA, Ik kA).</summary>
    public void OrtaGerilimSebekeAyarla(double ogGerilimKV, double sebekeMVA, double sebekeIkKA)
    {
        if (Proje.OrtaGerilim == null) return;
        Proje.OrtaGerilim.OgGerilimKV = ogGerilimKV;
        Proje.OrtaGerilim.SebekeKisaDevreMVA = sebekeMVA;
        Proje.OrtaGerilim.SebekeIkKA = sebekeIkKA;
        TumunuHesapla();
        OnPropertyChanged(nameof(AktifOrtaGerilim));
    }

    /// <summary>Trafo → ana pano çıkış bağlantısını ayarlar.</summary>
    public void OrtaGerilimCikisBaglantiAyarla(TrafoCikisBaglantiTipi tip, string referansAd,
        double uzunlukM, int paralelKol)
    {
        if (Proje.OrtaGerilim == null) return;
        var b = Proje.OrtaGerilim.CikisBaglanti;
        b.Tip = tip;
        b.ReferansAd = referansAd ?? "";
        b.UzunlukM = uzunlukM;
        b.ParalelKolSayisi = System.Math.Max(1, paralelKol);
        TumunuHesapla();
        OnPropertyChanged(nameof(AktifOrtaGerilim));
    }

    public void OrtaGerilimEmniyetFaktoruAyarla(double faktor)
    {
        if (Proje.OrtaGerilim == null || faktor <= 0) return;
        Proje.OrtaGerilim.EmniyetFaktoru = faktor;
        TumunuHesapla();
        OnPropertyChanged(nameof(AktifOrtaGerilim));
    }

}
