using System.Text.Json;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Terrain.Models;
using Kst.Acoustics.Core.Terrain.Services;

int passed=0;
void Test(string name,Action run){run();Console.WriteLine("PASS "+name);passed++;}
void Check(bool b,string message){if(!b)throw new Exception(message);}
void Near(double a,double b,double tolerance=1e-7){if(Math.Abs(a-b)>tolerance)throw new Exception($"Expected {b}, actual {a}");}
void Reject(Action action){try{action();}catch(ArgumentException){return;}throw new Exception("Expected validation rejection");}
List<Point2D> Square(double x,double y,double w=10,double h=10)=>[new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)];
AraziYuzeyi Flat(double z)=>new AraziYuzeyServisi().Ucgenle([new(-30,-30,z),new(40,-30,z),new(40,40,z),new(-30,40,z)]);
TasarimBolgesi Pad(string name,double z,double x=0,double y=0)=>new(){Ad=name,HedefKotM=z,Poligon=Square(x,y)};
TasarimYuzeyi Design(params TasarimBolgesi[] pads)=>new(){Bolgeler=pads.ToList()};
TasarimBolgesi SouthSlope(double z){var b=Pad("Güney şevi",z);b.YatayDuseyOrani=2;b.KenarAyarlari.Add(new(){KenarIndex=0,Tip=TasarimKenarTipi.Sev});return b;}

