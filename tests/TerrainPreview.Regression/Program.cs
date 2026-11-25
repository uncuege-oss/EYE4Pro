using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Terrain.Models;
using Kst.Acoustics.Core.Terrain.Services;

if (args.Length > 0)
{
    var source = AraziGeometriServisi.Ac(args[0]);
    var tin = new AraziYuzeyServisi().Olustur(source);
    var index = new AraziKotIndeksi(tin);
    double cx = (tin.Dugumler.Min(p=>p.X)+tin.Dugumler.Max(p=>p.X))/2;
    double cy = (tin.Dugumler.Min(p=>p.Y)+tin.Dugumler.Max(p=>p.Y))/2;
    var centers = tin.Ucgenler.Select(t=>new Point2D((tin.Dugumler[t.A].X+tin.Dugumler[t.B].X+tin.Dugumler[t.C].X)/3,
        (tin.Dugumler[t.A].Y+tin.Dugumler[t.B].Y+tin.Dugumler[t.C].Y)/3)).OrderBy(p=>Math.Abs(p.X-cx)+Math.Abs(p.Y-cy));
    var center = centers.First(p=>new[]{(-20,-20),(20,-20),(20,20),(-20,20)}.All(d=>index.KotSorgula(p.X+d.Item1,p.Y+d.Item2)!=null));
    var pad = new TasarimBolgesi { Ad="Bellek içi önizleme kontrolü", HedefKotM=index.KotSorgula(center.X,center.Y)!.Value-2,
        Poligon=[new(center.X-20,center.Y-20),new(center.X+20,center.Y-20),new(center.X+20,center.Y+20),new(center.X-20,center.Y+20)],
        KenarGecisi=TasarimKenarTipi.Sev,YatayDuseyOrani=1.5,SevAzamiMesafeM=50,
        KenarAyarlari=[new(){KenarIndex=1,Tip=TasarimKenarTipi.DikKesim}] };
    var center2=centers.First(p=>(Math.Abs(p.X-center.X)>70||Math.Abs(p.Y-center.Y)>70) &&
        new[]{(-20,-20),(20,-20),(20,20),(-20,20)}.All(d=>index.KotSorgula(p.X+d.Item1,p.Y+d.Item2)!=null));
    var pad2=new TasarimBolgesi { Ad="Bellek içi dik dolgu kontrolü", HedefKotM=index.KotSorgula(center2.X,center2.Y)!.Value+2,
        Poligon=[new(center2.X-20,center2.Y-20),new(center2.X+20,center2.Y-20),new(center2.X+20,center2.Y+20),new(center2.X-20,center2.Y+20)] };
    var sw=System.Diagnostics.Stopwatch.StartNew();
    double step=args.Length>1?double.Parse(args[1],System.Globalization.CultureInfo.InvariantCulture):2;
    var preview=AraziTasarimOnizlemeServisi.Olustur(tin,new(){Bolgeler=[pad,pad2]},step);
    Console.WriteLine($"Actual preview: {preview.Yuzeyler.Count} faces, {sw.ElapsedMilliseconds} ms, interval {preview.OrneklemeAraligiM} m; source design count {source.Tasarim.Bolgeler.Count}");
    Console.WriteLine(string.Join("\n",preview.Uyarilar));
    if(source.Tasarim.Bolgeler.Count!=0)throw new Exception("Measured source design was mutated");
    return;
}

int tests = 0;
void Test(string name, Action action) { action(); Console.WriteLine("PASS " + name); tests++; }
void Assert(bool ok, string message) { if (!ok) throw new Exception(message); }
void Near(double a, double b, double eps = 1e-5) => Assert(Math.Abs(a - b) <= eps, $"Expected {b}, got {a}");
AraziYuzeyi Flat(double x = 0, double y = 0) => new AraziYuzeyServisi().Ucgenle(new[] {
    new AraziDugum(x-20,y-20,100), new AraziDugum(x+20,y-20,100), new AraziDugum(x+20,y+20,100), new AraziDugum(x-20,y+20,100) });
TasarimBolgesi Pad(double h = 90, double x = 0, double y = 0) => new() { Ad = "Platform", HedefKotM = h,
    Poligon = [new(x-5,y-5),new(x+5,y-5),new(x+5,y+5),new(x-5,y+5)] };
