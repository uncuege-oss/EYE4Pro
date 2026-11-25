using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Terrain.Models;
using Kst.Acoustics.Core.Terrain.Services;

var tests = new (string, Action)[]
{
    ("Triangle hull excludes bounding-box corner", TriangleHull),
    ("Mean elevation is area weighted, independent of point density", WeightedMean),
    ("Translated map coordinates preserve elevations", TranslatedCoordinates),
    ("Identical and near points collapse", Deduplicate),
    ("Conflicting elevations fail explicitly", Conflict),
    ("Nonfinite data and settings are rejected", InvalidInput),
    ("Collinear points return empty TIN", Collinear),
    ("Contour densification preserves levels", Contours),
    ("Excessive contour densification is bounded", DensificationBudget),
    ("Custom triangular platform computes known volume", CustomTriangle),
    ("Self crossing and repeated corners are rejected", InvalidPolygon),
    ("Outside-hull corners are rejected", OutsidePolygon),
    ("Concave simple platforms remain supported", ConcavePolygon),
    ("Balanced platform has zero net geometric volume", BalancedVolume),
    ("Spatial index agrees with linear barycentric interpolation", IndexedQueries),
    ("Spatial index preserves edge tolerance and first triangle", IndexedBoundaries),
    ("Spatial index is an immutable geometry snapshot", IndexedSnapshot),
    ("Large spatial index computes analytic plane quickly", LargeIndex),
    ("Spatial index rejects invalid geometry", InvalidIndex)
};
foreach (var (name, run) in tests) { run(); Console.WriteLine($"PASS {name}"); }
Console.WriteLine($"{tests.Length}/{tests.Length} passed.");

