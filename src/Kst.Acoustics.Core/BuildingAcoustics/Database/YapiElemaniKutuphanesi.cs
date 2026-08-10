using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kst.Acoustics.Core.BuildingAcoustics.Calculation;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.BuildingAcoustics.Database;

public class YapiElemaniKutuphanesi
{
    private static List<YapiElemani>? _cache;
    private static KutuphaneRapor? _sonRapor;

    private static readonly string _tohumDbPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "BuildingAcoustics", "Database", "buildingelements.json");

    private static readonly string _kullaniciDbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KST", "EYE4Pro", "BuildingAcoustics", "buildingelements.user.json");

    // Bazı ara sürümler kullanıcı kataloğunu aynı uygulama-verisi klasöründe eski adla
    // oluşturdu. Yeni dosya yoksa bu dosya bir kez okunup yeni şemaya taşınır.
    private static readonly string _eskiKullaniciDbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KST", "EYE4Pro", "BuildingAcoustics", "buildingelements.json");

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Paketle gelen, salt-okunur kabul edilen başlangıç kataloğu.</summary>
    public static string TohumKatalogYolu => _tohumDbPath;

    /// <summary>Kullanıcı ekleme/değişiklik/silmelerinin yazıldığı uygulama-verisi dosyası.</summary>
    public static string KullaniciKatalogYolu => _kullaniciDbPath;

    public static List<YapiElemani> TumElemanlar => _cache ??= Yukle();

    /// B14 — Son JSON yüklemesinin doğrulama raporu (UI bilgilendirmesi için).
    /// İlk Yukle çağrısından önce null.
    public static KutuphaneRapor? SonYuklemeRaporu => _sonRapor;

    /// B14 — JSON kataloğu yükle + her elemanı detaylı doğrula.
    /// Hatalı elemanlar atılır, uyarılar SonYuklemeRaporu'na yazılır.
    public static (List<YapiElemani> Elemanlar, KutuphaneRapor Rapor) YukleVeDogrula() =>
        YukleVeDogrula(_tohumDbPath, _kullaniciDbPath, _eskiKullaniciDbPath);

    /// <summary>
    /// Belirtilen tohum ve kullanıcı dosyalarını yükler. Testler ve taşınabilir kurulumlar
    /// için yol alan sürümdür; kullanıcı dosyası yoksa yalnız tohum katalog kullanılır.
    /// </summary>
    public static (List<YapiElemani> Elemanlar, KutuphaneRapor Rapor) YukleVeDogrula(
        string tohumDosyasi,
        string kullaniciDosyasi) =>
        YukleVeDogrula(tohumDosyasi, kullaniciDosyasi, eskiKullaniciDosyasi: null);

    /// <summary>
    /// Eski per-user dosyasını ve uygulama-klasörü katalog farklarını taşıyabilen yol alan sürüm.
    /// <paramref name="eskiKullaniciDosyasi"/> null ise otomatik taşıma yapılmaz.
    /// </summary>
    public static (List<YapiElemani> Elemanlar, KutuphaneRapor Rapor) YukleVeDogrula(
        string tohumDosyasi,
        string kullaniciDosyasi,
        string? eskiKullaniciDosyasi)
    {
        var rapor = new KutuphaneRapor();
        try
        {
            var taban = TohumKatalogunuYukle(tohumDosyasi, rapor);
            var kullanici = KullaniciDegisiklikleriniYukle(
                kullaniciDosyasi, eskiKullaniciDosyasi, taban, rapor);
            var birlesik = DegisiklikleriUygula(taban, kullanici, rapor);

            return birlesik.Count > 0
                ? (birlesik, rapor)
                : (VarsayilanKatalog(), rapor);
        }
        catch (Exception ex)
        {
            rapor.GenelUyarilar.Add($"Beklenmeyen hata: {ex.Message}. Varsayılan katalog kullanılıyor.");
            return (VarsayilanKatalog(), rapor);
        }
    }

    private static List<YapiElemani> TohumKatalogunuYukle(string yol, KutuphaneRapor rapor)
    {
        List<YapiElemani> ham;
        if (!File.Exists(yol))
        {
            rapor.GenelUyarilar.Add($"Tohum katalog bulunamadı: {yol}. Yerleşik katalog kullanılıyor.");
            ham = new List<YapiElemani>();
        }
        else
        {
            try
            {
                ham = JsonSerializer.Deserialize<List<YapiElemani>>(File.ReadAllText(yol), _jsonOptions)
                      ?? new List<YapiElemani>();
            }
            catch (JsonException ex)
            {
                rapor.GenelUyarilar.Add($"Tohum katalog JSON hatası: {ex.Message}. Yerleşik katalog kullanılıyor.");
                ham = new List<YapiElemani>();
            }
        }

        var dogrulanmis = DogrulaVeTamamla(ham, rapor);
        var gecerli = new List<YapiElemani>();
        foreach (var eleman in dogrulanmis)
        {
            int mevcut = gecerli.FindIndex(e => Anahtar(e) == Anahtar(eleman));
            if (mevcut < 0)
            {
                gecerli.Add(eleman);
                continue;
            }

            // Dosya sırası deterministiktir; aynı anahtarda son kayıt kazanır.
            rapor.GenelUyarilar.Add(
                $"Tohum katalogda yinelenen '{eleman.Ad}' ({eleman.Tip}) kaydı bulundu; son kayıt kullanıldı.");
            gecerli[mevcut] = eleman;
        }
        foreach (var varsayilan in VarsayilanKatalog())
        {
            if (!gecerli.Any(e => Anahtar(e) == Anahtar(varsayilan)))
            {
                TamamlaOktavVerisi(varsayilan);
                gecerli.Add(varsayilan);
            }
        }

        return gecerli;
    }

    private static List<YapiElemani> DogrulaVeTamamla(
        IEnumerable<YapiElemani> elemanlar,
        KutuphaneRapor rapor)
    {
        var gecerli = new List<YapiElemani>();
        int index = 0;
        foreach (var eleman in elemanlar)
        {
            if (eleman is null)
            {
                rapor.GenelUyarilar.Add($"Katalogdaki #{index++} eleman null olduğu için atıldı.");
                continue;
            }
            // JSON'da açıkça null yazılmış koleksiyonları model varsayılanlarına döndür.
            eleman.Katmanlar ??= new List<YapiKatmani>();
            eleman.RThirdOctave ??= new List<double>();
            eleman.DeltaRThirdOctave ??= new List<double>();
            eleman.Belgeler ??= new List<UrunBelgesi>();
            var sonuc = DogrulaElemanDetay(eleman, index++);
            if (sonuc.Hatalar.Count > 0)
            {
                rapor.AtilanElemanlar.Add(sonuc);
                continue;
            }

            if (sonuc.Uyarilar.Count > 0)
                rapor.UyariliElemanlar.Add(sonuc);
            TamamlaOktavVerisi(eleman);
            gecerli.Add(eleman);
        }

        return gecerli;
    }

    private static KatalogDegisiklikDosyasi KullaniciDegisiklikleriniYukle(
        string kullaniciYolu,
        string? eskiKullaniciYolu,
        IReadOnlyCollection<YapiElemani> taban,
        KutuphaneRapor rapor)
    {
        string? okunacakYol = File.Exists(kullaniciYolu)
            ? kullaniciYolu
            : !string.IsNullOrWhiteSpace(eskiKullaniciYolu) && File.Exists(eskiKullaniciYolu)
                ? eskiKullaniciYolu
                : null;
        if (okunacakYol == null)
        {
            // Eski sürümler doğrudan uygulama klasöründeki buildingelements.json dosyasına
            // yazıyordu. Ayrı kullanıcı dosyasının ilk oluşturuluşunda, yerleşik katalogdan
            // farklı olan bu kayıtları kaybetmeden per-user alana taşı. Tohumla tamamen aynı
            // yerleşik elemanlar override yapılmaz; sonraki sürümlerin katalog güncellemelerini alır.
            if (!string.IsNullOrWhiteSpace(eskiKullaniciYolu))
            {
                var eskiFarklar = EskiTohumFarklariniBul(taban);
                if (eskiFarklar.Count > 0)
                {
                    var tasinacak = new KatalogDegisiklikDosyasi { Elemanlar = eskiFarklar };
                    try
                    {
                        AtomikYaz(kullaniciYolu, JsonSerializer.Serialize(tasinacak, _jsonOptions));
                        rapor.GenelUyarilar.Add(
                            $"Uygulama klasöründeki {eskiFarklar.Count} eski katalog farkı kullanıcı veri alanına taşındı.");
                    }
                    catch (Exception ex)
                    {
                        // Yükleme yine mevcut tohumla devam eder; sonraki açılışta taşıma yeniden denenir.
                        rapor.GenelUyarilar.Add($"Eski katalog farkları kullanıcı alanına taşınamadı: {ex.Message}");
                    }
                    return tasinacak;
                }
            }

            return new KatalogDegisiklikDosyasi();
        }

        try
        {
            string json = File.ReadAllText(okunacakYol);
            using var belge = JsonDocument.Parse(json);
            KatalogDegisiklikDosyasi dosya;
            bool eskiDiziSemasi = belge.RootElement.ValueKind == JsonValueKind.Array;
            if (eskiDiziSemasi)
            {
                var eskiListe = JsonSerializer.Deserialize<List<YapiElemani>>(json, _jsonOptions)
                                ?? new List<YapiElemani>();
                var eskiAnahtarlar = eskiListe.Select(Anahtar).ToHashSet(StringComparer.Ordinal);
                dosya = new KatalogDegisiklikDosyasi
                {
                    Elemanlar = eskiListe,
                    SilinenAnahtarlar = taban.Select(Anahtar)
                        .Where(k => !eskiAnahtarlar.Contains(k))
                        .OrderBy(k => k, StringComparer.Ordinal)
                        .ToList()
                };
            }
            else
            {
                dosya = JsonSerializer.Deserialize<KatalogDegisiklikDosyasi>(json, _jsonOptions)
                        ?? new KatalogDegisiklikDosyasi();
                if (dosya.SchemaVersion > KatalogDegisiklikDosyasi.GuncelSchemaVersion)
                {
                    rapor.GenelUyarilar.Add(
                        $"Kullanıcı kataloğu şeması desteklenmiyor ({dosya.SchemaVersion}). Değişiklikler yüklenmedi.");
                    return new KatalogDegisiklikDosyasi();
                }
            }

            dosya.Elemanlar ??= new List<YapiElemani>();
            dosya.SilinenAnahtarlar ??= new List<string>();

            // Eski dosya adı veya eski yalın-dizi biçimi atomik olarak güncel biçime taşınır.
            if (!string.Equals(okunacakYol, kullaniciYolu, StringComparison.Ordinal) || eskiDiziSemasi)
            {
                try
                {
                    AtomikYaz(kullaniciYolu, JsonSerializer.Serialize(dosya, _jsonOptions));
                    rapor.GenelUyarilar.Add("Eski kullanıcı kataloğu yeni uygulama-verisi şemasına taşındı.");
                }
                catch (Exception ex)
                {
                    rapor.GenelUyarilar.Add($"Kullanıcı kataloğu taşınamadı: {ex.Message}");
                }
            }

            return dosya;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            rapor.GenelUyarilar.Add($"Kullanıcı kataloğu okunamadı: {ex.Message}. Tohum katalog kullanılıyor.");
            return new KatalogDegisiklikDosyasi();
        }
    }

    private static List<YapiElemani> DegisiklikleriUygula(
        List<YapiElemani> taban,
        KatalogDegisiklikDosyasi dosya,
        KutuphaneRapor rapor)
    {
        var silinenler = dosya.SilinenAnahtarlar
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .ToHashSet(StringComparer.Ordinal);
        var sonuc = taban.Where(e => !silinenler.Contains(Anahtar(e))).ToList();
        var gecerliDegisiklikler = DogrulaVeTamamla(dosya.Elemanlar, rapor);

        foreach (var eleman in gecerliDegisiklikler)
        {
            string anahtar = Anahtar(eleman);
            int index = sonuc.FindIndex(e => Anahtar(e) == anahtar);
            if (index >= 0)
                sonuc[index] = eleman;
            else
                sonuc.Add(eleman);
        }

        return sonuc;
    }

    private static List<YapiElemani> EskiTohumFarklariniBul(
        IReadOnlyCollection<YapiElemani> taban)
    {
        var yerlesik = VarsayilanKatalog();
        foreach (var eleman in yerlesik)
            TamamlaOktavVerisi(eleman);
        var yerlesikSozlugu = yerlesik.ToDictionary(Anahtar, StringComparer.Ordinal);

        return taban
            .Where(e => !yerlesikSozlugu.TryGetValue(Anahtar(e), out var varsayilan) ||
                        !Esdeger(e, varsayilan))
            .ToList();
    }

    private static List<YapiElemani> Yukle()
    {
        var (elemanlar, rapor) = YukleVeDogrula();
        _sonRapor = rapor;
        return elemanlar;
    }

    /// B14 — Zorunlu alanlar: Ad dolu, Rw ∈ [0, 100], Tip enum geçerli.
    /// Tutarlılık: Doseme Lnw>0, Pencere/Kapı Lnw=0, vb.
    private static bool DogrulaEleman(YapiElemani e) =>
        DogrulaElemanDetay(e, -1).Hatalar.Count == 0;

    /// B14 — Detaylı eleman doğrulaması. Hatalar (ELE) elemanı atar; uyarılar (UYR)
    /// yalnızca raporda görünür, eleman yine kataloğa alınır.
    public static ElemanDogrulamaSonucu DogrulaElemanDetay(YapiElemani e, int index)
    {
        ArgumentNullException.ThrowIfNull(e);
        e.Katmanlar ??= new List<YapiKatmani>();
        e.RThirdOctave ??= new List<double>();
        e.DeltaRThirdOctave ??= new List<double>();
        e.Belgeler ??= new List<UrunBelgesi>();
        var ad = string.IsNullOrWhiteSpace(e.Ad) ? $"#{index}" : e.Ad;
        var sonuc = new ElemanDogrulamaSonucu(index, ad);

        // ── Zorunlu alanlar ──────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(e.Ad))
            sonuc.Hatalar.Add("Ad alanı boş");
        if (!Enum.IsDefined(typeof(YapiElemaniTipi), e.Tip))
            sonuc.Hatalar.Add($"Tip geçersiz: {e.Tip}");
        if (e.Rw < 0 || e.Rw > 100)
            sonuc.Hatalar.Add($"Rw aralık dışı: {e.Rw} (beklenen 0–100)");
        if (e.BirimFiyat < 0)
            sonuc.Hatalar.Add($"Birim fiyat negatif olamaz: {e.BirimFiyat}");
        if (e.FireYuzdesi is < 0 or > 100)
            sonuc.Hatalar.Add($"Fire yüzdesi aralık dışı: {e.FireYuzdesi} (beklenen 0–100)");
        if (string.IsNullOrWhiteSpace(e.ParaBirimi))
            sonuc.Uyarilar.Add("Para birimi boş; TRY kullanılacak");

        // ── Adaptasyon terimleri (genelde negatif) ───────────────────────
        if (e.C < -20 || e.C > 5)
            sonuc.Uyarilar.Add($"C alışılmadık: {e.C:F1} (beklenen -20..+5)");
        if (e.Ctr < -25 || e.Ctr > 5)
            sonuc.Uyarilar.Add($"Ctr alışılmadık: {e.Ctr:F1} (beklenen -25..+5)");

        // ── Per-band R değerleri ─────────────────────────────────────────
        void CheckR(double v, string band)
        {
            if (v > 0 && (v < 5 || v > 90))
                sonuc.Uyarilar.Add($"R{band} aralık dışı: {v:F1} (beklenen 5–90)");
        }
        CheckR(e.R63, "63"); CheckR(e.R125, "125"); CheckR(e.R250, "250"); CheckR(e.R500, "500");
        CheckR(e.R1k, "1k"); CheckR(e.R2k, "2k"); CheckR(e.R4k, "4k"); CheckR(e.R8k, "8k");

        // RThirdOctave: 16 bant beklenir
        if (e.RThirdOctave.Count > 0 && e.RThirdOctave.Count != 16)
            sonuc.Uyarilar.Add($"RThirdOctave 16 bant olmalı, alınan: {e.RThirdOctave.Count}");
        for (int i = 0; i < e.RThirdOctave.Count; i++)
        {
            var v = e.RThirdOctave[i];
            if (v > 0 && (v < 5 || v > 95))
                sonuc.Uyarilar.Add($"RThirdOctave[{i}] aralık dışı: {v:F1}");
        }

        // ── Yapısal RT, dinamik rijitlik ────────────────────────────────
        if (e.YapisalRtSn < 0 || e.YapisalRtSn > 5)
            sonuc.Uyarilar.Add($"YapisalRtSn aralık dışı: {e.YapisalRtSn:F2} (beklenen 0–5; 0=varsayılan)");
        if (e.DinamikRijitlikMNm3 < 0 || e.DinamikRijitlikMNm3 > 500)
            sonuc.Uyarilar.Add($"DinamikRijitlikMNm3 aralık dışı: {e.DinamikRijitlikMNm3:F1}");
        if (e.FlankingDuzeltmesiDb < 0 || e.FlankingDuzeltmesiDb > 30)
            sonuc.Uyarilar.Add($"FlankingDuzeltmesiDb aralık dışı: {e.FlankingDuzeltmesiDb:F1}");

        // ── Tip-spesifik tutarlılık ──────────────────────────────────────
        switch (e.Tip)
        {
            case YapiElemaniTipi.Doseme:
                if (e.Lnw <= 0)
                    sonuc.Uyarilar.Add("Döşeme için Lnw=0; çıplak L'n,w değeri gerekli (DeltaLw uygulanmadan)");
                else if (e.Lnw < 20 || e.Lnw > 100)
                    sonuc.Uyarilar.Add($"Lnw aralık dışı: {e.Lnw:F1} (beklenen 20–100)");
                if (e.DeltaLw < 0 || e.DeltaLw > 45)
                    sonuc.Uyarilar.Add($"DeltaLw aralık dışı: {e.DeltaLw:F1} (beklenen 0–45)");
                break;
            case YapiElemaniTipi.Pencere:
            case YapiElemaniTipi.Kapi:
                if (e.Lnw > 0)
                    sonuc.Uyarilar.Add($"{e.Tip} için Lnw>0 (atlanır): {e.Lnw:F1}");
                if (e.DeltaLw > 0)
                    sonuc.Uyarilar.Add($"{e.Tip} için DeltaLw>0 (atlanır): {e.DeltaLw:F1}");
                if (e.Rw > 0 && (e.Rw < 15 || e.Rw > 55))
                    sonuc.Uyarilar.Add($"{e.Tip} Rw alışılmadık: {e.Rw:F1} (tipik 20–50)");
                break;
        }

        return sonuc;
    }

    private static List<YapiElemani> VarsayilanKatalog()
    {
        var liste = new List<YapiElemani>
        {
            // ══════════════════════════════════════════════════════════════════
            // BETONARME DUVARLAR  (ρ≈2400 kg/m³, flanking=2.0)
            // Sıva: alçı/gipsit 1600 kg/m³ | çim-kireç 1800 kg/m³ | akustik 1600 kg/m³
            // Plaster Rw katkısı: alçı 1.5cm/yüz ≈+2 | çim 2cm/yüz ≈+3 | çim 3cm/yüz ≈+4 | ak.sıva 2cm/yüz ≈+6
            // ══════════════════════════════════════════════════════════════════

            // ── 10 cm çekirdek ───
            DT("Betonarme (10 cm) sıvasız",              10, 46, -1, -5, 2400, 2.0),
            DT("Betonarme (10 cm + alçı 1.5cm/yüz)",    13, 48, -1, -5, 2100, 2.0),
            DT("Betonarme (10 cm + çim.sıva 2cm/yüz)",  14, 49, -1, -5, 2200, 2.0),
            DT("Betonarme (10 cm + çim.sıva 3cm/yüz)",  16, 51, -1, -5, 2200, 2.0),
            DT("Betonarme (10 cm + ak.sıva 2cm/yüz)",   14, 52, -1, -5, 2100, 2.0),

            // ── 15 cm çekirdek ───
            DT("Betonarme (15 cm) sıvasız",              15, 50, -1, -5, 2400, 2.0),
            DT("Betonarme (15 cm + alçı 1.5cm/yüz)",    18, 52, -1, -5, 2200, 2.0),
            DT("Betonarme (15 cm + çim.sıva 2cm/yüz)",  19, 53, -1, -5, 2250, 2.0),
            DT("Betonarme (15 cm + çim.sıva 3cm/yüz)",  21, 55, -1, -5, 2250, 2.0),
            DT("Betonarme (15 cm + ak.sıva 2cm/yüz)",   19, 56, -1, -5, 2200, 2.0),

            // ── 20 cm çekirdek ───
            DT("Betonarme (20 cm) sıvasız",              20, 53, -1, -5, 2400, 2.0),
            DT("Betonarme (20 cm + alçı 1.5cm/yüz)",    23, 55, -1, -5, 2250, 2.0),
            DT("Betonarme (20 cm + çim.sıva 2cm/yüz)",  24, 56, -1, -5, 2280, 2.0),
            DT("Betonarme (20 cm + çim.sıva 3cm/yüz)",  26, 58, -1, -5, 2280, 2.0),
            DT("Betonarme (20 cm + ak.sıva 2cm/yüz)",   24, 59, -1, -5, 2250, 2.0),

            // ── 25 cm çekirdek ───
            DT("Betonarme (25 cm) sıvasız",              25, 56, -1, -5, 2400, 2.0),
            DT("Betonarme (25 cm + alçı 1.5cm/yüz)",    28, 58, -1, -5, 2280, 2.0),
            DT("Betonarme (25 cm + çim.sıva 2cm/yüz)",  29, 59, -1, -5, 2300, 2.0),
            DT("Betonarme (25 cm + çim.sıva 3cm/yüz)",  31, 61, -1, -5, 2300, 2.0),
            DT("Betonarme (25 cm + ak.sıva 2cm/yüz)",   29, 62, -1, -5, 2280, 2.0),

            // ── 30 cm çekirdek ───
            DT("Betonarme (30 cm) sıvasız",              30, 58, -1, -5, 2400, 2.0),
            DT("Betonarme (30 cm + çim.sıva 2cm/yüz)",  34, 61, -1, -5, 2300, 2.0),
            DT("Betonarme (30 cm + ak.sıva 2cm/yüz)",   34, 63, -1, -5, 2280, 2.0),

            // ══════════════════════════════════════════════════════════════════
            // TUĞLA DUVARLAR  (delikli tuğla ρ≈1300 kg/m³, flanking=2.5)
            // Sıva katkısı: alçı 1.5cm/yüz ≈+2 | çim 2cm/yüz ≈+3-4 | ak.sıva 2cm/yüz ≈+7-8
            // ══════════════════════════════════════════════════════════════════

            // ── 8.5 cm ───
            DT("Tuğla (8.5 cm) sıvasız",                 9, 39, -1, -4, 1300, 2.5),
            DT("Tuğla (8.5 cm + alçı 1.5cm/yüz)",       12, 41, -1, -4, 1450, 2.5),
            DT("Tuğla (8.5 cm + çim.sıva 2cm/yüz)",     13, 43, -1, -4, 1500, 2.5),
            DT("Tuğla (8.5 cm + çim.sıva 3cm/yüz)",     15, 45, -1, -4, 1550, 2.5),
            DT("Tuğla (8.5 cm + ak.sıva 2cm/yüz)",      13, 47, -1, -4, 1500, 2.5),

            // ── 11.5 cm ───
            DT("Tuğla (11.5 cm) sıvasız",               12, 42, -1, -4, 1300, 2.5),
            DT("Tuğla (11.5 cm + alçı 1.5cm/yüz)",      15, 44, -1, -4, 1450, 2.5),
            DT("Tuğla (11.5 cm + çim.sıva 2cm/yüz)",    16, 46, -1, -4, 1500, 2.5),
            DT("Tuğla (11.5 cm + çim.sıva 3cm/yüz)",    18, 48, -1, -4, 1550, 2.5),
            DT("Tuğla (11.5 cm + ak.sıva 2cm/yüz)",     16, 50, -1, -4, 1500, 2.5),

            // ── 19 cm ───
            DT("Tuğla (19 cm) sıvasız",                 19, 45, -1, -4, 1300, 2.5),
            DT("Tuğla (19 cm + alçı 1.5cm/yüz)",        22, 47, -1, -4, 1450, 2.5),
            DT("Tuğla (19 cm + çim.sıva 2cm/yüz)",      23, 49, -1, -4, 1500, 2.5),
            DT("Tuğla (19 cm + çim.sıva 3cm/yüz)",      25, 51, -1, -4, 1550, 2.5),
            DT("Tuğla (19 cm + ak.sıva 2cm/yüz)",       23, 53, -1, -4, 1500, 2.5),

            // ── 24 cm ───
            DT("Tuğla (24 cm) sıvasız",                 24, 47, -1, -4, 1300, 2.5),
            DT("Tuğla (24 cm + alçı 1.5cm/yüz)",        27, 49, -1, -4, 1450, 2.5),
            DT("Tuğla (24 cm + çim.sıva 2cm/yüz)",      28, 51, -1, -4, 1500, 2.5),
            DT("Tuğla (24 cm + çim.sıva 3cm/yüz)",      30, 53, -1, -4, 1550, 2.5),
            DT("Tuğla (24 cm + ak.sıva 2cm/yüz)",       28, 56, -1, -4, 1500, 2.5),

            // ── Çift tuğla + hava boşluğu ───
            DT("Çift Tuğla (8.5+5+8.5 cm) sıvasız",    22, 50, -2, -5, 1100, 3.0),
            DT("Çift Tuğla (8.5+5+8.5 cm + alçı)",     28, 53, -2, -5, 1200, 3.0),
            DT("Çift Tuğla (8.5+5+8.5 + çim.sıva 2)",  29, 55, -2, -5, 1250, 3.0),
            DT("Çift Tuğla (11.5+5+11.5 cm) sıvasız",  28, 53, -2, -5, 1100, 3.0),
            DT("Çift Tuğla (11.5+5+11.5 + alçı)",      34, 56, -2, -5, 1200, 3.0),

            // ══════════════════════════════════════════════════════════════════
            // GAZBETON / AAC  (ρ≈600 kg/m³, flanking=2.5)
            // Sıvasız Rw düşük — sıva kritik öneme sahip. Ak.sıva büyük katkı sağlar.
            // ══════════════════════════════════════════════════════════════════

            // ── 10 cm ───
            DT("Gazbeton AAC (10 cm) sıvasız",              10, 30, -1, -3,  600, 2.5),
            DT("Gazbeton AAC (10 cm + alçı 1.5cm/yüz)",     13, 33, -1, -4,  880, 2.5),
            DT("Gazbeton AAC (10 cm + çim.sıva 2cm/yüz)",   14, 35, -1, -4,  930, 2.5),
            DT("Gazbeton AAC (10 cm + çim.sıva 3cm/yüz)",   16, 37, -1, -4,  980, 2.5),
            DT("Gazbeton AAC (10 cm + ak.sıva 2cm/yüz)",    14, 40, -1, -4,  930, 2.5),

            // ── 15 cm ───
            DT("Gazbeton AAC (15 cm) sıvasız",              15, 34, -1, -3,  600, 2.5),
            DT("Gazbeton AAC (15 cm + alçı 1.5cm/yüz)",     18, 37, -1, -4,  840, 2.5),
            DT("Gazbeton AAC (15 cm + çim.sıva 2cm/yüz)",   19, 39, -1, -4,  890, 2.5),
            DT("Gazbeton AAC (15 cm + çim.sıva 3cm/yüz)",   21, 41, -1, -4,  940, 2.5),
            DT("Gazbeton AAC (15 cm + ak.sıva 2cm/yüz)",    19, 44, -1, -4,  890, 2.5),

            // ── 20 cm ───
            DT("Gazbeton AAC (20 cm) sıvasız",              20, 37, -1, -3,  600, 2.5),
            DT("Gazbeton AAC (20 cm + alçı 1.5cm/yüz)",     23, 40, -1, -4,  820, 2.5),
            DT("Gazbeton AAC (20 cm + çim.sıva 2cm/yüz)",   24, 42, -1, -4,  860, 2.5),
            DT("Gazbeton AAC (20 cm + çim.sıva 3cm/yüz)",   26, 44, -1, -4,  900, 2.5),
            DT("Gazbeton AAC (20 cm + ak.sıva 2cm/yüz)",    24, 47, -1, -4,  860, 2.5),

            // ── 25 cm ───
            DT("Gazbeton AAC (25 cm) sıvasız",              25, 40, -1, -3,  600, 2.5),
            DT("Gazbeton AAC (25 cm + alçı 1.5cm/yüz)",     28, 43, -1, -4,  800, 2.5),
            DT("Gazbeton AAC (25 cm + çim.sıva 2cm/yüz)",   29, 45, -1, -4,  840, 2.5),
            DT("Gazbeton AAC (25 cm + çim.sıva 3cm/yüz)",   31, 47, -1, -4,  870, 2.5),
            DT("Gazbeton AAC (25 cm + ak.sıva 2cm/yüz)",    29, 50, -1, -4,  840, 2.5),

            // ══════════════════════════════════════════════════════════════════
            // BİMS BETON BLOK  (ρ≈900 kg/m³, flanking=2.5)
            // ══════════════════════════════════════════════════════════════════

            DT("Bims Beton Blok (15 cm) sıvasız",           15, 40, -1, -4,  900, 2.5),
            DT("Bims Beton Blok (15 cm + alçı 1.5cm/yüz)",  18, 43, -1, -4, 1050, 2.5),
            DT("Bims Beton Blok (15 cm + çim.sıva 2cm/yüz)",19, 45, -1, -4, 1100, 2.5),
            DT("Bims Beton Blok (15 cm + ak.sıva 2cm/yüz)", 19, 48, -1, -4, 1100, 2.5),

            DT("Bims Beton Blok (20 cm) sıvasız",           20, 43, -1, -4,  900, 2.5),
            DT("Bims Beton Blok (20 cm + alçı 1.5cm/yüz)",  23, 46, -1, -4, 1050, 2.5),
            DT("Bims Beton Blok (20 cm + çim.sıva 2cm/yüz)",24, 48, -1, -4, 1100, 2.5),
            DT("Bims Beton Blok (20 cm + ak.sıva 2cm/yüz)", 24, 51, -1, -4, 1100, 2.5),

            // ══════════════════════════════════════════════════════════════════
            // DOĞAL TAŞ DUVARLAR  (ρ≈2500 kg/m³, flanking=2.0)
            // ══════════════════════════════════════════════════════════════════

            DT("Doğal Taş (20 cm) sıvasız",                 20, 50, -1, -5, 2500, 2.0),
            DT("Doğal Taş (20 cm + çim.sıva 2cm/yüz)",      24, 53, -1, -5, 2400, 2.0),
            DT("Doğal Taş (20 cm + ak.sıva 2cm/yüz)",       24, 55, -1, -5, 2400, 2.0),
            DT("Doğal Taş (30 cm) sıvasız",                 30, 54, -1, -5, 2500, 2.0),
            DT("Doğal Taş (30 cm + çim.sıva 2cm/yüz)",      34, 57, -1, -5, 2400, 2.0),

            // ══════════════════════════════════════════════════════════════════
            // HAFIF BÖLMELER
            // ══════════════════════════════════════════════════════════════════

            HafifBolme("Tek İskelet Alçıpan (C75, 1×12.5 mm)",        10, 38, 4.0),
            HafifBolme("Tek İskelet Alçıpan (C75, 2×12.5 mm)",        12, 42, 3.5),
            HafifBolme("Tek İskelet Alçıpan (C100, 2×12.5 mm + TY)",  13, 47, 3.0),
            HafifBolme("Çift İskelet Alçıpan (2×12.5 mm + 50 mm TY)", 17, 52, 2.5),
            HafifBolme("Çift İskelet Alçıpan (2×15 mm + 100 mm TY)",  19, 55, 2.0),
            HafifBolme("Bağımsız Karkas + Alçıpan + TY",              20, 60, 1.5),
            HafifBolme("CLT Panel (90 mm)",                             9, 40, 3.0),
            HafifBolme("CLT Panel (120 mm)",                           12, 44, 2.5),
            HafifBolme("CLT Panel (200 mm)",                           20, 50, 2.0),
            HafifBolme("Hafif Çelik Karkas Alçıpan",                   12, 44, 3.5),

            // ══════════════════════════════════════════════════════════════════
            // DÖŞEMELER
            // ══════════════════════════════════════════════════════════════════
            // Rw  = hava sesi yalıtım değeri [dB]
            // Lnw = standartize darbe sesi basınç düzeyi [dB] — düşük iyidir
            // Birleşik sistemlerde değerler doğrudan girilmiştir:
            //   Rw_birleşik  = Rw_beton + ΔRw_yüzer
            //   Lnw_birleşik = Lnw_beton − ΔLnw_yüzer

            // ── Çıplak betonarme döşemeler (referans) ─────────────────────────
            Doseme("Betonarme Döşeme (12 cm)",              12, 48, 79,  0, 2.0),
            Doseme("Betonarme Döşeme (15 cm)",              15, 52, 75,  0, 2.0),
            Doseme("Betonarme Döşeme (18 cm)",              18, 54, 73,  0, 2.0),
            Doseme("Betonarme Döşeme (20 cm)",              20, 55, 72,  0, 2.0),
            Doseme("Betonarme Döşeme (22 cm)",              22, 56, 71,  0, 2.0),
            Doseme("Betonarme Döşeme (25 cm)",              25, 57, 70,  0, 2.0),
            Doseme("Betonarme Döşeme (28 cm)",              28, 59, 68,  0, 2.0),
            Doseme("Betonarme Döşeme (30 cm)",              30, 60, 67,  0, 2.0),

            // ── Asmolen döşemeler (çıplak referans) ───────────────────────────
            Doseme("Asmolen Döşeme (20+5 cm şap)",          25, 48, 79,  0, 2.0),
            Doseme("Asmolen Döşeme (25+5 cm şap)",          30, 50, 77,  0, 2.0),
            Doseme("Asmolen Döşeme (30+5 cm şap)",          35, 52, 75,  0, 2.0),

            // ── Nervürlü / kaset döşemeler (çıplak referans) ─────────────────
            Doseme("Nervürlü Döşeme (20+5 cm şap)",         25, 46, 80,  0, 2.0),
            Doseme("Kaset Döşeme (25+5 cm şap)",            30, 49, 78,  0, 2.0),

            // ── BA 15 cm + Yüzer Döşeme Sistemleri ───────────────────────────
            // Taban: Rw=52 dB, Lnw=75 dB  |  toplam kalınlık = 15 cm + yüzer sistem
            Doseme("BA 15cm + EPS 3cm + şap 5cm",           23, 53, 53,  0, 2.0),
            Doseme("BA 15cm + EPS 5cm + şap 5cm",           25, 53, 50,  0, 2.0),
            Doseme("BA 15cm + XPS 3cm + şap 5cm",           23, 53, 50,  0, 2.0),
            Doseme("BA 15cm + XPS 5cm + şap 5cm",           25, 54, 47,  0, 2.0),
            Doseme("BA 15cm + Taşyünü 2cm + şap 5cm",       22, 54, 47,  0, 2.0),
            Doseme("BA 15cm + Taşyünü 3cm + şap 5cm",       23, 54, 45,  0, 2.0),
            Doseme("BA 15cm + Taşyünü 4cm + şap 6cm",       25, 54, 43,  0, 2.0),
            Doseme("BA 15cm + Elastik mat + şap 5cm",        22, 55, 40,  0, 2.0),
            Doseme("BA 15cm + Geri dön. kauçuk + şap 5cm",  22, 54, 42,  0, 2.0),

            // ── BA 20 cm + Yüzer Döşeme Sistemleri ───────────────────────────
            // Taban: Rw=55 dB, Lnw=72 dB  |  toplam kalınlık = 20 cm + yüzer sistem
            Doseme("BA 20cm + EPS 3cm + şap 5cm",           28, 56, 50,  0, 2.0),
            Doseme("BA 20cm + EPS 5cm + şap 5cm",           30, 56, 47,  0, 2.0),
            Doseme("BA 20cm + XPS 3cm + şap 5cm",           28, 56, 47,  0, 2.0),
            Doseme("BA 20cm + XPS 5cm + şap 5cm",           30, 57, 44,  0, 2.0),
            Doseme("BA 20cm + Taşyünü 2cm + şap 5cm",       27, 57, 44,  0, 2.0),
            Doseme("BA 20cm + Taşyünü 3cm + şap 5cm",       28, 57, 42,  0, 2.0),
            Doseme("BA 20cm + Taşyünü 4cm + şap 6cm",       30, 57, 40,  0, 2.0),
            Doseme("BA 20cm + Elastik mat + şap 5cm",        27, 58, 37,  0, 2.0),
            Doseme("BA 20cm + Geri dön. kauçuk + şap 5cm",  27, 57, 39,  0, 2.0),

            // ── BA 25 cm + Yüzer Döşeme Sistemleri ───────────────────────────
            // Taban: Rw=57 dB, Lnw=70 dB  |  toplam kalınlık = 25 cm + yüzer sistem
            Doseme("BA 25cm + EPS 3cm + şap 5cm",           33, 58, 48,  0, 2.0),
            Doseme("BA 25cm + EPS 5cm + şap 5cm",           35, 58, 45,  0, 2.0),
            Doseme("BA 25cm + XPS 3cm + şap 5cm",           33, 58, 45,  0, 2.0),
            Doseme("BA 25cm + XPS 5cm + şap 5cm",           35, 59, 42,  0, 2.0),
            Doseme("BA 25cm + Taşyünü 2cm + şap 5cm",       32, 59, 42,  0, 2.0),
            Doseme("BA 25cm + Taşyünü 3cm + şap 5cm",       33, 59, 40,  0, 2.0),
            Doseme("BA 25cm + Taşyünü 4cm + şap 6cm",       35, 59, 38,  0, 2.0),
            Doseme("BA 25cm + Elastik mat + şap 5cm",        32, 60, 35,  0, 2.0),
            Doseme("BA 25cm + Geri dön. kauçuk + şap 5cm",  32, 59, 37,  0, 2.0),

            // ── Zemin kaplamaları (ΔLw katkısı; tek başına kullanılır) ────────
            Doseme("Seramik / Porselen Karo",                 1, 52, 80,  2, 0.0),
            Doseme("Beton / Epoksi Zemin (sert)",             1, 52, 80,  0, 0.0),
            Doseme("Laminat + PE köpük 3mm",                  1, 52, 80,  6, 0.0),
            Doseme("Laminat + PE köpük 5mm",                  1, 52, 80, 10, 0.0),
            Doseme("Laminat + akustik underlay 8mm",          1, 52, 80, 15, 0.0),
            Doseme("Ahşap Parke (14mm) + astar",              2, 52, 80, 10, 0.0),
            Doseme("Ahşap Parke (20mm) + astar",              2, 52, 80, 12, 0.0),
            Doseme("Geri dön. kauçuk underlay 5mm",           1, 52, 80, 14, 0.0),
            Doseme("Geri dön. kauçuk underlay 10mm",          1, 52, 80, 20, 0.0),
            Doseme("Mantar Döşeme (4mm)",                     1, 52, 80, 18, 0.0),
            Doseme("Mantar Döşeme (8mm)",                     1, 52, 80, 22, 0.0),
            Doseme("Halı (hafif, 5mm)",                       1, 52, 80, 20, 0.0),
            Doseme("Halı (orta, 8mm)",                        1, 52, 80, 25, 0.0),
            Doseme("Halı (ağır, 8mm + astar)",                1, 52, 80, 30, 0.0),

            // ══════════════════════════════════════════════════════════════════
            // TAVANLAR
            // ══════════════════════════════════════════════════════════════════

            Tavan("Alçıpan Asma Tavan (1×12.5 mm)",        10, 42, 3.0),
            Tavan("Alçıpan Asma Tavan (2×12.5 mm)",        12, 48, 2.5),
            Tavan("Akustik Asma Tavan (Mineral yün panel)", 15, 48, 2.0),
            Tavan("Akustik Asma Tavan (Yüksek performans)", 20, 52, 2.0),
            Tavan("Metal Kaset Asma Tavan",                 10, 35, 4.0),

            // ══════════════════════════════════════════════════════════════════
            // PENCERELER
            // ══════════════════════════════════════════════════════════════════

            Pencere("Tek Cam (4 mm)",                        23),
            Pencere("Tek Cam (6 mm)",                        25),
            Pencere("Tek Cam (8 mm)",                        27),
            Pencere("Çift Cam (4+6+4)",                      28),
            Pencere("Çift Cam (4+8+4)",                      30),
            Pencere("Çift Cam (4+12+4)",                     32),
            Pencere("Çift Cam (4+16+4)",                     34),
            Pencere("Çift Cam (6+12+6)",                     36),
            Pencere("Çift Cam (6+16+6)",                     38),
            Pencere("Lamine Cam (33.2)",                     35),
            Pencere("Lamine Cam (44.2)",                     38),
            Pencere("Lamine Cam (55.2)",                     40),
            Pencere("Akustik Çift Cam (lamine + boşluk)",    42),
            Pencere("Üçlü Cam Pencere",                      44),
            Pencere("Yüksek Performans Akustik Cam",         48),

            // ══════════════════════════════════════════════════════════════════
            // KAPILAR
            // ══════════════════════════════════════════════════════════════════

            Kapi("Ahşap İçi Boş Kapı (35 mm)",              18),
            Kapi("Ahşap Dolu Kapı (35 mm)",                  25),
            Kapi("Ahşap Dolu Kapı (45 mm)",                  28),
            Kapi("MDF Kapı (40 mm)",                         30),
            Kapi("Çelik Sac Kapı",                           32),
            Kapi("Ahşap Çerçeveli Cam Kapı",                 25),
            Kapi("Akustik Kapı (Rw 38)",                     38),
            Kapi("Yüksek Performans Akustik Kapı",           42),
            Kapi("Yangın + Akustik Kapı (E60)",              45),
        };
        foreach (var eleman in liste)
            TamamlaOktavVerisi(eleman);
        return liste;
    }

    /// Tam parametreli duvar yardımcısı — ρ ve C/Ctr değerleri açıkça girilir.
    /// "DT" kısaltması katalog okunabilirliği için (Duvar Tam).
    private static YapiElemani DT(string ad, double kalinlikCm, double rw,
        double c, double ctr, double yogunluk, double flanking) =>
        new()
        {
            Ad = ad, Tip = YapiElemaniTipi.Duvar,
            Rw = rw, C = c, Ctr = ctr,
            FlankingDuzeltmesiDb = flanking,
            Katmanlar = [new YapiKatmani
            {
                MalzemeAdi = ad, Kalinlik = kalinlikCm * 10, Yogunluk = yogunluk
            }]
        };

    private static YapiElemani Duvar(string ad, double kalinlikCm, double rw, double flanking) =>
        new()
        {
            Ad = ad, Tip = YapiElemaniTipi.Duvar,
            Rw = rw, C = -1, Ctr = -4,
            FlankingDuzeltmesiDb = flanking,
            Katmanlar = [new YapiKatmani
            {
                MalzemeAdi = ad, Kalinlik = kalinlikCm * 10,
                Yogunluk = ad.Contains("Beton") ? 2400 : 1200
            }]
        };

    private static YapiElemani HafifBolme(string ad, double kalinlikCm, double rw, double flanking) =>
        new()
        {
            Ad = ad, Tip = YapiElemaniTipi.Duvar,
            Rw = rw, C = -2, Ctr = -5,
            FlankingDuzeltmesiDb = flanking,
            Hafiflik = YapiElemaniHafifligi.Hafif,
            Katmanlar = [new YapiKatmani
            {
                MalzemeAdi = "Alçıpan + Taşyünü", Kalinlik = kalinlikCm * 10, Yogunluk = 450
            }]
        };

    private static YapiElemani Doseme(string ad, double kalinlikCm, double rw, double lnw,
                                       double deltaLw, double flanking) =>
        new()
        {
            Ad = ad, Tip = YapiElemaniTipi.Doseme,
            Rw = rw, Lnw = lnw, DeltaLw = deltaLw,
            FlankingDuzeltmesiDb = flanking,
            Katmanlar = [new YapiKatmani
            {
                MalzemeAdi = ad, Kalinlik = kalinlikCm * 10, Yogunluk = 2200
            }]
        };

    private static YapiElemani Tavan(string ad, double kalinlikCm, double rw, double flanking) =>
        new()
        {
            Ad = ad, Tip = YapiElemaniTipi.Tavan,
            Rw = rw, C = -2, Ctr = -5,
            FlankingDuzeltmesiDb = flanking,
            Hafiflik = YapiElemaniHafifligi.Hafif,
            Katmanlar = [new YapiKatmani
            {
                MalzemeAdi = ad, Kalinlik = kalinlikCm * 10, Yogunluk = 650
            }]
        };

    private static YapiElemani Pencere(string ad, double rw) =>
        new() { Ad = ad, Tip = YapiElemaniTipi.Pencere, Rw = rw, C = -1, Ctr = -4 };

    private static YapiElemani Kapi(string ad, double rw) =>
        new() { Ad = ad, Tip = YapiElemaniTipi.Kapi, Rw = rw, C = -1, Ctr = -3 };

    /// Belleği devre dışı bırakıp listeyi yeniden yüklemek için (test/kullanıcı güncellemesi)
    public static List<YapiElemani> YenidenYukle()
    {
        _cache = Yukle();
        return _cache;
    }

    /// <summary>
    /// Katalogdaki kullanıcı farklarını per-user uygulama-verisi klasörüne atomik olarak
    /// yazar. Hata durumunda istisna fırlatır; çağıran arayüz başarı mesajı göstermemelidir.
    /// </summary>
    public static void Kaydet(List<YapiElemani> elemanlar) =>
        Kaydet(elemanlar, _tohumDbPath, _kullaniciDbPath, cacheGuncelle: true);

    /// <summary>Belirtilen yolları kullanan kayıt sürümü (test/taşınabilir kurulum).</summary>
    public static void Kaydet(
        List<YapiElemani> elemanlar,
        string tohumDosyasi,
        string kullaniciDosyasi) =>
        Kaydet(elemanlar, tohumDosyasi, kullaniciDosyasi, cacheGuncelle: false);

    private static void Kaydet(
        List<YapiElemani> elemanlar,
        string tohumDosyasi,
        string kullaniciDosyasi,
        bool cacheGuncelle)
    {
        ArgumentNullException.ThrowIfNull(elemanlar);

        for (int i = 0; i < elemanlar.Count; i++)
        {
            if (elemanlar[i] is null)
                throw new InvalidDataException($"Katalogdaki #{i} eleman null olduğu için kaydedilemez.");
            var dogrulama = DogrulaElemanDetay(elemanlar[i], i);
            if (dogrulama.Hatalar.Count > 0)
            {
                throw new InvalidDataException(
                    $"'{dogrulama.Ad}' kaydedilemedi: {string.Join("; ", dogrulama.Hatalar)}");
            }
        }

        var tekrar = elemanlar
            .GroupBy(Anahtar, StringComparer.Ordinal)
            .FirstOrDefault(g => g.Count() > 1);
        if (tekrar != null)
            throw new InvalidDataException(
                $"Aynı ad ve tipte birden fazla katalog elemanı var: '{tekrar.First().Ad}' ({tekrar.First().Tip}).");

        foreach (var eleman in elemanlar)
            TamamlaOktavVerisi(eleman);

        var taban = TohumKatalogunuYukle(tohumDosyasi, new KutuphaneRapor());
        var tabanSozlugu = taban.ToDictionary(Anahtar, StringComparer.Ordinal);
        var mevcutSozlugu = elemanlar.ToDictionary(Anahtar, StringComparer.Ordinal);
        var dosya = new KatalogDegisiklikDosyasi
        {
            Elemanlar = elemanlar
                .Where(e => !tabanSozlugu.TryGetValue(Anahtar(e), out var temel) || !Esdeger(e, temel))
                .ToList(),
            SilinenAnahtarlar = taban
                .Select(Anahtar)
                .Where(k => !mevcutSozlugu.ContainsKey(k))
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToList()
        };

        AtomikYaz(kullaniciDosyasi, JsonSerializer.Serialize(dosya, _jsonOptions));
        if (cacheGuncelle)
            _cache = elemanlar;
    }

    public static void Degistir(List<YapiElemani> elemanlar)
    {
        Kaydet(elemanlar);
    }

    private static bool Esdeger(YapiElemani a, YapiElemani b) =>
        JsonSerializer.Serialize(a, _jsonOptions) == JsonSerializer.Serialize(b, _jsonOptions);

    private static string Anahtar(YapiElemani eleman) =>
        $"{(int)eleman.Tip}|{eleman.Ad.Trim().ToUpperInvariant()}";

    private static void AtomikYaz(string yol, string icerik)
    {
        string? klasor = Path.GetDirectoryName(yol);
        if (string.IsNullOrWhiteSpace(klasor))
            throw new IOException($"Katalog hedef klasörü belirlenemedi: {yol}");

        Directory.CreateDirectory(klasor);
        string gecici = Path.Combine(
            klasor,
            $".{Path.GetFileName(yol)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(gecici, icerik, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(gecici, yol, overwrite: true);
        }
        finally
        {
            if (File.Exists(gecici))
                File.Delete(gecici);
        }
    }

    /// Eksik oktav bant verilerini fizik modeliyle doldurur. Kısmi bir spektrum
    /// hiçbir zaman tam ölçüm/üretici verisi sayılmaz; modelle tamamlanırsa köken
    /// güvenli tarafta kalınarak Model'e düşürülür.
    public static void TamamlaOktavVerisi(YapiElemani eleman)
    {
        ArgumentNullException.ThrowIfNull(eleman);
        eleman.Ad ??= "";
        eleman.Uretici ??= "";
        eleman.UrunKodu ??= "";
        eleman.Kategori ??= "";
        eleman.PozNo ??= "";
        eleman.ParaBirimi = string.IsNullOrWhiteSpace(eleman.ParaBirimi) ? "TRY" : eleman.ParaBirimi;
        eleman.Katmanlar ??= new List<YapiKatmani>();
        eleman.RThirdOctave ??= new List<double>();
        eleman.DeltaRThirdOctave ??= new List<double>();
        eleman.Belgeler ??= new List<UrunBelgesi>();
        if (eleman.Id == Guid.Empty)
            eleman.Id = M08KararliKimlik.YapiElemani(eleman);

        var baslangicHavaKaynagi = OktavBantElemanServisi.EtkinSpektrumKaynagi(eleman);
        var baslangicDarbeKaynagi = OktavBantElemanServisi.EtkinDarbeSpektrumKaynagi(eleman);
        bool havaModeliKullanildi = false;
        bool darbeModeliKullanildi = false;
        bool ucuncuOktavTam = OktavBantElemanServisi.TamUcuncuOktavHavaVerisiVar(eleman);
        bool oktavTam = OktavBantElemanServisi.TamOktavHavaVerisiVar(eleman);

        if (eleman.Rw > 0 && !ucuncuOktavTam && !oktavTam)
        {
            // Hiçbir tam bant seti yok: kısmi değerleri ölçüm gibi kullanma;
            // fizik modelinden tutarlı ve eksiksiz bir set üret.
            double[] rf = OktavBantElemanServisi.GetRDizisi(eleman);
            eleman.R63  = Math.Round(rf[0], 1);
            eleman.R125 = Math.Round(rf[1], 1);
            eleman.R250 = Math.Round(rf[2], 1);
            eleman.R500 = Math.Round(rf[3], 1);
            eleman.R1k  = Math.Round(rf[4], 1);
            eleman.R2k  = Math.Round(rf[5], 1);
            eleman.R4k  = Math.Round(rf[6], 1);
            eleman.R8k = eleman.R4k;
            oktavTam = true;
            havaModeliKullanildi = true;
        }

        if (eleman.Rw > 0 && !ucuncuOktavTam)
        {
            // Tam 1/1 oktav seti ölçüm/üretici kaynaklıysa interpolasyon aynı kökeni
            // korur; yukarıda model üretildiyse model kökeni korunur.
            var r3 = OktavBantElemanServisi.GetRUcuncuOktavDizisi(eleman);
            eleman.RThirdOctave = r3.Select(v => Math.Round(v, 1)).ToList();
        }

        // Darbe sesi Ln(f): sadece döşemelerde, eksikse model tahmini
        if (eleman.Tip == YapiElemaniTipi.Doseme && eleman.Lnw > 0)
        {
            if (!OktavBantElemanServisi.TamOktavDarbeVerisiVar(eleman))
            {
                double[] lnf = OktavBantElemanServisi.GetLnDizisi(eleman);
                eleman.L63  = Math.Round(lnf[0], 1);
                eleman.L125 = Math.Round(lnf[1], 1);
                eleman.L250 = Math.Round(lnf[2], 1);
                eleman.L500 = Math.Round(lnf[3], 1);
                eleman.L1k  = Math.Round(lnf[4], 1);
                eleman.L2k  = Math.Round(lnf[5], 1);
                eleman.L4k  = Math.Round(lnf[6], 1);
                darbeModeliKullanildi = true;
            }
        }

        eleman.SpektrumKaynagi = havaModeliKullanildi
            ? SpektrumVeriKaynagi.Model
            : baslangicHavaKaynagi;

        if (eleman.Tip == YapiElemaniTipi.Doseme && eleman.Lnw > 0)
            eleman.DarbeSpektrumKaynagi = darbeModeliKullanildi
                ? SpektrumVeriKaynagi.Model
                : baslangicDarbeKaynagi;

        // Kayıplı legacy bool yalnız her ilgili spektrum gerçekten ölçüm ise true
        // kalır. Yeni kod iki köken alanını ayrı ayrı kullanır.
        eleman.HasRealOktavData = eleman.SpektrumKaynagi == SpektrumVeriKaynagi.Olcum &&
            (eleman.Tip != YapiElemaniTipi.Doseme || eleman.Lnw <= 0 ||
             eleman.DarbeSpektrumKaynagi == SpektrumVeriKaynagi.Olcum);
    }

    private sealed class KatalogDegisiklikDosyasi
    {
        public const int GuncelSchemaVersion = 1;

        public int SchemaVersion { get; set; } = GuncelSchemaVersion;
        public List<YapiElemani> Elemanlar { get; set; } = new();
        public List<string> SilinenAnahtarlar { get; set; } = new();
    }
}

