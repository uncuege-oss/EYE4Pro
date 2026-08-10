using Kst.Acoustics.App.ViewModels;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Importing;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Simulation;
using Kst.Acoustics.Core.Validation;
using Xunit;

namespace Kst.Acoustics.Tests;

public static class M01ModelHealthTests
{
    public static void AnalysisFindsTopologyGeometryMaterialVolumeAndGridRisks()
    {
        var surfaces = new List<ProjectSurface>
        {
            new(1, "Açık", "ATANMAMIS", [new(0,0,0), new(2,0,0), new(2,2,0), new(0,2,0)]),
            new(2, "NM-1", "ATANMAMIS", [new(10,0,0), new(12,0,0), new(11,1,0)]),
            new(3, "NM-2", "ATANMAMIS", [new(12,0,0), new(10,0,0), new(11,-1,0)]),
            new(4, "NM-3", "ATANMAMIS", [new(10,0,0), new(12,0,0), new(11,0,1)]),
            new(5, "Kopya A", "ATANMAMIS", [new(20,0,0), new(22,0,0), new(22,2,0), new(20,2,0)]),
            new(6, "Kopya B", "ATANMAMIS", [new(22,2,0), new(22,0,0), new(20,0,0), new(20,2,0)]),
            new(7, "Mikro", "ATANMAMIS", [new(30,0,0), new(30.005,0,0), new(30,0.005,0)]),
            new(8, "Sıfır", "ATANMAMIS", [new(40,0,0), new(41,0,0), new(42,0,0)])
        };
        var settings = new SimulationParameters(
            ReceiverGrid: new ReceiverGrid(1.2, 0, 100, 0, 100, 1));
        var project = BuildProject(surfaces, roomVolume: 0, settings: settings);

        var report = new ModelHealthService().Analyze(project);
        var codes = report.Findings.Select(f => f.Code).ToHashSet();
        AssertContains(codes, ModelHealthIssueCode.OpenEdge);
        AssertContains(codes, ModelHealthIssueCode.NonManifoldEdge);
        AssertContains(codes, ModelHealthIssueCode.DuplicateSurface);
        AssertContains(codes, ModelHealthIssueCode.MicroSurface);
        AssertContains(codes, ModelHealthIssueCode.ZeroAreaSurface);
        AssertContains(codes, ModelHealthIssueCode.MissingMaterial);
        AssertContains(codes, ModelHealthIssueCode.SuspiciousVolume);
        AssertContains(codes, ModelHealthIssueCode.ExcessiveGrid);
        AssertTrue(report.ErrorCount > 0, "Yapısal riskler hata öneminde raporlanmalı.");
        AssertTrue(report.Findings.Any(f => f.AffectedSurfaceIds.Count > 0),
            "Geometri bulguları viewport için yüzey ID'si taşımalı.");
    }

    public static void ClosedRoomReportsOutsideSourceAndReceiver()
    {
        var project = BuildProject(
            BoxSurfaces(),
            roomVolume: 48,
            sources:
            [
                new AcousticPoint(1, "Dış kaynak", AcousticPointType.Source, new Point3D(9, 9, 9)),
                new AcousticPoint(2, "İç kaynak", AcousticPointType.Source, new Point3D(1, 1, 1))
            ],
            receivers:
            [
                new AcousticPoint(3, "Dış alıcı", AcousticPointType.Receiver, new Point3D(-1, 1, 1)),
                new AcousticPoint(4, "İç alıcı", AcousticPointType.Receiver, new Point3D(2, 2, 1))
            ],
            materials: new Dictionary<string, string> { ["ODA"] = "plaster" });

        var outside = new ModelHealthService().Analyze(project).Findings
            .Where(f => f.Code == ModelHealthIssueCode.PointOutsideRoom)
            .ToArray();
        AssertTrue(outside.Length == 2, "Yalnız oda dışındaki kaynak ve alıcı raporlanmalı.");
        var ids = outside.SelectMany(f => f.AffectedPointIds).ToHashSet();
        AssertTrue(ids.SetEquals([1, 3]), "Dış nokta kimlikleri doğru olmalı.");
    }

