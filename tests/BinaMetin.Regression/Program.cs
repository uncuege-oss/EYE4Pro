using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Bina.Services;
using Kst.Acoustics.Core.Common.Geometry;

if (args.Length > 0)
{
    var dxf = netDxf.DxfDocument.Load(args[0]);
    foreach (var t in dxf.Entities.Texts.Where(t => t.Value.Trim().StartsWith('+') || t.Value.Trim().StartsWith('-') || t.Layer.Name.Contains("KOT", StringComparison.OrdinalIgnoreCase)))
        Console.WriteLine($"{t.Position.X:F2},{t.Position.Y:F2}: {t.Value}");
    return;
}

void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); }
List<(Point2D Pos, string Value)> Texts(params string[] values) => values.Select(s => (new Point2D(0, 0), s)).ToList();
var h = MimariProjeMetinServisi.YukseklikOku(Texts("KAT YÜKSEKLİĞİ: 350 cm", "h=2.80", "+3.50"));
Check(h.KatYuksekligiM == 3.5 && h.TabanKotuM == 3.5, "explicit storey height and elevation");
Check(MimariProjeMetinServisi.YukseklikOku(Texts("h=2.80", "90/210", "P1 120/150")).KatYuksekligiM is null, "clear height and opening dimensions do not become storey height");
h = MimariProjeMetinServisi.YukseklikOku(Texts("KAT YÜKSEKLİĞİ=3.50", "KAT YÜKSEKLİĞİ=4.00", "+3.50", "+3.70"));
Check(h.KatYuksekligiM is null && h.TabanKotuM is null && h.Uyarilar.Count == 2, "conflicting dimensions are reported");
h = MimariProjeMetinServisi.YukseklikOku(Texts("+0.00", "+0.00", "+0.00", "+0.00", "+1.33", "+2.66", "+919.25", "+919.25"));
Check(h.TabanKotuM == 0 && h.Uyarilar.Count == 1, "dominant relative level ignores absolute terrain levels and reports secondary levels");
Check(MimariProjeMetinServisi.YukseklikOku(Texts("+0.00", "+0.00", "+1.00", "+1.00")).TabanKotuM is null, "split level plan requires review");
Check(MimariProjeMetinServisi.KotFarkindanYukseklik(-3.2, 0) == 3.2 && MimariProjeMetinServisi.KotFarkindanYukseklik(3, 0) is null, "only positive plausible level difference");
var kat = new BinaKat();
var door = new BinaAciklik { Bas = new(0,0), Bit = new(.9,0), YukseklikM = 2.1 };
var window = new BinaAciklik { Bas = new(4,0), Bit = new(5.5,0), YukseklikM = 2.1 };
kat.Acikliklar.AddRange([door, window]);
kat.HamMetinler = [(new(.45,.2), "K1 90/220"), (new(4.75,.2), "P1 150/140 DENİZLİK=80")];
var result = MimariProjeMetinServisi.AciklikBoyutlariniUygula(kat);
Check(result.Uygulanan == 2 && door.YukseklikM == 2.2 && window.Tip == BinaAciklikTipi.Pencere && window.YukseklikM == 1.4 && window.EsikYuksekligiM == .8, "local door/window dimensions and sill");
kat.HamMetinler = [(new(.45,.2), "K1 90/230"), (new(.45,.3), "K1 90/250")];
result = MimariProjeMetinServisi.AciklikBoyutlariniUygula(kat);
Check(result.Uygulanan == 0 && door.YukseklikM == 2.2 && result.Uyarilar.Count == 1, "conflicting opening labels do not overwrite");
kat.HamMetinler = [(new(.45,.2), "K1"), (new(.45,.4), "90/240"), (new(100,100), "P1 150/100")];
result = MimariProjeMetinServisi.AciklikBoyutlariniUygula(kat);
Check(result.Uygulanan == 1 && door.YukseklikM == 2.4 && window.YukseklikM == 1.4, "split nearby label and remote schedule ignored");
kat.HamMetinler = [(new(.45,.2), "K1 150/240")];
result = MimariProjeMetinServisi.AciklikBoyutlariniUygula(kat);
Check(result.Uygulanan == 0 && result.Uyarilar.Count == 1, "width mismatch reported without moving wall geometry");
var room = new BinaOda { Ad="Salon", Poligon=[new(0,0), new(4,0), new(4,4), new(0,4)] };
kat.Odalar.Add(room);
kat.HamMetinler = [(new(2,2), "h=280 cm"), (new(9,9), "h=5.00"), (new(1,1), "KAT YÜKSEKLİĞİ: 3.50")];
result = MimariProjeMetinServisi.OdaYukseklikleriniUygula(kat);
Check(result.Uygulanan == 1 && room.YukseklikOverrideM == 2.8 && kat.KatYuksekligiM == 3, "net height belongs only to containing room");
kat.HamMetinler = [(new(2,2), "SU BAZLI SATEN BOYA:h=2.50"), (new(.45,.2), "- K01 - 90/210 LAMİNAT KAPLAMA")];
Check(MimariProjeMetinServisi.OdaYukseklikleriniUygula(kat).Uygulanan == 0 && MimariProjeMetinServisi.AciklikBoyutlariniUygula(kat).Uygulanan == 0, "finish notes and legend rows are not local dimensions");
kat.HamMetinler = [(new(.45,.2), "YK 90/220"), (new(4.75,.2), "150/160")];
result = MimariProjeMetinServisi.AciklikBoyutlariniUygula(kat);
Check(result.Uygulanan == 2 && door.YukseklikM == 2.2 && window.YukseklikM == 1.6, "fire door and plain size on known window");
kat.HamMetinler = [(new(4.75,.2), "P1 150/240 DENİZLİK=0")];
result = MimariProjeMetinServisi.AciklikBoyutlariniUygula(kat);
Check(result.Uygulanan == 1 && window.EsikYuksekligiBelirtildi && window.EtkinEsikYuksekligiM == 0, "explicit zero window sill is preserved");

