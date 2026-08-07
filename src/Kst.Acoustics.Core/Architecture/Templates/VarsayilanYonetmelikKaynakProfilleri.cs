using System;
using Kst.Acoustics.Core.Architecture.Rules;

namespace Kst.Acoustics.Core.Architecture.Templates;

/// <summary>
/// M34 icin baslangic kaynak profilleri. Bu katalog on kontrol iz kaydi saglar;
/// kural degerleri uygulanmadan once guncel resmi metin ayrica kontrol edilmelidir.
/// </summary>
public static class VarsayilanYonetmelikKaynakProfilleri
{
    private static readonly DateOnly DogrulamaTarihi = new(2026, 6, 12);

    private static readonly IReadOnlyList<YonetmelikKaynakProfili> Profiller =
    [
        new()
        {
            Kod = YonetmelikOnKontrolServisi.VarsayilanVeriKaynakKodu,
            Ad = "EYE4PRO Mimari Veri Ön Kontrolü",
            YayinlayanKurum = "EYE4PRO",
            SurumTarihi = DogrulamaTarihi,
            ResmiKaynakMi = false,
            GuncelMetinTekrarKontrolGerektirir = false,
            SonDogrulamaTarihi = DogrulamaTarihi,
            ResmiKaynakUrl = "",
            Aciklama = "Bina işlevi, ihtiyaç programı, mahal verisi ve alan oranı gibi model içi veri kontrollerinin kaynak profili.",
            KuralKodlari = YonetmelikOnKontrolServisi.IlkVeriKuralKodlari.ToList()
        },
        new()
        {
            Kod = "PAIY",
            Ad = "Planlı Alanlar İmar Yönetmeliği",
            YayinlayanKurum = "Çevre, Şehircilik ve İklim Değişikliği Bakanlığı",
            SurumTarihi = new DateOnly(2017, 7, 3),
            ResmiGazeteSayisi = "30113",
            ResmiKaynakUrl = "https://www.resmigazete.gov.tr/eskiler/2017/07/20170703.htm",
            SonDogrulamaTarihi = DogrulamaTarihi,
            Aciklama = "Planlı alanlarda yapılaşma, yapı ruhsatı ve imar uygulamalarına ilişkin ana kaynak profili."
        },
        new()
        {
            Kod = "BYKHY",
            Ad = "Binaların Yangından Korunması Hakkında Yönetmelik",
            YayinlayanKurum = "Bakanlar Kurulu / Cumhurbaşkanlığı",
            SurumTarihi = new DateOnly(2007, 12, 19),
            ResmiGazeteSayisi = "26735",
            ResmiKaynakUrl = "https://www.resmigazete.gov.tr/eskiler/2007/12/20071219.htm",
            SonDogrulamaTarihi = DogrulamaTarihi,
            Aciklama = "Yangın güvenliği, kaçış, merdiven, kompartıman ve tesisat koordinasyonu kontrolleri için kaynak profili."
        },
        new()
        {
            Kod = "OTOPARK",
            Ad = "Otopark Yönetmeliği",
            YayinlayanKurum = "Çevre, Şehircilik ve İklim Değişikliği Bakanlığı",
            SurumTarihi = new DateOnly(2018, 2, 22),
            ResmiGazeteSayisi = "30340",
            ResmiKaynakUrl = "https://www.resmigazete.gov.tr/eskiler/2018/02/20180222.htm",
            SonDogrulamaTarihi = DogrulamaTarihi,
            Aciklama = "Otopark ihtiyacı, araç sayısı ve yerleşim kararları için kaynak profili."
        },
        new()
        {
            Kod = "TBDY",
            Ad = "Türkiye Bina Deprem Yönetmeliği",
            YayinlayanKurum = "Afet ve Acil Durum Yönetimi Başkanlığı",
            SurumTarihi = new DateOnly(2018, 3, 18),
            ResmiGazeteSayisi = "30364 Mükerrer",
            ResmiKaynakUrl = "https://www.resmigazete.gov.tr/eskiler/2018/03/20180318M1.htm",
            SonDogrulamaTarihi = DogrulamaTarihi,
            Aciklama = "Taşıyıcı sistem, düzensizlik, kat ve çekirdek kararlarının yapı mühendisliği koordinasyonu için kaynak profili."
        },
        new()
        {
            Kod = "BEPY",
            Ad = "Binalarda Enerji Performansı Yönetmeliği",
            YayinlayanKurum = "Çevre, Şehircilik ve İklim Değişikliği Bakanlığı",
            SurumTarihi = new DateOnly(2008, 12, 5),
            ResmiGazeteSayisi = "27075",
            ResmiKaynakUrl = "https://www.resmigazete.gov.tr/eskiler/2008/12/20081205.htm",
            SonDogrulamaTarihi = DogrulamaTarihi,
            Aciklama = "Kabuk, enerji performansı ve mekanik sistem koordinasyonu için kaynak profili."
        },
        new()
        {
            Kod = "ERISILEBILIRLIK",
            Ad = "Erişilebilirlik İzleme ve Denetleme Yönetmeliği",
            YayinlayanKurum = "Aile ve Sosyal Hizmetler Bakanlığı",
            SurumTarihi = new DateOnly(2013, 7, 20),
            ResmiGazeteSayisi = "28713",
            ResmiKaynakUrl = "https://www.resmigazete.gov.tr/eskiler/2013/07/20130720.htm",
            SonDogrulamaTarihi = DogrulamaTarihi,
            Aciklama = "Erişilebilir rota, rampa, wc, kapı ve dolaşım kararları için kaynak profili."
        }
    ];

    public static IReadOnlyList<YonetmelikKaynakProfili> Tum => Profiller;

    public static IReadOnlyList<YonetmelikKaynakProfili> ResmiKaynaklar
        => Profiller.Where(p => p.ResmiKaynakMi).ToList();

    public static YonetmelikKaynakProfili? Bul(string? kod)
    {
        if (string.IsNullOrWhiteSpace(kod))
            return null;

        return Profiller.FirstOrDefault(p => string.Equals(p.Kod, kod.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static YonetmelikKaynakProfili? KuralKaynagiBul(string? kuralKodu)
    {
        if (string.IsNullOrWhiteSpace(kuralKodu))
            return null;

        return Profiller.FirstOrDefault(p =>
            p.KuralKodlari.Any(k => string.Equals(k, kuralKodu.Trim(), StringComparison.OrdinalIgnoreCase)));
    }
}
