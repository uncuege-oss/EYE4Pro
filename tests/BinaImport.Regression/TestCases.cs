using Kst.Acoustics.Core.Bina.Services;
using netDxf;
using netDxf.Entities;
using netDxf.Units;

internal static class TestCases
{
    public static void Run()
    {
        string folder = Path.Combine(Path.GetTempPath(), "bina-auto-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var doc = new DxfDocument(); doc.DrawingVariables.InsUnits = DrawingUnits.Meters;
            void Floor(double x, string title, string room, double height)
            {
                doc.Entities.Add(new Polyline2D(new[] { new Vector2(x, 0), new Vector2(x + 10, 0), new Vector2(x + 10, 8), new Vector2(x, 8) }, true));
                doc.Entities.Add(new Text(title, new Vector2(x + 2, -1), .3));
                doc.Entities.Add(new Text(room, new Vector2(x + 5, 4), .3));
                doc.Entities.Add(new Text("KAT YÜKSEKLİĞİ=" + height.ToString(System.Globalization.CultureInfo.InvariantCulture) + " m", new Vector2(x + 2, 1), .3));
            }
            Floor(0, "ZEMİN KAT PLANI 1/50", "OKUMA SALONU", 3.4);
            Floor(40, "1. KAT PLANI 1/50", "TOPLANTI ODASI", 3.6);
            var path = Path.Combine(folder, "building.dxf"); doc.Save(path);
            var result = new DxfKatYukleyici().OtomatikYukle(path);
            Check(result.Katlar.Count == 2, "Two physical floors");
            Check(result.Katlar[0].Ad == "1. Kat" && result.Katlar[1].Ad == "Zemin Kat", "Top-to-bottom order");
            Check(result.Katlar[0].KatYuksekligiM == 3.6 && result.Katlar[1].KatYuksekligiM == 3.4, "Explicit storey heights");
            Check(result.Katlar[1].Odalar.Any(o => o.Ad == "OKUMA SALONU"), "Semantic room name");
            var geo = BinaGeometriServisi.YeniOlustur("Test");
            foreach (var kat in result.Katlar) geo.Katlar.Add(kat);
            var saved = Path.Combine(folder, "building.e4bina.json");
            BinaGeometriServisi.Kaydet(geo, saved);
            var restored = BinaGeometriServisi.Ac(saved);
            Check(restored.Katlar.Count == 2 && restored.Katlar[0].IceriAktarimNotlari.Count > 0, "Report and geometry round trip");
            Check(Bina3DGorunumServisi.YuzeyleriOlustur(restored).Count > 0, "3D generation after restore");
            using(var direct = MimariCadDosyasi.AcAsync(path).GetAwaiter().GetResult()) Check(direct.DxfYolu == path, "DXF bypasses converter");
            Check(File.Exists(path), "Source DXF remains after disposal");
            var single = new DxfDocument(); single.DrawingVariables.InsUnits = DrawingUnits.Meters;
            single.Entities.Add(new Polyline2D(new[] { new Vector2(0,0), new Vector2(8,0), new Vector2(8,6), new Vector2(0,6) }, true));
            single.Entities.Add(new Text("DEPO", new Vector2(4,3), .3));
            var singlePath = Path.Combine(folder,"single.dxf"); single.Save(singlePath);
            var unknown = new DxfKatYukleyici().OtomatikYukle(singlePath);
            Check(unknown.Katlar.Count == 1 && unknown.Notlar.Any(n => n.Contains("geçici")), "Unknown heights explicitly reported");
            Console.WriteLine("9 automatic import assertions passed");
        }
        finally { Directory.Delete(folder, true); }
    }
    private static void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); }
}
