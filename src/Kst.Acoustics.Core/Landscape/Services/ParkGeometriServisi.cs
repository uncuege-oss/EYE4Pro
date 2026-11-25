using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kst.Acoustics.Core.Landscape.Models;
using Kst.Acoustics.Core.Common;

namespace Kst.Acoustics.Core.Landscape.Services;

/// M70 F5 — park projesinin (`.e4p70`) kaydet/aç/migrasyon servisi ve paylaşılan
/// `.e4park.json` artifact üretimi. M50 `BinaGeometriServisi` / M60 `AraziGeometriServisi`
/// desenini izler. `ParkOgesi` polimorfiktir (`$tip` ayracı — bkz. `ParkOgesi`).
public static class ParkGeometriServisi
{
    /// Geçerli şema sürümü — migrasyon tetikleyicisi.
    public const int GuncelSemaSurum = 1;

    private static readonly JsonSerializerOptions JsonAyar = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// Park projesini kaydet (`.e4p70`). `GeometriRevizyon` her kayıtta artırılır
    /// (tüketici fark uyarısı için).
    public static void Kaydet(ParkModeli park, string dosyaYolu)
    {
        if (park.SemaSurum > GuncelSemaSurum)
            throw new InvalidDataException("Park dosyası sürümü bu uygulamadan daha yeni.");
        var onceki = (park.GeometriRevizyon, park.DosyaYolu);
        try
        {
            park.GeometriRevizyon = checked(park.GeometriRevizyon + 1);
            park.DosyaYolu = dosyaYolu;
            GuvenliDosyaYazmaServisi.Yaz(dosyaYolu, JsonSerializer.Serialize(park, JsonAyar));
        }
        catch
        {
            (park.GeometriRevizyon, park.DosyaYolu) = onceki;
            throw;
        }
    }

    /// Park projesini dosyadan yükle ve gerekiyorsa migrasyon uygula.
    public static ParkModeli Ac(string dosyaYolu)
    {
        if (!File.Exists(dosyaYolu))
            throw new FileNotFoundException($"Park dosyası bulunamadı: {dosyaYolu}");

        var park = JsonSerializer.Deserialize<ParkModeli>(File.ReadAllText(dosyaYolu), JsonAyar)
                   ?? throw new InvalidDataException("Dosya okunamadı veya boş.");
        park.DosyaYolu = dosyaYolu;
        Migrate(park);
        return park;
    }

    /// Paylaşılan `.e4park.json` artifact'ını yazar (M71/M15 salt-okunur tüketir).
    /// Bu bir DIŞA AKTARMADIR — mevcut durumun kopyasıdır, `GeometriRevizyon`'u
    /// ARTIRMAZ (revizyon yalnız proje kayıtlarında artar).
    public static void ArtifactYaz(ParkModeli park, string dosyaYolu)
        => GuvenliDosyaYazmaServisi.Yaz(dosyaYolu, JsonSerializer.Serialize(park, JsonAyar));

    /// Yeni boş park modeli oluşturur.
    public static ParkModeli YeniOlustur(string parkAdi = "")
        => new()
        {
            ParkAdi          = parkAdi,
            SemaSurum        = GuncelSemaSurum,
            GeometriRevizyon = 0
        };

    // ── Migrasyon ─────────────────────────────────────────────────────────────

    private static void Migrate(ParkModeli park)
    {
        if (park.SemaSurum > GuncelSemaSurum)
            throw new InvalidDataException("Park dosyası sürümü bu uygulamadan daha yeni.");
        if (park.SemaSurum >= GuncelSemaSurum) return;

        // Sürüm 0 → 1: ilk resmi şema; özel dönüşüm gerekmez.
        park.SemaSurum = GuncelSemaSurum;
    }
}
