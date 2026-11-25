using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Bina.Services;
using Kst.Acoustics.Core.Common.Geometry;

if (args.Contains("--raw")) {
    var raw = netDxf.DxfDocument.Load("scratch/bina-import/source.dxf");
    foreach (var t in raw.Entities.Texts.Where(t => t.Position.X > 329000 && t.Position.X < 334800 && t.Value.StartsWith('+')))
        Console.WriteLine($"{t.Layer.Name}: {t.Position.X:0.00},{t.Position.Y:0.00} {t.Value}");
    return;
}
if (args.Contains("--sample"))
{
    var drawing = new DxfKatPlaniParser().KatmanliOku("scratch/bina-import/source.dxf");
    var layers = new[] { "0", "alcipan", "CAM", "DUVAR", "KAPI", "KOLON", "KOT", "PLAN_YAZI", "SIVA", "sıva", "YAZI", "YAZİ", "YAZI TEF" };
    var ham = drawing.HamKatOlustur(layers);
    var clusters = KatKumelemeServisi.Kumele(ham.HamSegmentler, ham.HamMetinler,
        ham.DxfArclar.Select(a => (a.Merkez, a.R, a.BasAciDeg, a.BitAciDeg)).ToList());
    var (plans, _) = KatKumelemeServisi.PlanFiltrele(clusters);
    var candidates = clusters.Select(c => {
        var title = KatEtiketServisi.TahminEt(c);
        return new DxfKatYukleyici.CokluKatAdayi(c, title.OnerilenAd, title.SiraDegeri, title.EtiketBulundu, plans.Contains(c))
        { Olculer = MimariProjeMetinServisi.YukseklikOku(c.Metinler) };
    }).ToList();
    var result = MimariKatYukseklikServisi.Cozumle(candidates);
    if (args.Contains("--dump")) foreach (var c in candidates.Where(c => !c.OneriliDahil)) {
        Console.WriteLine($"CLUSTER {c.Kume.MinX:0.00},{c.Kume.MinY:0.00} {c.Kume.Metinler.Count}");
        foreach (var t in c.Kume.Metinler.Where(t => t.Value.StartsWith('+') || t.Value.Contains("KESİT"))) Console.WriteLine($"TEXT {t.Pos.X:0.00},{t.Pos.Y:0.00} {t.Value}");
    }
    foreach (var floor in result.Where(c => c.OneriliDahil).OrderBy(c => c.SiraDegeri))
    {
        Console.WriteLine($"FLOOR {floor.OnerilenAd} z={floor.Olculer!.TabanKotuM} h={floor.Olculer.KatYuksekligiM}");
        foreach (var s in floor.Olculer.Kanitlar.Concat(floor.Olculer.Uyarilar)) Console.WriteLine(s);
    }
    var selected = result.Where(c => c.OneriliDahil).OrderBy(c => c.SiraDegeri).ToList();
    Check(selected.Count == 3, "sample three plans");
    Check(selected[0].Olculer!.TabanKotuM == 0 && selected[0].Olculer!.KatYuksekligiM == 4, "sample ground 0 / 4");
    Check(selected[1].Olculer!.TabanKotuM == 4 && selected[1].Olculer!.KatYuksekligiM == 4, "sample first 4 / 4");
    Check(selected[2].Olculer!.TabanKotuM == 8 && selected[2].Olculer!.KatYuksekligiM is null, "sample roof 8 / unknown");
    return;
}

var plans3 = new[] { Plan("Zemin Kat", 0, 0, 1, 1.33), Plan("1. Kat", 1, 4, 4.5, 3.17), Plan("Çatı Katı", 9999, 8) };
var section = Section("A-A KESİTİ 1/50", 20, 0, 4, 8);
var solved = MimariKatYukseklikServisi.Cozumle([..plans3, section]);
Check(solved[0].Olculer!.TabanKotuM == 0 && solved[0].Olculer!.KatYuksekligiM == 4 &&
    solved[1].Olculer!.TabanKotuM == 4 && solved[1].Olculer!.KatYuksekligiM == 4 &&
    solved[2].Olculer!.TabanKotuM == 8 && solved[2].Olculer!.KatYuksekligiM is null, "section confirms 0/4/8 and roof adjacency without inventing roof height");
