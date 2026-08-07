using System.Globalization;
using System.Text;
using Kst.Acoustics.App.ViewModels;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Importing;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Salon.Models;
using Kst.Acoustics.Core.Salon.Services;

namespace Kst.Acoustics.Tests;

public static class M01GeometryReliabilityTests
{
    public static void UnknownDxfUnitDefersCoplanarMergeUntilFinalized()
    {
        var parser = new DxfSurfaceParser();
        var dxf = BuildDxf(
            header: string.Empty,
            entities: Face(0, 0, 0, 1, 1, "ZEMIN")
                      + Face(1.0003, 0, 0, 2, 1, "ZEMIN"));

        if (!parser.TryParse(dxf, out var document) || document is null)
            throw new InvalidOperationException("Birimsiz DXF ayrıştırılamadı.");
        AssertEqual(2, document.Surfaces.Count,
            "Birim bilinmeden yüzeyler birleştirilmemeli");

        var finalized = DxfSurfaceParser.FinalizeUnit(document, SurfaceImportUnit.Meters);
        AssertEqual(1, finalized.Surfaces.Count,
            "Metre birimi kesinleşince 0,3 mm köşe farkı birleştirilmeli");
    }

    public static void ParserReadsOnlyEntitiesAndWarnsForInsert()
    {
        var dxf = """
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
BLOCKS
0
BLOCK
2
HAYALET
0
3DFACE
8
HAYALET
10
100
20
100
30
0
11
110
21
100
31
0
12
110
22
110
32
0
13
100
23
110
33
0
0
ENDBLK
0
ENDSEC
0
SECTION
2
ENTITIES
""" + "\n" + Face(0, 0, 0, 1, 1, "GERCEK") + """
0
INSERT
2
HAYALET
10
25
20
25
30
0
0
ENDSEC
0
EOF
""";

        var result = new SurfaceImportService().Parse(dxf);
        AssertEqual(1, result.Document.Surfaces.Count,
            "BLOCKS içindeki tanım model yüzeyi sayılmamalı");
        if (result.Document.Surfaces[0].Category != "GERCEK")
            throw new InvalidOperationException("Yalnız ENTITIES yüzeyi içe aktarılmalı.");
        if (result.Document.Warnings?.Any(w => w.Contains("INSERT", StringComparison.OrdinalIgnoreCase)) != true)
            throw new InvalidOperationException("Desteklenmeyen INSERT için kalıcı uyarı üretilmeli.");
    }

    public static void HoleRegionNeverProducesNaNVertices()
    {
        var entities = new StringBuilder()
            .Append(LwPolyline([(0,0),(4,0),(4,1),(3,1),(1,1),(0,1)], "ZEMIN"))
            .Append(LwPolyline([(3,1),(4,1),(4,4),(3,4),(3,3)], "ZEMIN"))
            .Append(LwPolyline([(0,3),(1,3),(3,3),(3,4),(0,4)], "ZEMIN"))
            .Append(LwPolyline([(0,1),(1,1),(1,3),(0,3)], "ZEMIN"))
            .ToString();
        var result = new SurfaceImportService().Parse(BuildDxf(UnitsHeader(6), entities));

        if (result.Document.Surfaces.SelectMany(s => s.Vertices).Any(p => !IsFinite(p)))
            throw new InvalidOperationException("Delikli bölge kalıcı modele NaN sentinel taşımamalı.");
        AssertEqual(4, result.Document.Surfaces.Count,
            "Delikli birleşimde kaynak basit yüzeyler korunmalı");
    }

    public static void PlaneToleranceKeepsSeparatedParallelFacesApart()
    {
        var dxf = BuildDxf(
            UnitsHeader(6),
            Face(0, 0, 0, 1, 1, "PANEL") + Face(0, 0, 0.02, 1, 1, "PANEL"));
        var result = new SurfaceImportService().Parse(dxf);
        AssertEqual(2, result.Document.Surfaces.Count,
            "20 mm ayrık paralel yüzeyler aynı düzlem sayılmamalı");
    }

