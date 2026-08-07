using Kst.Acoustics.Core.BuildingAcoustics.Models;
using System;
using System.IO;
using System.Text.Json;

namespace Kst.Acoustics.Core.BuildingAcoustics.Parsers;

/// <summary>
/// Deserialize edilmiş bir .e4p08 JSON'unu (eski/eksik alanlar dahil) güvenli varsayılanlarla
/// tamamlar. Eski JSON'larda bazı liste alanları açık `null` olarak yazılmış olabilir
/// (System.Text.Json bu durumda property'ye null atar, koleksiyon initializer'ı devreye girmez);
/// bu, ilerleyen kodda NullReferenceException'a yol açar. ProjeAc, deserialize edilen projeyi
/// doğrudan (Proje = yuklenen) atamadan önce bunu çağırmalıdır.
/// </summary>
public static class BinaProjeMigrator
{
    /// <summary>
    /// Bir dosyayı deserialize etmeden önce M08 belge kimliğini denetler. Eski .e4p08
    /// belgelerinde ModuleId bulunmadığı için yalnız doğru uzantı ve ayırt edici kök alanlar
    /// birlikte mevcutsa geriye uyumlu olarak kabul edilir. Genel *.json dosyaları kabul edilmez.
    /// </summary>
    public static bool DosyaKimliginiDogrula(string json, string dosyaYolu, out string hata)
    {
        hata = "";
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                hata = "M08 proje belgesinin kökü bir JSON nesnesi olmalıdır.";
                return false;
            }

            var root = doc.RootElement;
            static bool JsonListeMi(JsonElement element) =>
                element.ValueKind == JsonValueKind.Array ||
                (element.ValueKind == JsonValueKind.Object &&
                 element.TryGetProperty("$values", out var values) &&
                 values.ValueKind == JsonValueKind.Array);

            if (root.TryGetProperty(nameof(BinaProje.ModuleId), out var moduleElement))
            {
                if (moduleElement.ValueKind != JsonValueKind.String)
                {
                    hata = "M08 belge kimliği metin biçiminde olmalıdır.";
                    return false;
                }
                var moduleId = moduleElement.GetString();
                if (!string.Equals(moduleId, BinaProje.CurrentModuleId, StringComparison.OrdinalIgnoreCase))
                {
                    hata = $"Bu dosya M08'e ait değil (ModuleId: {moduleId ?? "yok"}).";
                    return false;
                }
            }
            else
            {
                // ModuleId'siz yalnız eski, özgün .e4p08 belgelerine izin ver. Böylece
                // .e4bina.json veya başka modüllerin kısmen benzer JSON'ları açılmaz.
                if (!string.Equals(Path.GetExtension(dosyaYolu), ".e4p08", StringComparison.OrdinalIgnoreCase) ||
                    !root.TryGetProperty(nameof(BinaProje.Katlar), out var katlar) ||
                    !JsonListeMi(katlar) ||
                    !root.TryGetProperty(nameof(BinaProje.ProjeAdi), out var projeAdi) ||
                    projeAdi.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
                {
                    hata = "Dosyada M08 belge kimliği yok. Yalnız eski .e4p08 belgeleri dönüştürülebilir.";
                    return false;
                }
            }

            if (root.TryGetProperty(nameof(BinaProje.SchemaVersion), out var schemaElement))
            {
                if (schemaElement.ValueKind != JsonValueKind.Number ||
                    !schemaElement.TryGetInt32(out var schemaVersion) || schemaVersion <= 0)
                {
                    hata = "M08 şema sürümü pozitif bir tam sayı olmalıdır.";
                    return false;
                }
                if (schemaVersion > BinaProje.CurrentSchemaVersion)
                {
                    hata = $"Proje şeması ({schemaVersion}) bu uygulamanın desteklediği sürümden " +
                           $"({BinaProje.CurrentSchemaVersion}) daha yeni.";
                    return false;
                }
            }

            return true;
        }
        catch (JsonException ex)
        {
            hata = $"Geçersiz JSON: {ex.Message}";
            return false;
        }
    }

    public static void Normalize(BinaProje proje)
    {
        // ModuleId doğrulaması deserialize öncesinde yapılır. Normalize hiçbir zaman başka
        // bir modül kimliğini M08'e çevirmez; yalnız kimliksiz eski belgeyi yükseltir.
        if (string.IsNullOrWhiteSpace(proje.ModuleId))
            proje.ModuleId = BinaProje.CurrentModuleId;
        if (proje.SchemaVersion <= 0)
            proje.SchemaVersion = 1;
        proje.ProjeAdi ??= "";
        proje.Adres ??= "";
        proje.MusteriAdi ??= "";
        proje.SorumluKisiKurulus ??= "";
        proje.DisGurultuThirdOctave ??= new();
        proje.BagimsizBolumler ??= new();
        proje.DuseyMekanBirlesimleri ??= new();
        proje.YapiElemaniKutuphanesi ??= new();
        proje.AkustikNoktalar ??= new();
        proje.DisGurultuKaynaklari ??= new();
        proje.Katlar ??= new();

        if (proje.BinaIslevi == BinaIslevi.Tanimsiz)
            proje.BinaIslevi = BinaIslevi.Konut;
        if (proje.DisGurultuDb <= 0)
            proje.DisGurultuDb = 65.0;

        foreach (var kat in proje.Katlar)
            NormalizeKat(kat);

        // Bellekteki belge artık güncel biçimde kaydedilir.
        proje.SchemaVersion = BinaProje.CurrentSchemaVersion;
    }

    private static void NormalizeKat(Kat kat)
    {
        kat.Ad ??= "";
        kat.DxfFilePath ??= "";
        kat.Mekanlar ??= new();

        foreach (var mekan in kat.Mekanlar)
            NormalizeMekan(mekan);
    }

    private static void NormalizeMekan(Mekan mekan)
    {
        mekan.Ad ??= "";
        mekan.KullanimTipi ??= "";
        mekan.KaynakDerecesi ??= "";
        mekan.AliciSinifi ??= "";
        mekan.Poligon ??= new();
        // Mekan.Bolmeler setter zaten null-guard'lı (value ?? new ObservableCollection<Bolme>()).

        foreach (var bolme in mekan.Bolmeler)
            NormalizeBolme(bolme);
    }

    private static void NormalizeBolme(Bolme bolme)
    {
        bolme.Ad ??= "";
        bolme.Acikliklar ??= new();
        bolme.Menfezler ??= new();
        bolme.Junctions ??= new();
    }
}
