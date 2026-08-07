using System.Globalization;
using System.Text;
using ClosedXML.Excel;

namespace Kst.Acoustics.Core.Projects;

internal static class MasterMaterialLibraryLoader
{
    private const string FileName = "akustik_malzeme_kutuphanesi_master_tekil.xlsx";

    public static IReadOnlyList<AcousticMaterial> Load()
    {
        var path = FindDataFile();
        if (path is null) return [];

        try
        {
            using var wb = new XLWorkbook(path);
            var ws = wb.TryGetWorksheet("Malzeme_Kutuphanesi", out var named)
                ? named
                : wb.Worksheets.FirstOrDefault();
            if (ws is null) return [];

            var used = ws.LastRowUsed()?.RowNumber() ?? 1;
            var materials = new List<AcousticMaterial>();
            var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var row = 2; row <= used; row++)
            {
                var name = ReadString(ws, row, 1);
                if (string.IsNullOrWhiteSpace(name)) continue;

                var alpha = new double[8];
                var valid = true;
                for (var i = 0; i < 8; i++)
                {
                    if (!TryReadDouble(ws, row, 2 + i, out alpha[i]))
                    {
                        valid = false;
                        break;
                    }
                    alpha[i] = Math.Clamp(alpha[i], 0.0, 1.0);
                }
                if (!valid) continue;

                var scattering = TryReadDouble(ws, row, 10, out var s)
                    ? Math.Clamp(s, 0.0, 1.0)
                    : 0.05;

                var id = UniqueId("master_" + Slugify(name), usedIds);
                materials.Add(new AcousticMaterial(
                    id,
                    name,
                    alpha,
                    scattering,
                    ReadString(ws, row, 12),
                    ReadString(ws, row, 13),
                    ReadString(ws, row, 14),
                    ReadString(ws, row, 11)));
            }

            return materials;
        }
        catch
        {
            return [];
        }
    }

    private static string? FindDataFile()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "Data", FileName),
            Path.Combine(baseDir, FileName)
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate)) return candidate;
        }

        var dir = new DirectoryInfo(baseDir);
        for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            var sourceCandidate = Path.Combine(dir.FullName, "Data", FileName);
            if (File.Exists(sourceCandidate)) return sourceCandidate;
        }

        return null;
    }

    private static string ReadString(IXLWorksheet ws, int row, int col) =>
        ws.Cell(row, col).GetValue<string>().Trim();

    private static bool TryReadDouble(IXLWorksheet ws, int row, int col, out double value)
    {
        var cell = ws.Cell(row, col);
        if (cell.TryGetValue<double>(out value)) return true;

        var text = cell.GetValue<string>().Trim().Replace(',', '.');
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static string UniqueId(string baseId, HashSet<string> usedIds)
    {
        var id = string.IsNullOrWhiteSpace(baseId) ? "master_material" : baseId;
        var candidate = id;
        var suffix = 2;
        while (!usedIds.Add(candidate))
        {
            candidate = $"{id}_{suffix++}";
        }

        return candidate;
    }

    private static string Slugify(string text)
    {
        var normalized = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        var lastWasSeparator = false;

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark) continue;

            var c = ch switch
            {
                'ı' => 'i',
                'ş' => 's',
                'ğ' => 'g',
                'ü' => 'u',
                'ö' => 'o',
                'ç' => 'c',
                _ => ch
            };

            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                lastWasSeparator = false;
            }
            else if (!lastWasSeparator)
            {
                sb.Append('_');
                lastWasSeparator = true;
            }
        }

        return sb.ToString().Trim('_');
    }
}
