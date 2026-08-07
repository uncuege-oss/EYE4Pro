using System;
using System.Text;

namespace Kst.Acoustics.Core.Electric.CevreAydinlatma;

/// <summary>
/// DXF katman adından yüzey kategorisi tahmin eder (büyük/küçük harf ve Türkçe karakter duyarsız).
/// Otomatik atama best-effort'tur; kullanıcı yüzey ekranında elle düzeltebilir.
/// </summary>
public static class YuzeyKategoriEsleyici
{
    public static YuzeyKategori Esle(string? katmanAdi)
    {
        if (string.IsNullOrWhiteSpace(katmanAdi))
            return YuzeyKategori.Diger;

        string n = Normalize(katmanAdi);

        if (Iceriyor(n, "yesil", "green", "cim", "peyzaj", "bitki"))
            return YuzeyKategori.YesilAlan;
        if (Iceriyor(n, "spor", "sport", "saha", "oyun", "basket", "futbol", "tenis"))
            return YuzeyKategori.SporAlani;
        if (Iceriyor(n, "yol", "road", "kaldirim", "yaya", "patika", "path", "walk"))
            return YuzeyKategori.Yol;
        if (Iceriyor(n, "bina", "build", "yapi", "kulube", "tuvalet", "wc"))
            return YuzeyKategori.Bina;

        return YuzeyKategori.Diger;
    }

    private static bool Iceriyor(string kaynak, params string[] anahtarlar)
    {
        foreach (var a in anahtarlar)
            if (kaynak.Contains(a, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static string Normalize(string s)
    {
        var lowered = s.Replace('İ', 'i').ToLowerInvariant();
        var sb = new StringBuilder(lowered.Length);
        foreach (var c in lowered)
            sb.Append(c switch
            {
                'ı' => 'i',
                'ş' => 's',
                'ğ' => 'g',
                'ü' => 'u',
                'ö' => 'o',
                'ç' => 'c',
                'â' => 'a',
                'î' => 'i',
                'û' => 'u',
                _ => c
            });
        return sb.ToString();
    }
}
