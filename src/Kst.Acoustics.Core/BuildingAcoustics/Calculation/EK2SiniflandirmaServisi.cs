using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.BuildingAcoustics.Calculation;

/// EK-2 tablosuna göre bina işlevi → mekan kategorisi → (KaynakDerecesi, AliciSinifi) eşlemesi.
public record MekanKategori(string Ad, string KaynakDerecesi, string AliciSinifi);

public static class EK2SiniflandirmaServisi
{
    public const string HesapDisiKategoriAdi = "Hesap Dışı";

    // ── Kategori listeleri ──────────────────────────────────────────────────

    // Dereceler Yönetmelik EK-2 Tablo 2.1 ile birebir doğrulandı (2026-06-10).
    private static readonly IReadOnlyList<MekanKategori> _konut =
    [
        new("Yatak Odaları",        "OG", "I"),
        new("Yaşam Alanları",       "OG", "II"),
        new("Mutfaklar / Banyo",    "OG", "III"),
        new("Sirkülasyon Alanları", "OG", "III"),
        new("Teknik Merkezler",     "YG", "III"),
    ];

    private static readonly IReadOnlyList<MekanKategori> _egitim =
    [
        new("Derslikler",                    "OG", "I"),
        new("Çok Amaçlı Salonlar",           "YG", "II"),   // Tablo 2.1 "Özel Derslikler"
        new("Spor Salonları",                "YG", "III"),
        new("Okuma Odaları",                 "DG", "I"),
        new("İdari / Ofis Alanları",         "OG", "II"),
        new("Sirkülasyon / Ortak Alanlar",   "OG", "III"),
        new("Teknik / Servis Alanları",      "YG", "III"),
    ];

    private static readonly IReadOnlyList<MekanKategori> _saglik =
    [
        new("Hasta / Yaşlı Odaları",         "OG", "I"),
        new("Muayene / Tedavi Odaları",      "OG", "II"),
        new("Ameliyathaneler",               "DG", "I"),
        new("Laboratuvarlar",                "DG", "II"),
        new("Bekleme / Ortak Alanlar",       "OG", "III"),
        new("İdari / Ofis Alanları",         "OG", "II"),
        new("Sirkülasyon Alanları",          "OG", "III"),
        new("Teknik / Servis Alanları",      "YG", "III"),
    ];

    private static readonly IReadOnlyList<MekanKategori> _buro =
    [
        new("Açık Ofisler",                  "OG", "II"),
        new("Bireysel Ofisler",              "OG", "I"),    // Tablo 2.1 "Özel Odalar"
        new("Toplantı Odaları",              "OG", "I"),
        new("Sirkülasyon / Bekleme",         "OG", "III"),
        new("Teknik / Servis Alanları",      "YG", "III"),
    ];

    private static readonly IReadOnlyList<MekanKategori> _konaklama =
    [
        new("Misafir Odaları",               "OG", "I"),
        new("Yeme-İçme Alanları",            "YG", "III"),
        new("Toplantı / Çok Amaçlı",        "OG", "II"),
        new("Sirkülasyon / Lobi",            "OG", "III"),
        new("Teknik / Servis Alanları",      "YG", "III"),
    ];

    private static readonly IReadOnlyList<MekanKategori> _yurt =
    [
        new("Yatak / Çalışma Odaları",       "OG", "I"),
        new("Ortak Yaşam Alanları",          "OG", "II"),
        new("Yemekhane",                     "YG", "III"),
        new("Sirkülasyon Alanları",          "OG", "III"),
        new("Teknik / Servis Alanları",      "YG", "III"),
    ];

    private static readonly IReadOnlyList<MekanKategori> _kulturel =
    [
        new("Performans / Gösteri Salonları","YG", "I"),   // tiyatro/sinema/konser/stüdyo
        new("Sergi / Çok Amaçlı Alanlar",   "OG", "II"),   // müzeler
        new("Kütüphaneler",                  "DG", "I"),
        new("İdari / Ofis Alanları",         "OG", "II"),
        new("Sirkülasyon / Fuaye",           "OG", "III"),
        new("Teknik / Servis Alanları",      "YG", "III"),
    ];

    private static readonly IReadOnlyList<MekanKategori> _ticari =
    [
        new("Satış / Ticaret Alanları",      "YG", "III"),
        new("Yeme-İçme Alanları",            "YG", "III"),
        new("İdari / Ofis Alanları",         "OG", "II"),  // postane/bankacılık dahil
        new("Sirkülasyon / Ortak Alanlar",   "OG", "III"),
        new("Teknik / Servis Alanları",      "YG", "III"),
    ];