    public static void LegacyHoleSentinelDoesNotPoisonTransformOrArea()
    {
        var nan = new Point3D(double.NaN, double.NaN, double.NaN);
        Point3D[] vertices =
        [
            new(0,0,0), new(4,0,0), new(4,4,0), new(0,4,0),
            nan,
            new(1,1,0), new(1,3,0), new(3,3,0), new(3,1,0)
        ];

        var centroid = SurfaceTransformService.ComputeCentroid(vertices);
        if (!IsFinite(centroid)) throw new InvalidOperationException("Centroid sonlu olmalı.");
        AssertClose(12, SurfaceTransformService.ComputeArea(vertices), 1e-9,
            "Delikli yüzey net alanı");

        var rotated = SurfaceTransformService.RotateAroundCentroid(
            new ProjectSurface(1, "Legacy", "ZEMIN", vertices), RotationAxis.Z, 30);
        if (rotated.Vertices.Count(IsFinite) != 8)
            throw new InvalidOperationException("Dönüşüm sonlu köşeleri NaN yapmamalı.");
    }

    public static void MainWindowSnapUsesPhysicalOffsetAndChoosesRoomInterior()
    {
        foreach (var (unit, factor) in new[]
                 {
                     (SurfaceImportUnit.Meters, 1.0),
                     (SurfaceImportUnit.Millimeters, 1000.0)
                 })
        {
            var project = new AcousticsProject(
                "Snap", null, DateTime.UtcNow, SurfaceImportFormat.Dxf, unit,
                BoxSurfaces(4 * factor, 4 * factor, 3 * factor));
            var vm = new MainWindowViewModel();
            vm.UpdateProject(project);
            vm.SelectedSurface = vm.SurfacePreviews.First(surface => surface.Name == "Tavan");
            vm.SnapSourceToSurface();

            if (vm.SourcePoints.Count != 1)
                throw new InvalidOperationException("Snap bir kaynak üretmeli.");
            AssertClose(2.9 * factor, vm.SourcePoints[0].Position.Z, 1e-6 * factor,
                $"{unit} tavan snap iç kotu");
        }
    }

