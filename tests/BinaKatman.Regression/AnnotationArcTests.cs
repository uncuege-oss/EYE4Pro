using Kst.Acoustics.Core.Bina.Services;
using netDxf;
using netDxf.Entities;
using netDxf.Tables;
using netDxf.Units;

internal static class AnnotationArcTests
{
    public static int Run()
    {
        string file = Path.Combine(Path.GetTempPath(), "bina-arc-" + Guid.NewGuid().ToString("N") + ".dxf");
        try
        {
            var doc = new DxfDocument();
            doc.DrawingVariables.InsUnits = DrawingUnits.Meters;
            var wall = new Layer("DUVAR"); var door = new Layer("KAPI"); var furniture = new Layer("TEFRİŞ"); var text = new Layer("yazı");
            doc.Entities.Add(new Line(new Vector2(0, 0), new Vector2(10, 0)) { Layer = wall });
            doc.Entities.Add(new Arc(new Vector2(1, 0), .9, 0, 90) { Layer = door });
            doc.Entities.Add(new Arc(new Vector2(5, 0), .9, 0, 90) { Layer = furniture });
            doc.Entities.Add(new Arc(new Vector2(8, 0), .9, 0, 90) { Layer = text });
            doc.Save(file);
            var source = new DxfKatPlaniParser().KatmanliOku(file);
            var all = source.HamKatOlustur(source.Katmanlar.Select(k => k.Ad));
            var architectural = source.HamKatOlustur(["DUVAR", "KAPI"]);
            if (all.HamSegmentler.Count <= architectural.HamSegmentler.Count)
                throw new Exception("Visible decorative arcs disappeared from preview.");
            if (all.DxfArclar.Count != 1 || all.DxfArclar[0].Merkez.X != 1)
                throw new Exception("Annotation arc entered detection metadata.");
            var detected = AciklikTespitServisi.Bul(all.HamTespitSegmentler,
                all.DxfArclar.Select(a => (a.Merkez, a.R, a.BasAciDeg, a.BitAciDeg)).ToList(), []);
            if (detected.Count != 1 || detected[0].Bas.X > 3 || detected[0].Bit.X > 3)
                throw new Exception("Decoration generated a false door.");
            Console.WriteLine("PASS Visible decoration remains in preview");
            Console.WriteLine("PASS Annotation and lowercase yazı arcs excluded from door metadata");
            Console.WriteLine("PASS Only real architectural arc generates a door");
            return 3;
        }
        finally { File.Delete(file); }
    }
}
