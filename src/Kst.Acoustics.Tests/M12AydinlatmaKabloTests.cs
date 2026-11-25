using System.Text.Json;
using Kst.Acoustics.App.Electric.BasitProjeCiz.ViewModels;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;
using Kst.Acoustics.Core.Electric.Calculation;

namespace Kst.Acoustics.Tests;

public static class M12AydinlatmaKabloTests
{
    public static void AsamalarAtanmisPanoyuKullanirVeAyriGeriAlinir()
    {
        var vm = Ornek();
        var kat = vm.SeciliKat!;
        vm.AydinlatmaAsamasi = BpcAydinlatmaAsamasi.MahalKablosu;
        vm.MahalAydinlatmaKablolariniCek();
        if (kat.Devreler.Single().BuatId != "buat" || kat.Kablolar.Count == 0 || kat.BuatPanoBaglantilari.Count != 0)
            throw new InvalidOperationException("Dördüncü adım mahal içinde kalmalı.");
        vm.AydinlatmaAsamasi = BpcAydinlatmaAsamasi.PanoKablosu;
        vm.AtanmisPanolaraKabloCek();
        if (kat.BuatPanoBaglantilari.Count != 0 || vm.PanoAydinlatmaSigortalariniOlustur())
            throw new InvalidOperationException("Atamasız mahal en yakın panoya gitmemeli veya tamamlanmamalı.");
        kat.Elemanlar.Single(e => e.Id == "pano").BagliOdaIds.Add("oda");
        vm.AtanmisPanolaraKabloCek();
        var bag = kat.BuatPanoBaglantilari.Single();
        if (bag.PanoId != "pano" || !kat.Kablolar.Any(k => k.DevreId == bag.Id && k.Rota.Count >= 2))
            throw new InvalidOperationException("Beşinci adım atanmış panoya rota çizmedi.");
        vm.GeriAlGenelCommand.Execute(null);
        if (kat.BuatPanoBaglantilari.Count != 0 || kat.Devreler.Count != 1)
            throw new InvalidOperationException("Beşinci adımı geri almak mahal içi devreyi korumalı.");
        vm.IleriAlGenelCommand.Execute(null);
        if (kat.BuatPanoBaglantilari.Single().Id != bag.Id)
            throw new InvalidOperationException("İleri alma linye kimliğini değiştirdi.");
    }

    public static void SigortaTamamlamaManuelDegisikligiVeAktarimiKorur()
    {
        var vm = Ornek();
        var kat = vm.SeciliKat!;
        BasitProjeEleman Pano() => kat.Elemanlar.Single(e => e.Id == "pano");
        Pano().BagliOdaIds.Add("oda");
        vm.MahalAydinlatmaKablolariniCek();
        vm.AtanmisPanolaraKabloCek();
        if (!vm.PanoAydinlatmaSigortalariniOlustur()) throw new InvalidOperationException(vm.DurumMesaji);
        var linyeId = kat.BuatPanoBaglantilari.Single().Id;
        var otomatik = Pano().AydinlatmaSigortalari![linyeId];
        if (otomatik.Manuel || otomatik.Amper <= 0 || Pano().AydinlatmaSigortalari![AydinlatmaTasarimServisi.Giris].Amper <= 0)
            throw new InvalidOperationException("Giriş ve çıkış sigortaları birlikte oluşturulmalı.");
        vm.PanoSigortasiniDuzenle(Pano(), linyeId, 16);
        vm.GeriAlGenelCommand.Execute(null);
        if (Pano().AydinlatmaSigortalari![linyeId] != otomatik) throw new InvalidOperationException("Sigorta undo hatası.");
        vm.IleriAlGenelCommand.Execute(null);
        vm.PanoSigortasiniDuzenle(Pano(), AydinlatmaTasarimServisi.Giris, 32);
        vm.PanoSigortasiniDuzenle(Pano(), AydinlatmaTasarimServisi.YedekOnEk + "test", 10);
        vm.PanoAydinlatmaSigortalariniOlustur();
        var json = JsonSerializer.Serialize(vm.Proje, BasitProjeCizViewModel.ProjeJsonAyarlari);
        var proje = JsonSerializer.Deserialize<BasitProjeCizProje>(json, BasitProjeCizViewModel.ProjeJsonAyarlari)!;
        var aktarma = TekHatAktarimServisi.Donustur(proje, []);
        if (!aktarma.Basarili) throw new InvalidOperationException(aktarma.Hata);
        var pano11 = aktarma.Proje!.KokPano!;
        var cikis = pano11.Linyeler.Single(l => l.YukTipiAd == "Aydınlatma");
        LinyeHesapServisi.LinyeHesapla(cikis, 230, 400, 0.95);
        if (cikis.SigortaAmper != 16 || pano11.ManuelGirisSigortaA != 32 ||
            pano11.Linyeler.Single(l => l.YukTipiAd == "Yedek").SigortaAmper != 10)
            throw new InvalidOperationException("Elle seçilen giriş, çıkış ve yedek sigortalar M11'e aktarılmalı.");
        vm.PanoSigortasiniDuzenle(Pano(), linyeId, 0);
        vm.PanoAydinlatmaSigortalariniOlustur();
        if (Pano().AydinlatmaSigortalari![linyeId].Amper != 0)
            throw new InvalidOperationException("Elle silinen sigorta yeniden üretilmemeli.");
    }

