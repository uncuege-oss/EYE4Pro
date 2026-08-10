using System.Security.Cryptography;
using Kst.Acoustics.Core.BuildingAcoustics.Models;

namespace Kst.Acoustics.Core.BuildingAcoustics.Database;

/// <summary>
/// Yapı elemanı teknik belgelerini kullanıcı veri alanında yönetir. Kaynak belge yerinde
/// bırakılır; yönetimli kopya atomik yazılır ve SHA-256 özeti katalog kaydına eklenir.
/// </summary>
public static class UrunBelgesiServisi
{
    public static string VarsayilanKokDizin => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EYE4PRO", "BuildingAcoustics", "Documents");

    public static UrunBelgesi Ekle(
        YapiElemani eleman,
        string kaynakYol,
        UrunBelgesiTuru tur = UrunBelgesiTuru.TeknikFoy,
        string? kokDizin = null)
    {
        ArgumentNullException.ThrowIfNull(eleman);
        if (string.IsNullOrWhiteSpace(kaynakYol) || !File.Exists(kaynakYol))
            throw new FileNotFoundException("Ürün belgesi bulunamadı.", kaynakYol);

        if (eleman.Id == Guid.Empty)
            eleman.Id = M08KararliKimlik.YapiElemani(eleman);

        kokDizin ??= VarsayilanKokDizin;
        var hedefDizin = Path.Combine(kokDizin, eleman.Id.ToString("N"));
        Directory.CreateDirectory(hedefDizin);

        var dosyaAdi = GuvenliDosyaAdi(Path.GetFileName(kaynakYol));
        var hedefYol = Path.Combine(hedefDizin, dosyaAdi);
        var kaynakOzet = Sha256(kaynakYol);

        // Aynı adlı fakat farklı içerikli dosya önceki belgeyi sessizce ezmesin.
        if (File.Exists(hedefYol) && !string.Equals(Sha256(hedefYol), kaynakOzet, StringComparison.OrdinalIgnoreCase))
        {
            var ad = Path.GetFileNameWithoutExtension(dosyaAdi);
            var uzanti = Path.GetExtension(dosyaAdi);
            hedefYol = Path.Combine(hedefDizin, $"{ad}-{kaynakOzet[..8]}{uzanti}");
        }

        var gecici = hedefYol + ".tmp";
        try
        {
            File.Copy(kaynakYol, gecici, overwrite: true);
            File.Move(gecici, hedefYol, overwrite: true);
        }
        finally
        {
            if (File.Exists(gecici))
                try { File.Delete(gecici); } catch { /* en iyi çaba */ }
        }

        var belge = new UrunBelgesi
        {
            Ad = Path.GetFileNameWithoutExtension(kaynakYol),
            Tur = tur,
            DosyaYolu = hedefYol,
            Sha256 = kaynakOzet,
            MimeType = MimeTuru(Path.GetExtension(kaynakYol)),
            YayinTarihi = File.GetLastWriteTime(kaynakYol),
        };
        eleman.Belgeler ??= new();
        eleman.Belgeler.Add(belge);
        return belge;
    }

    public static bool Dogrula(UrunBelgesi belge, out string aciklama)
    {
        ArgumentNullException.ThrowIfNull(belge);
        if (string.IsNullOrWhiteSpace(belge.DosyaYolu) || !File.Exists(belge.DosyaYolu))
        {
            aciklama = "Belge dosyası bulunamadı.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(belge.Sha256))
        {
            aciklama = "Belgenin kayıtlı SHA-256 özeti yok.";
            return false;
        }
        var guncel = Sha256(belge.DosyaYolu);
        var gecerli = string.Equals(guncel, belge.Sha256, StringComparison.OrdinalIgnoreCase);
        aciklama = gecerli ? "Belge bütünlüğü doğrulandı." : "Belge içeriği kayıt sonrasında değişmiş.";
        return gecerli;
    }

    public static string Sha256(string yol)
    {
        using var akis = File.OpenRead(yol);
        return Convert.ToHexString(SHA256.HashData(akis));
    }

    private static string GuvenliDosyaAdi(string ad)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) ad = ad.Replace(c, '_');
        return string.IsNullOrWhiteSpace(ad) ? "belge.bin" : ad;
    }

    private static string MimeTuru(string uzanti) => uzanti.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls" => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        _ => "application/octet-stream",
    };
}
