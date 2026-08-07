using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Importing;
using Kst.Acoustics.Core.Projects;

bool detail = args.Contains("--detail");
bool loadProject = args.Contains("--load-project");
var pathArg = args.FirstOrDefault(a => !a.StartsWith("--"));
if (pathArg is null)
{
    Console.Error.WriteLine("Kullanım: dotnet run --project Kst.Acoustics.ImportPreview -- <dosya> [--detail] [--load-project]");
    return 64;
}

var path = Path.GetFullPath(pathArg);

if (!File.Exists(path))
{
    Console.Error.WriteLine($"File not found: {path}");
    return 1;
}

if (loadProject)
{
    try
    {
        var svc = new ProjectFileService();
        var loadResult = svc.LoadWithStatusAsync(path).GetAwaiter().GetResult();
        var p = loadResult.Project;
        if (loadResult.RecoveredFromBackup)
        {
            Console.Error.WriteLine("UYARI: Ana proje dosyası okunamadı; .bak yedeği kullanıldı.");
            foreach (var warning in loadResult.SessionWarnings)
                Console.Error.WriteLine($"  {warning}");
        }
        Console.WriteLine($"OK: {p.Name}, Surfaces={p.Surfaces.Count}, Unit={p.Unit}");
        Console.WriteLine($"  PanelKonfigurasyon: {(p.PanelKonfigurasyon is null ? "null" : "yüklendi")}");
        if (p.PanelKonfigurasyon is not null)
            Console.WriteLine($"    Paneller={p.PanelKonfigurasyon.Paneller.Count}, AktifTipler=[{string.Join(",", p.PanelKonfigurasyon.AktifTipler)}], TavanYuzeyId={p.PanelKonfigurasyon.TavanYuzeyId}");
        Console.WriteLine($"  Sources={p.Sources.Count} Receivers={p.Receivers.Count} LastResult={(p.LastResult is null ? "null" : "var")}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"HATA ({ex.GetType().Name}): {ex.Message}");
        Console.Error.WriteLine(ex.StackTrace);
        return 99;
    }
}

var importService = new SurfaceImportService();
SurfaceImportResult result;

try
{
    result = importService.ParseFile(path);
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

var document = result.Document;
var categoryCounts = document.Surfaces
    .GroupBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
    .OrderByDescending(x => x.Count())
    .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
    .ToArray();

Console.WriteLine($"Importer: {result.Format}");
Console.WriteLine($"Unit: {document.Unit}");
Console.WriteLine($"Surface count: {document.Surfaces.Count}");
Console.WriteLine($"Category count: {categoryCounts.Length}");
Console.WriteLine();

Console.WriteLine("Top categories:");
foreach (var entry in categoryCounts.Take(10))
{
    Console.WriteLine($"  {entry.Key}: {entry.Count()}");
}

Console.WriteLine();
Console.WriteLine("First surfaces:");
foreach (var surface in document.Surfaces.Take(5))
{
    Console.WriteLine(
        $"  Id={surface.Id}, Name={surface.Name}, Category={surface.Category}, Vertices={surface.Vertices.Count}");
}

if (detail)
{
    var konfig = new Kst.Acoustics.Core.Projects.AcousticPanelKonfigurasyon();
    var projectSurfaces = document.Surfaces
        .Select(s => new Kst.Acoustics.Core.Projects.ProjectSurface(s.Id, s.Name, s.Category, s.Vertices))
        .ToList();
    var hedefler = Kst.Acoustics.Core.Geometry.PanelHedefDetector.Bul(projectSurfaces, konfig);
    Console.WriteLine();
    Console.WriteLine("=== Otomatik panel hedef tespiti ===");
    Console.WriteLine($"  Tavan    : {hedefler.Tavan?.Surface.Name ?? "—"}  (cat={hedefler.Tavan?.Surface.Category})");
    Console.WriteLine($"  Sol duvar: {hedefler.SolDuvar?.Surface.Name ?? "—"}  (cat={hedefler.SolDuvar?.Surface.Category})");
    Console.WriteLine($"  Sağ duvar: {hedefler.SagDuvar?.Surface.Name ?? "—"}  (cat={hedefler.SagDuvar?.Surface.Category})");

    // Varsayılan ayarlarla panel üretim simülasyonu (kilitlenme regresyon kontrolü)
    var demoKonfig = new Kst.Acoustics.Core.Projects.AcousticPanelKonfigurasyon
    {
        PanelEniM = 0.6,
        SarkmaCikmaM = 0.5,
        AktifTipler = new[]
        {
            Kst.Acoustics.Core.Projects.PanelTipi.Tavan,
            Kst.Acoustics.Core.Projects.PanelTipi.DuvarSol,
            Kst.Acoustics.Core.Projects.PanelTipi.DuvarSag
        }
    };
    var demoBoyut = Kst.Acoustics.Core.Geometry.PanelYerlesimServisi.BoyutlariHesapla(projectSurfaces, null, document.Unit);
    var demoPaneller = Kst.Acoustics.Core.Geometry.PanelYerlesimServisi.PanelleriUret(demoKonfig, projectSurfaces, demoBoyut);
    Console.WriteLine($"  Varsayılan üretim (0.6 m eni, 0.5 m sarkma, 3 tip): {demoPaneller.Count} panel  (birim={document.Unit}, metreCarpan={demoBoyut.MetreCarpan})");

    Console.WriteLine();
    Console.WriteLine("=== Detail: per-surface centroid + normal + area ===");
    var allPts = document.Surfaces.SelectMany(s => s.Vertices).ToList();
    if (allPts.Count > 0)
    {
        var minX = allPts.Min(p => p.X); var maxX = allPts.Max(p => p.X);
        var minY = allPts.Min(p => p.Y); var maxY = allPts.Max(p => p.Y);
        var minZ = allPts.Min(p => p.Z); var maxZ = allPts.Max(p => p.Z);
        Console.WriteLine($"Bounds: X=[{minX:F2},{maxX:F2}] Y=[{minY:F2},{maxY:F2}] Z=[{minZ:F2},{maxZ:F2}]");
        Console.WriteLine($"Size:   W={maxX - minX:F2}  D={maxY - minY:F2}  H={maxZ - minZ:F2}");
    }
    Console.WriteLine();
    Console.WriteLine($"{"Id",4} {"Cat",-18} {"Verts",5} {"Area",10}  {"Centroid",-30}  {"Normal",-30}");
    foreach (var s in document.Surfaces.OrderBy(s => s.Category).ThenBy(s => s.Id))
    {
        var verts = s.Vertices;
        if (verts.Count < 3) { Console.WriteLine($"{s.Id,4} {s.Category,-18} {verts.Count,5}  (degenerate)"); continue; }
        double cx = verts.Average(p => p.X), cy = verts.Average(p => p.Y), cz = verts.Average(p => p.Z);
        double nx = 0, ny = 0, nz = 0;
        for (int i = 1; i < verts.Count - 1; i++)
        {
            var ax = verts[i].X - verts[0].X; var ay = verts[i].Y - verts[0].Y; var az = verts[i].Z - verts[0].Z;
            var bx = verts[i + 1].X - verts[0].X; var by = verts[i + 1].Y - verts[0].Y; var bz = verts[i + 1].Z - verts[0].Z;
            nx += ay * bz - az * by;
            ny += az * bx - ax * bz;
            nz += ax * by - ay * bx;
        }
        var len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
        double area = len * 0.5;
        if (len > 1e-12) { nx /= len; ny /= len; nz /= len; }
        Console.WriteLine($"{s.Id,4} {s.Category,-18} {verts.Count,5} {area,10:F2}  ({cx,8:F1},{cy,8:F1},{cz,8:F1})  ({nx,7:F3},{ny,7:F3},{nz,7:F3})");
    }
}

return 0;
