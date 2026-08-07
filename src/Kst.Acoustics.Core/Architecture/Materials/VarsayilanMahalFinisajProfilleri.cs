using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Architecture.Models;

namespace Kst.Acoustics.Core.Architecture.Materials;

/// <summary>
/// M36 icin mahal tipine gore marka bagimsiz baslangic finisaj profili katalogu.
/// Profiller varsayilan yapi elemani tip Id'lerine isaret eder.
/// </summary>
public static class VarsayilanMahalFinisajProfilleri
{
    private static readonly MahalFinisajProfili[] Profiller =
    [
        // Konut ana hacimleri
        P("FIN-KONUT-SALON", "Konut yaşam alanı finisajı", MahalTipi.Salon, KullanimSinifi.AnaKullanim,
            VarsayilanYapiElemaniTipleri.VarsayilanDosemeTipiId,
            VarsayilanYapiElemaniTipleri.VarsayilanDuvarTipiId,
            VarsayilanYapiElemaniTipleri.VarsayilanBoyaliTavanTipiId,
            notlar: "Salon ve oturma alanları için genel finisaj önerisi."),
        P("FIN-KONUT-OTURMA", "Konut oturma odası finisajı", MahalTipi.OturmaOdasi, KullanimSinifi.AnaKullanim,
            VarsayilanYapiElemaniTipleri.VarsayilanDosemeTipiId,
            VarsayilanYapiElemaniTipleri.VarsayilanDuvarTipiId,
            VarsayilanYapiElemaniTipleri.VarsayilanBoyaliTavanTipiId),
        P("FIN-KONUT-YATAK", "Konut yatak odası finisajı", MahalTipi.YatakOdasi, KullanimSinifi.AnaKullanim,
            VarsayilanYapiElemaniTipleri.VarsayilanDosemeTipiId,
            VarsayilanYapiElemaniTipleri.VarsayilanDuvarTipiId,
            VarsayilanYapiElemaniTipleri.VarsayilanBoyaliTavanTipiId),

        // Ofis ve toplantı
        P("FIN-OFIS-ACIK", "Açık ofis finisajı", MahalTipi.AcikOfis, KullanimSinifi.AnaKullanim,
            VarsayilanYapiElemaniTipleri.VarsayilanDosemeTipiId,
            VarsayilanYapiElemaniTipleri.VarsayilanDuvarTipiId,
            VarsayilanYapiElemaniTipleri.VarsayilanTavanTipiId,
            notlar: "Açık ofiste konuşma mahremiyeti için akustik tavan önerilir."),
        P("FIN-OFIS", "Ofis finisajı", MahalTipi.Ofis, KullanimSinifi.AnaKullanim,
            VarsayilanYapiElemaniTipleri.VarsayilanDosemeTipiId,
            VarsayilanYapiElemaniTipleri.VarsayilanDuvarTipiId,
            VarsayilanYapiElemaniTipleri.VarsayilanTavanTipiId),
        P("FIN-TOPLANTI", "Toplantı odası finisajı", MahalTipi.ToplantiOdasi, KullanimSinifi.AnaKullanim,
            VarsayilanYapiElemaniTipleri.VarsayilanDosemeTipiId,
            VarsayilanYapiElemaniTipleri.VarsayilanDuvarTipiId,
            VarsayilanYapiElemaniTipleri.VarsayilanTavanTipiId,
            notlar: "Toplantı odalarında akustik tavan ile yankı kontrol edilir."),

        // Eğitim ve konferans
        P("FIN-DERSLIK", "Derslik finisajı", MahalTipi.Derslik, KullanimSinifi.AnaKullanim,
            VarsayilanYapiElemaniTipleri.VarsayilanDosemeTipiId,
            VarsayilanYapiElemaniTipleri.VarsayilanDuvarTipiId,
            VarsayilanYapiElemaniTipleri.VarsayilanTavanTipiId,
            notlar: "Eğitim hacimlerinde konuşma anlaşılırlığı için akustik tavan önerilir."),
        P("FIN-KONFERANS", "Konferans salonu finisajı", MahalTipi.KonferansSalonu, KullanimSinifi.AnaKullanim,
            VarsayilanYapiElemaniTipleri.VarsayilanDosemeTipiId,
            VarsayilanYapiElemaniTipleri.VarsayilanDuvarTipiId,
            VarsayilanYapiElemaniTipleri.VarsayilanTavanTipiId,
            notlar: "Konferans hacminde akustik tavan ile yankı süresi düşürülür."),

        // Islak hacimler
        P("FIN-ISLAK-BANYO", "Banyo ıslak hacim finisajı", MahalTipi.Banyo, KullanimSinifi.IslakHacim,
            VarsayilanYapiElemaniTipleri.VarsayilanIslakHacimDosemeTipiId,
            VarsayilanYapiElemaniTipleri.VarsayilanDuvarTipiId,
            VarsayilanYapiElemaniTipleri.VarsayilanBoyaliTavanTipiId,
            uyariNotlari: ["Islak hacim zemininde su yalıtımı ve kaymazlık şarttır."],
            notlar: "Banyo için su yalıtımlı ve kaymaz döşeme önerisi."),
        P("FIN-ISLAK-WC", "WC ıslak hacim finisajı", MahalTipi.Wc, KullanimSinifi.IslakHacim,
            VarsayilanYapiElemaniTipleri.VarsayilanIslakHacimDosemeTipiId,
            VarsayilanYapiElemaniTipleri.VarsayilanDuvarTipiId,
            VarsayilanYapiElemaniTipleri.VarsayilanBoyaliTavanTipiId,
            uyariNotlari: ["Islak hacim zemininde su yalıtımı ve kaymazlık şarttır."]),
        P("FIN-ISLAK-MUTFAK", "Mutfak ıslak hacim finisajı", MahalTipi.Mutfak, KullanimSinifi.IslakHacim,
            VarsayilanYapiElemaniTipleri.VarsayilanIslakHacimDosemeTipiId,
            VarsayilanYapiElemaniTipleri.VarsayilanDuvarTipiId,
            VarsayilanYapiElemaniTipleri.VarsayilanBoyaliTavanTipiId,
            uyariNotlari: ["Islak hacim zemininde su yalıtımı ve kaymazlık şarttır."]),

        // Teknik hacim
        P("FIN-TEKNIK", "Teknik hacim finisajı", MahalTipi.TeknikOda, KullanimSinifi.TeknikHacim,
            VarsayilanYapiElemaniTipleri.VarsayilanIslakHacimDosemeTipiId,
            VarsayilanYapiElemaniTipleri.VarsayilanDuvarTipiId,
            VarsayilanYapiElemaniTipleri.VarsayilanBoyaliTavanTipiId,
            notlar: "Teknik hacimlerde dayanıklı ve temizlenebilir zemin önerilir."),
    ];

