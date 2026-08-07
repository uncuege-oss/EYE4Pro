using Kst.Acoustics.App.ViewModels;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Importing;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Simulation;

namespace Kst.Acoustics.Tests;

/// <summary>
/// Birim dönüşümü yüzey ve kaynak/alıcı koordinatlarını dönüştürür; RoomVolume ise her
/// zaman fiziksel m³ olduğundan değişmez. Özel fiziksel ölçeklemede hacim küple ölçeklenir.
/// </summary>
public static class MainWindowViewModelUnitConversionTests
{
    public static void UnitConversionScalesSurfacesSourcesReceiversAndVolumeTogether()
    {
        const double lx = 4, ly = 4, lz = 3;
        var project = new AcousticsProject(
            "OlcekTest", null, DateTime.UtcNow,
            SurfaceImportFormat.Dxf, SurfaceImportUnit.Meters,
            BoxSurfaces(lx, ly, lz, "DUVAR"),
            Sources:    [new AcousticPoint(1, "S", AcousticPointType.Source,   new Point3D(1, 1, 1.5))],
            Receivers:  [new AcousticPoint(2, "R", AcousticPointType.Receiver, new Point3D(3, 3, 1.5))],
            RoomVolume: lx * ly * lz,
            PanelKonfigurasyon: BuildPanelConfiguration(),
            SimulationSettings: new SimulationParameters(
                ReceiverRadius: 0.5,
                ReceiverGrid: new ReceiverGrid(1.2, 0, 4, 0, 4, 1),
                UnitScale: 1));

        var vm = new MainWindowViewModel();
        vm.UpdateProject(project);

        vm.TargetUnitOption = vm.UnitOptions.First(o => o.Value == SurfaceImportUnit.Millimeters);
        vm.ConvertProjectUnits();

        AssertClose(lx * ly * lz,
            ProjectScalingService.ConvertRoomVolumeM3(
                lx * ly * lz, SurfaceImportUnit.Meters, SurfaceImportUnit.Millimeters),
            1e-9,
            "ProjectScalingService m³ sözleşmesi");

        var scaledSource = vm.Sources[0];
        AssertClose(1000.0, Parse(scaledSource.XText), 1e-6, "Kaynak X (mm dönüşümü sonrası)");
        AssertClose(1500.0, Parse(scaledSource.ZText), 1e-6, "Kaynak Z (mm dönüşümü sonrası)");

        var scaledReceiver = vm.Receivers[0];
        AssertClose(3000.0, Parse(scaledReceiver.XText), 1e-6, "Alıcı X (mm dönüşümü sonrası)");

        var scaledProject = vm.BuildCurrentProject()
            ?? throw new InvalidOperationException("Proje snapshot alınamadı.");
        AssertClose(lx * ly * lz, scaledProject.RoomVolume, 1e-9,
            "Oda hacmi birim dönüşümü sonrası fiziksel m³ olarak sabit kalmalı");
        AssertClose(1000, scaledProject.PanelKonfigurasyon!.Paneller[0].Vertices[1].X, 1e-9,
            "Panel koordinatı birim dönüşümü");
        AssertClose(0.6, scaledProject.PanelKonfigurasyon.PanelEniM, 1e-12,
            "Panel metre ölçüsü birim dönüşümünde sabit kalmalı");
        AssertClose(500, scaledProject.SimulationSettings!.ReceiverRadius, 1e-9,
            "Alıcı yarıçapı proje birimine dönüştürülmeli");
        AssertClose(4000, scaledProject.SimulationSettings.ReceiverGrid!.XMax, 1e-9,
            "Izgara sınırı birim dönüşümü");
        AssertClose(0.001, scaledProject.SimulationSettings.UnitScale, 1e-12,
            "Simülasyon birim ölçeği");
        if (scaledProject.Surfaces[0].OwnerId != "ODA-1")
            throw new InvalidOperationException("Birim dönüşümü yüzey OwnerId bilgisini korumalı.");

        // m → mm → m round-trip: yüzey, kaynak, alıcı ve hacim orijinal değerlere dönmeli.
        vm.TargetUnitOption = vm.UnitOptions.First(o => o.Value == SurfaceImportUnit.Meters);
        vm.ConvertProjectUnits();

        AssertClose(1.0, Parse(vm.Sources[0].XText), 1e-9, "Kaynak X round-trip");
        AssertClose(1.5, Parse(vm.Sources[0].ZText), 1e-9, "Kaynak Z round-trip");
        AssertClose(3.0, Parse(vm.Receivers[0].XText), 1e-9, "Alıcı X round-trip");

        var roundTripProject = vm.BuildCurrentProject()
            ?? throw new InvalidOperationException("Proje snapshot alınamadı.");
        AssertClose(lx * ly * lz, roundTripProject.RoomVolume, 1e-6, "Oda hacmi round-trip");
    }

