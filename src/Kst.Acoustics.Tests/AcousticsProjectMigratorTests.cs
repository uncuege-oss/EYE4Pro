using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Importing;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Simulation;

namespace Kst.Acoustics.Tests;

/// <summary>
/// M01ProjectMigrator/Validator: şema sürümü kontrolü, yinelenen ID, dejenere yüzey ve
/// NaN/Infinity koordinat tespiti. Bunlar olmadan bozuk bir .e4p01 dosyası sessizce
/// açılıp hacim/RT60/ışın izleme hesaplarını NaN'a sürükleyebilir (maincodex M01 P1).
/// </summary>
public static class AcousticsProjectMigratorTests
{
    public static void ValidMinimalProjectPassesUnchanged()
    {
        var project = BuildValidProject();
        var result = AcousticsProjectMigrator.MigrateAndValidate(project);

        AssertTrue(result.Surfaces.Count == project.Surfaces.Count, "Geçerli proje değişmeden dönmeli.");
    }

    public static void FutureSchemaVersionIsRejected()
    {
        var project = BuildValidProject() with { SemaSurum = AcousticsProjectSchema.CurrentVersion + 1 };

        AssertThrows<UnsupportedProjectSchemaException>(
            () => AcousticsProjectMigrator.MigrateAndValidate(project),
            "İleri şema sürümü sessizce açılmamalı.");
    }

    public static void NonPositiveSchemaVersionIsRejected()
    {
        foreach (var version in new[] { 0, -1 })
        {
            var project = BuildValidProject() with { SemaSurum = version };
            AssertThrows<InvalidDataException>(
                () => AcousticsProjectMigrator.MigrateAndValidate(project),
                $"Sıfır/negatif şema sürümü ({version}) güncel sürüm diye damgalanmamalı.");
        }
    }

    public static void DuplicateSurfaceIdsAreRejected()
    {
        var surfaces = BoxSurfaces();
        surfaces[1] = surfaces[1] with { Id = surfaces[0].Id };
        var project = BuildValidProject() with { Surfaces = surfaces };

        AssertThrows<InvalidDataException>(
            () => AcousticsProjectMigrator.MigrateAndValidate(project),
            "Yinelenen yüzey ID'si reddedilmeli.");
    }

    public static void DegenerateSurfaceWithFewerThanThreeVerticesIsRejected()
    {
        var surfaces = BoxSurfaces();
        surfaces[0] = surfaces[0] with { Vertices = [new Point3D(0, 0, 0), new Point3D(1, 0, 0)] };
        var project = BuildValidProject() with { Surfaces = surfaces };

        AssertThrows<InvalidDataException>(
            () => AcousticsProjectMigrator.MigrateAndValidate(project),
            "Üçten az köşeli yüzey reddedilmeli.");
    }

    public static void NaNVertexCoordinateIsRejected()
    {
        var surfaces = BoxSurfaces();
        var verts = surfaces[0].Vertices.ToList();
        verts[0] = new Point3D(double.NaN, 0, 0);
        surfaces[0] = surfaces[0] with { Vertices = verts };
        var project = BuildValidProject() with { Surfaces = surfaces };

        AssertThrows<InvalidDataException>(
            () => AcousticsProjectMigrator.MigrateAndValidate(project),
            "NaN köşe koordinatı reddedilmeli.");
    }

    public static void CollinearSurfaceIsRejectedButThinValidSurfacePasses()
    {
        var surfaces = BoxSurfaces();
        surfaces[0] = surfaces[0] with
        {
            Vertices = [new(0, 0, 0), new(1, 0, 0), new(2, 0, 0), new(3, 0, 0)]
        };
        AssertThrows<InvalidDataException>(
            () => AcousticsProjectMigrator.MigrateAndValidate(
                BuildValidProject() with { Surfaces = surfaces }),
            "Eşdoğrusal/sıfır alanlı yüzey reddedilmeli.");

        surfaces = BoxSurfaces();
        surfaces[0] = surfaces[0] with
        {
            Vertices = [new(0, 0, 0), new(4, 0, 0), new(4, 1e-9, 0), new(0, 1e-9, 0)]
        };
        _ = AcousticsProjectMigrator.MigrateAndValidate(
            BuildValidProject() with { Surfaces = surfaces });
    }

