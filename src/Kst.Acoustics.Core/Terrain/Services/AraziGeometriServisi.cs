using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kst.Acoustics.Core.Terrain.Models;

namespace Kst.Acoustics.Core.Terrain.Services;

/// `.e4arazi.json` paylaşılan arazi artifact'ının kaydet / aç / migrasyon servisi.
/// M50 `BinaGeometriServisi` deseninin arazi karşılığı. Tüketiciler (M61, M15, mimari)
/// bu dosyayı salt-okunur yükler; üretici M60'tır.
public static class AraziGeometriServisi
{
    /// Geçerli şema sürümü — migrasyon tetikleyicisi.
    public const int GuncelSemaSurum = 1;

    private static readonly JsonSerializerOptions JsonAyar = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// Araziyi kaydet. `GeometriRevizyon` her kayıtta artırılır (tüketici fark uyarısı için).
    public static void Kaydet(AraziModeli arazi, string dosyaYolu)
    {
        arazi.GeometriRevizyon++;
        arazi.DosyaYolu = dosyaYolu;
        var json = JsonSerializer.Serialize(arazi, JsonAyar);
        File.WriteAllText(dosyaYolu, json);
    }

    /// Dosyadan yükle ve gerekiyorsa migrasyon uygula. TIN ve hafriyat sonucu serileştirilmez;
    /// yüklemeden sonra `AraziYuzeyServisi`/`HafriyatHesabiServisi` ile yeniden hesaplanır.
    public static AraziModeli Ac(string dosyaYolu)
    {
        if (!File.Exists(dosyaYolu))
            throw new FileNotFoundException($"Arazi dosyası bulunamadı: {dosyaYolu}");

        var json = File.ReadAllText(dosyaYolu);
        var arazi = JsonSerializer.Deserialize<AraziModeli>(json, JsonAyar)
                    ?? throw new InvalidDataException("Dosya okunamadı veya boş.");
        arazi.DosyaYolu = dosyaYolu;
        Migrate(arazi);
        return arazi;
    }

    /// Yeni boş arazi modeli oluşturur.
    public static AraziModeli YeniOlustur(string araziAdi = "")
        => new()
        {
            AraziAdi         = araziAdi,
            SemaSurum        = GuncelSemaSurum,
            GeometriRevizyon = 0
        };

    // ── Migrasyon ─────────────────────────────────────────────────────────────

    private static void Migrate(AraziModeli arazi)
    {
        if (arazi.SemaSurum >= GuncelSemaSurum) return;

        // Sürüm 0 → 1: ilk resmi şema; özel dönüşüm gerekmez.
        arazi.SemaSurum = GuncelSemaSurum;
    }
}
