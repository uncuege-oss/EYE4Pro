using System;
using System.Linq;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.Core.Electric.Calculation;

/// <summary>
/// Busbar hesap servisi — toplam akım, gerilim düşümü ve ısı kaybı.
/// </summary>
public static class BusbarHesapServisi
{
    /// <summary>
    /// Bir busbar hattı için toplam akım, gerilim düşümü ve ısı kaybı hesabı.
    /// </summary>
    public static void Hesapla(BusbarHatti busbar, ElektrikProje proje, ElektrikKutuphaneServisi kutuphane)
    {
        // Çıkış kutularına bağlı panoların akımlarını topla
        double toplamI = 0;
        foreach (var kutu in busbar.CikisKutulari)
        {
            var bagliPano = proje.TumPanolar.FirstOrDefault(p => p.Ad == kutu.BagliPanoAd);
            if (bagliPano != null)
                toplamI += bagliPano.GirisAkimA;
        }
        busbar.ToplamAkimA = toplamI;

        if (busbar.OtomatikTipSec)
        {
            var uygun = UygunBusbarTipiSec(toplamI, kutuphane);
            if (uygun != null)
                busbar.BusbarTipiAd = uygun.Ad;
        }

        // Kütüphaneden busbar tipini bul
        var tip = kutuphane.BusbarTipleri.FirstOrDefault(b => b.Ad == busbar.BusbarTipiAd);
        if (tip == null)
        {
            busbar.GerilimDususYuzde = 0;
            busbar.IsiKaybiW = 0;
            return;
        }

        // Kademeli gerilim düşümü (reaktans dahil): ΔU = √3 · Σ(I_segment · L_segment) · (R'·cosφ + X'·sinφ).
        // Çıkış kutuları pozisyon sırasına göre çözülür; her segmentte akım o kutudan ayrılan yük kadar
        // azalır. Her kutuya, busbar başlangıcından kendi pozisyonuna kadarki birikimli ΔU da yazılır.
        double r_ohm_per_m = tip.DirencMOhmPerM / 1000.0;
        double iL = 0;
        double x_ohm_per_m = tip.ReaktansMOhmPerM / 1000.0;
        double cosF = NumberHelper.NormalizeCosF(proje.CosF, 0.85);
        double sinF = Math.Sqrt(Math.Max(0, 1 - cosF * cosF));
        double empedansBirim = r_ohm_per_m * cosF + x_ohm_per_m * sinF;
        double vSistem = proje.SistemGerilimV > 0 ? proje.SistemGerilimV : 400;

        var siraliKutular = busbar.CikisKutulari.OrderBy(k => k.PozisyonM).ToList();
        double oncekiPoz = 0;
        double kalanAkim = busbar.ToplamAkimA;
        foreach (var kutu in siraliKutular)
        {
            double segmentL = kutu.PozisyonM - oncekiPoz;
            iL += kalanAkim * segmentL;
            // Busbar başlangıcından bu kutu pozisyonuna kadar birikimli ΔU (kümülatif zincir için).
            kutu.GerilimDususYuzde = (Math.Sqrt(3) * iL * empedansBirim / vSistem) * 100.0;
            // Bu kutudan ayrılan akımı düş
            var bagliPano = proje.TumPanolar.FirstOrDefault(p => p.Ad == kutu.BagliPanoAd);
            if (bagliPano != null)
                kalanAkim -= bagliPano.GirisAkimA;
            oncekiPoz = kutu.PozisyonM;
        }

        double dusumV = Math.Sqrt(3) * iL * empedansBirim;
        busbar.GerilimDususYuzde = (dusumV / vSistem) * 100.0;

        // Isı kaybı: P = 3 × Σ(I_segment² × R_segment)
        double pLoss = 0;
        oncekiPoz = 0;
        kalanAkim = busbar.ToplamAkimA;
        foreach (var kutu in siraliKutular)
        {
            double segmentL = kutu.PozisyonM - oncekiPoz;
            double rSeg = r_ohm_per_m * segmentL;
            pLoss += 3.0 * kalanAkim * kalanAkim * rSeg;
            var bagliPano = proje.TumPanolar.FirstOrDefault(p => p.Ad == kutu.BagliPanoAd);
            if (bagliPano != null)
                kalanAkim -= bagliPano.GirisAkimA;
            oncekiPoz = kutu.PozisyonM;
        }
        busbar.IsiKaybiW = pLoss;
    }

    public static BusbarTanim? UygunBusbarTipiSec(double toplamAkimA, ElektrikKutuphaneServisi kutuphane)
    {
        if (kutuphane.BusbarTipleri.Count == 0) return null;
        return kutuphane.BusbarTipleri
            .Where(b => b.NominalAkimA >= toplamAkimA)
            .OrderBy(b => b.NominalAkimA)
            .ThenBy(b => b.Malzeme, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault()
            ?? kutuphane.BusbarTipleri
                .OrderByDescending(b => b.NominalAkimA)
                .FirstOrDefault();
    }
}
