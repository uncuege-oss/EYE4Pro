using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.Core.Electric.Calculation;

/// <summary>
/// Kompanzasyon hesap servisi.
/// Tetik: Toplam güç > 15kW → sadece ana panoda.
/// Kondansatör kademe sayısı: 6 / 12 / 18.
/// Reaktör aktif ise daima 3 kademe; SVC sürücü = reaktör toplam kVAr'dan büyük tek sürücü.
/// </summary>
public static class KompanzasyonHesapServisi
{
    public static bool KompanzasyonGerekliMi(Pano pano, double tetikKW = 15)
    {
        return pano.PanoTipi == PanoTipi.AnaPano && (pano.ToplamGucW / 1000.0) > tetikKW;
    }

    /// <summary>Q = P × (tanφ₁ - tanφ₂)</summary>
    public static double GerekliQHesapla(double aktifGucW, double mevcutCosF, double hedefCosF)
    {
        if (aktifGucW <= 0 || mevcutCosF <= 0 || hedefCosF <= 0) return 0;
        mevcutCosF = NumberHelper.NormalizeCosF(mevcutCosF, 0.85);
        hedefCosF = NumberHelper.NormalizeCosF(hedefCosF, 0.99);
        double fi1 = Math.Acos(mevcutCosF);
        double fi2 = Math.Acos(hedefCosF);
        double qVAR = aktifGucW * (Math.Tan(fi1) - Math.Tan(fi2));
        return Math.Max(0, qVAR);
    }

    /// <summary>
    /// Standart kondansatör kademe sayıları: 6, 12, 18.
    /// </summary>
    public static readonly int[] StandartKondansatorKademeSayilari = { 6, 12, 18 };

    /// <summary>
    /// Tüm hesabı yapar. mevcut sonuç verilmişse ve KademelerElleAyarlandi=true ise listeler korunur.
    /// </summary>
    public static KompanzasyonSonucu Hesapla(Pano pano, ElektrikProje proje,
        ElektrikKutuphaneServisi kutuphane, KompanzasyonSonucu? mevcut = null)
    {
        var sonuc = mevcut ?? new KompanzasyonSonucu();
        sonuc.ToplamAktifGucKW = pano.ToplamGucW / 1000.0;
        sonuc.MevcutCosF = NumberHelper.NormalizeCosF(proje.CosF, 0.85);
        sonuc.HedefCosF = NumberHelper.NormalizeCosF(proje.HedefCosF, 0.99);

        double qVar = GerekliQHesapla(pano.ToplamGucW, sonuc.MevcutCosF, sonuc.HedefCosF);
        sonuc.GerekliQkVar = qVar / 1000.0;

        // Kondansatör kademe sayısı normalize (6/12/18)
        if (!StandartKondansatorKademeSayilari.Contains(sonuc.KondansatorKademeSayisi))
            sonuc.KondansatorKademeSayisi = 6;

        if (!sonuc.KademelerElleAyarlandi)
        {
            KondansatorKademeleriOlustur(sonuc, kutuphane);
            if (sonuc.ReaktorAktif)
                ReaktorKademeleriOlustur(sonuc, kutuphane);
            else
                sonuc.ReaktorKademeleri.Clear();
        }

        // Toplamlar (her zaman güncel)
        sonuc.ToplamKondansatorKVar = sonuc.KondansatorKademeleri.Sum(k => k.KVar);
        sonuc.ToplamReaktorKVar = sonuc.ReaktorKademeleri.Sum(k => k.KVar);

        // SVC yük sürücü seçimi: reaktör toplamından büyük ilk sürücü
        if (sonuc.ReaktorAktif && sonuc.ToplamReaktorKVar > 0)
        {
            if (sonuc.SecilenYukSurucu == null || sonuc.SecilenYukSurucu.KademeGucKVar < sonuc.ToplamReaktorKVar)
            {
                sonuc.SecilenYukSurucu = kutuphane.SvcSuruculer
                    .Where(s => s.KademeGucKVar >= sonuc.ToplamReaktorKVar)
                    .OrderBy(s => s.KademeGucKVar)
                    .FirstOrDefault()
                    ?? kutuphane.SvcSuruculer.OrderByDescending(s => s.KademeGucKVar).FirstOrDefault();
            }
        }
        else
        {
            sonuc.SecilenYukSurucu = null;
        }

        // KademeNo'ları sırala
        for (int i = 0; i < sonuc.KondansatorKademeleri.Count; i++)
            sonuc.KondansatorKademeleri[i].KademeNo = i + 1;
        for (int i = 0; i < sonuc.ReaktorKademeleri.Count; i++)
            sonuc.ReaktorKademeleri[i].KademeNo = i + 1;

        return sonuc;
    }

