using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.Measurement.Data;

namespace Kst.Acoustics.Core.BuildingAcoustics.Parsers;

public sealed class SahaOlcumImportSonucu
{
    public List<SahaOlcumKaydi> Kayitlar { get; set; } = new();
    public List<string> Uyarilar { get; set; } = new();
    public List<string> Hatalar { get; set; } = new();
    public bool Basarili => Hatalar.Count == 0;
}

/// <summary>
/// M08 saha doğrulama kayıtlarını CSV, M08 JSON veya M07 ölçüm oturumundan okur.
/// Okuma proje nesnesini değiştirmez; <see cref="ProjeyeEkle"/> yalnız tüm kayıtlar
/// geçerliyse yeni listeyi tek seferde atar.
/// </summary>
public static class SahaOlcumImportServisi
{
    private static readonly double[] OktavFrekanslari = [63, 125, 250, 500, 1000, 2000, 4000, 8000];
    private static readonly double[] UcuncuOktavFrekanslari =
        [100, 125, 160, 200, 250, 315, 400, 500, 630, 800, 1000, 1250, 1600, 2000, 2500, 3150];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly string[] CsvBasliklari =
    [
        "Tur", "Ad", "Deger", "Birim", "Belirsizlik", "Durum", "KatKimligi",
        "KaynakMekanId", "AliciMekanId", "BolmeId", "OlcumTarihi", "Standart",
        "Yontem", "Cihaz", "Operator", "Notlar", "BantFrekanslariHz", "BantDegerleri",
        "RaporlamaIcinOnayli"
    ];

