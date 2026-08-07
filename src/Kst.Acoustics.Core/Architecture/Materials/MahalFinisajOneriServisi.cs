using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Architecture.Models;

namespace Kst.Acoustics.Core.Architecture.Materials;

/// <summary>
/// Mahal tipine gore zemin/duvar/tavan finisaj onerisi uretir ve M36 finisaj on kontrol
/// uyarilarini hesaplar.
/// </summary>
public static class MahalFinisajOneriServisi
{
    public const string ProfilEksikKodu = "M36-FINISAJ-PROFILI-YOK";
    public const string IslakZeminUygunsuzKodu = "M36-ISLAK-ZEMIN-UYGUNSUZ";
    public const string KacisYanginEksikKodu = "M36-KACIS-YANGIN-EKSIK";
    public const string AkustikTavanEksikKodu = "M36-AKUSTIK-TAVAN-EKSIK";

    private static readonly MahalTipi[] KacisMahalTipleri =
        [MahalTipi.Merdiven, MahalTipi.YanginMerdiveni, MahalTipi.Koridor];

    private static readonly KullanimSinifi[] KacisKullanimSiniflari =
        [KullanimSinifi.Sirkulasyon, KullanimSinifi.DuseySirkulasyon, KullanimSinifi.GuvenlikVeYangin];

    private static readonly MahalTipi[] AkustikMahalTipleri =
        [MahalTipi.KonferansSalonu, MahalTipi.Derslik, MahalTipi.ToplantiOdasi, MahalTipi.AcikOfis, MahalTipi.Ofis];

    /// <summary>
    /// Mahal tipi ve kullanim sinifindan finisaj profili onerir.
    /// Once ozel profiller, sonra varsayilan katalog, sonra kullanim sinifi yedegi denenir.
    /// </summary>
    public static MahalFinisajProfili Oner(
        MahalTipi mahalTipi,
        KullanimSinifi kullanimSinifi = KullanimSinifi.Tanimsiz,
        IEnumerable<MahalFinisajProfili>? ozelProfiller = null)
    {
        var ozel = ozelProfiller?.ToList() ?? [];

        var profil =
            ozel.FirstOrDefault(p => p.MahalTipi == mahalTipi)?.Kopyala()
            ?? VarsayilanMahalFinisajProfilleri.MahalTipineGore(mahalTipi)
            ?? (kullanimSinifi != KullanimSinifi.Tanimsiz
                ? ozel.FirstOrDefault(p => p.KullanimSinifi == kullanimSinifi)?.Kopyala()
                  ?? VarsayilanMahalFinisajProfilleri.KullanimSinifinaGore(kullanimSinifi)
                : null);

        return profil ?? GenelProfil(mahalTipi, kullanimSinifi);
    }

    public static MahalFinisajProfili Oner(MahalGereksinimi mahal, IEnumerable<MahalFinisajProfili>? ozelProfiller = null)
    {
        ArgumentNullException.ThrowIfNull(mahal);
        return Oner(mahal.MahalTipi, mahal.KullanimSinifi, ozelProfiller);
    }

    /// <summary>
    /// Projedeki mahaller icin finisaj on kontrol uyarilarini uretir.
    /// </summary>
    public static IReadOnlyList<MahalFinisajUyarisi> FinisajUyarilari(MimariProje? proje)
    {
        var uyarilar = new List<MahalFinisajUyarisi>();
        var mahaller = proje?.IhtiyacProgrami?.Mahaller;
        if (mahaller is null || mahaller.Count == 0)
            return uyarilar;

        var tipDict = TipSozlugu(proje!);
        var malzemeDict = MalzemeSozlugu(proje!);
        var ozelProfiller = proje!.FinisajProfilleri;

        foreach (var mahal in mahaller)
        {
            if (mahal is null || mahal.MahalTipi == MahalTipi.Tanimsiz)
                continue;

            var profil = Oner(mahal.MahalTipi, mahal.KullanimSinifi, ozelProfiller);
            var ad = string.IsNullOrWhiteSpace(mahal.Ad) ? mahal.MahalTipi.ToString() : mahal.Ad;

            if (!profil.OnerisiVar)
            {
                uyarilar.Add(new MahalFinisajUyarisi
                {
                    KuralKodu = ProfilEksikKodu,
                    Mesaj = $"'{ad}' mahali için zemin/duvar/tavan finisaj önerisi bulunamadı.",
                    CozumOnerisi = "Mahal tipine uygun bir finisaj profili tanımlayın veya yapı elemanı tipi atayın.",
                    EksikVeriMi = true,
                    EtkilenenMahalId = mahal.Id,
                    VeriEksigi = "MahalFinisajProfili"
                });
                continue;
            }

            if (IslakHacimMi(mahal))
            {
                var zeminTip = TipBul(tipDict, profil.ZeminTipiId);
                var suUygun = TipKosulu(zeminTip, malzemeDict, m => m.IslakHacimUygun);
                var kaymazVar = TipKosulu(zeminTip, malzemeDict, m => m.Performans.KaymazlikBilgisiVar);
                if (!suUygun || !kaymazVar)
                {
                    uyarilar.Add(new MahalFinisajUyarisi
                    {
                        KuralKodu = IslakZeminUygunsuzKodu,
                        Mesaj = $"'{ad}' ıslak hacim mahalinin önerilen zemini su dayanımı veya kaymazlık bilgisi taşımıyor.",
                        CozumOnerisi = "Islak hacim için su yalıtımlı ve kaymaz (ör. R10) zemin tipi atayın.",
                        EtkilenenMahalId = mahal.Id
                    });
                }
            }

            if (KacisMi(mahal))
            {
                var yanginVar = ProfilTipleri(profil).Any(id =>
                    TipKosulu(TipBul(tipDict, id), malzemeDict, m => m.Performans.YanginBilgisiVar));
                if (!yanginVar)
                {
                    uyarilar.Add(new MahalFinisajUyarisi
                    {
                        KuralKodu = KacisYanginEksikKodu,
                        Mesaj = $"'{ad}' kaçış/sirkülasyon mahalinin finisajında yangın tepki/dayanım bilgisi bulunmuyor.",
                        CozumOnerisi = "Kaçış yolu kaplamalarına yangın tepki sınıfı taşıyan malzeme/tip atayın.",
                        EtkilenenMahalId = mahal.Id
                    });
                }
            }

            if (AkustikMi(mahal))
            {
                var tavanTip = TipBul(tipDict, profil.TavanTipiId);
                var akustikVar = TipKosulu(tavanTip, malzemeDict, m => m.Performans.AkustikBilgisiVar);
                if (!akustikVar)
                {
                    uyarilar.Add(new MahalFinisajUyarisi
                    {
                        KuralKodu = AkustikTavanEksikKodu,
                        Mesaj = $"'{ad}' mahalinin önerilen tavanında akustik performans bilgisi bulunmuyor.",
                        CozumOnerisi = "Toplantı/eğitim/ofis hacimlerinde yutuculuk veya ses azaltım verisi taşıyan tavan tipi atayın.",
                        EtkilenenMahalId = mahal.Id
                    });
                }
            }
        }

        return uyarilar;
    }

