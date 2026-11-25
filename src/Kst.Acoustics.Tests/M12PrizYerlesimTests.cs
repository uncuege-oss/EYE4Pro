using Kst.Acoustics.App.Electric.BasitProjeCiz.ViewModels;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

namespace Kst.Acoustics.Tests;

public static class M12PrizYerlesimTests
{
    public static void IcBukeyMahaldePrizBuatiSinirIcindeKalir()
    {
        var vm = new BasitProjeCizViewModel();
        var oda = new BasitProjeOda { Poligon = [new(0, 0), new(10, 0), new(10, 2), new(2, 2), new(2, 10), new(0, 10)] };
        var kat = new BasitProjeKat { Odalar = [oda] };
        kat.Elemanlar.Add(new() { Tip = BpcElemanTipi.Priz, OdaId = oda.Id, X = 6, Y = 1.85, PaftaTipi = BpcPaftaTipi.Priz });
        vm.KatEkle(kat);
        vm.AktifPafta = BpcPaftaTipi.Priz;
        vm.PrizAsamasi = BpcPrizAsamasi.Buat;
        vm.MahalListesindenOtomatikYerlestirCommand.Execute(null);
        var buat = kat.Elemanlar.Single(e => e.Tip == BpcElemanTipi.Buat);
        if (!PoligonYardimci.IsPointInside(new(buat.X, buat.Y), oda.Poligon))
            throw new InvalidOperationException("L biçiminde iç duvara yakın bir prizin buatı mahal sınırının içinde kalmalı.");
    }

    public static void PrizVeBuatYerlesimiAyridir()
    {
        var vm = new BasitProjeCizViewModel();
        var kat = new BasitProjeKat { KatNo = 1 };
        var salon = new BasitProjeOda { Ad = "Salon", Poligon = [new(0, 0), new(12, 0), new(12, 8), new(0, 8)] };
        var ofis = new BasitProjeOda { Ad = "Ofis", Poligon = [new(12, 0), new(24, 0), new(24, 8), new(12, 8)] };
        kat.Odalar.Add(salon);
        kat.Odalar.Add(ofis);
        vm.KatEkle(kat);
        vm.AktifPafta = BpcPaftaTipi.Priz;
        var normal = vm.Prizler.Single(p => p.BlokAdi == "E4P_PRIZ");
        var satir = new BpcMahalSatiri
        {
            Tip = BpcElemanTipi.Priz, ArmatürTanimId = normal.Id, YerlesimTipi = "Homojen Duvar", MontajTipi = "Duvar"
        };
        foreach (var (oda, adet) in new[] { (salon, 8), (ofis, 7) })
        {
            var ayar = satir.HucreGetirVeyaOlustur(oda.Id);
            ayar.Aktif = true;
            ayar.Adet = adet;
        }
        kat.MahalListesi.Add(satir);
        var elle = new BasitProjeEleman { Tip = BpcElemanTipi.Priz, BlokAdi = normal.BlokAdi, OdaId = salon.Id,
            X = 1, Y = 0.15, PaftaTipi = BpcPaftaTipi.Priz };
        var elleBuat = new BasitProjeEleman { Tip = BpcElemanTipi.Buat, OdaId = salon.Id,
            X = 2, Y = 0.4, PaftaTipi = BpcPaftaTipi.Priz };
        var isikBuati = new BasitProjeEleman { Tip = BpcElemanTipi.Buat, OdaId = ofis.Id,
            X = 13, Y = 0.4, PaftaTipi = BpcPaftaTipi.Aydinlatma };
        kat.Elemanlar.Add(elle);
        kat.Elemanlar.Add(elleBuat);
        kat.Elemanlar.Add(isikBuati);
        foreach (var tip in new[] { BpcElemanTipi.Priz, BpcElemanTipi.TrifazePriz, BpcElemanTipi.UpsPrizi, BpcElemanTipi.KombinePrizKutusu })
            kat.Elemanlar.Add(new() { Tip = tip, BlokAdi = tip == BpcElemanTipi.Priz ? "E4P_MUSTAKIL_PRIZ" : "",
                OdaId = ofis.Id, X = 17, Y = 0.15, PaftaTipi = BpcPaftaTipi.Priz });

        if (!vm.SadecaEksikleriEkle || Enum.GetValues<BpcElemanTipi>().Any(t =>
                vm.MahalKalemiGorunurMu(t) != BasitProjeCizKabloServisi.ElemanPrizMi(t)))
            throw new InvalidOperationException("Priz adımı mahal listesinde yalnız priz çeşitlerini göstermeli.");
        vm.MahalListesindenOtomatikYerlestirCommand.Execute(null);
        if (kat.Elemanlar.Count(e => e.BlokAdi == normal.BlokAdi) != 15 ||
            kat.Elemanlar.Count(e => e.Tip == BpcElemanTipi.Buat) != 2 || !kat.Elemanlar.Contains(elle))
            throw new InvalidOperationException("İlk adım oda adetlerini elle yerleştirilen prizi de sayarak tamamlamalı.");

        string[] PrizKimlikleri() => kat.Elemanlar.Where(e => BasitProjeCizKabloServisi.ElemanPrizMi(e.Tip)).Select(e => e.Id).ToArray();
        var prizIds = PrizKimlikleri();
        vm.PrizAsamasi = BpcPrizAsamasi.Buat;
        vm.MahalListesindenOtomatikYerlestirCommand.Execute(null);
        var prizBuatlari = kat.Elemanlar.Where(e => e.Tip == BpcElemanTipi.Buat && e.PaftaTipi == BpcPaftaTipi.Priz).ToList();
        if (prizBuatlari.Count(b => b.OdaId == salon.Id) != 2 || prizBuatlari.Count(b => b.OdaId == ofis.Id) != 1 ||
            !prizBuatlari.Contains(elleBuat) || !kat.Elemanlar.Contains(isikBuati) || !prizIds.SequenceEqual(PrizKimlikleri()))
            throw new InvalidOperationException("Buatlar mahal başına hesaplanmalı, doğrudan pano prizleri ile aydınlatma buatları sayılmamalı.");
        vm.TemizIsaretle();
        vm.MahalListesindenOtomatikYerlestirCommand.Execute(null);
        if (vm.IsDirty) throw new InvalidOperationException("Eksikleri yeniden tamamlama mevcut buatları değiştirmemeli.");
        vm.GeriAlGenelCommand.Execute(null);
        if (kat.Elemanlar.Count(e => e.Tip == BpcElemanTipi.Buat) != 2 || !prizIds.SequenceEqual(PrizKimlikleri()))
            throw new InvalidOperationException("Buat adımının geri alınması yalnız eklenen buatları kaldırmalı.");
        vm.IleriAlGenelCommand.Execute(null);
        if (prizBuatlari.Any(b => !kat.Elemanlar.Any(e => e.Id == b.Id)))
            throw new InvalidOperationException("İleri almada buat kimlikleri korunmalı.");
        vm.PrizAsamasi = BpcPrizAsamasi.Priz;
        vm.SadecaEksikleriEkle = false;
        vm.MahalListesindenOtomatikYerlestirCommand.Execute(null);
        if (!kat.Elemanlar.Any(e => e.Id == elle.Id) || prizBuatlari.Any(b => !kat.Elemanlar.Any(e => e.Id == b.Id)))
            throw new InvalidOperationException("Priz adımında tam yenileme, kullanıcı prizini ve önceki buatları korumalı.");
    }
}
