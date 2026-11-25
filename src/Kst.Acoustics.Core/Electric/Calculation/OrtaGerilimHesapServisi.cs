using System;
using System.Linq;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.Core.Electric.Calculation;

/// <summary>
/// Orta gerilim (OG) hesap servisi: ana pano yüküne göre trafo tayini, trafo AG/OG akımı,
/// kaynak (şebeke + trafo) empedansı, OG hücre anma akımı kontrolü ve trafo→ana pano
/// bağlantısının (kablo / Bar-24) boyutlandırması.
/// <para>
/// Hesap derinliği: ekipman seçimi + şema + kısa devre katkısı. OG koruma koordinasyonu
/// (röle ayarları, selektivite) kapsam dışıdır.
/// </para>
/// </summary>
public static class OrtaGerilimHesapServisi
{
    /// <summary>OG XLPE kablo segmentini tip, malzeme, kesit ve metrajıyla etiketler.</summary>
    public static string XlpeKabloEtiketi(OgXlpeKablo? kablo)
    {
        kablo ??= new OgXlpeKablo();
        string tip = kablo.IletkenMalzemesi == OgKabloIletkenMalzemesi.Bakir ? "N2XSY" : "NA2XSY";
        string malzeme = kablo.IletkenMalzemesi == OgKabloIletkenMalzemesi.Bakir ? "Cu" : "Al";
        double kesit = Math.Max(1, kablo.KesitMm2);
        double uzunluk = Math.Max(0, kablo.UzunlukM);
        return $"{tip} XLPE {malzeme} • 3x(1x{kesit:0.##} mm²) • {uzunluk:0.##}m";
    }

    /// <summary>
    /// Verilen görünür güce uygun trafoyu kütüphaneden seçer — yük×emniyet ≥ kVA olan en küçük trafo.
    /// İstenirse belirli bir <paramref name="tip"/> ile sınırlanır.
    /// </summary>
    public static TrafoTanim? UygunTrafoSec(double yukKVA, double emniyetFaktoru,
        TrafoTipi? tip, ElektrikKutuphaneServisi kutuphane)
    {
        double gerekliKVA = yukKVA * (emniyetFaktoru <= 0 ? 1.0 : emniyetFaktoru);

        var sirali = kutuphane.Trafolar
            .Where(t => tip == null || t.Tip == tip.Value)
            .OrderBy(t => t.GucKVA)
            .ToList();

        return sirali.FirstOrDefault(t => t.GucKVA >= gerekliKVA) ?? sirali.LastOrDefault();
    }

    /// <summary>Trafo AG (sekonder) tam yük akımı (A) — S·1000/(√3·V_AG).</summary>
    public static double TrafoAgAkimA(TrafoTanim trafo)
    {
        double v = trafo.SekonderGerilimV > 0 ? trafo.SekonderGerilimV : 400;
        return trafo.GucKVA * 1000.0 / (Math.Sqrt(3) * v);
    }

    /// <summary>Trafo OG (primer) tam yük akımı (A) — S·1000/(√3·V_OG).</summary>
    public static double TrafoOgAkimA(TrafoTanim trafo)
    {
        double vOg = (trafo.PrimerGerilimKV > 0 ? trafo.PrimerGerilimKV : 34.5) * 1000.0;
        return trafo.GucKVA * 1000.0 / (Math.Sqrt(3) * vOg);
    }

    /// <summary>
    /// Trafo AG barasındaki kaynak empedansını (Ω, faz) hesaplar:
    /// Zq (şebeke, AG'ye indirgenmiş) + Zt (trafo). Skq verilmezse şebeke kısa devre akımından
    /// türetilir. AG'ye indirgenmiş şebeke empedansı Zq_AG = V_AG² / Skq olarak sadeleşir.
    /// </summary>
    public static double KaynakEmpedansiAgOhm(OrtaGerilimSistemi og, TrafoTanim trafo)
    {
        double vAg = trafo.SekonderGerilimV > 0 ? trafo.SekonderGerilimV : 400;
        double vOg = (og.OgGerilimKV > 0 ? og.OgGerilimKV : 34.5) * 1000.0;

        // Şebeke kısa devre gücü Skq (VA)
        double skq = og.SebekeKisaDevreMVA > 0
            ? og.SebekeKisaDevreMVA * 1_000_000.0
            : Math.Sqrt(3) * vOg * Math.Max(0.1, og.SebekeIkKA) * 1000.0;

        double zqAg = skq > 0 ? (vAg * vAg) / skq : 0;

        // Trafo empedansı (AG tarafına yansıyan)
        double sVA = trafo.GucKVA * 1000.0;
        double zt = (trafo.UkYuzde > 0 && sVA > 0) ? (trafo.UkYuzde / 100.0) * (vAg * vAg) / sVA : 0;

        return zqAg + zt;
    }

