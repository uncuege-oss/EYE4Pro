using System.Text.Json;
using System.Text.Json.Serialization;
using Kst.Acoustics.Core.Common;

namespace Kst.Acoustics.Core.Electric.KesifHazirla;

public static class KesifJsonServisi
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static void Kaydet(KesifProje proje, string path)
    {
        GuvenliDosyaYazmaServisi.Yaz(path, JsonOlustur(proje));
    }

    /// <summary>
    /// Proje grafiğini çağıran thread'de JSON'a dönüştürür, yalnız dosya yazımını asenkron yapar.
    /// Avalonia'ya bağlı ObservableCollection nesnelerinin arka plan thread'inden okunmasını önler.
    /// </summary>
    public static async Task KaydetAsync(KesifProje proje, string path, CancellationToken cancellationToken = default)
    {
        var json = JsonOlustur(proje);
        await GuvenliDosyaYazmaServisi.YazAsync(path, json, cancellationToken);
    }

    /// <summary>Kaydedilen proje içeriğinin değişip değişmediğini yan etkisiz karşılaştırmak için kullanılır.</summary>
    public static string ParmakIziOlustur(KesifProje proje)
        => JsonSerializer.Serialize(proje, Options);

    private static string JsonOlustur(KesifProje proje)
    {
        proje.GuncellemeTarihi = DateTime.Now;

        // PozKutuphane [JsonIgnore]; manuel pozları (liste-dışı) kalıcı listeye senkronla.
        // Liste pozları zaten BirimFiyatListeleri içinde yazıldığından burada tekrar yazılmaz.
        var manualPozlar = proje.ManuelPozlar.Count > 0
            ? proje.ManuelPozlar.Where(p => !p.ListePozuMu).DistinctBy(p => p.Id).ToList()
            : proje.PozKutuphane.Where(p => !p.ListePozuMu).DistinctBy(p => p.Id).ToList();
        proje.ManuelPozlar.Clear();
        foreach (var poz in manualPozlar)
            proje.ManuelPozlar.Add(poz);

        return JsonSerializer.Serialize(proje, Options);
    }

    public static KesifProje Ac(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<KesifProje>(json, Options)
               ?? throw new InvalidOperationException("Keşif dosyası okunamadı.");
    }
}
