using System;
using System.Globalization;

namespace Kst.Acoustics.Core.Electric.Calculation;

/// <summary>
/// Sayı ayrıştırma yardımcısı — hem nokta (.) hem virgül (,) ondalık ayraçını kabul eder.
/// </summary>
public static class NumberHelper
{
    /// <summary>Hem . hem , kabul ederek double parse eder. Başarısızsa default değeri döner.</summary>
    public static double Parse(string? text, double defaultValue = 0)
    {
        if (string.IsNullOrWhiteSpace(text)) return defaultValue;
        var s = text.Trim().Replace(',', '.');
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v : defaultValue;
    }

    /// <summary>TryParse versiyonu.</summary>
    public static bool TryParse(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var s = text.Trim().Replace(',', '.');
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>int için TryParse.</summary>
    public static bool TryParseInt(string? text, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        return int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Güç faktörünü hesaplarda güvenli 0..1 aralığına taşır.</summary>
    public static double NormalizeCosF(double cosF, double defaultValue = 1.0)
    {
        if (double.IsNaN(cosF) || double.IsInfinity(cosF) || cosF <= 0)
            return Math.Clamp(defaultValue, 0.0001, 1.0);

        return Math.Clamp(cosF, 0.0001, 1.0);
    }
}