Check(plans3[0].Olculer!.TabanKotuM is null && plans3[0].Olculer!.KatYuksekligiM is null, "source candidate not mutated");
Check(solved[0].Olculer!.Kanitlar.Any(s => s.Contains("düşey")), "coordinate evidence retained");

var steps = MimariKatYukseklikServisi.Cozumle([..plans3, Section("B-B KESİTİ", 40, 0, 1, 4, 8)]);
Check(steps[0].Olculer!.TabanKotuM == 0 && steps[0].Olculer!.Uyarilar.Any(s => s.Contains("Kademeli")), "stepped floor retains lower main reference with explicit local warning");

var unrelated = Section("VAZİYET PLANI", 20, 0, 4, 8);
Check(MimariKatYukseklikServisi.Cozumle([..plans3, unrelated])[0].Olculer!.TabanKotuM is null, "site is not a vertical section");
var scaled = section with { Kume = section.Kume with { Metinler = section.Kume.Metinler.Select(t => (new Point2D(t.Pos.X, t.Pos.Y * 2), t.Value)).ToList() } };
Check(MimariKatYukseklikServisi.Cozumle([..plans3, scaled])[0].Olculer!.TabanKotuM is null, "inconsistent drawing scale does not manufacture storey height");
var duplicate = section with { Kume = section.Kume with { Metinler = section.Kume.Metinler.Select(t => (new Point2D(0, t.Pos.Y), t.Value)).ToList() } };
Check(MimariKatYukseklikServisi.Cozumle([..plans3, duplicate])[0].Olculer!.TabanKotuM is null, "overlaid CAD duplicates are not independent level evidence");
var mixed = Plan("Zemin Kat", 0, 0, 4);
Check(MimariKatYukseklikServisi.Cozumle([mixed, section])[0].Olculer!.TabanKotuM is null, "two storey ranges in one plan remain unresolved");
var explicitHeight = plans3[0] with { Olculer = plans3[0].Olculer! with { KatYuksekligiM = 3.5 } };
var conflict = MimariKatYukseklikServisi.Cozumle([explicitHeight, plans3[1], plans3[2], section])[0].Olculer!;
Check(conflict.KatYuksekligiM == 3.5 && conflict.Uyarilar.Any(s => s.Contains("uyuşmuyor")), "explicit height conflict is visible and not overwritten");
var skipped = MimariKatYukseklikServisi.Cozumle([Plan("1. Kat", 1, 4), Plan("Çatı Katı", 9999, 12), Section("A-A KESİTİ", 20, 4, 8, 12)]);
Check(skipped[0].Olculer!.KatYuksekligiM is null, "missing intermediate storey does not become roof-adjacent height");
var sparseRoof = MimariKatYukseklikServisi.Cozumle([Plan("1. Kat", 1, 4), Plan("Çatı Katı", 9999, 12),
    Section("A-A KESİTİ", 20, 4, 8, 12), Section("B-B KESİTİ", 50, 4, 12)]);
Check(sparseRoof[0].Olculer!.KatYuksekligiM is null, "sparse second view cannot bypass intermediate storey found in first section");
var partialRoof = MimariKatYukseklikServisi.Cozumle([Plan("1. Kat", 1, 4), Plan("Çatı Katı", 9999, 12),
    Section("A-A KESİTİ", 20, 4, 8), Section("B-B KESİTİ", 50, 4, 12)]);
Check(partialRoof[0].Olculer!.KatYuksekligiM is null, "intermediate storey in partial section blocks skipped roof height");
var duplicateFloor = MimariKatYukseklikServisi.Cozumle([plans3[0], plans3[0], plans3[1], section]);
Check(duplicateFloor[0].Olculer!.TabanKotuM is null, "duplicate floor numbers across blocks are not combined");
var lowerHidden = Plan("Zemin Kat", 0, 0, 0, 1, 1);
Check(MimariKatYukseklikServisi.Cozumle([lowerHidden, plans3[1], Section("A-A KESİTİ", 20, 1, 4, 8)])[0].Olculer!.TabanKotuM is null,
    "hidden lower section datum does not promote upper landing into storey base");
var blockPlan = plans3[0] with { Kume = plans3[0].Kume with { Metinler = [..plans3[0].Kume.Metinler, (new(0, -3), "A BLOK ZEMİN KAT PLANI")] } };
Check(MimariKatYukseklikServisi.Cozumle([blockPlan, section])[0].Olculer!.TabanKotuM is null, "named blocks require explicit section-plan correspondence");
Check(MimariKatYukseklikServisi.BlokEslesmesiGerekli([blockPlan, section]) &&
    !MimariKatYukseklikServisi.BlokEslesmesiGerekli([..plans3, section]), "typed block guard available to loader fallback");
