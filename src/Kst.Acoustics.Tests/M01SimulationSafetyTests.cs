using Kst.Acoustics.App.ViewModels;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Importing;
using Kst.Acoustics.Core.Measurement.Comparison;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Simulation;
using Xunit;

namespace Kst.Acoustics.Tests;

/// <summary>M01 simülasyon güvenilirliği ve proje yaşam döngüsü regresyonları.</summary>
public static class M01SimulationSafetyTests
{
    public static void ReceiverRadiusUsesPhysicalMetersAcrossUnits()
    {
        using var metersVm = new ProjectWindowViewModel(BuildBoxProject(SurfaceImportUnit.Meters, 1.0));
        using var millimetersVm = new ProjectWindowViewModel(BuildBoxProject(SurfaceImportUnit.Millimeters, 1000.0));

        metersVm.ReceiverRadius = 0.75;
        millimetersVm.ReceiverRadius = 0.75;

        var meters = metersVm.BuildStoredSimulationSettings();
        var millimeters = millimetersVm.BuildStoredSimulationSettings();
        AssertClose(0.75, meters.ReceiverRadius * meters.UnitScale, 1e-12,
            "Metre projesinde fiziksel alıcı yarıçapı");
        AssertClose(0.75, millimeters.ReceiverRadius * millimeters.UnitScale, 1e-12,
            "Milimetre projesinde fiziksel alıcı yarıçapı");
        AssertClose(750.0, millimeters.ReceiverRadius, 1e-9,
            "0,75 m yarıçapın milimetre proje birimi karşılığı");
    }