    public static void DuplicateSourceIdIsRejected()
    {
        var project = BuildValidProject() with
        {
            Sources =
            [
                new AcousticPoint(1, "S1", AcousticPointType.Source, new Point3D(1, 1, 1)),
                new AcousticPoint(1, "S2", AcousticPointType.Source, new Point3D(2, 2, 1))
            ]
        };

        AssertThrows<InvalidDataException>(
            () => AcousticsProjectMigrator.MigrateAndValidate(project),
            "Yinelenen kaynak ID'si reddedilmeli.");
    }

    public static void MalformedPanelDefinitionsAreRejected()
    {
        AssertInvalidPanel(config => config.Paneller[0].Id = Guid.Empty,
            "Boş panel GUID'i reddedilmeli.");
        AssertInvalidPanel(config => config.Paneller[0].Tip = (PanelTipi)999,
            "Bilinmeyen panel tipi reddedilmeli.");
        AssertInvalidPanel(config => config.Paneller[0].PanelEniM = -0.1,
            "Negatif panel eni reddedilmeli.");
        AssertInvalidPanel(config => config.Paneller[0].Vertices =
            [new(0, 0, 2), new(1, 0, 2), new(2, 0, 2)],
            "Eşdoğrusal panel köşeleri reddedilmeli.");
        AssertInvalidPanel(config => config.Paneller[0].Vertices =
            [new(0, 0, 2), new(1, 0, 2)],
            "Üçten az panel köşesi reddedilmeli.");
        AssertInvalidPanel(config => config.PanelEniM = 0,
            "Etkin tek tip panel konfigürasyonunda sıfır en reddedilmeli.");
        AssertInvalidPanel(config =>
        {
            config.PanelEniModu = PanelEniModu.Sirali;
            config.PanelEnleriM = [0.8, 0];
        }, "Etkin sıralı konfigürasyonda sıfır en reddedilmeli.");
    }

    public static void SimulationSettingsUseUiRangesAndPhysicalReceiverRadius()
    {
        var project = BuildMillimeterProject();
        var valid = new SimulationParameters(
            RayCount: 5_000,
            MaxOrder: 150,
            ReceiverRadius: 750,
            AirTemperatureCelsius: 20,
            RelativeHumidity: 50,
            TransitionOrder: 30,
            UnitScale: 0.001,
            ConvergenceModeIndex: 1);

        _ = AcousticsProjectMigrator.MigrateAndValidate(project with
        {
            SemaSurum = AcousticsProjectSchema.CurrentVersion,
            SimulationSettings = valid
        });

        var invalidSettings = new[]
        {
            valid with { RayCount = 99 },
            valid with { RayCount = 100_001 },
            valid with { MaxOrder = 9 },
            valid with { MaxOrder = 501 },
            valid with { TransitionOrder = 4 },
            valid with { TransitionOrder = 151 },
            valid with { AirTemperatureCelsius = -20.1 },
            valid with { RelativeHumidity = 9.9 },
            valid with { ConvergenceModeIndex = 3 },
            valid with { ReceiverRadius = 9 },
            valid with { ReceiverRadius = 10_001 },
            valid with { ReceiverRadius = 0.5, UnitScale = 1 }
        };

        foreach (var settings in invalidSettings)
            AssertThrows<InvalidDataException>(
                () => AcousticsProjectMigrator.MigrateAndValidate(project with
                {
                    SemaSurum = AcousticsProjectSchema.CurrentVersion,
                    SimulationSettings = settings
                }),
                "UI aralığı veya proje birim damgası dışındaki simülasyon ayarı reddedilmeli.");
    }

