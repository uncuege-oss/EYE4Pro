using Kst.Acoustics.Core.Architecture.Models;

namespace Kst.Acoustics.Core.Architecture.Templates;

/// <summary>
/// M31 icin bina islevine gore baslangic mahal sablonlari.
/// </summary>
public static class VarsayilanMahalSablonlari
{
    private static readonly MahalSablonu[] Sablonlar =
    [
        S("KON-SALON", "Salon", MahalTipi.Salon, KullanimSinifi.AnaKullanim, [BinaIslevi.Konut, BinaIslevi.Villa], 1, 16, 28, 2.6, 0, true, true),
        S("KON-YATAK", "Yatak Odası", MahalTipi.YatakOdasi, KullanimSinifi.AnaKullanim, [BinaIslevi.Konut, BinaIslevi.Villa, BinaIslevi.Konaklama], 2, 10, 14, 2.6, 0, true, true),
        S("KON-MUTFAK", "Mutfak", MahalTipi.Mutfak, KullanimSinifi.Servis, [BinaIslevi.Konut, BinaIslevi.Villa], 1, 8, 12, 2.6, 0, true, true),
        S("KON-BANYO", "Banyo", MahalTipi.Banyo, KullanimSinifi.IslakHacim, [BinaIslevi.Konut, BinaIslevi.Villa, BinaIslevi.Konaklama], 1, 4, 6, 2.4, 0, true, false, sameAxis: true),
        S("KON-WC", "WC", MahalTipi.Wc, KullanimSinifi.IslakHacim, [BinaIslevi.Konut, BinaIslevi.Villa], 1, 2, 3, 2.4, 0, false, false, sameAxis: true),
        S("KON-ANTRE", "Antre", MahalTipi.Antre, KullanimSinifi.Sirkulasyon, [BinaIslevi.Konut, BinaIslevi.Villa], 1, 3, 5, 2.4),
        S("KON-KORIDOR", "Koridor / Hol", MahalTipi.Koridor, KullanimSinifi.Sirkulasyon, [BinaIslevi.Konut, BinaIslevi.Villa], 1, 4, 8, 2.4),
        S("KON-DEPO", "Depo", MahalTipi.Depo, KullanimSinifi.Depolama, [BinaIslevi.Konut, BinaIslevi.Villa], 1, 2, 4, 2.4, 0, false),

        S("OFIS-ACIK", "Açık Ofis", MahalTipi.AcikOfis, KullanimSinifi.AnaKullanim, [BinaIslevi.Ofis, BinaIslevi.KamuIdari], 1, 30, 60, 2.8, 6.0, true, true),
        S("OFIS-TOPLANTI", "Toplantı Odası", MahalTipi.ToplantiOdasi, KullanimSinifi.YardimciKullanim, [BinaIslevi.Ofis, BinaIslevi.KamuIdari, BinaIslevi.KarmaKullanim], 1, 12, 24, 2.8, 2.0, true, true),
        S("OFIS-RESEPSIYON", "Resepsiyon", MahalTipi.Resepsiyon, KullanimSinifi.YardimciKullanim, [BinaIslevi.Ofis, BinaIslevi.KamuIdari, BinaIslevi.Konaklama], 1, 10, 18, 2.8, 0, false, true),
        S("OFIS-ARSIV", "Arşiv", MahalTipi.Arsiv, KullanimSinifi.Depolama, [BinaIslevi.Ofis, BinaIslevi.KamuIdari], 1, 6, 12, 2.6, 0, false),
        S("OFIS-SERVER", "Server Odası", MahalTipi.ServerOdasi, KullanimSinifi.TeknikHacim, [BinaIslevi.Ofis, BinaIslevi.KamuIdari], 1, 4, 8, 2.6, 0, false),
        S("OFIS-WC", "WC Grubu", MahalTipi.Wc, KullanimSinifi.IslakHacim, [BinaIslevi.Ofis, BinaIslevi.KamuIdari], 1, 6, 12, 2.4, 0, true, false, sameAxis: true),
        S("OFIS-TEKNIK", "Teknik Oda", MahalTipi.TeknikOda, KullanimSinifi.TeknikHacim, [BinaIslevi.Ofis, BinaIslevi.KamuIdari], 1, 6, 10, 2.6),

        S("EGT-DERSLIK", "Derslik", MahalTipi.Derslik, KullanimSinifi.AnaKullanim, [BinaIslevi.Egitim], 1, 40, 55, 3.0, 1.8, true, true),
        S("EGT-LAB", "Laboratuvar", MahalTipi.Laboratuvar, KullanimSinifi.AnaKullanim, [BinaIslevi.Egitim], 1, 45, 70, 3.2, 2.5, false, true),
        S("EGT-OGRETMEN", "Öğretmen Odası", MahalTipi.OgretmenOdasi, KullanimSinifi.YardimciKullanim, [BinaIslevi.Egitim], 1, 16, 30, 2.8, 0, true, true),
        S("EGT-KUTUPHANE", "Kütüphane", MahalTipi.Kutuphane, KullanimSinifi.YardimciKullanim, [BinaIslevi.Egitim, BinaIslevi.Kulturel], 1, 30, 60, 3.0, 2.0, false, true),
        S("EGT-WC", "Öğrenci WC", MahalTipi.Wc, KullanimSinifi.IslakHacim, [BinaIslevi.Egitim], 1, 10, 18, 2.4, 0, true, false, sameAxis: true),
        S("EGT-KORIDOR", "Ana Koridor", MahalTipi.Koridor, KullanimSinifi.Sirkulasyon, [BinaIslevi.Egitim], 1, 25, 45, 2.8),

        S("SAG-MUAYENE", "Muayene Odası", MahalTipi.MuayeneOdasi, KullanimSinifi.AnaKullanim, [BinaIslevi.Saglik], 1, 12, 18, 2.8, 0, true, true),
        S("SAG-BEKLEME", "Bekleme Alanı", MahalTipi.BeklemeAlani, KullanimSinifi.YardimciKullanim, [BinaIslevi.Saglik], 1, 20, 40, 2.8, 1.2, true, true),
        S("SAG-HASTA", "Hasta Odası", MahalTipi.HastaOdasi, KullanimSinifi.AnaKullanim, [BinaIslevi.Saglik], 1, 16, 24, 2.8, 0, false, true),
        S("SAG-PERSONEL", "Personel Odası", MahalTipi.PersonelOdasi, KullanimSinifi.YardimciKullanim, [BinaIslevi.Saglik], 1, 10, 18, 2.8),
        S("SAG-WC", "Hasta / Ziyaretçi WC", MahalTipi.Wc, KullanimSinifi.IslakHacim, [BinaIslevi.Saglik], 1, 8, 14, 2.4, 0, true, false, sameAxis: true),
        S("SAG-TEKNIK", "Medikal Teknik Oda", MahalTipi.TeknikOda, KullanimSinifi.TeknikHacim, [BinaIslevi.Saglik], 1, 8, 14, 2.8),

        S("KONAK-ODA", "Konaklama Odası", MahalTipi.YatakOdasi, KullanimSinifi.AnaKullanim, [BinaIslevi.Konaklama], 1, 18, 24, 2.7, 0, true, true),
        S("KONAK-LOBI", "Lobi / Resepsiyon", MahalTipi.Resepsiyon, KullanimSinifi.YardimciKullanim, [BinaIslevi.Konaklama], 1, 30, 60, 3.2, 0, true, true),
        S("KONAK-REST", "Kahvaltı / Restoran", MahalTipi.Restoran, KullanimSinifi.YardimciKullanim, [BinaIslevi.Konaklama, BinaIslevi.RestoranVeYemeIcme], 1, 35, 70, 3.0, 1.5, false, true),
        S("KONAK-DEPO", "Kat Deposu", MahalTipi.Depo, KullanimSinifi.Depolama, [BinaIslevi.Konaklama], 1, 4, 8, 2.4, 0, false),
        S("KONAK-KORIDOR", "Oda Koridoru", MahalTipi.Koridor, KullanimSinifi.Sirkulasyon, [BinaIslevi.Konaklama], 1, 20, 40, 2.6),

        S("KULT-SERGI", "Sergi / Çok Amaçlı Alan", MahalTipi.TicariAlan, KullanimSinifi.AnaKullanim, [BinaIslevi.Kulturel], 1, 50, 100, 3.5, 2.0, false, true),
        S("KULT-KUTUPHANE", "Kütüphane", MahalTipi.Kutuphane, KullanimSinifi.AnaKullanim, [BinaIslevi.Kulturel], 1, 40, 80, 3.0, 2.0, false, true),
        S("KONF-SALON", "Konferans Salonu", MahalTipi.KonferansSalonu, KullanimSinifi.AnaKullanim, [BinaIslevi.KonferansVeToplanti, BinaIslevi.Kulturel], 1, 120, 250, 4.5, 1.2, true, false),
        S("KONF-FUAYE", "Fuaye", MahalTipi.Fuaye, KullanimSinifi.Sirkulasyon, [BinaIslevi.KonferansVeToplanti, BinaIslevi.Kulturel], 1, 40, 90, 3.5, 0.6, true, true),
        S("KONF-SAHNE", "Sahne", MahalTipi.Sahne, KullanimSinifi.AnaKullanim, [BinaIslevi.KonferansVeToplanti, BinaIslevi.Kulturel], 1, 35, 70, 5.0, 0, false),
        S("KONF-KULIS", "Kulis", MahalTipi.Kulis, KullanimSinifi.YardimciKullanim, [BinaIslevi.KonferansVeToplanti, BinaIslevi.Kulturel], 1, 12, 24, 2.8),
        S("KONF-WC", "Fuaye WC", MahalTipi.Wc, KullanimSinifi.IslakHacim, [BinaIslevi.KonferansVeToplanti, BinaIslevi.Kulturel], 1, 12, 24, 2.6, 0, true, false, sameAxis: true),

        S("TIC-MAGAZA", "Mağaza / Satış Alanı", MahalTipi.Magaza, KullanimSinifi.AnaKullanim, [BinaIslevi.Ticari, BinaIslevi.KarmaKullanim], 1, 40, 80, 3.5, 2.0, true, true),
        S("TIC-SERVIS", "Servis / Depo", MahalTipi.Depo, KullanimSinifi.Servis, [BinaIslevi.Ticari, BinaIslevi.RestoranVeYemeIcme], 1, 8, 16, 2.6),
        S("TIC-WC", "Müşteri WC", MahalTipi.Wc, KullanimSinifi.IslakHacim, [BinaIslevi.Ticari, BinaIslevi.RestoranVeYemeIcme], 1, 6, 12, 2.4, 0, true, false, sameAxis: true),
        S("REST-SALON", "Yeme İçme Salonu", MahalTipi.Restoran, KullanimSinifi.AnaKullanim, [BinaIslevi.RestoranVeYemeIcme], 1, 40, 80, 3.0, 1.6, true, true),
        S("REST-MUTFAK", "Endüstriyel Mutfak", MahalTipi.Mutfak, KullanimSinifi.Servis, [BinaIslevi.RestoranVeYemeIcme], 1, 20, 45, 3.0, 0, true),

        S("END-URETIM", "Üretim / Atölye Alanı", MahalTipi.Atolye, KullanimSinifi.AnaKullanim, [BinaIslevi.Endustri], 1, 120, 250, 5.0, 8.0, true),
        S("END-TEKNIK", "Teknik Hacim", MahalTipi.TeknikOda, KullanimSinifi.TeknikHacim, [BinaIslevi.Endustri, BinaIslevi.DepoVeLojistik], 1, 20, 40, 3.2),
        S("END-PERSONEL", "Personel Odası", MahalTipi.PersonelOdasi, KullanimSinifi.YardimciKullanim, [BinaIslevi.Endustri, BinaIslevi.DepoVeLojistik], 1, 16, 30, 2.8),
        S("DEPO-ANA", "Depolama Alanı", MahalTipi.Depo, KullanimSinifi.AnaKullanim, [BinaIslevi.DepoVeLojistik], 1, 200, 500, 6.0, 0, true),
        S("DEPO-YUKLEME", "Yükleme / Sevkiyat Alanı", MahalTipi.TicariAlan, KullanimSinifi.Servis, [BinaIslevi.DepoVeLojistik], 1, 50, 120, 5.0),
        S("ORT-MERDIVEN", "Merdiven", MahalTipi.Merdiven, KullanimSinifi.DuseySirkulasyon, TumIslevler(), 1, 12, 20, 2.4, 0, true, false, sameAxis: true),
        S("ORT-ASANSOR", "Asansör", MahalTipi.Asansor, KullanimSinifi.DuseySirkulasyon, [BinaIslevi.Ofis, BinaIslevi.Egitim, BinaIslevi.Saglik, BinaIslevi.Konaklama, BinaIslevi.Kulturel, BinaIslevi.KonferansVeToplanti, BinaIslevi.Ticari, BinaIslevi.KamuIdari, BinaIslevi.KarmaKullanim], 1, 3, 5, 2.4, 0, false, false, sameAxis: true),
        S("ORT-SAFT", "Tesisat Şaftı", MahalTipi.Saft, KullanimSinifi.TeknikHacim, TumIslevler(), 1, 1, 3, 2.4, 0, true, false, sameAxis: true),
        S("ORT-ELEKTRIK", "Elektrik Odası", MahalTipi.ElektrikOdasi, KullanimSinifi.TeknikHacim, TumIslevler(), 1, 4, 8, 2.6),
    ];

