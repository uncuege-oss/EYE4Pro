using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Bina.Services;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Projects;

var cases = new (string Name, Action Run)[]
{
    ("Kapı ve pencere alanı duvardan çıkarılır", DoorAndWindow),
    ("Döndürülmüş duvar ve kat ofsetleri", RotatedWall),
    ("Yinelenen ve çakışan açıklıkların alanı tek sayılır", OverlappingOpenings),
    ("İptal, geçersiz ve duvar dışı açıklıklar duvarı kesmez", InvalidOpenings),
    ("Kat sırası, görünürlük ve eşik kotları korunur", FloorOrder),
    ("Bölünmüş kenar üzerindeki açıklık ve tavan sınırı", SplitWall),
    ("Açık sıfır eşik ile eski varsayılan kayıt ayrılır", ExplicitZeroSill),
    ("Mutlak kot, negatif bodrum ve karma istifleme", ExplicitFloorElevation),
    ("Kot ve açık sıfır eşik kayıt sonrası korunur", MetadataRoundTrip)
};
foreach (var (name, run) in cases)
{
    run();
    Console.WriteLine($"PASS {name}");
}
Console.WriteLine($"{cases.Length}/{cases.Length} passed.");

static (BinaGeometri Geo, BinaKat Kat, BinaOda Oda) Room()
{
    var geo = new BinaGeometri();
    var kat = new BinaKat { KatYuksekligiM = 3 };
    var oda = new BinaOda { Ad = "Test", Poligon = [new(0, 0), new(5, 0), new(5, 4), new(0, 4)] };
    kat.Odalar.Add(oda);
    geo.Katlar.Add(kat);
    return (geo, kat, oda);
}

static BinaAciklik Opening(double start, double end, BinaAciklikTipi type = BinaAciklikTipi.Kapi) =>
    new() { Bas = new(start, 0), Bit = new(end, 0), Tip = type };

static void Near(double actual, double expected, string message)
{
    if (Math.Abs(actual - expected) > 1e-6 || !double.IsFinite(actual))
        throw new Exception($"{message}: expected {expected}, got {actual}.");
}

static double Area(ProjectSurface surface)
{
    var a = surface.Vertices[0]; var b = surface.Vertices[1]; var d = surface.Vertices[3];
    double ux = b.X - a.X, uy = b.Y - a.Y, uz = b.Z - a.Z;
    double vx = d.X - a.X, vy = d.Y - a.Y, vz = d.Z - a.Z;
    return Math.Sqrt(Math.Pow(uy * vz - uz * vy, 2) + Math.Pow(uz * vx - ux * vz, 2) + Math.Pow(ux * vy - uy * vx, 2));
}

static void DoorAndWindow()
{
    var (geo, kat, oda) = Room();
    kat.Acikliklar.Add(Opening(0.5, 1.5));
    var window = Opening(2.5, 4, BinaAciklikTipi.Pencere);
    window.EsikYuksekligiM = 1.1; window.YukseklikM = 1.2;
    kat.Acikliklar.Add(window);
    var surfaces = Bina3DGorunumServisi.YuzeyleriOlustur(geo);
    Near(surfaces.Where(s => s.Category == "Duvar").Sum(Area), 54 - 2.1 - 1.8, "Wall area");
    Near(surfaces.Where(s => s.Category == "Kapi").Sum(Area), 2.1, "Door area");
    Near(surfaces.Where(s => s.Category == "Pencere").Sum(Area), 1.8, "Window area");
    Near(surfaces.Where(s => s.Category == "Pencere").SelectMany(s => s.Vertices).Min(p => p.Z), 1.1, "Window sill");
    Near(surfaces.Where(s => s.Category == "Pencere").SelectMany(s => s.Vertices).Max(p => p.Z), 2.3, "Window head");
    if (surfaces.Select(s => s.Id).Distinct().Count() != surfaces.Count || surfaces.Any(s => s.OwnerId != oda.Id))
        throw new Exception("Surface IDs or room ownership lost.");
}

