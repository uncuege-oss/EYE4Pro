using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Bina.Services;
using Kst.Acoustics.Core.Common.Geometry;
var tests = new (string,Action)[] {
("Çatı taraması oda hacmine dönüşmez; kaynak adaylar korunur",()=>{
 var k=Kat("Çatı Altı ve Teras","Oda 1");k.HamMetinler.Add((new(2,2),"KENETLİ SAÇ ÇATI ÖRTÜSÜ 0.7mm"));k.HamMetinler.Add((new(3,3),"KLİMA TERASI"));
 var result=MimariGeometriKaliteServisi.Uygula(k);Check(!k.BloklariGoster && k.Odalar.Count==1 && result.Any(t=>t.Contains("3D")),"Çatı hacmi kapatılmadı veya aday silindi.");
}),
("Gerçek çatı katı odası gizlenmez",()=>{
 var k=Kat("Çatı Katı","Yatak Odası");k.HamMetinler.Add((new(2,2),"YATAK ODASI"));
 MimariGeometriKaliteServisi.Uygula(k);Check(k.BloklariGoster,"Gerçek mahal gizlendi.");
}),
("Birleşmiş iki farklı mahal uyarılır",()=>{
 var k=Kat("Zemin Kat","SALON");k.HamMetinler.Add((new(1,1),"SALON"));k.HamMetinler.Add((new(4,4),"MUTFAK"));
 var result=MimariGeometriKaliteServisi.Uygula(k);Check(k.Odalar.Count==1 && result.Any(t=>t.Contains("birden fazla mahal")),"Birleşmiş oda işaretlenmedi.");
}),
("Zengin etiketli planda adsız adaylar kontrol ister",()=>{
 var k=Kat("Zemin Kat","Oda 1");for(int i=0;i<5;i++)k.HamMetinler.Add((new(20+i,20),"SALON"));
 var result=MimariGeometriKaliteServisi.Uygula(k);Check(k.BloklariGoster && result.Any(t=>t.Contains("mahal etiketi bulunamadı")),"Adsız oda kontrolü eksik.");
}),
("Geçersiz alan değiştirilmeden raporlanır",()=>{
 var k=Kat("1. Kat","OFİS");k.Odalar[0].AlanM2=500;
 var result=MimariGeometriKaliteServisi.Uygula(k);Check(k.Odalar[0].AlanM2==500 && result.Any(t=>t.Contains("alan/sınır")),"Alan tutarsızlığı raporlanmadı.");
}),
("Etiketsiz normal kat otomatik gizlenmez",()=>{
 var k=Kat("Zemin Kat","Oda 1");MimariGeometriKaliteServisi.Uygula(k);Check(k.BloklariGoster,"Normal kat gizlendi.");
})};
int failed=0;foreach(var (name,test) in tests){try{test();Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
Console.WriteLine($"{tests.Length-failed}/{tests.Length} geçti.");return failed==0?0:1;
static BinaKat Kat(string name,string room){var k=new BinaKat{Ad=name};k.Odalar.Add(new(){Ad=room,AlanM2=25,Poligon=[new(0,0),new(5,0),new(5,5),new(0,5)]});return k;}
static void Check(bool b,string m){if(!b)throw new Exception(m);}
