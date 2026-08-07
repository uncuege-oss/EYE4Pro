using Kst.Acoustics.Metraj.Io;
using Kst.Acoustics.Metraj.Models;
using Kst.Acoustics.Metraj.Counting;
using Kst.Acoustics.Metraj.Metrics;
using Kst.Acoustics.Metraj.Reporting;
using Kst.Acoustics.Metraj.Template;
using Kst.Acoustics.Metraj.Filters;
using Kst.Acoustics.Metraj.Rules;
using netDxf;
using netDxf.Entities;
using netDxf.Blocks;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kst.Acoustics.Metraj.Tests;

/// <summary>
/// Full pipeline integration test: DXF → Load → Filter → Count → Match → XLSX
/// ODA Converter is NOT required. This validates everything except DWG→DXF conversion.
/// </summary>
public class EndToEndPipelineTests
{
    [Fact]
    public void FullPipeline_MixedEntities_ProducesCorrectXlsx()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "KstMetraj_E2E_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            // === 1. Create a realistic project DXF ===
            var projectDoc = new DxfDocument();

            // Layer setup
            var wallLayer = new netDxf.Tables.Layer("WALL") { Color = new netDxf.AciColor(1) }; // Red
            var doorLayer = new netDxf.Tables.Layer("DOOR") { Color = new netDxf.AciColor(3) }; // Green
            var elecLayer = new netDxf.Tables.Layer("ELEC") { Color = new netDxf.AciColor(5) }; // Blue
            projectDoc.Layers.Add(wallLayer);
            projectDoc.Layers.Add(doorLayer);
            projectDoc.Layers.Add(elecLayer);

            // Walls (lines on WALL layer)
            projectDoc.Entities.Add(new Line(new Vector3(0,0,0), new Vector3(100,0,0)) { Layer = wallLayer });
            projectDoc.Entities.Add(new Line(new Vector3(100,0,0), new Vector3(100,50,0)) { Layer = wallLayer });
            projectDoc.Entities.Add(new Line(new Vector3(100,50,0), new Vector3(0,50,0)) { Layer = wallLayer });
            projectDoc.Entities.Add(new Line(new Vector3(0,50,0), new Vector3(0,0,0)) { Layer = wallLayer });

            // Door block (INSERT on DOOR layer)
            var doorBlock = new Block("KAPI_90");
            doorBlock.Entities.Add(new Line(Vector3.Zero, new Vector3(9, 0, 0)));
            doorBlock.Entities.Add(new Arc(Vector3.Zero, 9, 0, 90));
            projectDoc.Blocks.Add(doorBlock);
            projectDoc.Entities.Add(new Insert(doorBlock, new Vector3(30, 0, 0)) { Layer = doorLayer });
            projectDoc.Entities.Add(new Insert(doorBlock, new Vector3(70, 0, 0)) { Layer = doorLayer });

            // Electrical symbols (INSERT on ELEC layer)
            var switchBlock = new Block("ANAHTAR");
            switchBlock.Entities.Add(new Circle(Vector3.Zero, 2));
            projectDoc.Blocks.Add(switchBlock);
            projectDoc.Entities.Add(new Insert(switchBlock, new Vector3(10, 25, 0)) { Layer = elecLayer });
            projectDoc.Entities.Add(new Insert(switchBlock, new Vector3(50, 25, 0)) { Layer = elecLayer });
            projectDoc.Entities.Add(new Insert(switchBlock, new Vector3(90, 25, 0)) { Layer = elecLayer });

            // Exploded L-shape on WALL layer (should match L template)
            projectDoc.Entities.Add(new Line(new Vector3(200,0,0), new Vector3(210,0,0)) { Layer = wallLayer });
            projectDoc.Entities.Add(new Line(new Vector3(210,0,0), new Vector3(210,5,0)) { Layer = wallLayer });

            string projectPath = Path.Combine(tempDir, "test_project.dxf");
            projectDoc.Save(projectPath);

            // === 2. Create a template DXF (L-shape) ===
            var templateDoc = new DxfDocument();
            templateDoc.Entities.Add(new Line(new Vector3(0,0,0), new Vector3(10,0,0)));
            templateDoc.Entities.Add(new Line(new Vector3(10,0,0), new Vector3(10,5,0)));
            string templatePath = Path.Combine(tempDir, "L-Shape.dxf");
            templateDoc.Save(templatePath);

            // === 3. Load project ===
            var loader = new DxfDocumentLoader();
            var project = loader.Load(projectPath);

            Assert.NotNull(project);
            Assert.True(project.Entities.Count > 0, "Entities loaded");
            Assert.True(project.AllInserts.Count > 0, "Inserts flattened");

            // === 4. Block counting ===
            var blockCounter = new BlockCounter();
            var blockCounts = blockCounter.Count(project);

            var doorKey = blockCounts.Keys.FirstOrDefault(k => k.BlockName == "KAPI_90");
            Assert.NotNull(doorKey);
            Assert.Equal(2, blockCounts[doorKey]);

            var switchKey = blockCounts.Keys.FirstOrDefault(k => k.BlockName == "ANAHTAR");
            Assert.NotNull(switchKey);
            Assert.Equal(3, blockCounts[switchKey]);