    public static void SalonLoadNormalizesNullCollectionsAndRejectsFutureSchema()
    {
        var path = Path.Combine(Path.GetTempPath(), $"m01-salon-{Guid.NewGuid():N}.e4salon.json");
        try
        {
            File.WriteAllText(path,
                "{\"SemaSurum\":1,\"SalonAdi\":null,\"Katmanlar\":null,\"CizimOgeleri\":null," +
                "\"SeyirciSekciyonlari\":null,\"Balkonlar\":null,\"Acikliklar\":null,\"TavanProfili\":null}");
            var salon = SalonGeometriServisi.Ac(path);
            if (salon.Katmanlar is null || salon.CizimOgeleri is null
                || salon.SeyirciSekciyonlari is null || salon.Balkonlar is null
                || salon.Acikliklar is null || salon.TavanProfili is null)
            {
                throw new InvalidOperationException("Eksik/null koleksiyonlar boş koleksiyona normalize edilmeli.");
            }

            File.WriteAllText(path, "{\"SemaSurum\":999}");
            AssertThrows<InvalidDataException>(() => SalonGeometriServisi.Ac(path),
                "İleri salon şeması reddedilmeli");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public static void ElevatedAudienceDoesNotConsumeAirBelowBaseElevation()
    {
        var salon = SalonGeometriServisi.YeniOlustur("Yükseltilmiş Seyirci");
        salon.SalonYuksekligiM = 10;
        var floor = salon.Katmanlar.First(k => k.Tip == SalonKatmanTipi.Zemin);
        salon.CizimOgeleri.Add(new SalonCizimOgesi
        {
            Tip = SalonCizimTipi.Dikdortgen,
            Kapali = true,
            KatmanId = floor.Id,
            Noktalar = [new(0,0), new(10,0), new(10,10), new(0,10)]
        });
        salon.SeyirciSekciyonlari.Add(new SeyirciSekciyonu
        {
            GenislikM = 4,
            DerinlikM = 4,
            KademeSayisi = 2,
            KademeYuksekligiM = 1,
            TabanKotuM = 5,
            MerkezX = 5,
            MerkezY = 5
        });

        // Temel hacim 1000; iki eşit basamaktan yalnız ikincinin 1 m katısı düşer:
        // 8 m² × 1 m = 8 m³. 0..5 m arasındaki hava korunur.
        AssertClose(992, SalonHacimServisi.Hesapla(salon), 1e-6,
            "Yükseltilmiş seyirci hacmi");
    }

    private static string BuildDxf(string header, string entities)
    {
        var sb = new StringBuilder()
            .AppendLine("0").AppendLine("SECTION")
            .AppendLine("2").AppendLine("HEADER");
        if (!string.IsNullOrWhiteSpace(header))
            sb.AppendLine(header.TrimEnd('\r', '\n'));
        sb.AppendLine("0").AppendLine("ENDSEC")
            .AppendLine("0").AppendLine("SECTION")
            .AppendLine("2").AppendLine("ENTITIES");
        if (!string.IsNullOrWhiteSpace(entities))
            sb.AppendLine(entities.TrimEnd('\r', '\n'));
        sb.AppendLine("0").AppendLine("ENDSEC")
            .AppendLine("0").AppendLine("EOF");
        return sb.ToString();
    }

    private static string UnitsHeader(int code) => $"""
9
$INSUNITS
70
{code}
""";

    private static string Face(double x0, double y0, double z, double x1, double y1, string layer)
    {
        var f = CultureInfo.InvariantCulture;
        string N(double value) => value.ToString("R", f);
        return $"""
0
3DFACE
8
{layer}
10
{N(x0)}
20
{N(y0)}
30
{N(z)}
11
{N(x1)}
21
{N(y0)}
31
{N(z)}
12
{N(x1)}
22
{N(y1)}
32
{N(z)}
13
{N(x0)}
23
{N(y1)}
33
{N(z)}
""" + "\n";
    }

    private static string LwPolyline((double X, double Y)[] points, string layer)
    {
        var sb = new StringBuilder()
            .AppendLine("0").AppendLine("LWPOLYLINE")
            .AppendLine("8").AppendLine(layer)
            .AppendLine("70").AppendLine("1");
        foreach (var point in points)
        {
            sb.AppendLine("10").AppendLine(point.X.ToString("R", CultureInfo.InvariantCulture));
            sb.AppendLine("20").AppendLine(point.Y.ToString("R", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    private static IReadOnlyList<ProjectSurface> BoxSurfaces(double x, double y, double z) =>
    [
        new(1, "Zemin", "ZEMIN", [new(0,0,0), new(0,y,0), new(x,y,0), new(x,0,0)]),
        new(2, "Tavan", "TAVAN", [new(0,0,z), new(x,0,z), new(x,y,z), new(0,y,z)]),
        new(3, "Y0", "DUVAR", [new(0,0,0), new(x,0,0), new(x,0,z), new(0,0,z)]),
        new(4, "X1", "DUVAR", [new(x,0,0), new(x,y,0), new(x,y,z), new(x,0,z)]),
        new(5, "Y1", "DUVAR", [new(x,y,0), new(0,y,0), new(0,y,z), new(x,y,z)]),
        new(6, "X0", "DUVAR", [new(0,y,0), new(0,0,0), new(0,0,z), new(0,y,z)])
    ];

    private static bool IsFinite(Point3D p) =>
        double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z);

    private static void AssertEqual(int expected, int actual, string label)
    {
        if (expected != actual)
            throw new InvalidOperationException($"{label}: beklenen {expected}, gerçek {actual}.");
    }

    private static void AssertClose(double expected, double actual, double tolerance, string label)
    {
        if (Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException(
                $"{label}: beklenen {expected:R}, gerçek {actual:R}, tolerans {tolerance:R}.");
    }

    private static void AssertThrows<T>(Action action, string label) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }

        throw new InvalidOperationException($"{label}: {typeof(T).Name} bekleniyordu.");
    }
}