    public static SahaOlcumImportSonucu DosyadanOku(string dosyaYolu)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dosyaYolu);
        if (!File.Exists(dosyaYolu))
            throw new FileNotFoundException("Saha ölçümü dosyası bulunamadı.", dosyaYolu);

        byte[] icerik = File.ReadAllBytes(dosyaYolu);
        string sha = Convert.ToHexString(SHA256.HashData(icerik)).ToLowerInvariant();
        string uzanti = Path.GetExtension(dosyaYolu);
        var sonuc = string.Equals(uzanti, ".csv", StringComparison.OrdinalIgnoreCase)
            ? CsvOku(Encoding.UTF8.GetString(icerik))
            : JsonOku(Encoding.UTF8.GetString(icerik));

        foreach (var kayit in sonuc.Kayitlar)
        {
            kayit.KaynakDosya = Path.GetFullPath(dosyaYolu);
            kayit.KaynakDosyaSha256 = sha;
            if (string.IsNullOrWhiteSpace(kayit.KaynakKayitId))
                kayit.KaynakKayitId = kayit.Id.ToString("N");
        }
        Dogrula(sonuc);
        return sonuc;
    }

    public static SahaOlcumImportSonucu CsvMetnindenOku(string csv) => CsvOku(csv);

    public static void CsvSablonuOlustur(string dosyaYolu)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(';', CsvBasliklari));
        sb.AppendLine("BolmeHavaSesi;Örnek ölçüm;52.4;dB;1.2;Taslak;;00000000-0000-0000-0000-000000000000;;;2026-01-01T10:00:00;TS EN ISO 16283-1;;;;;100|125|160;45.1|47.2|49.0;false");
        M08Csv.AtomikYaz(dosyaYolu, sb.ToString());
    }

    public static int ProjeyeEkle(
        BinaProje proje,
        SahaOlcumImportSonucu import,
        bool mevcutlariGuncelle = true)
    {
        ArgumentNullException.ThrowIfNull(proje);
        ArgumentNullException.ThrowIfNull(import);
        Dogrula(import);
        if (!import.Basarili)
            throw new InvalidDataException("Saha ölçümleri uygulanamadı: " + string.Join("; ", import.Hatalar));

        var yeniListe = (proje.SahaOlcumleri ?? new List<SahaOlcumKaydi>()).ToList();
        int eklenen = 0;
        foreach (var kayit in import.Kayitlar)
        {
            int index = yeniListe.FindIndex(m => AyniKayit(m, kayit));
            if (index >= 0)
            {
                if (mevcutlariGuncelle)
                    yeniListe[index] = kayit;
                continue;
            }
            yeniListe.Add(kayit);
            eklenen++;
        }
        proje.SahaOlcumleri = yeniListe;
        return eklenen;
    }

    private static bool AyniKayit(SahaOlcumKaydi a, SahaOlcumKaydi b)
    {
        if (!string.IsNullOrWhiteSpace(a.KaynakDosyaSha256) &&
            !string.IsNullOrWhiteSpace(a.KaynakKayitId) &&
            !string.IsNullOrWhiteSpace(b.KaynakDosyaSha256) &&
            !string.IsNullOrWhiteSpace(b.KaynakKayitId))
        {
            return a.Tur == b.Tur &&
                   string.Equals(a.KaynakDosyaSha256, b.KaynakDosyaSha256, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(a.KaynakKayitId, b.KaynakKayitId, StringComparison.Ordinal);
        }
        return a.Id == b.Id;
    }

    private static SahaOlcumImportSonucu JsonOku(string json)
    {
        var sonuc = new SahaOlcumImportSonucu();
        try
        {
            using var belge = JsonDocument.Parse(json);
            var kok = belge.RootElement;
            if (kok.ValueKind == JsonValueKind.Array)
            {
                sonuc.Kayitlar = JsonSerializer.Deserialize<List<SahaOlcumKaydi>>(json, JsonOptions) ?? new();
                return sonuc;
            }

            if (kok.ValueKind != JsonValueKind.Object)
            {
                sonuc.Hatalar.Add("JSON kökü nesne veya dizi olmalıdır.");
                return sonuc;
            }

            if (OzellikVar(kok, "Measurements"))
            {
                var oturum = JsonSerializer.Deserialize<MeasurementSession>(json, JsonOptions);
                if (oturum is null)
                    sonuc.Hatalar.Add("M07 ölçüm oturumu okunamadı.");
                else
                    M07OturumunuDonustur(oturum, sonuc);
                return sonuc;
            }

            if (OzellikBul(kok, "Kayitlar", out var kayitlar) && kayitlar.ValueKind == JsonValueKind.Array)
            {
                sonuc.Kayitlar = kayitlar.Deserialize<List<SahaOlcumKaydi>>(JsonOptions) ?? new();
                return sonuc;
            }

            var tek = JsonSerializer.Deserialize<SahaOlcumKaydi>(json, JsonOptions);
            if (tek is null)
                sonuc.Hatalar.Add("Saha ölçümü JSON kaydı okunamadı.");
            else
                sonuc.Kayitlar.Add(tek);
        }
        catch (JsonException ex)
        {
            sonuc.Hatalar.Add($"Geçersiz JSON: {ex.Message}");
        }
        return sonuc;
    }

    private static void M07OturumunuDonustur(MeasurementSession oturum, SahaOlcumImportSonucu sonuc)
    {
        oturum.Measurements ??= new();
        foreach (var olcum in oturum.Measurements.Where(x => x is not null))
        {
            if (olcum.Parameters is null || !RtParametresiBul(olcum.Parameters, out var anahtar, out var bantlar))
            {
                sonuc.Uyarilar.Add($"M07 '{olcum.Label}' kaydında RT20/RT30/EDT/T60 bulunmadığı için atlandı.");
                continue;
            }

            var frekanslar = FrekanslariBelirle(bantlar.Length);
            var orta = bantlar
                .Select((v, i) => (Deger: v, Frekans: i < frekanslar.Length ? frekanslar[i] : double.NaN))
                .Where(x => double.IsFinite(x.Deger) && x.Deger > 0 && x.Frekans is >= 250 and <= 2000)
                .Select(x => x.Deger)
                .ToArray();
            if (orta.Length == 0)
                orta = bantlar.Where(v => double.IsFinite(v) && v > 0).ToArray();
            if (orta.Length == 0)
            {
                sonuc.Uyarilar.Add($"M07 '{olcum.Label}' kaydındaki {anahtar} değerleri geçersiz olduğu için atlandı.");
                continue;
            }

            sonuc.Kayitlar.Add(new SahaOlcumKaydi
            {
                Ad = string.IsNullOrWhiteSpace(olcum.Label) ? oturum.Name : olcum.Label,
                Tur = SahaOlcumTuru.Reverberasyon,
                Durum = SahaOlcumDurumu.Taslak,
                Deger = orta.Average(),
                Birim = "s",
                BantFrekanslariHz = frekanslar.Take(bantlar.Length).ToList(),
                BantDegerleri = bantlar.ToList(),
                OlcumTarihi = olcum.Timestamp,
                Yontem = $"M07 {anahtar}",
                Cihaz = oturum.InputDeviceName ?? "",
                KaynakKayitId = olcum.Id,
                Notlar = "M07 oda akustiği oturumundan aktarıldı; mekan eşlemesi kullanıcı tarafından yapılmalıdır."
            });
            sonuc.Uyarilar.Add($"M07 '{olcum.Label}' kaydı mekana otomatik eşlenemedi.");
        }
        if (sonuc.Kayitlar.Count == 0 && sonuc.Hatalar.Count == 0)
            sonuc.Hatalar.Add("M07 oturumunda aktarılabilir RT ölçümü bulunamadı.");
    }

    private static bool RtParametresiBul(
        IReadOnlyDictionary<string, double[]> parametreler,
        out string anahtar,
        out double[] degerler)
    {
        foreach (var aday in new[] { "RT30", "RT20", "T60", "EDT" })
        {
            var bulunan = parametreler.FirstOrDefault(p =>
                string.Equals(p.Key, aday, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(bulunan.Key) && bulunan.Value is { Length: > 0 })
            {
                anahtar = bulunan.Key;
                degerler = bulunan.Value;
                return true;
            }
        }
        anahtar = "";
        degerler = [];
        return false;
    }

    private static double[] FrekanslariBelirle(int sayi) => sayi switch
    {
        16 => UcuncuOktavFrekanslari,
        7 => OktavFrekanslari.Skip(1).ToArray(),
        _ => OktavFrekanslari
    };

    private static SahaOlcumImportSonucu CsvOku(string csv)
    {
        var sonuc = new SahaOlcumImportSonucu();
        var satirlar = csv.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n').Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (satirlar.Count == 0)
        {
            sonuc.Hatalar.Add("CSV dosyası boş.");
            return sonuc;
        }

        try
        {
            char ayirac = M08Csv.AyiracBul(satirlar[0]);
            var basliklar = M08Csv.AlanlaraAyir(satirlar[0], ayirac)
                .Select((ad, i) => (Anahtar: M08Csv.Anahtarlastir(ad), Index: i))
                .GroupBy(x => x.Anahtar).ToDictionary(g => g.Key, g => g.First().Index);
            string Al(IReadOnlyList<string> alanlar, params string[] adlar)
            {
                foreach (var ad in adlar)
                    if (basliklar.TryGetValue(M08Csv.Anahtarlastir(ad), out int i) && i < alanlar.Count)
                        return alanlar[i].Trim();
                return "";
            }

            if (!basliklar.ContainsKey("tur") || !basliklar.ContainsKey("deger"))
            {
                sonuc.Hatalar.Add("CSV başlığında en az Tur ve Deger alanları bulunmalıdır.");
                return sonuc;
            }

            for (int satirNo = 1; satirNo < satirlar.Count; satirNo++)
            {
                var alanlar = M08Csv.AlanlaraAyir(satirlar[satirNo], ayirac);
                if (!TurOku(Al(alanlar, "Tur", "Ölçüm Türü"), out var tur))
                {
                    sonuc.Hatalar.Add($"Satır {satirNo + 1}: ölçüm türü geçersiz.");
                    continue;
                }
                if (!M08Csv.DoubleOku(Al(alanlar, "Deger", "Değer"), out var deger))
                {
                    sonuc.Hatalar.Add($"Satır {satirNo + 1}: ölçüm değeri sayı değil.");
                    continue;
                }

                var kayit = new SahaOlcumKaydi
                {
                    Tur = tur,
                    Ad = Al(alanlar, "Ad", "Ölçüm Adı"),
                    Deger = deger,
                    Birim = Al(alanlar, "Birim"),
                    KatKimligi = Al(alanlar, "KatKimligi", "Kat"),
                    Standart = Al(alanlar, "Standart"),
                    Yontem = Al(alanlar, "Yontem", "Yöntem"),
                    Cihaz = Al(alanlar, "Cihaz"),
                    Operator = Al(alanlar, "Operator", "Operatör"),
                    Notlar = Al(alanlar, "Notlar", "Not"),
                    KaynakKayitId = $"csv-{satirNo}"
                };
                if (string.IsNullOrWhiteSpace(kayit.Ad)) kayit.Ad = $"Ölçüm {satirNo}";
                if (string.IsNullOrWhiteSpace(kayit.Birim)) kayit.Birim = tur == SahaOlcumTuru.Reverberasyon ? "s" : "dB";
                if (M08Csv.DoubleOku(Al(alanlar, "Belirsizlik"), out var belirsizlik)) kayit.Belirsizlik = belirsizlik;
                if (Enum.TryParse<SahaOlcumDurumu>(Al(alanlar, "Durum"), true, out var durum)) kayit.Durum = durum;
                if (Guid.TryParse(Al(alanlar, "KaynakMekanId"), out var kaynakId)) kayit.KaynakMekanId = kaynakId;
                if (Guid.TryParse(Al(alanlar, "AliciMekanId", "AlıcıMekanId"), out var aliciId)) kayit.AliciMekanId = aliciId;
                if (Guid.TryParse(Al(alanlar, "BolmeId", "BölmeId"), out var bolmeId)) kayit.BolmeId = bolmeId;
                if (DateTime.TryParse(Al(alanlar, "OlcumTarihi", "ÖlçümTarihi"), CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeLocal, out var tarih) ||
                    DateTime.TryParse(Al(alanlar, "OlcumTarihi", "ÖlçümTarihi"), out tarih))
                    kayit.OlcumTarihi = tarih;
                kayit.BantFrekanslariHz = DiziOku(Al(alanlar, "BantFrekanslariHz"), sonuc, satirNo + 1);
                kayit.BantDegerleri = DiziOku(Al(alanlar, "BantDegerleri"), sonuc, satirNo + 1);
                kayit.RaporlamaIcinOnayli = BoolOku(Al(alanlar, "RaporlamaIcinOnayli"));
                sonuc.Kayitlar.Add(kayit);
            }
        }
        catch (InvalidDataException ex)
        {
            sonuc.Hatalar.Add(ex.Message);
        }
        Dogrula(sonuc);
        return sonuc;
    }

    private static List<double> DiziOku(string metin, SahaOlcumImportSonucu sonuc, int satirNo)
    {
        if (string.IsNullOrWhiteSpace(metin)) return new();
        var liste = new List<double>();
        foreach (var parca in metin.Split(['|', '/'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!M08Csv.DoubleOku(parca, out var deger))
                sonuc.Hatalar.Add($"Satır {satirNo}: bant değeri sayı değil ('{parca}').");
            else
                liste.Add(deger);
        }
        return liste;
    }

    private static bool TurOku(string metin, out SahaOlcumTuru tur)
    {
        if (Enum.TryParse(metin, true, out tur)) return true;
        tur = M08Csv.Anahtarlastir(metin) switch
        {
            "havasese" or "bolmehavasese" or "dntw" or "rw" => SahaOlcumTuru.BolmeHavaSesi,
            "discephe" or "discephehavasese" or "d2mntw" => SahaOlcumTuru.DisCepheHavaSesi,
            "darbesese" or "lntw" or "lnw" => SahaOlcumTuru.DarbeSesi,
            "reverberasyon" or "rt" or "rt60" => SahaOlcumTuru.Reverberasyon,
            "icgurultusurekli" => SahaOlcumTuru.IcGurultuSurekli,
            "icgurultukesikli" => SahaOlcumTuru.IcGurultuKesikli,
            "disgurultu" => SahaOlcumTuru.DisGurultu,
            _ => (SahaOlcumTuru)(-1)
        };
        return Enum.IsDefined(tur);
    }

    private static bool BoolOku(string metin) =>
        M08Csv.Anahtarlastir(metin) is "true" or "1" or "evet" or "yes";

    private static void Dogrula(SahaOlcumImportSonucu sonuc)
    {
        for (int i = 0; i < sonuc.Kayitlar.Count; i++)
        {
            var kayit = sonuc.Kayitlar[i];
            string onEk = $"Kayıt {i + 1}";
            if (!Enum.IsDefined(kayit.Tur)) sonuc.Hatalar.Add($"{onEk}: ölçüm türü geçersiz.");
            if (!double.IsFinite(kayit.Deger)) sonuc.Hatalar.Add($"{onEk}: ölçüm değeri sonlu olmalıdır.");
            if (kayit.Tur == SahaOlcumTuru.Reverberasyon && kayit.Deger <= 0)
                sonuc.Hatalar.Add($"{onEk}: reverberasyon süresi sıfırdan büyük olmalıdır.");
            if (kayit.Belirsizlik is { } u && (!double.IsFinite(u) || u < 0))
                sonuc.Hatalar.Add($"{onEk}: belirsizlik negatif olamaz.");
            kayit.BantFrekanslariHz ??= new();
            kayit.BantDegerleri ??= new();
            if (kayit.BantFrekanslariHz.Count != kayit.BantDegerleri.Count)
                sonuc.Hatalar.Add($"{onEk}: bant frekansı ve değeri sayıları eşit olmalıdır.");
            if (kayit.BantFrekanslariHz.Any(f => !double.IsFinite(f) || f <= 0) ||
                kayit.BantDegerleri.Any(v => !double.IsFinite(v)))
                sonuc.Hatalar.Add($"{onEk}: bant verilerinde geçersiz sayı var.");
        }
        sonuc.Hatalar = sonuc.Hatalar.Distinct(StringComparer.Ordinal).ToList();
    }

    private static bool OzellikVar(JsonElement nesne, string ad) => OzellikBul(nesne, ad, out _);

    private static bool OzellikBul(JsonElement nesne, string ad, out JsonElement deger)
    {
        foreach (var property in nesne.EnumerateObject())
        {
            if (string.Equals(property.Name, ad, StringComparison.OrdinalIgnoreCase))
            {
                deger = property.Value;
                return true;
            }
        }
        deger = default;
        return false;
    }
}
