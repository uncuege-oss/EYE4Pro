using System;
using System.Linq;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.Core.Electric.Calculation;

/// <summary>
/// Jeneratör hesap servisi: güce göre jeneratör tayini, transfer şalteri tayini, yakıt tüketim tahmini.
/// </summary>
public static class JeneratorHesapServisi
{
    /// <summary>Jeneratör güç hatlarında kullanılan 240 mm² kablonun proje akım sınırı.</summary>
    public const double Jenerator240Mm2AkimKapasitesiA =
        KabloHesapServisi.Maksimum240Mm2AkimKapasitesiA;

    /// <summary>
    /// Verilen aktif güce uygun jeneratörü kütüphaneden seçer.
    /// Mod = Prime → PrimeGucKW kullanılır; Mod = Standby → StandbyGucKW kullanılır.
    /// Emniyet faktörü ile çarpılarak en küçük yeterli jeneratör döner.
    /// </summary>
    public static JeneratorTanim? UygunJeneratorSec(double yukKW, JeneratorMod mod,
        double emniyetFaktoru, ElektrikKutuphaneServisi kutuphane)
        => UygunJeneratorSec(yukKW, 0, mod, emniyetFaktoru, kutuphane);

    /// <summary>
    /// Verilen aktif ve görünür güce uygun jeneratörü seçer.
    /// kW ve kVA kriterlerinin ikisini de sağlayan en küçük jeneratör döner.
    /// </summary>
    public static JeneratorTanim? UygunJeneratorSec(double yukKW, double yukKVA, JeneratorMod mod,
        double emniyetFaktoru, ElektrikKutuphaneServisi kutuphane)
    {
        double gerekliKW = yukKW * emniyetFaktoru;
        double gerekliKVA = yukKVA * emniyetFaktoru;

        var siralı = kutuphane.Jeneratorler
            .OrderBy(j => mod == JeneratorMod.Prime ? j.PrimeGucKVA : j.StandbyGucKVA)
            .ThenBy(j => mod == JeneratorMod.Prime ? j.PrimeGucKW : j.StandbyGucKW)
            .ToList();

        return siralı.FirstOrDefault(j =>
                   (mod == JeneratorMod.Prime ? j.PrimeGucKW : j.StandbyGucKW) >= gerekliKW &&
                   (yukKVA <= 0 || (mod == JeneratorMod.Prime ? j.PrimeGucKVA : j.StandbyGucKVA) >= gerekliKVA))
               ?? siralı.LastOrDefault();
    }

    /// <summary>
    /// Transfer şalterinin akımını jeneratör ve ana pano akımının büyüğüne göre belirler.
    /// Kütüphaneden uygun anma akımlı ilk ATS döner.
    /// </summary>
    public static TransferSalterTanim? UygunTransferSalterSec(
        double anaPanoAkimA, double jeneratorAnaAkimA,
        ElektrikKutuphaneServisi kutuphane, string tercihTipi = "ATS")
    {
        double hedefAkim = Math.Max(anaPanoAkimA, jeneratorAnaAkimA);

        var siralı = kutuphane.TransferSalterleri
            .Where(t => t.Tipi == tercihTipi)
            .OrderBy(t => t.AnmaAkimA)
            .ToList();

        return siralı.FirstOrDefault(t => t.AnmaAkimA >= hedefAkim)
               ?? siralı.LastOrDefault()
               ?? kutuphane.TransferSalterleri.OrderByDescending(t => t.AnmaAkimA).FirstOrDefault();
    }

    /// <summary>
    /// Jeneratörün anma akımını (A) hesaplar — kVA × 1000 / (√3 × V).
    /// </summary>
    public static double JeneratorAnmaAkimA(JeneratorTanim jen, JeneratorMod mod)
    {
        double kva = mod == JeneratorMod.Prime ? jen.PrimeGucKVA : jen.StandbyGucKVA;
        return (kva * 1000.0) / (Math.Sqrt(3) * jen.GerilimV);
    }

