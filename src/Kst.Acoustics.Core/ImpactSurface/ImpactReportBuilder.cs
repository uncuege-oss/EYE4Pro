using System;
using System.Linq;
using System.Text;

namespace Kst.Acoustics.Core.ImpactSurface;

/// <summary>
/// Darbe emici yüzey analiz ve sertifika tipi raporu Markdown formatında üretir.
/// EN 1177 / ASTM F1292 tarama sonuçlarını ve ASCII ivme-zaman grafiğini içerir.
/// </summary>
public static class ImpactReportBuilder
{
    /// <summary>Ana rapor: parametreler + tek koşu sonucu + isteğe bağlı uyum taraması.</summary>
    public static string Build(
        ImpactSurfaceProject project,
        ImpactCalculationResult result,
        StandardComplianceReport? compliance = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(result);

        var sb = new StringBuilder();
        sb.AppendLine("# Darbe Emici Yüzey Analiz Raporu");
        sb.AppendLine($"Proje: **{project.Name}**");
        sb.AppendLine($"Tarih: {DateTime.Now:dd.MM.yyyy HH:mm}");
        sb.AppendLine("---");

        AppendParameters(sb, project);
        AppendResults(sb, result);
        AppendLayers(sb, project);

        if (result.AccelSeries is { Length: > 0 })
            AppendAccelChart(sb, result);

        if (compliance != null)
            AppendCompliance(sb, compliance);

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("*Bu rapor Eye4Pro Acoustics simülasyon motoru tarafından üretilmiştir.* ");
        sb.AppendLine("*Sonuçlar tasarım amaçlı tahminlerdir; akredite laboratuvar testinin yerini tutmaz.*");
        return sb.ToString();
    }

    private static void AppendParameters(StringBuilder sb, ImpactSurfaceProject project)
    {
        sb.AppendLine("## 1. Simülasyon Parametreleri");
        sb.AppendLine($"- **Düşüş Yüksekliği:** {project.DropHeight:F2} m");
        sb.AppendLine($"- **Kafa Kütlesi:** {project.HeadMass:F2} kg");
        sb.AppendLine($"- **Temas Alanı:** {project.ContactArea:F4} m²");
        if (!string.IsNullOrWhiteSpace(project.TemplateName))
            sb.AppendLine($"- **Şablon:** {project.TemplateName}");
        sb.AppendLine();
    }

    private static void AppendResults(StringBuilder sb, ImpactCalculationResult result)
    {
        sb.AppendLine("## 2. Hesaplama Sonuçları (Tahmini)");
        sb.AppendLine($"- **Gmax (Tepe İvme):** {result.Gmax:F1} g  (limit < 200 g)");
        sb.AppendLine($"- **HIC (Kafa Yaralanma Kriteryumu):** {result.HIC:F0}  (limit < 1000)");
        sb.AppendLine($"- **Kritik Düşüş Yüksekliği (CFH, kaba tahmin):** {result.CriticalFallHeight:F2} m");
        sb.AppendLine($"- **Deformasyon:** {result.Deformation:F1} mm");
        sb.AppendLine($"- **Geri Sıçrama Katsayısı (COR):** {result.CoefficientOfRestitution:F2}");
        sb.AppendLine($"- **Toplam Kalınlık:** {result.TotalThickness:F0} mm");
        sb.AppendLine($"- **Toplam Yüzey Ağırlığı:** {result.TotalWeight:F1} kg/m²");
        sb.AppendLine($"- **Kompozit αw:** {result.TotalAlphaW:F2}");
        sb.AppendLine();
        sb.AppendLine($"**Durum:** {result.StatusMessage}");
        sb.AppendLine();
    }

    private static void AppendLayers(StringBuilder sb, ImpactSurfaceProject project)
    {
        if (project.Layers.Count == 0) return;
        sb.AppendLine("## 3. Katman Yapısı");
        sb.AppendLine("| # | Malzeme | Kalınlık (mm) | Fonksiyon |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var layer in project.Layers)
            sb.AppendLine($"| {layer.Index} | {layer.MaterialCode} | {layer.ThicknessMm:F0} | {layer.FunctionTag} |");
        sb.AppendLine();
    }

    /// <summary>
    /// İvme-zaman grafiğinin ASCII gösterimi. Tepe Gmax referans alınır;
    /// 200g limit çizgisi `-` ile, eğri `█` ile çizilir.
    /// </summary>
    private static void AppendAccelChart(StringBuilder sb, ImpactCalculationResult result)
    {
        var series = result.AccelSeries;
        const int height = 12;
        const int width = 60;

        double peak = Math.Max(series.Max(), 250.0); // 200g limit en az 4/5 yüksekliğe gelsin
        double limitRow = height * (200.0 / peak);

        sb.AppendLine("## 4. İvme-Zaman Eğrisi (Tahmini)");
        sb.AppendLine("```");
        sb.AppendLine($"Tepe: {result.Gmax:F1} g    Süre: {result.PulseDurationMs:F1} ms    (— = 200 g limit)");

        // Y ekseninde her satır için seriyi tara
        for (int row = height; row >= 1; row--)
        {
            double rowG = peak * row / height;
            var line = new StringBuilder();
            line.Append($"{rowG,5:F0} g |");
            int sampleStep = Math.Max(1, series.Length / width);
            for (int i = 0; i < series.Length; i += sampleStep)
            {
                bool atBar = series[i] >= rowG && series[i] < rowG + (peak / height);
                bool atFill = series[i] >= rowG;
                bool atLimit = Math.Abs(row - limitRow) < 0.5 && (i % 4 == 0);
                if (atBar) line.Append('█');
                else if (atFill) line.Append('│');
                else if (atLimit) line.Append('-');
                else line.Append(' ');
            }
            sb.AppendLine(line.ToString());
        }

        var axis = new StringBuilder("        +");
        for (int i = 0; i < width; i++) axis.Append('-');
        sb.AppendLine(axis.ToString());
        sb.AppendLine($"        0{new string(' ', width / 2 - 1)}t/2{new string(' ', width / 2 - 1)}T");
        sb.AppendLine("```");
        sb.AppendLine();
    }

    private static void AppendCompliance(StringBuilder sb, StandardComplianceReport c)
    {
        sb.AppendLine("## 5. Standart Uyum Tarama (EN 1177 / ASTM F1292)");
        sb.AppendLine($"**Standart:** {c.StandardLabel}  ");
        sb.AppendLine($"**Test Başı:** {c.HeadFormProfile}  ");
        sb.AppendLine($"**Kafa Kütlesi:** {c.HeadMassKg:F2} kg, **Temas Alanı:** {c.ContactAreaM2:F4} m²");
        sb.AppendLine();
        sb.AppendLine("| Düşüş (m) | Gmax (g) | HIC | Durum |");
        sb.AppendLine("|---:|---:|---:|---|");
        foreach (var t in c.Trials)
            sb.AppendLine($"| {t.DropHeightM:F2} | {t.GmaxG:F1} | {t.Hic:F0} | {t.Verdict} |");
        sb.AppendLine();
        sb.AppendLine($"**Sertifikalı Kritik Düşüş Yüksekliği (bin arama):** **{c.CertifiedCriticalFallHeightM:F2} m**");
        if (c.Notes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Notlar:**");
            foreach (var n in c.Notes) sb.AppendLine($"- {n}");
        }
        sb.AppendLine();
    }
}
