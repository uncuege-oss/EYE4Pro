using Kst.Acoustics.App.Electric.BasitProjeCiz.ViewModels;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

namespace Kst.Acoustics.Tests;

public static class M12PrizKablolamaTests
{
    public static void KapasiteManuelKablolarVePanoAtamasiKorunur()
    {
        var vm = new BasitProjeCizViewModel();
        var kat = new BasitProjeKat();
        var oda = new BasitProjeOda { Ad = "Atölye", Poligon = [new(0, 0), new(18, 0), new(18, 6), new(0, 6)] };
        kat.Odalar.Add(oda);
        BasitProjeEleman Ekle(string id, BpcElemanTipi tip, double x, double y)
        {
            var e = new BasitProjeEleman { Id = id, Tip = tip, PaftaTipi = BpcPaftaTipi.Priz, OdaId = oda.Id, X = x, Y = y };
            kat.Elemanlar.Add(e);
            return e;
        }
        var b1 = Ekle("b1", BpcElemanTipi.Buat, 0, 2);
        var b2 = Ekle("b2", BpcElemanTipi.Buat, 18, 2);
        var uzak = Ekle("uzak", BpcElemanTipi.KatPanosu, 18, 5);
        var yakin = Ekle("yakın", BpcElemanTipi.KatPanosu, 0, 1);
        var direkt = Ekle("ups", BpcElemanTipi.UpsPrizi, 1, 0);
        var prizler = Enumerable.Range(1, 14).Select(i => Ekle($"p{i}", BpcElemanTipi.Priz, i, 6)).ToList();
        var korunacak = new BpcPrizDevre { BuatId = b2.Id, PrizElemanIds = prizler.Take(7).Select(e => e.Id).ToList() };
        kat.PrizDevreleri.Add(korunacak);
        vm.KatEkle(kat);
        vm.AktifPafta = BpcPaftaTipi.Priz;
        vm.PrizAsamasi = BpcPrizAsamasi.MahalKablosu;
        vm.PrizMahalKablolariniCek();
        if (korunacak.PrizElemanIds.Count != 7 || kat.PrizDevreleri.Single(d => d.BuatId == b1.Id).PrizElemanIds.Count != 7 ||
            kat.BuatPanoBaglantilari.Count > 0 || kat.Kablolar.Any(k => k.Tip != BpcKabloTipi.PrizKablosu))
            throw new InvalidOperationException("Önceden dolu sondaki buat korunduğunda ilk buat kalan yedi prizi beslemeli.");

        vm.PrizAsamasi = BpcPrizAsamasi.PanoKablosu;
        vm.PrizPanoKablolariniCek();
        if (kat.BuatPanoBaglantilari.Count != 0) throw new InvalidOperationException("Atamasız mahalde yakındaki panoya hat çekildi.");
        vm.MahalePanoAta(oda.Id, uzak.Id);
        vm.PrizPanoKablolariniCek();
        if (kat.BuatPanoBaglantilari.Count != 3 || kat.BuatPanoBaglantilari.Any(b => b.PanoId != uzak.Id) ||
            !kat.PrizDevreleri.Any(d => d.BuatId == direkt.Id && d.PrizElemanIds.SequenceEqual([direkt.Id])))
            throw new InvalidOperationException("İki buat ve UPS prizi için atanmış panoda üç ayrı linye bekleniyordu.");
        var kimlikler = kat.BuatPanoBaglantilari.Select(b => b.Id).ToArray();
        vm.PrizPanoKablolariniCek();
        if (!kat.BuatPanoBaglantilari.Select(b => b.Id).SequenceEqual(kimlikler)) throw new InvalidOperationException("Tekrarda linye kimlikleri değişti.");
        vm.MahalePanoAta(oda.Id, yakin.Id);
        if (kat.BuatPanoBaglantilari.Count != 0 || kat.Kablolar.Any(k => k.Tip == BpcKabloTipi.PanoKablosu))
            throw new InvalidOperationException("Atama değiştiğinde eski priz çıkışları temizlenmeli.");
        vm.GeriAlGenelCommand.Execute(null);
        if (!kat.BuatPanoBaglantilari.Select(b => b.Id).SequenceEqual(kimlikler))
            throw new InvalidOperationException("Tek geri alma tüm priz çıkışlarını geri getirmeli.");
        kat.Elemanlar.Single(e => e.Id == "p1").X += 0.9;
        vm.PrizPanoKablolariniCek();
        if (kat.BuatPanoBaglantilari.Any(b => b.BuatId == b2.Id) || !kat.BuatPanoBaglantilari.Any(b => b.BuatId == b1.Id))
            throw new InvalidOperationException("Taşınan prizin güncellenmemiş devresi pano bağlantısı almamalı.");
    }
}
