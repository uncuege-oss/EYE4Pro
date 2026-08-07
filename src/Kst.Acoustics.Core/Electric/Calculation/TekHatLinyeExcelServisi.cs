using ClosedXML.Excel;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.Core.Electric.Calculation;

public static class TekHatLinyeExcelServisi
{
    private static readonly string[] Headers =
    [
        "Yük Tipi",
        "Faz",
        "Sorti Sayısı",
        "Birim Güç (W)",
        "Talep Faktörü",
        "Kablo Mesafesi (m)",
        "Sigorta Cinsi",
        "Açıklama",
        "Linye No"
    ];

    public static void SablonOlustur(string filePath)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Linyeler");

        WriteHeader(ws);
        WriteSample(ws, 2, "Aydınlatma", "Monofaze", 12, 40, 1.0, 10, "B", "Ofis aydınlatma");
        WriteSample(ws, 3, "Priz", "Monofaze", 8, 300, 1.0, 15, "B", "Genel priz");
        WriteSample(ws, 4, "Motor", "Trifaze", 1, 4000, 1.0, 25, "C", "Havalandırma fanı");

        var note = ws.Cell(6, 1);
        note.Value = "Not: Linye No boş bırakılabilir. İçe aktarımda seçili pano içinde yük tipine göre A1/A2, P1/P2 gibi otomatik sıralanır.";
        ws.Range(6, 1, 6, Headers.Length).Merge();
        note.Style.Font.Italic = true;
        note.Style.Font.FontColor = XLColor.DarkGreen;