    /// <summary>
    /// Kondansatör kademelerini hedef kademe sayısı kadar <b>kademeli (graded)</b> üretir:
    /// küçük kademeler ince çözünürlük, büyük kademeler kütle için. Ağırlıklar 1:2:4:8 (8'de sınırlı)
    /// dağıtılır, her kademe en yakın standart kondansatör boyutuna yuvarlanır (hata-geri-beslemeli),
    /// ardından kademeler büyütülüp küçültülerek toplam gerekli Q'ya yaklaştırılır.
    /// Böylece tüm kademelerin eşit olması (ve toplam aşırı kompanzasyon) önlenir.
    /// </summary>
    private static void KondansatorKademeleriOlustur(KompanzasyonSonucu sonuc, ElektrikKutuphaneServisi kutuphane)
    {
        sonuc.KondansatorKademeleri.Clear();
        int hedef = sonuc.KondansatorKademeSayisi;
        if (hedef <= 0) return;

        var kondansatorler = kutuphane.Kondansatorler
            .Where(k => k.FazTipi == "Trifaze")
            .OrderBy(k => k.GucKVar)
            .ToList();
        if (kondansatorler.Count == 0) return;

        double q = sonuc.GerekliQkVar > 0 ? sonuc.GerekliQkVar : kondansatorler[0].GucKVar * hedef;

        // 1) Kademeli ağırlıklar: 1,2,4,8,8,8,... (ikiye katlanıp 8'de sınırlanır)
        var agirlik = new double[hedef];
        for (int i = 0; i < hedef; i++)
            agirlik[i] = Math.Min(8.0, Math.Pow(2, i));
        double taban = q / agirlik.Sum();

        // 2) Hata-geri-beslemeli yuvarlama: her kademe en yakın standart boyuta gider,
        //    yuvarlama hatası sonraki kademeye taşınır → toplam ≈ Q.
        var secim = new KondansatorTanim[hedef];
        double tasima = 0;
        for (int i = 0; i < hedef; i++)
        {
            double hedefKVar = agirlik[i] * taban + tasima;
            secim[i] = EnYakinKondansator(kondansatorler, hedefKVar);
            tasima = hedefKVar - secim[i].GucKVar;
        }

        // 3) Toplamı Q'ya yaklaştır: aşımda en büyük kademeyi küçült, eksikte en küçük kademeyi büyüt.
        double tol = kondansatorler[0].GucKVar / 2.0;
        double Toplam() => secim.Sum(s => s.GucKVar);
        int guvenlik = 0;
        while (Toplam() > q + tol && guvenlik++ < hedef * 6)
        {
            int idx = EnBuyukIndeks(secim);
            var alt = BirAltKondansator(kondansatorler, secim[idx].GucKVar);
            if (alt == null) break;
            secim[idx] = alt;
        }
        while (Toplam() < q - tol && guvenlik++ < hedef * 12)
        {
            int idx = EnKucukIndeks(secim);
            var ust = BirUstKondansator(kondansatorler, secim[idx].GucKVar);
            if (ust == null) break;
            secim[idx] = ust;
        }

        // 4) Küçükten büyüğe sırala ve kademeleri ekle
        Array.Sort(secim, (a, b) => a.GucKVar.CompareTo(b.GucKVar));
        for (int i = 0; i < hedef; i++)
            sonuc.KondansatorKademeleri.Add(new KompanzasyonKademe
            {
                KademeNo = i + 1,
                KVar = secim[i].GucKVar,
                KondansatorAd = secim[i].Ad,
                FazTipi = secim[i].FazTipi
            });
    }

    private static KondansatorTanim EnYakinKondansator(List<KondansatorTanim> liste, double hedefKVar)
        => liste.OrderBy(k => Math.Abs(k.GucKVar - hedefKVar)).ThenBy(k => k.GucKVar).First();

    private static KondansatorTanim? BirUstKondansator(List<KondansatorTanim> liste, double kVar)
        => liste.Where(k => k.GucKVar > kVar + 1e-6).OrderBy(k => k.GucKVar).FirstOrDefault();

