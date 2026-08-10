using Kst.Acoustics.App.ViewModels;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Importing;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Simulation;
using Xunit;

namespace Kst.Acoustics.Tests;

public static class M01ScenarioAndTargetTests
{
    public static void ScenarioManagerCreatesIndependentSnapshots()
    {
        var state = AcousticScenarioCloner.FromProject(BuildProject());
        var original = AcousticScenarioManager.Create("Alternatif A", state);
        var duplicate = AcousticScenarioManager.Duplicate(original);

        duplicate.State.PanelKonfigurasyon!.Paneller[0].Vertices[0] = new Point3D(99, 99, 99);

        AssertTrue(original.Id != duplicate.Id, "Kopya senaryo yeni kimlik taşımalı.");
        AssertTrue(original.Name != duplicate.Name, "Kopya senaryo ayırt edilebilir ad taşımalı.");
        AssertClose(0, original.State.PanelKonfigurasyon!.Paneller[0].Vertices[0].X, 0,
            "Senaryo snapshot'ları mutable panel köşesi paylaşmamalı.");

        var renamed = AcousticScenarioManager.Rename(duplicate, "Akustik Bulut");
        AssertTrue(renamed.Name == "Akustik Bulut", "Senaryo yeniden adlandırılmalı.");
        AssertTrue(AcousticScenarioManager.Delete([original, renamed], original.Id).Single().Id == renamed.Id,
            "Silme yalnız hedef senaryoyu kaldırmalı.");
    }

    public static void ScenarioValidationRejectsDuplicateAndDanglingActiveIds()
    {
        var project = BuildProject();
        var state = AcousticScenarioCloner.FromProject(project);
        var first = AcousticScenarioManager.Create("A", state);
        var duplicate = first with { Name = "B" };

        AssertThrows<InvalidDataException>(() => AcousticsProjectMigrator.MigrateAndValidate(project with
        {
            SemaSurum = AcousticsProjectSchema.CurrentVersion,
            Scenarios = [first, duplicate]
        }), "Yinelenen senaryo ID'si reddedilmeli.");

        AssertThrows<InvalidDataException>(() => AcousticsProjectMigrator.MigrateAndValidate(project with
        {
            SemaSurum = AcousticsProjectSchema.CurrentVersion,
            Scenarios = [first],
            ActiveScenarioId = Guid.NewGuid()
        }), "Listede olmayan etkin senaryo reddedilmeli.");
    }