BinaAciklik Door(double x0, double y0, double x1, double y1, string source = "arc") =>
    new() { Bas = new(x0, y0), Bit = new(x1, y1), Tip = BinaAciklikTipi.Kapi, YukseklikM = 2.1, Kaynak = source };
var narrow = Door(0, 0, .3, 0);
var correct = Door(0, .5, .9, .5);
var candidates = new BinaKat { Acikliklar = [narrow, correct], HamMetinler = [(new(.15,.1), "K1 90/230")] };
result = MimariProjeMetinServisi.AciklikBoyutlariniUygula(candidates);
Check(result.Uygulanan == 1 && correct.YukseklikM == 2.3 && narrow.YukseklikM == 2.1, "nearer wrong-width opening does not block compatible opening");
var face1 = Door(0, 0, .9, 0);
var face2 = Door(.9, .2, 0, .2, "arc-yansima");
candidates = new() { Acikliklar = [face1, face2], HamMetinler = [(new(.45,.1), "K4 90/240")] };
result = MimariProjeMetinServisi.AciklikBoyutlariniUygula(candidates);
Check(result.Uygulanan == 2 && face1.YukseklikM == 2.4 && face2.YukseklikM == 2.4, "both sides of same opening receive dimensions");
var leaf1 = Door(0, 0, .78, 0);
var leaf2 = Door(1.6, 0, .82, 0);
var leaf1Back = Door(.78, .2, 0, .2, "arc-yansima");
var leaf2Back = Door(.82, .2, 1.6, .2, "arc-yansima");
candidates = new() { Acikliklar = [leaf1, leaf2, leaf1Back, leaf2Back], HamMetinler = [(new(.8,.15), "K01 160/240")] };
result = MimariProjeMetinServisi.AciklikBoyutlariniUygula(candidates);
Check(result.Uygulanan == 4 && candidates.Acikliklar.All(a => a.YukseklikM == 2.4) && leaf1.Bas == new Point2D(0,0) && leaf1.Bit == new Point2D(.78,0) && Math.Abs(leaf1.GenislikM-.78)<1e-9, "double door leaves and rear faces keep geometry and receive common height");
candidates = new() { Acikliklar = [Door(0,0,.8,0), Door(1.05,0,1.85,0)], HamMetinler = [(new(.9,.1),"K1 160/240")] };
result = MimariProjeMetinServisi.AciklikBoyutlariniUygula(candidates);
Check(result.Uygulanan == 0, "separated adjacent doors are not combined");
candidates = new() { Acikliklar = [Door(0,0,.8,0), Door(.8,0,.8,.8)], HamMetinler = [(new(.8,.1),"K1 160/240")] };
Check(MimariProjeMetinServisi.AciklikBoyutlariniUygula(candidates).Uygulanan == 0, "perpendicular doors are not combined");
candidates = new() { Acikliklar = [Door(0,0,.9,0), Door(1.1,0,2,0)], HamMetinler = [(new(1,.1),"K1 90/240")] };
result = MimariProjeMetinServisi.AciklikBoyutlariniUygula(candidates);
Check(result.Uygulanan == 0 && result.Uyarilar.Any(s => s.Contains("birden fazla")), "ambiguous adjacent compatible doors require review");

