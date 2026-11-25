using System.Text.Json;
using Kst.Acoustics.App.Electric.BasitProjeCiz.ViewModels;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

namespace Kst.Acoustics.Tests;

public static class M12PrizSigortaTests
{
    private const string Giris = AydinlatmaTasarimServisi.Giris;

    public static void LinyeVeToplamYukSigortalari()
    {
        var vm = Ornek();
        var kat = vm.SeciliKat!;
        var prizHatti = Hat(kat, "pb");
        var mustakilHat = Hat(kat, "mustakil");
        var aydinlatmaHatti = Hat(kat, "ab");
        Sigorta(vm, "ana", Giris, 13);
        Tamamla(vm);
        // Tek fazda I = W / (230 × 0,95). 2500 W → 13 A, 4200 W → 20 A,
        // 1000 W müstakil priz → 16 A alt sınırı, 7700 W ortak giriş → 40 A.
        Sigorta(vm, "ana", prizHatti, 20);
        Sigorta(vm, "ana", mustakilHat, 16);
        Sigorta(vm, "ana", aydinlatmaHatti, 13);
        Sigorta(vm, "ana", Giris, 40);
        var hesap = vm.PrizSigortaHesaplari().Single();
        if (hesap.ToplamGucW != 7700 || hesap.GirisSigortaA != 40)
            throw new InvalidOperationException($"Aydınlatma ve priz toplamı 7700 W, giriş 40 A bekleniyordu; bulunan {hesap.ToplamGucW} W, {hesap.GirisSigortaA} A.");

        vm.TemizIsaretle();
        Tamamla(vm);
        if (vm.IsDirty) throw new InvalidOperationException("Aynı sonucu tekrar hesaplamak projeyi kirletmemeli.");
        vm.GeriAlGenelCommand.Execute(null);
        Sigorta(vm, "ana", Giris, 13);
        if (Pano(vm).AydinlatmaSigortalari!.ContainsKey(prizHatti))
            throw new InvalidOperationException("Tek geri alma tamamlanan priz sigortalarını geri almalı.");
        vm.IleriAlGenelCommand.Execute(null);
        Sigorta(vm, "ana", Giris, 40);
        Eleman(vm, "priz").CustomGucW = 6600;
        Tamamla(vm);
        Sigorta(vm, "ana", prizHatti, 32);
        Sigorta(vm, "ana", Giris, 50);

        // İkinci durumda her linye katalog içinde, yalnız ortak giriş sınırın üstünde.
        foreach (var (normal, mustakil) in new[] { (2_000_000, 1000), (750_000, 750_000) })
        {
            Eleman(vm, "priz").CustomGucW = normal;
            Eleman(vm, "mustakil").CustomGucW = mustakil;
            vm.TemizIsaretle();
            if (vm.PrizProjesiniTamamla() || vm.IsDirty)
                throw new InvalidOperationException("Katalog üst sınırını aşan akım için geçersiz sigorta kaydedilmemeli.");
            Sigorta(vm, "ana", prizHatti, 32);
            Sigorta(vm, "ana", Giris, 50);
        }
        Eleman(vm, "priz").CustomGucW = 6600;
        Eleman(vm, "mustakil").CustomGucW = 1000;

        // Taşınmış bir priz artık çizili güzergâhın ucunda değildir.
        Eleman(vm, "priz").X += 1;
        vm.TemizIsaretle();
        if (vm.PrizProjesiniTamamla() || vm.IsDirty)
            throw new InvalidOperationException("Güncel olmayan bir priz hattı sonuçları değiştirmeden bildirilmeli.");
        Sigorta(vm, "ana", Giris, 50);

        vm.PrizAsamasi = BpcPrizAsamasi.Priz;
        foreach (var id in new[] { "priz", "mustakil" })
        {
            vm.SeciliEleman = Eleman(vm, id);
            vm.SeciliElemanSilCommand.Execute(null);
        }
        vm.PrizAsamasi = BpcPrizAsamasi.PanoKablosu;
        vm.PrizPanoKablolariniCek();
        vm.PrizAsamasi = BpcPrizAsamasi.Sigortalar;
        Tamamla(vm);
        Sigorta(vm, "ana", Giris, 13);
        if (Pano(vm).AydinlatmaSigortalari!.Count != 2)
            throw new InvalidOperationException("Son prizler silindiğinde yalnız giriş ve aydınlatma çıkışı kalmalı.");
    }