    private static readonly IReadOnlyList<MekanKategori> _terminal =
    [
        new("Bekleme / Genel Alanlar",       "YG", "III"),
        new("İdari / Ofis Alanları",         "OG", "II"),
        new("Ticari Alanlar",                "YG", "III"),
        new("Teknik / Servis Alanları",      "YG", "III"),
    ];

    private static readonly IReadOnlyList<MekanKategori> _dini =
    [
        new("İbadet Salonları",              "YG", "II"),
        new("İdari / Ofis Alanları",         "OG", "II"),
        new("Sirkülasyon / Fuaye",           "OG", "III"),
        new("Teknik / Servis Alanları",      "YG", "III"),
    ];

    private static readonly IReadOnlyList<MekanKategori> _eglenceSporr =
    [
        new("Spor / Etkinlik Alanları",      "YG", "III"),
        new("Soyunma / Duş",                 "YG", "III"),
        new("İdari / Ofis Alanları",         "OG", "II"),
        new("Sirkülasyon / Ortak Alanlar",   "OG", "III"),
        new("Teknik / Servis Alanları",      "YG", "III"),
    ];

    private static readonly IReadOnlyList<MekanKategori> _sanayi =
    [
        new("Üretim / Atölye Alanları",      "YG", "III"),
        new("İdari / Ofis Alanları",         "OG", "II"),  // Tablo 2.1 "Personel Ofis - Dinlenme"
        new("Sirkülasyon / Ortak Alanlar",   "OG", "III"),
        new("Teknik / Servis Alanları",      "YG", "III"),
    ];

    // ── Public API ─────────────────────────────────────────────────────────

    /// Bina işlevine göre EK-2 mekan kategorileri döndürür.
    public static IReadOnlyList<MekanKategori> GetKategoriler(BinaIslevi islevi) => WithHesapDisi(islevi switch
    {
        BinaIslevi.Konut                => _konut,
        BinaIslevi.EgitimTesisleri      => _egitim,
        BinaIslevi.SaglikVeYasliEvleri  => _saglik,
        BinaIslevi.BuroIdari            => _buro,
        BinaIslevi.Konaklama            => _konaklama,
        BinaIslevi.Yurt                 => _yurt,
        BinaIslevi.KulturelTesisler     => _kulturel,
        BinaIslevi.TicariTesisler       => _ticari,
        BinaIslevi.Terminaller          => _terminal,
        BinaIslevi.DiniTesisler         => _dini,
        BinaIslevi.EglenceSporTesisleri => _eglenceSporr,
        BinaIslevi.SanayiTesisleri      => _sanayi,
        _                               => [],
    });

    /// Kategori adından doğrudan (Kaynak, Alici) döndürür.
    public static (string Kaynak, string Alici) KategoridekiDegerleri(
        BinaIslevi islevi, string kategoriAd)
    {
        var k = GetKategoriler(islevi)
                    .FirstOrDefault(x => x.Ad == kategoriAd);
        return k != null ? (k.KaynakDerecesi, k.AliciSinifi) : ("", "");
    }

    // ── Yardımcı: bina işlevi ne olursa olsun uygun kategoriyi bul ──────────

    private static MekanKategori? OrtakTeknikKategori(IReadOnlyList<MekanKategori> kategoriler) =>
        kategoriler.FirstOrDefault(k =>
            k.Ad.Contains("Teknik") || k.Ad.Contains("Servis") || k.Ad.Contains("Merkez"));

    private static MekanKategori? OrtakSirkulasyonKategori(IReadOnlyList<MekanKategori> kategoriler) =>
        kategoriler.FirstOrDefault(k =>
            k.Ad.Contains("Sirkülasyon") || k.Ad.Contains("Lobi") || k.Ad.Contains("Bekleme") || k.Ad.Contains("Fuaye"));

    private static MekanKategori? OrtakBanyoKategori(IReadOnlyList<MekanKategori> kategoriler) =>
        kategoriler.FirstOrDefault(k =>
            k.Ad.Contains("Banyo") || k.Ad.Contains("Soyunma") || k.Ad.Contains("Servis"))
        ?? OrtakTeknikKategori(kategoriler);