static void Near(double a, double b, double eps = 1e-8)
{
    if (!double.IsFinite(a) || Math.Abs(a - b) > eps) throw new Exception($"Expected {b}, got {a}.");
}
static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
static void Reject(Action action)
{
    try { action(); } catch (ArgumentException) { return; }
    throw new Exception("Expected an explicit input error.");
}
static AraziYuzeyi Plane() => new AraziYuzeyServisi().Ucgenle([new(0, 0, 10), new(10, 0, 10), new(10, 10, 10), new(0, 10, 10)]);
static void TriangleHull()
{
    var y = new AraziYuzeyServisi().Ucgenle([new(0, 0, 5), new(10, 0, 15), new(0, 10, 25), new(1, 1, 8)]);
    var s = AraziTasarimServisi.Ozetle(y);
    Near(s.PlanAlanM2, 50); Near(s.OrtalamaKotM, 15); Near(s.MinKotM, 5); Near(s.MaxKotM, 25);
    Assert(s.SinirPoligonu.Count == 3, "Triangle hull must have three corners.");
    var t = AraziTasarimServisi.PlatformOlustur(y, 15);
    Assert(t.HedefKot(9, 9) is null, "Rectangle corner must remain outside design.");
    Near(t.HedefKot(2, 2)!.Value, 15);
}
static void WeightedMean()
{
    // z=x over a 10x10 square: integral mean is 5, while most samples are near x=0.
    var p = new List<AraziDugum> { new(0, 0, 0), new(10, 0, 10), new(10, 10, 10), new(0, 10, 0) };
    for (int i = 1; i < 10; i++) p.Add(new(0.1, i, 0.1));
    var s = AraziTasarimServisi.Ozetle(new AraziYuzeyServisi().Ucgenle(p));
    Near(s.PlanAlanM2, 100); Near(s.OrtalamaKotM, 5);
}
static void TranslatedCoordinates()
{
    const double x = 477123.123, y = 4521456.789;
    var tin = new AraziYuzeyServisi().Ucgenle([new(x, y, 100), new(x + 10, y, 110), new(x + 10, y + 10, 130), new(x, y + 10, 120)]);
    Near(tin.KotSorgula(x + 3, y + 4)!.Value, 111, 1e-7);
    var s = AraziTasarimServisi.Ozetle(tin); Near(s.OrtalamaKotM, 115); Near(s.PlanAlanM2, 100);
}
static void Deduplicate()
{
    var y = new AraziYuzeyServisi().Ucgenle([new(0, 0, 10), new(10, 0, 10), new(0, 10, 10), new(0, 0, 10), new(0.0000002, 0.0000002, 10)]);
    Assert(y.Dugumler.Count == 3 && y.Ucgenler.Count == 1, "Duplicates must not create tiny triangles.");
    Near(y.KotSorgula(1, 1)!.Value, 10);
}
static void Conflict()
{
    Reject(() => new AraziYuzeyServisi().Ucgenle([new(0, 0, 10), new(10, 0, 10), new(0, 10, 10), new(0, 0, 11)]));
}
static void InvalidInput()
{
    Reject(() => new AraziYuzeyServisi().Ucgenle([new(0, 0, double.NaN)]));
    Reject(() => new AraziYuzeyServisi { KonturOrneklemeAraligiM = double.PositiveInfinity }.Ucgenle([]));
    Reject(() => new AraziYuzeyServisi { TekillestirmeToleransiM = 0 }.Ucgenle([]));
    Reject(() => AraziTasarimServisi.PlatformOlustur(Plane(), double.NaN));
    Reject(() => AraziTasarimServisi.Ozetle(new AraziYuzeyi()));
}
static void Collinear()
{
    Assert(new AraziYuzeyServisi().Ucgenle([new(0, 0, 10), new(1, 1, 11), new(2, 2, 12)]).Ucgenler.Count == 0, "Collinear data should have no surface.");
}
static void Contours()
{
    var m = new AraziModeli { Konturlar = [
        new() { KotM = 100, KotKaynagi = AraziKotKaynagi.DuzlemKotu, Noktalar = [new(0, 0), new(10, 0)] },
        new() { KotM = 110, KotKaynagi = AraziKotKaynagi.DuzlemKotu, Noktalar = [new(0, 10), new(10, 10)] }] };
    var y = new AraziYuzeyServisi { KonturOrneklemeAraligiM = 2 }.Olustur(m);
    Near(y.KotSorgula(4, 6)!.Value, 106); Near(AraziTasarimServisi.Ozetle(y).OrtalamaKotM, 105);
}
static void DensificationBudget()
{
    var m = new AraziModeli { Konturlar = [new() { KotM = 100, KotKaynagi = AraziKotKaynagi.DuzlemKotu, Noktalar = [new(0, 0), new(1000, 0)] }] };
    Reject(() => new AraziYuzeyServisi { KonturOrneklemeAraligiM = 0.0001, AzamiNoktaSayisi = 100 }.Olustur(m));
}
static void CustomTriangle()
{
    var y = Plane();
    var t = AraziTasarimServisi.PlatformOlustur(y, 8, [new(1, 1), new(9, 1), new(1, 9)], "A");
    Assert(t.Bolgeler[0].Poligon.Count == 3 && t.Bolgeler[0].Ad == "A", "Custom boundary or name lost.");
    var r = new HafriyatHesabiServisi { GridAraligiM = .05 }.Hesapla(y, t);
    Near(r.YarmaHacmiM3, 64, .5); Near(r.DolguHacmiM3, 0); Near(r.KapsamDisiHucreSayisi, 0);
}
static void InvalidPolygon()
{
    var y = Plane();
    Reject(() => AraziTasarimServisi.PlatformOlustur(y, 8, [new(1, 1), new(9, 9), new(1, 9), new(9, 1)]));
    Reject(() => AraziTasarimServisi.PlatformOlustur(y, 8, [new(1, 1), new(9, 1), new(9, 1), new(1, 9)]));
    Reject(() => AraziTasarimServisi.PlatformOlustur(y, 8, [new(1, 1), new(5, 1), new(9, 1)]));
    Reject(() => AraziTasarimServisi.PlatformOlustur(y, 8, [new(1, 1), new(9, 1), new(5, 1), new(5, 9), new(1, 9)]));
    Reject(() => AraziTasarimServisi.PlatformOlustur(y, 8, [new(double.NaN, 1), new(9, 1), new(1, 9)]));
}
static void OutsidePolygon()
{
    var y = new AraziYuzeyServisi().Ucgenle([new(0, 0, 10), new(10, 0, 10), new(0, 10, 10)]);
    Reject(() => AraziTasarimServisi.PlatformOlustur(y, 8, [new(1, 1), new(9, 1), new(9, 9)]));
}
static void ConcavePolygon()
{
    var t = AraziTasarimServisi.PlatformOlustur(Plane(), 8, [new(1, 1), new(9, 1), new(9, 4), new(4, 4), new(4, 9), new(1, 9)]);
    Near(t.HedefKot(2, 8)!.Value, 8); Assert(t.HedefKot(8, 8) is null, "Concave notch must be outside.");
}
static void BalancedVolume()
{
    var y = new AraziYuzeyServisi().Ucgenle([new(0, 0, 0), new(10, 0, 10), new(10, 10, 10), new(0, 10, 0)]);
    var t = AraziTasarimServisi.PlatformOlustur(y, AraziTasarimServisi.Ozetle(y).OrtalamaKotM);
    var r = new HafriyatHesabiServisi { GridAraligiM = 0.5 }.Hesapla(y, t);
    Near(r.NetHacimM3, 0); Near(r.YarmaHacmiM3, 125); Near(r.DolguHacmiM3, 125);
}