            // === 5. Template matching (exploded L-shape) ===
            var lib = new TemplateLibrary();
            // Register L-shape template with explicit points (same geometry as template DXF)
            var lPoints = new[] { new MetrajPoint(0,0,0), new MetrajPoint(10,0,0), new MetrajPoint(10,5,0) };
            lib.AddTemplate("L-Shape", lPoints);

            var matcher = new TemplateMatcher(lib);
            var matches = matcher.Match(project.Entities);
            int lMatchCount = matches.Where(m => m.TemplateName == "L-Shape").Sum(m => m.Count);
            Assert.True(lMatchCount >= 1, $"Expected ≥1 L-Shape match, got {lMatchCount}");

            // === 6. Length calculation ===
            var lengthCalc = new LengthCalculator();
            var layerLengths = lengthCalc.CalculateByLayer(project.Entities);
            Assert.True(layerLengths.ContainsKey("WALL"), "WALL layer should have length");
            Assert.True(layerLengths["WALL"] > 0, "WALL length should be > 0");

            var colorLengths = lengthCalc.CalculateByColor(project.Entities);
            Assert.True(colorLengths.Count > 0, "Should have color-based lengths");

            // === 7. Filter test ===
            var wallFilter = new LayerFilter("WALL", false);
            var wallEntities = project.Entities.Where(wallFilter.IsMatch).ToList();
            Assert.True(wallEntities.Count >= 4, "At least 4 wall lines");
            Assert.True(wallEntities.All(e => e.Layer == "WALL"));

            // === 8. XLSX export ===
            var reportData = new MetrajReportData
            {
                ProjectName = "EndToEnd Test",
                ExportDate = DateTime.Now
            };
            foreach (var layer in layerLengths)
            {
                int count = blockCounts.Where(c => c.Key.LayerName == layer.Key).Sum(c => c.Value);
                reportData.LayerResults.Add(new MetrajReportRow
                {
                    Name = layer.Key,
                    Count = count,
                    Length = layer.Value,
                    Unit = "mm"
                });
            }
            foreach (var color in colorLengths)
            {
                reportData.ColorResults.Add(new MetrajReportRow
                {
                    Name = color.Key,
                    Count = project.Entities.Count(e => {
                        var c = e.EffectiveColor ?? e.Color;
                        string key = c.R.HasValue ? $"#{c.R:X2}{c.G:X2}{c.B:X2}" : c.Aci.ToString();
                        return key == color.Key;
                    }),
                    Length = color.Value,
                    Unit = "mm"
                });
            }
            reportData.TemplateResults["KAPI_90"] = 2;
            reportData.TemplateResults["ANAHTAR"] = 3;
            reportData.TemplateResults["L-Shape"] = lMatchCount;

            string xlsxPath = Path.Combine(tempDir, "result.xlsx");
            var exporter = new XlsxExporter();
            exporter.Export(xlsxPath, reportData);

            Assert.True(File.Exists(xlsxPath), "XLSX should be created");
            Assert.True(new FileInfo(xlsxPath).Length > 0, "XLSX should not be empty");

            // === 9. RuleSet serialization roundtrip ===
            var ruleSet = new RuleSet
            {
                LengthTolerance = 2.5,
                AngleTolerance = 3.0,
                TargetUnit = MetrajUnit.Meters,
                RulesCombinator = FilterLogic.And
            };
            ruleSet.Rules.Add(new MetrajRule { Name = "Duvar", LayerPattern = "WALL.*", ColorAci = 1 });
            ruleSet.Rules.Add(new MetrajRule { Name = "RGB Test", ColorR = 255, ColorG = 128, ColorB = 0 });

            string json = ruleSet.ToJson();
            var deserialized = RuleSet.FromJson(json);
            Assert.NotNull(deserialized);
            Assert.Equal(2.5, deserialized.LengthTolerance);
            Assert.Equal(FilterLogic.And, deserialized.RulesCombinator);
            Assert.Equal(2, deserialized.Rules.Count);
            Assert.Equal((byte?)255, deserialized.Rules[1].ColorR);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    private IEnumerable<MetrajPoint> ExtractPoints(MetrajEntity entity)
    {
        if (entity is MetrajLine l) return new[] { l.Start, l.End };
        if (entity is MetrajPolyline p) return p.Vertices;
        return Enumerable.Empty<MetrajPoint>();
    }
}