    private static MekanKategori? OrtakIdariKategori(IReadOnlyList<MekanKategori> kategoriler) =>
        kategoriler.FirstOrDefault(k => k.Ad.Contains("İdari") || k.Ad.Contains("Ofis"));

    /// Oda adı / kullanım etiketi gibi serbest metinlerden EK-2 mekan kategorisini tahmin eder.
    /// Önce bina işlevinden bağımsız genel filtreler uygulanır, sonra işleve özgü eşleme yapılır.
    public static MekanKategori? TahminEt(BinaIslevi islevi, string? mekanAdi, string? kullanimTipi)
    {
        var kategoriler = GetKategoriler(islevi);
        var metin = Normalize($"{kullanimTipi} {mekanAdi}");
        if (string.IsNullOrWhiteSpace(metin))
            return null;

        // 1) Birebir eşleşme
        var birebir = kategoriler.FirstOrDefault(k => Normalize(k.Ad) == metin);
        if (birebir != null) return birebir;

        // 2) İşlev-bağımsız "hesap dışı" eşleşme
        if (ContainsAny(metin,
                "BALKON", "TERAS", "ACIK ALAN", "HESAP DISI",
                "CATI TERAS", "AVLU", "OTOPARK", "GARAJ",
                "SIGINAK",          // sığınak yapısal → hesap dışı
                "DENGE TANKI",      // su mühendisliği → hesap dışı / teknik
                "YANGIN DEPOSU", "SU DEPOSU"))
            return kategoriler.FirstOrDefault(k => k.Ad == HesapDisiKategoriAdi)
                   ?? OrtakTeknikKategori(kategoriler);

        // 3) İşlev-bağımsız teknik alan
        if (ContainsAny(metin,
                "TEKNIK", "MAKINE", "TESISAT", "KAZAN", "POMPA",
                "JENERATOR", "ELEKTRIK", "PANO", "DATA", "SUNUCU",
                "DEPO ", " DEPO", "ARDIYE", "AMBAR",
                "TANK", "REZERVUAR", "SAYAC", "SAYACLAR",
                "SOGUK HAVA", "DOGALGAZ", "KOMPRESSOR", "TRAFO",
                "IKSA", "BODRUM KAT"))
            return OrtakTeknikKategori(kategoriler);

        // 3b) İşlev-bağımsız idari alanlar
        if (ContainsAny(metin,
                "ARSIV", "IDARE ", " IDARE", "IDARI",
                "YONETIM", "MUDURLUK", "SEKRETERLIK"))
            return OrtakIdariKategori(kategoriler) ?? OrtakTeknikKategori(kategoriler);

        // 3c) İşlev-bağımsız ortak/yemek alanları — işleve özgü yemek kategorisi (Tablo 2.1
        // lokanta/yemekhane YG/III) varsa o tercih edilir, yoksa sirkülasyona düşer.
        if (ContainsAny(metin, "YEMEKHANE", "KAFETERYA", "KANTIN"))
            return kategoriler.FirstOrDefault(k => k.Ad.Contains("Yemekhane") || k.Ad.Contains("Yeme-İçme"))
                   ?? OrtakSirkulasyonKategori(kategoriler);

        // 4) İşlev-bağımsız sirkülasyon
        if (metin == "HOL" || ContainsAny(metin,
                "MERDIVEN", "ASANSOR", "KORIDOR", " HOL", "HOL ",
                "LOBI", "FUAYE", "ANTRE", " GIRIS", "GIRIS ",
                "RUZGARLIK", "TAMPON", "GECIT", "GECIS"))
            return OrtakSirkulasyonKategori(kategoriler);

        // 5) İşlev-bağımsız WC/banyo/soyunma
        if (metin == "WC" || metin == "BANYO" || ContainsAny(metin,
                " WC", "WC ", "TUVALET", "LAVABO", " BANYO", "BANYO ",
                " DUS", "DUS ", "SOYUNMA", "DUSAKABIN",
                "ENGELLI WC", "PERSONEL BANYOSU"))
            return OrtakBanyoKategori(kategoriler);

        // 6) İlk yardım / sağlık odası — bina tipinden bağımsız
        if (ContainsAny(metin, "ILK YARDIM", "SAGLIK ODASI", "REVIR", "DOKTOR ODASI", "HEKIM ODASI"))
        {
            var saglik = GetKategoriler(BinaIslevi.SaglikVeYasliEvleri);
            return saglik.FirstOrDefault(k => k.Ad == "Muayene / Tedavi Odaları")
                   ?? OrtakIdariKategori(kategoriler);
        }

        // 7) İşleve özgü detaylı eşleme
        return islevi switch
        {
            BinaIslevi.Konut => Match(kategoriler, metin,
                Map("Yatak Odaları",        "YATAK", "EBEVEYN", "COCUK ODASI", "BEBEK ODASI", "GENC ODASI"),
                Map("Yaşam Alanları",       "SALON", "OTURMA", "YASAM", "YEMEK", "YEMEK ODASI", "KILER"),
                Map("Mutfaklar / Banyo",    "MUTFAK"),
                Map("Sirkülasyon Alanları", "HOL", "KORIDOR", "ANTRE", "MERDIVEN", "GIRIS"),
                Map("Teknik Merkezler",     "SAYAC", "TESISAT", "MAKINE", "BODRUM")),

            BinaIslevi.EgitimTesisleri => Match(kategoriler, metin,
                Map("Okuma Odaları",               "KUTUPHANE", "OKUMA"),
                Map("Derslikler",                  "SINIF", "DERSLIK", "DERSHANE", "ATOLYE ODASI",
                                                   "LABORATUVAR", "LAB", "BILGISAYAR"),
                Map("Çok Amaçlı Salonlar",         "KONFERANS", "TOPLANTI", "COK AMACLI", "AMFI",
                                                   "SEMINER", "SUNUM", "SALON",
                                                   "MUZIK ODASI", "RESIM ODASI", "DRAMA", "DANS"),
                Map("Spor Salonları",              "SPOR", "JIMNASTIK", "BEDEN EGITIMI", "HAVUZ", "YUZME"),
                Map("İdari / Ofis Alanları",       "OFIS", "BURO", "IDARI", "MUDUR", "MUDURLUK",
                                                   "OGRETMEN", "PERSONEL", "YONETIM", "SEKRETER", "REHBERLIK"),
                Map("Sirkülasyon / Ortak Alanlar", "BEKLEME", "KANTIN", "YEMEKHANE"),
                Map("Teknik / Servis Alanları",    "SERVIS", "TEMIZLIK", "ARSIV", "ARŞIV")),

            BinaIslevi.SaglikVeYasliEvleri => Match(kategoriler, metin,
                Map("Hasta / Yaşlı Odaları",       "HASTA", "YASLI", "YATAK", "YATAN"),
                Map("Ameliyathaneler",             "AMELIYAT", "YOGUN BAKIM", "STERIL"),
                Map("Laboratuvarlar",              "LABORATUVAR", "LABARATUVAR", "RONTGEN", "RADYOLOJI"),
                Map("Muayene / Tedavi Odaları",    "MUAYENE", "TEDAVI", "DOKTOR", "HEKIM",
                                                   "KLINIK", "POLIKLINIK",
                                                   "ECZANE", "PANSUMAN", "DIYALIZ"),
                Map("Bekleme / Ortak Alanlar",     "BEKLEME", "ZIYARETCI", "YEMEK", "KAFETERYA", "SOSYAL"),
                Map("İdari / Ofis Alanları",       "OFIS", "BURO", "IDARI", "YONETIM", "SEKRETER", "ARSIV"),
                Map("Sirkülasyon Alanları",        "ANTRE", "GIRIS"),
                Map("Teknik / Servis Alanları",    "SERVIS", "CAMASIR", "CAMASIRHANE")),

            BinaIslevi.BuroIdari => Match(kategoriler, metin,
                Map("Açık Ofisler",                "ACIK OFIS", "ACIK BURO", "CALISMA ALANI", "OPEN SPACE"),
                Map("Bireysel Ofisler",            "OFIS", "BURO", "MUDUR", "MUDURLUK", "YONETICI",
                                                   "AVUKAT", "DANISMA", "ODA"),
                Map("Toplantı Odaları",            "TOPLANTI", "KONFERANS", "GORUSME", "MULAKAT"),
                Map("Sirkülasyon / Bekleme",       "BEKLEME", "RESEPSIYON", "KARSILAMA"),
                Map("Teknik / Servis Alanları",    "SERVIS", "MUTFAK", "DINLENME",
                                                   "FOTOKOPI", "ARSIV", "ARŞIV")),

            BinaIslevi.Konaklama => Match(kategoriler, metin,
                Map("Misafir Odaları",             "MISAFIR", "KONAKLAMA", "SUIT", "STANDART ODA",
                                                   "DELUX", "SUPERIOR", "PENTHOUSE"),
                Map("Yeme-İçme Alanları",          "RESTORAN", "YEMEK", "MUTFAK", "KAFE", "KAFETERYA",
                                                   "BAR", "LOUNGE", "ALAKART", "BANKET"),
                Map("Toplantı / Çok Amaçlı",      "TOPLANTI", "KONFERANS", "SALON", "BALO"),
                Map("Sirkülasyon / Lobi",          "LOBI", "RESEPSIYON", "KARSILAMA"),
                Map("Teknik / Servis Alanları",    "SERVIS", "CAMASIR", "CAMASIRHANE", "SOGUK ODA")),

            BinaIslevi.Yurt => Match(kategoriler, metin,
                Map("Yatak / Çalışma Odaları",     "YATAK", "CALISMA", "ETUT", "ODA"),
                Map("Yemekhane",                   "YEMEKHANE", "YEMEK"),
                Map("Ortak Yaşam Alanları",        "SALON", "YASAM", "SOSYAL", "OTURMA"),
                Map("Sirkülasyon Alanları",        "HOL", "KORIDOR", "MERDIVEN"),
                Map("Teknik / Servis Alanları",    "SERVIS", "MUTFAK", "CAMASIR")),

            BinaIslevi.KulturelTesisler => Match(kategoriler, metin,
                Map("Performans / Gösteri Salonları","SAHNE", "TIYATRO", "KONSER", "OPERA", "SINEMA",
                                                   "AMFI", "GOSTERI", "PERFORMANS", "SALON"),
                Map("Kütüphaneler",                "KUTUPHANE", "OKUMA"),
                Map("Sergi / Çok Amaçlı Alanlar", "SERGI", "GALERI", "MUZE", "FUAYE", "RESIM"),
                Map("İdari / Ofis Alanları",       "OFIS", "BURO", "IDARI", "YONETIM", "MUDUR", "ARSIV"),
                Map("Sirkülasyon / Fuaye",         "HOL", "KORIDOR", "LOBI", "BEKLEME"),
                Map("Teknik / Servis Alanları",    "SERVIS", "SAHNE ARKASI", "DEPOLAR")),

            BinaIslevi.TicariTesisler => Match(kategoriler, metin,
                Map("Satış / Ticaret Alanları",    "SATIS", "MAGAZA", "MARKET", "DUKKAN",
                                                   "TICARET", "SHOWROOM", "VITRIN"),
                Map("Yeme-İçme Alanları",          "RESTORAN", "KAFE", "KAFETERYA", "YEMEK"),
                Map("İdari / Ofis Alanları",       "OFIS", "BURO", "IDARI", "YONETIM", "ARSIV"),
                Map("Sirkülasyon / Ortak Alanlar", "HOL", "KORIDOR", "LOBI", "BEKLEME", "ATRIUM"),
                Map("Teknik / Servis Alanları",    "SERVIS", "SOGUK DEPO", "MUTFAK")),

            BinaIslevi.Terminaller => Match(kategoriler, metin,
                Map("Bekleme / Genel Alanlar",     "BEKLEME", "PERON", "TERMINAL", "YOLCU", "GIDIS", "GELIS"),
                Map("İdari / Ofis Alanları",       "OFIS", "BURO", "IDARI", "KONTROL", "GUVENLIK"),
                Map("Ticari Alanlar",              "KAFE", "RESTORAN", "MAGAZA", "MARKET"),
                Map("Teknik / Servis Alanları",    "SERVIS", "MUTFAK")),

            BinaIslevi.DiniTesisler => Match(kategoriler, metin,
                Map("İbadet Salonları",            "IBADET", "CAMI", "NAMAZ", "MESCIT", "MINARE",
                                                   "MIHRAP", "CEMAAT", "KILISE", "HAVRA"),
                Map("İdari / Ofis Alanları",       "OFIS", "IMAM", "MUEZZIN", "IDARI", "ARSIV"),
                Map("Sirkülasyon / Fuaye",         "HOL", "FUAYE", "GIRIS", "BEKLEME"),
                Map("Teknik / Servis Alanları",    "SERVIS", "ABDEST")),

            BinaIslevi.EglenceSporTesisleri => Match(kategoriler, metin,
                Map("Spor / Etkinlik Alanları",    "SPOR", "ETKINLIK", "FITNESS", "YUZME", "HAVUZ",
                                                   "OLIMPIK", "YARI OLIMPIK", "SAHA", "KORT", "RING",
                                                   "JIMNASTIK", "PILATES", "YOGA", "BOKS",
                                                   "TENIS", "SQUASH", "DOVUS", "KARATE",
                                                   "BOWLING", "BILARDO", "DANS", "SALON"),
                Map("Soyunma / Duş",               "SOYUNMA", "HAKEM", "ANTRENOR", "SPORCU"),
                Map("İdari / Ofis Alanları",       "OFIS", "BURO", "IDARI", "MUDUR", "ARSIV", "KUTUPHANE", "OKUMA"),
                Map("Sirkülasyon / Ortak Alanlar", "BEKLEME", "KANTIN", "KAFE", "KAFETERYA", "YEMEK"),
                Map("Teknik / Servis Alanları",    "SERVIS", "MUTFAK", "TEMIZLIK", "POMPA")),

            BinaIslevi.SanayiTesisleri => Match(kategoriler, metin,
                Map("Üretim / Atölye Alanları",    "URETIM", "ATOLYE", "IMALAT", "FABRIKA",
                                                   "MONTAJ", "PAKETLEME", "DEPOLAMA"),
                Map("İdari / Ofis Alanları",       "OFIS", "BURO", "IDARI", "YONETIM", "KALITE",
                                                   "LABORATUVAR", "TEST", "ARSIV"),
                Map("Sirkülasyon / Ortak Alanlar", "HOL", "KORIDOR", "YEMEKHANE", "DINLENME"),
                Map("Teknik / Servis Alanları",    "KOMPRESSOR", "TRAFO")),

            _ => Match(kategoriler, metin,
                Map("İdari / Ofis Alanları",       "OFIS", "BURO", "IDARI", "YONETIM"),
                Map("Satış / Ticaret Alanları",    "SATIS", "MAGAZA", "TICARET"),
                Map("Üretim / Atölye Alanları",    "URETIM", "ATOLYE", "IMALAT"),
                Map("Spor / Etkinlik Alanları",    "SPOR", "HAVUZ", "SALON"),
                Map("İbadet Salonları",            "IBADET", "CAMI", "MESCIT"),
                Map("Bekleme / Genel Alanlar",     "BEKLEME", "GENEL")),
        };
    }

