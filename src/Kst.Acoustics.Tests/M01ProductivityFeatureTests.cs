using System.IO.Compression;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Importing;
using Kst.Acoustics.Core.Projects;
using Xunit;

namespace Kst.Acoustics.Tests;

public static class M01ProductivityFeatureTests
{
    public static void PlacementUsesPhysicalMetersAcrossProjectUnits()
    {
        var meters = AcousticPointPlacementService.Generate(new AcousticPointPlacementRequest(
            AcousticPointType.Receiver,
            AcousticPointArrayPattern.Linear,
            new Point3D(0, 0, 1),
            CountX: 3,
            SpacingMeters: 1,
            Unit: SurfaceImportUnit.Meters,
            KeepInsideRoom: false));
        var millimeters = AcousticPointPlacementService.Generate(new AcousticPointPlacementRequest(
            AcousticPointType.Receiver,
            AcousticPointArrayPattern.Linear,
            new Point3D(0, 0, 1000),
            CountX: 3,
            SpacingMeters: 1,
            Unit: SurfaceImportUnit.Millimeters,
            KeepInsideRoom: false));

        AssertClose(2, meters.Points[2].Position.X, 0, "Metre yerleştirme aralığı");
        AssertClose(2000, millimeters.Points[2].Position.X, 0, "Milimetre yerleştirme aralığı");
        AssertTrue(meters.Points.Select(point => point.Id).SequenceEqual([1, 2, 3]),
            "Yerleştirme kimlikleri kararlı olmalı.");
    }

    public static void PlacementBoundaryAndSafetyLimitAreEnforced()
    {
        var boundary = new[]
        {
            new Point3D(0, 0, 0), new Point3D(3.1, 0, 0),
            new Point3D(3.1, 3.1, 0), new Point3D(0, 3.1, 0)
        };
        var result = AcousticPointPlacementService.Generate(new AcousticPointPlacementRequest(
            AcousticPointType.Receiver,
            AcousticPointArrayPattern.BoundaryGrid,
            new Point3D(0, 0, 1.2),
            CountX: 1,
            CountY: 1,
            SpacingMeters: 1,
            Boundary: boundary,
            KeepInsideRoom: false));
        AssertTrue(result.Points.Count is > 0 and <= 16, "Sınır ızgarası yalnız poligon içinde kalmalı.");
        AssertTrue(result.Points.All(point => point.Position.X is >= 0 and <= 3.1 &&
                                              point.Position.Y is >= 0 and <= 3.1),
            "Sınır dışı nokta üretilmemeli.");

        AssertThrows<InvalidDataException>(() => AcousticPointPlacementService.Generate(
            new AcousticPointPlacementRequest(
                AcousticPointType.Receiver,
                AcousticPointArrayPattern.RectangularGrid,
                new Point3D(0, 0, 0),
                CountX: 100,
                CountY: 100,
                MaximumPointCount: 500,
                KeepInsideRoom: false)),
            "Aşırı yerleştirme, bellek ayrılmadan önce reddedilmeli.");
    }