    public static void V1UnitScaleMigratesWithoutRewritingHistoricalResult()
    {
        var historicalParameters = new SimulationParameters(
            ReceiverRadius: 0.5,
            UnitScale: 1);
        var project = BuildMillimeterProject() with
        {
            SemaSurum = 1,
            SimulationSettings = new SimulationParameters(
                ReceiverRadius: 750,
                UnitScale: 1),
            LastResult = new SimulationResult(
                [new EchogramData(1, 2, "S", "R")],
                historicalParameters,
                TimeSpan.Zero)
            {
                IsStale = true,
                StaleReason = "Birim dönüştü."
            }
        };

        var migrated = AcousticsProjectMigrator.MigrateAndValidate(project);
        AssertClose(0.001, migrated.SimulationSettings!.UnitScale, 1e-12,
            "v1 güncel simülasyon ayarı proje birimine migrate edilmeli.");
        AssertClose(1, migrated.LastResult!.Parameters.UnitScale, 0,
            "Stale tarihsel sonucun eski birim damgası yeniden yazılmamalı.");
    }

    public static void ComputedPairIdsMustMatchEchogram()
    {
        var project = BuildValidProject() with
        {
            LastResult = BuildResult(
                BuildAcousticParameters(sourceId: 9, receiverId: 2))
        };

        AssertThrows<InvalidDataException>(
            () => AcousticsProjectMigrator.MigrateAndValidate(project),
            "Hesaplanmış parametre kaynak/alıcı ID'leri ekogramla eşleşmeli.");
    }

    public static void AcousticMetricsAndSchroederCurveAreValidated()
    {
        var infiniteMetric = BuildAcousticParameters(1, 2, bands =>
        {
            bands[0] = bands[0] with { RT60 = double.PositiveInfinity };
        });
        AssertThrows<InvalidDataException>(
            () => AcousticsProjectMigrator.MigrateAndValidate(
                BuildValidProject() with { LastResult = BuildResult(infiniteMetric) }),
            "Sonsuz akustik metrik reddedilmeli.");

        var unorderedCurve = BuildAcousticParameters(1, 2, bands =>
        {
            bands[0] = bands[0] with
            {
                SchroederCurve = [new(0.2, -2), new(0.1, -3)]
            };
        });
        AssertThrows<InvalidDataException>(
            () => AcousticsProjectMigrator.MigrateAndValidate(
                BuildValidProject() with { LastResult = BuildResult(unorderedCurve) }),
            "Zaman sırası bozuk Schroeder eğrisi reddedilmeli.");

        var increasingCurve = BuildAcousticParameters(1, 2, bands =>
        {
            bands[0] = bands[0] with
            {
                SchroederCurve = [new(0.1, -3), new(0.2, -2)]
            };
        });
        AssertThrows<InvalidDataException>(
            () => AcousticsProjectMigrator.MigrateAndValidate(
                BuildValidProject() with { LastResult = BuildResult(increasingCurve) }),
            "Yükselen Schroeder enerji eğrisi reddedilmeli.");
    }

