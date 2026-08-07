using ClosedXML.Excel;

namespace Kst.Acoustics.Core.Projects;

public static class MaterialExcelService
{
    // ─── Sütun indeksleri (1-tabanlı) ────────────────────────────────────────
    private const int ColId          = 1;
    private const int ColName        = 2;
    private const int ColAlpha63     = 3;
    private const int ColAlpha125    = 4;
    private const int ColAlpha250    = 5;
    private const int ColAlpha500    = 6;
    private const int ColAlpha1k     = 7;
    private const int ColAlpha2k     = 8;
    private const int ColAlpha4k     = 9;
    private const int ColAlpha8k     = 10;
    private const int ColScattering  = 11;
    private const int ColNotes       = 12;

    private static readonly string[] Headers =
    [
        "id", "displayName",
        "alpha_63", "alpha_125", "alpha_250", "alpha_500",
        "alpha_1k", "alpha_2k",  "alpha_4k",  "alpha_8k",
        "scattering_mid", "notes"
    ];

    // ─── Dışa aktar ──────────────────────────────────────────────────────────

    public static void Export(string filePath, IEnumerable<AcousticMaterial> materials)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Materyaller");

        WriteHeader(ws);

        var row = 2;
        foreach (var m in materials)
        {
            ws.Cell(row, ColId).Value          = m.Id;
            ws.Cell(row, ColName).Value        = m.DisplayName;
            ws.Cell(row, ColAlpha63).Value     = m.Alpha[0];
            ws.Cell(row, ColAlpha125).Value    = m.Alpha[1];
            ws.Cell(row, ColAlpha250).Value    = m.Alpha[2];
            ws.Cell(row, ColAlpha500).Value    = m.Alpha[3];
            ws.Cell(row, ColAlpha1k).Value     = m.Alpha[4];
            ws.Cell(row, ColAlpha2k).Value     = m.Alpha[5];
            ws.Cell(row, ColAlpha4k).Value     = m.Alpha[6];
            ws.Cell(row, ColAlpha8k).Value     = m.Alpha[7];
            ws.Cell(row, ColScattering).Value  = m.ScatteringMid;
            row++;
        }

        StyleSheet(ws, row - 1);
        wb.SaveAs(filePath);
    }

    // ─── Şablon ──────────────────────────────────────────────────────────────

    public static void ExportTemplate(string filePath)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Materyaller");

        WriteHeader(ws);

        WriteSampleRow(ws, 2, "ornek_materyal_1", "Örnek Beton",
            0.01, 0.01, 0.02, 0.03, 0.03, 0.04, 0.05, 0.05, 0.05);
        WriteSampleRow(ws, 3, "ornek_materyal_2", "Örnek Halı",
            0.02, 0.05, 0.10, 0.20, 0.45, 0.60, 0.65, 0.68, 0.10);

        var note = ws.Cell(5, ColId);
        note.Value = "⚠ id alanı benzersiz olmalıdır. Alpha değerleri 0.00–1.00 arasında olmalıdır.";
        ws.Range(5, ColId, 5, ColNotes).Merge();
        note.Style.Font.Italic    = true;
        note.Style.Font.FontColor = XLColor.DarkRed;

        StyleSheet(ws, 3);
        wb.SaveAs(filePath);
    }

    // ─── İçe aktar ───────────────────────────────────────────────────────────

    public static (List<AcousticMaterial> Materials, List<string> Errors) Import(string filePath)
    {
        var materials = new List<AcousticMaterial>();
        var errors    = new List<string>();

        using var wb = new XLWorkbook(filePath);
        var ws = wb.Worksheets.FirstOrDefault();
        if (ws is null) { errors.Add("Çalışma sayfası bulunamadı."); return (materials, errors); }

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

        for (var r = 2; r <= lastRow; r++)
        {
            var id   = ws.Cell(r, ColId).GetValue<string>().Trim();
            var name = ws.Cell(r, ColName).GetValue<string>().Trim();

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                continue;

            if (!TryGetAlpha(ws, r, ColAlpha63,  out var a0, errors, id) ||
                !TryGetAlpha(ws, r, ColAlpha125, out var a1, errors, id) ||
                !TryGetAlpha(ws, r, ColAlpha250, out var a2, errors, id) ||
                !TryGetAlpha(ws, r, ColAlpha500, out var a3, errors, id) ||
                !TryGetAlpha(ws, r, ColAlpha1k,  out var a4, errors, id) ||
                !TryGetAlpha(ws, r, ColAlpha2k,  out var a5, errors, id) ||
                !TryGetAlpha(ws, r, ColAlpha4k,  out var a6, errors, id) ||
                !TryGetAlpha(ws, r, ColAlpha8k,  out var a7, errors, id))
                continue;

            var scattering = ws.Cell(r, ColScattering).TryGetValue<double>(out var sc)
                ? Math.Clamp(sc, 0, 1) : 0.05;

            materials.Add(new AcousticMaterial(id, name,
                [a0, a1, a2, a3, a4, a5, a6, a7], scattering));
        }

        return (materials, errors);
    }

    // ─── Yardımcılar ─────────────────────────────────────────────────────────

    private static bool TryGetAlpha(IXLWorksheet ws, int row, int col,
        out double value, List<string> errors, string id)
    {
        if (ws.Cell(row, col).TryGetValue<double>(out value))
        {
            value = Math.Clamp(value, 0, 1);
            return true;
        }
        errors.Add($"'{id}' satırı {row}, sütun {col}: geçersiz alpha değeri.");
        value = 0;
        return false;
    }

    private static void WriteHeader(IXLWorksheet ws)
    {
        for (var c = 0; c < Headers.Length; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = Headers[c];
            cell.Style.Font.Bold      = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E79");
            cell.Style.Font.FontColor = XLColor.White;
        }
    }

    private static void WriteSampleRow(IXLWorksheet ws, int row, string id, string name,
        double a0, double a1, double a2, double a3, double a4, double a5, double a6, double a7, double s)
    {
        ws.Cell(row, ColId).Value         = id;
        ws.Cell(row, ColName).Value       = name;
        ws.Cell(row, ColAlpha63).Value    = a0;
        ws.Cell(row, ColAlpha125).Value   = a1;
        ws.Cell(row, ColAlpha250).Value   = a2;
        ws.Cell(row, ColAlpha500).Value   = a3;
        ws.Cell(row, ColAlpha1k).Value    = a4;
        ws.Cell(row, ColAlpha2k).Value    = a5;
        ws.Cell(row, ColAlpha4k).Value    = a6;
        ws.Cell(row, ColAlpha8k).Value    = a7;
        ws.Cell(row, ColScattering).Value = s;
    }

    private static void StyleSheet(IXLWorksheet ws, int lastDataRow)
    {
        ws.Column(ColId).Width    = 22;
        ws.Column(ColName).Width  = 28;
        for (var c = ColAlpha63; c <= ColScattering; c++) ws.Column(c).Width = 10;
        ws.Column(ColNotes).Width = 30;

        if (lastDataRow >= 2)
        {
            for (var c = ColAlpha63; c <= ColScattering; c++)
                ws.Range(2, c, lastDataRow, c).Style.NumberFormat.Format = "0.00";

            for (var r = 2; r <= lastDataRow; r++)
            {
                if (r % 2 == 0)
                    ws.Range(r, ColId, r, ColNotes).Style.Fill.BackgroundColor =
                        XLColor.FromHtml("#EBF3FB");
            }
        }

        ws.SheetView.FreezeRows(1);
    }
}
