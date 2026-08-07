using ClosedXML.Excel;
using System.Globalization;
using System.Text;

namespace Kst.Acoustics.Core.Electric.KesifHazirla;

public static class KesifPozImportServisi
{
    public static IReadOnlyList<KesifPoz> DosyadanOku(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Dosya yolu boş olamaz.", nameof(path));

        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".xlsx" => ExceldenOku(path),
            ".csv" => AyracliMetindenOku(path, null),
            ".tsv" => AyracliMetindenOku(path, '\t'),
            _ => throw new InvalidOperationException("Desteklenen formatlar: .xlsx, .csv, .tsv")
        };
    }

    public static IReadOnlyList<KesifPoz> ExceldenOku(string path)
    {
        using var workbook = new XLWorkbook(path);
        var sheet = workbook.Worksheets.First();
        var headerRow = sheet.FirstRowUsed() ?? throw new InvalidOperationException("Excel dosyasında başlık satırı bulunamadı.");

        // Başlık → GERÇEK kolon numarası eşlemesi. CellsUsed() boş hücreleri atladığından
        // sıralı indeks kullanmak kolonları kaydırırdı; hücrenin gerçek adresini kullanıyoruz.
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.CellsUsed())
        {
            var key = NormalizeHeader(cell.GetString());
            if (!string.IsNullOrWhiteSpace(key) && !headers.ContainsKey(key))
                headers[key] = cell.Address.ColumnNumber;
        }

        var result = new List<KesifPoz>();
        foreach (var row in sheet.RowsUsed().Skip(1))
        {
            var pozNo = ReadCell(row, headers, "pozno");
            var birimAdi = ReadCell(row, headers, "birimadi", "imalatadi", "tanim", "tanimi", "pozadi", "aciklama");
            var birim = ReadCell(row, headers, "birim");

            if (string.IsNullOrWhiteSpace(pozNo) && string.IsNullOrWhiteSpace(birimAdi))
                continue;

            result.Add(new KesifPoz
            {
                PozNo = pozNo,
                BirimAdi = birimAdi,
                Birim = string.IsNullOrWhiteSpace(birim) ? "adet" : birim,
                BirimFiyat = ReadDecimal(row, headers, "birimfiyat", "fiyat"),
                Aciklama = ReadCell(row, headers, "not"),
                Kategori = ReadCell(row, headers, "kategori")
            });
        }

        return result;
    }

    private static IReadOnlyList<KesifPoz> AyracliMetindenOku(string path, char? delimiter)
    {
        var lines = File.ReadAllLines(path, Encoding.UTF8).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        if (lines.Count == 0)
            return [];

        var effectiveDelimiter = delimiter ?? DetectDelimiter(lines[0]);
        var headers = HeaderMap(ParseDelimitedLine(lines[0], effectiveDelimiter));
        var result = new List<KesifPoz>();
        foreach (var line in lines.Skip(1))
        {
            var cells = ParseDelimitedLine(line, effectiveDelimiter);
            string Read(params string[] names)
            {
                foreach (var name in names)
                {
                    if (headers.TryGetValue(name, out var index) && index < cells.Count)
                        return cells[index].Trim();
                }
                return "";
            }

            var pozNo = Read("pozno");
            var birimAdi = Read("birimadi", "imalatadi", "tanim", "tanimi", "pozadi", "aciklama");
            if (string.IsNullOrWhiteSpace(pozNo) && string.IsNullOrWhiteSpace(birimAdi))
                continue;

            result.Add(new KesifPoz
            {
                PozNo = pozNo,
                BirimAdi = birimAdi,
                Birim = string.IsNullOrWhiteSpace(Read("birim")) ? "adet" : Read("birim"),
                BirimFiyat = ParseDecimal(Read("birimfiyat", "fiyat")),
                Aciklama = Read("not"),
                Kategori = Read("kategori")
            });
        }

        return result;
    }

    private static char DetectDelimiter(string headerLine)
    {
        var candidates = new[] { ',', ';', '\t' };
        return candidates
            .Select(delimiter =>
            {
                var cells = ParseDelimitedLine(headerLine, delimiter);
                var headers = HeaderMap(cells);
                return new { Delimiter = delimiter, HeaderScore = headers.Count, CellCount = cells.Count };
            })
            .OrderByDescending(x => x.HeaderScore)
            .ThenByDescending(x => x.CellCount)
            .First()
            .Delimiter;
    }

    private static IReadOnlyList<string> ParseDelimitedLine(string line, char delimiter)
    {
        var cells = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (!inQuotes && c == delimiter)
            {
                cells.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        cells.Add(current.ToString().Trim());
        return cells;
    }

    private static Dictionary<string, int> HeaderMap(IEnumerable<string> headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var header in headers)
        {
            var key = NormalizeHeader(header);
            if (!string.IsNullOrWhiteSpace(key) && !map.ContainsKey(key))
                map[key] = index;
            index++;
        }

        return map;
    }

    private static string ReadCell(IXLRow row, Dictionary<string, int> headers, params string[] names)
    {
        foreach (var name in names)
        {
            // headers değeri artık 1-tabanlı GERÇEK kolon numarasıdır.
            if (headers.TryGetValue(name, out var col))
                return row.Cell(col).GetFormattedString().Trim();
        }

        return "";
    }

    private static decimal ReadDecimal(IXLRow row, Dictionary<string, int> headers, params string[] names)
    {
        foreach (var name in names)
        {
            if (!headers.TryGetValue(name, out var col))
                continue;

            var cell = row.Cell(col);
            // Sayısal hücreyi doğrudan oku — kültüre bağlı metin ayrıştırması yapma.
            if (cell.DataType == XLDataType.Number)
                return (decimal)cell.GetDouble();

            return ParseDecimal(cell.GetFormattedString());
        }

        return 0;
    }

    internal static string NormalizeHeader(string value)
    {
        var text = value.Trim().ToLowerInvariant()
            .Replace("ı", "i", StringComparison.Ordinal)
            .Replace("ğ", "g", StringComparison.Ordinal)
            .Replace("ü", "u", StringComparison.Ordinal)
            .Replace("ş", "s", StringComparison.Ordinal)
            .Replace("ö", "o", StringComparison.Ordinal)
            .Replace("ç", "c", StringComparison.Ordinal);

        return new string(text.Where(char.IsLetterOrDigit).ToArray());
    }

    internal static decimal ParseDecimal(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        var clean = value.Replace("₺", "", StringComparison.Ordinal)
                         .Replace("TL", "", StringComparison.OrdinalIgnoreCase)
                         .Replace(" ", "", StringComparison.Ordinal)
                         .Replace("\u00A0", "", StringComparison.Ordinal)
                         .Trim();
        if (clean.Length == 0)
            return 0;

        var decimalSeparator = DetectDecimalSeparator(clean);
        if (decimalSeparator is null)
        {
            var integerOnly = clean
                .Replace(".", "", StringComparison.Ordinal)
                .Replace(",", "", StringComparison.Ordinal);
            return decimal.TryParse(integerOnly, NumberStyles.Any, CultureInfo.InvariantCulture, out var whole)
                ? whole
                : 0;
        }

        var decSep = clean.LastIndexOf(decimalSeparator.Value);
        string normalized;
        var intPart = clean[..decSep]
            .Replace(".", "", StringComparison.Ordinal)
            .Replace(",", "", StringComparison.Ordinal);
        var fracPart = clean[(decSep + 1)..];
        normalized = intPart + "." + fracPart;

        return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0;
    }

    private static char? DetectDecimalSeparator(string clean)
    {
        var commaCount = clean.Count(c => c == ',');
        var dotCount = clean.Count(c => c == '.');
        if (commaCount == 0 && dotCount == 0)
            return null;

        if (commaCount > 0 && dotCount > 0)
            return clean.LastIndexOf(',') > clean.LastIndexOf('.') ? ',' : '.';

        var separator = commaCount > 0 ? ',' : '.';
        var parts = clean.Split(separator);
        if (parts.Length > 1 &&
            parts[0].Length is >= 1 and <= 3 &&
            parts.Skip(1).All(part => part.Length == 3))
        {
            return null;
        }

        return separator;
    }
}
