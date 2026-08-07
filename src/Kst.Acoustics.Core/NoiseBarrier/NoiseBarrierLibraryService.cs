using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ClosedXML.Excel;

namespace Kst.Acoustics.Core.NoiseBarrier;

public static class NoiseBarrierLibraryService
{
    private static readonly string LibraryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "KstAcoustics", "noise_barrier_library.json");

    public static string? LastError { get; private set; }

    public static List<NoiseBarrierMaterial> GetBuiltInMaterials()
    {
        return new List<NoiseBarrierMaterial>
        {
            new() { Id = "concrete_std", Name = "Standart Beton Panel", Category = "Beton", ThicknessMm = 150, SurfaceMass = 350, Rw = 52, Nrc = 0.05, Source = "Built-in", Confidence = 95 },
            new() { Id = "metal_absorptive", Name = "Absorptif Metal Panel", Category = "Metal", ThicknessMm = 100, SurfaceMass = 25, Rw = 32, Nrc = 0.85, Source = "Built-in", Confidence = 90 },
            new() { Id = "acrylic_transparent", Name = "Akrilik Şeffaf Bariyer", Category = "Cam/Plastik", ThicknessMm = 20, SurfaceMass = 24, Rw = 30, Nrc = 0.05, Source = "Built-in", Confidence = 90 }
        }.Select(m => {
            foreach(var f in new double[]{63,125,250,500,1000,2000,4000,8000}) {
                m.TransmissionLoss[f] = m.Rw;
                m.AbsorptionCoefficients[f] = m.Nrc;
            }
            return m;
        }).ToList();
    }

    public static List<NoiseBarrierMaterial> LoadLibrary()
    {
        var builtIn = GetBuiltInMaterials();
        LastError = null;
        if (!File.Exists(LibraryPath)) return builtIn;

        try
        {
            var json = File.ReadAllText(LibraryPath);
            var userMaterials = JsonSerializer.Deserialize<List<NoiseBarrierMaterial>>(json) ?? new();
            
            var library = new List<NoiseBarrierMaterial>(builtIn);
            foreach (var um in userMaterials)
            {
                var existing = library.Find(m => m.Id == um.Id);
                if (existing != null) library.Remove(existing);
                library.Add(um);
            }
            return library;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return builtIn;
        }
    }

    public static void SaveLibrary(List<NoiseBarrierMaterial> materials)
    {
        LastError = null;
        try
        {
            var dir = Path.GetDirectoryName(LibraryPath);
            if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            
            var json = JsonSerializer.Serialize(materials, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(LibraryPath, json);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
    }

    public static void ExportToExcel(string filePath, List<NoiseBarrierMaterial> materials)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("NoiseBarrierMaterials");

        ws.Cell(1, 1).Value = "Id";
        ws.Cell(1, 2).Value = "Ad";
        ws.Cell(1, 3).Value = "Kategori";
        ws.Cell(1, 4).Value = "Rw (dB)";
        ws.Cell(1, 5).Value = "NRC";
        ws.Cell(1, 6).Value = "Kalınlık mm";
        ws.Cell(1, 7).Value = "Yüzey Kütlesi kg/m2";
        ws.Cell(1, 8).Value = "Kaynak";
        ws.Cell(1, 9).Value = "Güven %";
        ws.Cell(1, 10).Value = "TL 63Hz";
        ws.Cell(1, 11).Value = "TL 125Hz";
        ws.Cell(1, 12).Value = "TL 250Hz";
        ws.Cell(1, 13).Value = "TL 500Hz";
        ws.Cell(1, 14).Value = "TL 1kHz";
        ws.Cell(1, 15).Value = "TL 2kHz";
        ws.Cell(1, 16).Value = "TL 4kHz";
        ws.Cell(1, 17).Value = "TL 8kHz";
        ws.Cell(1, 18).Value = "Alpha 63Hz";
        ws.Cell(1, 19).Value = "Alpha 125Hz";
        ws.Cell(1, 20).Value = "Alpha 250Hz";
        ws.Cell(1, 21).Value = "Alpha 500Hz";
        ws.Cell(1, 22).Value = "Alpha 1kHz";
        ws.Cell(1, 23).Value = "Alpha 2kHz";
        ws.Cell(1, 24).Value = "Alpha 4kHz";
        ws.Cell(1, 25).Value = "Alpha 8kHz";

        var headerRange = ws.Range(1, 1, 1, 25);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;

        for (int i = 0; i < materials.Count; i++)
        {
            var row = i + 2;
            var material = materials[i];
            ws.Cell(row, 1).Value = material.Id;
            ws.Cell(row, 2).Value = material.Name;
            ws.Cell(row, 3).Value = material.Category;
            ws.Cell(row, 4).Value = material.Rw;
            ws.Cell(row, 5).Value = material.Nrc;
            ws.Cell(row, 6).Value = material.ThicknessMm;
            ws.Cell(row, 7).Value = material.SurfaceMass;
            ws.Cell(row, 8).Value = material.Source;
            ws.Cell(row, 9).Value = material.Confidence;
            ws.Cell(row, 10).Value = material.Tl63;
            ws.Cell(row, 11).Value = material.Tl125;
            ws.Cell(row, 12).Value = material.Tl250;
            ws.Cell(row, 13).Value = material.Tl500;
            ws.Cell(row, 14).Value = material.Tl1k;
            ws.Cell(row, 15).Value = material.Tl2k;
            ws.Cell(row, 16).Value = material.Tl4k;
            ws.Cell(row, 17).Value = material.Tl8k;
            ws.Cell(row, 18).Value = material.Alpha63;
            ws.Cell(row, 19).Value = material.Alpha125;
            ws.Cell(row, 20).Value = material.Alpha250;
            ws.Cell(row, 21).Value = material.Alpha500;
            ws.Cell(row, 22).Value = material.Alpha1k;
            ws.Cell(row, 23).Value = material.Alpha2k;
            ws.Cell(row, 24).Value = material.Alpha4k;
            ws.Cell(row, 25).Value = material.Alpha8k;
        }

        ws.Columns().AdjustToContents();
        workbook.SaveAs(filePath);
    }

    public static List<NoiseBarrierMaterial> ImportFromExcel(string filePath)
    {
        var materials = new List<NoiseBarrierMaterial>();
        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheet(1);
        var usedRange = worksheet.RangeUsed();
        if (usedRange is null) return materials;

        foreach (var row in usedRange.RowsUsed().Skip(1))
        {
            var id = row.Cell(1).GetString().Trim();
            var name = row.Cell(2).GetString().Trim();
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                continue;

            var hasExtendedColumns = usedRange.ColumnCount() >= 25;
            var tlStart = hasExtendedColumns ? 10 : 6;
            var alphaStart = hasExtendedColumns ? 18 : 14;

            var material = new NoiseBarrierMaterial
            {
                Id = row.Cell(1).GetValue<string>(),
                Name = row.Cell(2).GetValue<string>(),
                Category = row.Cell(3).GetValue<string>(),
                Rw = row.Cell(4).GetValue<double>(),
                Nrc = row.Cell(5).GetValue<double>(),
                ThicknessMm = hasExtendedColumns ? GetDouble(row.Cell(6), 0) : 0,
                SurfaceMass = hasExtendedColumns ? GetDouble(row.Cell(7), 0) : 0,
                Source = hasExtendedColumns ? row.Cell(8).GetString() : string.Empty,
                Confidence = hasExtendedColumns ? (int)Math.Clamp(GetDouble(row.Cell(9), 100), 0, 100) : 100,
                Tl63 = GetDouble(row.Cell(tlStart), 0),
                Tl125 = GetDouble(row.Cell(tlStart + 1), 0),
                Tl250 = GetDouble(row.Cell(tlStart + 2), 0),
                Tl500 = GetDouble(row.Cell(tlStart + 3), 0),
                Tl1k = GetDouble(row.Cell(tlStart + 4), 0),
                Tl2k = GetDouble(row.Cell(tlStart + 5), 0),
                Tl4k = GetDouble(row.Cell(tlStart + 6), 0),
                Tl8k = GetDouble(row.Cell(tlStart + 7), 0),
                Alpha63 = GetDouble(row.Cell(alphaStart), 0),
                Alpha125 = GetDouble(row.Cell(alphaStart + 1), 0),
                Alpha250 = GetDouble(row.Cell(alphaStart + 2), 0),
                Alpha500 = GetDouble(row.Cell(alphaStart + 3), 0),
                Alpha1k = GetDouble(row.Cell(alphaStart + 4), 0),
                Alpha2k = GetDouble(row.Cell(alphaStart + 5), 0),
                Alpha4k = GetDouble(row.Cell(alphaStart + 6), 0),
                Alpha8k = GetDouble(row.Cell(alphaStart + 7), 0)
            };

            materials.Add(material);
        }

        return materials;
    }

    public static void GenerateExcelTemplate(string filePath)
    {
        ExportToExcel(filePath, new List<NoiseBarrierMaterial>
        {
            new()
            {
                Id = "custom_barrier_001",
                Name = "Örnek Bariyer Paneli",
                Category = "Özel",
                ThicknessMm = 100,
                SurfaceMass = 35,
                Rw = 32,
                Nrc = 0.65,
                Tl63 = 18,
                Tl125 = 22,
                Tl250 = 27,
                Tl500 = 32,
                Tl1k = 36,
                Tl2k = 40,
                Tl4k = 43,
                Tl8k = 45,
                Alpha63 = 0.15,
                Alpha125 = 0.30,
                Alpha250 = 0.55,
                Alpha500 = 0.75,
                Alpha1k = 0.85,
                Alpha2k = 0.80,
                Alpha4k = 0.70,
                Alpha8k = 0.60,
                Source = "Şablon",
                Confidence = 80
            }
        });
    }

    private static double GetDouble(IXLCell cell, double fallback = 0)
    {
        return cell.TryGetValue<double>(out var value) ? value : fallback;
    }
}