static void RotatedWall()
{
    var (geo, kat, oda) = Room();
    double angle = 0.72;
    Point2D Rotate(Point2D p) => new(p.X * Math.Cos(angle) - p.Y * Math.Sin(angle), p.X * Math.Sin(angle) + p.Y * Math.Cos(angle));
    oda.Poligon = oda.Poligon.Select(Rotate).ToList();
    kat.OriginOffset = new(13, -7);
    kat.Acikliklar.Add(new BinaAciklik { Bas = Rotate(new(1, 0)), Bit = Rotate(new(2, 0)), YukseklikM = 2 });
    var surfaces = Bina3DGorunumServisi.YuzeyleriOlustur(geo);
    Near(surfaces.Where(s => s.Category == "Duvar").Sum(Area), 52, "Rotated wall area");
    var door = surfaces.Single(s => s.Category == "Kapi");
    Near(door.Vertices[0].X, 13 + Math.Cos(angle), "Door offset X");
    Near(door.Vertices[0].Y, -7 + Math.Sin(angle), "Door offset Y");
}

static void OverlappingOpenings()
{
    var (geo, kat, _) = Room();
    kat.Acikliklar.Add(Opening(1, 2));
    kat.Acikliklar.Add(Opening(1, 2));
    kat.Acikliklar.Add(Opening(1.5, 2.5));
    var surfaces = Bina3DGorunumServisi.YuzeyleriOlustur(geo);
    Near(surfaces.Where(s => s.Category == "Duvar").Sum(Area), 54 - 1.5 * 2.1, "Overlapping wall area");
    Near(surfaces.Where(s => s.Category == "Kapi").Sum(Area), 1.5 * 2.1, "Union door area");
}

static void InvalidOpenings()
{
    var (geo, kat, _) = Room();
    kat.Acikliklar.Add(new BinaAciklik { Bas = new(1, 0), Bit = new(2, 0), IptalEdildi = true });
    kat.Acikliklar.Add(new BinaAciklik { Bas = new(1, 0), Bit = new(2, 0), YukseklikM = double.NaN });
    kat.Acikliklar.Add(new BinaAciklik { Bas = new(1, 0), Bit = new(2, 0), EsikYuksekligiM = -1 });
    kat.Acikliklar.Add(new BinaAciklik { Bas = new(1, 0), Bit = new(2, 0), YukseklikM = -1 });
    kat.Acikliklar.Add(new BinaAciklik { Bas = new(1, 0), Bit = new(1, 0) });
    kat.Acikliklar.Add(new BinaAciklik { Bas = new(double.NaN, 0), Bit = new(2, 0) });
    kat.Acikliklar.Add(new BinaAciklik { Bas = new(1, 0), Bit = new(2, 0), EsikYuksekligiM = 4 });
    kat.Acikliklar.Add(new BinaAciklik { Bas = new(1, 2), Bit = new(2, 2) });
    kat.Acikliklar.Add(new BinaAciklik { Bas = new(1, 0), Bit = new(1, 1) });
    kat.Acikliklar.Add(Opening(8, 9));
    var surfaces = Bina3DGorunumServisi.YuzeyleriOlustur(geo);
    Near(surfaces.Where(s => s.Category == "Duvar").Sum(Area), 54, "Invalid openings leave full walls");
    if (surfaces.Any(s => s.Category is "Kapi" or "Pencere")) throw new Exception("Invalid opening rendered.");
    if (Bina3DGorunumServisi.Acikliklar3D(geo).Count != 0) throw new Exception("Invalid opening line rendered.");
}

static void FloorOrder()
{
    var (geo, kat, oda) = Room();
    kat.Acikliklar.Add(Opening(1, 2, BinaAciklikTipi.Pencere));
    var altKat = new BinaKat { KatYuksekligiM = 4, BloklariGoster = false };
    geo.Katlar.Add(altKat);
    var surfaces = Bina3DGorunumServisi.YuzeyleriOlustur(geo);
    Near(surfaces.Single(s => s.Category == "Doseme").Vertices[0].Z, 4, "Upper floor elevation");
    Near(surfaces.Where(s => s.Category == "Pencere").SelectMany(s => s.Vertices).Min(p => p.Z), 4.9, "Upper sill");
    Near(Bina3DGorunumServisi.Acikliklar3D(geo).Single().Bas.Z, 4.9, "Opening line sill");
}

static void SplitWall()
{
    var (geo, kat, oda) = Room();
    oda.Poligon.Insert(1, new(1.5, 0));
    var door = Opening(1, 2); door.YukseklikM = 4;
    kat.Acikliklar.Add(door);
    var surfaces = Bina3DGorunumServisi.YuzeyleriOlustur(geo);
    Near(surfaces.Where(s => s.Category == "Kapi").Sum(Area), 3, "Split opening area clipped to ceiling");
    Near(surfaces.Where(s => s.Category == "Duvar").Sum(Area), 51, "Split wall remainder");
}