    /// <summary>Seçili moddaki jeneratör görünür gücü (kVA).</summary>
    public static double JeneratorGucKVA(JeneratorTanim jen, JeneratorMod mod)
        => mod == JeneratorMod.Prime ? jen.PrimeGucKVA : jen.StandbyGucKVA;

    /// <summary>Seçili moddaki jeneratör aktif gücü (kW).</summary>
    public static double JeneratorGucKW(JeneratorTanim jen, JeneratorMod mod)
        => mod == JeneratorMod.Prime ? jen.PrimeGucKW : jen.StandbyGucKW;

    /// <summary>
    /// Yük yüzdesine göre yakıt tüketim tahmini (lineer interpolasyon).
    /// %25 yükte ≈ tam yükün %40'ı; %50 yükte ≈ %60'ı; %75 yükte ≈ %80'i.
    /// </summary>
    public static double YakitTuketimTahmini(JeneratorTanim jen, double yukYuzde)
    {
        if (jen.YakitTuketimLh100 <= 0) return 0;
        double yzd = Math.Clamp(yukYuzde, 0, 100) / 100.0;

        // Tipik dizel jeneratör eğrisi: idle ≈ 0.25, %50 ≈ 0.60, %100 ≈ 1.0
        double katsayi = 0.25 + 0.75 * yzd; // basit lineer model
        return jen.YakitTuketimLh100 * katsayi;
    }

    /// <summary>
    /// Tüm jeneratör hesabını yapar — uygun jeneratör/şalter seçimi ve türev değerler.
    /// jen.JeneratorTanimAd veya TransferSalterAd boşsa otomatik seçer.
    /// </summary>
    public static void Hesapla(Jenerator jen, Pano anaPano, ElektrikProje proje,
        ElektrikKutuphaneServisi kutuphane)
    {
        jen.HesaplananYukKW = anaPano.ToplamGucW / 1000.0;
        jen.HesaplananYukKVA = anaPano.ToplamGorunurVA / 1000.0;
        jen.GerekliKVAMin = jen.HesaplananYukKVA * jen.EmniyetFaktoru;

        // Jeneratör seçimi (atanmış değilse otomatik)
        JeneratorTanim? jenTanim = null;
        if (!string.IsNullOrEmpty(jen.JeneratorTanimAd))
            jenTanim = kutuphane.Jeneratorler.FirstOrDefault(j => j.Ad == jen.JeneratorTanimAd);

        if (jenTanim == null)
        {
            jenTanim = UygunJeneratorSec(jen.HesaplananYukKW, jen.HesaplananYukKVA, jen.Mod, jen.EmniyetFaktoru, kutuphane);
            jen.JeneratorTanimAd = jenTanim?.Ad ?? "";
        }

        // Yük yeterliliği
        if (jenTanim != null)
        {
            double jenKW = jen.Mod == JeneratorMod.Prime ? jenTanim.PrimeGucKW : jenTanim.StandbyGucKW;
            double jenKVA = jen.Mod == JeneratorMod.Prime ? jenTanim.PrimeGucKVA : jenTanim.StandbyGucKVA;
            jen.YukYeterliMi = jenKW >= jen.HesaplananYukKW * jen.EmniyetFaktoru
                && jenKVA >= jen.GerekliKVAMin;

            // Yakıt tüketimi (gerçek yük yüzdesi)
            double yukYzd = jenKW > 0 ? (jen.HesaplananYukKW / jenKW) * 100 : 0;
            jen.YakitTuketimLh = YakitTuketimTahmini(jenTanim, yukYzd);
        }
        else
        {
            jen.YukYeterliMi = false;
            jen.YakitTuketimLh = 0;
        }

        // Transfer şalteri seçimi — manuel seçilmemişse ana pano akımına göre her hesapta güncellenir
        // (yük büyüdükçe şalter de büyür; bir kez seçilip "donmaz").
        if (!jen.TransferSalterManuel && jenTanim != null)
        {
            double jenAkim = JeneratorAnmaAkimA(jenTanim, jen.Mod);
            var salter = UygunTransferSalterSec(anaPano.GirisAkimA, jenAkim, kutuphane);
            jen.TransferSalterAd = salter?.Ad ?? "";
        }

        // Anma akımı + kablo kesitleri (ünite → senkron, senkron → hedef)
        if (jenTanim != null)
        {
            jen.AnmaAkimA = JeneratorAnmaAkimA(jenTanim, jen.Mod);
            var uniteKablo = JeneratorKabloHesapla(
                jen.AnmaAkimA, jen.SenkronPanoKabloMesafeM, jenTanim.GerilimV, proje);
            jen.SenkronKabloParalelKolSayisi = uniteKablo.ParalelKolSayisi;
            jen.SenkronKabloKesiti = uniteKablo.KabloEtiketi;

            var hedefKablo = JeneratorKabloHesapla(
                jen.AnmaAkimA, jen.SenkronHedefPanoKabloMesafeM, jenTanim.GerilimV, proje);
            jen.SenkronHedefKabloParalelKolSayisi = hedefKablo.ParalelKolSayisi;
            jen.SenkronHedefKabloKesiti = hedefKablo.KabloEtiketi;
        }
    }

