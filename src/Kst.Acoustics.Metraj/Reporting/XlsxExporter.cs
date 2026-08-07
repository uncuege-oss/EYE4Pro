using ClosedXML.Excel;
using Kst.Acoustics.Metraj.Rules;

namespace Kst.Acoustics.Metraj.Reporting;

public class MetrajReportRow
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Length { get; set; }
    public string Unit { get; set; } = string.Empty;
}

public class MetrajPartialMatch
{
    public string TemplateName { get; set; } = string.Empty;
    public int RequiredParts { get; set; }
    public int FoundParts { get; set; }
    public string Location { get; set; } = string.Empty; // approximate center
}

public class MetrajReportData
{
    public string ProjectName { get; set; } = string.Empty;
    public DateTime ExportDate { get; set; } = DateTime.Now;
    public RuleSet? RuleSet { get; set; }
    public List<MetrajReportRow> LayerResults { get; set; } = new();
    public List<MetrajReportRow> ColorResults { get; set; } = new();
    public Dictionary<string, int> TemplateResults { get; set; } = new();
    public List<MetrajPartialMatch> PartialMatches { get; set; } = new();
}

public class XlsxExporter
{
    public void Export(string outputPath, MetrajReportData data)
    {
        using var workbook = new XLWorkbook();
        
        bool onlyTemplates = data.RuleSet?.OnlyTemplates ?? false;

        if (!onlyTemplates)
        {
            AddByLayerSheet(workbook, data);
            AddByColorSheet(workbook, data);
        }
        
        AddByTemplateSheet(workbook, data);
        
        if (data.PartialMatches.Count > 0)
            AddPartialMatchSheet(workbook, data);

        workbook.SaveAs(outputPath);
    }

    private void AddHeader(IXLWorksheet sheet, MetrajReportData data, string title)
    {
        sheet.Cell(1, 1).Value = "EYE4PRO ACOUSTICS - METRAJ RAPORU";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;

        sheet.Cell(2, 1).Value = $"Proje: {data.ProjectName}";
        sheet.Cell(3, 1).Value = $"Tarih: {data.ExportDate:dd.MM.yyyy HH:mm}";
        sheet.Cell(4, 1).Value = $"Birim: {data.RuleSet?.TargetUnit.ToString() ?? "mm"}";
        
        sheet.Cell(6, 1).Value = title;
        sheet.Cell(6, 1).Style.Font.Bold = true;
    }

    private void AddByLayerSheet(XLWorkbook wb, MetrajReportData data)
    {
        var ws = wb.Worksheets.Add("By-Layer");
        AddHeader(ws, data, "Katman Bazlı Metraj");

        int row = 8;
        ws.Cell(row, 1).Value = "Katman Adı";
        ws.Cell(row, 2).Value = "Adet";
        ws.Cell(row, 3).Value = "Toplam Uzunluk";
        ws.Cell(row, 4).Value = "Birim";
        ws.Range(row, 1, row, 4).Style.Font.Bold = true;
        ws.Range(row, 1, row, 4).Style.Fill.BackgroundColor = XLColor.LightGray;

        foreach (var item in data.LayerResults)
        {
            row++;
            ws.Cell(row, 1).Value = item.Name;
            ws.Cell(row, 2).Value = item.Count;
            ws.Cell(row, 3).Value = item.Length;
            ws.Cell(row, 4).Value = item.Unit;
        }
        ws.Columns().AdjustToContents();
    }

    private void AddByColorSheet(XLWorkbook wb, MetrajReportData data)
    {
        var ws = wb.Worksheets.Add("By-Color");
        AddHeader(ws, data, "Renk Bazlı Metraj");

        int row = 8;
        ws.Cell(row, 1).Value = "Renk (ACI/RGB)";
        ws.Cell(row, 2).Value = "Entity Sayısı";
        ws.Cell(row, 3).Value = "Toplam Uzunluk";
        ws.Cell(row, 4).Value = "Birim";
        ws.Range(row, 1, row, 4).Style.Font.Bold = true;
        ws.Range(row, 1, row, 4).Style.Fill.BackgroundColor = XLColor.LightGray;

        foreach (var item in data.ColorResults)
        {
            row++;
            ws.Cell(row, 1).Value = item.Name;
            ws.Cell(row, 2).Value = item.Count;
            ws.Cell(row, 3).Value = item.Length;
            ws.Cell(row, 4).Value = item.Unit;
        }
        ws.Columns().AdjustToContents();
    }

    private void AddByTemplateSheet(XLWorkbook wb, MetrajReportData data)
    {
        var ws = wb.Worksheets.Add("By-Template");
        AddHeader(ws, data, "Şablon Tespit Sayıları");

        int row = 8;
        ws.Cell(row, 1).Value = "Şablon Adı";
        ws.Cell(row, 2).Value = "Tespit Adedi";
        ws.Range(row, 1, row, 2).Style.Font.Bold = true;
        ws.Range(row, 1, row, 2).Style.Fill.BackgroundColor = XLColor.LightGray;

        foreach (var item in data.TemplateResults)
        {
            row++;
            ws.Cell(row, 1).Value = item.Key;
            ws.Cell(row, 2).Value = item.Value;
        }
        ws.Columns().AdjustToContents();
    }

    private void AddPartialMatchSheet(XLWorkbook wb, MetrajReportData data)
    {
        var ws = wb.Worksheets.Add("Eksik Eşleşmeler");
        AddHeader(ws, data, "Eksik Parça Uyarıları");

        int row = 8;
        ws.Cell(row, 1).Value = "Şablon Adı";
        ws.Cell(row, 2).Value = "Gereken Parça";
        ws.Cell(row, 3).Value = "Bulunan Parça";
        ws.Cell(row, 4).Value = "Konum (X, Y)";
        ws.Range(row, 1, row, 4).Style.Font.Bold = true;
        ws.Range(row, 1, row, 4).Style.Fill.BackgroundColor = XLColor.LightYellow;

        foreach (var pm in data.PartialMatches)
        {
            row++;
            ws.Cell(row, 1).Value = pm.TemplateName;
            ws.Cell(row, 2).Value = pm.RequiredParts;
            ws.Cell(row, 3).Value = pm.FoundParts;
            ws.Cell(row, 4).Value = pm.Location;
            ws.Range(row, 1, row, 4).Style.Font.FontColor = XLColor.DarkRed;
        }
        ws.Columns().AdjustToContents();
    }
}
