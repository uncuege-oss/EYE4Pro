using System.Text.Json;
using Kst.Acoustics.App.Electric.BasitProjeCiz.ViewModels;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

namespace Kst.Acoustics.Tests;

public static class M12PanoAtamaTests
{
    public static void MahalPanosunuDegistirmeKablolariVeGecmisiKorur()
    {
        var vm = new BasitProjeCizViewModel();
        var kat = new BasitProjeKat { KatNo = 1, OriginOffset = new(130, -47) };
        var oda = new BasitProjeOda { Poligon = [new(0, 0), new(8, 0), new(8, 3), new(3, 3), new(3, 6), new(0, 6)] };
        kat.Odalar.Add(oda);
        var eskiKat = new BasitProjeKat { KatNo = 0 };
        var eski = new BasitProjeEleman { Tip = BpcElemanTipi.KatPanosu, BagliOdaIds = [oda.Id] };
        eskiKat.Elemanlar.Add(eski);
        var yeni = new BasitProjeEleman { Tip = BpcElemanTipi.KatPanosu, BlokAdi = "E4P_ALT_PANO" };
        var buat = new BasitProjeEleman { Tip = BpcElemanTipi.Buat, OdaId = oda.Id };
        kat.Elemanlar.Add(yeni);
        kat.Elemanlar.Add(buat);
        var isik = new BpcBuatPanoBaglanti { BuatId = buat.Id, PanoId = eski.Id, LinyePrefix = "A" };
        var priz = new BpcBuatPanoBaglanti { BuatId = buat.Id, PanoId = eski.Id, LinyePrefix = "P" };
        foreach (var b in new[] { isik, priz })
        {
            kat.BuatPanoBaglantilari.Add(b);
            kat.Kablolar.Add(new BpcKablo { DevreId = b.Id, Tip = BpcKabloTipi.PanoKablosu });
        }
        vm.Katlar.Add(eskiKat);
        vm.Katlar.Add(kat);
        vm.SeciliKat = kat;
        // Sağ tık dünya koordinatında gelir. L biçimli mahallin girintisi seçim dışında kalır.
        vm.PickRoomAt(132, -42);
        if (vm.SeciliOda != oda) throw new InvalidOperationException("Ofsetli kat planındaki mahal bulunamadı.");
        vm.PickRoomAt(136, -42);
        if (vm.SeciliOda != null) throw new InvalidOperationException("Mahalin girintisi dikdörtgen sınırla seçildi.");
        vm.TemizIsaretle();

        string? Hedef() => BasitProjeCizKabloServisi.MahaleAtananPano(oda.Id, vm.Katlar.SelectMany(k => k.Elemanlar))?.Id;
        vm.ProjeKilitli = true;
        vm.MahalePanoAta(oda.Id, yeni.Id);
        if (Hedef() != eski.Id || vm.IsDirty) throw new InvalidOperationException("Kilitli projede atama değişti.");
        vm.ProjeKilitli = false;
        vm.MahalePanoAta(oda.Id, yeni.Id);
        if (Hedef() != yeni.Id || eski.BagliOdaIds.Count != 0 || !vm.IsDirty ||
            kat.BuatPanoBaglantilari.Single().Id != priz.Id || kat.Kablolar.Single().DevreId != priz.Id)
            throw new InvalidOperationException("Yalnız eski aydınlatma çıkışı temizlenmeli ve mahal tek panoya atanmalı.");
        vm.GeriAlGenelCommand.Execute(null);
        if (Hedef() != eski.Id || kat.BuatPanoBaglantilari.Count != 2 || kat.Kablolar.Count != 2)
            throw new InvalidOperationException("Tek geri alma, iki katın atamalarını ve kabloyu geri getirmeli.");
        vm.IleriAlGenelCommand.Execute(null);
        if (Hedef() != yeni.Id || kat.Kablolar.Count != 1)
            throw new InvalidOperationException("İleri alma yeni atamayı geri getirmeli.");
        var json = JsonSerializer.Serialize(vm.Proje, BasitProjeCizViewModel.ProjeJsonAyarlari);
        var kayit = JsonSerializer.Deserialize<BasitProjeCizProje>(json, BasitProjeCizViewModel.ProjeJsonAyarlari)!;
        if (BasitProjeCizKabloServisi.MahaleAtananPano(oda.Id, kayit.Katlar.SelectMany(k => k.Elemanlar))?.Id != yeni.Id)
            throw new InvalidOperationException("Mahal pano ataması .e4p12 serileştirmesinde kayboldu.");
        if (!new BasitProjeGeometriBagimliVeri(kayit).BagimliKayitlar().Any(k => k.Tur == "Mahal pano ataması" && k.OdaId == oda.Id))
            throw new InvalidOperationException("M50 geometrisi yeniden yüklenirken pano ataması kontrol edilmeli.");
        vm.MahalePanoAta(oda.Id, null);
        if (Hedef() != null) throw new InvalidOperationException("Mahal ataması kaldırılamadı.");
    }