    public static void MaterialMappingMatchesTurkishNamesAndPersistsTemplates()
    {
        var path = Path.Combine(Path.GetTempPath(), $"m01-mapping-{Guid.NewGuid():N}.json");
        try
        {
            var service = new MaterialMappingTemplateService(path);
            var surfaces = new[]
            {
                Surface(1, "Tavan 1", "AKUSTİK TAVAN"),
                Surface(2, "Pencere 1", "CAM")
            };
            var matches = service.Apply(
                MaterialMappingTemplateService.BuiltInTemplate,
                surfaces,
                new HashSet<string>(["acoustic_ceiling", "glass"], StringComparer.OrdinalIgnoreCase));
            AssertTrue(matches.Any(match => match.Category == "AKUSTİK TAVAN" &&
                                            match.MaterialId == "acoustic_ceiling"),
                "Türkçe kategori aksan duyarsız eşlenmeli.");
            AssertTrue(matches.Any(match => match.Category == "CAM" && match.MaterialId == "glass"),
                "Cam kategorisi yerleşik şablonla eşlenmeli.");

            var custom = MaterialMappingTemplateService.FromExactCategoryAssignments(
                "Ofis standardı",
                new Dictionary<string, string> { ["DUVAR"] = "plaster" });
            service.SaveUserTemplatesAsync([custom]).GetAwaiter().GetResult();
            var loaded = service.LoadAsync().GetAwaiter().GetResult();
            AssertTrue(loaded.Any(template => template.Id == custom.Id && !template.IsBuiltIn),
                "Kullanıcı materyal şablonu kalıcı olmalı.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public static void ProjectPackageRoundTripsWithReferencedLibrariesAndSource()
    {
        var root = Path.Combine(Path.GetTempPath(), $"m01-package-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var sourcePath = Path.Combine(root, "salon.dxf");
        var packagePath = Path.Combine(root, "salon.e4pkg");
        File.WriteAllText(sourcePath, "0\nSECTION\n0\nEOF\n");
        try
        {
            var material = new AcousticMaterial(
                "package-mat", "Paket Materyali",
                Enumerable.Repeat(0.25, 8).ToArray(),
                Enumerable.Repeat(0.10, 8).ToArray());
            var loudspeaker = new AcousticLoudspeaker(
                "package-speaker", "EYE4", "Test",
                Enumerable.Repeat(90d, 8).ToArray(), []);
            var project = BuildProject(sourcePath) with
            {
                CategoryMaterials = new Dictionary<string, string> { ["ODA"] = material.Id },
                Sources =
                [
                    new AcousticPoint(1, "S1", AcousticPointType.Source, new Point3D(1, 1, 1),
                        LoudspeakerId: loudspeaker.Id)
                ]
            };
            var service = new ProjectPackageService();
            var created = service.CreateAsync(packagePath, project, [material], [loudspeaker])
                .GetAwaiter().GetResult();
            AssertTrue(created.SourceIncluded && created.IncludedMaterialCount == 1 &&
                       created.IncludedLoudspeakerCount == 1,
                "Paket referanslı kütüphaneleri ve kaynak geometriyi içermeli.");

            var validation = service.ValidateAsync(packagePath).GetAwaiter().GetResult();
            AssertTrue(validation.IsValid && validation.Manifest?.Entries.Count == 5,
                "Yeni paket manifest ve hash denetimini geçmeli.");
            var loaded = service.LoadProjectAsync(packagePath).GetAwaiter().GetResult();
            AssertTrue(loaded.Project.Name == project.Name && loaded.Project.Surfaces.Count == project.Surfaces.Count,
                "Paket içindeki proje round-trip korunmalı.");
            AssertTrue(loaded.Materials.Single().Id == material.Id &&
                       loaded.Loudspeakers.Single().Id == loudspeaker.Id,
                "Paket kütüphane tanımları yeniden okunabilmeli.");
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    public static void ProjectPackageDetectsEntryTampering()
    {
        var root = Path.Combine(Path.GetTempPath(), $"m01-package-tamper-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var packagePath = Path.Combine(root, "tamper.e4pkg");
        try
        {
            var service = new ProjectPackageService();
            service.CreateAsync(packagePath, BuildProject(), [], []).GetAwaiter().GetResult();
            using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Update))
            {
                archive.GetEntry("README.txt")!.Delete();
                using var writer = new StreamWriter(archive.CreateEntry("README.txt").Open());
                writer.Write("değiştirildi");
            }

            var validation = service.ValidateAsync(packagePath).GetAwaiter().GetResult();
            AssertTrue(!validation.IsValid && validation.Message.Contains("bütünlük", StringComparison.OrdinalIgnoreCase),
                "Manifestten sonra değiştirilen girdi reddedilmeli.");
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    public static void ViewportReductionIsDeterministicBalancedAndKeepsPinnedExtrema()
    {
        var surfaces = Enumerable.Range(1, 20)
            .Select(id => Surface(id, $"Yüzey {id}", $"K{id % 3}", x: id))
            .ToArray();
        var first = ViewportSurfaceDecimator.Reduce(surfaces, 8, [17]);
        var second = ViewportSurfaceDecimator.Reduce(surfaces, 8, [17]);

        AssertTrue(first.IsReduced && first.Surfaces.Count == 8 && first.HiddenCount == 12,
            "Viewport indirgeme sınıra uymalı.");
        AssertTrue(first.Surfaces.Any(surface => surface.Id == 17), "Seçili yüzey korunmalı.");
        AssertTrue(first.Surfaces.Any(surface => surface.Id == 1) &&
                   first.Surfaces.Any(surface => surface.Id == 20),
            "Geometrik minimum ve maksimum kapsam korunmalı.");
        AssertTrue(first.Surfaces.Select(surface => surface.Category).Distinct().Count() == 3,
            "Kategoriler dengeli temsil edilmeli.");
        AssertTrue(first.Surfaces.Select(surface => surface.Id)
                .SequenceEqual(second.Surfaces.Select(surface => surface.Id)),
            "Aynı girdi için viewport seçimi deterministik olmalı.");
    }

    private static AcousticsProject BuildProject(string? sourcePath = null)
    {
        const double x = 4, y = 3, z = 2.5;
        var surfaces = new[]
        {
            new ProjectSurface(1, "Zemin", "ODA", [new(0,0,0), new(0,y,0), new(x,y,0), new(x,0,0)]),
            new ProjectSurface(2, "Tavan", "ODA", [new(0,0,z), new(x,0,z), new(x,y,z), new(0,y,z)]),
            new ProjectSurface(3, "Duvar 1", "ODA", [new(0,0,0), new(x,0,0), new(x,0,z), new(0,0,z)]),
            new ProjectSurface(4, "Duvar 2", "ODA", [new(x,0,0), new(x,y,0), new(x,y,z), new(x,0,z)]),
            new ProjectSurface(5, "Duvar 3", "ODA", [new(x,y,0), new(0,y,0), new(0,y,z), new(x,y,z)]),
            new ProjectSurface(6, "Duvar 4", "ODA", [new(0,y,0), new(0,0,0), new(0,0,z), new(0,y,z)])
        };
        return new AcousticsProject(
            "Paket Testi", sourcePath, DateTime.UtcNow,
            SurfaceImportFormat.Dxf, SurfaceImportUnit.Meters, surfaces,
            RoomVolume: x * y * z,
            VolumeOrigin: RoomVolumeOrigin.ClosedMesh)
        {
            SemaSurum = AcousticsProjectSchema.CurrentVersion
        };
    }

    private static ProjectSurface Surface(int id, string name, string category, double x = 0) =>
        new(id, name, category,
        [
            new Point3D(x, 0, 0), new Point3D(x + 0.8, 0, 0),
            new Point3D(x + 0.8, 0.8, 0), new Point3D(x, 0.8, 0)
        ]);

    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path)) return;
        try { Directory.Delete(path, recursive: true); } catch { }
    }

    private static void AssertClose(double expected, double actual, double tolerance, string message)
    {
        if (Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException($"{message}: beklenen {expected}, gerçek {actual}.");
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void AssertThrows<TException>(Action action, string message) where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        throw new InvalidOperationException(message);
    }
}

public sealed class M01ProductivityFeatureXunitTests
{
    [Fact]
    public void PlacementIsUnitAware() =>
        M01ProductivityFeatureTests.PlacementUsesPhysicalMetersAcrossProjectUnits();

    [Fact]
    public void PlacementBoundaryAndLimitAreSafe() =>
        M01ProductivityFeatureTests.PlacementBoundaryAndSafetyLimitAreEnforced();

    [Fact]
    public void MaterialTemplatesMatchAndPersist() =>
        M01ProductivityFeatureTests.MaterialMappingMatchesTurkishNamesAndPersistsTemplates();

    [Fact]
    public void PackageRoundTrips() =>
        M01ProductivityFeatureTests.ProjectPackageRoundTripsWithReferencedLibrariesAndSource();

    [Fact]
    public void PackageTamperingIsDetected() =>
        M01ProductivityFeatureTests.ProjectPackageDetectsEntryTampering();

    [Fact]
    public void ViewportReductionIsStable() =>
        M01ProductivityFeatureTests.ViewportReductionIsDeterministicBalancedAndKeepsPinnedExtrema();
}
