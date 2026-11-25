using System.Text.Json;
using Kst.Acoustics.App.Electric.BasitProjeCiz.ViewModels;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Bina.Services;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

namespace Kst.Acoustics.Tests;

public static class M12AydinlatmaAkisTests
{
    public static void M50denSigortalara()
    {
        var dizin = Directory.CreateTempSubdirectory();
        var binaYolu = Path.Combine(dizin.FullName, "zemin.e4bina.json");
        var projeYolu = Path.Combine(dizin.FullName, "aydinlatma.e4p12");
        try
        {
            var oda = new BinaOda { Ad = "Ofis", AlanM2 = 48, Poligon = [new(0, 0), new(8, 0), new(8, 6), new(0, 6)] };
            var kat = new BinaKat { KatNo = 1, Ad = "Zemin", Odalar = [oda], OriginOffset = new(100, 60) };
            kat.Acikliklar.Add(new BinaAciklik { Bas = new(1, 0), Bit = new(2, 0), Tip = BinaAciklikTipi.Kapi });
            BinaGeometriServisi.Kaydet(new BinaGeometri { Katlar = [kat] }, binaYolu);
            var vm = new BasitProjeCizViewModel();
            vm.BinaGeometrisiYukle(binaYolu);
            var plan = vm.SeciliKat!;
            if (vm.AydinlatmaAsamasi != BpcAydinlatmaAsamasi.Pano || !vm.IsDirty)
                throw new InvalidOperationException("Yeni M50 planı kaydedilebilir pano yerleşiminde açılmalı.");
            vm.ElemanEkleModeBaslatCommand.Execute(vm.AsamaSembolleri.Single(s => s.BlokAdi == "E4P_ANA_PANO"));
            vm.EklenecekSembolKanvasaEkle(4, 0.1);
            var pano = plan.Elemanlar.Single();
            vm.MahalePanoAta(oda.Id, pano.Id);

            void MahalKalemiYerlestir(BpcAydinlatmaAsamasi asama, BpcArmatürTanim sembol, int adet)
            {
                vm.AydinlatmaAsamasi = asama;
                var satir = new BpcMahalSatiri
                {
                    Tip = BasitProjeCizKutuphaneServisi.TanimTipi(sembol),
                    ArmatürTanimId = sembol.Id, MontajTipi = sembol.MontajTipi
                };
                var hucre = satir.HucreGetirVeyaOlustur(oda.Id);
                hucre.Aktif = true;
                hucre.Adet = adet;
                plan.MahalListesi.Add(satir);
                vm.MahalListesindenOtomatikYerlestirCommand.Execute(null);
            }

            MahalKalemiYerlestir(BpcAydinlatmaAsamasi.Armatur, vm.Armatürler.First(s => s.GucW > 0), 2);
            MahalKalemiYerlestir(BpcAydinlatmaAsamasi.Anahtar, vm.Anahtarlar.First(s => s.BlokAdi == "E4P_ANAHTAR"), 1);
            vm.AydinlatmaAsamasi = BpcAydinlatmaAsamasi.Buat;
            vm.MahalListesindenOtomatikYerlestirCommand.Execute(null);
            vm.AydinlatmaAsamasi = BpcAydinlatmaAsamasi.MahalKablosu;
            vm.MahalAydinlatmaKablolariniCekCommand.Execute(null);
            if (plan.BuatPanoBaglantilari.Count != 0 || plan.Devreler.Single().ArmaturElemanIds.Count != 2)
                throw new InvalidOperationException("Mahal içi kablolar ayrı bir adımda üretilmeli.");
            vm.AydinlatmaAsamasi = BpcAydinlatmaAsamasi.PanoKablosu;
            vm.AtanmisPanolaraKabloCekCommand.Execute(null);
            var cikisId = plan.BuatPanoBaglantilari.Single().Id;
            if (!vm.PanoAydinlatmaSigortalariniOlustur())
                throw new InvalidOperationException(vm.DurumMesaji);
            vm.AydinlatmaAsamasi = BpcAydinlatmaAsamasi.Sigortalar;
            vm.PanoSigortasiniDuzenle(pano, cikisId, 16);
            if (!vm.ProjeyiKaydet(projeYolu)) throw new InvalidOperationException(vm.DurumMesaji);
            vm.YeniProje();
            vm.ProjeyiYukle(JsonSerializer.Deserialize<BasitProjeCizProje>(File.ReadAllText(projeYolu),
                BasitProjeCizViewModel.ProjeJsonAyarlari)!, projeYolu);
            var yuklenenPano = vm.SeciliKat!.Elemanlar.Single(e => e.Id == pano.Id);
            if (vm.AydinlatmaAsamasi != BpcAydinlatmaAsamasi.Sigortalar || vm.IsDirty ||
                !yuklenenPano.BagliOdaIds.Contains(oda.Id) || yuklenenPano.AydinlatmaSigortalari is not { } sigortalar ||
                sigortalar[cikisId] != new BpcPanoSigortasi(16, true) || sigortalar[AydinlatmaTasarimServisi.Giris].Amper <= 0)
                throw new InvalidOperationException("Aşama, pano ataması ve sigortalar proje açılışında korunmalı.");
        }
        finally
        {
            File.Delete(projeYolu);
            File.Delete(binaYolu);
            dizin.Delete();
        }
    }
}