    /// <summary>
    /// OG sistemini hesaplar — uygun trafo/bağlantı seçimi (boşsa) ve türev değerler.
    /// <see cref="JeneratorHesapServisi.Hesapla"/> ile aynı akış/sözleşmeyi izler.
    /// </summary>
    public static void Hesapla(OrtaGerilimSistemi og, Pano anaPano, ElektrikProje proje,
        ElektrikKutuphaneServisi kutuphane, bool anaPanoyaYansit = true)
    {
        og.SebekeHucreXlpeKablosu ??= new OgXlpeKablo();
        og.HucreTrafoXlpeKablosu ??= new OgXlpeKablo();
        og.HesaplananYukKVA = anaPano.ToplamGorunurVA / 1000.0;
        double emniyet = og.EmniyetFaktoru <= 0 ? 1.0 : og.EmniyetFaktoru;
        og.GerekliKVAMin = og.HesaplananYukKVA * emniyet;

        // Trafo seçimi: kullanıcı sabitlemediyse her hesapta güncel yüke göre yeniden seçilir.
        TrafoTanim? trafo = null;
        if (og.TrafoManuel && !string.IsNullOrEmpty(og.TrafoTanimAd))
            trafo = kutuphane.Trafolar.FirstOrDefault(t => t.Ad == og.TrafoTanimAd);
        if (trafo == null)
        {
            trafo = UygunTrafoSec(og.HesaplananYukKVA, emniyet, null, kutuphane);
            og.TrafoTanimAd = trafo?.Ad ?? "";
        }

        if (trafo == null)
        {
            og.TrafoYeterliMi = false;
            og.TrafoAgAkimA = og.TrafoOgAkimA = og.AgBaraIkKA = 0;
            return;
        }

        og.TrafoYeterliMi = trafo.GucKVA >= og.GerekliKVAMin;
        og.TrafoAgAkimA = TrafoAgAkimA(trafo);
        og.TrafoOgAkimA = TrafoOgAkimA(trafo);

        // Trafo AG barasındaki kısa devre akımı (Zq + Zt)
        double vAg = trafo.SekonderGerilimV > 0 ? trafo.SekonderGerilimV : 400;
        double zKaynak = KaynakEmpedansiAgOhm(og, trafo);
        og.AgBaraIkKA = KisaDevreHesapServisi.Ik3KA(vAg, zKaynak);

        // OG hücre anma akımı kontrolü — tek trafoda bara/giriş/trafo koruma OG tam yük akımını taşır.
        foreach (var h in og.Hucreler)
        {
            var tanim = kutuphane.OgHucreleri.FirstOrDefault(x => x.Ad == h.HucreTanimAd);
            h.AkimA = og.TrafoOgAkimA;
            h.AnmaAkimYeterliMi = tanim == null || tanim.AnmaAkimA >= h.AkimA;
        }

        // Trafo → ana pano bağlantısı boyutlandırma
        BaglantiHesapla(og.CikisBaglanti, og.TrafoAgAkimA, proje, kutuphane);
        if (anaPanoyaYansit)
            AnaPanoBaglantisiniYansit(og, anaPano, proje);
    }

