using Kst.Acoustics.App.Bina.ViewModels;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Bina.Services;

public static class LayerPersistenceTests
{
    public static void KayitSonrasiKapaliKatmanlarTaramayaDonmez()
    {
        string folder = Path.Combine(Path.GetTempPath(), "bina-layer-persist-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            // Eski dosya-yolu fallback'i bu veriyi seçimin dışında yeniden okurdu.
            string source = Path.Combine(folder, "source.dxf");
            File.WriteAllText(source, "not a DXF: selected geometry must be used without reopening this file");
            var geo = BinaGeometriServisi.YeniOlustur();
            var kat = new BinaKat { DxfFilePath = source, KaynakKatmanlari = ["YAZI"], KayitliTespitSegmentleri = [],
                DxfSegmentler = [new() { A = new(0, 0), B = new(3, 0) }] };
            geo.Katlar.Add(kat);
            string path = Path.Combine(folder, "test.e4bina.json");
            BinaGeometriServisi.Kaydet(geo, path);
            var vm = new BinaGeometriViewModel { Geo = BinaGeometriServisi.Ac(path) };
            vm.AciklikTara();
            if (vm.IsDirty || !vm.DurumMesaji.Contains("segmenti yok"))
                throw new Exception("Empty filtered geometry was replaced by unfiltered DXF fallback.");
            if (vm.SeciliKat!.KaynakKatmanlari?.Single() != "YAZI" || vm.SeciliKat.KayitliTespitSegmentleri?.Count != 0)
                throw new Exception("Saved selection or explicit empty geometry was lost.");
        }
        finally { Directory.Delete(folder, true); }
    }

    public static void HamKatmanSecimiOlusturmakProjeyiDegistirmez()
    {
        var geo = BinaGeometriServisi.YeniOlustur();
        geo.Katlar.Add(new BinaKat { Ad = "Mevcut Kat" });
        var vm = new BinaGeometriViewModel { Geo = geo };
        var document = new netDxf.DxfDocument();
        document.Entities.Add(new netDxf.Entities.Line(new netDxf.Vector2(0,0), new netDxf.Vector2(8,0)));
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".dxf");
        try
        {
            document.Save(path);
            var source = new DxfKatPlaniParser().KatmanliOku(path);
            var empty = source.HamKatOlustur([]);
            var open = source.HamKatOlustur(source.Katmanlar.Select(k => k.Ad));
            if (open.Odalar.Count != 0 || empty.Odalar.Count != 0 || geo.Katlar.Count != 1 || vm.IsDirty)
                throw new Exception("Preview unexpectedly generated rooms or changed the project.");
        }
        finally { File.Delete(path); }
    }
}
