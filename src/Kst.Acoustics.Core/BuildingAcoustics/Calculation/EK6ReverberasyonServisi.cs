using System;
using System.Collections.Generic;
using Kst.Acoustics.Core.BuildingAcoustics.Models;

namespace Kst.Acoustics.Core.BuildingAcoustics.Calculation;

/// <summary>
/// Yönetmelik EK-6 Tablo 6.1 — akustik performans sınıfına bağlı izin verilen en yüksek
/// reverberasyon süreleri (250-2000 Hz aritmetik ortalaması, s).
///
/// Dipnot 2: verilen sınır değerler yalnız C ve D sınıfları için geçerlidir; diğer
/// sınıflarda akustik uzman karar verir — bu servis A/B/E/F için de tabloyu döndürür ama
/// `UzmanKararigerekir=true` bayrağıyla (raporda not düşülür).
/// Dipnot 5 ("-" satırları): tiyatro/konser/ibadet/spor/sanayi genel alanları gibi özel
/// akustik tasarım hacimleri — limit null döner ("uzman belirler").
///
/// RT tahmini: Sabine T = 0.161·V/A; A = Mekan.EtkiliEmilimAlani (kullanıcı emilim alanı
/// girmediyse 0.32·V varsayımı → T ≈ 0.5 s). Bu kaba bir ön tahmindir; ayrıntılı iç
/// hacim akustiği M01 modülünün işidir.
/// </summary>
public static class EK6ReverberasyonServisi
{
    public sealed record RtSatiri(string Ad, double? TMaxSn, string[] Anahtarlar);

    private static readonly IReadOnlyDictionary<BinaIslevi, RtSatiri[]> _tablo = new Dictionary<BinaIslevi, RtSatiri[]>
    {
        [BinaIslevi.Konut] =
        [
            new("Yatak Odaları", 0.5, ["YATAK", "EBEVEYN", "COCUK ODASI"]),
            new("Yaşam Alanları, Mutfak", 0.8, ["YASAM", "SALON", "OTURMA", "MUTFAK"]),
            new("Sirkülasyon Alanları", 1.2, ["SIRKULASYON", "KORIDOR", "HOL", "MERDIVEN", "ANTRE", "GIRIS"]),
        ],
        [BinaIslevi.EgitimTesisleri] =
        [
            new("Derslikler / Özel Derslik / İdari / Okuma", 0.8, ["DERSLIK", "SINIF", "OZEL DERSLIK", "IDARI", "OKUMA", "OGRETMENLER", "OFIS", "ETUT"]),
            new("Spor Salonu", 1.8, ["SPOR"]),
            new("Kreş Oyun-Yemek Alanları", 0.8, ["OYUN", "YEMEK ALANI", "KRES"]),
            new("Kreş Yatak Odaları", 0.5, ["YATAK", "UYKU"]),
            new("Sirkülasyon Alanları", 1.2, ["SIRKULASYON", "KORIDOR", "HOL", "MERDIVEN", "GIRIS"]),
        ],
        [BinaIslevi.SaglikVeYasliEvleri] =
        [
            new("Özel Hasta Odaları", 0.5, ["OZEL HASTA", "TEK KISILIK", "HASTA ODASI", "YASLI ODASI"]),
            new("Muayene / Ameliyathane / Laboratuvar", 0.8, ["MUAYENE", "TEDAVI", "AMELIYAT", "LABORATUVAR", "LAB", "POLIKLINIK"]),
            new("Çok Yataklı Odalar", 1.0, ["COK YATAKLI", "KOGUS"]),
            new("Sirkülasyon Alanları", 1.2, ["SIRKULASYON", "KORIDOR", "HOL", "BEKLEME", "MERDIVEN", "GIRIS"]),
        ],
        [BinaIslevi.BuroIdari] =
        [
            new("Açık Planlı Alanlar", 1.0, ["ACIK PLAN", "ACIK OFIS"]),
            new("Toplantı-Yönetici Odaları, Dinlenme", 0.8, ["TOPLANTI", "YONETICI", "MUDUR", "DINLENME", "OZEL ODA", "OFIS"]),
            new("Telekonferans Odaları", 0.4, ["TELEKONFERANS", "VIDEO KONFERANS"]),
            new("Mahkeme Salonları", 1.2, ["MAHKEME", "DURUSMA"]),
            new("Sirkülasyon Alanları", 1.2, ["SIRKULASYON", "KORIDOR", "HOL", "BEKLEME", "MERDIVEN", "GIRIS"]),
        ],
        [BinaIslevi.Konaklama] =
        [
            new("Yatak Odaları", 0.5, ["YATAK", "MISAFIR ODA", "ODA", "SUIT"]),
            new("Lokantalar / Hizmet Destek", 1.0, ["LOKANTA", "RESTORAN", "YEMEK", "HIZMET", "DESTEK", "SERVIS"]),
            new("Sirkülasyon Alanları", 1.2, ["SIRKULASYON", "KORIDOR", "LOBI", "HOL", "MERDIVEN", "GIRIS"]),
        ],
        [BinaIslevi.Yurt] =
        [
            new("Yatakhane", 0.5, ["YATAKHANE", "YATAK", "UYKU"]),
            new("Etüd Odası", 0.8, ["ETUT", "ETUD", "CALISMA"]),
            new("Sirkülasyon / Yemekhane", 1.2, ["SIRKULASYON", "KORIDOR", "HOL", "YEMEKHANE", "YEMEK", "GIRIS"]),
        ],
        [BinaIslevi.KulturelTesisler] =
        [
            new("Tiyatro / Sinema / Konser / Stüdyo (özel tasarım)", null, ["TIYATRO", "SINEMA", "KONSER", "STUDYO", "ODITORYUM", "KONFERANS"]),
            new("Müzeler", 1.2, ["MUZE", "SERGI", "GALERI"]),
            new("Kütüphaneler", 0.8, ["KUTUPHANE", "OKUMA"]),
            new("Sirkülasyon Alanları", 1.2, ["SIRKULASYON", "KORIDOR", "FUAYE", "HOL", "GIRIS"]),
        ],
        [BinaIslevi.TicariTesisler] =
        [
            new("Mağaza-Dükkan", 1.0, ["MAGAZA", "DUKKAN", "SATIS"]),
            new("Alışveriş Merkezi - Marketler", 2.0, ["ALISVERIS", "AVM", "MARKET", "SUPERMARKET", "ATRIUM"]),
            new("Postane, Genel Bankacılık", 1.2, ["POSTANE", "BANKA", "GISELER"]),
            new("Sirkülasyon Alanları", 1.2, ["SIRKULASYON", "KORIDOR", "HOL", "GIRIS"]),
        ],
        [BinaIslevi.Terminaller] =
        [
            new("Bekleme Alanları", 1.0, ["BEKLEME", "TERMINAL", "PERON", "GATE"]),
            new("Personel Ofis-Dinlenme Odaları", 0.8, ["PERSONEL", "OFIS", "DINLENME"]),
        ],
        [BinaIslevi.DiniTesisler] =
        [
            new("İbadet Alanları (özel tasarım)", null, ["IBADET", "CAMI", "MESCIT", "KILISE", "HAVRA"]),
        ],
        [BinaIslevi.EglenceSporTesisleri] =
        [
            new("Spor Tesisleri (özel tasarım)", null, ["SPOR", "YUZME", "HAVUZ"]),
            new("Lokantalar / Yemek / Eğlence", 1.0, ["LOKANTA", "RESTORAN", "YEMEK", "EGLENCE", "BAR", "KAFE"]),
        ],
        [BinaIslevi.SanayiTesisleri] =
        [
            new("Genel Alanlar (özel tasarım)", null, ["URETIM", "ATOLYE", "FABRIKA", "IMALAT"]),
            new("Personel Ofis-Dinlenme Odaları", 0.8, ["PERSONEL", "OFIS", "DINLENME"]),
            new("Sirkülasyon Alanları", 1.2, ["SIRKULASYON", "KORIDOR", "HOL", "GIRIS"]),
        ],
    };

