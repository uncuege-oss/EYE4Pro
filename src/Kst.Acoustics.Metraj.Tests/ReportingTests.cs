using Kst.Acoustics.Metraj.Reporting;
using Kst.Acoustics.Metraj.Rules;
using Kst.Acoustics.Metraj.Metrics;

namespace Kst.Acoustics.Metraj.Tests;

public class ReportingTests
{
    [Fact]
    public void XlsxExporter_EndToEnd_ShouldCreateFile()
    {
        string outputPath = "TestReport.xlsx";
        if (File.Exists(outputPath)) File.Delete(outputPath);

        var data = new MetrajReportData
        {
            ProjectName = "Deneme Projesi",
            RuleSet = new RuleSet { TargetUnit = MetrajUnit.Millimeters },
            LayerResults = new List<MetrajReportRow>
            {
                new MetrajReportRow { Name = "DUVAR", Count = 10, Length = 5000, Unit = "mm" },
                new MetrajReportRow { Name = "KAPI", Count = 2, Length = 400, Unit = "mm" }
            },
            ColorResults = new List<MetrajReportRow>
            {
                new MetrajReportRow { Name = "1 (Red)", Count = 12, Length = 5400, Unit = "mm" }
            },
            TemplateResults = new Dictionary<string, int>
            {
                { "Masa", 4 },
                { "Sandalye", 16 }
            }
        };

        var exporter = new XlsxExporter();
        exporter.Export(outputPath, data);

        Assert.True(File.Exists(outputPath));
        
        // Temizlik
        if (File.Exists(outputPath)) File.Delete(outputPath);
    }
}
