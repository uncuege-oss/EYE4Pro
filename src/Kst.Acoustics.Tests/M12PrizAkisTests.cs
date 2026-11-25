using System.Text.Json;
using Kst.Acoustics.App.Electric.BasitProjeCiz.ViewModels;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Bina.Services;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

namespace Kst.Acoustics.Tests;

public static class M12PrizAkisTests
{
    public static void M50denPrizPaftasinaGecisVeYenidenAcma()
    {
        var dizin = Directory.CreateTempSubdirectory();
        string binaYolu = Path.Combine(dizin.FullName, "ofis.e4bina.json");
        string projeYolu = Path.Combine(dizin.FullName, "elektrik.e4p12");
        try
        {
            var oda = new BinaOda { Ad = "Ofis", AlanM2 = 80, Poligon = [new(0, 0), new(10, 0), new(10, 8), new(0, 8)] };
            var binaKat = new BinaKat { Ad = "Zemin", KatNo = 1, OriginOffset = new(128, -32), Odalar = [oda] };
            BinaGeometriServisi.Kaydet(new BinaGeometri { Katlar = [binaKat] }, binaYolu);
            var vm = new BasitProjeCizViewModel();
            vm.BinaGeometrisiYukle(binaYolu);
            var kat = vm.SeciliKat!;

            void Yerlestir(BpcArmatürTanim sembol, double x, double y)
            {
                vm.ElemanEkleModeBaslatCommand.Execute(sembol);
                vm.EklenecekSembolKanvasaEkle(x, y);
            }

            Yerlestir(vm.AsamaSembolleri.Single(s => s.BlokAdi == "E4P_ANA_PANO"), 3, 0.1);
            string panoId = kat.Elemanlar.Single().Id;
            vm.MahalePanoAta(oda.Id, panoId);
            vm.AydinlatmaAsamasi = BpcAydinlatmaAsamasi.Armatur;
            Yerlestir(vm.Armatürler.First(s => s.GucW > 0), 5, 4);
            vm.AydinlatmaAsamasi = BpcAydinlatmaAsamasi.Anahtar;
            Yerlestir(vm.Anahtarlar.First(s => s.BlokAdi == "E4P_ANAHTAR"), 1, 0.1);
            vm.AydinlatmaAsamasi = BpcAydinlatmaAsamasi.Buat;
            vm.MahalListesindenOtomatikYerlestirCommand.Execute(null);
            vm.AydinlatmaAsamasi = BpcAydinlatmaAsamasi.MahalKablosu;
            vm.MahalAydinlatmaKablolariniCekCommand.Execute(null);
            vm.AydinlatmaAsamasi = BpcAydinlatmaAsamasi.PanoKablosu;
            vm.AtanmisPanolaraKabloCekCommand.Execute(null);
            if (!vm.PanoAydinlatmaSigortalariniOlustur()) throw new InvalidOperationException(vm.DurumMesaji);
            vm.AydinlatmaAsamasi = BpcAydinlatmaAsamasi.Sigortalar;
            string aydinlatma = AydinlatmaKayitlari(kat);

            vm.AktifPafta = BpcPaftaTipi.Priz;
            if (!vm.PrizAkisiAktif || vm.PrizAsamasi != BpcPrizAsamasi.Priz)
                throw new InvalidOperationException("Priz paftası ilk olarak priz yerleşimini açmalı.");

            void PrizSatiriEkle(string blok, int adet)
            {
                var sembol = vm.Prizler.Single(s => s.BlokAdi == blok);
                var satir = new BpcMahalSatiri
                {
                    Tip = BasitProjeCizKutuphaneServisi.TanimTipi(sembol), ArmatürTanimId = sembol.Id,
                    YerlesimTipi = "Homojen Duvar", MontajTipi = "Duvar", DuvarAraligiM = 1.0
                };
                var ayar = satir.HucreGetirVeyaOlustur(oda.Id);
                ayar.Aktif = true;
                ayar.Adet = adet;
                kat.MahalListesi.Add(satir);
            }

            PrizSatiriEkle("E4P_PRIZ", 8);
            PrizSatiriEkle("E4P_MUSTAKIL_PRIZ", 1);
            vm.MahalListesindenOtomatikYerlestirCommand.Execute(null);
            var standart = kat.Elemanlar.Where(e => e.BlokAdi == "E4P_PRIZ").Select(e => e.Id).ToHashSet();
            var mustakil = kat.Elemanlar.Single(e => e.BlokAdi == "E4P_MUSTAKIL_PRIZ");
            if (standart.Count != 8 || kat.Elemanlar.Count(e => e.PaftaTipi == BpcPaftaTipi.Priz) != 9 ||
                kat.PrizDevreleri.Count != 0)
                throw new InvalidOperationException("Priz adımı mahal listesindeki standart ve müstakil prizleri ayrı ayrı yerleştirmeli.");

            vm.PrizAsamasi = BpcPrizAsamasi.Buat;
            vm.MahalListesindenOtomatikYerlestirCommand.Execute(null);
            var buatlar = kat.Elemanlar.Where(e => e.PaftaTipi == BpcPaftaTipi.Priz && e.Tip == BpcElemanTipi.Buat)
                .Select(e => e.Id).ToHashSet();
            if (buatlar.Count != 2 || kat.PrizDevreleri.Count != 0)
                throw new InvalidOperationException("Sekiz normal priz için ikinci adım iki buat oluşturmalı.");

            vm.PrizAsamasi = BpcPrizAsamasi.MahalKablosu;
            vm.PrizMahalKablolariniCekCommand.Execute(null);
            if (kat.PrizDevreleri.Count != 2 || !standart.SetEquals(kat.PrizDevreleri.SelectMany(d => d.PrizElemanIds)) ||
                kat.PrizDevreleri.Any(d => !buatlar.Contains(d.BuatId!) || d.PrizElemanIds.Count > 7) ||
                kat.BuatPanoBaglantilari.Any(b => b.LinyePrefix == "P"))
                throw new InvalidOperationException("Mahal içinde sekiz priz iki buat arasında dağıtılmalı. Müstakil priz pano adımını beklemeli.");

            vm.PrizAsamasi = BpcPrizAsamasi.PanoKablosu;
            vm.PrizPanoKablolariniCekCommand.Execute(null);
            var hatlar = kat.BuatPanoBaglantilari.Where(b => b.LinyePrefix == "P").ToList();
            var cikislar = buatlar.Append(mustakil.Id).ToHashSet();
            if (hatlar.Count != 3 || !cikislar.SetEquals(hatlar.Select(b => b.BuatId)) ||
                hatlar.Any(b => b.PanoId != panoId || !kat.Kablolar.Any(k => k.DevreId == b.Id && k.Rota.Count >= 2)))
                throw new InvalidOperationException("İki buat ve müstakil priz, önceki pano atamasını kullanarak çizilmeli.");
            if (AydinlatmaKayitlari(kat) != aydinlatma)
                throw new InvalidOperationException("Priz çizimi mevcut aydınlatma elemanlarını, hatlarını, sigortalarını veya oda atamasını değiştirdi.");
            vm.GeriAlGenelCommand.Execute(null);
            if (kat.BuatPanoBaglantilari.Any(b => b.LinyePrefix == "P") || kat.PrizDevreleri.Count != 2 ||
                AydinlatmaKayitlari(kat) != aydinlatma)
                throw new InvalidOperationException("Son adımı geri alma yalnız priz pano bağlantılarını geri almalı.");
            vm.IleriAlGenelCommand.Execute(null);

            if (!vm.ProjeyiKaydet(projeYolu)) throw new InvalidOperationException(vm.DurumMesaji);
            var yuklenen = JsonSerializer.Deserialize<BasitProjeCizProje>(File.ReadAllText(projeYolu), BasitProjeCizViewModel.ProjeJsonAyarlari)!;
            var acilan = new BasitProjeCizViewModel();
            acilan.ProjeyiYukle(yuklenen, projeYolu);
            var sonKat = acilan.SeciliKat!;
            var prizHatIds = hatlar.Select(b => b.Id).ToHashSet();
            if (!acilan.PrizAkisiAktif || acilan.PrizAsamasi != BpcPrizAsamasi.PanoKablosu || acilan.IsDirty ||
                !prizHatIds.SetEquals(sonKat.BuatPanoBaglantilari.Where(b => b.LinyePrefix == "P").Select(b => b.Id)) ||
                AydinlatmaKayitlari(sonKat) != aydinlatma)
                throw new InvalidOperationException("Dosya yeniden açıldığında son priz aşaması ve her iki paftanın verileri korunmalı.");
        }
        finally
        {
            File.Delete(projeYolu);
            File.Delete(binaYolu);
            dizin.Delete();
        }
    }

    private static string AydinlatmaKayitlari(BasitProjeKat kat)
    {
        var hatlar = kat.BuatPanoBaglantilari.Where(b => b.LinyePrefix == "A").ToList();
        var ids = hatlar.Select(b => b.Id).Concat(kat.Devreler.Select(d => d.Id)).ToHashSet();
        return JsonSerializer.Serialize(new
        {
            Elemanlar = kat.Elemanlar.Where(e => e.PaftaTipi == BpcPaftaTipi.Aydinlatma),
            kat.Devreler, Hatlar = hatlar, Kablolar = kat.Kablolar.Where(k => ids.Contains(k.DevreId))
        });
    }
}
