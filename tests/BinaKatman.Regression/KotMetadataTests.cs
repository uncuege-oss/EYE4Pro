using Kst.Acoustics.Core.Bina.Services;
using netDxf;
using netDxf.Blocks;
using netDxf.Entities;
using netDxf.Tables;
using netDxf.Units;

internal static class KotMetadataTests
{
    public static int Run()
    {
        int passed = 0;
        void Check(bool value, string title)
        {
            if (!value) throw new Exception(title);
            passed++; Console.WriteLine("PASS " + title);
        }
        string folder = Path.Combine(Path.GetTempPath(), "bina-kot-metadata-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var doc = new DxfDocument(); doc.DrawingVariables.InsUnits = DrawingUnits.Meters;
            var wall = new Layer("WALL"); var label = new Layer("LABEL");
            var annotation = new Layer("YAZI TEF") { IsVisible = false };
            var parent = new Layer("KOT_BLOCK") { IsVisible = false };
            var child = new Layer("CHILD_KOT");
            doc.Entities.Add(new Polyline2D(new[] { new Vector2(0,0), new Vector2(10,0),
                new Vector2(10,8), new Vector2(0,8) }, true) { Layer = wall });
            doc.Entities.Add(new Text("OKUMA SALONU", new Vector2(5,4), .3) { Layer = label });
            doc.Entities.Add(new Text("+0.00", new Vector2(2,2), .3) { Layer = annotation });
            doc.Entities.Add(new Text("+4.00", new Vector2(2,3), .3) { Layer = annotation });
            doc.Entities.Add(new Text("MASA 120/60", new Vector2(2,4), .3) { Layer = annotation });
            var block = new Block("LEVEL_LABELS");
            block.Entities.Add(new Text("+0.00", new Vector2(0,0), .3) { Layer = child });
            block.Entities.Add(new Text("+4.00", new Vector2(0,1), .3) { Layer = child });
            block.Entities.Add(new Text("KOLTUK", new Vector2(0,2), .3) { Layer = child });
            doc.Entities.Add(new Insert(block, new Vector2(6,2)) { Layer = parent });
            string path = Path.Combine(folder, "level-metadata.dxf"); doc.Save(path);

            var drawing = new DxfKatPlaniParser().KatmanliOku(path);
            var defaults = drawing.Katmanlar.Where(k => k.VarsayilanAcik).Select(k => k.Ad).ToList();
            Check(drawing.Katmanlar.Single(k => k.Ad == "YAZI TEF").KotYukseklikEtiketiSayisi == 2,
                "Hidden annotation layer counts only its two level labels, not furniture text");
            Check(drawing.Katmanlar.Single(k => k.Ad == "LABEL").KotYukseklikEtiketiSayisi == 0 &&
                  drawing.Katmanlar.Single(k => k.Ad == "WALL").KotYukseklikEtiketiSayisi == 0,
                "Room labels and wall geometry do not inflate elevation metadata");
            Check(drawing.Katmanlar.Single(k => k.Ad == "CHILD_KOT").KotYukseklikEtiketiSayisi == 2 &&
                  drawing.Katmanlar.Single(k => k.Ad == "KOT_BLOCK").KotYukseklikEtiketiSayisi == 2,
                "Child level labels are counted on both the child and required insert parent");
            var hidden = drawing.HamKatOlustur(defaults);
            Check(defaults.Contains("CHILD_KOT") && !defaults.Contains("KOT_BLOCK") &&
                  hidden.HamMetinler.Count == 1 && hidden.HamMetinler[0].Value == "OKUMA SALONU",
                "Closed annotation and insert parent keep every level label out of the filtered snapshot");
            var generated = new DxfKatYukleyici().OtomatikYukle(hidden);
            Check(generated.Katlar.Count == 1 && generated.Katlar[0].KatYuksekligiM == 3 &&
                  generated.Katlar[0].HamMetinler.All(t => !t.Value.StartsWith("+")),
                "Metadata warning counts cannot feed hidden level evidence into generation");
            var annotationOn = drawing.HamKatOlustur(defaults.Append("YAZI TEF"));
            Check(annotationOn.HamMetinler.Count(t => t.Value.StartsWith("+")) == 2 &&
                  annotationOn.HamMetinler.Any(t => t.Value == "MASA 120/60") &&
                  annotationOn.HamMetinler.All(t => t.Value != "KOLTUK"),
                "Opening annotation layer exposes its texts without exposing the closed block");
            var parentOn = drawing.HamKatOlustur(defaults.Append("KOT_BLOCK"));
            Check(parentOn.HamMetinler.Count(t => t.Value.StartsWith("+")) == 2 &&
                  parentOn.HamMetinler.Any(t => t.Value == "KOLTUK") &&
                  parentOn.HamMetinler.All(t => t.Value != "MASA 120/60"),
                "Opening parent exposes child level evidence only when both required layers are open");
            var childOff = drawing.HamKatOlustur(defaults.Where(k => k != "CHILD_KOT").Append("KOT_BLOCK"));
            Check(childOff.HamMetinler.Count == 1 &&
                  drawing.Katmanlar.Single(k => k.Ad == "KOT_BLOCK").KotYukseklikEtiketiSayisi == 2,
                "Closing the child hides its texts while cached parent warning metadata remains available");
        }
        finally { Directory.Delete(folder, true); }
        return passed;
    }
}
