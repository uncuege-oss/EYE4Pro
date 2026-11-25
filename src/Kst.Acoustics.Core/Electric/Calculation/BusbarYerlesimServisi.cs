using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.Core.Electric.Calculation;

/// <summary>
/// Kat bazlı busbar uzunluklarını, çıkış kutularının kendi katındaki konumlarını ve hesaplarda
/// kullanılan busbar başlangıcından itibaren mutlak konumları birbirine dönüştürür.
/// </summary>
public static class BusbarYerlesimServisi
{
    public static List<string> KatlariGetir(ElektrikProje proje, BusbarHatti busbar)
    {
        var katlar = proje.Bina?.KatEtiketleri() ?? new List<string>();
        foreach (var kat in busbar.KatBolumleri.Select(k => k.Kat)
                     .Concat(proje.TumPanolar
                         .Where(p => p.BaglandigiBusbarId == busbar.Id || p.Ad == busbar.UstPanoAd)
                         .Select(p => p.Kat)))
        {
            if (!string.IsNullOrWhiteSpace(kat) && !katlar.Any(k =>
                    string.Equals(k, kat, StringComparison.OrdinalIgnoreCase)))
                katlar.Add(kat);
        }

        return katlar;
    }

    public static double KatUzunlugu(BusbarHatti busbar, string? kat)
        => busbar.KatBolumleri.FirstOrDefault(k =>
            string.Equals(k.Kat, kat, StringComparison.OrdinalIgnoreCase))?.UzunlukM ?? 0;

    public static double MutlakPozisyonHesapla(ElektrikProje proje, BusbarHatti busbar,
        string? hedefKat, double katPozisyonM)
    {
        katPozisyonM = Math.Max(0, katPozisyonM);
        var katlar = KatlariGetir(proje, busbar);
        var kaynakKat = proje.TumPanolar.FirstOrDefault(p =>
            string.Equals(p.Ad, busbar.UstPanoAd, StringComparison.OrdinalIgnoreCase))?.Kat ?? "Z";
        hedefKat = string.IsNullOrWhiteSpace(hedefKat) ? kaynakKat : hedefKat;

        int kaynak = katlar.FindIndex(k => string.Equals(k, kaynakKat, StringComparison.OrdinalIgnoreCase));
        int hedef = katlar.FindIndex(k => string.Equals(k, hedefKat, StringComparison.OrdinalIgnoreCase));
        if (kaynak < 0 || hedef < 0 || kaynak == hedef)
            return katPozisyonM;

        int adim = hedef > kaynak ? 1 : -1;
        double sonuc = 0;
        for (int i = kaynak; i != hedef; i += adim)
            sonuc += KatUzunlugu(busbar, katlar[i]);
        return sonuc + katPozisyonM;
    }

    public static void Uygula(ElektrikProje proje, BusbarHatti busbar,
        IEnumerable<BusbarKatBolumu> katBolumleri)
    {
        busbar.KatBolumleri = katBolumleri
            .Where(k => !string.IsNullOrWhiteSpace(k.Kat))
            .GroupBy(k => k.Kat.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new BusbarKatBolumu
            {
                Kat = g.First().Kat.Trim(),
                UzunlukM = Math.Max(0, g.Last().UzunlukM)
            })
            .ToList();

        foreach (var kutu in busbar.CikisKutulari)
        {
            var pano = proje.TumPanolar.FirstOrDefault(p =>
                string.Equals(p.Ad, kutu.BagliPanoAd, StringComparison.OrdinalIgnoreCase));
            double yerel = Math.Max(0, kutu.KatPozisyonM ?? kutu.PozisyonM);
            kutu.KatPozisyonM = yerel;
            kutu.PozisyonM = MutlakPozisyonHesapla(proje, busbar, pano?.Kat, yerel);
        }

        busbar.ToplamUzunlukM = busbar.KatBolumleri.Sum(k => k.UzunlukM);
    }
}
