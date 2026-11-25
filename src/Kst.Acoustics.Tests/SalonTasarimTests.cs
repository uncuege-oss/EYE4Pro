using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Salon.Models;
using Kst.Acoustics.Core.Salon.Services;

namespace Kst.Acoustics.Tests;

public static class SalonTasarimTests
{
    public static SalonGeometri Ornek()
    {
        var s=SalonGeometriServisi.YeniOlustur("Serbest salon testi");s.SerbestTasarim=true;s.SalonYuksekligiM=10;
        Ekle(s,SalonIslevi.Sinir,0,0,10,10);return s;
    }
    static SalonCizimOgesi Ekle(SalonGeometri s,SalonIslevi r,double x,double y,double w,double h)
    {
        var o=new SalonCizimOgesi{Islev=r,Ad=SalonTasarimServisi.Ad(r),Tip=SalonCizimTipi.Polyline,Kapali=true,Noktalar=new(){new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)},KatmanId=s.Katmanlar.FirstOrDefault(k=>k.Tip==SalonTasarimServisi.KatmanTipi(r))?.Id??""};o.KenarlariHazirla();s.CizimOgeleri.Add(o);return o;
    }
    static void Equal(double actual,double expected,double tolerance=.001){if(Math.Abs(actual-expected)>tolerance)throw new Exception($"Expected {expected}, got {actual}");}
    public static void KayitKoleksiyonKimliginiKorur()
    {
        var s=Ornek();var list=s.CizimOgeleri;string path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".e4salon.json");
        try{SalonGeometriServisi.Kaydet(s,path);if(!ReferenceEquals(list,s.CizimOgeleri))throw new Exception("Collection replaced");var loaded=SalonGeometriServisi.Ac(path);if(!loaded.SerbestTasarim||loaded.CizimOgeleri[0].Islev!=SalonIslevi.Sinir)throw new Exception("New model lost");}finally{File.Delete(path);}
    }
    public static void BoslukAlanVeHacmiKorur()
    {
        var s=Ornek();Ekle(s,SalonIslevi.Bosluk,3,3,4,4);var mesh=SalonTasarimMeshServisi.Uret(s);Equal(mesh.Hacim,840);Equal(SalonTasarimServisi.Alan(SalonTasarimServisi.Kabuk(s)),84);
        var floors=mesh.Yuzeyler.Where(p=>p.Category=="Zemin");Equal(floors.Sum(p=>Math.Abs(ClipperAdaptor.IsaretliAlan(p.Vertices.Select(v=>new Point2D(v.X,v.Y)).ToList()))),84);
    }
    public static void KirpilanKademeGercekHacim()
    {
        var s=Ornek();s.CizimOgeleri[0].Noktalar=new(){new(0,5),new(10,5),new(10,10),new(0,10)};
        var o=Ekle(s,SalonIslevi.Seyirci,0,0,10,10);o.KademeSayisi=2;o.KademeYuksekligiM=2;Equal(SalonTasarimMeshServisi.Uret(s).Hacim,400);
    }
    public static void CakisanBolgeAktarimiDurdurur()
    {
        var s=Ornek();Ekle(s,SalonIslevi.Sahne,2,2,3,3);Ekle(s,SalonIslevi.Sahne,2,2,3,3);
        if(!SalonTasarimDogrulama.Kontrol(s).Any(x=>x.Mesaj.Contains("üst üste")))throw new Exception("Overlap not reported");
        try{SalonTasarimMeshServisi.Uret(s);}catch(InvalidDataException){return;}throw new Exception("Overlap silently calculated");
    }
    public static void DuzTavanCarpismaKontrolu()
    {
        var s=Ornek();s.SalonYuksekligiM=3;var o=Ekle(s,SalonIslevi.Sahne,2,2,2,2);o.YukseklikM=4;
        if(!SalonTasarimDogrulama.Kontrol(s).Any(x=>x.NesneId==o.Id&&x.Mesaj.Contains("tavan")))throw new Exception("Ceiling collision missed");
    }
    public static void EgikTavanHacimIntegrali()
    {
        var s=Ornek();var roof=Ekle(s,SalonIslevi.Tavan,0,0,10,10);roof.KotM=5;roof.EgimX=.2;roof.EgimY=.4;Equal(SalonTasarimMeshServisi.Uret(s).Hacim,800);
    }
    public static void KarmaYaySiniriKayittaKorunur()
    {
        var s=Ornek();var o=s.CizimOgeleri[0];o.KenarKavisleri[2]=Math.Tan(Math.PI/8);var p=o.KenarNoktasi(2,.5);
        var restored=SalonGeometriServisi.KopyadanYukle(SalonGeometriServisi.KopyaJson(s)).CizimOgeleri[0];Equal(restored.KenarNoktasi(2,.5).Y,p.Y);if(restored.KapaliPoligon().Count<=4)throw new Exception("Curve flattened in model");
        var copy=SalonDonusumServisi.Kopyala(o);SalonDonusumServisi.Aynala(copy,new(0,0),new(0,1));Equal(copy.KenarKavisleri[2],-o.KenarKavisleri[2]);
    }
    public static void AciklikDuvariIzler()
    {
        var s=Ornek();var o=s.CizimOgeleri[0];var a=new DuvarAcikligi{MerkezX=5,MerkezY=0};s.Acikliklar.Add(a);SalonTasarimServisi.AcikligiBagla(s,a);SalonDonusumServisi.Tasi(o,3,4);SalonTasarimServisi.BaglariGuncelle(s);Equal(a.MerkezX,8);Equal(a.MerkezY,4);
        SalonDonusumServisi.Dondur(o,new(0,0),90);SalonTasarimServisi.BaglariGuncelle(s);Equal(a.MerkezX,-4);Equal(a.MerkezY,8);
    }
    public static void KoltuklarKoridorVeSiniraKirpilir()
    {
        var s=Ornek();var o=Ekle(s,SalonIslevi.Seyirci,-2,0,14,10);o.ZeminBicimi=SalonZeminBicimi.Duz;
        int before=SalonTasarimServisi.Koltuklar(s,o).Count;Ekle(s,SalonIslevi.Koridor,4,0,2,10);var seats=SalonTasarimServisi.Koltuklar(s,o);
        if(seats.Count>=before||seats.Count==0||seats.Any(p=>p.X<0||p.X>10||(p.X>4&&p.X<6)))throw new Exception("Seats exceed permitted footprint");
        o.YayliSiralar=true;o.SiraMerkezi=new(5,-1);if(SalonTasarimServisi.Koltuklar(s,o).Count==0)throw new Exception("Curved rows empty");
    }
    public static void DortCizgiSiniraBirlesir()
    {
        var s=Ornek();var first=s.CizimOgeleri[0];s.CizimOgeleri.Clear();foreach(var (a,b) in first.Segmentler())s.CizimOgeleri.Add(new(){Tip=SalonCizimTipi.Cizgi,Noktalar=new(){a,b}});
        SalonSinirDuzenleme.Bagla(s,s.CizimOgeleri.ToList());Equal(SalonTasarimMeshServisi.Uret(s).Hacim,1000);
    }
    public static void GorunurlukCiktiyiDegistirmez()
    {
        var s=Ornek();double before=SalonTasarimMeshServisi.Uret(s).Hacim;s.Katmanlar[0].Gorunur=false;Equal(SalonTasarimMeshServisi.Uret(s).Hacim,before);
        s.CizimOgeleri[0].CiktiyaDahil=false;Equal(SalonTasarimMeshServisi.Uret(s).Hacim,0);
    }
    public static void EskiDosyaDonusumuGeometriyiKorur()
    {
        var s=SalonParametrikServisi.Olustur(new SalonSablonu());double before=SalonHacimServisi.Hesapla(s);SalonTasarimServisi.Hazirla(s);Equal(SalonHacimServisi.Hesapla(s),before,.05);
        if(s.Sahne!=null||s.SeyirciSekciyonlari.Count>0||!s.CizimOgeleri.Any(o=>o.Islev==SalonIslevi.Seyirci))throw new Exception("Migration duplicated or lost objects");
    }
    static double YuzeyAlani(SalonTasarimMeshServisi.Sonuc mesh)
    {
        double area=0;
        foreach(var face in mesh.Yuzeyler)
        {
            double x=0,y=0,z=0;var vs=face.Vertices;
            for(int i=0;i<vs.Count;i++){var a=vs[i];var b=vs[(i+1)%vs.Count];x+=a.Y*b.Z-a.Z*b.Y;y+=a.Z*b.X-a.X*b.Z;z+=a.X*b.Y-a.Y*b.X;}
            area+=Math.Sqrt(x*x+y*y+z*z)/2;
        }
        return area;
    }
    public static void DuvaraBitisikSahneYuzeyleriTekildir()
    {
        var s=Ornek();var stage=Ekle(s,SalonIslevi.Sahne,0,0,10,5);stage.YukseklikM=2;
        var mesh=SalonTasarimMeshServisi.Uret(s);Equal(mesh.Hacim,900);Equal(YuzeyAlani(mesh),580);
    }
    public static void DuvaraBitisikKademelerKapaliKabukUretir()
    {
        var s=Ornek();var audience=Ekle(s,SalonIslevi.Seyirci,0,0,10,10);audience.KademeSayisi=2;audience.KademeYuksekligiM=2;
        var mesh=SalonTasarimMeshServisi.Uret(s);Equal(mesh.Hacim,900);Equal(YuzeyAlani(mesh),580);
    }
    public static void BalkonUzerindekiDuzSeyirciYuzeyTekrarlamaz()
    {
        var s=Ornek();var balcony=Ekle(s,SalonIslevi.Balkon,3,3,4,4);balcony.KotM=4;
        var audience=Ekle(s,SalonIslevi.Seyirci,3,3,4,4);audience.ZeminBicimi=SalonZeminBicimi.Duz;audience.UstNesneId=balcony.Id;
        var mesh=SalonTasarimMeshServisi.Uret(s);Equal(mesh.Hacim,995.2);Equal(YuzeyAlani(mesh),636.8);
    }
    public static void DuvaraBitisikCukurDuvariAsagiUzatir()
    {
        var s=Ornek();var pit=Ekle(s,SalonIslevi.Cukur,0,0,10,5);pit.YukseklikM=2;
        var mesh=SalonTasarimMeshServisi.Uret(s);Equal(mesh.Hacim,1100);Equal(YuzeyAlani(mesh),660);
    }
    public static void TavanKotuKirilimiDuseyYuzeyleKapanir()
    {
        var s=Ornek();Ekle(s,SalonIslevi.Tavan,0,0,10,5).KotM=8;
        var mesh=SalonTasarimMeshServisi.Uret(s);Equal(mesh.Hacim,900);Equal(YuzeyAlani(mesh),580);
    }
    public static void IcBoslukluBalkonDonusumVeKayittaKorunur()
    {
        var s=Ornek();var outer=Ekle(s,SalonIslevi.Balkon,1,1,8,8);outer.KotM=4;var cut=Ekle(s,SalonIslevi.Balkon,3,3,4,4);cut.KotM=4;
        SalonSinirDuzenleme.Birlestir(s,new(){outer,cut},true);var balcony=s.CizimOgeleri.Single(o=>o.Islev==SalonIslevi.Balkon);
        if(balcony.IcBosluklar.Count!=1)throw new Exception("Balcony hole lost");
        var mesh=SalonTasarimMeshServisi.Uret(s);Equal(mesh.Hacim,985.6);Equal(YuzeyAlani(mesh),710.4);
        SalonDonusumServisi.Dondur(balcony,new(5,5),90);var loaded=SalonGeometriServisi.KopyadanYukle(SalonGeometriServisi.KopyaJson(s));Equal(SalonTasarimMeshServisi.Uret(loaded).Hacim,985.6);
    }
    public static void GeriAlDosyaKimliginiVeTemizDurumuKorur()
    {
        var s=Ornek();var history=new SalonDuzenlemeGecmisi();history.Sifirla(s);history.Degisecek(s);s.SalonAdi="Yeni ad";
        s.DosyaYolu="Yeni.e4salon.json";s.GeometriRevizyon=7;history.Kaydedildi(s);
        s=history.Geri(s)!;if(s.DosyaYolu!="Yeni.e4salon.json"||s.GeometriRevizyon!=7||!history.Kirli(s))throw new Exception("Undo rewound file identity");
        s=history.Ileri(s)!;if(history.Kirli(s))throw new Exception("Redo to saved content marked dirty");
    }
    public static void YayKenariBolununceAciklikYerindeKalir()
    {
        var s=Ornek();var o=s.CizimOgeleri[0];o.KenarKavisleri[0]=.4;var p=o.KenarNoktasi(0,.7);var ac=new DuvarAcikligi{MerkezX=p.X,MerkezY=p.Y};s.Acikliklar.Add(ac);SalonTasarimServisi.AcikligiBagla(s,ac);
        SalonSinirDuzenleme.KenarBol(s,o,0,.5);Equal(ac.MerkezX,p.X,.002);Equal(ac.MerkezY,p.Y,.002);Equal(o.KenarNoktasi(1,.4).X,p.X);Equal(o.KenarNoktasi(1,.4).Y,p.Y);
    }
    public static void DaireselSalonaAciklikBaglanir()
    {
        var s=Ornek();var o=s.CizimOgeleri[0];o.Tip=SalonCizimTipi.Daire;o.Noktalar=new(){new(5,5)};o.YariCapM=5;o.OrtakSiniraDonustur();
        var p=o.KenarNoktasi(0,.5);var ac=new DuvarAcikligi{MerkezX=p.X,MerkezY=p.Y,GenislikM=2,YukseklikM=2};s.Acikliklar.Add(ac);SalonTasarimServisi.AcikligiBagla(s,ac);
        if(ac.DuvarId!=o.Id)throw new Exception("Circle opening unbound");
        var mesh=SalonTasarimMeshServisi.Uret(s);var opening=mesh with {Yuzeyler=mesh.Yuzeyler.Where(f=>f.Category=="Kapı").ToList()};Equal(YuzeyAlani(opening),4,.12);
    }
    public static void AciklikTasmasiVeCakismasiRaporlanir()
    {
        var s=Ornek();var a=new DuvarAcikligi{MerkezX=.2,MerkezY=0,GenislikM=1};s.Acikliklar.Add(a);SalonTasarimServisi.AcikligiBagla(s,a);
        if(!SalonTasarimDogrulama.Kontrol(s).Any(i=>i.Mesaj.Contains("kenar ucunu")))throw new Exception("Opening overflow missed");
        a.KenarOrani=.5;var b=new DuvarAcikligi{MerkezX=5,MerkezY=0};s.Acikliklar.Add(b);SalonTasarimServisi.AcikligiBagla(s,b);
        if(!SalonTasarimDogrulama.Kontrol(s).Any(i=>i.Mesaj.Contains("açıklıkla çakışıyor")))throw new Exception("Opening overlap missed");
    }
    public static void BoslukluCukurIcBolmeYuzeyiUretmez()
    {
        var s=Ornek();var pit=Ekle(s,SalonIslevi.Cukur,1,1,8,8);pit.YukseklikM=2;
        pit.IcBosluklar.Add(new(){new(3,3),new(7,3),new(7,7),new(3,7)});
        var mesh=SalonTasarimMeshServisi.Uret(s);Equal(mesh.Hacim,1096);Equal(YuzeyAlani(mesh),696);
    }
    public static void FarkliDerinlikteCakisanCukurlarEngellenir()
    {
        var s=Ornek();Ekle(s,SalonIslevi.Cukur,1,1,4,4).YukseklikM=1;Ekle(s,SalonIslevi.Cukur,2,2,4,4).YukseklikM=2;
        if(!SalonTasarimDogrulama.Kontrol(s).Any(i=>i.Mesaj.Contains("üst üste")))throw new Exception("Pit volume counted twice");
    }
}
