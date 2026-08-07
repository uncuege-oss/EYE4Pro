using ClosedXML.Excel;

namespace Kst.Acoustics.Core.Electric.KesifHazirla;

/// <summary>
/// M11/M13'ün ürettiği metraj excel'lerinin ilk sayfasını ("M13 Metraj Özeti") ayrıştırır.
/// Başlık satırı dosyanın 1. satırında değildir (M13 özetinde 7. satırdadır); bu yüzden
/// başlık satırı içerik taranarak bulunur. Kolonlar ad ile eşlenir (sıra varsayılmaz).
/// </summary>
public static class KesifMetrajImportServisi
{
    private const string AnaGrupKey = "anagrup";
    private const string AltGrupKey = "altgrup";
    private const string TeknikKey = "teknikozellik";
    private const string BirimKey = "birim";
    private const string MiktarKey = "miktar";
    private const string AciklamaKey = "aciklama";

    private static readonly string[] Basliklar =
        ["Ana Grup", "Alt Grup", "Teknik Özellik", "Birim", "Miktar", "Açıklama"];

    /// <summary>Metraj excel'ini okuyup bir <see cref="KesifReferansDosyasi"/> üretir.</summary>
    public static KesifReferansDosyasi DosyadanOku(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Dosya yolu boş olamaz.", nameof(path));

        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext != ".xlsx")
            throw new InvalidOperationException("Metraj referansı için yalnız .xlsx desteklenir.");

        using var workbook = new XLWorkbook(path);
        var sheet = workbook.Worksheets.First();

        var (headers, headerRowNumber) = BaslikSatiriniBul(sheet)
            ?? throw new InvalidOperationException(
                "Metraj başlık satırı bulunamadı. Beklenen kolonlar: Ana Grup, Alt Grup, Teknik Özellik, Birim, Miktar.");

        var referans = new KesifReferansDosyasi
        {
            DosyaAdi = Path.GetFileName(path),
            KaynakModul = "M13",
            SayfaAdi = sheet.Name,
            YuklemeTarihi = DateTime.Now
        };

        foreach (var row in sheet.RowsUsed().Where(r => r.RowNumber() > headerRowNumber))
        {
            var anaGrup = Hucre(row, headers, AnaGrupKey);
            var altGrup = Hucre(row, headers, AltGrupKey);
            var teknik = Hucre(row, headers, TeknikKey);
            var birim = Hucre(row, headers, BirimKey);

            // İçeriksiz satırları atla (özet/boş satır); en az teknik özellik veya ana grup olmalı.
            if (string.IsNullOrWhiteSpace(teknik) && string.IsNullOrWhiteSpace(anaGrup))
                continue;

            referans.MetrajSatirlari.Add(new KesifMetrajSatiri
            {
                AnaGrup = anaGrup,
                AltGrup = altGrup,
                TeknikOzellik = teknik,
                Birim = birim,
                Miktar = HucreDecimal(row, headers, MiktarKey),
                Aciklama = Hucre(row, headers, AciklamaKey)
            });
        }

        return referans;
    }

    /// <summary>"Excel Şablonu" — beklenen kolon yapısını gösteren, bir örnek satırlı boş xlsx üretir.</summary>
    public static void SablonOlustur(string path)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Metraj");

        for (var i = 0; i < Basliklar.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = Basliklar[i];
            cell.Style.Font.Bold = true;
        }

        // Örnek satır (kullanıcıya format göstermek için).
        ws.Cell(2, 1).Value = "Kablo";
        ws.Cell(2, 2).Value = "Güç kablosu";
        ws.Cell(2, 3).Value = "NHXMH 4x16 mm²";
        ws.Cell(2, 4).Value = "m";
        ws.Cell(2, 5).Value = 120;
        ws.Cell(2, 6).Value = "Örnek açıklama";

        ws.Columns().AdjustToContents();
        workbook.SaveAs(path);
    }

    /// <summary>İlk ~40 satırı tarayıp başlık kolonlarını içeren satırı bulur.</summary>
    private static (Dictionary<string, int> Headers, int RowNumber)? BaslikSatiriniBul(IXLWorksheet sheet)
    {
        foreach (var row in sheet.RowsUsed().Take(40))
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var cell in row.CellsUsed())
            {
                var key = KesifPozImportServisi.NormalizeHeader(cell.GetString());
                if (!string.IsNullOrWhiteSpace(key) && !map.ContainsKey(key))
                    map[key] = cell.Address.ColumnNumber;
            }

            // Başlık satırı: teknik özellik + birim + miktar kolonları aynı satırda bulunmalı.
            if (map.ContainsKey(TeknikKey) && map.ContainsKey(BirimKey) && map.ContainsKey(MiktarKey))
                return (map, row.RowNumber());
        }

        return null;
    }

    private static string Hucre(IXLRow row, Dictionary<string, int> headers, string key)
        => headers.TryGetValue(key, out var col) ? row.Cell(col).GetFormattedString().Trim() : "";

    private static decimal HucreDecimal(IXLRow row, Dictionary<string, int> headers, string key)
    {
        if (!headers.TryGetValue(key, out var col))
            return 0;

        var cell = row.Cell(col);
        if (cell.DataType == XLDataType.Number)
            return (decimal)cell.GetDouble();

        return KesifPozImportServisi.ParseDecimal(cell.GetFormattedString());
    }
}