/// <summary>
/// Manuel smoke test — yalnızca ortam değişkenleri ayarlıyken çalışır.
///
/// Kullanım (Windows):
///   set METRAJ_TEST_DWG=C:\path\to\test.dwg
///   dotnet test --filter ManualSmokeTest
///
/// Kullanım (macOS/Linux):
///   METRAJ_TEST_DWG=/path/to/test.dwg dotnet test --filter ManualSmokeTest
///
/// Faz 7 Publish Öncesi Manuel Doğrulama Kontrol Listesi:
/// ─────────────────────────────────────────────────────────
/// [ ] 1. Gerçek bir DWG dosyası ile METRAJ_TEST_DWG ayarla, testi koş
/// [ ] 2. Üretilen DXF dosyasının entity sayısını kontrol et (>0)
/// [ ] 3. Şablon DXF ekle, "Çalıştır" ile XLSX üret
/// [ ] 4. XLSX'teki By-Layer, By-Color, By-Template sayfalarını elle doğrula
/// [ ] 5. Eksik Eşleşmeler sayfasının mevcut olup olmadığını kontrol et
/// [ ] 6. macOS ARM64 publish: dotnet publish -r osx-arm64 -c Release --self-contained
/// [ ] 7. Windows x64 publish: dotnet publish -r win-x64 -c Release --self-contained
/// [ ] 8. Her iki EXE'yi de çalıştır, UI açılışını ve ODA yol diyaloğunu doğrula
/// </summary>
public class ManualSmokeTest
{
    [Fact]
    public async Task ODAConverter_RealDwg_FullPipelineValidation()
    {
        string? dwgPath = Environment.GetEnvironmentVariable("METRAJ_TEST_DWG");
        if (string.IsNullOrEmpty(dwgPath) || !File.Exists(dwgPath))
        {
            // Ortam değişkeni yoksa testi atla (başarılı say)
            return;
        }

        string tempDir = Path.Combine(Path.GetTempPath(), "KstMetraj_Smoke_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            // Step 1: ODA Converter DWG → DXF
            var config = Kst.Acoustics.Metraj.Config.ConfigStore.Load();
            var converter = new DwgToDxfConverter(config.OdaConverterPath);

            Assert.True(converter.IsConverterAvailable(),
                $"ODA Converter bulunamadı: {converter.ConverterPath}");

            string dxfPath = await converter.ConvertAsync(dwgPath);
            Assert.True(File.Exists(dxfPath), "DXF dosyası oluşturulamadı.");
            Assert.True(new FileInfo(dxfPath).Length > 0, "Oluşturulan DXF dosyası boş.");

            // Step 2: Loader
            var loader = new DxfDocumentLoader();
            var project = loader.Load(dxfPath);
            Assert.NotNull(project);
            Assert.NotEmpty(project.Entities);

            // Step 3: Block Counting
            var blockCounter = new BlockCounter();
            var blockCounts = blockCounter.Count(project);

            // Step 4: Length Calculation
            var lengthCalc = new LengthCalculator();
            var layerLengths = lengthCalc.CalculateByLayer(project.Entities);
            var colorLengths = lengthCalc.CalculateByColor(project.Entities);

            // Step 5: XLSX Export
            var reportData = new MetrajReportData
            {
                ProjectName = Path.GetFileName(dwgPath),
                ExportDate = DateTime.Now
            };
            foreach (var layer in layerLengths)
            {
                reportData.LayerResults.Add(new MetrajReportRow
                {
                    Name = layer.Key,
                    Count = blockCounts.Where(c => c.Key.LayerName == layer.Key).Sum(c => c.Value),
                    Length = layer.Value,
                    Unit = "mm"
                });
            }
            foreach (var color in colorLengths)
            {
                reportData.ColorResults.Add(new MetrajReportRow
                {
                    Name = color.Key,
                    Count = project.Entities.Count(e => {
                        var c = e.EffectiveColor ?? e.Color;
                        return (c.R.HasValue ? $"#{c.R:X2}{c.G:X2}{c.B:X2}" : c.Aci.ToString()) == color.Key;
                    }),
                    Length = color.Value,
                    Unit = "mm"
                });
            }
            foreach (var kv in blockCounts)
            {
                if (reportData.TemplateResults.ContainsKey(kv.Key.BlockName)) reportData.TemplateResults[kv.Key.BlockName] += kv.Value;
                else reportData.TemplateResults[kv.Key.BlockName] = kv.Value;
            }

            string xlsxPath = Path.Combine(tempDir, Path.GetFileNameWithoutExtension(dwgPath) + "_Metraj.xlsx");
            var exporter = new XlsxExporter();
            exporter.Export(xlsxPath, reportData);

            Assert.True(File.Exists(xlsxPath), "XLSX raporu oluşturulamadı.");
            Assert.True(new FileInfo(xlsxPath).Length > 1024, "XLSX dosyası çok küçük, muhtemelen boş.");

            // Summary output for manual inspection
            System.Console.WriteLine($"=== METRAJ SMOKE TEST SONUÇLARI ===");
            System.Console.WriteLine($"Girdi : {dwgPath}");
            System.Console.WriteLine($"DXF   : {dxfPath} ({new FileInfo(dxfPath).Length / 1024} KB)");
            System.Console.WriteLine($"Entity: {project.Entities.Count}");
            System.Console.WriteLine($"Insert: {project.AllInserts.Count}");
            System.Console.WriteLine($"Layer : {layerLengths.Count} adet");
            System.Console.WriteLine($"Blok  : {blockCounts.Count} tip, {blockCounts.Values.Sum()} toplam");
            System.Console.WriteLine($"XLSX  : {xlsxPath} ({new FileInfo(xlsxPath).Length / 1024} KB)");
            System.Console.WriteLine($"===================================");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
