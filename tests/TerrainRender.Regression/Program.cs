using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Kst.Acoustics.App.Controls;
using Kst.Acoustics.App.Terrain.Rendering;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Terrain.Models;
using Kst.Acoustics.Core.Terrain.Services;

internal class Program
{
    private static void Assert(bool value,string why) { if(!value) throw new Exception(why); }
    [STAThread] static void Main(string[] args)
    {
        var tin=new AraziYuzeyi { Dugumler=[new(628000,4110000,10),new(628010,4110000,10),
            new(628010,4110010,10),new(628000,4110010,10)], Ucgenler=[new(0,1,2),new(2,3,0)] };
        var mesh=TerrainMesh.Measured(tin);
        var draw=new TerrainRasterizer();
        var top=new TerrainCamera(0,Math.PI/2,1,0,0);
        draw.Draw(mesh,null,top,256,256,256,256);
        for(int y=27;y<229;y++) for(int x=27;x<229;x++)
            Assert(draw.Pixels[y*256+x]==mesh.Faces[0].Color,"Gap across projected TIN");
        Assert(mesh.Points[2].X==10 && mesh.Points[2].Y==10,"Survey origin lost precision");
        int[] heat=mesh.HeatColors(new Dictionary<int,double>{{1,0},{2,1}});
        draw.Draw(mesh,heat,top,256,256,256,256);
        Assert(draw.Pixels[194*256+194]==heat[0] && draw.Pixels[55*256+55]==heat[1] && heat[0]!=heat[1],
            "Heatmap IDs do not match the measured triangles");
        var a=new Point3D(628000,4110000,15); var b=new Point3D(a.X+10,a.Y,15); var c=new Point3D(a.X,a.Y+10,15);
        var upper=new ProjectSurface(1,"Pad","TasarimPlatform",[a,b,c]);
        var lower=new ProjectSurface(2,"Ground","AraziYuzey",[a with {Z=10},b with {Z=10},c with {Z=10}]);
        var walls=new ProjectSurface(3,"Cut","TasarimPlatform",[a,b,b with {Z=10},a with {Z=10}]);
        var stacked=TerrainMesh.Design([upper,lower,walls],mesh);
        Assert(stacked.Faces.Length==4,"Wall quad not fully triangulated");
        draw.Draw(stacked,null,top,256,256,256,256);
        Assert(draw.Pixels[180*256+70]==stacked.Faces[0].Color,"Hidden ground occluded upper pad");
        var first=(int[])draw.Pixels.Clone();
        draw.Draw(TerrainMesh.Design([lower,upper,walls],mesh),null,top,256,256,256,256);
        Assert(first.SequenceEqual(draw.Pixels),"Depth depends on draw order");
        draw.Draw(stacked,null,top with {PanX=100000},256,256,256,256);
        Assert(draw.Pixels.All(p=>p==TerrainRasterizer.Background),"Offscreen triangles leaked");
        Console.WriteLine("PASS: shared edge coverage, survey precision, heatmap IDs, wall quads, occlusion, order and clipping.");
        if(args.Length==0) return;
        var source=AraziGeometriServisi.Ac(args[0]);
        tin=new AraziYuzeyServisi().Olustur(source);
        var sw=Stopwatch.StartNew(); mesh=TerrainMesh.Measured(tin); sw.Stop();
        Console.WriteLine($"Source: {tin.Dugumler.Count} nodes, {tin.Ucgenler.Count} faces; display mesh prep {sw.Elapsed.TotalMilliseconds:F1} ms");
        Assert(mesh.Faces.Length==tin.Ucgenler.Count,"Measured triangles lost");
        AppBuilder.Configure<Application>().UsePlatformDetect().WithInterFont().SetupWithoutStarting();
        var size=new Size(1140,470); var pixel=new PixelSize(1140,470);
        using var target=new RenderTargetBitmap(pixel);
        using var upload=new WriteableBitmap(pixel,new(96,96),PixelFormat.Bgra8888,AlphaFormat.Premul);
        var old=new GeometryPreviewControl { Projection=GeometryProjectionMode.Isometric3D, UnitLabel="m",
            Surfaces=Arazi3DGorunumServisi.YuzeyleriOlustur(tin) };
        old.Measure(size); old.Arrange(new Rect(size));
        var angle=typeof(GeometryPreviewControl).GetField("_azimuth",BindingFlags.Instance|BindingFlags.NonPublic)!;
        void OldFrame(double az)
        {
            angle.SetValue(old,az);
            using var ctx=target.CreateDrawingContext();
            old.Render(ctx);
        }
        void NewFrame(TerrainMesh m,double az)
        {
            draw.Draw(m,null,TerrainCamera.Home with { Azimuth=az },pixel.Width,pixel.Height,size.Width,size.Height);
            using(var fb=upload.Lock())
                for(int y=0;y<pixel.Height;y++) Marshal.Copy(draw.Pixels,y*pixel.Width,fb.Address+y*fb.RowBytes,pixel.Width);
            using var ctx=target.CreateDrawingContext(); ctx.DrawImage(upload,new Rect(size));
        }
        static double Ms(Action action) { var s=Stopwatch.StartNew(); action(); return s.Elapsed.TotalMilliseconds; }
        Console.WriteLine($"First frame old / new raster+blit: {Ms(()=>OldFrame(.8)):F1} / {Ms(()=>NewFrame(mesh,.8)):F1} ms");
        var oldMs=new List<double>(); var newMs=new List<double>();
        for(int i=0;i<7;i++)
        {
            double az=.9+i*.1;
            oldMs.Add(Ms(()=>OldFrame(az))); newMs.Add(Ms(()=>NewFrame(mesh,az)));
        }
        Console.WriteLine($"1140x470 rotating median (7 views): old {oldMs.Order().ElementAt(3):F1} ms; new raster+blit {newMs.Order().ElementAt(3):F1} ms");
        var index=new AraziKotIndeksi(tin);
        var center=tin.Dugumler.First(p=>new[]{(-20,-20),(20,-20),(20,20),(-20,20)}.All(v=>index.KotSorgula(p.X+v.Item1,p.Y+v.Item2)!=null));
        var region=new TasarimBolgesi { Ad="Render test",HedefKotM=center.Z-2,
            Poligon=[new(center.X-20,center.Y-20),new(center.X+20,center.Y-20),new(center.X+20,center.Y+20),new(center.X-20,center.Y+20)],
            KenarGecisi=TasarimKenarTipi.Sev,KenarAyarlari=[new(){KenarIndex=0,Tip=TasarimKenarTipi.DikKesim}] };
        var preview=AraziTasarimOnizlemeServisi.Olustur(tin,new(){Bolgeler=[region]});
        var design=TerrainMesh.Design(preview.Yuzeyler,mesh);
        Assert(design.Faces.Length==preview.Yuzeyler.Sum(s=>s.Vertices.Count-2),"Design triangles lost");
        NewFrame(design,.8);
        var designMs=Enumerable.Range(0,15).Select(i=>Ms(()=>NewFrame(design,.9+i*.1))).Order().ToArray();
        Console.WriteLine($"Design {design.Faces.Length} faces incl. platform/walls/slopes, median raster+blit {designMs[7]:F1} ms");
    }
}
