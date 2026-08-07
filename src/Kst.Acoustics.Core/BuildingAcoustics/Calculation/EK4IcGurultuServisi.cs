using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.BuildingAcoustics.Models;

namespace Kst.Acoustics.Core.BuildingAcoustics.Calculation;

/// <summary>
/// Yönetmelik EK-4 Tablo 4.1 — akustik performans sınıfına bağlı izin verilen mekan içi
/// en yüksek gürültü düzeyleri (L_Aeq, dBA).
///
/// Tablo aritmetik seridir (docx kaynak metniyle birebir doğrulandı, 2026-07-17):
/// her satır için A = C−8, B = C−4, D = C+4, E = C+8, F = C+12. Bu yüzden yalnız C sınıfı
/// değeri kodlanır, diğer sınıflar türetilir.
///
/// Eşleme, EK-2 kategori granülaritesinden daha incedir (ör. konutta mutfak/banyo ayrımı,
/// sağlıkta özel/çok yataklı oda ayrımı) — bu yüzden servis kendi anahtar kelime eşleyicisini
/// taşır. Eşleşme bulunamazsa resmî akış null/VeriEksik üretir; yalnız açıkça adlandırılmış
/// geriye uyumlu ön-tahmin API'si eski 35 dBA değerine düşebilir.
/// </summary>
public static class EK4IcGurultuServisi
{
    /// Tablo 4.1 satırı: satır adı, C sınıfı L_Aeq değeri, zaman dilimi, eşleme anahtarları.
    public sealed record Tablo41Satiri(string Ad, double CDegeriDb, string ZamanDilimi, string[] Anahtarlar);

    /// Bulunan limit: satır bilgisi + istenen sınıf için türetilmiş L_Aeq sınırı.
    public sealed record IcGurultuLimiti(Tablo41Satiri Satir, AkustikPerformansSinifi Sinif, double LimitDb);

    // ── Tablo 4.1 verisi (docx'ten birebir; anahtarlar Türkçe normalize aranır) ──

