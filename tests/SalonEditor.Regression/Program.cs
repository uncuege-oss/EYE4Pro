using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Kst.Acoustics.App.Salon.Views;
using Kst.Acoustics.App.Salon.ViewModels;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Salon.Models;
using Kst.Acoustics.Core.Salon.Services;

internal static class Program
{
    static int passed,failed;
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Hidden)!.GetValue(o)!;
    static object? Call(object o,string method,params object[] args)=>o.GetType().GetMethod(method,Hidden)!.Invoke(o,args);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Run(string name,Action action){try{action();passed++;Console.WriteLine("PASS "+name);}catch(Exception ex){failed++;Console.WriteLine("FAIL "+name+": "+(ex.InnerException??ex));}}
    [STAThread]
    public static int Main()
    {
        foreach(var type in new[]{typeof(Kst.Acoustics.Tests.SalonGeometriTests),typeof(Kst.Acoustics.Tests.SalonTasarimTests)})
            foreach(var m in type.GetMethods(BindingFlags.Public|BindingFlags.Static).Where(m=>m.ReturnType==typeof(void)&&m.GetParameters().Length==0))Run(m.Name,()=>m.Invoke(null,null));
        AppBuilder.Configure<Kst.Acoustics.App.App>().UsePlatformDetect().SetupWithoutStarting();
        Run("Enter closes shared contour",()=>{
            var canvas=new SalonCizimCanvas{Salon=SalonGeometriServisi.YeniOlustur(),Arac=SalonAraci.Polyline};canvas.Salon.SerbestTasarim=true;
            Field<List<Point2D>>(canvas,"_cizimNoktalar").AddRange(new[]{new Point2D(0,0),new Point2D(10,0),new Point2D(10,10),new Point2D(0,10)});
            Call(canvas,"OnKeyDown",new KeyEventArgs{Key=Key.Enter});
            Check(canvas.Salon.CizimOgeleri.Count==1&&canvas.Salon.CizimOgeleri[0].Kapali,"Enter did not close contour");
        });
        Run("New document clears pending geometry",()=>{
            var canvas=new SalonCizimCanvas{Salon=SalonGeometriServisi.YeniOlustur(),Arac=SalonAraci.Polyline};Field<List<Point2D>>(canvas,"_cizimNoktalar").Add(new(2,3));
            canvas.Salon=SalonGeometriServisi.YeniOlustur();Check(Field<List<Point2D>>(canvas,"_cizimNoktalar").Count==0,"Previous drawing leaked into new document");
        });
        Run("Signed angle numeric entry",()=>{
            var method=typeof(SalonCizimCanvas).GetMethod("GirisiAyristir",BindingFlags.Static|BindingFlags.NonPublic)!;
            object?[] input={"5<-30",0d,null};Check((bool)method.Invoke(null,input)!,"Signed angle rejected");Check((double)input[1]! == 5&&(double)input[2]! == -30,"Wrong signed angle");
        });
        Run("Copy keeps balcony children and opening hosts",()=>{
            var s=Kst.Acoustics.Tests.SalonTasarimTests.Ornek();var boundary=s.CizimOgeleri[0];var ac=new DuvarAcikligi{MerkezX=5,MerkezY=0};s.Acikliklar.Add(ac);SalonTasarimServisi.AcikligiBagla(s,ac);
            var child=SalonDonusumServisi.Kopyala(boundary);child.Islev=SalonIslevi.Seyirci;child.UstNesneId=boundary.Id;s.CizimOgeleri.Add(child);
            var canvas=new SalonCizimCanvas{Salon=s};canvas.Sec(boundary);canvas.SecimiTasi(20,0,true);SalonTasarimServisi.BaglariGuncelle(s);
            Check(s.CizimOgeleri.Count==4&&s.Acikliklar.Count==2,"Group copy incomplete");var copy=s.CizimOgeleri.First(o=>o.Id!=boundary.Id&&o.Islev==SalonIslevi.Sinir);
            Check(s.CizimOgeleri.Any(o=>o.UstNesneId==copy.Id),"Child still linked to old parent");Check(s.Acikliklar.Any(a=>a.DuvarId==copy.Id&&Math.Abs(a.MerkezX-25)<.001),"Opening did not follow copy");
        });
        Run("Numeric negative rotation honors copy mode",()=>{
            var s=Kst.Acoustics.Tests.SalonTasarimTests.Ornek();var original=s.CizimOgeleri[0];var canvas=new SalonCizimCanvas{Salon=s,Arac=SalonAraci.Dondur};canvas.Sec(original);
            Field<List<Point2D>>(canvas,"_cizimNoktalar").Add(new(0,0));typeof(SalonCizimCanvas).GetField("_sayiGiris",Hidden)!.SetValue(canvas,"-90");Call(canvas,"SayiyiUygula",true);
            Check(s.CizimOgeleri.Count==2&&original.Noktalar[1]==new Point2D(10,0),"Copy changed original");var p=s.CizimOgeleri[1].Noktalar[1];Check(Math.Abs(p.X)<.001&&Math.Abs(p.Y+10)<.001,"Negative numeric angle not applied");
        });
        Run("Offset preserves region semantics",()=>{
            var s=Kst.Acoustics.Tests.SalonTasarimTests.Ornek();var o=s.CizimOgeleri[0];o.Islev=SalonIslevi.Sahne;o.YukseklikM=1.7;var canvas=new SalonCizimCanvas{Salon=s};
            typeof(SalonCizimCanvas).GetField("_offsetHedef",Hidden)!.SetValue(canvas,o);Call(canvas,"OffsetUygula",new Point2D(12,5),1d);
            Check(s.CizimOgeleri.Count==2&&s.CizimOgeleri[1].Islev==SalonIslevi.Sahne&&s.CizimOgeleri[1].YukseklikM==1.7,"Offset lost stage meaning/height");
        });
        Run("Editor initial layout and history",()=>{
            var win=new SalonTasarlaWindow();var vm=Field<SalonTasarlaViewModel>(win,"_vm");Call(win,"YenileModel");Check(!vm.IsDirty,"Empty editor starts dirty");
            foreach(double width in new[]{980d,1366d})
            {
                win.Width=width;win.Height=768;win.Measure(new Size(width,768));win.Arrange(new Rect(0,0,width,768));
                var canvas=win.FindControl<SalonCizimCanvas>("CizimCanvas")!;Check(canvas.Bounds.Width>400,"Canvas hidden at "+width);
                Check(win.GetVisualDescendants().OfType<Button>().Any(b=>b.Content?.ToString()=="Salon sınırı"&&b.Bounds.Width>0),"Primary tool missing");
            }
            win.Close();
        });
        Run("Independent section edits preserve earlier roof regions",()=>{
            var s=Kst.Acoustics.Tests.SalonTasarimTests.Ornek();var section=new SalonKesitCanvas{Salon=s,A=new(0,0),B=new(10,0)};
            section.ProfiliSifirla();section.NoktaDuzenle(1,10,8);var first=s.CizimOgeleri.Single(o=>o.Islev==SalonIslevi.Tavan);
            section.A=new(0,5);section.B=new(10,5);section.ProfiliSifirla();section.NoktaDuzenle(0,0,9);
            Check(s.CizimOgeleri.Contains(first)&&s.CizimOgeleri.Count(o=>o.Islev==SalonIslevi.Tavan)==2,"Second section deleted first section roof");
        });
        Run("Sample opens, validates and exports the same model",()=>{
            var root=new DirectoryInfo(Environment.CurrentDirectory);while(root!=null&&!Directory.Exists(Path.Combine(root.FullName,"ortak-moduller")))root=root.Parent;
            var s=SalonGeometriServisi.Ac(Path.Combine(root!.FullName,"ortak-moduller/51-salon-tasarla/ornekler/serbest-L-salon.e4salon.json"));
            SalonTasarimServisi.Hazirla(s);Check(!SalonTasarimDogrulama.Kontrol(s).Any(i=>i.Hata),"Sample invalid");
            var mesh=SalonTasarimMeshServisi.Uret(s);var project=SalonImporter.Cevir(s,s.DosyaYolu);Check(Math.Abs(project.RoomVolume-mesh.Hacim)<.001&&project.Surfaces.Count==mesh.Yuzeyler.Count,"M01 disagrees with preview");
            var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".dxf");try{SalonDxfExportServisi.DisaAktar(s,path);Check(new FileInfo(path).Length>100,"Empty DXF");}finally{File.Delete(path);}
        });
        Console.WriteLine($"RESULT {passed} passed; {failed} failed");return failed>0?1:0;
    }
}
