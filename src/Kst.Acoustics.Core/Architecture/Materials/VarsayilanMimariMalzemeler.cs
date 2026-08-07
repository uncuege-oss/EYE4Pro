using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Architecture.Models;

namespace Kst.Acoustics.Core.Architecture.Materials;

/// <summary>
/// M36 icin marka bagimsiz baslangic mimari malzeme katalogu.
/// </summary>
public static class VarsayilanMimariMalzemeler
{
    private static readonly MimariMalzeme[] Malzemeler =
    [
        M(
            "ZEM-SER-R10",
            "Kaymaz seramik zemin",
            MalzemeKategorisi.ZeminKaplamasi,
            [MalzemeTeknikSinifi.IslakHacim, MalzemeTeknikSinifi.KaymazYuzey],
            [MahalTipi.Banyo, MahalTipi.Wc, MahalTipi.Mutfak],
            [KullanimSinifi.IslakHacim, KullanimSinifi.Servis],
            yanginTepkiSinifi: "A1",
            suyaDayanikli: true,
            islakHacimUygun: true,
            kaymazlikSinifi: "R10",
            birimAgirlikKg: 22,
            notlar: "Islak hacim zeminleri icin ilk varsayilan seramik kaplama."),
        M(
            "ZEM-LVT-OFIS",
            "LVT ofis zemin kaplamasi",
            MalzemeKategorisi.ZeminKaplamasi,
            [MalzemeTeknikSinifi.Standart],
            [MahalTipi.Ofis, MahalTipi.AcikOfis, MahalTipi.ToplantiOdasi],
            [KullanimSinifi.AnaKullanim, KullanimSinifi.YardimciKullanim],
            yanginTepkiSinifi: "Bfl-s1",
            kaymazlikSinifi: "R9",
            birimAgirlikKg: 8,
            notlar: "Ofis ve toplanti alanlari icin hafif zemin kaplamasi."),
        M(
            "DUV-ALCI-BOYA",
            "Alci siva uzeri boya",
            MalzemeKategorisi.DuvarKaplamasi,
            [MalzemeTeknikSinifi.Standart],
            [],
            [],
            yanginTepkiSinifi: "A2-s1,d0",
            birimAgirlikKg: 12,
            notlar: "Genel ic duvar finisaji."),
        M(
            "TAV-AKUSTIK-PANEL",
            "Akustik asma tavan paneli",
            MalzemeKategorisi.TavanKaplamasi,
            [MalzemeTeknikSinifi.Akustik],
            [MahalTipi.KonferansSalonu, MahalTipi.ToplantiOdasi, MahalTipi.Derslik, MahalTipi.AcikOfis],
            [KullanimSinifi.AnaKullanim, KullanimSinifi.YardimciKullanim],
            yanginTepkiSinifi: "A2-s1,d0",
            akustikNot: "Yutucu tavan paneli",
            yutuculukAw: 0.75,
            birimAgirlikKg: 4.5,
            notlar: "Toplanti, egitim ve konferans hacimleri icin akustik tavan adayi."),
        M(
            "YAL-TAS-YUNU",
            "Tas yunu yalitim levhasi",
            MalzemeKategorisi.Yalitim,
            [MalzemeTeknikSinifi.YanginaDayanikli, MalzemeTeknikSinifi.IsilYalitim, MalzemeTeknikSinifi.Akustik],
            [],
            [],
            yanginTepkiSinifi: "A1",
            akustikNot: "Bosluklu duvar ve tavan sistemlerinde ses yutumu icin kullanilabilir.",
            isiIletkenlikWmK: 0.037,
            yogunlukKgM3: 50,
            notlar: "Katmanli duvar/doseme/tavan tipleri icin yalitim malzemesi."),
        M(
            "MEM-SU-YALITIM",
            "Surme su yalitim membrani",
            MalzemeKategorisi.Membran,
            [MalzemeTeknikSinifi.SuYalitimi, MalzemeTeknikSinifi.IslakHacim],
            [MahalTipi.Banyo, MahalTipi.Wc, MahalTipi.Mutfak],
            [KullanimSinifi.IslakHacim, KullanimSinifi.Servis],
            suyaDayanikli: true,
            islakHacimUygun: true,
            birimAgirlikKg: 2,
            notlar: "Islak hacim zemin ve duvar alt katmani icin su yalitimi."),
        M(
            "KAPI-YANGIN-60",
            "Yangin dayanimli kapi",
            MalzemeKategorisi.Kapi,
            [MalzemeTeknikSinifi.YanginaDayanikli],
            [MahalTipi.Merdiven, MahalTipi.YanginMerdiveni, MahalTipi.Koridor, MahalTipi.TeknikOda],
            [KullanimSinifi.Sirkulasyon, KullanimSinifi.DuseySirkulasyon, KullanimSinifi.TeknikHacim],
            yanginTepkiSinifi: "EI",
            yanginDayanimDakika: 60,
            birim: MetrajBirimi.Adet,
            birimAgirlikKg: 55,
            notlar: "Kacis ve teknik hacim kapilari icin yangin dayanimli kapi adayi."),
        M(
            "CAM-LOWE-CIFT",
            "Low-e cift cam unitesi",
            MalzemeKategorisi.Cam,
            [MalzemeTeknikSinifi.DisKabuk],
            [],
            [],
            uDegeriWm2K: 1.4,
            birimAgirlikKg: 25,
            notlar: "Pencere ve dis kabuk cam kararlari icin ilk teknik cam verisi."),
        M(
            "BET-C30",
            "Betonarme C30 sinifi",
            MalzemeKategorisi.Beton,
            [MalzemeTeknikSinifi.Tasiyici, MalzemeTeknikSinifi.YanginaDayanikli],
            [],
            [],
            yanginTepkiSinifi: "A1",
            yogunlukKgM3: 2400,
            birim: MetrajBirimi.Metrekup,
            notlar: "Tasiyici elemanlar icin ilk kaba beton malzeme kaydi.")
    ];