    /// <summary>
    /// Trafo AG çıkışı ile ana pano arasındaki bağlantının faz direncini (Ω) verir.
    /// Bu değer kısa devre hesabında kaynak empedansına eklenir.
    /// </summary>
    public static double BaglantiFazDirenciOhm(TrafoCikisBaglanti? baglanti,
        ElektrikKutuphaneServisi? kutuphane)
    {
        if (baglanti == null || baglanti.UzunlukM <= 0) return 0;

        switch (baglanti.Tip)
        {
            case TrafoCikisBaglantiTipi.Kablo:
            {
                double kesit = KabloHesapServisi.KesitDegeri(baglanti.KesitMm2);
                int kol = Math.Max(1, baglanti.HesaplananParalelKolSayisi);
                return kesit > 0
                    ? KabloHesapServisi.BakirOzdirenc * baglanti.UzunlukM / kesit / kol
                    : 0;
            }
            case TrafoCikisBaglantiTipi.Bar24:
            {
                var tip = kutuphane?.BusbarTipleri.FirstOrDefault(b => b.Ad == baglanti.ReferansAd);
                return tip != null
                    ? (tip.DirencMOhmPerM / 1000.0) * baglanti.UzunlukM
                    : 0;
            }
            case TrafoCikisBaglantiTipi.Hucre:
            default:
                return 0;
        }
    }

    /// <summary>
    /// Trafo AG çıkışı ile ana pano arasındaki bağlantının faz reaktansını (Ω) verir
    /// (<see cref="BaglantiFazDirenciOhm"/>'un reaktans karşılığı). Kısa devre zincirinde kaynak
    /// reaktansına eklenir.
    /// </summary>
    public static double BaglantiFazReaktansOhm(TrafoCikisBaglanti? baglanti,
        ElektrikKutuphaneServisi? kutuphane)
    {
        if (baglanti == null || baglanti.UzunlukM <= 0) return 0;

        switch (baglanti.Tip)
        {
            case TrafoCikisBaglantiTipi.Kablo:
            {
                int kol = Math.Max(1, baglanti.HesaplananParalelKolSayisi);
                return KabloHesapServisi.KabloReaktansOhmPerM * baglanti.UzunlukM / kol;
            }
            case TrafoCikisBaglantiTipi.Bar24:
            {
                var tip = kutuphane?.BusbarTipleri.FirstOrDefault(b => b.Ad == baglanti.ReferansAd);
                return tip != null
                    ? (tip.ReaktansMOhmPerM / 1000.0) * baglanti.UzunlukM
                    : 0;
            }
            case TrafoCikisBaglantiTipi.Hucre:
            default:
                return 0;
        }
    }

    private static void AnaPanoBaglantisiniYansit(OrtaGerilimSistemi og, Pano anaPano,
        ElektrikProje proje)
    {
        if (!string.IsNullOrWhiteSpace(og.AnaPanoAd) &&
            !string.Equals(og.AnaPanoAd, anaPano.Ad, StringComparison.OrdinalIgnoreCase))
            return;

        var b = og.CikisBaglanti;
        anaPano.KabloMesafeM = Math.Max(0, b.UzunlukM);
        anaPano.KabloGerilimDususYuzde = b.GerilimDususYuzde;
        anaPano.KabloIsiKaybiW = b.IsiKaybiW;

        // Trafo–ana pano arası giriş şalteri: trafo AG çıkış akımına göre (yükü kapsadığından
        // ana pano akımının da üstündeki ilk standart). Trafo yoksa PanoHesapServisi otomatik atar.
        anaPano.GirisSalteriA = PanoHesapServisi.GirisSigortaHesapla(
            Math.Max(og.TrafoAgAkimA, anaPano.GirisAkimA));

        switch (b.Tip)
        {
            case TrafoCikisBaglantiTipi.Kablo:
                anaPano.KabloKesiti = b.KesitMm2;
                anaPano.KabloParalelKolSayisi = Math.Max(1, b.HesaplananParalelKolSayisi);
                anaPano.KabloTipi = string.IsNullOrWhiteSpace(b.ReferansAd)
                    ? proje.VarsayilanKabloTipi
                    : b.ReferansAd;
                break;
            case TrafoCikisBaglantiTipi.Bar24:
                anaPano.KabloKesiti = string.IsNullOrWhiteSpace(b.KesitMm2) ? b.ReferansAd : b.KesitMm2;
                anaPano.KabloTipi = string.IsNullOrWhiteSpace(b.ReferansAd) ? "Bar-24" : $"Bar-24 {b.ReferansAd}";
                break;
            case TrafoCikisBaglantiTipi.Hucre:
            default:
                anaPano.KabloKesiti = string.IsNullOrWhiteSpace(b.ReferansAd) ? "AG Hücre" : b.ReferansAd;
                anaPano.KabloTipi = "";
                break;
        }
    }

