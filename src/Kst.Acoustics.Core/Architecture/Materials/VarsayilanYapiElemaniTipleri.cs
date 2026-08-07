using System;
using System.Collections.Generic;
using System.Linq;

namespace Kst.Acoustics.Core.Architecture.Materials;

/// <summary>
/// M36 icin marka bagimsiz baslangic yapi elemani tipi katalogu.
/// </summary>
public static class VarsayilanYapiElemaniTipleri
{
    public const string VarsayilanDuvarTipiId = "varsayilan-duvar";
    public const string VarsayilanDosemeTipiId = "varsayilan-doseme";
    public const string VarsayilanIslakHacimDosemeTipiId = "varsayilan-islak-doseme";
    public const string VarsayilanTavanTipiId = "varsayilan-tavan";
    public const string VarsayilanBoyaliTavanTipiId = "varsayilan-boyali-tavan";
    public const string VarsayilanKapiTipiId = "varsayilan-kapi";
    public const string VarsayilanPencereTipiId = "varsayilan-pencere";

    private static readonly YapiElemaniTipi[] Tipler =
    [
        T(
            VarsayilanDuvarTipiId,
            "DUV-GENEL-01",
            "Genel iç duvar tipi",
            YapiElemaniSinifi.Duvar,
            [
                K(1, "İç yüzey boya/sıva", "malzeme-duv-alci-boya", 0.015, MalzemeKatmanFonksiyonu.Siva),
                K(2, "Akustik/ısı yalıtım katmanı", "malzeme-yal-tas-yunu", 0.050, MalzemeKatmanFonksiyonu.Yalitim),
                K(3, "Dış yüzey boya/sıva", "malzeme-duv-alci-boya", 0.015, MalzemeKatmanFonksiyonu.Siva)
            ],
            "M35 varsayılan duvar tipi Id'siyle uyumlu iç duvar tipi."),
        T(
            VarsayilanDosemeTipiId,
            "DOS-GENEL-01",
            "Genel döşeme tipi",
            YapiElemaniSinifi.Doseme,
            [
                K(1, "Betonarme döşeme", "malzeme-bet-c30", 0.200, MalzemeKatmanFonksiyonu.Tasiyici),
                K(2, "Ofis zemin kaplaması", "malzeme-zem-lvt-ofis", 0.005, MalzemeKatmanFonksiyonu.Kaplama)
            ],
            "M35 varsayılan döşeme tipi Id'siyle uyumlu kaba döşeme tipi."),
        T(
            VarsayilanIslakHacimDosemeTipiId,
            "DOS-ISLAK-01",
            "Islak hacim döşeme tipi",
            YapiElemaniSinifi.Doseme,
            [
                K(1, "Betonarme döşeme", "malzeme-bet-c30", 0.200, MalzemeKatmanFonksiyonu.Tasiyici),
                K(2, "Sürme su yalıtım membranı", "malzeme-mem-su-yalitim", 0.003, MalzemeKatmanFonksiyonu.Membran),
                K(3, "Kaymaz seramik zemin", "malzeme-zem-ser-r10", 0.010, MalzemeKatmanFonksiyonu.Kaplama)
            ],
            "Banyo, WC ve mutfak gibi ıslak hacimler için su yalıtımlı ve kaymaz döşeme tipi."),
        T(
            VarsayilanTavanTipiId,
            "TAV-AKUSTIK-01",
            "Akustik asma tavan tipi",
            YapiElemaniSinifi.Tavan,
            [
                K(1, "Taş yünü yutucu katman", "malzeme-yal-tas-yunu", 0.050, MalzemeKatmanFonksiyonu.Yalitim),
                K(2, "Akustik tavan paneli", "malzeme-tav-akustik-panel", 0.020, MalzemeKatmanFonksiyonu.Bitis)
            ],
            "Toplantı, eğitim ve konferans hacimleri için ilk akustik tavan tipi."),
        T(
            VarsayilanBoyaliTavanTipiId,
            "TAV-BOYALI-01",
            "Boyalı sıva tavan tipi",
            YapiElemaniSinifi.Tavan,
            [
                K(1, "Alçı sıva üzeri boya", "malzeme-duv-alci-boya", 0.015, MalzemeKatmanFonksiyonu.Siva)
            ],
            "Konut, teknik hacim ve genel mahaller için akustik gereksinimi olmayan tavan tipi."),
        T(
            VarsayilanKapiTipiId,
            "KAPI-YANGIN-60",
            "Yangın dayanımlı kapı tipi",
            YapiElemaniSinifi.Kapi,
            [
                K(1, "Yangın kapısı kanadı", "malzeme-kapi-yangin-60", 0.050, MalzemeKatmanFonksiyonu.Bitis)
            ],
            "Aciklik3D kapı placeholder'ına atanacak ilk yangın dayanımlı kapı tipi.",
            MetrajBirimi.Adet),
        T(
            VarsayilanPencereTipiId,
            "PENC-CAM-LOWE",
            "Low-e çift cam pencere tipi",
            YapiElemaniSinifi.Pencere,
            [
                K(1, "Low-e çift cam ünitesi", "malzeme-cam-lowe-cift", 0.024, MalzemeKatmanFonksiyonu.Bitis)
            ],
            "Aciklik3D pencere placeholder'ına atanacak ilk cam tipi.")
    ];