        StyleSheet(ws, 4);
        wb.SaveAs(filePath);
    }

    public static (List<Linye> Linyeler, List<string> Hatalar) IceriAktar(string filePath)
    {
        var linyeler = new List<Linye>();
        var hatalar = new List<string>();

        using var wb = new XLWorkbook(filePath);
        var ws = wb.Worksheets.FirstOrDefault();
        if (ws is null)
        {
            hatalar.Add("Çalışma sayfası bulunamadı.");
            return (linyeler, hatalar);
        }

        var map = HeaderMap(ws);
        if (!map.TryGetValue("Yük Tipi", out var colYukTipi))
        {
            hatalar.Add("'Yük Tipi' sütunu bulunamadı.");
            return (linyeler, hatalar);
        }

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (var row = 2; row <= lastRow; row++)
        {
            var yukTipi = ReadString(ws, row, colYukTipi);
            if (string.IsNullOrWhiteSpace(yukTipi))
                continue;
            if (yukTipi.StartsWith("Not:", StringComparison.OrdinalIgnoreCase) ||
                yukTipi.StartsWith("Açıklama:", StringComparison.OrdinalIgnoreCase))
                continue;

            var linye = new Linye
            {
                LinyeNo = ReadString(ws, row, map.GetValueOrDefault("Linye No")),
                YukTipiAd = yukTipi,
                FazTipi = ParseFaz(ReadString(ws, row, map.GetValueOrDefault("Faz"))),
                SortiSayisi = ReadInt(ws, row, map.GetValueOrDefault("Sorti Sayısı"), 1, hatalar),
                BirimGucW = ReadDouble(ws, row, map.GetValueOrDefault("Birim Güç (W)"), VarsayilanBirimGuc(yukTipi), hatalar),
                TalepFaktoru = ReadDouble(ws, row, map.GetValueOrDefault("Talep Faktörü"), 1.0, hatalar),
                KabloMesafeM = ReadDouble(ws, row, map.GetValueOrDefault("Kablo Mesafesi (m)"), 10.0, hatalar),
                SigortaCinsi = ParseSigorta(ReadString(ws, row, map.GetValueOrDefault("Sigorta Cinsi"))),
                Aciklama = ReadString(ws, row, map.GetValueOrDefault("Açıklama"))
            };

            if (linye.SortiSayisi < 1)
            {
                hatalar.Add($"Satır {row}: Sorti sayısı 1'den küçük olamaz; 1 kabul edildi.");
                linye.SortiSayisi = 1;
            }

            if (linye.BirimGucW <= 0)
            {
                hatalar.Add($"Satır {row}: Birim güç 0'dan büyük olmalı; varsayılan değer kabul edildi.");
                linye.BirimGucW = VarsayilanBirimGuc(yukTipi);
            }

            if (linye.TalepFaktoru <= 0 || linye.TalepFaktoru > 1)
            {
                hatalar.Add($"Satır {row}: Talep faktörü 0-1 aralığında olmalı; 1 kabul edildi.");
                linye.TalepFaktoru = 1.0;
            }

            if (linye.KabloMesafeM <= 0)
            {
                hatalar.Add($"Satır {row}: Kablo mesafesi 0'dan büyük olmalı; 10 m kabul edildi.");
                linye.KabloMesafeM = 10;
            }

            linyeler.Add(linye);
        }

        return (linyeler, hatalar);
    }

    private static Dictionary<string, int> HeaderMap(IXLWorksheet ws)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lastCol = ws.Row(1).LastCellUsed()?.Address.ColumnNumber ?? 0;
        for (var col = 1; col <= lastCol; col++)
        {
            var header = ws.Cell(1, col).GetValue<string>().Trim();
            if (!string.IsNullOrWhiteSpace(header) && !result.ContainsKey(header))
                result[header] = col;
        }
        return result;
    }

    private static void WriteHeader(IXLWorksheet ws)
    {
        for (var i = 0; i < Headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = Headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E79");
            cell.Style.Font.FontColor = XLColor.White;
        }
    }

    private static void WriteSample(IXLWorksheet ws, int row, string yukTipi, string faz, int sorti,
        double birimGucW, double talep, double mesafeM, string sigorta, string aciklama)
    {
        ws.Cell(row, 1).Value = yukTipi;
        ws.Cell(row, 2).Value = faz;
        ws.Cell(row, 3).Value = sorti;
        ws.Cell(row, 4).Value = birimGucW;
        ws.Cell(row, 5).Value = talep;
        ws.Cell(row, 6).Value = mesafeM;
        ws.Cell(row, 7).Value = sigorta;
        ws.Cell(row, 8).Value = aciklama;
        ws.Cell(row, 9).Value = "";
    }

    private static void StyleSheet(IXLWorksheet ws, int lastDataRow)
    {
        ws.Columns(1, Headers.Length).AdjustToContents();
        ws.Column(8).Width = 28;
        ws.Column(9).Width = 12;
        ws.SheetView.FreezeRows(1);
        ws.Range(1, 1, Math.Max(1, lastDataRow), Headers.Length).SetAutoFilter();
        ws.Range(2, 5, Math.Max(2, lastDataRow), 5).Style.NumberFormat.Format = "0.00";
        ws.Range(2, 4, Math.Max(2, lastDataRow), 6).Style.NumberFormat.Format = "0.##";
    }

    private static string ReadString(IXLWorksheet ws, int row, int col)
        => col <= 0 ? "" : ws.Cell(row, col).GetValue<string>().Trim();

    private static int ReadInt(IXLWorksheet ws, int row, int col, int fallback, List<string> hatalar)
    {
        if (col <= 0 || ws.Cell(row, col).IsEmpty()) return fallback;
        if (ws.Cell(row, col).TryGetValue<int>(out var value)) return value;
        hatalar.Add($"Satır {row}, sütun {col}: tam sayı okunamadı; {fallback} kabul edildi.");
        return fallback;
    }

    private static double ReadDouble(IXLWorksheet ws, int row, int col, double fallback, List<string> hatalar)
    {
        if (col <= 0 || ws.Cell(row, col).IsEmpty()) return fallback;
        if (ws.Cell(row, col).TryGetValue<double>(out var value)) return value;
        if (NumberHelper.TryParse(ws.Cell(row, col).GetValue<string>(), out value)) return value;
        hatalar.Add($"Satır {row}, sütun {col}: sayı okunamadı; {fallback:0.##} kabul edildi.");
        return fallback;
    }

    private static FazTipi ParseFaz(string text)
    {
        var s = text.Trim();
        if (string.IsNullOrWhiteSpace(s)) return FazTipi.Monofaze;
        if (s.Contains('3') || s.Contains("tri", StringComparison.OrdinalIgnoreCase)) return FazTipi.Trifaze;
        return FazTipi.Monofaze;
    }

    private static SigortaCinsi ParseSigorta(string text)
    {
        var s = text.Trim();
        if (Enum.TryParse<SigortaCinsi>(s, true, out var result)) return result;
        return SigortaCinsi.B;
    }

    private static double VarsayilanBirimGuc(string yukTipi)
    {
        if (yukTipi.Contains("Aydınlatma", StringComparison.OrdinalIgnoreCase) ||
            yukTipi.Contains("Aydinlatma", StringComparison.OrdinalIgnoreCase)) return 40;
        if (yukTipi.Contains("Mekanik", StringComparison.OrdinalIgnoreCase)) return 200;
        return 300;
    }
}