    private static void BaglantiHesapla(TrafoCikisBaglanti baglanti, double trafoAgAkimA,
        ElektrikProje proje, ElektrikKutuphaneServisi kutuphane)
    {
        baglanti.AkimA = trafoAgAkimA;
        double cosF = proje.CosF;
        double vAg = proje.SistemGerilimV > 0 ? proje.SistemGerilimV : 400;
        double izin = proje.IzinVerilenGerilimDususYuzde > 0 ? proje.IzinVerilenGerilimDususYuzde : 3.0;

        switch (baglanti.Tip)
        {
            case TrafoCikisBaglantiTipi.Kablo:
            {
                var kablo = KabloHesapServisi.ParalelKabloHesapla(
                    trafoAgAkimA,
                    baglanti.UzunlukM,
                    FazTipi.Trifaze,
                    vAg,
                    izin,
                    cosF,
                    maksimumKesitAkimKapasitesiA: KabloHesapServisi.Maksimum240Mm2AkimKapasitesiA,
                    minimumParalelKolSayisi: baglanti.ParalelKolSayisi);
                baglanti.HesaplananParalelKolSayisi = kablo.ParalelKolSayisi;
                baglanti.KesitMm2 = kablo.KabloEtiketi;
                baglanti.GerilimDususYuzde = kablo.GercekDusumYuzde;
                // Isı kaybı: 3 faz iletken × paralel kol × I_kol² × R_iletken
                double kolAkim = trafoAgAkimA / kablo.ParalelKolSayisi;
                double r = KabloHesapServisi.BakirOzdirenc * baglanti.UzunlukM / kablo.KesitMm2;
                baglanti.IsiKaybiW = 3.0 * kablo.ParalelKolSayisi * kolAkim * kolAkim * r;
                break;
            }
            case TrafoCikisBaglantiTipi.Bar24:
            {
                baglanti.HesaplananParalelKolSayisi = 1;
                // Referans seçilmemişse uygun busbar tipini seç (akımı taşıyan en küçük)
                var tip = kutuphane.BusbarTipleri.FirstOrDefault(b => b.Ad == baglanti.ReferansAd);
                if (tip == null)
                {
                    tip = kutuphane.BusbarTipleri
                        .OrderBy(b => b.NominalAkimA)
                        .FirstOrDefault(b => b.NominalAkimA >= trafoAgAkimA)
                        ?? kutuphane.BusbarTipleri.OrderByDescending(b => b.NominalAkimA).FirstOrDefault();
                    baglanti.ReferansAd = tip?.Ad ?? "";
                }
                if (tip != null)
                {
                    double rPerM = tip.DirencMOhmPerM / 1000.0;
                    double xPerM = tip.ReaktansMOhmPerM / 1000.0;
                    double sinF = Math.Sqrt(Math.Max(0, 1 - cosF * cosF));
                    double dusumV = Math.Sqrt(3) * trafoAgAkimA * baglanti.UzunlukM * (rPerM * cosF + xPerM * sinF);
                    baglanti.GerilimDususYuzde = (dusumV / vAg) * 100.0;
                    baglanti.IsiKaybiW = 3.0 * trafoAgAkimA * trafoAgAkimA * (rPerM * baglanti.UzunlukM);
                    baglanti.KesitMm2 = tip.KesitMm2;
                }
                break;
            }
            case TrafoCikisBaglantiTipi.Hucre:
            default:
                // AG giriş hücresi/kofresi — boyutlandırma hücre seçimine bırakılır.
                baglanti.HesaplananParalelKolSayisi = 1;
                baglanti.KesitMm2 = "";
                baglanti.GerilimDususYuzde = 0;
                baglanti.IsiKaybiW = 0;
                break;
        }
    }
}