    public static void ElleSecimKayitAktarimVeOtomatigeDonus()
    {
        var vm = Ornek();
        Tamamla(vm);
        var kat = vm.SeciliKat!;
        var hat = Hat(kat, "pb");
        var mustakilHat = Hat(kat, "mustakil");
        vm.PanoSigortasiniDuzenle(Pano(vm), hat, 25);
        vm.PanoSigortasiniDuzenle(Pano(vm), Giris, 63);
        vm.PanoSigortasiniDuzenle(Pano(vm), mustakilHat, 0);
        Tamamla(vm);
        vm.AktifPafta = BpcPaftaTipi.Aydinlatma;
        if (!vm.PanoAydinlatmaSigortalariniOlustur()) throw new InvalidOperationException(vm.DurumMesaji);
        vm.AktifPafta = BpcPaftaTipi.Priz;
        vm.PrizAsamasi = BpcPrizAsamasi.Sigortalar;
        var dizin = Directory.CreateTempSubdirectory();
        var yol = Path.Combine(dizin.FullName, "sigortalar.e4p12");
        try
        {
            if (!vm.ProjeyiKaydet(yol)) throw new InvalidOperationException(vm.DurumMesaji);
            var acilan = JsonSerializer.Deserialize<BasitProjeCizProje>(File.ReadAllText(yol), BasitProjeCizViewModel.ProjeJsonAyarlari)!;
            vm = new BasitProjeCizViewModel();
            vm.ProjeyiYukle(acilan, yol);
            if (!vm.PrizAkisiAktif || vm.PrizAsamasi != BpcPrizAsamasi.Sigortalar)
                throw new InvalidOperationException("Priz sigortaları adımı kayıttan geri yüklenmeli.");
            Sigorta(vm, "ana", hat, 25, true);
            Sigorta(vm, "ana", Giris, 63, true);
            Sigorta(vm, "ana", mustakilHat, 0, true);
            var ozet = BasitProjeCizPanoServisi.ProjePanoOzetleri(vm.Proje, []).Single();
            if (ozet.GirisSigortaA != 63 || ozet.Linyeler.Single(l => l.BaglantiId == hat).SigortaA != 25 ||
                ozet.Linyeler.Single(l => l.BaglantiId == mustakilHat).SigortaA != 0)
                throw new InvalidOperationException("Pano özeti kaydedilmiş giriş ve çıkış akımlarını kullanmalı.");
            var m11 = TekHatAktarimServisi.Donustur(vm.Proje, []);
            if (!m11.Basarili || m11.Proje!.KokPano!.ManuelGirisSigortaA != 63 ||
                !m11.Proje.KokPano.Linyeler.Any(l => l.ToplamGucW == 4200 && l.ManuelSigortaAmper == 25))
                throw new InvalidOperationException("M11'e aktarılan giriş ve priz çıkışı elle belirlenen değeri korumalı.");
            vm.PanoSigortasiniOtomatigeAl(Pano(vm), hat);
            Sigorta(vm, "ana", hat, 20);
            vm.GeriAlGenelCommand.Execute(null);
            Sigorta(vm, "ana", hat, 25, true);
            vm.IleriAlGenelCommand.Execute(null);
            Sigorta(vm, "ana", hat, 20);
            vm.PanoSigortasiniOtomatigeAl(Pano(vm), mustakilHat);
            Sigorta(vm, "ana", mustakilHat, 16);
            Sigorta(vm, "ana", Giris, 63, true);
        }
        finally
        {
            File.Delete(yol);
            dizin.Delete();
        }
    }

    public static void AltPanonunPrizYukuUstPanoyaBirKezYansir()
    {
        var vm = Ornek(taliPano: true);
        Tamamla(vm);
        var besleme = vm.SeciliKat!.PanoBeslemeHatlari.Single(h => h.AnaPanoId == "ana" && h.TaliPanoId == "tali");
        Sigorta(vm, "tali", Giris, 40);
        Sigorta(vm, "ana", besleme.Id, 40);
        Sigorta(vm, "ana", Giris, 40);
        if (vm.PrizSigortaHesaplari().Any(o => o.ToplamGucW != 7700))
            throw new InvalidOperationException("Tali panonun toplam 7700 W yükü ana panoda bir kez sayılmalı.");
        Eleman(vm, "ana").Y += 0.5;
        vm.TemizIsaretle();
        if (vm.PrizProjesiniTamamla() || vm.IsDirty)
            throw new InvalidOperationException("Besleme rotası taşınmış ana panoya ulaşana kadar kayıtlı sigortalar korunmalı.");
        Sigorta(vm, "ana", besleme.Id, 40);
    }

