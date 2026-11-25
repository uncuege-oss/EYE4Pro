using netDxf;
using netDxf.Entities;
using netDxf.Tables;
using netDxf.Units;
using Kst.Acoustics.Core.Bina.Services;
using Kst.Acoustics.Core.Terrain.Services;
using Kst.Acoustics.Core.Terrain.Models;

if (args.Length > 0)
{
    string path = args[0];
    using var cad = await MimariCadDosyasi.AcAsync(path);
    Directory.CreateDirectory("scratch/terrain-import");
    string copy = Path.GetFullPath("scratch/terrain-import/adrenapark.dxf");
    if (!string.Equals(cad.DxfYolu, copy, StringComparison.OrdinalIgnoreCase)) File.Copy(cad.DxfYolu, copy, true);
    var doc = DxfDocument.Load(copy);
    Console.WriteLine($"Converted: {copy}; units: {doc.DrawingVariables.InsUnits}; entities: {doc.Entities.All.Count()}");
    var result = new DxfAraziParser().Parse(doc);
    if (args.Contains("--terrain"))
    {
        var layers = result.Katmanlar.Where(l => l.AraziKatmaniOnerisi).Select(l => l.Ad).ToHashSet(StringComparer.OrdinalIgnoreCase);
        result = new DxfAraziParser { Katmanlar = layers, MetrePerBirim = 1 }.Parse(doc);
        result.Arazi.AraziAdi = "Adrenapark — mevcut arazi (eşyükselti eğrileri)";
        result.Arazi.DosyaYolu = path;
        Console.WriteLine("SELECTED " + string.Join(", ",layers));
        Console.WriteLine($"RESULT points={result.Arazi.Noktalar.Count} contours={result.Arazi.Konturlar.Count} unknown={result.KotsuzKonturSayisi}");
        foreach (var g in result.Arazi.Konturlar.GroupBy(k=>k.KatmanAdi))
            Console.WriteLine($"CONTOUR {g.Key}: {g.Count()} vertices={g.Sum(k=>k.Noktalar.Count)} z={g.Min(k=>k.KotM):0.###}..{g.Max(k=>k.KotM):0.###} sources={string.Join(",", g.Select(k=>k.KotKaynagi).Distinct())}");
        foreach(var w in result.Uyarilar)Console.WriteLine("WARN " + w);
        int sourceIndex = Array.IndexOf(args,"--source");
        result.Arazi.Kaynak!.DosyaYolu = Path.GetFullPath(sourceIndex >= 0 && sourceIndex+1 < args.Length ? args[sourceIndex+1] : path);
        string outputDir="terrain/60-hafriyat-hesapla/ornekler/adrenapark";
        Directory.CreateDirectory(outputDir);
        var output=Path.Combine(outputDir,"adrenapark-mevcut.e4arazi.json");
        AraziGeometriServisi.Kaydet(result.Arazi,output);
        Console.WriteLine("SAVED " + Path.GetFullPath(output));
        var sw=System.Diagnostics.Stopwatch.StartNew();
        var surface=new AraziYuzeyServisi().Olustur(result.Arazi);
        double area=surface.Ucgenler.Sum(t=> {
            var a=surface.Dugumler[t.A];var b=surface.Dugumler[t.B];var c=surface.Dugumler[t.C];
            return Math.Abs((b.X-a.X)*(c.Y-a.Y)-(c.X-a.X)*(b.Y-a.Y))/2;
        });
        Console.WriteLine($"TIN nodes={surface.Dugumler.Count} triangles={surface.Ucgenler.Count} area={area:0.###} z={surface.Dugumler.Min(n=>n.Z):0.###}..{surface.Dugumler.Max(n=>n.Z):0.###} elapsed={sw.Elapsed.TotalSeconds:0.##}s");
        using(var csv=new StreamWriter(Path.Combine(outputDir,"adrenapark-kotlar.csv"),false,new System.Text.UTF8Encoding(true))) {
            csv.WriteLine("Katman;X_m;Y_m;Kot_m;Kaynak");
            foreach(var k in result.Arazi.Konturlar)
                foreach(var p in k.Noktalar)
                    csv.WriteLine(FormattableString.Invariant($"{k.KatmanAdi};{p.X:R};{p.Y:R};{k.KotM:R};{k.KotKaynagi}"));
            foreach(var p in result.Arazi.Noktalar)
                csv.WriteLine(FormattableString.Invariant($"{p.KatmanAdi};{p.X:R};{p.Y:R};{p.Z:R};{p.KotKaynagi}"));
        }
        return;
    }
    foreach (var l in result.Katmanlar)
        Console.WriteLine($"LAYER {l.Ad}: entities={l.ElemanSayisi} points={l.NoktaSayisi} contours={l.KonturSayisi} numericTexts={l.KotMetniSayisi} suggested={l.AraziKatmaniOnerisi}");
    Console.WriteLine($"RESULT points={result.Arazi.Noktalar.Count} contours={result.Arazi.Konturlar.Count} unlevelled={result.KotsuzKonturSayisi}");
    foreach (var w in result.Uyarilar) Console.WriteLine("WARN " + w);
    foreach (var l in doc.Entities.Texts.GroupBy(t=>t.Layer.Name))
        Console.WriteLine($"SAMPLE {l.Key}: " + string.Join(" | ", l.Take(10).Select(t => t.Value)));
    return;
}