    public static IReadOnlyList<MimariMalzeme> Tum => Malzemeler.Select(Kopyala).ToList();

    public static IReadOnlyList<MimariMalzeme> KategoriyeGore(MalzemeKategorisi kategori)
        => Malzemeler
            .Where(m => m.Kategori == kategori)
            .Select(Kopyala)
            .ToList();

    public static MimariMalzeme? KodlaBul(string? kod)
    {
        if (string.IsNullOrWhiteSpace(kod))
            return null;

        var malzeme = Malzemeler.FirstOrDefault(m => string.Equals(m.Kod, kod, StringComparison.OrdinalIgnoreCase));
        return malzeme is null ? null : Kopyala(malzeme);
    }

    private static MimariMalzeme M(
        string kod,
        string ad,
        MalzemeKategorisi kategori,
        List<MalzemeTeknikSinifi> teknikSiniflar,
        List<MahalTipi> uygunMahalTipleri,
        List<KullanimSinifi> uygunKullanimSiniflari,
        string yanginTepkiSinifi = "",
        int yanginDayanimDakika = 0,
        string akustikNot = "",
        double sesAzaltimRw = 0,
        double yutuculukAw = 0,
        double isiIletkenlikWmK = 0,
        double uDegeriWm2K = 0,
        bool suyaDayanikli = false,
        bool islakHacimUygun = false,
        string kaymazlikSinifi = "",
        MetrajBirimi birim = MetrajBirimi.Metrekare,
        double birimAgirlikKg = 0,
        double yogunlukKgM3 = 0,
        string notlar = "")
        => new()
        {
            Id = $"malzeme-{kod.ToLowerInvariant()}",
            Kod = kod,
            Ad = ad,
            Kategori = kategori,
            TeknikSiniflar = teknikSiniflar,
            UygunMahalTipleri = uygunMahalTipleri,
            UygunKullanimSiniflari = uygunKullanimSiniflari,
            Performans = new MalzemePerformansi
            {
                YanginTepkiSinifi = yanginTepkiSinifi,
                YanginDayanimDakika = yanginDayanimDakika,
                AkustikNot = akustikNot,
                SesAzaltimRw = sesAzaltimRw,
                YutuculukAw = yutuculukAw,
                IsiIletkenlikWmK = isiIletkenlikWmK,
                UDegeriWm2K = uDegeriWm2K,
                SuyaDayanikli = suyaDayanikli,
                IslakHacimUygun = islakHacimUygun,
                KaymazlikSinifi = kaymazlikSinifi
            },
            Metraj = new MetrajBilgisi
            {
                VarsayilanBirim = birim,
                BirimAgirlikKg = birimAgirlikKg,
                YogunlukKgM3 = yogunlukKgM3
            },
            KaynakKodu = "M36",
            Notlar = notlar
        };

    private static MimariMalzeme Kopyala(MimariMalzeme malzeme)
        => new()
        {
            Id = malzeme.Id,
            Kod = malzeme.Kod,
            Ad = malzeme.Ad,
            Kategori = malzeme.Kategori,
            TeknikSiniflar = [.. malzeme.TeknikSiniflar],
            UygunMahalTipleri = [.. malzeme.UygunMahalTipleri],
            UygunKullanimSiniflari = [.. malzeme.UygunKullanimSiniflari],
            Performans = new MalzemePerformansi
            {
                YanginTepkiSinifi = malzeme.Performans.YanginTepkiSinifi,
                YanginDayanimDakika = malzeme.Performans.YanginDayanimDakika,
                AkustikNot = malzeme.Performans.AkustikNot,
                SesAzaltimRw = malzeme.Performans.SesAzaltimRw,
                YutuculukAw = malzeme.Performans.YutuculukAw,
                IsiIletkenlikWmK = malzeme.Performans.IsiIletkenlikWmK,
                UDegeriWm2K = malzeme.Performans.UDegeriWm2K,
                SuyaDayanikli = malzeme.Performans.SuyaDayanikli,
                IslakHacimUygun = malzeme.Performans.IslakHacimUygun,
                KaymazlikSinifi = malzeme.Performans.KaymazlikSinifi
            },
            Metraj = new MetrajBilgisi
            {
                VarsayilanBirim = malzeme.Metraj.VarsayilanBirim,
                BirimAgirlikKg = malzeme.Metraj.BirimAgirlikKg,
                YogunlukKgM3 = malzeme.Metraj.YogunlukKgM3,
                BirimMaliyetNotu = malzeme.Metraj.BirimMaliyetNotu,
                MetrajNotu = malzeme.Metraj.MetrajNotu
            },
            KaynakKodu = malzeme.KaynakKodu,
            Notlar = malzeme.Notlar
        };
}
