using Kst.Acoustics.App.ViewModels;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Importing;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.Tests;

/// <summary>
/// IsDirty; kalıcı proje verisini değiştiren her etkileşimde true olmalı — kategori
/// malzemesi, oda hacmi elle girişi, yeni simülasyon sonucu, ana ekranda oda tipi seçimi.
/// Aynı zamanda taze/az önce yüklenmiş bir projede YANLIŞLIKLA dirty görünmemeli
/// (maincodex M01 incelemesi P1).
/// </summary>
public static class IsDirtyTrackingTests
{
    public static void FreshlyConstructedProjectWindowIsNotDirty()
    {
        var vm = BuildProjectWindowViewModel();
        AssertFalse(vm.IsDirty, "Taze açılmış proje IsDirty=false ile başlamalı.");
    }

    public static void EditingRoomVolumeManuallyMarksDirty()
    {
        var vm = BuildProjectWindowViewModel();
        AssertFalse(vm.IsDirty, "Ön koşul: taze proje temiz olmalı.");

        vm.RoomVolume = vm.RoomVolume + 5;

        AssertTrue(vm.IsDirty, "Oda hacmini elle değiştirmek IsDirty=true yapmalı.");
    }

    public static void ChangingCategoryMaterialMarksDirty()
    {
        var vm = BuildProjectWindowViewModel();
        AssertFalse(vm.IsDirty, "Ön koşul: taze proje temiz olmalı.");

        var row = vm.CategoryMaterialRows.First();
        var alternative = MaterialDatabase.Instance.FindById("carpet_thin")
            ?? throw new InvalidOperationException("Test materyali bulunamadı: carpet_thin");
        row.SelectedMaterial = row.SelectedMaterial.Id == alternative.Id ? MaterialLibrary.Default : alternative;

        AssertTrue(vm.IsDirty, "Kategori malzemesi değişikliği IsDirty=true yapmalı.");
    }

    public static void CompletingSimulationMarksDirty()
    {
        var vm = BuildProjectWindowViewModel(withPoints: true);
        AssertFalse(vm.IsDirty, "Ön koşul: taze proje (kaynak/alıcı dosyadan yüklü) temiz olmalı.");

        vm.RayCount = 2000;
        vm.RunSimulationAsync().GetAwaiter().GetResult();

        AssertTrue(vm.IsDirty, "Simülasyon tamamlanması IsDirty=true yapmalı.");
    }

    public static void SelectedRoomTypeDoesNotDirtyWithoutLoadedProject()
    {
        var vm = new MainWindowViewModel();
        AssertFalse(vm.IsDirty, "Proje yokken taze VM temiz olmalı.");

        vm.SelectedRoomType = vm.RoomTypes.First(r => r != vm.SelectedRoomType);

        AssertFalse(vm.IsDirty,
            "Proje yüklenmeden önce SelectedRoomType değişikliği yanlışlıkla dirty işaretlememeli.");
    }

    public static void SelectedRoomTypeMarksDirtyAfterProjectLoaded()
    {
        var vm = new MainWindowViewModel();
        var project = new AcousticsProject(
            "OdaTipiTest", null, DateTime.UtcNow,
            SurfaceImportFormat.Dxf, SurfaceImportUnit.Meters,
            [new ProjectSurface(1, "Zemin", "ZEMIN", [new(0,0,0), new(4,0,0), new(4,4,0), new(0,4,0)])]);
        vm.UpdateProject(project);
        AssertFalse(vm.IsDirty, "Proje yeni yüklendiğinde IsDirty=false olmalı.");

        vm.SelectedRoomType = vm.RoomTypes.First(r => r != vm.SelectedRoomType);

        AssertTrue(vm.IsDirty, "Proje yüklüyken oda tipi değişikliği IsDirty=true yapmalı.");
    }

    private static ProjectWindowViewModel BuildProjectWindowViewModel(bool withPoints = false)
    {
        const double lx = 4, ly = 4, lz = 3;
        var surfaces = BoxSurfaces(lx, ly, lz);

        var project = new AcousticsProject(
            "DirtyTest", null, DateTime.UtcNow,
            SurfaceImportFormat.Dxf, SurfaceImportUnit.Meters,
            surfaces,
            Sources:    withPoints ? [new AcousticPoint(1, "S", AcousticPointType.Source,   new Point3D(1, 1, 1.5))] : [],
            Receivers:  withPoints ? [new AcousticPoint(2, "R", AcousticPointType.Receiver, new Point3D(3, 3, 1.5))] : [],
            RoomVolume: lx * ly * lz);

        return new ProjectWindowViewModel(project);
    }

    private static List<ProjectSurface> BoxSurfaces(double lx, double ly, double lz) =>
    [
        new(1, "Zemin",   "ZEMIN", [new(0,0,0),  new(lx,0,0),  new(lx,ly,0),  new(0,ly,0)]),
        new(2, "Tavan",   "TAVAN", [new(0,0,lz), new(lx,0,lz), new(lx,ly,lz), new(0,ly,lz)]),
        new(3, "DuvarY0", "DUVAR", [new(0,0,0),  new(lx,0,0),  new(lx,0,lz),  new(0,0,lz)]),
        new(4, "DuvarY1", "DUVAR", [new(0,ly,0), new(lx,ly,0), new(lx,ly,lz), new(0,ly,lz)]),
        new(5, "DuvarX0", "DUVAR", [new(0,0,0),  new(0,ly,0),  new(0,ly,lz),  new(0,0,lz)]),
        new(6, "DuvarX1", "DUVAR", [new(lx,0,0), new(lx,ly,0), new(lx,ly,lz), new(lx,0,lz)])
    ];

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void AssertFalse(bool condition, string message)
    {
        if (condition) throw new InvalidOperationException(message);
    }
}