int count=0;
void Test(string name, Action test) { test(); Console.WriteLine("PASS " + name); count++; }
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
void Near(double actual, double expected) => Check(Math.Abs(actual-expected)<1e-7, $"Expected {expected}, got {actual}");
DxfDocument Doc(DrawingUnits units=DrawingUnits.Meters) { var d=new DxfDocument();d.DrawingVariables.InsUnits=units;return d; }

Test("Explicit unit override scales coordinates but not text heights", () => {
    var d=Doc(DrawingUnits.Unitless); d.Entities.Add(new Text("KOT=125,50",new Vector2(1000,2000),10));
    var r=new DxfAraziParser { MetrePerBirim=.01 }.Parse(d);
    Near(r.Arazi.Noktalar.Single().X,10);Near(r.Arazi.Noktalar.Single().Z,125.5);Check(!r.BirimOnayiGerekiyor,"explicit units");
});
Test("Undefined units require review", () => Check(new DxfAraziParser().Parse(Doc(DrawingUnits.Unitless)).BirimOnayiGerekiyor,"unit confirmation"));
Test("Point pairs to comma elevation label at original point coordinate", () => {
    var d=Doc();d.Entities.Add(new netDxf.Entities.Point(new Vector3(10,20,0)));d.Entities.Add(new Text("123,45",new Vector2(10.5,20.3),.2));
    var r=new DxfAraziParser().Parse(d); Check(r.Arazi.Noktalar.Count==1,"duplicate text point");Near(r.Arazi.Noktalar[0].X,10);Near(r.Arazi.Noktalar[0].Z,123.45);
});
Test("Formatted MTEXT and signed zero are elevations", () => {
    var d=Doc(); d.Entities.Add(new MText(@"{\C1;KOT: -1,25}",new Vector3(0,0,0),.2,5));d.Entities.Add(new Text("±0.00",new Vector2(10,0),.2));
    var r=new DxfAraziParser().Parse(d);Check(r.Arazi.Noktalar.Count==2,"MTEXT missing");Near(r.Arazi.Noktalar[0].Z,-1.25);Near(r.Arazi.Noktalar[1].Z,0);
});
Test("Room area scale stationing and unrelated layer numbers are not heights", () => {
    var d=Doc();foreach(var s in new[]{"ODA 101","12.5 m²","1/100","0+120","DN100","12.5"})d.Entities.Add(new Text(s,new Vector2(0,0),.2));
    d.Entities.Add(new Polyline2D(new[]{new Vector2(20,0),new Vector2(30,0)}){Layer=new Layer("PARSEL_123")});
    var r=new DxfAraziParser().Parse(d);Check(r.Arazi.Noktalar.Count==0,"unrelated text accepted");Check(r.KotsuzKonturSayisi==1,"parcel number accepted");
});
Test("Explicit layer enables pure numeric spot labels",()=> {
    var d=Doc();d.Entities.Add(new Text("127.35",new Vector2(1,2),.2){Layer=new Layer("Olcu")});d.Entities.Add(new Text("98.20",new Vector2(5,5),.2){Layer=new Layer("Diger")});
    var r=new DxfAraziParser{Katmanlar=new HashSet<string>{"Olcu"}}.Parse(d);Check(r.Arazi.Noktalar.Count==1,"layer filter");Near(r.Arazi.Noktalar[0].Z,127.35);
});
Test("Contour label associates to segment midpoint",()=> {
    var d=Doc();d.Entities.Add(new Polyline2D(new[]{new Vector2(0,0),new Vector2(100,0)}));d.Entities.Add(new Text("580",new Vector2(50,1),.2));
    var r=new DxfAraziParser().Parse(d);Near(r.Arazi.Konturlar.Single().KotM,580);
});
Test("Varying polyline Z is preserved instead of averaged",()=> {
    var d=Doc();d.Entities.Add(new Polyline3D(new[]{new Vector3(0,0,100),new Vector3(10,0,110),new Vector3(10,10,120)}));
    var r=new DxfAraziParser().Parse(d);Check(r.Arazi.Konturlar.Count==0,"slope flattened");Check(r.Arazi.Noktalar.Select(n=>n.Z).SequenceEqual(new[]{100.0,110,120}),"varying z lost");
});
Test("Geometric height wins over conflicting annotation",()=> {
    var d=Doc();d.Entities.Add(new netDxf.Entities.Point(new Vector3(0,0,100)));d.Entities.Add(new Text("KOT 105",new Vector2(.1,.1),.2));
    var r=new DxfAraziParser().Parse(d);Near(r.Arazi.Noktalar.Single().Z,100);Check(r.Uyarilar.Any(w=>w.Contains("farklı")),"conflict not reported");
});
Test("Block world transform and inherited layer are read",()=> {
    var d=Doc();var b=new netDxf.Blocks.Block("kot");b.Entities.Add(new Text("KOT 120",new Vector2(1,2),.2));d.Entities.Add(new Insert(b,new Vector3(100,200,0)){Layer=new Layer("TOPO")});
    var r=new DxfAraziParser().Parse(d);Near(r.Arazi.Noktalar.Single().X,101);Near(r.Arazi.Noktalar.Single().Y,202);Check(r.Katmanlar.Any(l=>l.Ad=="TOPO"),"block layer missing");
});
Test("Text toggle suppresses both standalone and paired labels",()=> {
    var d=Doc();d.Entities.Add(new Text("KOT 100",new Vector2(0,0),.2));var r=new DxfAraziParser{MetinKotlariniOku=false}.Parse(d);Check(r.Arazi.Noktalar.Count==0,"text toggle ignored");
});
Test("Known contour labels do not add shifted measurement points",()=> {
    var d=Doc();d.Entities.Add(new Polyline2D(new[]{new Vector2(0,0),new Vector2(20,0)}){Elevation=100});
    d.Entities.Add(new Polyline2D(new[]{new Vector2(0,10),new Vector2(20,10)}){Elevation=110});
    foreach(var x in new[]{2,10,18}) {d.Entities.Add(new Text("KOT 100",new Vector2(x,1),.2));d.Entities.Add(new Text("KOT 110",new Vector2(x,11),.2));}
    var r=new DxfAraziParser().Parse(d);Check(r.Arazi.Noktalar.Count==0,"annotation became spot point");Check(r.Arazi.Konturlar.Count==2,"contours missing");
});
Test("Contour suggestions exclude construction and supplemental labels",()=> {
    var d=Doc();d.Entities.Add(new Polyline2D(new[]{new Vector2(0,0),new Vector2(10,0)}){Elevation=100,Layer=new Layer("EĞRI_1M")});
    d.Entities.Add(new Text("100",new Vector2(0,0),.2){Layer=new Layer("EĞRI_1M_MKOT")});
    d.Entities.Add(new Text("105",new Vector2(0,0),.2){Layer=new Layer("TERASMAN_KOT")});
    var r=new DxfAraziParser().Parse(d);Check(r.Katmanlar.Where(k=>k.AraziKatmaniOnerisi).Select(k=>k.Ad).SequenceEqual(new[]{"EĞRI_1M"}),"unsafe default selection");
});
Test("Conflicting contour annotation is reported instead of changing ground",()=> {
    var d=Doc();d.Entities.Add(new Polyline2D(new[]{new Vector2(0,0),new Vector2(20,0)}){Elevation=100});
    d.Entities.Add(new Text("KOT 105",new Vector2(10,1),.2));
    var r=new DxfAraziParser().Parse(d);Check(r.Arazi.Noktalar.Count==0,"conflicting label created ground point");Check(r.Uyarilar.Any(w=>w.Contains("farklı")),"missing conflict warning");
});
Test("Original CAD provenance survives project save and reopen",()=> {
    var d=Doc();d.Entities.Add(new Text("100.25",new Vector2(1,2),.2){Layer=new Layer("TOPO")});
    var r=new DxfAraziParser{MetrePerBirim=1,Katmanlar=new HashSet<string>{"TOPO"}}.Parse(d);
    r.Arazi.Kaynak!.DosyaYolu="original.dwg";
    string path=Path.Combine(Path.GetTempPath(),"terrain-provenance-"+Guid.NewGuid().ToString("N")+".e4arazi.json");
    try {AraziGeometriServisi.Kaydet(r.Arazi,path);var read=AraziGeometriServisi.Ac(path);Check(read.Kaynak?.DosyaYolu=="original.dwg","CAD source replaced");Check(read.Kaynak!.Katmanlar.SequenceEqual(new[]{"TOPO"}),"layers lost");Near(read.Kaynak.MetrePerBirim,1);}
    finally {if(File.Exists(path))File.Delete(path);}
});
Test("Nonfinite scale is rejected",()=> {try {new DxfAraziParser{MetrePerBirim=double.NaN}.Parse(Doc());throw new Exception("NaN accepted");}catch(ArgumentOutOfRangeException) {}});
Test("CAD triangles retain the surveyed footprint and survive save/open",()=> {
    var d=Doc();
    var layer=new Layer("PROJEYÜZEY");
    d.Entities.Add(new Face3D(new Vector3(0,0,10),new Vector3(1,0,11),new Vector3(0,1,10)){Layer=layer});
    d.Entities.Add(new Face3D(new Vector3(4,0,15),new Vector3(5,0,15),new Vector3(4,1,15)){Layer=layer});
    d.Entities.Add(new Polyline2D(new[]{new Vector2(0,0),new Vector2(5,0)}){Elevation=12,Layer=new Layer("EĞRI_1M")});
    var suggested=new DxfAraziParser().Parse(d).Katmanlar.Where(k=>k.AraziKatmaniOnerisi).Select(k=>k.Ad).ToHashSet();
    Check(suggested.SetEquals(new[]{"PROJEYÜZEY"}),"native layer not selected");
    var model=new DxfAraziParser{Katmanlar=suggested}.Parse(d).Arazi;
    var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".e4arazi.json");
    try {
        AraziGeometriServisi.Kaydet(model,path);
        var saved=AraziGeometriServisi.Ac(path);
        var tin=new AraziYuzeyServisi().Olustur(saved);
        Check(saved.SemaSurum==3 && tin.Ucgenler.Count==2 && tin.Dugumler.Count==6,"CAD faces lost");
        Check(tin.KotSorgula(2.5,.25)==null,"gap filled by convex hull");
        Near(AraziTasarimServisi.Ozetle(tin).PlanAlanM2,1);
    } finally { if(File.Exists(path))File.Delete(path); }
});
Test("Overlapping source faces cannot count earthwork twice",()=> {
    var d=Doc();
    foreach(var z in new[]{10,11})
        d.Entities.Add(new Face3D(new Vector3(0,0,z),new Vector3(1,0,z),new Vector3(0,1,z)));
    try { new AraziYuzeyServisi().Olustur(new DxfAraziParser().Parse(d).Arazi); }
    catch(InvalidDataException ex) when(ex.Message.Contains("üst üste")) { return; }
    throw new Exception("stacked surfaces accepted");
});
Console.WriteLine($"Terrain import regression: {count}/{count}");