    public static void ProjectFileServiceRoundTripAppliesMigrator()
    {
        var project = BuildValidProject();
        var path = Path.Combine(Path.GetTempPath(), $"e4p01-migrator-test-{Guid.NewGuid():N}.e4p01");
        try
        {
            var service = new ProjectFileService();
            service.Save(path, project);
            var loaded = service.Load(path);

            AssertTrue(loaded.Surfaces.Count == project.Surfaces.Count,
                "Geçerli round-trip sonrası yüzey sayısı korunmalı.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static AcousticsProject BuildValidProject() => new(
        "MigratorTest", null, DateTime.UtcNow,
        SurfaceImportFormat.Dxf, SurfaceImportUnit.Meters,
        BoxSurfaces(),
        RoomVolume: 48.0);

    private static AcousticsProject BuildMillimeterProject()
    {
        var surfaces = BoxSurfaces()
            .Select(surface => surface with
            {
                Vertices = surface.Vertices
                    .Select(point => new Point3D(point.X * 1_000, point.Y * 1_000, point.Z * 1_000))
                    .ToArray()
            })
            .ToArray();
        return BuildValidProject() with
        {
            Unit = SurfaceImportUnit.Millimeters,
            Surfaces = surfaces
        };
    }

    private static AcousticPanelKonfigurasyon BuildValidPanelConfiguration() => new()
    {
        PanelEniM = 0.8,
        PanelEniModu = PanelEniModu.TekTip,
        SarkmaCikmaM = 0.2,
        AktifTipler = [PanelTipi.Tavan],
        Paneller =
        [
            new AcousticPanel
            {
                Tip = PanelTipi.Tavan,
                DilimNo = 1,
                PanelEniM = 0.8,
                SarkmaCikmaM = 0.2,
                Vertices = [new(0, 0, 2), new(1, 0, 2), new(1, 1, 2), new(0, 1, 2)]
            }
        ]
    };

    private static void AssertInvalidPanel(
        Action<AcousticPanelKonfigurasyon> mutate,
        string message)
    {
        var config = BuildValidPanelConfiguration();
        mutate(config);
        AssertThrows<InvalidDataException>(
            () => AcousticsProjectMigrator.MigrateAndValidate(
                BuildValidProject() with { PanelKonfigurasyon = config }),
            message);
    }

    private static SimulationResult BuildResult(AcousticParameters parameters) => new(
        [new EchogramData(1, 2, "S", "R")],
        new SimulationParameters(),
        TimeSpan.Zero)
    {
        ComputedParameters = [parameters]
    };

    private static AcousticParameters BuildAcousticParameters(
        int sourceId,
        int receiverId,
        Action<BandParameters[]>? mutate = null)
    {
        int[] frequencies = [63, 125, 250, 500, 1000, 2000, 4000, 8000];
        var bands = frequencies.Select(frequency => new BandParameters(
            frequency,
            double.NaN, double.NaN, double.NaN, double.NaN,
            double.NaN, double.NaN, double.NaN, double.NaN,
            double.NaN, double.NaN, double.NaN, double.NaN, double.NaN,
            [])).ToArray();
        mutate?.Invoke(bands);
        return new AcousticParameters(sourceId, receiverId, "S", "R", bands);
    }

    private static List<ProjectSurface> BoxSurfaces()
    {
        const double lx = 4, ly = 4, lz = 3;
        return
        [
            new(1, "Zemin",   "ZEMIN", [new(0,0,0),  new(lx,0,0),  new(lx,ly,0),  new(0,ly,0)]),
            new(2, "Tavan",   "TAVAN", [new(0,0,lz), new(lx,0,lz), new(lx,ly,lz), new(0,ly,lz)]),
            new(3, "DuvarY0", "DUVAR", [new(0,0,0),  new(lx,0,0),  new(lx,0,lz),  new(0,0,lz)]),
            new(4, "DuvarY1", "DUVAR", [new(0,ly,0), new(lx,ly,0), new(lx,ly,lz), new(0,ly,lz)]),
            new(5, "DuvarX0", "DUVAR", [new(0,0,0),  new(0,ly,0),  new(0,ly,lz),  new(0,0,lz)]),
            new(6, "DuvarX1", "DUVAR", [new(lx,0,0), new(lx,ly,0), new(lx,ly,lz), new(lx,0,lz)])
        ];
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void AssertClose(double expected, double actual, double tolerance, string message)
    {
        if (Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException(
                $"{message} Beklenen: {expected:R}, gerçek: {actual:R}.");
    }

    private static void AssertThrows<TException>(Action action, string message) where TException : Exception
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

    public sealed class M01ValidatorXunitTests
    {
        [Xunit.Fact]
        public void RejectsNonPositiveSchemaVersions() => NonPositiveSchemaVersionIsRejected();

        [Xunit.Fact]
        public void RejectsZeroAreaGeometry() => CollinearSurfaceIsRejectedButThinValidSurfacePasses();

        [Xunit.Fact]
        public void RejectsMalformedPanels() => MalformedPanelDefinitionsAreRejected();

        [Xunit.Fact]
        public void EnforcesSimulationUiRangesAndUnitScale() =>
            SimulationSettingsUseUiRangesAndPhysicalReceiverRadius();

        [Xunit.Fact]
        public void MigratesCurrentSettingsButPreservesHistoricalResultUnits() =>
            V1UnitScaleMigratesWithoutRewritingHistoricalResult();

        [Xunit.Fact]
        public void MatchesComputedAndEchogramPairIds() => ComputedPairIdsMustMatchEchogram();

        [Xunit.Fact]
        public void RejectsInvalidAcousticMetricsAndCurves() =>
            AcousticMetricsAndSchroederCurveAreValidated();
    }
}
