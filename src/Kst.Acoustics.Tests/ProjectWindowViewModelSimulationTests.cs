using Kst.Acoustics.App.ViewModels;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Importing;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.Tests;

/// <summary>
/// BuildSimulationProject artık ViewModel'in güncel SourcePoints/ReceiverPoints listesini
/// simülasyona aktarır (_project.Sources/Receivers'ı değil). Bu testler, proje dosyasında
/// hiç kaynak/alıcı yokken kullanıcının UI'da eklediği noktaların simülasyona ulaştığını,
/// ve silinen bir noktanın simülasyondan düştüğünü doğrular.
/// </summary>
public static class ProjectWindowViewModelSimulationTests
{
    public static void EditedSourceAndReceiverReachSimulation()
    {
        var vm = BuildViewModelWithEmptyPoints();

        vm.NewSourceX = "1"; vm.NewSourceY = "1"; vm.NewSourceZ = "1.5";
        vm.AddSource();
        vm.NewReceiverX = "3"; vm.NewReceiverY = "3"; vm.NewReceiverZ = "1.5";
        vm.AddReceiver();
        vm.RayCount = 3000;

        vm.RunSimulationAsync().GetAwaiter().GetResult();

        if (vm.LastResult is null || vm.LastResult.TotalArrivals == 0)
            throw new InvalidOperationException(
                "UI'da eklenen kaynak/alıcı simülasyona ulaşmadı " +
                $"(TotalArrivals={vm.LastResult?.TotalArrivals ?? 0}). " +
                "BuildSimulationProject Sources/Receivers'ı güncel SourcePoints/ReceiverPoints'ten mi alıyor kontrol edin.");
    }

    public static void RemovedReceiverDoesNotReachSimulation()
    {
        var vm = BuildViewModelWithEmptyPoints();

        vm.NewSourceX = "1"; vm.NewSourceY = "1"; vm.NewSourceZ = "1.5";
        vm.AddSource();
        vm.NewReceiverX = "3"; vm.NewReceiverY = "3"; vm.NewReceiverZ = "1.5";
        vm.AddReceiver();

        vm.SelectedReceiver = vm.Receivers[0];
        vm.RemoveSelectedReceiver();

        if (vm.CanRunSimulation)
            throw new InvalidOperationException(
                "Tüm alıcılar silindiğinde CanRunSimulation false olmalı.");
    }

    private static ProjectWindowViewModel BuildViewModelWithEmptyPoints()
    {
        const double lx = 4, ly = 4, lz = 3;
        var surfaces = BoxSurfaces(lx, ly, lz, "DUVAR");

        var project = new AcousticsProject(
            "SimAktarimTest", null, DateTime.UtcNow,
            SurfaceImportFormat.Dxf, SurfaceImportUnit.Meters,
            surfaces,
            Sources:    [],
            Receivers:  [],
            RoomVolume: lx * ly * lz);

        return new ProjectWindowViewModel(project);
    }

    private static List<ProjectSurface> BoxSurfaces(double lx, double ly, double lz, string category) =>
    [
        new(1, "Zemin",   category, [new(0,0,0),  new(lx,0,0),  new(lx,ly,0),  new(0,ly,0)]),
        new(2, "Tavan",   category, [new(0,0,lz), new(lx,0,lz), new(lx,ly,lz), new(0,ly,lz)]),
        new(3, "DuvarY0", category, [new(0,0,0),  new(lx,0,0),  new(lx,0,lz),  new(0,0,lz)]),
        new(4, "DuvarY1", category, [new(0,ly,0), new(lx,ly,0), new(lx,ly,lz), new(0,ly,lz)]),
        new(5, "DuvarX0", category, [new(0,0,0),  new(0,ly,0),  new(0,ly,lz),  new(0,0,lz)]),
        new(6, "DuvarX1", category, [new(lx,0,0), new(lx,ly,0), new(lx,ly,lz), new(lx,0,lz)])
    ];
}
