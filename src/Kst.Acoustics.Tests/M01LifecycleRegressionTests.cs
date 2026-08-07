using Kst.Acoustics.App.ViewModels;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Importing;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Simulation;

namespace Kst.Acoustics.Tests;

public static class M01LifecycleRegressionTests
{
    public static void EmptyProjectDoesNotInventRoomVolume()
    {
        var project = ProjectFactory.CreateEmpty("Boş Proje");
        if (project.RoomVolume != 0 || project.VolumeOrigin is not null)
            throw new InvalidOperationException(
                "Boş proje bilinmeyen hacmi sessizce 100 m³ veya kullanıcı girdisi saymamalı.");
        if (project.SemaSurum != AcousticsProjectSchema.CurrentVersion)
            throw new InvalidOperationException("Yeni proje güncel şema sürümüyle başlamalı.");
    }

    public static void NullOrBlankPointLabelIsRejectedWithoutThrowing()
    {
        var row = new AcousticPointViewModel
        {
            Id = 1,
            Label = null!,
            Type = AcousticPointType.Source,
            XText = "1",
            YText = "2",
            ZText = "3"
        };

        if (row.TryBuildPoint() is not null)
            throw new InvalidOperationException("Null nokta etiketi geçerli model üretmemeli.");

        row.Label = "   ";
        if (row.TryBuildPoint() is not null)
            throw new InvalidOperationException("Boş nokta etiketi geçerli model üretmemeli.");
    }


    public static void PointRoundTripPreservesPrecision()
    {
        var original = new AcousticPoint(
            7, "Hassas", AcousticPointType.Source,
            new Point3D(1.234567890123, -2.345678901234, 3.456789012345),
            DirectivityPattern.Cardioid,
            12.345678901, -6.789012345, 0.123456789);

        var rebuilt = AcousticPointViewModel.FromModel(original).TryBuildPoint()
            ?? throw new InvalidOperationException("Geçerli nokta yeniden oluşturulamadı.");

        if (rebuilt.Position != original.Position ||
            rebuilt.DirectivityAzimuthDeg != original.DirectivityAzimuthDeg ||
            rebuilt.DirectivityElevationDeg != original.DirectivityElevationDeg ||
            rebuilt.DirectivityRollDeg != original.DirectivityRollDeg)
            throw new InvalidOperationException("Nokta aç→kaydet dönüşümünde hassasiyet kaybetti.");
    }

    public static void NewlyAddedProjectPointPreservesPrecision()
    {
        var vm = new ProjectWindowViewModel(BuildProject())
        {
            NewSourceX = "1.23456789012345",
            NewSourceY = "2.34567890123456",
            NewSourceZ = "3.45678901234567"
        };
        vm.AddSource();
        var point = vm.Sources.Single().TryBuildPoint()
            ?? throw new InvalidOperationException("Yeni kaynak oluşturulamadı.");
        AssertClose(1.23456789012345, point.Position.X, 0,
            "Yeni kaynak koordinatı yuvarlanmamalı.");
    }

    public static void NonFinitePointIsRejectedInsteadOfDropped()
    {
        var vm = new AcousticPointViewModel
        {
            Id = 1,
            Label = "S1",
            Type = AcousticPointType.Source,
            XText = "NaN",
            YText = "1",
            ZText = "1"
        };

        if (vm.TryBuildPoint() is not null)
            throw new InvalidOperationException("NaN koordinatlı nokta kabul edilmemeli.");

        vm.XText = "1";
        vm.AzimuthText = "Infinity";
        if (vm.TryBuildPoint() is not null)
            throw new InvalidOperationException("Sonsuz yön açılı nokta kabul edilmemeli.");
    }