static void ExplicitZeroSill()
{
    var (geo, kat, _) = Room();
    var window = Opening(1, 2, BinaAciklikTipi.Pencere);
    Near(window.EtkinEsikYuksekligiM, 0.9, "Legacy sill default");
    window.EsikYuksekligiBelirtildi = true;
    window.ManuelEklendi = true;
    window.YukseklikM = 2.8;
    kat.Acikliklar.Add(window);
    var surfaces = Bina3DGorunumServisi.YuzeyleriOlustur(geo);
    Near(surfaces.Where(s => s.Category == "Pencere").SelectMany(s => s.Vertices).Min(p => p.Z), 0, "Explicit zero sill");
    Near(surfaces.Where(s => s.Category == "Pencere").Sum(Area), 2.8, "Full height glazing");
    var retained = AciklikTespitServisi.Bul([], [], kat.Acikliklar).Single();
    Near(retained.EtkinEsikYuksekligiM, 0, "Rescan retains explicit zero");
    if (!retained.EsikYuksekligiBelirtildi) throw new Exception("Rescan lost sill flag.");
}

static void ExplicitFloorElevation()
{
    var (geo, top, _) = Room();
    top.TabanKotuM = 4.2;
    top.DxfSegmentler.Add(new BinaDxfSegment { A = new(0, 0), B = new(5, 0) });
    top.Acikliklar.Add(Opening(1, 2, BinaAciklikTipi.Pencere));
    var middle = new BinaKat { KatYuksekligiM = 4.2 };
    middle.Odalar.Add(new BinaOda { Ad = "Middle", Poligon = [new(0, 0), new(5, 0), new(5, 4), new(0, 4)] });
    var bottom = new BinaKat { TabanKotuM = -3.5, KatYuksekligiM = 3.5, BloklariGoster = false };
    geo.Katlar.Add(middle);
    geo.Katlar.Add(bottom);
    var surfaces = Bina3DGorunumServisi.YuzeyleriOlustur(geo);
    Near(surfaces.Single(s => s.Category == "Doseme" && s.OwnerId == top.Odalar[0].Id).Vertices[0].Z, 4.2, "Explicit top elevation");
    Near(surfaces.Single(s => s.Category == "Doseme" && s.OwnerId == middle.Odalar[0].Id).Vertices[0].Z, 0, "Implicit middle from basement");
    Near(Bina3DGorunumServisi.ArkaPlanSegmentleri(geo).Single().A.Z, 4.2, "Background explicit elevation");
    Near(Bina3DGorunumServisi.Acikliklar3D(geo).Single().Bas.Z, 5.1, "Opening explicit elevation");
    top.TabanKotuM = null;
    Near(Bina3DGorunumServisi.YuzeyleriOlustur(geo).Single(s => s.Category == "Doseme" && s.OwnerId == top.Odalar[0].Id).Vertices[0].Z, 4.2, "Null top stacks once");
}

static void MetadataRoundTrip()
{
    var (geo, kat, _) = Room();
    kat.TabanKotuM = -2.75;
    var window = Opening(1, 2, BinaAciklikTipi.Pencere);
    window.EsikYuksekligiBelirtildi = true;
    kat.Acikliklar.Add(window);
    var path = Path.Combine(Path.GetTempPath(), $"bina3d-{Guid.NewGuid():N}.e4bina.json");
    try
    {
        BinaGeometriServisi.Kaydet(geo, path);
        var restored = BinaGeometriServisi.Ac(path);
        Near(restored.Katlar.Single().TabanKotuM ?? double.NaN, -2.75, "Saved floor elevation");
        var restoredWindow = restored.Katlar.Single().Acikliklar.Single();
        Near(restoredWindow.EtkinEsikYuksekligiM, 0, "Saved explicit zero");
        if (!restoredWindow.EsikYuksekligiBelirtildi) throw new Exception("Saved sill flag lost.");
        var old = System.Text.Json.JsonSerializer.Deserialize<BinaKat>("{\"Acikliklar\":[{\"Tip\":1,\"EsikYuksekligiM\":0}]}")!;
        if (old.TabanKotuM is not null) throw new Exception("Legacy floor acquires false elevation.");
        Near(old.Acikliklar.Single().EtkinEsikYuksekligiM, 0.9, "Legacy JSON default sill");
    }
    finally { if (File.Exists(path)) File.Delete(path); }
}
