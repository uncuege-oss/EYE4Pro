using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ClosedXML.Excel;

namespace Kst.Acoustics.Core.SoundInsulation;

public static class SoundInsulationLibraryService
{
    private static readonly string LibraryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
        "EYE4PRO", "Acoustics", "materials_insulation.json");

    public static List<SoundInsulationMaterial> LoadLibrary()
    {
        var builtIn = SoundInsulationLibrary.GetBuiltInMaterials();
        foreach (var m in builtIn) m.IsBuiltIn = true;

        if (!File.Exists(LibraryPath))
        {
            return builtIn;
        }

        try
        {
            var json = File.ReadAllText(LibraryPath);
            var userMaterials = JsonSerializer.Deserialize<List<SoundInsulationMaterial>>(json) ?? new();

            // Merge: built-in base, user materials override by ID
            var library = new List<SoundInsulationMaterial>(builtIn);
            foreach (var userMat in userMaterials)
            {
                var existing = library.FirstOrDefault(m => m.Id == userMat.Id);
                if (existing != null)
                    library.Remove(existing);
                library.Add(userMat);
            }
            return library;
        }
        catch
        {
            return builtIn;
        }
    }

    public static void SaveLibrary(List<SoundInsulationMaterial> materials)
    {
        try
        {
            var dir = Path.GetDirectoryName(LibraryPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);

            var json = JsonSerializer.Serialize(materials, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(LibraryPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Library Save Error: {ex.Message}");
        }
    }

    public static void ExportToExcel(string filePath, List<SoundInsulationMaterial> materials)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Materials");

        // Headers
        worksheet.Cell(1, 1).Value = "Id";
        worksheet.Cell(1, 2).Value = "Name";
        worksheet.Cell(1, 3).Value = "Category";
        worksheet.Cell(1, 4).Value = "SubCategory";
        worksheet.Cell(1, 5).Value = "Density (kg/m3)";
        worksheet.Cell(1, 6).Value = "YoungModulus (Pa)";
        worksheet.Cell(1, 7).Value = "PoissonRatio";
        worksheet.Cell(1, 8).Value = "LossFactor";
        worksheet.Cell(1, 9).Value = "FlowResistivity";
        worksheet.Cell(1, 10).Value = "Porosity";
        worksheet.Cell(1, 11).Value = "Tortuosity";
        worksheet.Cell(1, 12).Value = "DynamicStiffness (MN/m3)";
        worksheet.Cell(1, 13).Value = "Manufacturer";
        worksheet.Cell(1, 14).Value = "Source";
        worksheet.Cell(1, 15).Value = "Reliability";
        worksheet.Cell(1, 16).Value = "Reference";
        worksheet.Cell(1, 17).Value = "Note";
        worksheet.Cell(1, 18).Value = "IsFavorite (TRUE/FALSE)";

        // Style header
        var range = worksheet.Range(1, 1, 1, 18);
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.LightBlue;

        for (int i = 0; i < materials.Count; i++)
        {
            var m = materials[i];
            int row = i + 2;
            worksheet.Cell(row, 1).Value = m.Id;
            worksheet.Cell(row, 2).Value = m.Name;
            worksheet.Cell(row, 3).Value = m.Category;
            worksheet.Cell(row, 4).Value = m.SubCategory;
            worksheet.Cell(row, 5).Value = m.Density;
            worksheet.Cell(row, 6).Value = m.YoungModulus;
            worksheet.Cell(row, 7).Value = m.PoissonRatio;
            worksheet.Cell(row, 8).Value = m.LossFactor;
            worksheet.Cell(row, 9).Value = m.FlowResistivity;
            worksheet.Cell(row, 10).Value = m.Porosity;
            worksheet.Cell(row, 11).Value = m.Tortuosity;
            worksheet.Cell(row, 12).Value = m.DynamicStiffness;
            worksheet.Cell(row, 13).Value = m.Manufacturer;
            worksheet.Cell(row, 14).Value = m.Source;
            worksheet.Cell(row, 15).Value = m.Reliability;
            worksheet.Cell(row, 16).Value = m.Reference;
            worksheet.Cell(row, 17).Value = m.Note;
            worksheet.Cell(row, 18).Value = m.IsFavorite;
        }

        workbook.SaveAs(filePath);
    }

    public static List<SoundInsulationMaterial> ImportFromExcel(string filePath)
    {
        var materials = new List<SoundInsulationMaterial>();
        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheet(1);
        var usedRange = worksheet.RangeUsed();
        if (usedRange is null) return materials;

        var rows = usedRange.RowsUsed().Skip(1); // Skip header

        foreach (var row in rows)
        {
            try
            {
                var m = new SoundInsulationMaterial
                {
                    Id = row.Cell(1).GetString(),
                    Name = row.Cell(2).GetString(),
                    Category = row.Cell(3).GetString(),
                    SubCategory = row.Cell(4).GetString(),
                    Density = row.Cell(5).GetDouble(),
                    YoungModulus = row.Cell(6).GetDouble(),
                    PoissonRatio = row.Cell(7).GetDouble(),
                    LossFactor = row.Cell(8).GetDouble(),
                    FlowResistivity = row.Cell(9).GetDouble(),
                    Porosity = TryGetDouble(row.Cell(10), 0.99),
                    Tortuosity = TryGetDouble(row.Cell(11), 1.0),
                    DynamicStiffness = TryGetDouble(row.Cell(12), 0),
                    Manufacturer = row.Cell(13).GetString(),
                    Source = string.IsNullOrWhiteSpace(row.Cell(14).GetString()) ? "User" : row.Cell(14).GetString(),
                    Reliability = string.IsNullOrWhiteSpace(row.Cell(15).GetString()) ? "Orta" : row.Cell(15).GetString(),
                    Reference = row.Cell(16).GetString(),
                    Note = row.Cell(17).GetString(),
                    IsFavorite = TryGetBoolean(row.Cell(18), false)
                };
                materials.Add(m);
            }
            catch { /* Skip invalid rows */ }
        }

        return materials;
    }

    public static void GenerateExcelTemplate(string filePath)
    {
        var samples = new List<SoundInsulationMaterial>
        {
            new("ID_001", "Örnek Malzeme", "Kategori", "Alt Kategori", 1000, 3e9, 0.2, 0.01, 0, false)
        };
        ExportToExcel(filePath, samples);
    }

    private static double TryGetDouble(IXLCell cell, double fallback)
    {
        try { return cell.GetDouble(); }
        catch { return fallback; }
    }

    private static bool TryGetBoolean(IXLCell cell, bool fallback)
    {
        try { return cell.GetBoolean(); }
        catch { return fallback; }
    }
}
