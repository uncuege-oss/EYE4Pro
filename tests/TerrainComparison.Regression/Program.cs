using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Kst.Acoustics.App.Terrain.Rendering;
using Kst.Acoustics.App.Terrain.Views;
using Kst.Acoustics.Core.Terrain.Models;
using Kst.Acoustics.Core.Terrain.Services;

internal static class Program
{
    private static void Near(double value, double expected, double tolerance = 1e-7)
    {
        if (Math.Abs(value-expected) > tolerance || !double.IsFinite(value))
            throw new Exception($"Expected {expected}, got {value}");
    }
    private static void Check(bool value, string why)
    {
        if (!value) throw new Exception(why);
    }
    private static AraziYuzeyi RectTin(double x, double y, double width, double height, Func<double,double,double> z, bool other = false)
    {
        AraziDugum Node(double dx,double dy) => new(x+dx,y+dy,z(dx,dy));
        return new() { Dugumler=[Node(0,0),Node(width,0),Node(width,height),Node(0,height)],
            Ucgenler = other ? [new(3,1,0),new(3,2,1)] : [new(0,1,2),new(0,2,3)] };
    }
    [STAThread] private static void Main(string[] args)
    {
        var initial = RectTin(628000,4110000,10,10,(_,_)=>10);
        var final = RectTin(628000,4110000,10,10,(x,_)=>10+x-5,true);
        var result = AraziKarsilastirmaServisi.Hesapla(initial,final);
        Near(result.YarmaM3,125); Near(result.DolguM3,125); Near(result.OrtakAlanM2,100);
        Near(result.YarmaAlaniM2,50); Near(result.DolguAlaniM2,50);
        Check(result.Parcalar.All(p=>Math.Min(p.A.Fark,Math.Min(p.B.Fark,p.C.Fark))>=-1e-8 ||
            Math.Max(p.A.Fark,Math.Max(p.B.Fark,p.C.Fark))<=1e-8),"Unsplit cut/fill face");
        var meshes = TerrainMesh.Compare(result,initial,final,CancellationToken.None);
        Check(ReferenceEquals(meshes.First.SharedFrame,meshes.Last.SharedFrame),"Camera extents differ");
        var raster = new TerrainRasterizer();
        double leftScale = raster.Draw(meshes.First,null,TerrainCamera.Home,400,300,400,300);
        Near(raster.Draw(meshes.Last,null,TerrainCamera.Home,400,300,400,300),leftScale);
        Check(meshes.Last.Faces.Any(f=>((f.Color>>16)&255)>(f.Color&255)) &&
              meshes.Last.Faces.Any(f=>((f.Color>>16)&255)<(f.Color&255)),"Cut/fill colors missing");
        var csv = new StringWriter();
        AraziMetrajCsv.Yaz(csv,result,"ilk","son",CancellationToken.None);
        Check(csv.ToString().Contains("Yarma;50;125") && csv.ToString().Contains("Dolgu;50;125"),"CSV totals");
        var different = RectTin(628005,4110002,10,6,(_,_)=>8,true);
        var partial = AraziKarsilastirmaServisi.Hesapla(initial,different);
        Near(partial.OrtakAlanM2,30); Near(partial.YarmaM3,60); Near(partial.DolguM3,0);
        Near(partial.IlkAlanM2,100); Near(partial.SonAlanM2,60);
        var reverse = AraziKarsilastirmaServisi.Hesapla(different,initial);
        Near(reverse.DolguM3,60); Near(reverse.NetM3,-60);
        var same = AraziKarsilastirmaServisi.Hesapla(initial,initial);
        Near(same.EsitAlanM2,100); Near(same.DolguM3+same.YarmaM3,0);
        var peak = RectTin(0,0,10,10,(x,y)=>x*y/10);
        var flipped = RectTin(0,0,10,10,(x,y)=>x*y/10,true);
        var saddle = AraziKarsilastirmaServisi.Hesapla(peak,flipped);
        Near(saddle.YarmaM3,1000d/6); Near(saddle.DolguM3,0);
        foreach (double shift in new[]{10d,12d})
        {
            bool rejected=false;
            try { AraziKarsilastirmaServisi.Hesapla(initial,RectTin(628000+shift,4110000,10,10,(_,_)=>10)); }
            catch(InvalidOperationException ex) when(ex.Message.Contains("ortak alanı yok")) { rejected=true; }
            Check(rejected,"Empty overlap reported as zero earthwork");
        }
        Console.WriteLine("PASS · mixed cut/fill, partial coverage, roles, opposite diagonals, empty/touching limits, colors, shared scale and CSV");
        AppBuilder.Configure<Application>().UsePlatformDetect().WithInterFont().SetupWithoutStarting();
        using var finished = new CancellationTokenSource();
        Exception? failure=null;
        Dispatcher.UIThread.Post(async () => {
            try { await CheckProjectWorkflow(); }
            catch(Exception ex) { failure=ex; }
            finally { finished.Cancel(); }
        });
        Dispatcher.UIThread.MainLoop(finished.Token);
        if(failure!=null) throw new Exception("Comparison window workflow",failure);
        if(args.Length>0) CheckAdrenapark(args[0]);
    }