    public static void AnaTaliAltBeslemeleriKararliVeElleDegistirilebilir()
    {
        var vm = new BasitProjeCizViewModel();
        var kat = new BasitProjeKat();
        kat.Odalar.Add(new BasitProjeOda { Poligon = [new(0, 0), new(12, 0), new(12, 8), new(0, 8)] });
        vm.Katlar.Add(kat);
        vm.SeciliKat = kat;
        BasitProjeEleman Ekle(string blok, double x)
        {
            var sembol = vm.AsamaSembolleri.Single(p => p.BlokAdi == blok);
            vm.ElemanEkleModeBaslatCommand.Execute(sembol);
            vm.EklenecekSembolKanvasaEkle(x, 1);
            return kat.Elemanlar.Last();
        }
        var ana = Ekle("E4P_ANA_PANO", 2);
        var tali = Ekle("E4P_TALI_PANO", 5);
        var alt = Ekle("E4P_ALT_PANO", 7);
        if (alt.Tip != BpcElemanTipi.KatPanosu || alt.OdaId != kat.Odalar[0].Id)
            throw new InvalidOperationException("Alt pano plan üzerinde doğru mahal ve tiple yerleşmeli.");
        vm.AydinlatmaAsamasi = BpcAydinlatmaAsamasi.PanoKablosu;
        vm.AtanmisPanolaraKabloCek();
        var taliHatId = kat.PanoBeslemeHatlari.Single(h => h.TaliPanoId == tali.Id && h.AnaPanoId == ana.Id).Id;
        var altHatId = kat.PanoBeslemeHatlari.Single(h => h.TaliPanoId == alt.Id && h.AnaPanoId == tali.Id).Id;
        vm.AtanmisPanolaraKabloCek();
        if (kat.PanoBeslemeHatlari.Count != 2 || !kat.PanoBeslemeHatlari.Any(h => h.Id == taliHatId) ||
            !kat.PanoBeslemeHatlari.Any(h => h.Id == altHatId))
            throw new InvalidOperationException("5. adım tekrarında besleme kimlikleri sabit kalmalı.");
        vm.AydinlatmaAsamasi = BpcAydinlatmaAsamasi.Pano;
        vm.SeciliEleman = alt;
        vm.SeciliTaliPanoHedefPano = ana;
        vm.AydinlatmaAsamasi = BpcAydinlatmaAsamasi.PanoKablosu;
        vm.AtanmisPanolaraKabloCek();
        if (kat.PanoBeslemeHatlari.Single(h => h.TaliPanoId == alt.Id).AnaPanoId != ana.Id)
            throw new InvalidOperationException("Elle seçilen üst pano kullanılmalı.");
    }

    public static void YigindaSonYirmiDegisiklikTersZamanSirasiylaGeriAlinir()
    {
        var vm = new BasitProjeCizViewModel();
        var kat = new BasitProjeKat();
        var pano = new BasitProjeEleman { Tip = BpcElemanTipi.KatPanosu, Ad = "P0" };
        kat.Elemanlar.Add(pano);
        vm.Katlar.Add(kat);
        vm.SeciliKat = kat;
        vm.SeciliEleman = pano;
        for (int i = 1; i <= 24; i++) vm.SeciliElemanAd = $"P{i}";
        vm.SeciliElemanlar.Add(pano);
        for (int i = 23; i >= 4; i--)
        {
            vm.GeriAlGenelCommand.Execute(null);
            if (kat.Elemanlar.Single().Ad != $"P{i}" || vm.SeciliElemanlar.Count != 0)
                throw new InvalidOperationException($"Geri al sırası yanlış veya eski eleman seçili: beklenen P{i}.");
        }
        for (int i = 5; i <= 24; i++)
        {
            vm.IleriAlGenelCommand.Execute(null);
            if (kat.Elemanlar.Single().Ad != $"P{i}") throw new InvalidOperationException("İleri al sırası yanlış.");
        }
    }
}
