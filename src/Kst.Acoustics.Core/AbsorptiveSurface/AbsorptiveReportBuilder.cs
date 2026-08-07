using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Kst.Acoustics.Core.AbsorptiveSurface;

/// <summary>
/// Ses emici yüzey için Markdown analiz raporu.
/// Katman tablosu, oktav-bant α tablosu ve ASCII α spektrum çubuk grafiği içerir.
/// </summary>
public static class AbsorptiveReportBuilder
{
    public static string Build(
        string projectName,
        IEnumerable<AbsorptiveLayer> layers,
        AbsorptiveSurfaceResult result)
    {
        ArgumentNullException.ThrowIfNull(layers);
        ArgumentNullException.ThrowIfNull(result);

        var sb = new StringBuilder();
        sb.AppendLine("# Ses Emici Yüzey Analiz Raporu");
        sb.AppendLine($"Proje: **{(string.IsNullOrWhiteSpace(projectName) ? "(isimsiz)" : projectName)}**");
        sb.AppendLine($"Tarih: {DateTime.Now:dd.MM.yyyy HH:mm}");
        sb.AppendLine("---");

        AppendSummary(sb, result);
        AppendLayers(sb, layers);
        AppendBandTable(sb, result);
        AppendBandChart(sb, result);
        AppendWarnings(sb, result);

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("*Bu rapor Eye4Pro Acoustics ses emici yüzey motoru tarafından üretilmiştir.* ");
        sb.AppendLine("*α değerleri tasarım amaçlı mühendislik tahminleridir; akredite ISO 354 ölçümünün yerini tutmaz.*");
        return sb.ToString();
    }

    private static void AppendSummary(StringBuilder sb, AbsorptiveSurfaceResult r)
    {
        sb.AppendLine("## 1. Özet");
        sb.AppendLine($"- **NRC:** {r.Nrc:F2}");
        sb.AppendLine($"- **αw (Ağırlıklı):** {r.AlphaW:F2}");
        sb.AppendLine($"- **SAA:** {r.Saa:F2}");
        sb.AppendLine($"- **Toplam Kalınlık:** {r.TotalThicknessMm:F0} mm");
        sb.AppendLine($"- **Performans:** {r.PerformanceSummary}");
        if (!string.IsNullOrWhiteSpace(r.RecommendedUse))
            sb.AppendLine($"- **Önerilen Kullanım:** {r.RecommendedUse}");
        sb.AppendLine();
    }

    private static void AppendLayers(StringBuilder sb, IEnumerable<AbsorptiveLayer> layers)
    {
        var list = layers.ToList();
        if (list.Count == 0) return;

        sb.AppendLine("## 2. Katman Yapısı (Yüzeyden Arkaya)");
        sb.AppendLine("| # | Malzeme | Tip | Kalınlık (mm) | Aktif |");
        sb.AppendLine("|---|---|---|---:|:---:|");
        foreach (var l in list.OrderBy(l => l.Index))
            sb.AppendLine($"| {l.Index} | {l.MaterialCode} | {l.LayerType} | {l.ThicknessMm:F0} | {(l.IsEnabled ? "✓" : "—")} |");
        sb.AppendLine();
    }

    private static void AppendBandTable(StringBuilder sb, AbsorptiveSurfaceResult r)
    {
        sb.AppendLine("## 3. Oktav Bant Soğurma Katsayıları");
        sb.AppendLine("| Frekans (Hz) | α |");
        sb.AppendLine("|---:|---:|");
        foreach (var band in r.AbsorptionBands.OrderBy(b => b.Key))
            sb.AppendLine($"| {band.Key.ToString("F0", CultureInfo.InvariantCulture)} | {band.Value.ToString("F2", CultureInfo.InvariantCulture)} |");
        sb.AppendLine();
    }

    /// <summary>α spektrumunun ASCII çubuk grafiği. 0..1 aralığında, 40 sütun genişlik.</summary>
    private static void AppendBandChart(StringBuilder sb, AbsorptiveSurfaceResult r)
    {
        if (r.AbsorptionBands.Count == 0) return;

        const int width = 40;
        sb.AppendLine("## 4. α Spektrum Görseli");
        sb.AppendLine("```");
        sb.AppendLine($"   Hz | α    0.0{new string(' ', width / 2 - 4)}0.5{new string(' ', width / 2 - 4)}1.0");
        sb.AppendLine($"      |     {new string('-', width)}");
        foreach (var band in r.AbsorptionBands.OrderBy(b => b.Key))
        {
            int filled = (int)Math.Round(Math.Clamp(band.Value, 0, 1) * width);
            string bar = new string('█', filled) + new string(' ', width - filled);
            string freqLabel = band.Key.ToString("F0", CultureInfo.InvariantCulture).PadLeft(5);
            string alphaLabel = band.Value.ToString("F2", CultureInfo.InvariantCulture).PadLeft(4);
            sb.AppendLine($"{freqLabel} | {alphaLabel} {bar}");
        }
        sb.AppendLine("```");
        sb.AppendLine();
    }

    private static void AppendWarnings(StringBuilder sb, AbsorptiveSurfaceResult r)
    {
        if (r.Warnings.Count == 0) return;
        sb.AppendLine("## 5. Uyarılar");
        foreach (var w in r.Warnings) sb.AppendLine($"- {w}");
        sb.AppendLine();
    }
}
