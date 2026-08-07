using System.Linq;

namespace Kst.Acoustics.Core.BuildingAcoustics.Models;

/// EK-2 kapsamındaki bina işlev türleri (12 ana kategori).
public enum BinaIslevi
{
    Tanimsiz              = 0,
    Konut                 = 1,   // Apartman, villa, müstakil ev
    EgitimTesisleri       = 2,   // Okul, üniversite, kreş
    SaglikVeYasliEvleri   = 3,   // Hastane, klinik, yaşlı bakım evi
    BuroIdari             = 4,   // Büro, idari bina, ticari büro
    Konaklama             = 5,   // Otel, motel, apart, pansiyon
    Yurt                  = 6,   // Öğrenci yurdu, misafirhane
    KulturelTesisler      = 7,   // Müze, tiyatro, sinema, kütüphane, konser salonu
    TicariTesisler        = 8,   // Mağaza, AVM, restoran, kafe
    Terminaller           = 9,   // Havalimanı, otogar, tren garı
    DiniTesisler          = 10,  // Cami, kilise, havra, ibadethane
    EglenceSporTesisleri  = 11,  // Spor salonu, yüzme havuzu, eğlence merkezi
    SanayiTesisleri       = 12,  // Fabrika, atölye, üretim tesisi
}

public record BinaIsleviInfo(BinaIslevi Islevi, string Ad, string Aciklama);

public static class BinaIsleviHelper
{
    private static readonly BinaIsleviInfo[] _bilgiler =
    [
        new(BinaIslevi.Tanimsiz,             "—",           "Tanımlanmamış / Seçilmedi"),
        new(BinaIslevi.Konut,                "Konutlar",    "Apartman, villa, müstakil ev"),
        new(BinaIslevi.EgitimTesisleri,      "Eğitim",      "Okul, üniversite, kreş, yüksekokul"),
        new(BinaIslevi.SaglikVeYasliEvleri,  "Sağlık/Bakım","Hastane, klinik, yaşlı bakım evi"),
        new(BinaIslevi.BuroIdari,            "Büro/İdari",  "Ofis, idari bina, ticari büro"),
        new(BinaIslevi.Konaklama,            "Konaklama",   "Otel, motel, apart otel, pansiyon"),
        new(BinaIslevi.Yurt,                 "Yurt",        "Öğrenci yurdu, misafirhane"),
        new(BinaIslevi.KulturelTesisler,     "Kültürel",    "Müze, tiyatro, sinema, kütüphane"),
        new(BinaIslevi.TicariTesisler,       "Ticari",      "Mağaza, AVM, restoran, kafe"),
        new(BinaIslevi.Terminaller,          "Terminal",    "Havalimanı, otogar, tren garı"),
        new(BinaIslevi.DiniTesisler,         "Dini",        "Cami, kilise, havra, ibadethane"),
        new(BinaIslevi.EglenceSporTesisleri, "Eğlence/Spor","Spor salonu, havuz, eğlence merkezi"),
        new(BinaIslevi.SanayiTesisleri,      "Sanayi",      "Fabrika, atölye, üretim tesisi"),
    ];

    public static IReadOnlyList<BinaIsleviInfo> Tumü => _bilgiler;

    public static BinaIsleviInfo BilgiAl(BinaIslevi islevi) =>
        _bilgiler.FirstOrDefault(b => b.Islevi == islevi) ?? _bilgiler[0];

    public static BinaIsleviInfo Varsayilan => _bilgiler[1]; // Konutlar varsayılan
}

/// <summary>
/// Yönetmelik Madde 5 kullanım sınıfları (Binaların Gürültüye Karşı Korunması, RG 31.05.2017/30082)
/// S1–S5 bu projenin iç kodu; yönetmelik Ek tablolarındaki sınıf sıralamasına karşılık gelir.
/// </summary>
public enum BinaSinifi
{
    S1, // Konut (apartman, villa)
    S2, // Otel, yurt, pansiyon
    S3, // Ofis, idari bina
    S4, // Okul, hastane, ibadet yeri
    S5  // Sanayi + komşu bina
}

public enum AkustikPerformansSinifi
{
    A,
    B,
    C,
    D,
    E,
    F
}

public record AkustikPerformansSinifiInfo(
    AkustikPerformansSinifi Sinif,
    string Kod,
    string Aciklama
);

public static class AkustikPerformansSinifiHelper
{
    private static readonly AkustikPerformansSinifiInfo[] _bilgiler =
    [
        new(AkustikPerformansSinifi.A, "A", "En yüksek hedef"),
        new(AkustikPerformansSinifi.B, "B", "Yüksek hedef"),
        new(AkustikPerformansSinifi.C, "C", "Yönetmelik tablosu orta hedef"),
        new(AkustikPerformansSinifi.D, "D", "Orta-alt hedef"),
        new(AkustikPerformansSinifi.E, "E", "Düşük hedef"),
        new(AkustikPerformansSinifi.F, "F", "En düşük hedef"),
    ];

