using System;
using System.Linq;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.Core.Electric.Calculation;

/// <summary>
/// UPS (KGK) hesap servisi: kritik yüke göre UPS tayini, giriş/çıkış akımı,
/// yük yeterliliği ve akü grubu boyutlandırması (AkuHesapServisi üzerinden).
/// </summary>
public static class UpsHesapServisi
{
    /// <summary>
    /// Gerekli görünür/aktif güce uygun en küçük UPS'i kütüphaneden seçer.
    /// kVA ve kW kriterlerinin ikisini de sağlamalı.
    /// </summary>
    public static UpsTanim? UygunUpsSec(double gerekliKVA, double gerekliKW,
        ElektrikKutuphaneServisi kutuphane)
    {
        var sirali = kutuphane.Upsler.OrderBy(u => u.GucKVA).ToList();
        return sirali.FirstOrDefault(u => u.GucKVA >= gerekliKVA && u.GucKW >= gerekliKW)
               ?? sirali.LastOrDefault();
    }

    /// <summary>
    /// Tüm UPS hesabını yapar. ups.UpsTanimAd / AkuTanimAd boşsa otomatik seçer.
    /// </summary>
    public static void Hesapla(Ups ups, Pano kritikPano, ElektrikProje proje,
        ElektrikKutuphaneServisi kutuphane)
    {
        double V = proje.SistemGerilimV > 0 ? proje.SistemGerilimV : 400;
        double kok3 = Math.Sqrt(3);

        // 1) Kritik yük
        ups.KritikYukKW = kritikPano.ToplamGucW / 1000.0;
        ups.KritikYukKVA = kritikPano.ToplamGorunurVA / 1000.0;
        ups.GerekliKVA = ups.KritikYukKVA * ups.EmniyetFaktoru;
        double gerekliKW = ups.KritikYukKW * ups.EmniyetFaktoru;

        // 2) UPS seçimi (atanmamışsa otomatik)
        UpsTanim? upsTanim = null;
        if (ups.UpsTanimManuel && !string.IsNullOrEmpty(ups.UpsTanimAd))
            upsTanim = kutuphane.Upsler.FirstOrDefault(u => u.Ad == ups.UpsTanimAd);
        if (!ups.UpsTanimManuel || upsTanim == null)
        {
            upsTanim = UygunUpsSec(ups.GerekliKVA, gerekliKW, kutuphane);
            ups.UpsTanimAd = upsTanim?.Ad ?? "";
        }

        // 3) Yük yeterliliği + akımlar
        ups.SeciliUpsGucKVA = upsTanim?.GucKVA ?? 0;
        if (upsTanim != null)
        {
            ups.YukYeterliMi = upsTanim.GucKVA >= ups.GerekliKVA && upsTanim.GucKW >= gerekliKW;

            // Çıkış akımı — gerçek kritik yük kVA'sından
            ups.CikisAkimA = ups.KritikYukKVA > 0
                ? (ups.KritikYukKVA * 1000.0) / (kok3 * V)
                : 0;

            // Giriş akımı — çıkış aktif gücü / verim, akü şarj payı eklenir, giriş cosφ ile
            double eta = Math.Clamp(upsTanim.VerimYuzde, 50, 100) / 100.0;
            double girisGucW = (ups.KritikYukKW * 1000.0) / eta * (1 + ups.SarjPayiYuzde / 100.0);
            double girisCosF = upsTanim.GirisCosF > 0 ? upsTanim.GirisCosF : 0.99;
            ups.GirisAkimA = girisGucW / (kok3 * V * girisCosF);
        }
        else
        {
            ups.YukYeterliMi = false;
            ups.CikisAkimA = 0;
            ups.GirisAkimA = 0;
        }

        // 4) Akü grubu boyutlandırması
        AkuTanim? akuTanim = null;
        if (!string.IsNullOrEmpty(ups.AkuTanimAd))
            akuTanim = kutuphane.Akuler.FirstOrDefault(a => a.Ad == ups.AkuTanimAd);
        if (akuTanim == null)
        {
            // Varsayılan: ilk akü tipi (kullanıcı sonra değiştirebilir)
            akuTanim = kutuphane.Akuler.FirstOrDefault();
            ups.AkuTanimAd = akuTanim?.Ad ?? "";
        }

        if (upsTanim != null && akuTanim != null)
        {
            AkuHesapServisi.Hesapla(ups, upsTanim, akuTanim);
        }
        else
        {
            ups.AkuYeterliMi = false;
        }
    }

    /// <summary>
    /// Projedeki tüm UPS'leri hesaplar — her UPS'in kritik panosunu bulur ve hesabını yapar.
    /// </summary>
    public static void HesaplaTumu(ElektrikProje proje, ElektrikKutuphaneServisi kutuphane)
    {
        foreach (var ups in proje.Upsler)
        {
            var kritikPano = proje.TumPanolar.FirstOrDefault(p => p.Ad == ups.KritikPanoAd);
            if (kritikPano != null)
                Hesapla(ups, kritikPano, proje, kutuphane);
        }
    }
}
