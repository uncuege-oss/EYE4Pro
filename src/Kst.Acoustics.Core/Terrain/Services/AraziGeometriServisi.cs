using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kst.Acoustics.Core.Terrain.Models;
using Kst.Acoustics.Core.Common;

namespace Kst.Acoustics.Core.Terrain.Services;

/// `.e4arazi.json` paylaşılan arazi artifact'ının kaydet / aç / migrasyon servisi.
/// M50 `BinaGeometriServisi` deseninin arazi karşılığı. Tüketiciler (M61, M15, mimari)
/// bu dosyayı salt-okunur yükler; üretici M60'tır.
public static class AraziGeometriServisi
{
    /// Geçerli şema sürümü — migrasyon tetikleyicisi.
    public const int GuncelSemaSurum = 3;

    private static readonly JsonSerializerOptions JsonAyar = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// Araziyi kaydet. `GeometriRevizyon` her kayıtta artırılır (tüketici fark uyarısı için).
    public static void Kaydet(AraziModeli arazi, string dosyaYolu)
    {
        if (arazi.SemaSurum > GuncelSemaSurum)
            throw new InvalidDataException("Arazi dosyası sürümü bu uygulamadan daha yeni.");
        var onceki = (arazi.GeometriRevizyon, arazi.DosyaYolu, arazi.SemaSurum);
        try
        {
            // Eski uygulamaların şev alanlarını sessizce düz platform gibi okumasını engeller.
            if (arazi.KaynakUcgenleri != null) arazi.SemaSurum = 3;
            else if (arazi.Tasarim.Bolgeler.Any(b => b.KenarGecisi == TasarimKenarTipi.Sev || b.KenarAyarlari.Count > 0))
                arazi.SemaSurum = Math.Max(arazi.SemaSurum, 2);
            arazi.GeometriRevizyon = checked(arazi.GeometriRevizyon + 1);
            arazi.DosyaYolu = dosyaYolu;
            GuvenliDosyaYazmaServisi.Yaz(dosyaYolu, JsonSerializer.Serialize(arazi, JsonAyar));
        }
        catch
        {
            (arazi.GeometriRevizyon, arazi.DosyaYolu, arazi.SemaSurum) = onceki;
            throw;
        }
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
        if (arazi.SemaSurum > GuncelSemaSurum)
            throw new InvalidDataException("Arazi dosyası sürümü bu uygulamadan daha yeni.");
        // Sürüm 1 alanları yeni modelin varsayılanlarıyla uyumludur; mevcut araziyi sırf açarken yükseltme.
        if (arazi.SemaSurum < 1) arazi.SemaSurum = 1;
    }
}