    public static void CustomPhysicalScaleCubesRoomVolume()
    {
        var project = new AcousticsProject(
            "FizikselOlcek", null, DateTime.UtcNow,
            SurfaceImportFormat.Dxf, SurfaceImportUnit.Meters,
            BoxSurfaces(4, 4, 3, "DUVAR"),
            Sources: [new AcousticPoint(1, "S", AcousticPointType.Source, new Point3D(1, 1, 1))],
            RoomVolume: 48,
            PanelKonfigurasyon: BuildPanelConfiguration(),
            SimulationSettings: new SimulationParameters(
                ReceiverRadius: 0.5,
                ReceiverGrid: new ReceiverGrid(1, 0, 4, 0, 4, 1),
                UnitScale: 1));

        var vm = new MainWindowViewModel();
        vm.UpdateProject(project);
        vm.CustomScaleFactor = "2";
        vm.ApplyCustomScale();

        var scaled = vm.BuildCurrentProject()
            ?? throw new InvalidOperationException("Proje snapshot alınamadı.");
        AssertClose(384, scaled.RoomVolume, 1e-9, "Fiziksel ölçekte hacim");
        AssertClose(2, Parse(vm.Sources[0].XText), 1e-9, "Fiziksel ölçekte kaynak X");
        AssertClose(2, scaled.PanelKonfigurasyon!.Paneller[0].Vertices[1].X, 1e-9,
            "Fiziksel ölçekte panel koordinatı");
        AssertClose(1.2, scaled.PanelKonfigurasyon.PanelEniM, 1e-12,
            "Fiziksel ölçekte panel metre ölçüsü");
        AssertClose(1.0, scaled.PanelKonfigurasyon.SarkmaCikmaM, 1e-12,
            "Fiziksel ölçekte panel sarkması");
        AssertClose(0.5, scaled.SimulationSettings!.ReceiverRadius, 1e-12,
            "Fiziksel ölçekte kullanıcı alıcı yarıçapı sabit kalmalı");
        AssertClose(8, scaled.SimulationSettings.ReceiverGrid!.XMax, 1e-9,
            "Fiziksel ölçekte ızgara sınırı");
        AssertClose(2, scaled.SimulationSettings.ReceiverGrid.CellSize, 1e-9,
            "Fiziksel ölçekte ızgara adımı");
    }

    public static void LegacyResultParametersBecomeScaledProjectSettings()
    {
        var parameters = new SimulationParameters(
            ReceiverRadius: 0.5,
            ReceiverGrid: new ReceiverGrid(1, 0, 4, 0, 4, 1),
            UnitScale: 1);
        var project = new AcousticsProject(
            "EskiAyar", null, DateTime.UtcNow,
            SurfaceImportFormat.Dxf, SurfaceImportUnit.Meters,
            BoxSurfaces(4, 4, 3, "DUVAR"),
            RoomVolume: 48,
            LastResult: new SimulationResult(
                [new EchogramData(1, 1, "S", "R")], parameters, TimeSpan.Zero),
            SimulationSettings: null);

        var vm = new MainWindowViewModel();
        vm.UpdateProject(project);
        vm.TargetUnitOption = vm.UnitOptions.First(o => o.Value == SurfaceImportUnit.Centimeters);
        vm.ConvertProjectUnits();

        var scaled = vm.BuildCurrentProject()
            ?? throw new InvalidOperationException("Eski proje snapshot'ı alınamadı.");
        AssertClose(50, scaled.SimulationSettings!.ReceiverRadius, 1e-9,
            "Eski sonuçtaki alıcı yarıçapı proje birimine dönüştürülmeli");
        AssertClose(400, scaled.SimulationSettings.ReceiverGrid!.XMax, 1e-9,
            "Eski sonuçtaki ızgara sınırı dönüştürülmeli");
        AssertClose(0.01, scaled.SimulationSettings.UnitScale, 1e-12,
            "Eski sonuçtan türetilen UnitScale güncellenmeli");
        if (scaled.LastResult?.IsStale != true)
            throw new InvalidOperationException("Ana ekranda değişen proje kayıtlı sonucu eski işaretlemeli.");
    }

