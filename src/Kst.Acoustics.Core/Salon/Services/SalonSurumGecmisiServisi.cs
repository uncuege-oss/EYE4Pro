using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kst.Acoustics.Core.Common;
using Kst.Acoustics.Core.Salon.Models;

namespace Kst.Acoustics.Core.Salon.Services;

public sealed record SalonSurumBilgisi(
    string DosyaYolu,
    DateTime KayitZamaniUtc,
    string SalonAdi,
    string? KaynakDosyaYolu,
    int GeometriRevizyon,
    string IcerikOzeti,
    bool OtomatikKayit);

public sealed record SalonKurtarmaAdayi(
    SalonSurumBilgisi Surum,
    bool KaynaktanDahaYeni,
    TimeSpan Yas);

/// <summary>
/// Ana dosyayı ve revizyon sayacını değiştirmeden atomik otomatik kayıt, kurtarma
/// ve adlandırılmış sürüm geçmişi üretir.
/// </summary>
public static class SalonSurumGecmisiServisi
{
    private const string MetaUzanti = ".meta.json";

    public static string VarsayilanKlasor => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EYE4Pro", "M51", "AutoSave");

    public static SalonSurumBilgisi OtomatikKaydet(
        SalonGeometri salon, string klasor, int azamiSurum = 20, DateTime? simdiUtc = null)
        => SurumKaydet(salon, klasor, otomatik: true, azamiSurum, simdiUtc);

    public static SalonSurumBilgisi AnlikSurumKaydet(
        SalonGeometri salon, string klasor, int azamiSurum = 50, DateTime? simdiUtc = null)
        => SurumKaydet(salon, klasor, otomatik: false, azamiSurum, simdiUtc);

    public static IReadOnlyList<SalonSurumBilgisi> Listele(string klasor)
    {
        if (string.IsNullOrWhiteSpace(klasor) || !Directory.Exists(klasor)) return [];
        var r = new List<SalonSurumBilgisi>();
        foreach (var meta in Directory.EnumerateFiles(klasor, "*" + MetaUzanti))
        {
            try
            {
                var x = JsonSerializer.Deserialize<SalonSurumBilgisi>(File.ReadAllText(meta));
                if (x is not null && File.Exists(x.DosyaYolu)) r.Add(x);
            }
            catch (JsonException) { /* tek bozuk metadata diğer kurtarma adaylarını gizlememeli */ }
            catch (IOException) { /* eşzamanlı temizleme/listeme yarışı */ }
        }
        return r.OrderByDescending(x => x.KayitZamaniUtc).ToList();
    }

    public static IReadOnlyList<SalonKurtarmaAdayi> KurtarmaAdaylari(
        string klasor, DateTime? simdiUtc = null)
    {
        var now = simdiUtc ?? DateTime.UtcNow;
        return Listele(klasor).Where(x => x.OtomatikKayit).Select(x =>
        {
            DateTime kaynak = x.KaynakDosyaYolu is { Length: > 0 } p && File.Exists(p)
                ? File.GetLastWriteTimeUtc(p)
                : DateTime.MinValue;
            return new SalonKurtarmaAdayi(x, x.KayitZamaniUtc > kaynak, now - x.KayitZamaniUtc);
        }).Where(x => x.KaynaktanDahaYeni).OrderByDescending(x => x.Surum.KayitZamaniUtc).ToList();
    }

    public static SalonGeometri GeriYukle(SalonSurumBilgisi surum)
    {
        ArgumentNullException.ThrowIfNull(surum);
        if (!File.Exists(surum.DosyaYolu))
            throw new FileNotFoundException("Salon sürüm dosyası bulunamadı.", surum.DosyaYolu);
        return SalonGeometriServisi.KopyadanYukle(File.ReadAllText(surum.DosyaYolu));
    }

    private static SalonSurumBilgisi SurumKaydet(
        SalonGeometri salon, string klasor, bool otomatik, int azamiSurum, DateTime? simdiUtc)
    {
        ArgumentNullException.ThrowIfNull(salon);
        ArgumentException.ThrowIfNullOrWhiteSpace(klasor);
        Directory.CreateDirectory(klasor);
        DateTime now = (simdiUtc ?? DateTime.UtcNow).ToUniversalTime();
        string json = SalonGeometriServisi.KopyaJson(salon);
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant()[..12];
        string tur = otomatik ? "autosave" : "version";
        string ad = $"{tur}-{now:yyyyMMdd-HHmmss-fffffff}-{hash}.e4salon.json";
        string yol = Path.Combine(Path.GetFullPath(klasor), ad);
        GuvenliDosyaYazmaServisi.Yaz(yol, json);
        var bilgi = new SalonSurumBilgisi(
            yol, now, string.IsNullOrWhiteSpace(salon.SalonAdi) ? "Adsız Salon" : salon.SalonAdi,
            salon.DosyaYolu, salon.GeometriRevizyon, hash, otomatik);
        GuvenliDosyaYazmaServisi.Yaz(yol + MetaUzanti, JsonSerializer.Serialize(bilgi));
        Temizle(klasor, tur, Math.Clamp(azamiSurum, 1, 500));
        return bilgi;
    }

    private static void Temizle(string klasor, string tur, int azami)
    {
        var metas = Directory.EnumerateFiles(klasor, tur + "-*" + MetaUzanti)
            .Select(p => new FileInfo(p)).OrderByDescending(x => x.LastWriteTimeUtc).ToList();
        foreach (var m in metas.Skip(azami))
        {
            string veri = m.FullName[..^MetaUzanti.Length];
            try { if (File.Exists(veri)) File.Delete(veri); } catch (IOException) { }
            try { m.Delete(); } catch (IOException) { }
        }
    }
}