    public static IReadOnlyList<AkustikPerformansSinifiInfo> Tumü => _bilgiler;

    public static AkustikPerformansSinifiInfo BilgiAl(AkustikPerformansSinifi sinif) =>
        _bilgiler.First(b => b.Sinif == sinif);
}

/// <summary>
/// Alıcı mekanın kullanım tipi — yönetmelik limitlerini etkiler.
/// Sessiz kullanım (yatak odası, hastane odası) daha sıkı limit gerektirir.
/// </summary>
public enum MekanKullanimiTipi
{
    YatakOdasi,     // En sıkı limit: gece saati gürültüsüne duyarlı
    OturmaOdasi,    // Standart konut oturma alanı
    Mutfak,         // Konut servis alanı
    Banyo,          // Konut servis alanı
    HolKoridoru,    // Geçiş alanı
    Ofis,           // Çalışma odası
    AcikBuro,       // Açık ofis, daha az hassas
    Sinif,          // Eğitim mekânı
    HastaneOdasi,   // Sağlık; en sıkı sınıflardan biri
    GenelAlan,      // Varsayılan / tanımlanmamış
    HesapDisi,      // Balkon, açık teras vb. hesap dışı
}

public static class MekanKullanimiHelper
{
    /// Mekan kullanim tipini string'den parse eder (DXF etiketleri için)
    public static MekanKullanimiTipi Parse(string kullanimStr)
    {
        var s = (kullanimStr ?? "").ToUpperInvariant();
        return s switch
        {
            var v when v.Contains("HESAP DIŞI") => MekanKullanimiTipi.HesapDisi,
            var v when v.Contains("HESAP DISI") => MekanKullanimiTipi.HesapDisi,
            var v when v.Contains("BALKON")     => MekanKullanimiTipi.HesapDisi,
            var v when v.Contains("TERAS")      => MekanKullanimiTipi.HesapDisi,
            var v when v.Contains("YATAK")     => MekanKullanimiTipi.YatakOdasi,
            var v when v.Contains("OTURMA")    => MekanKullanimiTipi.OturmaOdasi,
            var v when v.Contains("SALON")     => MekanKullanimiTipi.OturmaOdasi,
            var v when v.Contains("MUTFAK")    => MekanKullanimiTipi.Mutfak,
            var v when v.Contains("BANYO")     => MekanKullanimiTipi.Banyo,
            var v when v.Contains("WC")        => MekanKullanimiTipi.Banyo,
            var v when v.Contains("HOL")       => MekanKullanimiTipi.HolKoridoru,
            var v when v.Contains("KORİDOR")   => MekanKullanimiTipi.HolKoridoru,
            var v when v.Contains("OFİS")      => MekanKullanimiTipi.Ofis,
            var v when v.Contains("BÜRO")      => MekanKullanimiTipi.Ofis,
            var v when v.Contains("SINIF")     => MekanKullanimiTipi.Sinif,
            var v when v.Contains("DERSLIK")   => MekanKullanimiTipi.Sinif,
            var v when v.Contains("HASTA")     => MekanKullanimiTipi.HastaneOdasi,
            _                                  => MekanKullanimiTipi.GenelAlan,
        };
    }

    public static bool SessizKullanim(MekanKullanimiTipi tip) =>
        tip is MekanKullanimiTipi.YatakOdasi or MekanKullanimiTipi.HastaneOdasi;

    /// UX — Tip → kullanıcı dostu Türkçe etiket. Sonuç tablosunda ve mekan listesinde gösterilir.
    public static string Etiket(MekanKullanimiTipi tip) => tip switch
    {
        MekanKullanimiTipi.YatakOdasi   => "Yatak Odası",
        MekanKullanimiTipi.HastaneOdasi => "Hastane Odası",
        MekanKullanimiTipi.OturmaOdasi  => "Oturma Odası",
        MekanKullanimiTipi.Mutfak       => "Mutfak",
        MekanKullanimiTipi.Banyo        => "Banyo / WC",
        MekanKullanimiTipi.HolKoridoru  => "Hol / Koridor",
        MekanKullanimiTipi.Ofis         => "Ofis",
        MekanKullanimiTipi.AcikBuro     => "Açık Büro",
        MekanKullanimiTipi.Sinif        => "Derslik",
        MekanKullanimiTipi.HesapDisi    => "Hesap Dışı",
        _                               => "Genel Alan",
    };