    public sealed record RtSonucu(
        string SatirAdi,
        double? TMaxSn,
        double TahminiTSn,
        UyumDurumu Durum,
        bool UzmanKarariGerekir,
        bool OzelTasarim)
    {
        public double? MarjSn => TMaxSn is { } t ? t - TahminiTSn : null; // pozitif = uygun
    }

    /// Sabine ön tahmini: T = 0.161·V/A (A = etkili emilim alanı).
    public static double SabineRt(Mekan mekan) =>
        mekan.Hacim <= 0 || mekan.EtkiliEmilimAlani <= 0
            ? 0
            : 0.161 * mekan.Hacim / mekan.EtkiliEmilimAlani;

    /// Mekan için Tablo 6.1 sınırı + Sabine tahmini uyumu. Satır eşleşmezse null
    /// (mekan işlev tablosunda RT kriteri tanımlı değil).
    public static RtSonucu? Degerlendir(BinaProje proje, Mekan mekan)
    {
        if (!_tablo.TryGetValue(proje.BinaIslevi, out var satirlar))
            return null;

        var metin = Normalize($"{mekan.KullanimTipi} {mekan.Ad}");
        if (string.IsNullOrWhiteSpace(metin))
            return null;

        RtSatiri? enIyi = null;
        int enIyiUzunluk = 0;
        foreach (var satir in satirlar)
            foreach (var anahtar in satir.Anahtarlar)
                if (metin.Contains(anahtar, StringComparison.Ordinal) && anahtar.Length >= enIyiUzunluk)
                {
                    enIyi = satir;
                    enIyiUzunluk = anahtar.Length;
                }

        if (enIyi == null)
            return null;

        var tahmin = SabineRt(mekan);

        // Dipnot 5: özel akustik tasarım hacmi — sayısal limit yok, uzman belirler.
        if (enIyi.TMaxSn is not { } tMax)
            return new RtSonucu(enIyi.Ad, null, tahmin, UyumDurumu.VeriEksik,
                UzmanKarariGerekir: true, OzelTasarim: true);

        if (tahmin <= 0)
            return new RtSonucu(enIyi.Ad, tMax, 0, UyumDurumu.VeriEksik,
                UzmanKarariGerekir: false, OzelTasarim: false);

        // 0.32·V yalnız kaba ön-tahmin içindir. Kullanıcı/proje tarafından emilim
        // alanı beyan edilmemişken bu varsayımdan resmî GEÇTİ/KALDI sonucu üretme.
        if (mekan.EmilimAlani <= 0)
            return new RtSonucu(enIyi.Ad, tMax, tahmin, UyumDurumu.VeriEksik,
                UzmanKarariGerekir: true, OzelTasarim: false);

        // Dipnot 2: tablo değerleri C-D sınıfları için; diğer sınıflarda uzman kararı.
        bool uzman = proje.AkustikPerformansSinifi is not
            (AkustikPerformansSinifi.C or AkustikPerformansSinifi.D);

        return new RtSonucu(enIyi.Ad, tMax, tahmin,
            tahmin <= tMax ? UyumDurumu.Gecti : UyumDurumu.Kaldi,
            UzmanKarariGerekir: uzman, OzelTasarim: false);
    }

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