    public static IReadOnlyList<MahalSablonu> Tum => Sablonlar.Select(Kopyala).ToList();

    public static IReadOnlyList<MahalSablonu> BinaIslevineGore(BinaIslevi islev)
        => Sablonlar
            .Where(s => s.UygunBinaIslevleri.Contains(islev))
            .Select(Kopyala)
            .ToList();

    private static MahalSablonu S(
        string kod,
        string ad,
        MahalTipi mahalTipi,
        KullanimSinifi kullanimSinifi,
        List<BinaIslevi> uygunIslevler,
        int adet,
        double minAlan,
        double idealAlan,
        double minYukseklik,
        double kisiBasinaAlan = 0,
        bool zorunlu = true,
        bool disCephe = false,
        bool sameAxis = false)
        => new()
        {
            Kod = kod,
            VarsayilanAd = ad,
            MahalTipi = mahalTipi,
            KullanimSinifi = kullanimSinifi,
            UygunBinaIslevleri = uygunIslevler,
            VarsayilanAdet = adet,
            MinimumAlanM2 = minAlan,
            IdealAlanM2 = idealAlan,
            MinimumNetYukseklikM = minYukseklik,
            KisiBasinaAlanM2 = kisiBasinaAlan,
            Zorunlu = zorunlu,
            DisCepheGerekli = disCephe,
            AyniAkstaSureklilikGerekli = sameAxis
        };

    private static List<BinaIslevi> TumIslevler()
        => Enum.GetValues<BinaIslevi>()
            .Where(i => i != BinaIslevi.Tanimsiz)
            .ToList();

    private static MahalSablonu Kopyala(MahalSablonu s)
        => new()
        {
            Kod = s.Kod,
            VarsayilanAd = s.VarsayilanAd,
            MahalTipi = s.MahalTipi,
            KullanimSinifi = s.KullanimSinifi,
            UygunBinaIslevleri = [.. s.UygunBinaIslevleri],
            VarsayilanAdet = s.VarsayilanAdet,
            MinimumAlanM2 = s.MinimumAlanM2,
            IdealAlanM2 = s.IdealAlanM2,
            MinimumNetYukseklikM = s.MinimumNetYukseklikM,
            KisiBasinaAlanM2 = s.KisiBasinaAlanM2,
            Zorunlu = s.Zorunlu,
            YakinOlmali = [.. s.YakinOlmali],
            UzakDurmali = [.. s.UzakDurmali],
            DisCepheGerekli = s.DisCepheGerekli,
            AyniAkstaSureklilikGerekli = s.AyniAkstaSureklilikGerekli
        };
}
