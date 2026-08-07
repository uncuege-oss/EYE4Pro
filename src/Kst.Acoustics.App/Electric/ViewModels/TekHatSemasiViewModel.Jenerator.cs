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
    // === Jeneratör Yönetimi ===

    public JeneratorGrubu? AktifJeneratorGrubu => Proje.JeneratorGruplari.FirstOrDefault();
    public Jenerator? AktifJenerator => AktifJeneratorGrubu?.Jeneratorler.FirstOrDefault() ?? Proje.Jenerator;
    public bool JeneratorVar => Proje.JeneratorGruplari.Count > 0 || Proje.Jenerator != null;

    /// <summary>Verilen panoyu hedefleyen jeneratör grubu (yoksa null).</summary>
    public JeneratorGrubu? JeneratorGrubuBul(Pano? pano)
        => pano == null ? null : Proje.JeneratorGruplari.FirstOrDefault(g => g.AnaPanoAd == pano.Ad);

    /// <summary>
    /// Bir panoya tek jeneratör + senkron panosu ekler. Geriye dönük çağrılar için korunur.
    /// </summary>
    public void JeneratorEkle(Pano hedefPano, JeneratorMod mod = JeneratorMod.Standby,
        double emniyetFaktoru = 1.25, JeneratorTanim? jenTanim = null,
        TransferSalterTanim? transferSalter = null,
        JeneratorBeslemeKapsami kapsam = JeneratorBeslemeKapsami.TesisGeneli)
        => JeneratorGrubuUygula(hedefPano, mod, emniyetFaktoru,
            jenTanim == null ? Array.Empty<JeneratorTanim>() : new[] { jenTanim }, transferSalter, kapsam);

    /// <summary>
    /// Bir hedef panoya jeneratör grubu + senkron/transfer panosu uygular. Hedef ana pano ise
    /// kapsam tesis geneli, bir tali pano ise lokal bölge beslemesidir. Aynı hedefin mevcut
    /// grubu güncellenir; farklı hedeflerde birden çok grup birlikte bulunabilir.
    /// </summary>
    public void JeneratorGrubuUygula(Pano hedefPano, JeneratorMod mod,
        double emniyetFaktoru, IEnumerable<JeneratorTanim> jeneratorler,
        TransferSalterTanim? transferSalter = null,
        JeneratorBeslemeKapsami kapsam = JeneratorBeslemeKapsami.TesisGeneli,
        string? kat = null,
        IReadOnlyList<double>? jeneratorSenkronKabloMesafeleri = null,
        double? senkronHedefPanoKabloMesafeM = null,
        IReadOnlyList<bool>? jeneratorDisMekanSecimleri = null)
    {
        if (hedefPano.PanoTipi == PanoTipi.TransferPanosu)
            throw new System.InvalidOperationException("Transfer panosu jeneratör hedefi olamaz.");

        // Hedefin türüne göre kapsamı tutarlı tut: ana pano → tesis geneli, diğer → lokal bölge.
        kapsam = hedefPano.PanoTipi == PanoTipi.AnaPano
            ? JeneratorBeslemeKapsami.TesisGeneli
            : JeneratorBeslemeKapsami.LokalBolge;

        var grup = JeneratorGrubuBul(hedefPano);
        string senkronPanoAd = grup?.SenkronPanoAd ?? $"Senkron-{hedefPano.Ad}";
        int n = 2;
        while (Proje.TumPanolar.Any(p => p.Ad == senkronPanoAd && p.Ad != grup?.SenkronPanoAd))
            senkronPanoAd = $"Senkron-{hedefPano.Ad}-{n++}";

        var senkronPano = Proje.TumPanolar.FirstOrDefault(p => p.Ad == senkronPanoAd);
        if (senkronPano == null)
        {
            senkronPano = new Pano
            {
                Ad = senkronPanoAd,
                Kat = hedefPano.Kat,
                PanoTipi = PanoTipi.TransferPanosu
            };
            Panolar.Add(senkronPano);
            Proje.TumPanolar.Add(senkronPano);
        }
        else
        {
            senkronPano.Kat = hedefPano.Kat;
            senkronPano.PanoTipi = PanoTipi.TransferPanosu;
        }

        var eskiJeneratorMesafeleri = grup?.Jeneratorler
            .Select(j => j.SenkronPanoKabloMesafeM)
            .ToList() ?? new List<double>();
        var eskiDisMekanSecimleri = grup?.Jeneratorler
            .Select(j => j.DisMekandaMi)
            .ToList() ?? new List<bool>();
        grup ??= new JeneratorGrubu();
        grup.AnaPanoAd = hedefPano.Ad;
        grup.BeslemeKapsami = kapsam;
        grup.SenkronPanoAd = senkronPano.Ad;
        if (kat != null) grup.Kat = kat.Trim();   // null → mevcut katı koru; "" → hedef pano katı
        if (senkronHedefPanoKabloMesafeM.HasValue)
            grup.SenkronHedefPanoKabloMesafeM = Math.Max(0, senkronHedefPanoKabloMesafeM.Value);
        grup.Mod = mod;
        grup.EmniyetFaktoru = Math.Clamp(emniyetFaktoru, 1.0, 2.0);
        // transferSalter null ise "(otomatik tayin)" seçildi → motor ana pano akımına göre seçsin.
        grup.TransferSalterManuel = transferSalter != null;
        grup.TransferSalterAd = transferSalter?.Ad ?? "";
        grup.Jeneratorler.Clear();

        var jeneratorTanimlari = jeneratorler.ToList();
        for (var i = 0; i < jeneratorTanimlari.Count; i++)
        {
            var tanim = jeneratorTanimlari[i];
            var kabloMesafeM = jeneratorSenkronKabloMesafeleri != null && i < jeneratorSenkronKabloMesafeleri.Count
                ? jeneratorSenkronKabloMesafeleri[i]
                : i < eskiJeneratorMesafeleri.Count ? eskiJeneratorMesafeleri[i] : 10;
            bool disMekanda = jeneratorDisMekanSecimleri != null && i < jeneratorDisMekanSecimleri.Count
                ? jeneratorDisMekanSecimleri[i]
                : i < eskiDisMekanSecimleri.Count && eskiDisMekanSecimleri[i];
            grup.Jeneratorler.Add(new Jenerator
            {
                AnaPanoAd = hedefPano.Ad,
                TransferPanoAd = senkronPano.Ad,
                Mod = mod,
                EmniyetFaktoru = grup.EmniyetFaktoru,
                JeneratorTanimAd = tanim.Ad,
                TransferSalterAd = grup.TransferSalterAd,
                TransferSalterManuel = grup.TransferSalterManuel,
                Kat = grup.Kat,
                DisMekandaMi = disMekanda,
                SenkronPanoKabloMesafeM = Math.Max(0, kabloMesafeM)
            });
        }

        if (!Proje.JeneratorGruplari.Contains(grup))
            Proje.JeneratorGruplari.Add(grup);
        Proje.Jenerator = Proje.JeneratorGruplari.FirstOrDefault()?.Jeneratorler.FirstOrDefault();

        TumunuHesapla();
        OnPropertyChanged(nameof(AktifJenerator));
        OnPropertyChanged(nameof(AktifJeneratorGrubu));
        OnPropertyChanged(nameof(JeneratorVar));
    }

    /// <summary>Belirli bir hedef panonun jeneratör grubunu ve senkron/transfer panosunu siler.</summary>
    public void JeneratorGrubuSil(Pano hedefPano)
    {
        var grup = JeneratorGrubuBul(hedefPano);
        if (grup == null) return;

        var senkron = Proje.TumPanolar.FirstOrDefault(p => p.Ad == grup.SenkronPanoAd);
        if (senkron != null)
        {
            Panolar.Remove(senkron);
            Proje.TumPanolar.Remove(senkron);
        }
        Proje.JeneratorGruplari.Remove(grup);
        Proje.Jenerator = Proje.JeneratorGruplari.FirstOrDefault()?.Jeneratorler.FirstOrDefault();

        TumunuHesapla();
        OnPropertyChanged(nameof(AktifJenerator));
        OnPropertyChanged(nameof(AktifJeneratorGrubu));
        OnPropertyChanged(nameof(JeneratorVar));
    }

    /// <summary>Tüm jeneratör gruplarını ve senkron/transfer panolarını siler.</summary>
    public void JeneratorSil()
    {
        var panolar = Proje.JeneratorGruplari
            .Select(g => g.SenkronPanoAd)
            .Append(Proje.Jenerator?.TransferPanoAd ?? "")
            .Where(ad => !string.IsNullOrWhiteSpace(ad))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var ad in panolar)
        {
            var pano = Proje.TumPanolar.FirstOrDefault(p => p.Ad == ad);
            if (pano != null)
            {
                Panolar.Remove(pano);
                Proje.TumPanolar.Remove(pano);
            }
        }

        Proje.JeneratorGruplari.Clear();
        Proje.Jenerator = null;
        TumunuHesapla();
        OnPropertyChanged(nameof(AktifJenerator));
        OnPropertyChanged(nameof(AktifJeneratorGrubu));
        OnPropertyChanged(nameof(JeneratorVar));
    }

    public void JeneratorTipiAyarla(JeneratorTanim? tip)
    {
        var grup = AktifJeneratorGrubu;
        if (grup != null)
        {
            if (grup.Jeneratorler.Count == 0)
                grup.Jeneratorler.Add(new Jenerator { AnaPanoAd = grup.AnaPanoAd, TransferPanoAd = grup.SenkronPanoAd });
            grup.Jeneratorler[0].JeneratorTanimAd = tip?.Ad ?? "";
            Proje.Jenerator = grup.Jeneratorler.FirstOrDefault();
        }
        else if (Proje.Jenerator != null)
        {
            Proje.Jenerator.JeneratorTanimAd = tip?.Ad ?? "";
        }
        else return;
        TumunuHesapla();
        OnPropertyChanged(nameof(AktifJenerator));
        OnPropertyChanged(nameof(AktifJeneratorGrubu));
    }

    public void JeneratorModuAyarla(JeneratorMod mod)
    {
        var grup = AktifJeneratorGrubu;
        if (grup != null)
            grup.Mod = mod;
        else if (Proje.Jenerator != null)
            Proje.Jenerator.Mod = mod;
        else return;
        TumunuHesapla();
        OnPropertyChanged(nameof(AktifJenerator));
        OnPropertyChanged(nameof(AktifJeneratorGrubu));
    }

    public void JeneratorEmniyetFaktoruAyarla(double faktor)
    {
        if (faktor < 1.0) return;
        var grup = AktifJeneratorGrubu;
        if (grup != null)
            grup.EmniyetFaktoru = faktor;
        else if (Proje.Jenerator != null)
            Proje.Jenerator.EmniyetFaktoru = faktor;
        else return;
        TumunuHesapla();
        OnPropertyChanged(nameof(AktifJenerator));
        OnPropertyChanged(nameof(AktifJeneratorGrubu));
    }

    public void JeneratorTransferSalteriAyarla(TransferSalterTanim? salter)
    {
        // salter null → "(otomatik tayin)": motor ana pano akımına göre seçsin ve yükle güncellesin.
        var grup = AktifJeneratorGrubu;
        if (grup != null)
        {
            grup.TransferSalterManuel = salter != null;
            grup.TransferSalterAd = salter?.Ad ?? "";
        }
        else if (Proje.Jenerator != null)
        {
            Proje.Jenerator.TransferSalterManuel = salter != null;
            Proje.Jenerator.TransferSalterAd = salter?.Ad ?? "";
        }
        else return;
        TumunuHesapla();
        OnPropertyChanged(nameof(AktifJenerator));
        OnPropertyChanged(nameof(AktifJeneratorGrubu));
    }

}