    /// UX — Mekan adından (DXF etiketi vb.) en uygun etiketi üretir.
    /// Parse genel alan dönüyorsa boş döner (kullanıcının manuel girmesi için).
    public static string? OneriEtiket(string mekanAdi)
    {
        var tip = Parse(mekanAdi);
        return tip == MekanKullanimiTipi.GenelAlan ? null : Etiket(tip);
    }
}

public record BinaSinifiInfo(
    BinaSinifi Sinif,
    string KodGosterim,
    string Aciklama,
    string YonetmelikEk
);

public static class BinaSinifiHelper
{
    private static readonly BinaSinifiInfo[] _bilgiler =
    [
        new(BinaSinifi.S1, "S1", "Konut — apartman, villa, müstakil ev",          "Ek-2 Tablo 1 / Ek-3 Tablo 1"),
        new(BinaSinifi.S2, "S2", "Konaklama — otel, yurt, pansiyon",              "Ek-2 Tablo 2 / Ek-3 Tablo 2"),
        new(BinaSinifi.S3, "S3", "Ofis / İdari bina — büro, ticaret",             "Ek-2 Tablo 3 / Ek-3 Tablo 3"),
        new(BinaSinifi.S4, "S4", "Eğitim & Sağlık — okul, hastane, ibadet yeri", "Ek-2 Tablo 4 / Ek-3 Tablo 4"),
        new(BinaSinifi.S5, "S5", "Sanayi & komşu bina",                           "Ek-2 Tablo 5 / Ek-3 Tablo 5"),
    ];

    public static IReadOnlyList<BinaSinifiInfo> Tumü => _bilgiler;

    public static BinaSinifiInfo BilgiAl(BinaSinifi sinif) =>
        _bilgiler.First(b => b.Sinif == sinif);

    public static string UiMetni(BinaSinifi sinif)
    {
        var b = BilgiAl(sinif);
        return $"{b.KodGosterim} — {b.Aciklama}";
    }
}

public enum BolmeTipi
{
    OrtakDuvar,
    Doseme,
    DisCephe
}

public class AkustikLimit
{
    public BinaSinifi BinaSinifi      { get; set; }
    public BolmeTipi  BolmeTipi       { get; set; }
    public double     HedefDeger       { get; set; }
    public string     Birim            { get; set; } = "";
    public string     Aciklama         { get; set; } = "";
}

public static class YonetmelikLimitleri
{
    // Tüm sınır değerler Yönetmelik (RG 31.05.2017/30082) EK-3 tablolarıyla birebir
    // doğrulanmıştır (2026-06-10). Eski R'w tabanlı sabit tablo kaldırıldı; tüm yollar
    // Tablo 3.1 / 3.2 / 3.3 / 3.4 / 3.5 üzerinden akar.

    private static AkustikLimit Limit(double hedef, string birim, string aciklama) =>
        new() { HedefDeger = hedef, Birim = birim, Aciklama = aciklama };

    /// <summary>
    /// EK-3 Tablo 3.3 — döşemeler için en yüksek darbe sesi sınırı (L'nT,w, dB).
    /// Limit KAYNAK odasının (üst kat) gürültülülük derecesine ve performans sınıfına bağlıdır.
    /// </summary>
    public static AkustikLimit GetTablo33Limit(AkustikPerformansSinifi performansSinifi, string kaynakDerecesi)
    {
        var kaynak = NormalizeKaynak(kaynakDerecesi);
        var values = kaynak switch
        {
            "YG" => new[] { 40, 44, 48, 52, 56, 60 },
            "OG" => new[] { 46, 50, 54, 58, 62, 66 },
            "DG" => new[] { 50, 54, 58, 62, 66, 70 },
            _ => null
        };

        if (values == null)
            return Limit(0, "dB L'nT,w", "Tablo 3.3 için kaynak odası gürültülülük derecesi eksik");

        return Limit(values[(int)performansSinifi], "dB L'nT,w",
            $"Tablo 3.3: kaynak {kaynak}, sınıf {performansSinifi}");
    }