    public static void ExtremeCustomScaleDoesNotMutateProject()
    {
        var project = new AcousticsProject(
            "GuvenliOlcek", null, DateTime.UtcNow,
            SurfaceImportFormat.Dxf, SurfaceImportUnit.Meters,
            BoxSurfaces(4, 4, 3, "DUVAR"),
            Sources: [new AcousticPoint(1, "S", AcousticPointType.Source, new Point3D(1, 1, 1))],
            RoomVolume: 48,
            PanelKonfigurasyon: BuildPanelConfiguration(),
            SimulationSettings: new SimulationParameters(
                ReceiverRadius: 0.5,
                ReceiverGrid: new ReceiverGrid(1, 0, 4, 0, 4, 1),
                UnitScale: 1));
        var vm = new MainWindowViewModel();
        vm.UpdateProject(project);

        foreach (var unsafeFactor in new[] { "1e308", "1e-100" })
        {
            vm.CustomScaleFactor = unsafeFactor;
            vm.ApplyCustomScale();

            var unchanged = vm.BuildCurrentProject()
                ?? throw new InvalidOperationException("Proje snapshot alınamadı.");
            AssertClose(4, unchanged.Surfaces[0].Vertices[1].X, 0,
                $"{unsafeFactor} sonrası yüzey değişmemeli");
            AssertClose(1, unchanged.Sources[0].Position.X, 0,
                $"{unsafeFactor} sonrası kaynak değişmemeli");
            AssertClose(48, unchanged.RoomVolume, 0,
                $"{unsafeFactor} sonrası hacim değişmemeli");
            AssertClose(4, unchanged.SimulationSettings!.ReceiverGrid!.XMax, 0,
                $"{unsafeFactor} sonrası ızgara değişmemeli");
            if (vm.IsDirty)
                throw new InvalidOperationException("Reddedilen ölçek belgeyi kirli işaretlememeli.");
        }
    }