    public static void ScenarioRoundTripPreservesBaselineAndActiveSnapshot()
    {
        var baseline = BuildProject() with
        {
            CategoryMaterials = new Dictionary<string, string> { ["DUVAR"] = "BASELINE-MAT" }
        };
        var scenarioState = AcousticScenarioCloner.FromProject(baseline with
        {
            CategoryMaterials = new Dictionary<string, string> { ["DUVAR"] = "SCENARIO-MAT" },
            Sources = [new AcousticPoint(9, "S9", AcousticPointType.Source, new Point3D(1, 1, 1))]
        });
        var scenario = AcousticScenarioManager.Create("Alternatif", scenarioState);
        var project = baseline with
        {
            SemaSurum = AcousticsProjectSchema.CurrentVersion,
            Scenarios = [scenario],
            ActiveScenarioId = scenario.Id
        };
        var path = Path.Combine(Path.GetTempPath(), $"m01-scenario-{Guid.NewGuid():N}.e4p01");

        try
        {
            var service = new ProjectFileService();
            service.Save(path, project);
            var loaded = service.Load(path);

            AssertTrue(loaded.SemaSurum == AcousticsProjectSchema.CurrentVersion,
                "Senaryo içeren dosya güncel şemada saklanmalı.");
            AssertTrue(loaded.ActiveScenarioId == scenario.Id && loaded.Scenarios.Count == 1,
                "Etkin senaryo ve liste round-trip korunmalı.");
            AssertTrue(loaded.CategoryMaterials["DUVAR"] == "BASELINE-MAT",
                "Etkin senaryo baseline materyalini ezmemeli.");
            AssertTrue(loaded.Scenarios[0].State.CategoryMaterials["DUVAR"] == "SCENARIO-MAT",
                "Senaryo materyal snapshot'ı korunmalı.");
            AssertTrue(loaded.Scenarios[0].State.Sources.Single().Id == 9,
                "Senaryo kaynak snapshot'ı korunmalı.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
        }
    }

    public static void TargetPerformanceReportsBandsAndAbsorptionNeed()
    {
        var result = BuildResult(rt60: 1.50, sti: 0.42, c50: -3, c80: 7, g: 15);
        var report = TargetPerformanceService.Evaluate(RoomType.ConferenceHall, 500, result);

        AssertTrue(report.Metrics.Count == 12,
            "Sekiz RT60 bandı ile STI/C50/C80/G birlikte değerlendirilmeli.");
        AssertTrue(report.Metrics.Count(m => m.Metric == "RT60") == 8,
            "RT60 değerlendirmesi bant bazında olmalı.");
        AssertTrue(report.OverallStatus == TargetPerformanceStatus.Iyilestirilmeli,
            "Belirgin kötü metrikler iyileştirilmeli olarak sınıflanmalı.");
        AssertTrue(report.AdditionalEquivalentAbsorptionAreaM2 is > 0,
            "Yüksek RT60 için pozitif ek eşdeğer emilim tahmini üretilmeli.");
        AssertTrue(report.AbsorptionRecommendation.Contains("m² Sabin", StringComparison.Ordinal),
            "Emilim önerisi birim ve yöntemini kullanıcıya açıklamalı.");
    }

    public static void TargetPerformanceExplainsMissingAndStaleDataInTurkish()
    {
        var missing = TargetPerformanceService.Evaluate(RoomType.Classroom, 120, null);
        AssertTrue(!missing.HasData && missing.OverallStatus == TargetPerformanceStatus.VeriYok,
            "Sonuç yokken durum VeriYok olmalı.");
        AssertTrue(missing.Summary.Contains("Önce simülasyon", StringComparison.Ordinal),
            "Veri yok açıklaması Türkçe ve eyleme dönük olmalı.");

        var staleResult = BuildResult(0.7, 0.65, 3, 1, 5);
        staleResult.IsStale = true;
        var stale = TargetPerformanceService.Evaluate(RoomType.Classroom, 120, staleResult);
        AssertTrue(stale.UsesStaleResult && stale.Summary.Contains("eski", StringComparison.OrdinalIgnoreCase),
            "Eski sonuç hedef panelinde açıkça belirtilmeli.");
    }

    public static void ViewModelScenarioSwitchPreservesBaselineAndMarksResultStale()
    {
        var vm = new ProjectWindowViewModel(BuildProject() with { LastResult = BuildResult(0.8, 0.65, 3, 1, 5) });
        vm.ScenarioNameInput = "Panel Alternatifi";
        vm.CreateScenario();

        AssertTrue(!vm.IsBaselineScenarioActive && vm.LastResult?.IsStale == true,
            "Yeni senaryo etkinleştiğinde taşınan sonuç stale olmalı.");
        vm.NewReceiverLabel = "R2";
        vm.NewReceiverX = "3";
        vm.NewReceiverY = "2";
        vm.NewReceiverZ = "1.2";
        vm.AddReceiver();
        AssertTrue(vm.Receivers.Count == 2, "Senaryo düzenlemesi çalışma state'ine uygulanmalı.");

        vm.ActivateBaselineScenario();
        AssertTrue(vm.IsBaselineScenarioActive && vm.Receivers.Count == 1,
            "Ana modele dönüş baseline alıcılarını eksiksiz geri yüklemeli.");

        var saved = vm.BuildCurrentProject();
        AssertTrue(saved.Receivers.Count == 1,
            "Top-level proje baseline olarak kalmalı.");
        AssertTrue(saved.Scenarios.Single().State.Receivers.Count == 2,
            "Senaryo düzenlemesi yalnız senaryo snapshot'ında saklanmalı.");
    }

    private static AcousticsProject BuildProject()
    {
        const double lx = 4, ly = 4, lz = 3;
        var panel = new AcousticPanelKonfigurasyon
        {
            OturumId = Guid.NewGuid(),
            PanelEniM = 1,
            PanelEnleriM = [1],
            AktifTipler = [PanelTipi.Tavan],
            Paneller =
            [
                new AcousticPanel
                {
                    Id = Guid.NewGuid(), Tip = PanelTipi.Tavan, DilimNo = 1,
                    PanelEniM = 1, Vertices = [new(0,0,2), new(1,0,2), new(1,1,2), new(0,1,2)]
                }
            ]
        };
        return new AcousticsProject(
            "Senaryo Test", null, DateTime.UtcNow,
            SurfaceImportFormat.Dxf, SurfaceImportUnit.Meters,
            BoxSurfaces(lx, ly, lz),
            Sources: [new AcousticPoint(1, "S1", AcousticPointType.Source, new Point3D(1, 1, 1))],
            Receivers: [new AcousticPoint(1, "R1", AcousticPointType.Receiver, new Point3D(2, 2, 1))],
            CategoryMaterials: new Dictionary<string, string> { ["DUVAR"] = "MAT" },
            SelectedRoomType: RoomType.ConferenceHall,
            RoomVolume: lx * ly * lz,
            PanelKonfigurasyon: panel,
            SimulationSettings: new SimulationParameters())
        {
            SemaSurum = AcousticsProjectSchema.CurrentVersion
        };
    }

    private static SimulationResult BuildResult(double rt60, double sti, double c50, double c80, double g)
    {
        int[] frequencies = [63, 125, 250, 500, 1000, 2000, 4000, 8000];
        var bands = frequencies.Select(f => new BandParameters(
            f, rt60, rt60, rt60, rt60, 50, g, c50, c80,
            0.6, 0.2, 0.2, -5, -8, [], 0.98, -45)).ToArray();
        var parameters = new AcousticParameters(1, 1, "S1", "R1", bands) { STI = sti };
        return new SimulationResult(
            [new EchogramData(1, 1, "S1", "R1") { StoredArrivalCount = 500 }],
            new SimulationParameters(), TimeSpan.FromSeconds(1))
        {
            ComputedParameters = [parameters]
        };
    }

    private static List<ProjectSurface> BoxSurfaces(double lx, double ly, double lz) =>
    [
        new(1, "Zemin",   "DUVAR", [new(0,0,0),  new(lx,0,0),  new(lx,ly,0),  new(0,ly,0)]),
        new(2, "Tavan",   "DUVAR", [new(0,0,lz), new(lx,0,lz), new(lx,ly,lz), new(0,ly,lz)]),
        new(3, "DuvarY0", "DUVAR", [new(0,0,0),  new(lx,0,0),  new(lx,0,lz),  new(0,0,lz)]),
        new(4, "DuvarY1", "DUVAR", [new(0,ly,0), new(lx,ly,0), new(lx,ly,lz), new(0,ly,lz)]),
        new(5, "DuvarX0", "DUVAR", [new(0,0,0),  new(0,ly,0),  new(0,ly,lz),  new(0,0,lz)]),
        new(6, "DuvarX1", "DUVAR", [new(lx,0,0), new(lx,ly,0), new(lx,ly,lz), new(lx,0,lz)])
    ];

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void AssertClose(double expected, double actual, double tolerance, string message)
    {
        if (Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException(message);
    }

    private static void AssertThrows<TException>(Action action, string message) where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        throw new InvalidOperationException(message);
    }
}

public sealed class M01ScenarioAndTargetXunitTests
{
    [Fact]
    public void ScenarioSnapshotsAreIndependent() =>
        M01ScenarioAndTargetTests.ScenarioManagerCreatesIndependentSnapshots();

    [Fact]
    public void ScenarioIdsAndActivationAreValidated() =>
        M01ScenarioAndTargetTests.ScenarioValidationRejectsDuplicateAndDanglingActiveIds();

    [Fact]
    public void ScenarioFileRoundTripKeepsBaseline() =>
        M01ScenarioAndTargetTests.ScenarioRoundTripPreservesBaselineAndActiveSnapshot();

    [Fact]
    public void TargetsIncludeBandsAndAbsorption() =>
        M01ScenarioAndTargetTests.TargetPerformanceReportsBandsAndAbsorptionNeed();

    [Fact]
    public void MissingAndStaleDataAreExplicit() =>
        M01ScenarioAndTargetTests.TargetPerformanceExplainsMissingAndStaleDataInTurkish();

    [Fact]
    public void ScenarioSwitchKeepsBaseline() =>
        M01ScenarioAndTargetTests.ViewModelScenarioSwitchPreservesBaselineAndMarksResultStale();
}