    public static bool HesapDisiMi(string? kategoriAd) =>
        string.Equals(kategoriAd?.Trim(), HesapDisiKategoriAdi, System.StringComparison.OrdinalIgnoreCase);

    private static MekanKategori? Match(
        IReadOnlyList<MekanKategori> kategoriler,
        string metin,
        params (string Kategori, string[] Anahtarlar)[] eslemeler)
    {
        foreach (var (kategori, anahtarlar) in eslemeler)
            if (ContainsAny(metin, anahtarlar))
                return kategoriler.FirstOrDefault(k => k.Ad == kategori);

        return null;
    }

    private static (string Kategori, string[] Anahtarlar) Map(string kategori, params string[] anahtarlar) =>
        (kategori, anahtarlar);

    private static bool ContainsAny(string metin, params string[] anahtarlar) =>
        anahtarlar.Any(metin.Contains);

    private static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        return text
            .Trim()
            .ToUpperInvariant()
            // Türkçe karakterler
            .Replace('ı', 'I')
            .Replace('İ', 'I')
            .Replace('Ş', 'S')
            .Replace('Ğ', 'G')
            .Replace('Ü', 'U')
            .Replace('Ö', 'O')
            .Replace('Ç', 'C')
            // Noktalama / özel karakterleri boşluğa çevir
            .Replace('.', ' ')
            .Replace(',', ' ')
            .Replace('-', ' ')
            .Replace('_', ' ')
            .Replace('/', ' ')
            // Çok boşluğu tek boşluğa indir
            .Replace("  ", " ").Replace("  ", " ").Trim();
    }

    private static IReadOnlyList<MekanKategori> WithHesapDisi(IReadOnlyList<MekanKategori> kategoriler)
    {
        var list = kategoriler.ToList();
        if (list.All(k => k.Ad != HesapDisiKategoriAdi))
            list.Add(new MekanKategori(HesapDisiKategoriAdi, "", ""));
        return list;
    }
}
