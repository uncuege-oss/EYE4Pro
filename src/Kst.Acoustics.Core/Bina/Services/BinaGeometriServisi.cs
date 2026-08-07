using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kst.Acoustics.Core.Bina.Models;

namespace Kst.Acoustics.Core.Bina.Services;

/// `.e4bina.json` dosyasının kaydet / aç / migrasyon servisi.
public static class BinaGeometriServisi
{
    /// Geçerli şema sürümü — migrasyon tetikleyicisi.
    /// 2: düşey birleşim üyelerine kararlı KatId eklendi (kat sıralama dayanıklılığı).
    public const int GuncelSemaSurum = 2;

    private static readonly JsonSerializerOptions JsonAyar = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// Geometriyi kaydet. `GeometriRevizyon` yalnız yazım BAŞARILI olduktan sonra artırılır
    /// (aksi halde disk hatasında bellek revizyonu dosyadakinden ileri kalır ve bir sonraki
    /// başarılı kayıt damgayı atlar). Atomik yazım: önce `.tmp`'ye yazılır, sonra hedefin
    /// üzerine taşınır — yarıda kalan yazım mevcut `.e4bina`'yı bozmaz.
    public static void Kaydet(BinaGeometri geo, string dosyaYolu)
    {
        int yeniRevizyon = geo.GeometriRevizyon + 1;
        string eskiDosyaYolu = geo.DosyaYolu ?? "";
        geo.DosyaYolu = dosyaYolu;
        geo.GeometriRevizyon = yeniRevizyon;
        try
        {
            var json = JsonSerializer.Serialize(geo, JsonAyar);
            var tmp = dosyaYolu + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, dosyaYolu, overwrite: true);
        }
        catch
        {
            geo.GeometriRevizyon = yeniRevizyon - 1;
            geo.DosyaYolu = eskiDosyaYolu;
            throw;
        }
    }

    /// Dosyadan yükle ve gerekiyorsa migrasyon uygula.
    /// Yükleme sonrası `[JsonIgnore]` olan `HamSegmentler`/`HamMetinler` boş kalır;
    /// DXF arka plan segmentleri `DxfSegmentler`'den (serileştirilmiş) alınır.
    public static BinaGeometri Ac(string dosyaYolu)
    {
        if (!File.Exists(dosyaYolu))
            throw new FileNotFoundException($"Bina geometrisi dosyası bulunamadı: {dosyaYolu}");

        var json = File.ReadAllText(dosyaYolu);
        var geo  = JsonSerializer.Deserialize<BinaGeometri>(json, JsonAyar)
                   ?? throw new InvalidDataException("Dosya okunamadı veya boş.");
        geo.DosyaYolu = dosyaYolu;
        Migrate(geo);
        return geo;
    }

    /// Yeni boş geometri oluşturur.
    public static BinaGeometri YeniOlustur(string binaAdi = "")
        => new()
        {
            BinaAdi          = binaAdi,
            SemaSurum        = GuncelSemaSurum,
            GeometriRevizyon = 0
        };

    // ── Migrasyon ─────────────────────────────────────────────────────────────

    private static void Migrate(BinaGeometri geo)
    {
        if (geo.SemaSurum >= GuncelSemaSurum) return;

        // Sürüm 0 → 1: ilk resmi şema; özel dönüşüm gerekmez.

        // Sürüm 1 → 2: düşey birleşim üyelerine kararlı KatId doldur (KatNo eşlemesinden).
        if (geo.SemaSurum < 2)
            BinaDuseyBirlesimServisi.KatIdleriTamamla(geo);

        geo.SemaSurum = GuncelSemaSurum;
    }
}