    /// <summary>
    /// EK-3 Tablo 3.1 — dış yapı elemanları için en düşük hava doğuşlu ses yalıtımı (DnT,A,tr, dB).
    /// Hedef = Lgag − ofset(hassasiyet, sınıf); A–D sınıflarında ek olarak 30 dB taban kriteri.
    /// Lgag: binanın en az 2 m uzağında, cephe yansımaları HARİÇ ölçülen düzey (dBA).
    /// Lgag girilmemişse yalnız taban kriteri döner (A–D: 30 dB; E–F: limit hesaplanamaz).
    /// </summary>
    public static AkustikLimit GetTablo31Limit(AkustikPerformansSinifi performansSinifi, string aliciSinifi, double lgag)
    {
        var alici = NormalizeAlici(aliciSinifi);
        var ofset = alici switch
        {
            "I"   => new[] { 14, 18, 22, 26, 30, 34 },
            "II"  => new[] { 17, 21, 25, 29, 33, 37 },
            "III" => new[] { 20, 24, 28, 32, 36, 40 },
            _ => null
        };

        if (ofset == null)
            return Limit(0, "dB DnT,A,tr", "Tablo 3.1 için alıcı odası hassasiyet derecesi eksik");

        double taban = performansSinifi <= AkustikPerformansSinifi.D ? 30 : 0;
        if (lgag <= 0)
            return Limit(taban, "dB DnT,A,tr",
                taban > 0
                    ? $"Tablo 3.1: Lgag girilmedi; yalnız 30 dB taban kriteri (sınıf {performansSinifi})"
                    : "Tablo 3.1 için dış gürültü düzeyi (Lgag) eksik");

        double hedef = Math.Max(lgag - ofset[(int)performansSinifi], taban);
        return Limit(hedef, "dB DnT,A,tr",
            $"Tablo 3.1: Lgag {lgag:F0} − {ofset[(int)performansSinifi]} dB (alıcı {alici}, sınıf {performansSinifi})" +
            (taban > 0 && hedef <= taban ? "; 30 dB taban kriteri belirleyici" : ""));
    }

    /// <summary>
    /// EK-3 Tablo 3.4 (KONUT) — aynı bağımsız birim içindeki oda bölmeleri (DnT,A, dB):
    /// yatak odası ↔ yaşam alanı/mutfak/banyo satırı.
    /// </summary>
    public static AkustikLimit GetTablo34AyniBirimLimit(AkustikPerformansSinifi performansSinifi)
    {
        var values = new[] { 54, 50, 44, 40, 36, 32 };
        return Limit(values[(int)performansSinifi], "dB DnT,A",
            $"Tablo 3.4: konut, aynı bağımsız birim içi bölme, sınıf {performansSinifi}");
    }

    /// <summary>
    /// EK-3 Tablo 3.5 (KONUT) — aynı bağımsız birime ait mekanlar arası döşemeler (L'nT,w, dB).
    /// </summary>
    public static AkustikLimit GetTablo35AyniBirimLimit(AkustikPerformansSinifi performansSinifi)
    {
        var values = new[] { 54, 58, 62, 66, 70, 74 };
        return Limit(values[(int)performansSinifi], "dB L'nT,w",
            $"Tablo 3.5: konut, aynı bağımsız birim içi döşeme, sınıf {performansSinifi}");
    }

    public static AkustikLimit GetTablo32Limit(
        AkustikPerformansSinifi performansSinifi,
        string kaynakDerecesi,
        string aliciSinifi)
    {
        var kaynak = NormalizeKaynak(kaynakDerecesi);
        var alici = NormalizeAlici(aliciSinifi);
        var index = (int)performansSinifi;

        var values = (kaynak, alici) switch
        {
            ("YG", "I")   => new[] { 68, 64, 58, 54, 50, 46 },
            ("YG", "II")  => new[] { 65, 61, 55, 51, 47, 43 },
            ("YG", "III") => new[] { 62, 58, 52, 48, 44, 40 },
            ("OG", "I")   => new[] { 62, 58, 52, 48, 44, 40 },
            ("OG", "II")  => new[] { 59, 55, 49, 45, 41, 37 },
            ("OG", "III") => new[] { 56, 52, 46, 42, 38, 34 },
            ("DG", "I")   => new[] { 56, 52, 46, 42, 38, 34 },
            ("DG", "II")  => new[] { 53, 49, 43, 39, 35, 31 },
            ("DG", "III") => new[] { 50, 46, 40, 36, 32, 28 },
            _ => null
        };

        if (values == null)
            return new AkustikLimit
            {
                HedefDeger = 0,
                Birim = "dB",
                Aciklama = "Tablo 3.2 için kaynak derecesi veya alıcı hassasiyeti eksik"
            };

        return new AkustikLimit
        {
            HedefDeger = values[index],
            Birim = "dB DnT,A",
            Aciklama = $"Tablo 3.2: {kaynak}-{alici}, sınıf {performansSinifi}"
        };
    }

    private static string NormalizeKaynak(string value)
    {
        var s = (value ?? "").Trim().ToUpperInvariant();
        if (s.Contains("YG") || s.Contains("YÜKSEK") || s.Contains("YUKSEK")) return "YG";
        if (s.Contains("DG") || s.Contains("DÜŞÜK") || s.Contains("DUSUK")) return "DG";
        if (s.Contains("OG") || s.Contains("ORTA")) return "OG";
        return "";
    }

    private static string NormalizeAlici(string value)
    {
        var s = (value ?? "").Trim().ToUpperInvariant();
        if (s.Contains("III")) return "III";
        if (s.Contains("II")) return "II";
        if (s.Contains("I")) return "I";
        return "";
    }
}