    public static void EmptyMetricsNeverProduceFalseConvergence()
    {
        var unavailable = new SimulationConvergenceMetrics(
            double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
        var empty = SimulationConvergenceEvaluator.Compare(unavailable, unavailable);
        AssertTrue(!empty.IsStable && empty.ValidMetricCount == 0,
            "İki boş/NaN koşu stabil kabul edilmemeli.");

        var onlyTwoMetricsA = new SimulationConvergenceMetrics(1.00, 2.0, double.NaN, double.NaN, double.NaN);
        var onlyTwoMetricsB = new SimulationConvergenceMetrics(1.01, 2.1, double.NaN, double.NaN, double.NaN);
        var insufficient = SimulationConvergenceEvaluator.Compare(onlyTwoMetricsA, onlyTwoMetricsB);
        AssertTrue(!insufficient.IsStable && insufficient.ValidMetricCount == 2,
            "Üçten az sonlu metrik erken yakınsama üretmemeli.");

        var stableA = new SimulationConvergenceMetrics(1.00, 2.0, 0.60, 0.90, -1.0);
        var stableB = new SimulationConvergenceMetrics(1.02, 2.5, 0.61, 0.92, -0.5);
        AssertTrue(SimulationConvergenceEvaluator.Compare(stableA, stableB).IsStable,
            "JND içinde kalan yeterli metrikler stabil kabul edilmeli.");
    }

    public static void LoadedResultIsDetachedBeforeInvalidation()
    {
        var project = BuildBoxProject(SurfaceImportUnit.Meters, 1.0);
        var parameters = new SimulationParameters(RayCount: 1_000, Seed: 7);
        parameters = parameters with { InputFingerprint = SimulationInputFingerprint.Compute(project, parameters) };
        var baselineResult = BuildStoredOnlyResult(parameters);
        var projectWithResult = project with { LastResult = baselineResult };

        using var vm = new ProjectWindowViewModel(projectWithResult);
        AssertTrue(vm.LastResult is not null && !vm.LastResult.IsStale,
            "Eşleşen fingerprint ile yüklenen sonuç güncel olmalı.");
        AssertTrue(!ReferenceEquals(vm.LastResult, baselineResult),
            "Alt pencere parent baseline sonucundan bağımsız bir sonuç kabuğu kullanmalı.");

        vm.RayCount += 100;
        AssertTrue(vm.LastResult!.IsStale, "Simülasyon girdisi değişince yerel sonuç eski olmalı.");
        AssertTrue(!baselineResult.IsStale && baselineResult.StaleReason is null,
            "Yerel stale işareti parent baseline sonucuna sızmamalı.");
    }

    public static void LoadedFingerprintMismatchIsMarkedStaleWithoutMutation()
    {
        var project = BuildBoxProject(SurfaceImportUnit.Meters, 1.0);
        var parameters = new SimulationParameters(RayCount: 1_000, Seed: 9);
        parameters = parameters with { InputFingerprint = SimulationInputFingerprint.Compute(project, parameters) };
        var baselineResult = BuildStoredOnlyResult(parameters);
        var editedProject = project with
        {
            RoomVolume = project.RoomVolume + 1.0,
            LastResult = baselineResult
        };

        using var vm = new ProjectWindowViewModel(editedProject);
        AssertTrue(vm.IsSimulationResultStale && vm.LastResult?.IsStale == true,
            "Proje girdileriyle eşleşmeyen kayıtlı sonuç eski işaretlenmeli.");
        AssertTrue(vm.LastResult?.StaleReason?.Contains("izi eşleşmiyor", StringComparison.Ordinal) == true,
            "Fingerprint uyuşmazlığı kullanıcıya açıklanmalı.");
        AssertTrue(!baselineResult.IsStale,
            "Uyuşmazlık denetimi parent baseline nesnesini değiştirmemeli.");
    }

    public static void LoadedSettingsMismatchIsMarkedStale()
    {
        var project = BuildBoxProject(SurfaceImportUnit.Meters, 1.0);
        var runParameters = new SimulationParameters(RayCount: 1_000, Seed: 19);
        runParameters = runParameters with
        {
            InputFingerprint = SimulationInputFingerprint.Compute(project, runParameters)
        };
        var storedResult = BuildStoredOnlyResult(runParameters);
        var editedSettings = runParameters with
        {
            RayCount = 2_000,
            InputFingerprint = null
        };
        var editedProject = project with
        {
            LastResult = storedResult,
            SimulationSettings = editedSettings
        };

        using var vm = new ProjectWindowViewModel(editedProject);
        AssertTrue(vm.IsSimulationResultStale && vm.LastResult?.IsStale == true,
            "Kayıtlı güncel ayarlar sonuç parametrelerinden farklıysa sonuç eski işaretlenmeli.");
        AssertTrue(!storedResult.IsStale,
            "Ayar uyuşmazlığı parent baseline sonucunu mutasyona uğratmamalı.");
    }

    public static void PreCancelledSimulationStopsBeforeWork()
    {
        var project = BuildBoxProject(SurfaceImportUnit.Meters, 1.0);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        try
        {
            _ = new RayTracingService().Run(
                project,
                project.CategoryMaterials,
                new SimulationParameters(RayCount: 100_000),
                ct: cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        throw new InvalidOperationException("Önceden iptal edilmiş simülasyon çalışmaya başlamamalı.");
    }

    public static void SimulationResultRoundTripPreservesMetricsCountsCurvesAndMask()
    {
        var path = Path.Combine(Path.GetTempPath(), $"eye4pro-m01-sim-{Guid.NewGuid():N}.e4p01");
        try
        {
            var project = BuildBoxProject(SurfaceImportUnit.Meters, 1.0);
            // Izgaranın tamamı 4×4×3 m kapalı odanın dışında. Eski güvenli-geri-
            // dönüş yolu sıfır aktif hücreyi yanlışlıkla yeniden etkinleştiriyordu.
            var grid = new ReceiverGrid(1.5, 5, 7, 5, 7, 1.0);
            var parameters = new SimulationParameters(
                RayCount: 1_000,
                MaxOrder: 30,
                ReceiverRadius: 0.55,
                IsmOrder: 1,
                TransitionOrder: 8,
                ReceiverGrid: grid,
                Seed: 2026,
                MaskGridOutsideRoom: true);
            parameters = parameters with
            {
                InputFingerprint = SimulationInputFingerprint.Compute(project, parameters)
            };

            var result = new RayTracingService().Run(project, project.CategoryMaterials, parameters);
            AssertTrue(result.TotalArrivals > 0, "Round-trip testi için simülasyon geliş üretmeli.");
            AssertTrue(result.ComputedParameters is { Count: > 0 },
                "Simülasyon hesaplanmış parametreleri üretmeli.");
            AssertTrue(result.ComputedParameters![0].Bands.Any(b => b.SchroederCurve.Length > 1),
                "Simülasyon kaydedilebilir Schroeder eğrisi üretmeli.");
            AssertTrue(result.GridMap is not null, "Simülasyon ızgara haritası üretmeli.");
            AssertTrue(double.IsNaN(result.GridMap!.GetValue(GridMapParameter.RT60, 0, 0)),
                "Kapalı odanın dışındaki ızgara hücresi NaN ile maskelenmeli.");

            var originalCount = result.TotalArrivals;
            var originalCurve = result.ComputedParameters[0].Bands
                .First(b => b.SchroederCurve.Length > 1).SchroederCurve;
            var service = new ProjectFileService();
            service.Save(path, project with
            {
                LastResult = result,
                SimulationSettings = parameters
            });

            var json = File.ReadAllText(path);
            AssertTrue(json.Contains("\"iacc\": null", StringComparison.Ordinal),
                "Ölçülemeyen akustik değer strict JSON null olarak saklanmalı.");
            AssertTrue(json.Contains("\"time\"", StringComparison.Ordinal) &&
                       json.Contains("\"db\"", StringComparison.Ordinal),
                "Schroeder eğrisi boş nesneler yerine zaman/seviye alanlarıyla saklanmalı.");

            var loaded = service.Load(path);
            var loadedResult = loaded.LastResult
                ?? throw new InvalidOperationException("Kaydedilen simülasyon sonucu geri yüklenmedi.");
            AssertTrue(loadedResult.TotalArrivals == originalCount,
                "Ham gelişler yazılmasa bile geliş sayısı round-trip boyunca korunmalı.");
            AssertTrue(!loadedResult.HasImpulseResponseData,
                "Ham gelişleri saklanmayan yüklenmiş sonuç auralizasyonu hazır göstermemeli.");
            AssertTrue(loadedResult.ComputedParameters is { Count: > 0 },
                "Hesaplanmış akustik parametreler geri yüklenmeli.");
            AssertTrue(double.IsNaN(loadedResult.ComputedParameters![0].IACC),
                "JSON null ölçülemeyen IACC olarak NaN'e dönmeli.");

            var loadedCurve = loadedResult.ComputedParameters[0].Bands
                .First(b => b.SchroederCurve.Length > 1).SchroederCurve;
            AssertTrue(loadedCurve.Length == originalCurve.Length,
                "Schroeder eğrisi nokta sayısı korunmalı.");
            AssertClose(originalCurve[0].Time, loadedCurve[0].Time, 1e-12,
                "Schroeder eğrisi ilk zaman değeri");
            AssertClose(originalCurve[0].Db, loadedCurve[0].Db, 1e-12,
                "Schroeder eğrisi ilk seviye değeri");
            AssertTrue(loadedResult.GridMap is not null &&
                       double.IsNaN(loadedResult.GridMap.GetValue(GridMapParameter.RT60, 0, 0)),
                "Oda dışı ızgara maskesi save/load sonrasında korunmalı.");

            var comparison = SimulationComparisonService.LoadFromProjectFileAsync(path)
                .GetAwaiter().GetResult();
            var expectedC80 = loadedResult.ComputedParameters[0].Bands
                .First(b => b.FrequencyHz == 1000).C80;
            var comparisonC80 = comparison.Parameters["C80"]
                .First(v => v.FreqHz == 1000).Value;
            AssertClose(expectedC80, comparisonC80, 1e-12,
                "Yeniden açılan proje ölçüm karşılaştırmasında saklı C80");

            // Karşılaştırma servisi backup recovery'yi sessizce gizlememeli.
            service.Save(path, loaded); // geçerli primary'yi sağlam .bak olarak oluştur
            File.WriteAllText(path, "{bozuk-json");
            var recoveredComparison = SimulationComparisonService.LoadFromProjectFileAsync(path)
                .GetAwaiter().GetResult();
            AssertTrue(recoveredComparison.RecoveredFromBackup,
                "Ölçüm karşılaştırması .bak recovery durumunu çağırana taşımalı.");
            AssertTrue(recoveredComparison.SessionWarnings.Any(w =>
                    w.Contains(".bak", StringComparison.OrdinalIgnoreCase)),
                "Ölçüm karşılaştırması recovery uyarısını çağırana taşımalı.");

            // Eski olduğu açıkça bilinen bir sonuç ölçüm kararı için kullanılamaz.
            loadedResult.IsStale = true;
            loadedResult.StaleReason = "Regresyon testi";
            service.Save(path, loaded);
            AssertThrows<InvalidOperationException>(
                () => SimulationComparisonService.LoadFromProjectFileAsync(path).GetAwaiter().GetResult(),
                "Ölçüm karşılaştırması eski simülasyon sonucunu reddetmeli.");
        }
        finally
        {
            TryDelete(path);
            TryDelete(path + ".bak");
        }
    }

    private static SimulationResult BuildStoredOnlyResult(SimulationParameters parameters)
    {
        var echogram = new EchogramData
        {
            SourceId = 1,
            ReceiverId = 2,
            SourceLabel = "K1",
            ReceiverLabel = "A1",
            StoredArrivalCount = 12
        };
        return new SimulationResult([echogram], parameters, TimeSpan.FromSeconds(1));
    }

    private static AcousticsProject BuildBoxProject(SurfaceImportUnit unit, double scale)
    {
        const double lx = 4, ly = 4, lz = 3;
        var surfaces = BoxSurfaces(lx * scale, ly * scale, lz * scale, "DUVAR");
        var materials = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DUVAR"] = "plaster"
        };
        return new AcousticsProject(
            "M01 Simülasyon Testi",
            null,
            DateTime.UtcNow,
            SurfaceImportFormat.Dxf,
            unit,
            surfaces,
            Sources:
            [
                new AcousticPoint(1, "K1", AcousticPointType.Source,
                    new Point3D(1.0 * scale, 1.0 * scale, 1.5 * scale))
            ],
            Receivers:
            [
                new AcousticPoint(2, "A1", AcousticPointType.Receiver,
                    new Point3D(3.0 * scale, 3.0 * scale, 1.5 * scale))
            ],
            CategoryMaterials: materials,
            RoomVolume: lx * ly * lz,
            VolumeOrigin: RoomVolumeOrigin.ClosedMesh);
    }

    private static List<ProjectSurface> BoxSurfaces(double lx, double ly, double lz, string category) =>
    [
        new(1, "Zemin", category, [new(0,0,0), new(lx,0,0), new(lx,ly,0), new(0,ly,0)]),
        new(2, "Tavan", category, [new(0,0,lz), new(lx,0,lz), new(lx,ly,lz), new(0,ly,lz)]),
        new(3, "Duvar Y0", category, [new(0,0,0), new(lx,0,0), new(lx,0,lz), new(0,0,lz)]),
        new(4, "Duvar Y1", category, [new(0,ly,0), new(lx,ly,0), new(lx,ly,lz), new(0,ly,lz)]),
        new(5, "Duvar X0", category, [new(0,0,0), new(0,ly,0), new(0,ly,lz), new(0,0,lz)]),
        new(6, "Duvar X1", category, [new(lx,0,0), new(lx,ly,0), new(lx,ly,lz), new(lx,0,lz)])
    ];

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void AssertClose(double expected, double actual, double tolerance, string label)
    {
        if (!double.IsFinite(actual) || Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException(
                $"{label}: beklenen {expected:G17}, alınan {actual:G17}, tolerans {tolerance:G3}.");
    }

    private static void AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Testin asıl sonucunu geçici dosya temizleme hatası gölgelemesin.
        }
    }
}

/// <summary>Standart <c>dotnet test</c> keşfi için M01 güvenlik regresyonları.</summary>
public sealed class M01SimulationSafetyXunitTests
{
    [Fact]
    public void ReceiverRadiusUsesPhysicalMetersAcrossUnits() =>
        M01SimulationSafetyTests.ReceiverRadiusUsesPhysicalMetersAcrossUnits();

    [Fact]
    public void EmptyMetricsNeverProduceFalseConvergence() =>
        M01SimulationSafetyTests.EmptyMetricsNeverProduceFalseConvergence();

    [Fact]
    public void LoadedResultIsDetachedBeforeInvalidation() =>
        M01SimulationSafetyTests.LoadedResultIsDetachedBeforeInvalidation();

    [Fact]
    public void LoadedFingerprintMismatchIsMarkedStaleWithoutMutation() =>
        M01SimulationSafetyTests.LoadedFingerprintMismatchIsMarkedStaleWithoutMutation();

    [Fact]
    public void LoadedSettingsMismatchIsMarkedStale() =>
        M01SimulationSafetyTests.LoadedSettingsMismatchIsMarkedStale();

    [Fact]
    public void PreCancelledSimulationStopsBeforeWork() =>
        M01SimulationSafetyTests.PreCancelledSimulationStopsBeforeWork();

    [Fact]
    public void SimulationResultRoundTripPreservesMetricsCountsCurvesAndMask() =>
        M01SimulationSafetyTests.SimulationResultRoundTripPreservesMetricsCountsCurvesAndMask();
}