    private static readonly IReadOnlyDictionary<BinaIslevi, Tablo41Satiri[]> _tablo = new Dictionary<BinaIslevi, Tablo41Satiri[]>
    {
        [BinaIslevi.Konut] =
        [
            new("Yatak Odaları", 34, "Gece", ["YATAK", "EBEVEYN", "COCUK ODASI", "UYKU"]),
            new("Yaşam Alanları", 39, "24 saat", ["YASAM", "SALON", "OTURMA", "MISAFIR"]),
            new("Mutfaklar", 39, "24 saat", ["MUTFAK"]),
        ],
        [BinaIslevi.EgitimTesisleri] =
        [
            new("Derslikler", 39, "Gündüz-Akşam", ["DERSLIK", "SINIF", "DERSHANE"]),
            new("Özel Derslikler", 44, "Gündüz-Akşam", ["OZEL DERSLIK", "MUZIK", "DANS", "RESIM", "EL ISI", "ATOLYE DERSLIK"]),
            new("İdari Odalar", 39, "Gündüz-Akşam", ["IDARI", "MUDUR", "OGRETMENLER", "OFIS"]),
            new("Spor Salonu", 49, "Gündüz-Akşam", ["SPOR"]),
            new("Okuma Odaları", 39, "Gündüz-Akşam", ["OKUMA", "KUTUPHANE", "ETUT"]),
            new("Sirkülasyon Alanları", 49, "Gündüz-Akşam", ["SIRKULASYON", "KORIDOR", "HOL", "MERDIVEN", "ANTRE", "GIRIS"]),
            new("Kreş Oyun-Yemek Alanları", 44, "Gündüz", ["OYUN", "YEMEK ALANI", "KRES"]),
            new("Kreş Yatak Odaları", 34, "Gündüz", ["YATAK", "UYKU"]),
        ],
        [BinaIslevi.SaglikVeYasliEvleri] =
        [
            new("Özel Hasta Odaları", 34, "24 saat", ["OZEL HASTA", "TEK KISILIK", "HASTA ODASI", "YASLI ODASI"]),
            new("Çok Yataklı Odalar", 39, "24 saat", ["COK YATAKLI", "KOGUS"]),
            new("Ameliyathaneler", 39, "24 saat", ["AMELIYATHANE", "AMELIYAT"]),
            new("Muayene-Tedavi Odaları", 39, "24 saat", ["MUAYENE", "TEDAVI", "POLIKLINIK"]),
            new("Laboratuvarlar", 44, "24 saat", ["LABORATUVAR", "LAB"]),
            new("Sirkülasyon Alanları", 49, "24 saat", ["SIRKULASYON", "KORIDOR", "HOL", "BEKLEME", "MERDIVEN", "GIRIS"]),
        ],
        [BinaIslevi.BuroIdari] =
        [
            new("Özel Odalar", 44, "Gündüz-Akşam", ["OZEL ODA", "BIREYSEL", "YONETICI", "MUDUR", "OFIS ODA"]),
            new("Açık Planlı Alanlar", 44, "Gündüz-Akşam", ["ACIK PLAN", "ACIK OFIS", "OPEN OFFICE"]),
            new("Toplantı Odaları", 39, "Gündüz-Akşam", ["TOPLANTI", "KONFERANS ODA"]),
            new("Telekonferans Odaları", 34, "Gündüz-Akşam", ["TELEKONFERANS", "VIDEO KONFERANS"]),
            new("Dinlenme Alanları", 44, "Gündüz-Akşam", ["DINLENME", "MOLA", "SOSYAL ALAN"]),
            new("Mahkeme Salonları", 39, "Gündüz", ["MAHKEME", "DURUSMA"]),
            new("Sirkülasyon Alanları", 49, "Gündüz-Akşam", ["SIRKULASYON", "KORIDOR", "HOL", "BEKLEME", "MERDIVEN", "GIRIS"]),
        ],
        [BinaIslevi.Konaklama] =
        [
            new("Yatak Odaları", 34, "Gece", ["YATAK", "MISAFIR ODA", "ODA", "SUIT"]),
            new("Lokantalar", 49, "24 saat", ["LOKANTA", "RESTORAN", "YEME", "KAHVALTI"]),
            new("Hizmet Destek Alanları", 54, "24 saat", ["HIZMET", "DESTEK", "CAMASIR", "SERVIS"]),
            new("Sirkülasyon Alanları", 49, "24 saat", ["SIRKULASYON", "KORIDOR", "LOBI", "HOL", "MERDIVEN", "GIRIS"]),
        ],
        [BinaIslevi.Yurt] =
        [
            new("Yatakhane", 34, "Gece", ["YATAKHANE", "YATAK", "UYKU"]),
            new("Etüd Odası", 39, "Gündüz-Akşam", ["ETUT", "ETUD", "CALISMA"]),
            new("Yemekhane", 49, "24 saat", ["YEMEKHANE", "YEMEK"]),
            new("Sirkülasyon Alanları", 49, "24 saat", ["SIRKULASYON", "KORIDOR", "HOL", "MERDIVEN", "GIRIS"]),
        ],
        [BinaIslevi.KulturelTesisler] =
        [
            new("Tiyatro-Konferans Salonları-Oditoryum", 39, "24 saat", ["TIYATRO", "KONFERANS", "ODITORYUM", "COK AMACLI SALON"]),
            new("Sinema Salonları", 39, "24 saat", ["SINEMA"]),
            new("Konser Salonları", 34, "24 saat", ["KONSER"]),
            new("Müzeler", 44, "Gündüz", ["MUZE", "SERGI", "GALERI"]),
            new("Kütüphaneler", 39, "24 saat", ["KUTUPHANE", "OKUMA"]),
            new("Müzik-TV Stüdyoları", 29, "24 saat", ["STUDYO", "KAYIT", "MUZIK ODASI", "TV"]),
            new("Sirkülasyon Alanları", 49, "24 saat", ["SIRKULASYON", "KORIDOR", "FUAYE", "HOL", "GIRIS"]),
        ],
        [BinaIslevi.TicariTesisler] =
        [
            new("Mağaza-Dükkan", 49, "Gündüz-Akşam", ["MAGAZA", "DUKKAN", "SATIS"]),
            new("Alışveriş Merkezleri", 54, "Gündüz-Akşam", ["ALISVERIS", "AVM", "GALERI", "ATRIUM"]),
            new("Süpermarketler", 54, "Gündüz-Akşam", ["SUPERMARKET", "MARKET"]),
            new("Postane - Genel Bankacılık", 49, "Gündüz-Akşam", ["POSTANE", "BANKA", "GISELER"]),
            new("Sirkülasyon Alanları", 49, "24 saat", ["SIRKULASYON", "KORIDOR", "HOL", "GIRIS"]),
        ],
        [BinaIslevi.Terminaller] =
        [
            new("Bekleme Alanları", 49, "24 saat", ["BEKLEME", "TERMINAL", "PERON", "GATE"]),
            new("Personel Ofis-Dinlenme Odaları", 44, "24 saat", ["PERSONEL", "OFIS", "DINLENME"]),
        ],
        [BinaIslevi.DiniTesisler] =
        [
            new("İbadet Alanları", 39, "24 saat", ["IBADET", "CAMI", "MESCIT", "KILISE", "HAVRA"]),
        ],
        [BinaIslevi.EglenceSporTesisleri] =
        [
            new("Lokantalar-Yemek Alanları", 49, "24 saat", ["LOKANTA", "RESTORAN", "YEMEK"]),
            new("Eğlence Yerleri", 59, "Gece", ["EGLENCE", "BAR", "KAFE", "GAZINO", "DUGUN", "CANLI MUZIK"]),
            new("Spor Salonları", 49, "Gündüz", ["SPOR SALON", "SPOR"]),
            new("Yüzme Havuzu", 49, "Gündüz", ["YUZME", "HAVUZ"]),
        ],
        [BinaIslevi.SanayiTesisleri] =
        [
            new("Laboratuvar Alanları", 54, "24 saat", ["LABORATUVAR", "LAB"]),
            new("Hassas Montaj veya Ölçüm Alanları", 49, "24 saat", ["HASSAS", "MONTAJ", "OLCUM", "KALIBRASYON"]),
            new("Kontrol Odaları", 59, "24 saat", ["KONTROL"]),
            new("Personel Ofis-Dinlenme Odaları", 44, "24 saat", ["PERSONEL", "OFIS", "DINLENME"]),
            new("Sağlık Odaları", 39, "24 saat", ["SAGLIK", "REVIR"]),
            new("Sirkülasyon Alanları", 49, "24 saat", ["SIRKULASYON", "KORIDOR", "HOL", "GIRIS"]),
        ],
    };