    private static bool IslakHacimMi(MahalGereksinimi mahal)
        => MahalTipiYardimcilari.IslakHacimMi(mahal.MahalTipi, mahal.KullanimSinifi);

    private static bool KacisMi(MahalGereksinimi mahal)
        => KacisKullanimSiniflari.Contains(mahal.KullanimSinifi) || KacisMahalTipleri.Contains(mahal.MahalTipi);

    private static bool AkustikMi(MahalGereksinimi mahal)
        => AkustikMahalTipleri.Contains(mahal.MahalTipi);

    private static IEnumerable<string> ProfilTipleri(MahalFinisajProfili profil)
        => new[] { profil.ZeminTipiId, profil.DuvarTipiId, profil.TavanTipiId }
            .Where(id => !string.IsNullOrWhiteSpace(id));

    private static MahalFinisajProfili GenelProfil(MahalTipi mahalTipi, KullanimSinifi kullanimSinifi)
        => new()
        {
            Kod = "FIN-GENEL",
            Ad = "Genel mahal finisajı",
            MahalTipi = mahalTipi,
            KullanimSinifi = kullanimSinifi,
            ZeminTipiId = VarsayilanYapiElemaniTipleri.VarsayilanDosemeTipiId,
            DuvarTipiId = VarsayilanYapiElemaniTipleri.VarsayilanDuvarTipiId,
            TavanTipiId = VarsayilanYapiElemaniTipleri.VarsayilanBoyaliTavanTipiId,
            KaynakKodu = "M36",
            Notlar = "Eşleşen özel profil bulunamadığında kullanılan genel finisaj önerisi."
        };

    private static Dictionary<string, YapiElemaniTipi> TipSozlugu(MimariProje proje)
    {
        var dict = new Dictionary<string, YapiElemaniTipi>(StringComparer.OrdinalIgnoreCase);
        foreach (var tip in VarsayilanYapiElemaniTipleri.Tum)
            dict[tip.Id] = tip;

        foreach (var tip in proje.YapiElemaniTipleri)
        {
            if (!string.IsNullOrWhiteSpace(tip.Id))
                dict[tip.Id] = tip;
        }

        return dict;
    }

    private static Dictionary<string, MimariMalzeme> MalzemeSozlugu(MimariProje proje)
    {
        var dict = new Dictionary<string, MimariMalzeme>(StringComparer.OrdinalIgnoreCase);

        void Ekle(MimariMalzeme malzeme)
        {
            if (!string.IsNullOrWhiteSpace(malzeme.Id))
                dict[malzeme.Id] = malzeme;
            if (!string.IsNullOrWhiteSpace(malzeme.Kod))
                dict[malzeme.Kod] = malzeme;
        }

        foreach (var malzeme in VarsayilanMimariMalzemeler.Tum)
            Ekle(malzeme);
        foreach (var malzeme in proje.Malzemeler)
            Ekle(malzeme);

        return dict;
    }

    private static YapiElemaniTipi? TipBul(Dictionary<string, YapiElemaniTipi> dict, string? id)
        => !string.IsNullOrWhiteSpace(id) && dict.TryGetValue(id, out var tip) ? tip : null;

    private static bool TipKosulu(
        YapiElemaniTipi? tip,
        Dictionary<string, MimariMalzeme> malzemeDict,
        Func<MimariMalzeme, bool> kosul)
    {
        if (tip is null)
            return false;

        foreach (var katman in tip.Katmanlar.Where(k => k.MalzemeBaglantisiVar))
        {
            if (malzemeDict.TryGetValue(katman.MalzemeId, out var malzeme) && kosul(malzeme))
                return true;
        }

        return false;
    }
}