foreach (string label in new[] { "OKUMA SALONU", "YEMEKHANE", "BAY WC", "BAYAN WC", "BAYAN TAZİYE", "BAY MESCİD", "BAYAN MESCİT", "DÜKKÂN", "GÖREVLİ", "TEM. HOL", "KLİMA TERASI", "Seramik Atölyesi", "Müşteri Hizmetleri", "Yaratıcı Çalışmalar" })
    Check(MimariOdaEtiketServisi.TryName(label, out var name) && name == label, "room name: " + label);
foreach (string label in new[] { "LAMİNAT KORUMA BANDI 30cm ENİNDE", "DVR 03", "KC01", "KENETLİ SAÇ ÇATI ÖRTÜSÜ 0.7mm", "OTURUM İZİ", "SAÇ HARPUŞTA", "SAC OLUK", "PASLANMAZ KORKULUK", "PASLANMAZ TEZGAH", "mermer denizlik", "AL. DOĞ. ARASI ISI CAM", "ALÜMiNYUM DOGRAMA(TEMPERLi CAM 10mm)", "60*120 MEKANİK KONSTRÜKSİYON", "OTOMATİK KEYPENK 8MM", "ZEMİN KAT PLANI", "A-A KESİTİ", "K7 90/240", "SU BAZLI SATEN BOYA:h=1 metre üstü", "2 KAT SU YALITIMI", "YANGIN KAPISI" })
    Check(!MimariOdaEtiketServisi.TryName(label, out _), "reject annotation: " + label);
Check(MimariOdaEtiketServisi.Degerlendir("OKUMA SALONU 25,30 m²")?.Ad == "OKUMA SALONU", "room area is removed from displayed name");
var roomPlan = new BinaKat
{
    HamSegmentler = [(new(0,0), new(8,0)), (new(8,0),new(8,6)), (new(8,6),new(0,6)), (new(0,6),new(0,0))],
    HamMetinler = [(new(4,3),"DVR 03"), (new(3,3), "LAMİNAT KORUMA BANDI 30cm ENİNDE"), (new(1,1),"OKUMA SALONU")]
};
Check(DxfKatPlaniParser.OdalariBosluklardanYenidenTara(roomPlan) == 1 && roomPlan.Odalar[0].Ad == "OKUMA SALONU", "parser assigns room label instead of central material annotation");

foreach (var label in new[] { "sığınak", "danışma", "çalışma odası" })
    Check(MimariOdaEtiketServisi.ContainsRoomName(label), "dotless Turkish i in room label: " + label);
Check(!MimariOdaEtiketServisi.TryName("sıva", out _), "lowercase sıva is not a room name");
foreach (var label in new[] { "MDFLAM ALTI DOLAP", "AKRİLİK KAP.", "GÖMME DOLAP" })
    Check(!MimariOdaEtiketServisi.TryName(label, out _), "furniture and abbreviated finish notes are not room names: " + label);
foreach (var label in new[] { "MDF Deposu", "Dolap Odası" })
    Check(MimariOdaEtiketServisi.ContainsRoomName(label), "room purpose distinguishes furniture storage: " + label);
var roofLabel = new KatKumelemeServisi.KatKumesi(0,0,10,10,[],[(new(1,1), "çatı katı planı 1/50")],[]);
Check(KatEtiketServisi.TahminEt(roofLabel).OnerilenAd == "Çatı Katı", "lowercase çatı katı planı recognized");
var lowerDoor = new BinaKat { KatYuksekligiM = 3.5, HamMetinler = [(new(.45,.1), "kapı 90/240")],
    Acikliklar = [new BinaAciklik { Bas = new(0,0), Bit = new(.9,0), Tip = BinaAciklikTipi.Kapi, YukseklikM = 2.1 }] };
Check(MimariProjeMetinServisi.AciklikBoyutlariniUygula(lowerDoor).Uygulanan == 1 && lowerDoor.Acikliklar[0].YukseklikM == 2.4,
    "lowercase kapı size applied");
