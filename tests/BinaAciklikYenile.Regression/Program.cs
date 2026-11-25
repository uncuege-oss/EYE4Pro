using System.Text.Json;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Bina.Services;
using Kst.Acoustics.Core.Common.Geometry;

int passed = 0;
void Check(bool value, string title)
{
    if (!value) throw new Exception(title);
    Console.WriteLine("PASS " + title); passed++;
}
var wall = new List<(Point2D A, Point2D B)> { (new(-5, 0), new(10, 0)) };
var arcs = new List<(Point2D Merkez, double R, double BasDeg, double BitDeg)> { (new(1, 0), .9, 0, 90) };
List<BinaAciklik> Find(IReadOnlyList<BinaAciklik> old,
    IReadOnlyList<(Point2D Merkez, double R, double BasDeg, double BitDeg)>? source = null,
    AciklikTespitServisi.Ayarlar? settings = null)
    => AciklikTespitServisi.Bul(wall, source ?? arcs, old, settings);
BinaAciklik Mark(BinaAciklik value, string id = "door")
{
    value.Id = id; value.Tip = BinaAciklikTipi.Pencere; value.YukseklikM = 2.4;
    value.EsikYuksekligiM = 0; value.EsikYuksekligiBelirtildi = true;
    value.Kaynak = "arc; etiket: P1 90/240";
    return value;
}
var original = Mark(Find([]).Single());
original.GenislikM = 123; // A stored stale width must not override current geometry.
var rescanned = Find([original]).Single();
Check(rescanned.Id == "door", "Unchanged automatic opening preserves identity");
Check(rescanned.Tip == BinaAciklikTipi.Pencere && rescanned.YukseklikM == 2.4,
    "Rescan preserves edited type and annotated height");
Check(rescanned.EsikYuksekligiBelirtildi && rescanned.EtkinEsikYuksekligiM == 0,
    "Explicit zero sill remains floor-level glazing");
Check(Math.Abs(rescanned.GenislikM - .9) < 1e-9 && original.GenislikM == 123 && !ReferenceEquals(original, rescanned),
    "Current geometric width retained without mutating prior opening");
Check(rescanned.Kaynak == original.Kaynak, "Dimension evidence survives rescan");

var restored = JsonSerializer.Deserialize<BinaAciklik>(JsonSerializer.Serialize(original))!;
Check(Find([restored]).Single().Id == "door" && Find([restored]).Single().YukseklikM == 2.4,
    "Identity and dimensions survive rescan after JSON reload");

var reversed = Mark(Find([]).Single(), "reverse");
(reversed.Bas, reversed.Bit) = (reversed.Bit, reversed.Bas);
Check(Find([reversed]).Single().Id == "reverse", "Reversed segment endpoints match unchanged opening");

var shifted = new List<(Point2D Merkez, double R, double BasDeg, double BitDeg)> { (new(1.005, 0), .9, 0, 90) };
Check(Find([original], shifted).Single().Id == "door" && Math.Abs(Find([original], shifted).Single().Bas.X - 1.005) < 1e-9,
    "Small coordinate rounding preserves data and current endpoint");
var moved = new List<(Point2D Merkez, double R, double BasDeg, double BitDeg)> { (new(1.1, 0), .9, 0, 90) };
Check(Find([original], moved).Single().Id != "door" && Find([original], moved).Single().YukseklikM == 2.1,
    "Moved opening does not inherit unrelated measurements");
var wider = new List<(Point2D Merkez, double R, double BasDeg, double BitDeg)> { (new(1, 0), 1.1, 0, 90) };
Check(Find([original], wider).Single().Id != "door" && Math.Abs(Find([original], wider).Single().GenislikM - 1.1) < 1e-9,
    "Changed opening span retains new geometry and new identity");
Check(Find([original], []).Count == 0, "Removed automatic opening is not resurrected");

var duplicate = Mark(Find([]).Single(), "duplicate"); duplicate.YukseklikM = 3.1;
var ambiguous = Find([original, duplicate]).Single();
Check(ambiguous.Id != original.Id && ambiguous.Id != duplicate.Id && ambiguous.YukseklikM == 2.1,
    "Conflicting old matches are never selected by list order");
var splitCandidates = new List<(Point2D Merkez, double R, double BasDeg, double BitDeg)> {
    (new(1,0),.9,0,90), (new(1.01,0),.9,0,90)
};
var split = Find([original], splitCandidates, new AciklikTespitServisi.Ayarlar { DedupTolM = 0 });
Check(split.Count == 2 && split.All(a => a.Id != "door" && a.YukseklikM == 2.1),
    "One previous record cannot be copied to two new candidates");

var manual = Mark(Find([]).Single(), "manual"); manual.ManuelEklendi = true;
var ignored = Mark(Find([]).Single(), "ignored"); ignored.IptalEdildi = true;
Check(Find([manual]).Single().Id == "manual" && Find([manual], []).Single().ManuelEklendi,
    "Manual opening survives and suppresses duplicate detection");
Check(Find([ignored]).Single().Id == "ignored" && Find([ignored], []).Single().IptalEdildi,
    "Ignored opening survives and remains ignored");

var doubleDoor = new List<(Point2D Merkez, double R, double BasDeg, double BitDeg)> {
    (new(1,0),.8,0,90), (new(2.6,0),.8,90,180)
};
var leaves = Find([], doubleDoor);
Check(leaves.Count == 2, "Fixture contains two contiguous door leaves");
Mark(leaves[0], "left"); Mark(leaves[1], "right");
leaves[0].YukseklikM = 2.2; leaves[1].YukseklikM = 2.5;
var reordered = Find(leaves, doubleDoor.AsEnumerable().Reverse().ToList());
Check(reordered.Single(a => a.Id == "left").YukseklikM == 2.2 && reordered.Single(a => a.Id == "right").YukseklikM == 2.5,
    "Double leaf identity and measurements independent of detection order");

var gapWalls = new List<(Point2D A, Point2D B)> {
    (new(-2,0),new(0,0)), (new(1,0),new(3,0)),
    (new(0,0),new(0,.2)), (new(1,0),new(1,.2)), (new(-2,.3),new(3,.3))
};
var gap = Mark(AciklikTespitServisi.Bul(gapWalls, [], []).Single(), "window-gap");
gap.EsikYuksekligiBelirtildi = false;
var gapAgain = AciklikTespitServisi.Bul(gapWalls, [], [gap]).Single();
Check(gapAgain.Id == "window-gap" && gapAgain.Tip == BinaAciklikTipi.Pencere && gapAgain.YukseklikM == 2.4,
    "Gap detection preserves corrected window metadata");
Check(!gapAgain.EsikYuksekligiBelirtildi && gapAgain.EtkinEsikYuksekligiM == .9,
    "Legacy unspecified window sill keeps its default semantics");

Console.WriteLine($"{passed}/{passed} passed");
