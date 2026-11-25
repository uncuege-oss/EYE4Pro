using Kst.Acoustics.App.Electric.BasitProjeCiz.ViewModels;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

namespace Kst.Acoustics.Tests;

public static class M12AydinlatmaYerlesimTests
{
    public static void SensorluArmaturlereSensorlerIkinciAdimdaEklenir()
    {
        var kutuphane = new BasitProjeCizKutuphaneServisi();
        var servis = new BasitProjeCizOtomatikYerlestirServisi(new());
        var oda = new BasitProjeOda { Poligon = [new(0, 0), new(8, 0), new(8, 5), new(0, 5)] };
        var kat = new BasitProjeKat();
        kat.Odalar.Add(oda);
        var satir = new BpcMahalSatiri { Tip = BpcElemanTipi.Armatür, ArmatürTanimId = kutuphane.Armatürler.First().Id };
        var ayar = satir.HucreGetirVeyaOlustur(oda.Id);
        ayar.Aktif = true;
        ayar.GenelAyarKullan = false;
        ayar.Sensorlu = true;
        ayar.Adet = 2;
        kat.MahalListesi.Add(satir);

        var (armaturler, _) = servis.TumMahalListesindenYerlestir(kat, kutuphane, true, asama: BpcAydinlatmaAsamasi.Armatur);
        if (armaturler.Count != 2 || armaturler.Any(e => !e.SensorKontrollu || !BasitProjeCizKabloServisi.ElemanArmaturMi(e.Tip)))
            throw new InvalidOperationException("Sensörlü armatür kuralı ilk adımda yalnız armatürleri üretmeli.");
        foreach (var armatur in armaturler) kat.Elemanlar.Add(armatur);
        var (sensorler, _) = servis.TumMahalListesindenYerlestir(kat, kutuphane, true, asama: BpcAydinlatmaAsamasi.Anahtar);
        if (sensorler.Count != 2 || sensorler.Any(e => !e.SensorKontrollu || e.Tip != BpcElemanTipi.Sensör))
            throw new InvalidOperationException("İkinci adımda birer sensör, üçüncü adımdan önce sıfır buat bekleniyor.");
        foreach (var sensor in sensorler) kat.Elemanlar.Add(sensor);
        if (servis.TumMahalListesindenYerlestir(kat, kutuphane, true, asama: BpcAydinlatmaAsamasi.Anahtar).Yeni.Count != 0)
            throw new InvalidOperationException("Sensörler ikinci çalıştırmada çoğalmamalı.");
    }

