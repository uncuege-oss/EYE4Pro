using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Kst.Acoustics.Core.PanelDesign;
using Kst.Acoustics.Core.SoundInsulation;
using Kst.Acoustics.Core.ImpactSurface;
using Kst.Acoustics.Core.RoomModes;
using Kst.Acoustics.Core.Measurement.Data;
using Kst.Acoustics.Core.Electric.Calculation;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;
using Kst.Acoustics.Core.Electric.CevreAydinlatma;
using Kst.Acoustics.Core.Electric.ZayifAkimTekHat;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.NoiseBarrier;
using Kst.Acoustics.Core.Projects;
using Xunit;

namespace Kst.Acoustics.Tests;

internal static class ModuleAuditRegressionTests
{
    public static (string Name, Action Test)[] Cases =>
    [
        ("Module audit M01: grid stays inside requested bounds", GridStaysInsideBounds),
        ("Module audit M01: decimal grid includes exact endpoint", GridIncludesDecimalEndpoint),
        ("Module audit M02: invalid absorption cannot become class A", InvalidAbsorptionRejected),
        ("Module audit M03: multiline CSV material round trip", MultilineMaterialRoundTrip),
        ("Module audit M03/M07: overwritten projects retain backup", AcousticBackups),
        ("Module audit M04: disabled sources produce no-source warning", DisabledSourcesWarning),
        ("Module audit M05: nonfinite damping invalidates calculation", InvalidDampingRejected),
        ("Module audit M06: unbounded room calculation is rejected", UnboundedRoomRejected),
        ("Module audit M07: ragged measurement matrix is rejected", RaggedMatrixRejected),
        ("Module audit M07: rectangular measurement matrix round trip", MatrixRoundTrip),
        ("Module audit M11: large circuit protection matches available ratings", LargeCircuitProtection),
        ("Module audit M12/M16: isolated foreign content is detected", IsolatedDisciplineContent),
        ("Module audit M13: battery strings use nominal voltage", BatteryStrings),
        ("Module audit M15: invalid pole spacing is rejected", InvalidPoleSpacing),
        ("Module audit M15: invalid resistance cannot pass RCD check", InvalidResistance)
    ];

    private static void GridStaysInsideBounds()
    {
        var grid = new ReceiverGrid(1.2, 0, 1, 0, 1, 0.6);
        Assert.Equal(2, grid.Cols);
        Assert.Equal(2, grid.Rows);
        Assert.True(grid.XMin + (grid.Cols - 1) * grid.CellSize <= grid.XMax);
    }

    private static void GridIncludesDecimalEndpoint()
    {
        var grid = new ReceiverGrid(1.2, 0, 0.3, 0, 0.3, 0.1);
        Assert.Equal(4, grid.Cols);
        Assert.Equal(16, grid.TotalCells);
    }

    private static void AcousticBackups()
    {
        var root = Path.Combine(Path.GetTempPath(), $"module-audit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var wallPath = Path.Combine(root, "wall.json");
            SoundInsulationStorageService.SaveToFile([new WallAssembly("one", "Duvar 1", [])], wallPath);
            var first = File.ReadAllText(wallPath);
            SoundInsulationStorageService.SaveToFile([new WallAssembly("two", "Duvar 2", [])], wallPath);
            Assert.Equal(first, File.ReadAllText(wallPath + ".bak"));
            Assert.Equal("two", SoundInsulationStorageService.LoadFromFile(wallPath).Single().Id);
            var sessionPath = Path.Combine(root, "measurement.json");
            MeasurementRepository.SaveSessionAsync(new MeasurementSession { Name = "First" }, sessionPath).GetAwaiter().GetResult();
            first = File.ReadAllText(sessionPath);
            MeasurementRepository.SaveSessionAsync(new MeasurementSession { Name = "Second" }, sessionPath).GetAwaiter().GetResult();
            Assert.Equal(first, File.ReadAllText(sessionPath + ".bak"));
            Assert.Equal("Second", MeasurementRepository.LoadSessionAsync(sessionPath).GetAwaiter().GetResult()!.Name);
        }
        finally { Directory.Delete(root, true); }
    }

    private static void DisabledSourcesWarning()
    {
        var project = new NoiseBarrierProject();
        project.Sources.Clear();
        project.Sources.Add(new NoiseSource { IsEnabled = false });
        new NoiseBarrierEngine(project, []).RunSimulation();
        Assert.Contains("Etkin kaynak bulunmuyor.", project.ValidationWarnings);
    }