    /// <summary>
    /// Senkron panolu jeneratör grubunu hesaplar. Üniteler farklı güçte olabilir; toplam
    /// kW/kVA kapasitesi yüke karşı kontrol edilir.
    /// </summary>
    public static void Hesapla(JeneratorGrubu grup, Pano anaPano, ElektrikProje proje,
        ElektrikKutuphaneServisi kutuphane)
    {
        grup.HesaplananYukKW = anaPano.ToplamGucW / 1000.0;
        grup.HesaplananYukKVA = anaPano.ToplamGorunurVA / 1000.0;
        grup.GerekliKVAMin = grup.HesaplananYukKVA * grup.EmniyetFaktoru;

        if (grup.Jeneratorler.Count == 0)
        {
            var otomatik = UygunJeneratorSec(
                grup.HesaplananYukKW, grup.HesaplananYukKVA, grup.Mod, grup.EmniyetFaktoru, kutuphane);
            if (otomatik != null)
            {
                grup.Jeneratorler.Add(new Jenerator
                {
                    AnaPanoAd = grup.AnaPanoAd,
                    TransferPanoAd = grup.SenkronPanoAd,
                    JeneratorTanimAd = otomatik.Ad,
                    Mod = grup.Mod,
                    EmniyetFaktoru = grup.EmniyetFaktoru
                });
            }
        }

        var secimler = grup.Jeneratorler
            .Select(j => new
            {
                Jenerator = j,
                Tanim = kutuphane.Jeneratorler.FirstOrDefault(t => t.Ad == j.JeneratorTanimAd)
            })
            .Where(x => x.Tanim != null)
            .ToList();

        grup.ToplamGucKW = secimler.Sum(x => JeneratorGucKW(x.Tanim!, grup.Mod));
        grup.ToplamGucKVA = secimler.Sum(x => JeneratorGucKVA(x.Tanim!, grup.Mod));
        grup.YukYeterliMi =
            grup.ToplamGucKW >= grup.HesaplananYukKW * grup.EmniyetFaktoru &&
            grup.ToplamGucKVA >= grup.GerekliKVAMin;

        double kapasiteKW = Math.Max(0.001, grup.ToplamGucKW);
        grup.YakitTuketimLh = 0;
        foreach (var secim in secimler)
        {
            var j = secim.Jenerator;
            var t = secim.Tanim!;
            double unitKW = JeneratorGucKW(t, grup.Mod);
            double pay = unitKW / kapasiteKW;

            j.AnaPanoAd = grup.AnaPanoAd;
            j.TransferPanoAd = grup.SenkronPanoAd;
            j.Mod = grup.Mod;
            j.EmniyetFaktoru = grup.EmniyetFaktoru;
            j.TransferSalterAd = grup.TransferSalterAd;
            j.HesaplananYukKW = grup.HesaplananYukKW * pay;
            j.HesaplananYukKVA = grup.HesaplananYukKVA * pay;
            j.GerekliKVAMin = j.HesaplananYukKVA * grup.EmniyetFaktoru;
            j.YukYeterliMi = unitKW >= j.HesaplananYukKW * grup.EmniyetFaktoru &&
                JeneratorGucKVA(t, grup.Mod) >= j.GerekliKVAMin;
            double yukYzd = unitKW > 0 ? (j.HesaplananYukKW / unitKW) * 100 : 0;
            j.YakitTuketimLh = YakitTuketimTahmini(t, yukYzd);
            grup.YakitTuketimLh += j.YakitTuketimLh;

            // Ünite anma akımı + bu ünite → senkron pano kablosunun kesiti
            j.AnmaAkimA = JeneratorAnmaAkimA(t, grup.Mod);
            var uniteKablo = JeneratorKabloHesapla(
                j.AnmaAkimA, j.SenkronPanoKabloMesafeM, t.GerilimV, proje);
            j.SenkronKabloParalelKolSayisi = uniteKablo.ParalelKolSayisi;
            j.SenkronKabloKesiti = uniteKablo.KabloEtiketi;
        }

        // Grup toplam anma akımı + senkron pano → hedef pano ortak kablosunun kesiti
        grup.ToplamAnmaAkimA = secimler.Sum(x => JeneratorAnmaAkimA(x.Tanim!, grup.Mod));
        int grupGerilimV = secimler.FirstOrDefault()?.Tanim?.GerilimV ?? 400;
        var hedefKablo = JeneratorKabloHesapla(
            grup.ToplamAnmaAkimA, grup.SenkronHedefPanoKabloMesafeM, grupGerilimV, proje);
        grup.SenkronHedefKabloParalelKolSayisi = hedefKablo.ParalelKolSayisi;
        grup.SenkronHedefKabloKesiti = hedefKablo.KabloEtiketi;

        // Transfer şalteri — manuel seçilmemişse ana pano akımına göre her hesapta güncellenir
        // (yük büyüdükçe şalter de büyür; bir kez seçilip "donmaz").
        if (!grup.TransferSalterManuel && secimler.Count > 0)
        {
            double grupAkim = secimler.Sum(x => JeneratorAnmaAkimA(x.Tanim!, grup.Mod));
            var salter = UygunTransferSalterSec(anaPano.GirisAkimA, grupAkim, kutuphane);
            grup.TransferSalterAd = salter?.Ad ?? "";
        }
        foreach (var j in grup.Jeneratorler)
            j.TransferSalterAd = grup.TransferSalterAd;
    }