    private static async Task CheckProjectWorkflow()
    {
        string folder = Directory.CreateTempSubdirectory("eye4-compare-").FullName;
        string before = Path.Combine(folder,"ilk.e4arazi.json"), after = Path.Combine(folder,"son.e4arazi.json");
        try
        {
            var model = new AraziModeli { AraziAdi="Ölçüm", Noktalar=[new(0,0,10),new(10,0,10),new(10,10,10),new(0,10,10)] };
            AraziGeometriServisi.Kaydet(model,before);
            model.Tasarim.Bolgeler.Add(new() { Ad="Dik platform", HedefKotM=8, Poligon=[new(1,1),new(6,1),new(6,6),new(1,6)] });
            AraziGeometriServisi.Kaydet(model,after);
            var win = new AraziKarsilastirmaWindow();
            try
            {
                await win.ProjeYukleAsync(true,before);
                await win.ProjeYukleAsync(false,after);
                var export=win.FindControl<Button>("Csv")!;
                var cut=win.FindControl<TextBlock>("Yarma")!;
                Check(export.IsEnabled && cut.Text=="50,00 m³","Saved design not applied: "+win.FindControl<TextBlock>("Durum")!.Text+" "+cut.Text);
                win.FindControl<ComboBox>("SonSecim")!.SelectedIndex=0;
                var watch=Stopwatch.StartNew();
                while(!export.IsEnabled && watch.Elapsed<TimeSpan.FromSeconds(10)) await Task.Delay(10);
                Check(export.IsEnabled && cut.Text=="0,00 m³","Switch to measured surface did not replace prior result");
                win.FindControl<ComboBox>("Mod")!.SelectedIndex=1;
                Check(win.FindControl<Slider>("Perde")!.IsVisible && win.FindControl<Arazi3DControl>("SonGorunum")!.Clip!=null,"Swipe mode");
                foreach(var p in model.Noktalar) p.X+=30;
                // Load a disjoint survey without a saved design.
                model.Tasarim.Bolgeler.Clear();
                AraziGeometriServisi.Kaydet(model,after);
                await win.ProjeYukleAsync(false,after);
                Check(!export.IsEnabled && cut.Text=="—" && win.FindControl<TextBlock>("Durum")!.Text!.Contains("ortak alanı yok"),"Stale result after replacement");
            }
            finally { win.Close(); }
            Console.WriteLine("PASS · initial/final project loading, saved design, measured surface switch, swipe and stale-result clearing");
        }
        finally
        {
            foreach(string path in new[]{before,after,before+".bak",after+".bak"}) if(File.Exists(path)) File.Delete(path);
            if (!Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
        }
    }

    private static void CheckAdrenapark(string path)
    {
        var tin = new AraziYuzeyServisi().Olustur(AraziGeometriServisi.Ac(path));
        // A measured-to-measured benchmark with deliberately different XY coverage and known Z offset.
        var flat = new AraziYuzeyi { Dugumler=tin.Dugumler.Select(p=>p with { Z=1000 }).ToList(),Ucgenler=tin.Ucgenler };
        var shifted = new AraziYuzeyi { Dugumler=tin.Dugumler.Select(p=>p with { X=p.X+.23,Y=p.Y+.14,Z=999 }).ToList(),Ucgenler=tin.Ucgenler };
        var watch=Stopwatch.StartNew();
        var result=AraziKarsilastirmaServisi.Hesapla(flat,shifted);
        Near(result.OrtakAlanM2,result.YarmaM3,1e-6); Near(result.DolguM3,0);
        Check(result.OrtakAlanM2>result.IlkAlanM2*.98 && result.OrtakAlanM2<result.IlkAlanM2,"Unexpected partial coverage");
        var meshes=TerrainMesh.Compare(result,flat,shifted,CancellationToken.None);
        Check(meshes.Last.Faces.Length < result.Parcalar.Count/3,"Fully covered, uniform faces were not consolidated");
        Console.WriteLine($"PASS · Adrenapark topology {tin.Ucgenler.Count} × 2, shifted test survey: {result.Parcalar.Count} metraj pieces, {meshes.First.Faces.Length} + {meshes.Last.Faces.Length} displayed faces in {watch.Elapsed.TotalSeconds:F2}s");
    }
}
