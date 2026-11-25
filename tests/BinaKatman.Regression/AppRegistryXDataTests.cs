using Kst.Acoustics.Core.Bina.Services;
using netDxf;
using netDxf.Blocks;
using netDxf.Entities;
using netDxf.Tables;
using netDxf.Units;

internal static class AppRegistryXDataTests
{
    public static int Run()
    {
        int passed = 0;
        void Check(bool value, string title)
        {
            if (!value) throw new Exception(title);
            passed++;
            Console.WriteLine("PASS " + title);
        }
        string folder = Path.Combine(Path.GetTempPath(), "bina-appid-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            // Both a self-reference (as in berecik.dwg) and a two-node cycle
            // must survive file loading and block expansion without recursion.
            foreach (bool mutual in new[] { false, true })
            {
                var doc = new DxfDocument();
                doc.DrawingVariables.InsUnits = DrawingUnits.Meters;
                var app = new ApplicationRegistry("CUSTOM_APP");
                var other = mutual ? new ApplicationRegistry("OTHER_APP") : app;
                doc.ApplicationRegistries.Add(app);
                if (mutual) doc.ApplicationRegistries.Add(other);

                var block = new Block("ROOM_BLOCK");
                var room = new Polyline2D(new[] { new Vector2(0, 0), new Vector2(8, 0),
                    new Vector2(8, 6), new Vector2(0, 6) }, true);
                var data = new XData(app);
                data.XDataRecord.Add(new XDataRecord(XDataCode.String, "keep entity metadata"));
                room.XData.Add(data);
                block.Entities.Add(room);
                block.Entities.Add(new Text("DEPO", new Vector2(4, 3), .3));
                doc.Entities.Add(new Insert(block, new Vector2(20, 30)));
                app.XData.Add(new XData(other));
                if (mutual) other.XData.Add(new XData(app));

                string path = Path.Combine(folder, mutual ? "mutual.dxf" : "self.dxf");
                doc.Save(path);
                var original = File.ReadAllBytes(path);
                var saved = DxfDocument.Load(path);
                Check(saved.ApplicationRegistries[app.Name].XData.Count == 1, "Cyclic APPID fixture persisted");
                Check(saved.Blocks[block.Name].Entities.First().XData.Count == 1, "Entity XData fixture persisted");

                var parser = new DxfKatPlaniParser();
                var drawing = parser.KatmanliOku(path);
                var preview = drawing.HamKatOlustur(drawing.Katmanlar.Select(k => k.Ad));
                Check(preview.HamSegmentler.Count == 4 && preview.HamMetinler.Single().Value == "DEPO",
                    "Cyclic APPID preview preserves geometry and text");
                Check(preview.HamSegmentler.Any(s => s.A.X == 20 && s.A.Y == 30 && s.B.X == 28),
                    "Cyclic APPID preview preserves block translation");
                var raw = parser.HamVeriyiOku(path);
                Check(raw.HamSegmentler.Count == 4 && raw.HamMetinler.Count == 1,
                    "Cyclic APPID raw import completes");
                Check(parser.ExtractRawSegmentsFromFile(path).Count == 4,
                    "Cyclic APPID segment reload completes");
                var floor = parser.Parse(path, 1, 3);
                Check(floor.Odalar.Count == 1 && Math.Abs(floor.Odalar[0].AlanM2 - 48) < 2,
                    "Cyclic APPID floor parsing retains room area");
                Check(original.SequenceEqual(File.ReadAllBytes(path)), "Import leaves original CAD file unchanged");
            }
        }
        finally { Directory.Delete(folder, true); }
        return passed;
    }
}