    public static void TasinanArmaturYeniMahaldeKablolanir()
    {
        var vm = Ornek();
        var kat = vm.SeciliKat!;
        vm.MahalAydinlatmaKablolariniCek();
        kat.Odalar.Add(new() { Id = "mutfak", Ad = "Mutfak", Poligon = [new(8, 0), new(16, 0), new(16, 8), new(8, 8)] });
        kat.Elemanlar.Add(new() { Id = "a2", Tip = BpcElemanTipi.Anahtar, OdaId = "mutfak", X = 16, Y = 1 });
        kat.Elemanlar.Add(new() { Id = "b2", Tip = BpcElemanTipi.Buat, OdaId = "mutfak", X = 16, Y = 2 });
        var lamba = kat.Elemanlar.Single(e => e.Id == "lamba");
        lamba.X = 12;
        lamba.OdaId = "mutfak";
        vm.MahalAydinlatmaKablolariniCek();
        var devre = kat.Devreler.Single(d => d.ArmaturElemanIds.Contains(lamba.Id));
        if (devre.AnahtarElemanId != "a2" || devre.BuatId != "b2" ||
            AydinlatmaTasarimServisi.EksikBaglantilariBul(kat, false).Count != 0)
            throw new InvalidOperationException("Taşınan armatür yeni mahalin kumandasına ve buatına bağlanmalı.");
    }

    private static BasitProjeCizViewModel Ornek()
    {
        var vm = new BasitProjeCizViewModel();
        var kat = new BasitProjeKat();
        kat.Odalar.Add(new() { Id = "oda", Ad = "Salon", Poligon = [new(0, 0), new(8, 0), new(8, 8), new(0, 8)] });
        kat.Elemanlar.Add(new() { Id = "lamba", OdaId = "oda", Tip = BpcElemanTipi.Armatür, CustomGucW = 40, X = 4, Y = 4 });
        kat.Elemanlar.Add(new() { Id = "anahtar", OdaId = "oda", Tip = BpcElemanTipi.Anahtar, X = 0, Y = 1 });
        kat.Elemanlar.Add(new() { Id = "buat", OdaId = "oda", Tip = BpcElemanTipi.Buat, X = 0, Y = 2 });
        kat.Elemanlar.Add(new() { Id = "pano", OdaId = "oda", Tip = BpcElemanTipi.KatPanosu, X = 0, Y = 6 });
        vm.Katlar.Add(kat);
        vm.SeciliKat = kat;
        return vm;
    }
}
