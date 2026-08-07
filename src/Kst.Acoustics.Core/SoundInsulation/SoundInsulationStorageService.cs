using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kst.Acoustics.Core.SoundInsulation;

/// <summary>
/// Service for saving and loading wall assemblies and materials as JSON.
/// </summary>
public static class SoundInsulationStorageService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string SerializeAssemblies(List<WallAssembly> assemblies)
    {
        return JsonSerializer.Serialize(assemblies, Options);
    }

    public static List<WallAssembly>? DeserializeAssemblies(string json)
    {
        return JsonSerializer.Deserialize<List<WallAssembly>>(json, Options);
    }

    public static void SaveToFile(List<WallAssembly> assemblies, string filePath)
    {
        string json = SerializeAssemblies(assemblies);
        File.WriteAllText(filePath, json);
    }

    public static List<WallAssembly> LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath)) return new List<WallAssembly>();
        string json = File.ReadAllText(filePath);
        return DeserializeAssemblies(json) ?? new List<WallAssembly>();
    }

    public static void ExportMaterialsToCsv(List<SoundInsulationMaterial> materials, string filePath)
    {
        using var writer = new StreamWriter(filePath);
        writer.WriteLine("Id;Name;Category;SubCategory;Density;YoungModulus;LossFactor;FlowResistivity");
        foreach (var m in materials)
        {
            writer.WriteLine(string.Join(";",
                EscapeCsv(m.Id),
                EscapeCsv(m.Name),
                EscapeCsv(m.Category),
                EscapeCsv(m.SubCategory),
                m.Density.ToString(CultureInfo.InvariantCulture),
                m.YoungModulus.ToString(CultureInfo.InvariantCulture),
                m.LossFactor.ToString(CultureInfo.InvariantCulture),
                m.FlowResistivity.ToString(CultureInfo.InvariantCulture)));
        }
    }

    public static List<SoundInsulationMaterial> ImportMaterialsFromCsv(string filePath)
    {
        var materials = new List<SoundInsulationMaterial>();
        if (!File.Exists(filePath)) return materials;

        var lines = File.ReadAllLines(filePath);
        if (lines.Length <= 1) return materials;

        for (int i = 1; i < lines.Length; i++)
        {
            var parts = SplitCsvLine(lines[i]);
            if (parts.Length < 8) continue;
            if (!double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var density)) continue;
            if (!double.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var youngModulus)) continue;
            if (!double.TryParse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture, out var lossFactor)) continue;
            if (!double.TryParse(parts[7], NumberStyles.Float, CultureInfo.InvariantCulture, out var flowResistivity)) continue;

            materials.Add(new SoundInsulationMaterial(
                id: parts[0],
                name: parts[1],
                category: parts[2],
                subCategory: parts[3],
                density: density,
                youngModulus: youngModulus,
                lossFactor: lossFactor,
                flowResistivity: flowResistivity
            ));
        }
        return materials;
    }

    private static string EscapeCsv(string value)
    {
        if (!value.Contains(';') && !value.Contains('"') && !value.Contains('\n')) return value;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string[] SplitCsvLine(string line)
    {
        var values = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
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
            else if (ch == ';' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }
        values.Add(current.ToString());
        return values.ToArray();
    }
}