    private static KondansatorTanim? BirAltKondansator(List<KondansatorTanim> liste, double kVar)
        => liste.Where(k => k.GucKVar < kVar - 1e-6).OrderByDescending(k => k.GucKVar).FirstOrDefault();

    private static int EnBuyukIndeks(KondansatorTanim[] dizi)
    {
        int idx = 0;
        for (int i = 1; i < dizi.Length; i++)
            if (dizi[i].GucKVar > dizi[idx].GucKVar) idx = i;
        return idx;
    }

    private static int EnKucukIndeks(KondansatorTanim[] dizi)
    {
        int idx = 0;
        for (int i = 1; i < dizi.Length; i++)
            if (dizi[i].GucKVar < dizi[idx].GucKVar) idx = i;
        return idx;
    }

    /// <summary>
    /// 3 reaktör kademesi oluşturur. Reaktör seçimi: kondansatör kademe kVAr'ına yakın akort %5.67 (varsayılan).
    /// </summary>
    private static void ReaktorKademeleriOlustur(KompanzasyonSonucu sonuc, ElektrikKutuphaneServisi kutuphane)
    {
        sonuc.ReaktorKademeleri.Clear();

        var reaktorler = kutuphane.Reaktorler.OrderBy(r => r.GucKVar).ToList();
        if (reaktorler.Count == 0) return;

        // Reaktör boyutu kondansatör kademesinin akort yüzdesi kadar kapasiteye yakın
        double akortHedef = sonuc.ReaktorAkortYuzde > 0 ? sonuc.ReaktorAkortYuzde : 7.0;
        // Kademeli kondansatör dağıtımında temsili (en büyük) kademe esas alınır.
        double temsilKademeKVar = sonuc.KondansatorKademeleri.Count > 0
            ? sonuc.KondansatorKademeleri.Max(k => k.KVar)
            : 5.0;
        double hedefReaktorKVar = temsilKademeKVar * (akortHedef / 100.0);

        // Yeterli kVAr'a sahip reaktörler arasından akort yüzdesi hedefe en yakın olanı seç.
        var yeterli = reaktorler.Where(r => r.GucKVar >= hedefReaktorKVar).ToList();
        var aday = yeterli.Count > 0 ? yeterli : reaktorler;
        var seciliReaktor = aday
            .OrderBy(r => Math.Abs(r.AkortYuzde - akortHedef))
            .ThenBy(r => r.GucKVar)
            .First();

        for (int i = 1; i <= 3; i++)
        {
            double xc = (400.0 * 400.0) / Math.Max(0.001, seciliReaktor.GucKVar * 1000.0);
            double xl = (seciliReaktor.AkortYuzde / 100.0) * xc;
            double l_mH = (xl / (2 * Math.PI * 50)) * 1000;

            sonuc.ReaktorKademeleri.Add(new KompanzasyonReaktorKademe
            {
                KademeNo = i,
                ReaktorAd = seciliReaktor.Ad,
                KVar = seciliReaktor.GucKVar,
                L_mH = l_mH,
                AkortFrekansHz = seciliReaktor.AkortFrekansHz
            });
        }
    }

    /// <summary>
    /// Bir kondansatör kademesinin değerlerini kütüphane tanımına göre günceller.
    /// </summary>
    public static void KondansatorKademesiniGuncelle(KompanzasyonKademe kademe, KondansatorTanim kond)
    {
        kademe.KondansatorAd = kond.Ad;
        kademe.KVar = kond.GucKVar;
        kademe.FazTipi = kond.FazTipi;
    }

    /// <summary>
    /// Bir reaktör kademesinin değerlerini kütüphane tanımına göre günceller (L_mH otomatik hesaplanır).
    /// </summary>
    public static void ReaktorKademesiniGuncelle(KompanzasyonReaktorKademe kademe, ReaktorTanim reaktor)
    {
        kademe.ReaktorAd = reaktor.Ad;
        kademe.KVar = reaktor.GucKVar;
        kademe.AkortFrekansHz = reaktor.AkortFrekansHz;
        double xc = (400.0 * 400.0) / Math.Max(0.001, reaktor.GucKVar * 1000.0);
        double xl = (reaktor.AkortYuzde / 100.0) * xc;
        kademe.L_mH = (xl / (2 * Math.PI * 50)) * 1000;
    }
}