    public static void UnitConversionOverflowDoesNotMutateProject()
    {
        var huge = double.MaxValue / 2;
        var project = new AcousticsProject(
            "TasmasizBirim", null, DateTime.UtcNow,
            SurfaceImportFormat.Dxf, SurfaceImportUnit.Meters,
            [new ProjectSurface(1, "Ucgen", "DUVAR", [
                new Point3D(0, 0, 0), new Point3D(huge, 0, 0), new Point3D(0, 1, 0)])]);
        var vm = new MainWindowViewModel();
        vm.UpdateProject(project);
        vm.TargetUnitOption = vm.UnitOptions.First(o => o.Value == SurfaceImportUnit.Millimeters);

        vm.ConvertProjectUnits();

        var unchanged = vm.BuildCurrentProject()
            ?? throw new InvalidOperationException("Proje snapshot alınamadı.");
        AssertClose(huge, unchanged.Surfaces[0].Vertices[1].X, 0,
            "Taşan birim dönüşümü yüzeyi değiştirmemeli");
        if (unchanged.Unit != SurfaceImportUnit.Meters || vm.IsDirty)
            throw new InvalidOperationException("Taşan birim dönüşümü birimi veya kayıt durumunu değiştirmemeli.");
        if (!vm.StatusMessage.Contains("proje değiştirilmedi", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Taşma kullanıcıya güvenli ve anlaşılır biçimde bildirilmelidir.");
    }

    public static void NestedComponentOverflowDoesNotPartiallyMutateProject()
    {
        var huge = double.MaxValue / 2;
        var baseProject = new AcousticsProject(
            "BilesenTasmasi", null, DateTime.UtcNow,
            SurfaceImportFormat.Dxf, SurfaceImportUnit.Meters,
            BoxSurfaces(4, 4, 3, "DUVAR"),
            RoomVolume: 48);

        var cases = new[]
        {
            baseProject with
            {
                Sources = [new AcousticPoint(1, "S", AcousticPointType.Source, new Point3D(huge, 1, 1))]
            },
            baseProject with
            {
                PanelKonfigurasyon = new AcousticPanelKonfigurasyon
                {
                    PanelEniM = huge,
                    PanelEnleriM = [huge],
                    SarkmaCikmaM = 0.5,
                    AktifTipler = [PanelTipi.Tavan],
                    Paneller = []
                }
            },
            baseProject with
            {
                SimulationSettings = new SimulationParameters(
                    ReceiverRadius: 0.5,
                    ReceiverGrid: new ReceiverGrid(1, 0, huge, 0, 4, 1),
                    UnitScale: 1)
            },
            baseProject with { RoomVolume = huge }
        };

        foreach (var project in cases)
        {
            var vm = new MainWindowViewModel();
            vm.UpdateProject(project);
            vm.CustomScaleFactor = "4";
            vm.ApplyCustomScale();

            var unchanged = vm.BuildCurrentProject()
                ?? throw new InvalidOperationException("Proje snapshot alınamadı.");
            AssertClose(4, unchanged.Surfaces[0].Vertices[1].X, 0,
                "Alt bileşen taşmasında yüzey değişmemeli");
            AssertClose(project.RoomVolume, unchanged.RoomVolume, 0,
                "Alt bileşen taşmasında hacim değişmemeli");
            if (vm.IsDirty)
                throw new InvalidOperationException("Alt bileşen taşması belgeyi kısmen değiştirmemeli/kirletmemeli.");
        }
    }

    private static double Parse(string text) =>
        double.Parse(text, System.Globalization.CultureInfo.InvariantCulture);

    private static void AssertClose(double expected, double actual, double tolerance, string label)
    {
        if (Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException(
                $"{label}: beklenen {expected:F6}, gerçek {actual:F6}, tolerans {tolerance:F6}.");
    }

    private static AcousticPanelKonfigurasyon BuildPanelConfiguration() => new()
    {
        PanelEniM = 0.6,
        PanelEniModu = PanelEniModu.Sirali,
        PanelEnleriM = [0.6, 0.8],
        SarkmaCikmaM = 0.5,
        AktifTipler = [PanelTipi.Tavan],
        Paneller =
        [
            new AcousticPanel
            {
                Tip = PanelTipi.Tavan,
                DilimNo = 1,
                PanelEniM = 0.6,
                SarkmaCikmaM = 0.5,
                Vertices = [new(0,0,2.5), new(1,0,2.5), new(1,1,2.5), new(0,1,2.5)]
            }
        ]
    };

    private static List<ProjectSurface> BoxSurfaces(double lx, double ly, double lz, string category) =>
    [
        new(1, "Zemin",   category, [new(0,0,0),  new(lx,0,0),  new(lx,ly,0),  new(0,ly,0)], "ODA-1"),
        new(2, "Tavan",   category, [new(0,0,lz), new(lx,0,lz), new(lx,ly,lz), new(0,ly,lz)]),
        new(3, "DuvarY0", category, [new(0,0,0),  new(lx,0,0),  new(lx,0,lz),  new(0,0,lz)]),
        new(4, "DuvarY1", category, [new(0,ly,0), new(lx,ly,0), new(lx,ly,lz), new(0,ly,lz)]),
        new(5, "DuvarX0", category, [new(0,0,0),  new(0,ly,0),  new(0,ly,lz),  new(0,0,lz)]),
        new(6, "DuvarX1", category, [new(lx,0,0), new(lx,ly,0), new(lx,ly,lz), new(lx,0,lz)])
    ];
}