    /// Tablo 4.1 aritmetik serisi: sınıf başına C değerinden türetme (A=C−8 … F=C+12).
    public static double SinifIcinDeger(double cDegeriDb, AkustikPerformansSinifi sinif) =>
        cDegeriDb + 4.0 * ((int)sinif - (int)AkustikPerformansSinifi.C);

    /// Mekanın adı/kullanım tipinden Tablo 4.1 satırını ve istenen sınıf için L_Aeq
    /// sınırını bulur. Eşleşme yoksa null döner; resmî değerlendirme bunu VeriEksik yapar.
    public static IcGurultuLimiti? LimitBul(
        BinaIslevi islev, AkustikPerformansSinifi sinif, string? mekanAdi, string? kullanimTipi)
    {
        var satir = SatirBul(islev, mekanAdi, kullanimTipi);
        return satir == null
            ? null
            : new IcGurultuLimiti(satir, sinif, SinifIcinDeger(satir.CDegeriDb, sinif));
    }

    /// Yalnız satırı bulur (EK-5 Tablo 5.1'in −4 dB türetimi de aynı satırı kullanır).
    public static Tablo41Satiri? SatirBul(BinaIslevi islev, string? mekanAdi, string? kullanimTipi)
    {
        if (!_tablo.TryGetValue(islev, out var satirlar))
            return null;

        var metin = Normalize($"{kullanimTipi} {mekanAdi}");
        if (string.IsNullOrWhiteSpace(metin))
            return null;

        // En uzun (en özgül) anahtar kazanır: "OZEL DERSLIK" > "DERSLIK".
        Tablo41Satiri? enIyi = null;
        int enIyiUzunluk = 0;
        foreach (var satir in satirlar)
        {
            foreach (var anahtar in satir.Anahtarlar)
            {
                // Eşit uzunlukta daha sonra tanımlanan özgül satır kazanır. Örneğin
                // "ÇOK YATAKLI HASTA ODASI" hem HASTA ODASI hem COK YATAKLI ile
                // eşleşir; tablo sırasındaki Çok Yataklı satırı seçilmelidir.
                if (metin.Contains(anahtar, StringComparison.Ordinal) && anahtar.Length >= enIyiUzunluk)
                {
                    enIyi = satir;
                    enIyiUzunluk = anahtar.Length;
                }
            }
        }
        return enIyi;
    }

    /// Eski ön-tahmin çağrıları için kullanılan sabit hedef. Resmî uygunluk akışı bu
    /// değere düşmez; <see cref="IcGurultuHedefiBul"/> ile null/VeriEksik üretir.
    public const double VarsayilanIcHedefDb = 35.0;

    /// Resmî uygunluk akışı için güvenli nullable sürüm. Mekan Tablo 4.1'de
    /// eşleşmiyorsa uydurma 35 dBA yerine null döner ve çağıran VeriEksik gösterebilir.
    public static double? IcGurultuHedefiBul(BinaProje proje, Mekan? aliciMekan) =>
        aliciMekan == null
            ? null
            : LimitBul(proje.BinaIslevi, proje.AkustikPerformansSinifi,
                aliciMekan.Ad, aliciMekan.KullanimTipi)?.LimitDb;

    /// Geriye dönük uyumlu ön-tahmin API'si. Resmî geçti/kaldı kararı için
    /// <see cref="IcGurultuHedefiBul"/> kullanılmalıdır.
    public static double IcGurultuHedefi(BinaProje proje, Mekan? aliciMekan) =>
        IcGurultuHedefiBul(proje, aliciMekan) ?? VarsayilanIcHedefDb;

    // Türkçe karakter normalize — EK2SiniflandirmaServisi ile aynı yaklaşım.
    private static string Normalize(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var ch in s.ToUpperInvariant())
        {
            sb.Append(ch switch
            {
                'Ç' => 'C', 'Ğ' => 'G', 'İ' => 'I', 'Ö' => 'O', 'Ş' => 'S', 'Ü' => 'U',
                'I' => 'I', 'ı' => 'I',
                _ => ch
            });
        }
        return sb.ToString();
    }
}
