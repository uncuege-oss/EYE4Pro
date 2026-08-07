using System.Globalization;
using Avalonia.Data.Converters;

namespace Kst.Acoustics.App.Converters;

/// <summary>
/// Eski durum iletilerindeki platforma bağlı emoji gliflerini ve teknik İngilizce
/// görünüm terimlerini M01 arayüzünde güvenli, Türkçe metne dönüştürür.
/// </summary>
public sealed class M01UserFacingTextConverter : IValueConverter
{
    private static readonly string[] UnsupportedGlyphs = ["🎭", "📍", "⚠", "✔", "✅", "❌"];

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value?.ToString() ?? string.Empty;

        foreach (var glyph in UnsupportedGlyphs)
        {
            text = text.Replace(glyph, string.Empty, StringComparison.Ordinal);
        }

        return text
            .Replace("bounding-box", "sınır kutusu", StringComparison.OrdinalIgnoreCase)
            .Replace("viewport", "görünüm", StringComparison.OrdinalIgnoreCase)
            .Replace(" snap:", " yüzeye yakalama:", StringComparison.OrdinalIgnoreCase)
            .TrimStart();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("Kullanıcı arayüzü metni yalnız görüntüleme amacıyla kullanılır.");
}