    private static ParalelKabloSonucu JeneratorKabloHesapla(
        double akimA, double mesafeM, double gerilimV, ElektrikProje proje)
    {
        double izinDusum = proje.IzinVerilenGerilimDususYuzde > 0
            ? proje.IzinVerilenGerilimDususYuzde
            : 3.0;
        return KabloHesapServisi.ParalelKabloHesapla(
            akimA,
            mesafeM,
            FazTipi.Trifaze,
            gerilimV,
            izinDusum,
            proje.CosF,
            maksimumKesitAkimKapasitesiA: Jenerator240Mm2AkimKapasitesiA);
    }

    public static void HesaplaTumu(ElektrikProje proje, ElektrikKutuphaneServisi kutuphane)
    {
        foreach (var grup in proje.JeneratorGruplari)
        {
            var hedef = proje.TumPanolar.FirstOrDefault(p => p.Ad == grup.AnaPanoAd);

            // Hedef pano bulunamazsa: yalnız TESİS GENELİ grup ana panoya düşebilir. LOKAL BÖLGE
            // grubu, hedef tali panosu silinmişse atlanır — aksi halde yanlışlıkla tüm tesise
            // (ana pano yüküne) boyutlandırılırdı.
            if (hedef == null && grup.BeslemeKapsami == JeneratorBeslemeKapsami.TesisGeneli)
                hedef = proje.KokPano
                    ?? proje.TumPanolar.FirstOrDefault(p => p.PanoTipi == PanoTipi.AnaPano);

            if (hedef != null)
                Hesapla(grup, hedef, proje, kutuphane);
        }
    }
}