var absolute = MimariKatYukseklikServisi.Cozumle([Plan("Zemin Kat", 0, 919.25), Section("A-A KESİTİ", 20, 919.25, 923.25)]);
Check(absolute[0].Olculer!.TabanKotuM is null, "survey altitude does not become relative floor datum");

var floorLocal = new BinaKat { HamMetinler = [(new(2, 2), "+1.00(+920.25)"), (new(2, 3), "+920.25"), (new(7, 2), "+4.50")] };
floorLocal.Odalar.Add(Room("DÜKKAN", 0)); floorLocal.Odalar.Add(Room("TAZİYE", 5));
var locals = MimariKatYukseklikServisi.YerelKotlariOku(floorLocal);
Check(locals.Kotlar.Count == 2 && locals.Kotlar[0].TabanKotuM == 1 && locals.Kotlar[1].TabanKotuM == 4.5, "local room elevations preserved independently of main storey datum");
Check(locals.Kotlar.All(k => !k.DosemeKotuAcik), "bare relative levels do not claim explicit floor evidence");
Check(floorLocal.Odalar.All(r => r.TabanKotuOverrideM is null && r.YukseklikOverrideM is null), "local analysis does not mutate rooms or infer clear heights");
floorLocal.HamMetinler.Add((new(1, 1), "+0.00"));
Check(MimariKatYukseklikServisi.YerelKotlariOku(floorLocal).Kotlar.Count == 1, "conflicting levels in merged room are not flattened");
floorLocal.HamMetinler = [(new(0, 2), "+1.00"), (new(4.5, 2), "+1.00")];
Check(MimariKatYukseklikServisi.YerelKotlariOku(floorLocal).Kotlar.Count == 0, "boundary and adjacent exterior labels are not propagated into rooms");
floorLocal.Odalar[0].Ad = "MERDİVEN"; floorLocal.HamMetinler = [(new(2, 2), "+1.33")];
Check(MimariKatYukseklikServisi.YerelKotlariOku(floorLocal).Kotlar.Count == 0, "stair landing is not whole-room floor elevation");
floorLocal.Odalar[0].Ad = "WC";
floorLocal.HamMetinler = [(new(2, 2), "+3.17"), (new(2, 3), "h=2.80 m")];
Check(!MimariKatYukseklikServisi.YerelKotlariOku(floorLocal).Kotlar.Single().DosemeKotuAcik,
    "clear height alone does not turn bare stair level into floor evidence");
floorLocal.HamMetinler = [(new(2, 2), "DÖŞEME KOTU: +1.00"), (new(2, 3), "h=2.80 m")];
Check(MimariKatYukseklikServisi.YerelKotlariOku(floorLocal).Kotlar.Single().DosemeKotuAcik,
    "explicit floor label provides separate floor-level evidence");
Console.WriteLine("All height regressions passed.");

void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
DxfKatYukleyici.CokluKatAdayi Plan(string name, double order, params double[] levels)
{
    var texts = levels.Select((z, i) => (new Point2D(i, 0), Signed(z))).ToList();
    var cluster = new KatKumelemeServisi.KatKumesi(0, 0, 20, 20, [], texts, []);
    return new(cluster, name, order, true, true) { Olculer = MimariProjeMetinServisi.YukseklikOku(texts) };
}
DxfKatYukleyici.CokluKatAdayi Section(string title, double y, params double[] levels)
{
    var texts = new List<(Point2D Pos, string Value)> { (new(5, y - 3), title) };
    foreach (var z in levels) { texts.Add((new(0, y + z), Signed(z))); texts.Add((new(10, y + z), Signed(z))); }
    var cluster = new KatKumelemeServisi.KatKumesi(0, y - 5, 20, y + 30, [], texts, []);
    return new(cluster, "Kesit", 100, false, false);
}
string Signed(double z) => z.ToString("+0.00;-0.00;+0.00", System.Globalization.CultureInfo.InvariantCulture);
BinaOda Room(string name, double x) => new() { Ad = name, Poligon = [new(x, 0), new(x + 4, 0), new(x + 4, 4), new(x, 4)] };