    private static BasitProjeCizViewModel Ornek(bool taliPano = false)
    {
        var vm = new BasitProjeCizViewModel();
        var oda = new BinaOda { Id = "oda", Ad = "Salon", Poligon = [new(0, 0), new(10, 0), new(10, 8), new(0, 8)] };
        vm.BinaGeometrisiUygula(new BinaGeometri { Katlar = [new BinaKat { KatNo = 1, Odalar = [oda] }] }, null);
        var kat = vm.SeciliKat!;
        BasitProjeEleman Ekle(string id, BpcElemanTipi tip, double x, double y, double? guc = null)
        {
            var eleman = new BasitProjeEleman { Id = id, Ad = id, Tip = tip, OdaId = oda.Id, X = x, Y = y, CustomGucW = guc, PaftaTipi = vm.AktifPafta };
            kat.Elemanlar.Add(eleman);
            return eleman;
        }
        Ekle("ana", BpcElemanTipi.KatPanosu, 0, 5).BlokAdi = "E4P_ANA_PANO";
        if (taliPano)
            Ekle("tali", BpcElemanTipi.KatPanosu, 0, 6).BlokAdi = "E4P_TALI_PANO";
        var hedef = taliPano ? "tali" : "ana";
        vm.MahalePanoAta(oda.Id, hedef);
        Ekle("lamba", BpcElemanTipi.Armatür, 5, 4, 2500);
        Ekle("anahtar", BpcElemanTipi.Anahtar, 0, 1);
        Ekle("ab", BpcElemanTipi.Buat, 0, 2);
        vm.AydinlatmaAsamasi = BpcAydinlatmaAsamasi.MahalKablosu;
        vm.MahalAydinlatmaKablolariniCek();
        vm.AydinlatmaAsamasi = BpcAydinlatmaAsamasi.PanoKablosu;
        vm.AtanmisPanolaraKabloCek();
        if (!vm.PanoAydinlatmaSigortalariniOlustur()) throw new InvalidOperationException(vm.DurumMesaji);
        // Ayrı mekanik paftadaki yük A+P toplamına girmemeli.
        var klima = Ekle("klima", BpcElemanTipi.KlimaIcUnite, 4, 6, 30000);
        klima.PaftaTipi = BpcPaftaTipi.Mekanik;
        kat.MekanikDevreleri.Add(new BpcMekanikDevre { KlimaElemanId = klima.Id, PanoId = hedef });
        vm.AktifPafta = BpcPaftaTipi.Priz;
        Ekle("priz", BpcElemanTipi.Priz, 3, 0, 4200);
        Ekle("mustakil", BpcElemanTipi.Priz, 8, 0, 1000).BlokAdi = "E4P_MUSTAKIL_PRIZ";
        Ekle("pb", BpcElemanTipi.Buat, 2, 0);
        vm.PrizAsamasi = BpcPrizAsamasi.MahalKablosu;
        vm.PrizMahalKablolariniCek();
        vm.PrizAsamasi = BpcPrizAsamasi.PanoKablosu;
        vm.PrizPanoKablolariniCek();
        return vm;
    }

    private static void Tamamla(BasitProjeCizViewModel vm)
    {
        if (!vm.PrizProjesiniTamamla()) throw new InvalidOperationException(vm.DurumMesaji);
    }

    private static string Hat(BasitProjeKat kat, string kaynak) =>
        kat.BuatPanoBaglantilari.Single(b => b.BuatId == kaynak).Id;
    private static BasitProjeEleman Eleman(BasitProjeCizViewModel vm, string id) =>
        vm.SeciliKat!.Elemanlar.Single(e => e.Id == id);
    private static BasitProjeEleman Pano(BasitProjeCizViewModel vm) => Eleman(vm, "ana");
    private static void Sigorta(BasitProjeCizViewModel vm, string panoId, string yer, int amper, bool manuel = false)
    {
        var kayit = Eleman(vm, panoId).AydinlatmaSigortalari?.GetValueOrDefault(yer);
        if (kayit != new BpcPanoSigortasi(amper, manuel))
            throw new InvalidOperationException($"{panoId}/{yer} için {amper} A (manuel={manuel}) bekleniyordu; bulunan {kayit}.");
    }
}