static void IndexedQueries()
{
    var rnd = new Random(761);
    var pts = new List<AraziDugum>();
    for (int i = 0; i < 120; i++)
    {
        double x = rnd.NextDouble() * 100, y = rnd.NextDouble() * 100;
        pts.Add(new(x + 476123.45, y + 4512345.67, Math.Sin(x / 10) * 5 + y / 10));
    }
    var tin = new AraziYuzeyServisi().Ucgenle(pts);
    var index = new AraziKotIndeksi(tin);
    for (int i = 0; i < 2000; i++)
    {
        double x = 476113.45 + rnd.NextDouble() * 120, y = 4512335.67 + rnd.NextDouble() * 120;
        double? expected = tin.KotSorgula(x, y), actual = index.KotSorgula(x, y);
        Assert(expected.HasValue == actual.HasValue, "Index coverage differs from linear query.");
        if (expected.HasValue) Near(actual!.Value, expected.Value, 1e-10);
    }
}
static void IndexedBoundaries()
{
    // Overlapping data must retain original triangle priority, even after BVH sorting.
    var tin = new AraziYuzeyi();
    for (int i = 0; i < 30; i++)
    {
        int n = tin.Dugumler.Count;
        tin.Dugumler.AddRange([new(0, 0, i), new(10, 0, i), new(0, 10, i)]);
        tin.Ucgenler.Add(new(n, n + 1, n + 2));
    }
    var index = new AraziKotIndeksi(tin);
    foreach (var p in new Point2D[] { new(0, 0), new(5, 5), new(10, 0), new(-5e-9, 0), new(5, 5 + 5e-9), new(5, 5 + 1e-6) })
    {
        var expected = tin.KotSorgula(p.X, p.Y); var actual = index.KotSorgula(p.X, p.Y);
        Assert(expected.HasValue == actual.HasValue, "Index changed epsilon boundary coverage.");
        if (expected.HasValue) Near(actual!.Value, expected.Value);
    }
    Assert(index.KotSorgula(double.NaN, 0) is null && index.KotSorgula(0, double.PositiveInfinity) is null, "Nonfinite query must be outside.");
}
static void IndexedSnapshot()
{
    var tin = Plane(); var index = new AraziKotIndeksi(tin);
    tin.Dugumler.Clear(); tin.Ucgenler.Clear();
    Near(index.KotSorgula(5, 5)!.Value, 10);
    Parallel.For(0, 200, i => Near(index.KotSorgula((i % 9) + .25, (i % 7) + .25)!.Value, 10));
}
static void LargeIndex()
{
    const int size = 225;
    var tin = new AraziYuzeyi();
    for (int y = 0; y <= size; y++)
    for (int x = 0; x <= size; x++) tin.Dugumler.Add(new(500000 + x, 4500000 + y, 100 + x * .1 + y * .2));
    for (int y = 0; y < size; y++)
    for (int x = 0; x < size; x++)
    {
        int a = y * (size + 1) + x, b = a + 1, c = a + size + 1, d = c + 1;
        tin.Ucgenler.Add(new(a, b, d)); tin.Ucgenler.Add(new(a, d, c));
    }
    var watch = System.Diagnostics.Stopwatch.StartNew();
    var index = new AraziKotIndeksi(tin);
    for (int i = 0; i < 15000; i++)
    {
        double x = (i * 17 % size) + .2, y = (i * 31 % size) + .4;
        Near(index.KotSorgula(500000 + x, 4500000 + y)!.Value, 100 + x * .1 + y * .2, 1e-8);
    }
    Console.WriteLine($"INFO {tin.Ucgenler.Count:N0} triangles, build + 15000 queries: {watch.ElapsedMilliseconds} ms");
}
static void InvalidIndex()
{
    Assert(new AraziKotIndeksi(new AraziYuzeyi()).KotSorgula(0, 0) is null, "Empty index must be outside.");
    Reject(() => new AraziKotIndeksi(new AraziYuzeyi { Ucgenler = [new(0, 1, 2)] }));
    Reject(() => new AraziKotIndeksi(new AraziYuzeyi { Dugumler = [new(0, 0, double.NaN), new(1, 0, 0), new(0, 1, 0)], Ucgenler = [new(0, 1, 2)] }));
}