    public static void ZeroRoomVolumeIsNotSilentlyInvented()
    {
        var vm = new ProjectWindowViewModel(BuildProject() with
        {
            RoomVolume = 0,
            Sources = [new AcousticPoint(1, "S1", AcousticPointType.Source, new Point3D(1, 1, 1))],
            Receivers = [new AcousticPoint(1, "R1", AcousticPointType.Receiver, new Point3D(2, 2, 1))]
        });
        if (vm.RoomVolume != 0)
            throw new InvalidOperationException("Hesaplanamayan oda hacmi sessizce varsayılan bir değere çevrilmemeli.");
        if (vm.CanRunSimulation)
            throw new InvalidOperationException("Pozitif hacim girilmeden simülasyon başlatılamamalı.");
        if (!vm.SimulationPreflightText.Contains("hacmi", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Simülasyon ön kontrolü sıfır hacmi kullanıcıya açıklamalı.");
    }

    public static void ProjectWindowInvalidPointBlocksSnapshot()
    {
        var project = BuildProject() with
        {
            Sources = [new AcousticPoint(1, "S1", AcousticPointType.Source, new Point3D(1, 1, 1))]
        };
        var vm = new ProjectWindowViewModel(project);
        vm.Sources[0].XText = "geçersiz";
        vm.OnPointEdited();

        if (!vm.HasInvalidPoints)
            throw new InvalidOperationException("Geçersiz nokta satırı UI doğrulamasında görünmeli.");
        AssertThrows<InvalidDataException>(() => vm.BuildCurrentProject(),
            "Geçersiz nokta sessizce düşürülmek yerine snapshot'ı engellemeli.");
    }

    public static void TransformMarksProjectDirty()
    {
        var vm = new ProjectWindowViewModel(BuildProject());
        vm.SelectedSurfaceIds = [1];
        vm.TranslateDX = 0.25;
        vm.ApplyTranslation();
        if (!vm.IsDirty)
            throw new InvalidOperationException("Kalıcı yüzey dönüşümü IsDirty=true yapmalı.");
    }

    public static void ImportedProjectStartsDirty()
    {
        var path = Path.Combine(Path.GetTempPath(), $"m01-import-dirty-{Guid.NewGuid():N}.dxf");
        File.WriteAllText(path, BuildSimpleDxf());
        try
        {
            var vm = new MainWindowViewModel();
            vm.ImportAsync(path).GetAwaiter().GetResult();
            if (!vm.IsDirty)
                throw new InvalidOperationException("Yeni DXF importu ilk kayda kadar kirli olmalı.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public static void FailedImportPreservesOpenDocument()
    {
        var validPath = Path.Combine(Path.GetTempPath(), $"m01-import-valid-{Guid.NewGuid():N}.dxf");
        var invalidPath = Path.Combine(Path.GetTempPath(), $"m01-import-invalid-{Guid.NewGuid():N}.txt");
        File.WriteAllText(validPath, BuildSimpleDxf());
        File.WriteAllText(invalidPath, "geçersiz");
        try
        {
            var vm = new MainWindowViewModel();
            vm.ImportAsync(validPath).GetAwaiter().GetResult();
            var before = vm.BuildCurrentProject()
                ?? throw new InvalidOperationException("Ön koşul projesi kurulamadı.");

            vm.ImportAsync(invalidPath).GetAwaiter().GetResult();
            var after = vm.BuildCurrentProject()
                ?? throw new InvalidOperationException("Başarısız import açık projeyi temizledi.");

            if (after.Surfaces.Count != before.Surfaces.Count || after.Name != before.Name)
                throw new InvalidOperationException("Başarısız import açık belgeyi değiştirdi.");
        }
        finally
        {
            if (File.Exists(validPath)) File.Delete(validPath);
            if (File.Exists(invalidPath)) File.Delete(invalidPath);
        }
    }

    public static void InvalidSavePreservesExistingTarget()
    {
        var path = Path.Combine(Path.GetTempPath(), $"m01-preserve-{Guid.NewGuid():N}.e4p01");
        const string sentinel = "sağlam-eski-proje";
        File.WriteAllText(path, sentinel);
        try
        {
            var invalid = BuildProject() with { RoomVolume = double.NaN };
            AssertThrows<InvalidDataException>(() => new ProjectFileService().Save(path, invalid),
                "Geçersiz proje save-side doğrulamada reddedilmeli.");
            if (File.ReadAllText(path) != sentinel)
                throw new InvalidOperationException("Başarısız kayıt sağlam hedef dosyayı değiştirdi.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
        }
    }

    public static void NullVertexCollectionIsRejectedCleanly()
    {
        var project = BuildProject() with
        {
            Surfaces = [new ProjectSurface(1, "Bozuk", "DUVAR", null!)]
        };
        AssertThrows<InvalidDataException>(() => AcousticsProjectMigrator.MigrateAndValidate(project),
            "Null vertex koleksiyonu kontrollü doğrulama hatası üretmeli.");
    }

    public static void MalformedSimulationPayloadIsRejectedCleanly()
    {
        var missingEchograms = BuildProject() with
        {
            LastResult = new SimulationResult
            {
                Echograms = null!,
                Parameters = new SimulationParameters()
            }
        };
        AssertThrows<InvalidDataException>(
            () => AcousticsProjectMigrator.MigrateAndValidate(missingEchograms),
            "Null ekogram koleksiyonu kontrollü doğrulama hatası üretmeli.");

        var malformedBands = BuildProject() with
        {
            LastResult = new SimulationResult
            {
                Echograms = [new EchogramData(1, 1, "S", "R")],
                Parameters = new SimulationParameters(),
                ComputedParameters = [new AcousticParameters(1, 1, "S", "R", [])]
            }
        };
        AssertThrows<InvalidDataException>(
            () => AcousticsProjectMigrator.MigrateAndValidate(malformedBands),
            "Sekiz bandı olmayan kayıtlı sonuç kontrollü doğrulama hatası üretmeli.");

        var invalidSettings = BuildProject() with
        {
            SimulationSettings = new SimulationParameters(ReceiverRadius: double.NaN)
        };
        AssertThrows<InvalidDataException>(
            () => AcousticsProjectMigrator.MigrateAndValidate(invalidSettings),
            "Geçersiz simülasyon ayarı kontrollü doğrulama hatası üretmeli.");
    }

    public static void VolumeOriginAndWarningsRoundTrip()
    {
        var path = Path.Combine(Path.GetTempPath(), $"m01-origin-{Guid.NewGuid():N}.e4p01");
        try
        {
            var project = BuildProject() with
            {
                VolumeOrigin = RoomVolumeOrigin.SourceModel,
                ImportWarnings = ["Kaynak geometri uyarısı"]
            };
            var service = new ProjectFileService();
            service.Save(path, project);
            var loaded = service.Load(path);
            if (loaded.VolumeOrigin != RoomVolumeOrigin.SourceModel)
                throw new InvalidOperationException("Hacim kaynağı round-trip sırasında kayboldu.");
            if (!loaded.ImportWarnings.Contains("Kaynak geometri uyarısı"))
                throw new InvalidOperationException("İçe aktarma uyarısı round-trip sırasında kayboldu.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
        }
    }

    public static void CorruptPrimaryRecoversLastBackup()
    {
        var path = Path.Combine(Path.GetTempPath(), $"m01-backup-{Guid.NewGuid():N}.e4p01");
        try
        {
            var service = new ProjectFileService();
            service.Save(path, BuildProject() with { Name = "İlk Sağlam" });
            service.Save(path, BuildProject() with { Name = "İkinci Sağlam" });
            File.WriteAllText(path, "{bozuk-json");

            AssertThrows<System.Text.Json.JsonException>(
                () => service.Load(path),
                "Durum döndürmeyen Load bozuk primary'yi sessizce backup ile değiştirmemeli.");

            var loadResult = service.LoadWithStatusAsync(path).GetAwaiter().GetResult();
            var recovered = loadResult.Project;
            if (!loadResult.RecoveredFromBackup || recovered.Name != "İlk Sağlam")
                throw new InvalidOperationException("Son .bak sürümü kurtarılamadı.");
            if (!loadResult.SessionWarnings.Any(w => w.Contains(".bak", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Yedekten kurtarma kullanıcıya açıkça bildirilmedi.");
            if (recovered.ImportWarnings.Any(w => w.Contains(".bak", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Oturuma özel kurtarma uyarısı proje verisine karışmamalı.");

            // Yeni servis örneği kullan: güvenlik, aynı instance'ın recovery
            // durumunu bellekte tutmasına değil dosyaların geçerliliğine dayanmalı.
            new ProjectFileService().Save(path, recovered, preserveExistingBackup: true);
            var reopened = service.LoadWithStatusAsync(path).GetAwaiter().GetResult();
            if (reopened.RecoveredFromBackup || reopened.SessionWarnings.Count != 0)
                throw new InvalidOperationException("Onarılan primary dosya sonraki açılışta hâlâ yedek uyarısı vermemeli.");
            if (reopened.Project.Name != "İlk Sağlam")
                throw new InvalidOperationException("Onarılan primary kurtarılan sağlam içeriği korumadı.");

            var backup = new ProjectFileService().Load(path + ".bak");
            if (backup.Name != "İlk Sağlam")
                throw new InvalidOperationException(
                    "Kurtarma sonrası kayıt sağlam .bak'ı bozuk primary ile ezdi.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
        }
    }

    public static void NormalOverwriteBacksUpEvenInvalidTarget()
    {
        var path = Path.Combine(Path.GetTempPath(), $"m01-invalid-target-{Guid.NewGuid():N}.e4p01");
        const string previousContent = "{elle-duzenlenmis-hedef";
        try
        {
            File.WriteAllText(path, previousContent);
            new ProjectFileService().Save(path, BuildProject() with { Name = "Yeni Sağlam" });

            var loaded = new ProjectFileService().Load(path);
            if (loaded.Name != "Yeni Sağlam")
                throw new InvalidOperationException("Yeni snapshot hedefe yazılamadı.");
            if (!File.Exists(path + ".bak") || File.ReadAllText(path + ".bak") != previousContent)
                throw new InvalidOperationException(
                    "Normal overwrite mevcut geçersiz/elle düzenlenmiş hedefi de .bak olarak korumalı.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
        }
    }

    public static void SchemaRecoveryDistinguishesCorruptionFromNewerFormat()
    {
        var path = Path.Combine(Path.GetTempPath(), $"m01-schema-recovery-{Guid.NewGuid():N}.e4p01");
        try
        {
            var service = new ProjectFileService();
            service.Save(path, BuildProject() with { Name = "Yedek Sağlam" });
            service.Save(path, BuildProject() with { Name = "Primary Sağlam" });

            var primaryJson = File.ReadAllText(path);
            var currentToken = $"\"semaSurum\": {AcousticsProjectSchema.CurrentVersion}";
            if (!primaryJson.Contains(currentToken, StringComparison.Ordinal))
                throw new InvalidOperationException("Test kurulumu güncel şema alanını bulamadı.");

            File.WriteAllText(path, primaryJson.Replace(
                currentToken, "\"semaSurum\": 0", StringComparison.Ordinal));
            var recovered = service.LoadWithStatusAsync(path).GetAwaiter().GetResult();
            if (!recovered.RecoveredFromBackup || recovered.Project.Name != "Yedek Sağlam")
                throw new InvalidOperationException(
                    "Geçersiz/non-positive şema bozulması sağlam backup'tan kurtarılmalı.");

            var futureToken = $"\"semaSurum\": {AcousticsProjectSchema.CurrentVersion + 1}";
            File.WriteAllText(path, primaryJson.Replace(
                currentToken, futureToken, StringComparison.Ordinal));
            AssertThrows<UnsupportedProjectSchemaException>(
                () => service.LoadWithStatusAsync(path).GetAwaiter().GetResult(),
                "İleri şemalı primary dosya eski backup ile sessizce değiştirilmemeli.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
        }
    }

    public sealed class RecoveryXunitTests
    {
        [Xunit.Fact]
        public void CorruptPrimaryRecoversAndPreservesHealthyBackup() =>
            CorruptPrimaryRecoversLastBackup();

        [Xunit.Fact]
        public void NormalOverwritePreservesPreviousInvalidTargetAsBackup() =>
            NormalOverwriteBacksUpEvenInvalidTarget();

        [Xunit.Fact]
        public void SchemaRecoveryDistinguishesCorruptionFromNewerFormat() =>
            M01LifecycleRegressionTests.SchemaRecoveryDistinguishesCorruptionFromNewerFormat();
    }

    private static AcousticsProject BuildProject() => new(
        "M01 Regresyon", null, DateTime.UtcNow,
        SurfaceImportFormat.Dxf, SurfaceImportUnit.Meters,
        [
            new ProjectSurface(1, "Zemin", "ZEMIN",
                [new(0,0,0), new(4,0,0), new(4,4,0), new(0,4,0)])
        ],
        RoomVolume: 48);

    private static string BuildSimpleDxf() => """
0
SECTION
2
HEADER
9
$INSUNITS
70
6
0
ENDSEC
0
SECTION
2
ENTITIES
0
3DFACE
10
0
20
0
30
0
11
4
21
0
31
0
12
4
22
4
32
0
13
0
23
4
33
0
0
ENDSEC
0
EOF
""";

    private static void AssertThrows<T>(Action action, string message) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException(message);
    }

    private static void AssertClose(double expected, double actual, double tolerance, string message)
    {
        if (Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException(message + $" Beklenen: {expected:R}, gerçek: {actual:R}.");
    }
}