    public static void SafeRepairPreviewDoesNotMutateInputAndRepairsGeometry()
    {
        var box = BoxSurfaces();
        box[0] = box[0] with
        {
            Vertices = [box[0].Vertices[0], box[0].Vertices[0], .. box[0].Vertices.Skip(1)]
        };
        box[1] = box[1] with { Vertices = box[1].Vertices.Reverse().ToArray() };
        box.Add(new ProjectSurface(7, "Duvar kopyası", "ODA", box[2].Vertices.Reverse().ToArray()));
        box.Add(new ProjectSurface(8, "Mikro", "ODA",
            [new(1,1,1), new(1.005,1,1), new(1,1.005,1)]));
        var project = BuildProject(box, 48,
            materials: new Dictionary<string, string> { ["ODA"] = "plaster" });
        var originalFloorVertices = project.Surfaces[0].Vertices.ToArray();
        var originalCeilingVertices = project.Surfaces[1].Vertices.ToArray();

        var preview = new ModelHealthService().CreateSafeRepairPreview(project);

        AssertTrue(project.Surfaces.Count == 8, "Önizleme giriş yüzey listesini değiştirmemeli.");
        AssertTrue(project.Surfaces[0].Vertices.SequenceEqual(originalFloorVertices),
            "Önizleme giriş köşe listesini değiştirmemeli.");
        AssertTrue(project.Surfaces[1].Vertices.SequenceEqual(originalCeilingVertices),
            "Winding önizlemesi giriş yüzeyini ters çevirmemeli.");
        AssertTrue(preview.RepairedProject.Surfaces.Count == 6,
            "Kopya ve mikro yüzey çıkarıldıktan sonra kapalı kutunun altı yüzeyi kalmalı.");
        AssertAction(preview, ModelHealthRepairAction.RemoveConsecutiveDuplicateVertex);
        AssertAction(preview, ModelHealthRepairAction.RemoveDuplicateSurface);
        AssertAction(preview, ModelHealthRepairAction.RemoveMicroSurface);
        AssertAction(preview, ModelHealthRepairAction.OrientSurfaceWinding);

        var remainingCodes = preview.RemainingReport.Findings.Select(f => f.Code).ToHashSet();
        AssertTrue(!remainingCodes.Contains(ModelHealthIssueCode.DuplicateSurface),
            "Onarım sonrası yinelenen yüzey kalmamalı.");
        AssertTrue(!remainingCodes.Contains(ModelHealthIssueCode.MicroSurface),
            "Onarım sonrası mikro yüzey kalmamalı.");
        AssertTrue(!remainingCodes.Contains(ModelHealthIssueCode.InconsistentWinding),
            "Onarım sonrası winding tutarlı olmalı.");
        AssertTrue(!remainingCodes.Contains(ModelHealthIssueCode.OpenEdge),
            "Onarım sonrası kutu kapalı kalmalı.");
    }

    public static void ViewModelCommandsPreviewSelectAndApplySafely()
    {
        var surfaces = BoxSurfaces();
        surfaces.Add(new ProjectSurface(7, "Duvar kopyası", "ODA", surfaces[2].Vertices.Reverse().ToArray()));
        var project = BuildProject(surfaces, 48,
            materials: new Dictionary<string, string> { ["ODA"] = "plaster" });
        using var vm = new ProjectWindowViewModel(project);

        vm.AnalyzeModelHealthCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        var duplicate = vm.HealthFindings.FirstOrDefault(f => f.Code == ModelHealthIssueCode.DuplicateSurface)
            ?? throw new InvalidOperationException("ViewModel analizi duplicate bulgusu üretmedi.");
        vm.SelectedHealthFinding = duplicate;
        AssertTrue(vm.HealthSelectedSurfaceIds.Count == 2 && vm.SelectedSurfaceIds.Count == 2,
            "Bulgu yüzey kimlikleri viewport seçimine bağlanmalı.");

        vm.PreviewSafeHealthRepairsCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        AssertTrue(vm.CanApplyHealthRepair && vm.ProjectSurfaces.Count == 7,
            "Önizleme canlı yüzeyleri değiştirmeden uygulanabilir olmalı.");
        vm.ApplySafeHealthRepairsCommand.Execute(null);
        AssertTrue(vm.ProjectSurfaces.Count == 6, "Uygula komutu yalnız önizlenen yüzeyleri aktarmalı.");
        AssertTrue(vm.IsDirty, "Sağlık onarımı belgeyi kirli işaretlemeli.");
    }