    private static void InvalidAbsorptionRejected()
    {
        int[] bands = [200, 250, 315, 400, 500, 630, 800, 1000, 1250, 1600, 2000, 2500, 3150, 4000, 5000];
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, -0.1 })
            Assert.Throws<ArgumentException>(() => TmmCalculator.CalculateAlphaWResultFromThirdOctaves(bands.ToDictionary(f => f, _ => invalid)));
    }

    private static void MultilineMaterialRoundTrip()
    {
        var path = Path.Combine(Path.GetTempPath(), $"module-audit-{Guid.NewGuid():N}.csv");
        try
        {
            var material = new SoundInsulationMaterial { Id = "test", Name = "İlk; \"katman\"\r\nİkinci satır", Density = 800 };
            SoundInsulationStorageService.ExportMaterialsToCsv([material], path);
            var loaded = SoundInsulationStorageService.ImportMaterialsFromCsv(path);
            Assert.Single(loaded);
            Assert.Equal(material.Name, loaded[0].Name);
            Assert.Equal(material.Density, loaded[0].Density);
        }
        finally { File.Delete(path); }
    }

    private static void InvalidDampingRejected()
    {
        var stack = new ImpactLayerStack { Layers = [new ImpactLayer { ThicknessMm = 50,
            Material = new ImpactMaterial { ElasticModulus = 1, ImpactAbsorption = double.NaN } }] };
        Assert.False(ImpactCalculationService.Calculate(stack).IsValid);
    }

    private static void UnboundedRoomRejected()
    {
        var result = RoomModeCalculator.Calculate(new RoomModeInput { LengthX = double.MaxValue, RT60 = 0.5 });
        Assert.Empty(result.Modes);
        Assert.NotEmpty(result.Warnings);
        result = RoomModeCalculator.Calculate(new RoomModeInput { LengthX = 1000, LengthY = 1000, LengthZ = 1000 });
        Assert.Empty(result.Modes);
        Assert.NotEmpty(result.Warnings);
    }

    private static JsonSerializerOptions MatrixOptions() => new() { Converters = { new Float2DArrayJsonConverter() } };

    private static void RaggedMatrixRejected()
    {
        foreach (var json in new[] { "[[1,2],[3]]", "[[1],[2,3]]", "[null,[1]]" })
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<float[,]>(json, MatrixOptions()));
    }

    private static void MatrixRoundTrip()
    {
        float[,] data = { { 1, 2 }, { 3, 4 } };
        var loaded = JsonSerializer.Deserialize<float[,]>(JsonSerializer.Serialize(data, MatrixOptions()), MatrixOptions())!;
        Assert.Equal(2, loaded.GetLength(0));
        Assert.Equal(4, loaded[1, 1]);
    }

    private static void LargeCircuitProtection()
    {
        Assert.Equal(1000, LinyeHesapServisi.SigortaBoyutlandir(900));
        Assert.Equal(6300, LinyeHesapServisi.SigortaBoyutlandir(6000));
    }

    private static void IsolatedDisciplineContent()
    {
        var project = new BasitProjeCizProje();
        var floor = new BasitProjeKat();
        project.Katlar.Add(floor);
        floor.Topraklama.Kaziklar.Add(new Point2D(0, 0));
        Assert.True(DisiplinAyiklamaServisi.Say(project).KuvvetliVar);
        DisiplinAyiklamaServisi.Ayikla(project, BpcDisiplin.ZayifAkim);
        Assert.False(DisiplinAyiklamaServisi.Say(project).KuvvetliVar);
    }

    private static void BatteryStrings()
    {
        var system = new ElvAnonsSistemi();
        system.AnalogGucKaynagi.NominalGerilimV = 48;
        system.AnalogGucKaynagi.AkuAdedi = 4;
        system.AnalogGucKaynagi.AkuKapasitesiAh = 7;
        Assert.Equal(7, ElvAnalogAnonsAkuServisi.Hesapla(system).MevcutAh);
    }

    private static void InvalidPoleSpacing()
    {
        Point2D[] route = [new(0, 0), new(10, 0)];
        Assert.Empty(DirekYerlesimServisi.GuzergahaYerlestir(route, double.NaN));
        Assert.Empty(DirekYerlesimServisi.GuzergahaYerlestir(route, double.PositiveInfinity));
        Assert.Empty(DirekYerlesimServisi.GuzergahaYerlestir(route, 1e-12));
    }

    private static void InvalidResistance()
    {
        Assert.False(TopraklamaServisi.RcdUygunMu(-1, 0.03));
        Assert.False(TopraklamaServisi.RcdUygunMu(double.NegativeInfinity, 0.03));
        Assert.Throws<ArgumentException>(() => TopraklamaServisi.KazikDirenci(-100, 2, 0.02));
    }
}