/// B14 — Tek bir elemanın doğrulama sonucu (hata = atılır, uyarı = raporda kalır).
public sealed class ElemanDogrulamaSonucu
{
    public int Index { get; }
    public string Ad { get; }
    public List<string> Hatalar { get; } = new();
    public List<string> Uyarilar { get; } = new();

    public ElemanDogrulamaSonucu(int index, string ad)
    {
        Index = index;
        Ad = ad;
    }

    public string Ozet => $"#{Index} '{Ad}' — {Hatalar.Count} hata, {Uyarilar.Count} uyarı";
}

/// B14 — JSON katalog yükleme + doğrulama raporu. Genel hatalar (dosya yok, JSON parse),
/// atılan elemanlar (zorunlu alan eksik) ve uyarılı geçenler ayrı listelerde.
public sealed class KutuphaneRapor
{
    public List<string> GenelUyarilar { get; } = new();
    public List<ElemanDogrulamaSonucu> AtilanElemanlar { get; } = new();
    public List<ElemanDogrulamaSonucu> UyariliElemanlar { get; } = new();

    public bool HerSeyTemiz => GenelUyarilar.Count == 0
        && AtilanElemanlar.Count == 0
        && UyariliElemanlar.Count == 0;

    public string KisaOzet =>
        AtilanElemanlar.Count == 0 && UyariliElemanlar.Count == 0 && GenelUyarilar.Count == 0
            ? "Katalog temiz"
            : $"{AtilanElemanlar.Count} eleman atıldı · {UyariliElemanlar.Count} uyarılı · {GenelUyarilar.Count} genel uyarı";
}