TasarimYuzeyi Design(params TasarimBolgesi[] regions) => new() { Bolgeler = regions.ToList() };
double Area(ProjectSurface s)
{
    var p = s.Vertices; double a = 0;
    for (int i = 1; i + 1 < p.Count; i++) a += (p[i].X - p[0].X) * (p[i+1].Y-p[0].Y) - (p[i+1].X-p[0].X)*(p[i].Y-p[0].Y);
    return Math.Abs(a)/2;
}
bool Ground(ProjectSurface s) => s.Category == Arazi3DGorunumServisi.KategoriArazi;
bool Wall(ProjectSurface s) => s.Name.EndsWith("— dik kesim");

Test("Empty design preserves original triangles exactly", () => {
    var source = Flat(); var p = AraziTasarimOnizlemeServisi.Olustur(source, Design());
    Assert(p.Yuzeyler.Count == 2, "Unchanged TIN was resampled"); Near(p.Yuzeyler.Sum(Area), 1600);
    Assert(p.Yuzeyler.All(s => s.Vertices.All(v => v.Z == 100)), "Existing elevation changed");
});
Test("Cut pad removes old ground and creates vertical faces", () => {
    var pad = Pad(); var p = AraziTasarimOnizlemeServisi.Olustur(Flat(), Design(pad), 2);
    Near(p.Yuzeyler.Where(s => !Wall(s)).Sum(Area), 1600, .02);
    Near(p.Yuzeyler.Where(s => !Ground(s) && !Wall(s)).Sum(Area), 100, .01);
    Assert(p.Yuzeyler.Where(Ground).All(s => s.Vertices.All(v => Math.Abs(v.Z-100)<1e-6)), "DikKesim turned into sloped ground");
    Assert(p.Yuzeyler.Where(Wall).All(s => Math.Abs(s.Vertices.Min(v=>v.Z)-90)<1e-6 && Math.Abs(s.Vertices.Max(v=>v.Z)-100)<1e-6), "Vertical cut limits incorrect");
    Assert(p.Yuzeyler.Any(Wall), "No vertical cut faces");
    foreach(var face in p.Yuzeyler.Where(Ground)) {
        var center = new Point2D(face.Vertices.Average(v=>v.X), face.Vertices.Average(v=>v.Y));
        Assert(!PoligonYardimci.IsPointInside(center,pad.Poligon), "Old terrain occludes platform");
    }
    Assert(p.Yuzeyler.Select(s=>s.Id).Distinct().Count()==p.Yuzeyler.Count,"Duplicate surface IDs");
});
Test("Fill pad has upright retaining face to existing grade", () => {
    var p = AraziTasarimOnizlemeServisi.Olustur(Flat(), Design(Pad(108)), 3);
    Assert(p.Yuzeyler.Where(Wall).All(s=>Math.Abs(s.Vertices.Min(v=>v.Z)-100)<1e-6&&Math.Abs(s.Vertices.Max(v=>v.Z)-108)<1e-6),"Fill wall incorrect");
    Near(p.Yuzeyler.Where(s=>!Wall(s)).Sum(Area),1600,.02);
});
Test("Slope preview changes outer terrain and does not draw vertical cut walls", () => {
    var pad = Pad(); pad.KenarGecisi=TasarimKenarTipi.Sev;pad.YatayDuseyOrani=1;pad.SevAzamiMesafeM=20;
    var p=AraziTasarimOnizlemeServisi.Olustur(Flat(),Design(pad),2);
    Assert(!p.Yuzeyler.Any(Wall),"Slope got a vertical cut wall");
    Assert(p.Yuzeyler.Where(Ground).SelectMany(s=>s.Vertices).Any(v=>v.Z>90.1&&v.Z<99.9),"No actual sloping surface");
    Assert(p.Yuzeyler.SelectMany(s=>s.Vertices).All(v=>v.Z>=90-1e-5&&v.Z<=100+1e-5),"Slope overshoots elevations");
    Near(p.Yuzeyler.Where(s=>!Wall(s)).Sum(Area),1600,.02);
});
Test("Mixed edges produce cut walls only on chosen side", () => {
    var pad=Pad();pad.KenarGecisi=TasarimKenarTipi.Sev;pad.YatayDuseyOrani=1;pad.SevAzamiMesafeM=20;
    pad.KenarAyarlari.Add(new(){KenarIndex=1,Tip=TasarimKenarTipi.DikKesim});
    var p=AraziTasarimOnizlemeServisi.Olustur(Flat(),Design(pad),2);
    Assert(p.Yuzeyler.Any(Wall),"Mixed edge wall missing");
    Assert(p.Yuzeyler.Where(Wall).All(s=>s.Vertices.All(v=>Math.Abs(v.X-5)<1e-7)),"Wall on slope edge");
});
Test("Concave pad footprint is exactly retained", () => {
    var pad=Pad();pad.Poligon=[new(-5,-5),new(5,-5),new(5,0),new(0,0),new(0,5),new(-5,5)];
    var p=AraziTasarimOnizlemeServisi.Olustur(Flat(),Design(pad),2);
    Near(p.Yuzeyler.Where(s=>!Ground(s)&&!Wall(s)).Sum(Area),75,.01);
    Near(p.Yuzeyler.Where(s=>!Wall(s)).Sum(Area),1600,.02);
});
Test("A pad enclosed inside one source triangle leaves a proper hole", () => {
    var source=new AraziYuzeyi { Dugumler=[new(0,0,100),new(30,0,100),new(0,30,100)], Ucgenler=[new(0,1,2)] };
    var pad=new TasarimBolgesi {Ad="İç platform",HedefKotM=90,Poligon=[new(4,4),new(8,4),new(8,8),new(4,8)]};
    var p=AraziTasarimOnizlemeServisi.Olustur(source,Design(pad),2);
    Near(p.Yuzeyler.Where(s=>!Wall(s)).Sum(Area),450,.02);
    Near(p.Yuzeyler.Where(s=>!Ground(s)&&!Wall(s)).Sum(Area),16,.01);
    foreach(var face in p.Yuzeyler.Where(Ground)) {
        var center=new Point2D(face.Vertices.Average(v=>v.X),face.Vertices.Average(v=>v.Y));
        Assert(!PoligonYardimci.IsPointInside(center,pad.Poligon),"Hole was filled with old ground");
    }
});
Test("Touching platforms meet at their own heights, not original ground", () => {
    var left=Pad(95,-5); var right=Pad(105,5);
    var p=AraziTasarimOnizlemeServisi.Olustur(Flat(),Design(left,right),2);
    var shared=p.Yuzeyler.Where(Wall).Where(s=>s.Vertices.All(v=>Math.Abs(v.X)<1e-7)).ToList();
    Assert(shared.Count>0,"Shared vertical wall missing");
    Assert(shared.All(s=>Math.Abs(s.Vertices.Min(v=>v.Z)-95)<1e-5&&Math.Abs(s.Vertices.Max(v=>v.Z)-105)<1e-5),"Shared wall used natural 100 m instead of adjacent pad");
    Near(p.Yuzeyler.Where(s=>!Wall(s)).Sum(Area),1600,.02);
});
Test("Large survey coordinates keep pad and ground areas",()=> {
    var p=AraziTasarimOnizlemeServisi.Olustur(Flat(628000,4110000),Design(Pad(90,628000,4110000)),2);
    Near(p.Yuzeyler.Where(s=>!Wall(s)).Sum(Area),1600,.02);
    Assert(p.Yuzeyler.All(s=>s.Vertices.All(v=>double.IsFinite(v.Z))),"Nonfinite height");
});
Test("Invalid resolution is rejected",()=> {
    foreach(double step in new[]{0d,-1,double.NaN,double.PositiveInfinity}) {
        bool caught=false;try{AraziTasarimOnizlemeServisi.Olustur(Flat(),Design(Pad()),step);}catch(ArgumentOutOfRangeException){caught=true;}
        Assert(caught,"Invalid resolution accepted");
    }
});
Test("Cut-fill heatmap includes the slope footprint outside a pad",()=> {
    var pad=Pad();pad.KenarGecisi=TasarimKenarTipi.Sev;pad.YatayDuseyOrani=1;pad.SevAzamiMesafeM=20;
    var source=Flat();var map=Arazi3DGorunumServisi.HafriyatRenkHaritasi(source,Design(pad));
    Assert(map.Count==2,"Triangle centers lie outside the pad but inside its cut slope; colors missing");
    Assert(map.Values.All(v=>v>.5),"Cut slope should use cut color");
});
Test("Platform outside TIN is clipped instead of inventing ground",()=> {
    var pad=Pad(90,20,0); var p=AraziTasarimOnizlemeServisi.Olustur(Flat(),Design(pad),2);
    Assert(p.Yuzeyler.SelectMany(s=>s.Vertices).All(v=>v.X<=20+1e-6),"Preview extends beyond measured TIN");
    Assert(p.Uyarilar.Any(s=>s.Contains("kapsamı dışında")),"Outside coverage not explained");
    Near(p.Yuzeyler.Where(s=>!Wall(s)).Sum(Area),1600,.02);
});
Console.WriteLine($"Terrain preview regression: {tests}/{tests}");