    private static AcousticsProject BuildProject(
        IReadOnlyList<ProjectSurface> surfaces,
        double roomVolume,
        IReadOnlyList<AcousticPoint>? sources = null,
        IReadOnlyList<AcousticPoint>? receivers = null,
        IReadOnlyDictionary<string, string>? materials = null,
        SimulationParameters? settings = null) =>
        new(
            "Model Sağlığı Testi",
            null,
            DateTime.UtcNow,
            SurfaceImportFormat.Dxf,
            SurfaceImportUnit.Meters,
            surfaces,
            Sources: sources,
            Receivers: receivers,
            CategoryMaterials: materials,
            RoomVolume: roomVolume,
            VolumeOrigin: RoomVolumeOrigin.ClosedMesh,
            SimulationSettings: settings);

    private static List<ProjectSurface> BoxSurfaces()
    {
        const double lx = 4, ly = 4, lz = 3;
        Point3D[] plan = [new(0,0,0), new(lx,0,0), new(lx,ly,0), new(0,ly,0)];
        var top = plan.Select(p => new Point3D(p.X, p.Y, lz)).ToArray();
        var surfaces = new List<ProjectSurface>
        {
            new(1, "Zemin", "ODA", plan.Reverse().ToArray()),
            new(2, "Tavan", "ODA", top)
        };
        for (var i = 0; i < plan.Length; i++)
        {
            var a = plan[i];
            var b = plan[(i + 1) % plan.Length];
            surfaces.Add(new ProjectSurface(i + 3, $"Duvar {i + 1}", "ODA",
                [a, b, new Point3D(b.X, b.Y, lz), new Point3D(a.X, a.Y, lz)]));
        }
        return surfaces;
    }

    private static void AssertAction(ModelHealthRepairPreview preview, ModelHealthRepairAction action) =>
        AssertTrue(preview.Changes.Any(c => c.Action == action), $"{action} onarım özeti bulunmalı.");

    private static void AssertContains(HashSet<ModelHealthIssueCode> codes, ModelHealthIssueCode code) =>
        AssertTrue(codes.Contains(code), $"{code} bulgusu bekleniyordu.");

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

/// <summary>Standart <c>dotnet test</c> keşfi için M01 model sağlığı regresyonları.</summary>
public sealed class M01ModelHealthXunitTests
{
    [Fact]
    public void AnalysisFindsTopologyGeometryMaterialVolumeAndGridRisks() =>
        M01ModelHealthTests.AnalysisFindsTopologyGeometryMaterialVolumeAndGridRisks();

    [Fact]
    public void ClosedRoomReportsOutsideSourceAndReceiver() =>
        M01ModelHealthTests.ClosedRoomReportsOutsideSourceAndReceiver();

    [Fact]
    public void SafeRepairPreviewDoesNotMutateInputAndRepairsGeometry() =>
        M01ModelHealthTests.SafeRepairPreviewDoesNotMutateInputAndRepairsGeometry();

    [Fact]
    public void ViewModelCommandsPreviewSelectAndApplySafely() =>
        M01ModelHealthTests.ViewModelCommandsPreviewSelectAndApplySafely();
}