    public static void AdimlarAyridirVeTekIslemdeGeriAlinir()
    {
        var vm = new BasitProjeCizViewModel();
        var kat = new BasitProjeKat();
        var oda = new BasitProjeOda { Poligon = [new(0, 0), new(10, 0), new(10, 6), new(0, 6)] };
        kat.Odalar.Add(oda);
        kat.Acikliklar.Add(new() { Tip = BpcAciklikTipi.Kapı, Bas = new(2, 0), Bit = new(3, 0) });
        vm.Katlar.Add(kat);
        vm.SeciliKat = kat;

        void SatirEkle(BpcArmatürTanim tanim, int adet)
        {
            var satir = new BpcMahalSatiri
            {
                Tip = BasitProjeCizKutuphaneServisi.TanimTipi(tanim), ArmatürTanimId = tanim.Id, MontajTipi = tanim.MontajTipi
            };
            var hucre = satir.HucreGetirVeyaOlustur(oda.Id);
            hucre.Aktif = true;
            hucre.Adet = adet;
            kat.MahalListesi.Add(satir);
        }
        var lamba = vm.Armatürler.First();
        SatirEkle(lamba, 3);
        SatirEkle(vm.Anahtarlar.First(t => t.BlokAdi == "E4P_3LU_KOMUTAT"), 1);
        SatirEkle(vm.Anahtarlar.First(t => t.BlokAdi == "E4P_SENSOR"), 4);
        SatirEkle(vm.Prizler.First(), 1);
        var manuel = new BasitProjeEleman
        {
            Tip = BasitProjeCizKutuphaneServisi.TanimTipi(lamba), BlokAdi = lamba.BlokAdi, Ad = "Elle düzenlenen",
            X = 7, Y = 5, OdaId = oda.Id
        };
        kat.Elemanlar.Add(manuel);
        var prizBuati = new BasitProjeEleman
        {
            Tip = BpcElemanTipi.Buat, PaftaTipi = BpcPaftaTipi.Priz, OdaId = oda.Id,
            OtomatikYerlesimKaynak = BasitProjeCizOtomatikYerlestirServisi.MahalListesiKaynak
        };
        kat.Elemanlar.Add(prizBuati);

        void Uygula(BpcAydinlatmaAsamasi asama)
        {
            vm.AydinlatmaAsamasi = asama;
            vm.MahalListesindenOtomatikYerlestirCommand.Execute(null);
        }
        Uygula(BpcAydinlatmaAsamasi.Armatur);
        var armaturler = kat.Elemanlar.Where(e => e.Tip == manuel.Tip).Select(e => e.Id).ToArray();
        if (vm.MahalKalemiGorunurMu(BpcElemanTipi.Sensör) || armaturler.Length != 3 || kat.Elemanlar.Count != 4)
            throw new InvalidOperationException("Armatür adımı mevcut manuel armatürü saymalı ve yalnız eksik armatürleri üretmeli.");

        Uygula(BpcAydinlatmaAsamasi.Anahtar);
        var anahtar = kat.Elemanlar.Single(e => e.Tip == BpcElemanTipi.UcluKomutator);
        if (vm.MahalKalemiGorunurMu(manuel.Tip) || !vm.MahalKalemiGorunurMu(BpcElemanTipi.Sensör) ||
            anahtar.X < 1.5 || anahtar.X > 3.5 || anahtar.Y > 0.3 ||
            kat.Elemanlar.Count != 9 || kat.Elemanlar.Any(e => e.Tip == BpcElemanTipi.Priz))
            throw new InvalidOperationException("Anahtar adımında komütatör kapı yanına, sensörler ayrı kumanda olarak yerleşmeli.");

        Uygula(BpcAydinlatmaAsamasi.Buat);
        var buatlar = kat.Elemanlar.Where(e => e.Tip == BpcElemanTipi.Buat && e.PaftaTipi == BpcPaftaTipi.Aydinlatma)
            .Select(e => e.Id).ToArray();
        if (buatlar.Length != 2 || !armaturler.All(id => kat.Elemanlar.Any(e => e.Id == id)))
            throw new InvalidOperationException("Beş kumandaya iki aydınlatma buatı gerekir; önceki adımın elemanları korunmalı.");
        Uygula(BpcAydinlatmaAsamasi.Buat);
        vm.ElemanGeriAlCommand.Execute(null);
        if (kat.Elemanlar.Count != 9 || !kat.Elemanlar.Any(e => e.Id == prizBuati.Id))
            throw new InvalidOperationException("Tek geri alma yalnız buat adımını kaldırmalı, değişiklik yapmayan tekrar geçmişi doldurmamalı.");
        vm.ElemanIleriAlCommand.Execute(null);
        if (!buatlar.All(id => kat.Elemanlar.Any(e => e.Id == id)))
            throw new InvalidOperationException("İleri al aydınlatma buatlarının kimliklerini korumalı.");
        vm.SadecaEksikleriEkle = false;
        Uygula(BpcAydinlatmaAsamasi.Armatur);
        if (kat.Elemanlar.Count != 11 || !kat.Elemanlar.Any(e => e.Id == anahtar.Id) ||
            !buatlar.All(id => kat.Elemanlar.Any(e => e.Id == id)) ||
            !kat.Elemanlar.Any(e => e.Id == manuel.Id && e.X == 7 && e.Ad == manuel.Ad))
            throw new InvalidOperationException("Tam yenileme yalnız bu adımdaki otomatik elemanları yeniden üretmeli.");
    }
}