    public static IReadOnlyList<MahalFinisajProfili> Tum => Profiller.Select(p => p.Kopyala()).ToList();

    public static MahalFinisajProfili? MahalTipineGore(MahalTipi mahalTipi)
    {
        var profil = Profiller.FirstOrDefault(p => p.MahalTipi == mahalTipi);
        return profil?.Kopyala();
    }

    public static MahalFinisajProfili? KullanimSinifinaGore(KullanimSinifi kullanimSinifi)
    {
        var profil = Profiller.FirstOrDefault(p => p.KullanimSinifi == kullanimSinifi);
        return profil?.Kopyala();
    }

    public static MahalFinisajProfili? KodlaBul(string? kod)
    {
        if (string.IsNullOrWhiteSpace(kod))
            return null;

        var profil = Profiller.FirstOrDefault(p => string.Equals(p.Kod, kod, StringComparison.OrdinalIgnoreCase));
        return profil?.Kopyala();
    }

    private static MahalFinisajProfili P(
        string kod,
        string ad,
        MahalTipi mahalTipi,
        KullanimSinifi kullanimSinifi,
        string zeminTipiId,
        string duvarTipiId,
        string tavanTipiId,
        List<string>? uyariNotlari = null,
        string notlar = "")
        => new()
        {
            Kod = kod,
            Ad = ad,
            MahalTipi = mahalTipi,
            KullanimSinifi = kullanimSinifi,
            ZeminTipiId = zeminTipiId,
            DuvarTipiId = duvarTipiId,
            TavanTipiId = tavanTipiId,
            UyariNotlari = uyariNotlari ?? [],
            KaynakKodu = "M36",
            Notlar = notlar
        };
}