    public static IReadOnlyList<YapiElemaniTipi> Tum => Tipler.Select(Kopyala).ToList();

    public static IReadOnlyList<YapiElemaniTipi> SinifaGore(YapiElemaniSinifi sinif)
        => Tipler
            .Where(t => t.Sinif == sinif)
            .Select(Kopyala)
            .ToList();

    public static YapiElemaniTipi? IdIleBul(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var tip = Tipler.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));
        return tip is null ? null : Kopyala(tip);
    }

    public static YapiElemaniTipi? KodlaBul(string? kod)
    {
        if (string.IsNullOrWhiteSpace(kod))
            return null;

        var tip = Tipler.FirstOrDefault(t => string.Equals(t.Kod, kod, StringComparison.OrdinalIgnoreCase));
        return tip is null ? null : Kopyala(tip);
    }

    private static YapiElemaniTipi T(
        string id,
        string kod,
        string ad,
        YapiElemaniSinifi sinif,
        List<YapiElemaniKatmani> katmanlar,
        string notlar,
        MetrajBirimi birim = MetrajBirimi.Metrekare)
        => new()
        {
            Id = id,
            Kod = kod,
            Ad = ad,
            Sinif = sinif,
            Katmanlar = katmanlar,
            VarsayilanMetrajBirimi = birim,
            KaynakKodu = "M36",
            Notlar = notlar
        };

    private static YapiElemaniKatmani K(
        int siraNo,
        string ad,
        string malzemeId,
        double kalinlikM,
        MalzemeKatmanFonksiyonu fonksiyon)
        => new()
        {
            Id = $"katman-{siraNo}-{malzemeId}",
            SiraNo = siraNo,
            Ad = ad,
            MalzemeId = malzemeId,
            KalinlikM = kalinlikM,
            Fonksiyon = fonksiyon
        };

    private static YapiElemaniTipi Kopyala(YapiElemaniTipi tip)
        => new()
        {
            Id = tip.Id,
            Kod = tip.Kod,
            Ad = tip.Ad,
            Sinif = tip.Sinif,
            Katmanlar = tip.Katmanlar
                .Select(k => new YapiElemaniKatmani
                {
                    Id = k.Id,
                    SiraNo = k.SiraNo,
                    Ad = k.Ad,
                    MalzemeId = k.MalzemeId,
                    KalinlikM = k.KalinlikM,
                    Fonksiyon = k.Fonksiyon,
                    Notlar = k.Notlar
                })
                .ToList(),
            VarsayilanMetrajBirimi = tip.VarsayilanMetrajBirimi,
            KaynakKodu = tip.KaynakKodu,
            Notlar = tip.Notlar
        };
}