Test("Multiple pads use independent levels and disjoint per-region volumes",()=>{
 var a=Pad("Kazı",8);var b=Pad("Dolgu",11,15);var r=new HafriyatHesabiServisi{GridAraligiM=.5}.Hesapla(Flat(10),Design(a,b));
 Near(r.YarmaHacmiM3,200);Near(r.DolguHacmiM3,100);Check(r.BolgeSonuclari.Count==2,"missing regions");
 Near(r.BolgeSonuclari.Sum(s=>s.YarmaHacmiM3),r.YarmaHacmiM3);
});
Test("Vertical edge preserves terrain immediately outside",()=>{
 var s=new AraziTesviyeServisi(Flat(10),Design(Pad("Dik",8)));Near(s.KotSorgula(5,5)!.Value,8);Near(s.KotSorgula(5,-.01)!.Value,10);
 Check(s.Sorgula(5,-.01) is null,"vertical cut leaked");
});
Test("Mixed edge cut slope reaches first daylight at 4m and leaves other edges vertical",()=>{
 var s=new AraziTesviyeServisi(Flat(10),Design(SouthSlope(8)));
 Near(s.KotSorgula(5,-1)!.Value,8.5);Near(s.KotSorgula(5,-3)!.Value,9.5);Near(s.KotSorgula(5,-4)!.Value,10);
 Check(s.Sorgula(5,-5) is null,"after daylight modified");Check(s.Sorgula(-1,5) is null,"vertical edge modified");
 Check(s.Sorgula(-1,-1) is null,"mixed corner must prefer vertical edge");Check(s.Uyarilar.Count==0,"unexpected slope warning");
});
Test("Side slope volume includes transition outside platform",()=>{
 var r=new HafriyatHesabiServisi{GridAraligiM=.25}.Hesapla(Flat(10),Design(SouthSlope(8)));
 Near(r.YarmaHacmiM3,240,1e-6);Near(r.DolguHacmiM3,0);Check(r.SevHucreSayisi>0,"slope cells missing");
});
Test("Fill slope descends outward and its volume is included",()=>{
 var b=SouthSlope(12);var s=new AraziTesviyeServisi(Flat(10),Design(b));Near(s.KotSorgula(5,-1)!.Value,11.5);
 var r=new HafriyatHesabiServisi{GridAraligiM=.25}.Hesapla(Flat(10),Design(b));Near(r.DolguHacmiM3,240,1e-6);
});
Test("All sloped edges create rounded corner envelope with analytic volume",()=>{
 var b=Pad("Çevre",8);b.KenarGecisi=TasarimKenarTipi.Sev;b.YatayDuseyOrani=2;
 var r=new HafriyatHesabiServisi{GridAraligiM=.2}.Hesapla(Flat(10),Design(b));Near(r.YarmaHacmiM3,360+32*Math.PI/3,.08);
});
Test("First daylight is exact through narrow valley and does not restart beyond hill",()=>{
 var ns=new List<AraziDugum>();foreach(var (y,z) in new[]{(-20.0,15.0),(-3.0,15.0),(-2.0,7.0),(-1.0,10.0),(0.0,10.0),(20.0,10.0)}){ns.Add(new(-20,y,z));ns.Add(new(20,y,z));}
 var tin=new AraziYuzeyServisi().Ucgenle(ns);var s=new AraziTesviyeServisi(tin,Design(SouthSlope(8)));
 Near(s.KotSorgula(5,-1)!.Value,8.5);Check(s.Sorgula(5,-3) is null,"slope reappeared after first crossing");
});
Test("Platform always wins over neighboring slope",()=>{
 var a=SouthSlope(8);var b=Pad("Alt platform",9,0,-15);var s=new AraziTesviyeServisi(Flat(10),Design(a,b));
 Near(s.Sorgula(5,-7)!.HedefKotM,9);Check(s.Sorgula(5,-7)!.Bolge.Id==b.Id,"platform precedence");
});
Test("Platform overlap rejected, touching boundaries allowed",()=>{
 Reject(()=>new AraziTesviyeServisi(Flat(10),Design(Pad("A",8),Pad("B",9,5))));
 var same=Pad("same",9);same.Poligon=Square(0,0);Reject(()=>new AraziTesviyeServisi(Flat(10),Design(Pad("A",8),same)));
 var touch=new AraziTesviyeServisi(Flat(10),Design(Pad("A",8),Pad("B",9,10)));Near(touch.KotSorgula(15,5)!.Value,9);
});
Test("Slope overlap has deterministic first-region priority and diagnostic",()=>{
 var a=Pad("A",8);a.KenarGecisi=TasarimKenarTipi.Sev;a.YatayDuseyOrani=2;
 var b=Pad("B",7,13);b.KenarGecisi=TasarimKenarTipi.Sev;b.YatayDuseyOrani=2;
 var s=new AraziTesviyeServisi(Flat(10),Design(a,b));var sample=s.Sorgula(11,5)!;Check(sample.Bolge.Id==a.Id,"priority mismatch");
 Check(s.Uyarilar.Any(w=>w.Contains("örtüşüyor")),"missing overlap diagnostic");
});
Test("Unclosed slope at maximum distance is reported",()=>{
 var b=SouthSlope(0);b.SevAzamiMesafeM=2;var s=new AraziTesviyeServisi(Flat(10),Design(b));
 Check(s.Uyarilar.Any(w=>w.Contains("azami")),"silent distance truncation");
 var r=new HafriyatHesabiServisi{GridAraligiM=1}.Hesapla(Flat(10),Design(b));Check(r.Uyarilar.Count>0,"report lost warnings");
});
Test("Slope terrain coverage truncation is reported",()=>{
 var b=SouthSlope(-100);b.SevAzamiMesafeM=100;var s=new AraziTesviyeServisi(Flat(10),Design(b));Check(s.Uyarilar.Any(w=>w.Contains("kapsamı")),"silent coverage truncation");
});
Test("Invalid ratios duplicate overrides and malformed polygons rejected",()=>{
 var b=SouthSlope(8);b.YatayDuseyOrani=0;Reject(()=>new AraziTesviyeServisi(Flat(10),Design(b)));
 b=SouthSlope(8);b.KenarAyarlari.Add(new(){KenarIndex=0,Tip=TasarimKenarTipi.DikKesim});Reject(()=>new AraziTesviyeServisi(Flat(10),Design(b)));
 b=Pad("Bowtie",8);b.Poligon=[new(0,0),new(10,10),new(0,10),new(10,0)];Reject(()=>new AraziTesviyeServisi(Flat(10),Design(b)));
});
Test("Prepared evaluator snapshots input design",()=>{
 var b=SouthSlope(8);var s=new AraziTesviyeServisi(Flat(10),Design(b));b.HedefKotM=500;b.Poligon.Clear();Near(s.KotSorgula(5,-1)!.Value,8.5);
});
Test("Ray profile respects large map coordinate origin and exact terrain slope",()=>{
 var ns=new[]{new AraziDugum(600000,4100000,10),new AraziDugum(600010,4100000,12),new AraziDugum(600010,4100010,14),new AraziDugum(600000,4100010,12)};
 var index=new AraziKotIndeksi(new AraziYuzeyServisi().Ucgenle(ns));var p=index.ProfilParcalari(600001,4100001,600009,4100009);Check(p.Count>=1,"profile empty");
 Near(p[0].BasKotM,10.4);Near(p[^1].BitKotM,13.6);
});
Test("Schema 2 preserves multi-region per-edge design; raw schema1 unchanged",()=>{
 var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".e4arazi.json");
 try{
 var model=new AraziModeli{SemaSurum=1,Noktalar=[new(0,0,10),new(10,0,10),new(0,10,10)],Tasarim=Design(SouthSlope(8),Pad("B",9,15))};
 model.Kaynak=new(){DosyaYolu="source.dwg",MetrePerBirim=1};AraziGeometriServisi.Kaydet(model,path);Check(model.SemaSurum==2,"grading save must require v2");
 var loaded=AraziGeometriServisi.Ac(path);Check(loaded.Tasarim.Bolgeler.Count==2,"lost regions");Check(loaded.Tasarim.Bolgeler[0].KenarTipi(0)==TasarimKenarTipi.Sev,"lost override");Check(loaded.Kaynak!.DosyaYolu=="source.dwg","lost source");
 var raw=new AraziModeli{SemaSurum=1};AraziGeometriServisi.Kaydet(raw,path);Check(AraziGeometriServisi.Ac(path).SemaSurum==1,"raw model upgraded unnecessarily");
 }finally{if(File.Exists(path))File.Delete(path);if(File.Exists(path+".bak"))File.Delete(path+".bak");}
});
Console.WriteLine($"Terrain grading regression: {passed}/{passed}");
