using ClosedXML.Excel;
using System.Globalization;
using System.Text;

namespace Kst.Acoustics.Core.Electric.KesifHazirla;

public static class KesifPozImportServisi
{
    private static readonly string[] AdBasliklari = ["birimadi", "imalatadi", "imalatincinsi", "tanim", "tanimi", "pozadi", "aciklama"];
    private static readonly string[] FiyatBasliklari = ["birimfiyat", "birimfiyati", "fiyat"];

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
        var result = new List<KesifPoz>();
        var baslikBulundu = false;
        foreach (var sheet in workbook.Worksheets)
        {
            Dictionary<string, int>? headers = null;
            var baslikSatiri = 0;
            foreach (var row in sheet.RowsUsed().Take(100))
            {
                var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var cell in row.CellsUsed())
                    map.TryAdd(NormalizeHeader(cell.GetFormattedString()), cell.Address.ColumnNumber);
                if (!GecerliBasliklar(map)) continue;
                headers = map;
                baslikSatiri = row.RowNumber();
                break;
            }
            if (headers is null) continue; // Kapak/açıklama sayfaları veri sayfası değildir.
            baslikBulundu = true;
            var kategori = "";
            foreach (var row in sheet.RowsUsed().Where(r => r.RowNumber() > baslikSatiri))
            {
                // Yaklaşık maliyet cetvellerindeki birleştirilmiş iş grubu satırları.
                var grup = row.CellsUsed().Select(c => c.GetFormattedString().Trim())
                    .FirstOrDefault(v => NormalizeHeader(v).StartsWith("isgrubu", StringComparison.Ordinal));
                if (grup is not null)
                {
                    kategori = grup.Contains(':') ? grup[(grup.IndexOf(':') + 1)..].Trim() : grup;
                    continue;
                }
                var pozNo = ReadCell(row, headers, "pozno");
                var birimAdi = ReadCell(row, headers, AdBasliklari);
                var birim = ReadCell(row, headers, "birim");
                if (string.IsNullOrWhiteSpace(pozNo) && string.IsNullOrWhiteSpace(birimAdi)) continue;
                if (NormalizeHeader(pozNo) == "pozno" && AdBasliklari.Contains(NormalizeHeader(birimAdi))) continue;
                var satirKategorisi = ReadCell(row, headers, "kategori");
                result.Add(new KesifPoz
                {
                    PozNo = pozNo,
                    BirimAdi = birimAdi,
                    Birim = string.IsNullOrWhiteSpace(birim) ? "adet" : birim,
                    BirimFiyat = ReadDecimal(row, headers, FiyatBasliklari),
                    Aciklama = ReadCell(row, headers, "not"),
                    Kategori = string.IsNullOrWhiteSpace(satirKategorisi) ? kategori : satirKategorisi
                });
            }
        }
        if (!baslikBulundu) throw BaslikHatasi();
        return result;
    }

    private static bool GecerliBasliklar(Dictionary<string, int> headers)
        => AdBasliklari.Any(headers.ContainsKey) && FiyatBasliklari.Any(headers.ContainsKey)
           && (headers.ContainsKey("pozno") || headers.ContainsKey("birim"));

    private static InvalidOperationException BaslikHatasi()
        => new("Poz başlıkları bulunamadı. Poz No, İmalat Adı (veya İmalatın Cinsi), Birim ve Birim Fiyat kolonlarını kontrol edin.");

    private static IReadOnlyList<KesifPoz> AyracliMetindenOku(string path, char? delimiter)
    {
        var lines = AyracliKayitlariOku(File.ReadAllText(path, Encoding.UTF8)).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        if (lines.Count == 0)
            return [];

        var effectiveDelimiter = delimiter ?? DetectDelimiter(lines[0]);
        var headers = HeaderMap(ParseDelimitedLine(lines[0], effectiveDelimiter));
        if (!GecerliBasliklar(headers)) throw BaslikHatasi();
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
            var birimAdi = Read(AdBasliklari);
            if (string.IsNullOrWhiteSpace(pozNo) && string.IsNullOrWhiteSpace(birimAdi))
                continue;

            result.Add(new KesifPoz
            {
                PozNo = pozNo,
                BirimAdi = birimAdi,
                Birim = string.IsNullOrWhiteSpace(Read("birim")) ? "adet" : Read("birim"),
                BirimFiyat = ParseDecimal(Read(FiyatBasliklari)),
                Aciklama = Read("not"),
                Kategori = Read("kategori")
            });
        }

        return result;
    }

    private static IEnumerable<string> AyracliKayitlariOku(string text)
    {
        var baslangic = 0;
        var tirnakIcinde = false;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '"')
            {
                if (tirnakIcinde && i + 1 < text.Length && text[i + 1] == '"') i++;
                else tirnakIcinde = !tirnakIcinde;
            }
            else if (!tirnakIcinde && text[i] is '\r' or '\n')
            {
                yield return text[baslangic..i];
                if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                baslangic = i + 1;
            }
        }
        if (tirnakIcinde) throw new InvalidOperationException("CSV dosyasında kapanmamış tırnaklı alan var.");
        if (baslangic < text.Length) yield return text[baslangic..];
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
        var text = value.Trim().Replace('İ', 'I').ToLowerInvariant()
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
