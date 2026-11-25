using Kst.Acoustics.App.Bina.ViewModels;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Bina.Services;

public static class RescanMirrorTests
{
    public static void TaramaIkiYuzunBilgileriniKorur()
    {
        var vm = new BinaGeometriViewModel { Geo = ProjeOlustur() };
        vm.AciklikTara();
        Dogrula(vm.SeciliKat!);
    }

    public static void KayitSonrasiTaramaYansimaKimliginiKorur()
    {
        string path = Path.Combine(Path.GetTempPath(), "bina-mirror-" + Guid.NewGuid().ToString("N") + ".e4bina.json");
        try
        {
            BinaGeometriServisi.Kaydet(ProjeOlustur(), path);
            var vm = new BinaGeometriViewModel { Geo = BinaGeometriServisi.Ac(path) };
            if (vm.SeciliKat!.HamSegmentler.Count != 0) throw new Exception("Fixture must exercise persisted detection geometry.");
            vm.AciklikTara();
            Dogrula(vm.SeciliKat);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    public static void KaldirilanKomsuYuzGeriGelmez()
    {
        var vm = new BinaGeometriViewModel { Geo = ProjeOlustur() };
        vm.SeciliKat!.Odalar.RemoveAt(1);
        vm.AciklikTara();
        if (vm.SeciliKat.Acikliklar.Count != 1 || vm.SeciliKat.Acikliklar.Single().Id != "main-face")
            throw new Exception("Removed neighboring face was resurrected by reconciliation.");
    }

    private static BinaGeometri ProjeOlustur()
    {
        var geo = BinaGeometriServisi.YeniOlustur();
        var kat = new BinaKat
        {
            Ad = "Zemin", KaynakKatmanlari = ["DUVAR", "KAPI"],
            KayitliTespitSegmentleri = [new BinaDxfSegment { A = new(0,0), B = new(4,0) }],
            DxfArclar = [new BinaDxfArc { Merkez = new(1,0), R = .9, BasAciDeg = 0, BitAciDeg = 90 }]
        };
        kat.Odalar.Add(new BinaOda { Id = "north", Ad = "Oda 1", Poligon = [new(0,0),new(4,0),new(4,3),new(0,3)] });
        kat.Odalar.Add(new BinaOda { Id = "south", Ad = "Oda 2", Poligon = [new(0,-.3),new(4,-.3),new(4,-3.3),new(0,-3.3)] });
        kat.Acikliklar = AciklikTespitServisi.Bul(kat.KayitliTespitSegmentleri.Select(s => (s.A,s.B)).ToList(),
            kat.DxfArclar.Select(a => (a.Merkez,a.R,a.BasAciDeg,a.BitAciDeg)).ToList(), []);
        if (kat.Acikliklar.Count != 1 || AciklikYardimci.KomsuDuvarlariTamamla(kat) != 1)
            throw new Exception("Fixture must create one directly detected opening and one generated mirror.");
        var first = kat.Acikliklar[0];
        first.Id = "main-face"; first.Tip = BinaAciklikTipi.Pencere;
        first.YukseklikM = 2.4; first.EsikYuksekligiM = 0; first.EsikYuksekligiBelirtildi = true;
        first.Kaynak = "arc; etiket: P1 90/240";
        var other = kat.Acikliklar[1];
        other.Id = "mirror-face"; other.Tip = BinaAciklikTipi.Pencere;
        other.YukseklikM = 2.3; other.EsikYuksekligiM = .1; other.EsikYuksekligiBelirtildi = true;
        other.Kaynak = "arc-yansima; etiket: P2 90/230";
        geo.Katlar.Add(kat);
        return geo;
    }

    private static void Dogrula(BinaKat kat)
    {
        if (kat.Acikliklar.Count != 2) throw new Exception("Both physical faces must remain.");
        var first = kat.Acikliklar.Single(a => a.Id == "main-face");
        var other = kat.Acikliklar.Single(a => a.Id == "mirror-face");
        if (first.Tip != BinaAciklikTipi.Pencere || other.Tip != BinaAciklikTipi.Pencere ||
            first.YukseklikM != 2.4 || other.YukseklikM != 2.3 ||
            !first.EsikYuksekligiBelirtildi || !other.EsikYuksekligiBelirtildi ||
            first.EtkinEsikYuksekligiM != 0 || other.EtkinEsikYuksekligiM != .1 ||
            first.Kaynak != "arc; etiket: P1 90/240" || other.Kaynak != "arc-yansima; etiket: P2 90/230")
            throw new Exception("Per-face dimensions, type, zero sill or evidence changed during rescan.");
    }
}
