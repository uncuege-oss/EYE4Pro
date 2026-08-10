using Kst.Acoustics.App.ViewModels;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Importing;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Simulation;
using Xunit;

namespace Kst.Acoustics.Tests;

public sealed class M01DocumentHistoryTests
{
    [Fact]
    public void UndoRedoRestoresSnapshotAndSavedBaseline()
    {
        using var vm = new ProjectWindowViewModel(BuildProject());
        var initialVolume = vm.RoomVolume;

        vm.RoomVolume = initialVolume + 7.25;

        Assert.True(vm.CanUndoDocument);
        Assert.True(vm.IsDirty);
        Assert.Contains("hacmi", vm.UndoDocumentDescription, StringComparison.OrdinalIgnoreCase);

        Assert.True(vm.UndoDocument());
        Assert.Equal(initialVolume, vm.RoomVolume, 10);
        Assert.False(vm.IsDirty);
        Assert.True(vm.CanRedoDocument);

        Assert.True(vm.RedoDocument());
        Assert.Equal(initialVolume + 7.25, vm.RoomVolume, 10);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void UndoRedoPreservesSimulationStaleState()
    {
        var project = BuildProject(withPoints: true);
        var parameters = new SimulationParameters(RayCount: 1_000);
        parameters = parameters with
        {
            InputFingerprint = SimulationInputFingerprint.Compute(project, parameters)
        };
        project = project with
        {
            LastResult = new SimulationResult(
                [new EchogramData
                {
                    SourceId = 1,
                    ReceiverId = 2,
                    SourceLabel = "S1",
                    ReceiverLabel = "R1",
                    StoredArrivalCount = 5
                }],
                parameters,
                TimeSpan.FromSeconds(1))
        };

        using var vm = new ProjectWindowViewModel(project);
        Assert.False(vm.IsSimulationResultStale);

        vm.RoomVolume += 1;
        Assert.True(vm.IsSimulationResultStale);

        Assert.True(vm.UndoDocument());
        var restoredProject = vm.BuildCurrentProject();
        var restoredFingerprint = SimulationInputFingerprint.Compute(
            restoredProject,
            restoredProject.SimulationSettings ?? restoredProject.LastResult!.Parameters);
        Assert.Equal(restoredProject.LastResult!.InputFingerprint, restoredFingerprint);
        Assert.False(vm.IsSimulationResultStale);
        Assert.NotNull(vm.LastResult);

        Assert.True(vm.RedoDocument());
        Assert.True(vm.IsSimulationResultStale);
        Assert.Contains("hacmi", vm.LastResult?.StaleReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UndoRestoresGeometryMaterialPointAndSimulationSettings()
    {
        using var vm = new ProjectWindowViewModel(BuildProject());
        var initialRayCount = vm.RayCount;
        var initialX = vm.ProjectSurfaces.First(s => s.Id == 1).Vertices[0].X;

        vm.NewSourceX = "1.25";
        vm.NewSourceY = "1.5";
        vm.NewSourceZ = "1.75";
        vm.AddSource();
        vm.RayCount = initialRayCount + 500;

        var alternative = MaterialDatabase.Instance.FindById("carpet_thin")
            ?? throw new InvalidOperationException("Test materyali bulunamadı: carpet_thin");
        vm.CategoryMaterialRows.First().SelectedMaterial = alternative;

        vm.SelectedSurfaceIds = [1];
        vm.TranslateDX = 0.5;
        vm.ApplyTranslation();
        Assert.Equal(initialX + 0.5, vm.ProjectSurfaces.First(s => s.Id == 1).Vertices[0].X, 10);

        Assert.True(vm.UndoDocument());
        Assert.Equal(initialX, vm.ProjectSurfaces.First(s => s.Id == 1).Vertices[0].X, 10);

        Assert.True(vm.UndoDocument());
        Assert.NotEqual(alternative.Id, vm.CategoryMaterialRows.First().PersistedMaterialId);

        Assert.True(vm.UndoDocument());
        Assert.Equal(initialRayCount, vm.RayCount);

        Assert.True(vm.UndoDocument());
        Assert.Empty(vm.Sources);
    }

    [Fact]
    public async Task VersionHistoryPrunesListsRestoresAndDeletes()
    {
        var root = Path.Combine(Path.GetTempPath(), $"eye4pro-m01-versions-{Guid.NewGuid():N}");
        try
        {
            var clock = new MutableTimeProvider(new DateTimeOffset(2026, 8, 7, 8, 0, 0, TimeSpan.Zero));
            var service = new ProjectVersionHistoryService(root, maximumVersions: 2, timeProvider: clock);
            var project = BuildProject();
            var path = Path.Combine(root, "project.e4p01");

            await service.SaveVersionAsync(project with { RoomVolume = 10 }, path, "İlk sürüm");
            clock.Advance(TimeSpan.FromMinutes(1));
            await service.SaveVersionAsync(project with { RoomVolume = 20 }, path, "İkinci sürüm");
            clock.Advance(TimeSpan.FromMinutes(1));
            await service.SaveVersionAsync(project with { RoomVolume = 30 }, path, "Üçüncü sürüm");

            var versions = await service.ListVersionsAsync(project, path);
            Assert.Equal(2, versions.Count);
            Assert.Equal("Üçüncü sürüm", versions[0].Description);
            Assert.Equal("İkinci sürüm", versions[1].Description);
            Assert.True(versions.All(v => v.SizeBytes > 0));

            var restored = await service.RestoreVersionAsync(versions[1]);
            Assert.Equal(20, restored.RoomVolume, 10);

            await service.DeleteVersionAsync(versions[1]);
            var afterDelete = await service.ListVersionsAsync(project, path);
            Assert.Single(afterDelete);
            Assert.Equal("Üçüncü sürüm", afterDelete[0].Description);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ViewModelUsesInjectedVersionRootAndDisposeStopsTimer()
    {
        var root = Path.Combine(Path.GetTempPath(), $"eye4pro-m01-vm-versions-{Guid.NewGuid():N}");
        try
        {
            var service = new ProjectVersionHistoryService(root);
            var vm = new ProjectWindowViewModel(BuildProject(), versionHistoryService: service);
            vm.RoomVolume += 3;

            var version = await vm.CreateProjectVersionAsync("Kontrol noktası");
            Assert.NotNull(version);
            Assert.Single(vm.ProjectVersionHistory);
            Assert.StartsWith(Path.GetFullPath(root), Path.GetFullPath(version!.SnapshotPath), StringComparison.Ordinal);

            vm.RoomVolume += 4;
            Assert.True(await vm.RestoreProjectVersionAsync(version));
            Assert.Equal(51, vm.RoomVolume, 10);
            Assert.True(vm.IsDirty);

            vm.StartAutoSave(TimeSpan.FromMilliseconds(25));
            vm.Dispose();
            Assert.Null(await vm.CreateProjectVersionAsync("Dispose sonrası"));
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    // Custom runner kayıtları xUnit ile aynı regresyonları tekrar kullanır.
    internal static void CustomUndoRedoRestoresSnapshotAndSavedBaseline() =>
        new M01DocumentHistoryTests().UndoRedoRestoresSnapshotAndSavedBaseline();

    internal static void CustomUndoRedoPreservesSimulationStaleState() =>
        new M01DocumentHistoryTests().UndoRedoPreservesSimulationStaleState();

    internal static void CustomUndoRestoresGeometryMaterialPointAndSimulationSettings() =>
        new M01DocumentHistoryTests().UndoRestoresGeometryMaterialPointAndSimulationSettings();

    internal static void CustomVersionHistoryPrunesListsRestoresAndDeletes() =>
        new M01DocumentHistoryTests().VersionHistoryPrunesListsRestoresAndDeletes().GetAwaiter().GetResult();

    internal static void CustomViewModelUsesInjectedVersionRootAndDisposeStopsTimer() =>
        new M01DocumentHistoryTests().ViewModelUsesInjectedVersionRootAndDisposeStopsTimer().GetAwaiter().GetResult();

    private static AcousticsProject BuildProject(bool withPoints = false)
    {
        const double lx = 4, ly = 4, lz = 3;
        return new AcousticsProject(
            "Geçmiş Testi",
            null,
            new DateTime(2026, 8, 7, 7, 0, 0, DateTimeKind.Utc),
            SurfaceImportFormat.Dxf,
            SurfaceImportUnit.Meters,
            BoxSurfaces(lx, ly, lz),
            Sources: withPoints
                ? [new AcousticPoint(1, "S1", AcousticPointType.Source, new Point3D(1, 1, 1.5))]
                : [],
            Receivers: withPoints
                ? [new AcousticPoint(2, "R1", AcousticPointType.Receiver, new Point3D(3, 3, 1.5))]
                : [],
            RoomVolume: lx * ly * lz,
            VolumeOrigin: RoomVolumeOrigin.ClosedMesh);
    }

    private static IReadOnlyList<ProjectSurface> BoxSurfaces(double lx, double ly, double lz) =>
    [
        new(1, "Zemin",   "ZEMIN", [new(0,0,0),  new(lx,0,0),  new(lx,ly,0),  new(0,ly,0)]),
        new(2, "Tavan",   "TAVAN", [new(0,0,lz), new(lx,0,lz), new(lx,ly,lz), new(0,ly,lz)]),
        new(3, "DuvarY0", "DUVAR", [new(0,0,0),  new(lx,0,0),  new(lx,0,lz),  new(0,0,lz)]),
        new(4, "DuvarY1", "DUVAR", [new(0,ly,0), new(lx,ly,0), new(lx,ly,lz), new(0,ly,lz)]),
        new(5, "DuvarX0", "DUVAR", [new(0,0,0),  new(0,ly,0),  new(0,ly,lz),  new(0,0,lz)]),
        new(6, "DuvarX1", "DUVAR", [new(lx,0,0), new(lx,ly,0), new(lx,ly,lz), new(lx,0,lz)])
    ];

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Test cleanup asıl assertion'ı gölgelemesin.
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }
}
