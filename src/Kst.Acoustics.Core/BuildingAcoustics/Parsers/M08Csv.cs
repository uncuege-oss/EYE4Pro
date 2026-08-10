using System.Globalization;
using System.Text;

namespace Kst.Acoustics.Core.BuildingAcoustics.Parsers;

/// <summary>M08'e özgü, bağımlılıksız ve tırnaklı alanları destekleyen CSV yardımcıları.</summary>
internal static class M08Csv
{
    public static char AyiracBul(string baslik)
    {
        int noktalivirgul = AlanlaraAyir(baslik, ';').Count;
        int virgul = AlanlaraAyir(baslik, ',').Count;
        return noktalivirgul >= virgul ? ';' : ',';
    }

    public static List<string> AlanlaraAyir(string satir, char ayirac)
    {
        var sonuc = new List<string>();
        var alan = new StringBuilder();
        bool tirnakIcinde = false;
        for (int i = 0; i < satir.Length; i++)
        {
            char c = satir[i];
            if (c == '"')
            {
                if (tirnakIcinde && i + 1 < satir.Length && satir[i + 1] == '"')
                {
                    alan.Append('"');
                    i++;
                }
                else
                {
                    tirnakIcinde = !tirnakIcinde;
                }
            }
            else if (c == ayirac && !tirnakIcinde)
            {
                sonuc.Add(alan.ToString().Trim());
                alan.Clear();
            }
            else
            {
                alan.Append(c);
            }
        }

        if (tirnakIcinde)
            throw new InvalidDataException("CSV satırında kapanmamış tırnak bulundu.");
        sonuc.Add(alan.ToString().Trim());
        return sonuc;
    }

    public static string Kacisla(string? deger, char ayirac = ';')
    {
        deger ??= "";
        return deger.IndexOfAny([ayirac, '"', '\r', '\n']) < 0
            ? deger
            : $"\"{deger.Replace("\"", "\"\"")}\"";
    }

    public static string Anahtarlastir(string? deger)
    {
        if (string.IsNullOrWhiteSpace(deger))
            return "";
        var sb = new StringBuilder();
        foreach (var c in deger.Trim().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(c))
                sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    public static bool DoubleOku(string? metin, out double deger)
    {
        metin = metin?.Trim();
        return double.TryParse(metin, NumberStyles.Float, CultureInfo.InvariantCulture, out deger) ||
               double.TryParse(metin, NumberStyles.Float, CultureInfo.GetCultureInfo("tr-TR"), out deger) ||
               double.TryParse(metin, NumberStyles.Float, CultureInfo.CurrentCulture, out deger);
    }

    public static bool DecimalOku(string? metin, out decimal deger)
    {
        metin = metin?.Trim();
        return decimal.TryParse(metin, NumberStyles.Number, CultureInfo.InvariantCulture, out deger) ||
               decimal.TryParse(metin, NumberStyles.Number, CultureInfo.GetCultureInfo("tr-TR"), out deger) ||
               decimal.TryParse(metin, NumberStyles.Number, CultureInfo.CurrentCulture, out deger);
    }

    public static void AtomikYaz(string yol, string icerik)
    {
        string tamYol = Path.GetFullPath(yol);
        string klasor = Path.GetDirectoryName(tamYol)
                         ?? throw new IOException($"Hedef klasör belirlenemedi: {yol}");
        Directory.CreateDirectory(klasor);
        string gecici = Path.Combine(klasor, $".{Path.GetFileName(tamYol)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(gecici, icerik, new UTF8Encoding(false));
            File.Move(gecici, tamYol, overwrite: true);
        }
        finally
        {
            if (File.Exists(gecici))
                File.Delete(gecici);
        }
    }
}
