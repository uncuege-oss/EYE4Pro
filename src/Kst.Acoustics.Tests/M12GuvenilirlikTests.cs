using System;
using System.IO;
using System.Linq;
using Kst.Acoustics.App.Electric.BasitProjeCiz.ViewModels;
using Kst.Acoustics.Core.Bina.Services;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

namespace Kst.Acoustics.Tests;

public static class M12GuvenilirlikTests
{
    public static void M12MigrasyonFarkliKaynagiGuvenliDosyayaAyirir()
    {
        var dizin = Path.Combine(Path.GetTempPath(), $"m12-kimlik-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dizin);
        var projeYolu = Path.Combine(dizin, "Proje.e4p12");
        try
        {
            BasitProjeCizProje Proje(string odaId)
            {
                var p = new BasitProjeCizProje { ProjeAdi = "M12" };
                var kat = new BasitProjeKat { Id = "kat", Ad = "Kat" };
                kat.Odalar.Add(new BasitProjeOda
                {
                    Id = odaId, Ad = odaId,
                    Poligon = [new(0, 0), new(4, 0), new(4, 4), new(0, 4)]
                });
                p.Katlar.Add(kat);
                return p;
            }

            var ilk = Proje("oda-1");
            BinaGeometriExportServisi.EskiProjeyiMigrateEt(ilk, projeYolu);
            var ilkYol = ilk.BinaGeometriYolu;

            var farkli = Proje("oda-2");
            var not = BinaGeometriExportServisi.EskiProjeyiMigrateEt(farkli, projeYolu);
            if (not is null || !not.Contains("güvenli yeni dosya") ||
                farkli.BinaGeometriYolu == ilkYol || !File.Exists(farkli.BinaGeometriYolu))
                throw new InvalidOperationException("Farklı kaynak aynı adlı M50 yan dosyasına bağlanmamalı.");
        }
        finally
        {
            if (Directory.Exists(dizin)) Directory.Delete(dizin, true);
        }
    }

    public static void OzellikDuzenlemeKilitDirtyVeUndoKurallarinaUyar()
    {
        var vm = new BasitProjeCizViewModel();
        var kat = new BasitProjeKat { Ad = "Kat" };
        var eleman = new BasitProjeEleman
        {
            Id = "armatur", Tip = BpcElemanTipi.Armatür, Ad = "L", CustomGucW = 100
        };
        kat.Elemanlar.Add(eleman);
        vm.Katlar.Add(kat);
        vm.SeciliKat = kat;
        vm.SeciliEleman = eleman;
        vm.TemizIsaretle();

        vm.ProjeKilitli = true;
        vm.SeciliElemanGucW = 200;
        if (eleman.CustomGucW != 100 || vm.IsDirty)
            throw new InvalidOperationException("Kilitli projede özellik değişmemeli ve proje kirlenmemeli.");

        vm.ProjeKilitli = false;
        vm.SeciliElemanGucW = 200;
        if (eleman.CustomGucW != 200 || !vm.IsDirty)
            throw new InvalidOperationException("Özellik değişikliği modeli güncelleyip IsDirty işaretlemeli.");

        vm.ElemanGeriAlCommand.Execute(null);
        var geri = kat.Elemanlar.Single(e => e.Id == "armatur");
        if (geri.CustomGucW != 100)
            throw new InvalidOperationException("Özellik değişikliği Ctrl+Z ile geri alınmalı.");
    }

    public static void ElemanSilmeDirtyVeUndoKurallarinaUyar()
    {
        var vm = new BasitProjeCizViewModel();
        var kat = new BasitProjeKat { Ad = "Kat" };
        var eleman = new BasitProjeEleman { Id = "silinecek", Tip = BpcElemanTipi.Armatür };
        kat.Elemanlar.Add(eleman);
        vm.Katlar.Add(kat);
        vm.SeciliKat = kat;
        vm.SeciliEleman = eleman;
        vm.TemizIsaretle();

        vm.SeciliElemanSilCommand.Execute(null);
        if (kat.Elemanlar.Any(e => e.Id == eleman.Id) || !vm.IsDirty)
            throw new InvalidOperationException("Sil komutu elemanı kaldırıp IsDirty işaretlemeli.");

        vm.ElemanGeriAlCommand.Execute(null);
        if (kat.Elemanlar.All(e => e.Id != eleman.Id))
            throw new InvalidOperationException("Silinen eleman tek undo adımıyla geri gelmeli.");
    }

    public static void DirektPrizPanoOzetineGirer()
    {
        var vm = new BasitProjeCizViewModel();
        var kat = new BasitProjeKat { Ad = "Kat" };
        var oda = new BasitProjeOda
        {
            Id = "oda", Ad = "Oda", Poligon = [new(0, 0), new(6, 0), new(6, 4), new(0, 4)]
        };
        var pano = new BasitProjeEleman
        {
            Id = "pano", Tip = BpcElemanTipi.KatPanosu, Ad = "KP", OdaId = oda.Id, X = 0.5, Y = 0.5
        };
        var priz = new BasitProjeEleman
        {
            Id = "trifaze", Tip = BpcElemanTipi.TrifazePriz, Ad = "Trifaze Priz",
            OdaId = oda.Id, X = 4, Y = 2, CustomGucW = 3000
        };
        kat.Odalar.Add(oda);
        kat.Elemanlar.Add(pano);
        kat.Elemanlar.Add(priz);
        vm.Katlar.Add(kat);
        vm.SeciliKat = kat;

        vm.BosUclariEnYakinPanoyaBagla();

        var devre = kat.PrizDevreleri.Single(d => d.PrizElemanIds.Contains(priz.Id));
        if (devre.BuatId != priz.Id)
            throw new InvalidOperationException("Doğrudan priz devresi uç kimliği olarak priz Id'sini taşımalı.");

        var linye = BasitProjeCizPanoServisi.PanoOzetleri(kat, [])
            .Single(o => o.Pano.Id == pano.Id).Linyeler.Single(l => l.LinyePrefix == "P");
        if (linye.ElemanSayisi != 1 || Math.Abs(linye.GucW - 3000) > 1e-6)
            throw new InvalidOperationException("Doğrudan priz pano özetine adet ve güç olarak girmeli.");
    }

    public static void PanoSilmeTumKatReferanslariniTemizlerVeUndoGeriGetirir()
    {
        var vm = new BasitProjeCizViewModel();
        var anaKat = new BasitProjeKat { Id = "ana-kat", Ad = "Ana Kat", KatNo = 0 };
        var taliKat = new BasitProjeKat { Id = "tali-kat", Ad = "Tali Kat", KatNo = 1 };
        var ana = new BasitProjeEleman
        {
            Id = "ana", Tip = BpcElemanTipi.KatPanosu, BlokAdi = "E4P_ANA_PANO", Ad = "AP"
        };
        var tali = new BasitProjeEleman
        {
            Id = "tali", Tip = BpcElemanTipi.KatPanosu, BlokAdi = "E4P_TALI_PANO",
            Ad = "TP", TaliPanoHedefPanoId = ana.Id
        };
        anaKat.Elemanlar.Add(ana);
        taliKat.Elemanlar.Add(tali);
        taliKat.PanoBeslemeHatlari.Add(new BpcPanoBeslemeHatti
        {
            Id = "besleme", AnaPanoId = ana.Id, TaliPanoId = tali.Id,
            Rota = [new(0, 0), new(1, 0)]
        });
        vm.Katlar.Add(anaKat);
        vm.Katlar.Add(taliKat);
        vm.SeciliKat = anaKat;
        vm.SeciliEleman = ana;

        vm.SeciliElemanSilCommand.Execute(null);
        if (taliKat.PanoBeslemeHatlari.Count != 0 || tali.TaliPanoHedefPanoId is not null)
            throw new InvalidOperationException("Pano silme tüm katlardaki besleme ve hedef referanslarını temizlemeli.");

        vm.ElemanGeriAlCommand.Execute(null);
        var geriTali = taliKat.Elemanlar.Single(e => e.Id == tali.Id);
        if (anaKat.Elemanlar.All(e => e.Id != ana.Id) ||
            taliKat.PanoBeslemeHatlari.All(h => h.Id != "besleme") ||
            geriTali.TaliPanoHedefPanoId != ana.Id)
            throw new InvalidOperationException("Çok katlı pano silme tek undo adımında bütünüyle geri gelmeli.");
    }

    public static void TumunuSilYalnizAktifPaftayiTemizler()
    {
        var vm = new BasitProjeCizViewModel();
        var kat = new BasitProjeKat { Ad = "Kat" };
        kat.Devreler.Add(new BpcDevre { Id = "a" });
        kat.PrizDevreleri.Add(new BpcPrizDevre { Id = "p" });
        kat.MekanikDevreleri.Add(new BpcMekanikDevre { Id = "m" });
        kat.SistemDevreleri.Add(new BpcSistemDevre { Id = "s", Sistem = BpcPaftaTipi.Data });
        kat.BuatPanoBaglantilari.Add(new BpcBuatPanoBaglanti { Id = "ba", LinyePrefix = "A" });
        kat.BuatPanoBaglantilari.Add(new BpcBuatPanoBaglanti { Id = "bp", LinyePrefix = "P" });
        foreach (var id in new[] { "a", "p", "m", "s", "ba", "bp" })
            kat.Kablolar.Add(new BpcKablo { DevreId = id, Rota = [new(0, 0), new(1, 0)] });
        vm.Katlar.Add(kat);
        vm.SeciliKat = kat;
        vm.AktifPafta = BpcPaftaTipi.Aydinlatma;

        vm.KabloTumunuSil();

        if (kat.Devreler.Count != 0 || kat.BuatPanoBaglantilari.Any(b => b.LinyePrefix == "A") ||
            kat.Kablolar.Any(k => k.DevreId is "a" or "ba"))
            throw new InvalidOperationException("Aydınlatma Tamamını Sil, aydınlatma kayıtlarını temizlemeli.");
        if (kat.PrizDevreleri.Count != 1 || kat.MekanikDevreleri.Count != 1 ||
            kat.SistemDevreleri.Count != 1 || kat.BuatPanoBaglantilari.All(b => b.LinyePrefix != "P"))
            throw new InvalidOperationException("Aydınlatma Tamamını Sil diğer paftalara dokunmamalı.");
    }

    public static void SeciliDevreSilmeYetimPanoBaglantisiBirakmaz()
    {
        var vm = new BasitProjeCizViewModel();
        var kat = new BasitProjeKat { Ad = "Kat" };
        var devre = new BpcDevre { Id = "devre", BuatId = "buat" };
        kat.Devreler.Add(devre);
        kat.BuatPanoBaglantilari.Add(new BpcBuatPanoBaglanti
        {
            Id = "baglanti", BuatId = "buat", PanoId = "pano", LinyePrefix = "A"
        });
        kat.Kablolar.Add(new BpcKablo
        {
            DevreId = "baglanti", Tip = BpcKabloTipi.PanoKablosu, Rota = [new(0, 0), new(1, 0)]
        });
        vm.Katlar.Add(kat);
        vm.SeciliKat = kat;
        vm.SeciliDevreId = devre.Id;

        vm.KabloSeciliDevreyiSilCommand.Execute(null);

        if (kat.Devreler.Any(d => d.Id == devre.Id) ||
            kat.BuatPanoBaglantilari.Any(b => b.BuatId == "buat" && b.LinyePrefix == "A") ||
            kat.Kablolar.Any(k => k.DevreId == "baglanti"))
            throw new InvalidOperationException("Son devre silinince kullanılmayan pano bağlantısı ve kablosu da silinmeli.");
    }

    public static void MahalSatiriKimligiAyniTanimiAyirir()
    {
        var vm = new BasitProjeCizViewModel();
        var kat = new BasitProjeKat { Ad = "Kat" };
        var satir1 = new BpcMahalSatiri
            { Id = "satir-1", Tip = BpcElemanTipi.Armatür, ArmatürTanimId = "ayni-tanim" };
        var satir2 = new BpcMahalSatiri
            { Id = "satir-2", Tip = BpcElemanTipi.Armatür, ArmatürTanimId = "ayni-tanim" };
        kat.MahalListesi.Add(satir1);
        kat.MahalListesi.Add(satir2);
        kat.Elemanlar.Add(new BasitProjeEleman
        {
            Id = "e1", Tip = BpcElemanTipi.Armatür, ArmatürTanimId = "ayni-tanim",
            OtomatikYerlesimKaynak = BasitProjeCizOtomatikYerlestirServisi.MahalListesiKaynak,
            OtomatikYerlesimGrupIds = [satir1.Id]
        });
        kat.Elemanlar.Add(new BasitProjeEleman
        {
            Id = "e2", Tip = BpcElemanTipi.Armatür, ArmatürTanimId = "ayni-tanim",
            OtomatikYerlesimKaynak = BasitProjeCizOtomatikYerlestirServisi.MahalListesiKaynak,
            OtomatikYerlesimGrupIds = [satir2.Id]
        });
        kat.Elemanlar.Add(new BasitProjeEleman
        {
            Id = "ortak-buat", Tip = BpcElemanTipi.Buat,
            OtomatikYerlesimKaynak = BasitProjeCizOtomatikYerlestirServisi.MahalListesiKaynak,
            OtomatikYerlesimGrupIds = [satir1.Id, satir2.Id]
        });
        vm.Katlar.Add(kat);
        vm.SeciliKat = kat;

        vm.MahalSatiriKaldir(kat, satir1);

        if (kat.Elemanlar.Any(e => e.Id == "e1") || kat.Elemanlar.All(e => e.Id == "e2"))
            throw new InvalidOperationException("Aynı tanımlı satırlardan yalnız silinen satırın elemanı kaldırılmalı.");
        var ortak = kat.Elemanlar.Single(e => e.Id == "ortak-buat");
        if (!ortak.OtomatikYerlesimGrupIds.SequenceEqual([satir2.Id]))
            throw new InvalidOperationException("Ortak bağımlı eleman kalan mahal satırına bağlı kalmalı.");
    }

    public static void M50ImportElektrikVeriliBosKatiKorurVeMutabakattaGosterir()
    {
        var proje = new BasitProjeCizProje();
        var eskiKat = new BasitProjeKat { Id = "eski-kat", Ad = "Eski Kat" };
        eskiKat.KabloTavalari.Add(new BpcKabloTavasi
        {
            Id = "tava", Rota = [new Point2D(0, 0), new Point2D(2, 0)]
        });
        proje.Katlar.Add(eskiKat);

        var geo = BinaGeometriServisi.YeniOlustur("Yeni bina");
        var sonuc = BinaGeometriImportServisi.Uygula(proje, geo, null);

        if (proje.Katlar.All(k => k.Id != eskiKat.Id) || sonuc.KorunanEskiKat != 1)
            throw new InvalidOperationException("Elemanı olmayan fakat tava taşıyan eski kat korunmalı.");

        var rapor = GeometriMutabakatServisi.Karsilastir(
            geo, new BasitProjeGeometriBagimliVeri(proje).BagimliKayitlar());
        if (!rapor.AskidaKayitlar.Any(k => k.Tur == "Kablo tavası" && k.KatId == eskiKat.Id))
            throw new InvalidOperationException("Korunan katın kablo tavası mutabakat raporunda görünmeli.");
    }

    public static void AyniKatPanoZinciriDogruKokuVeTekYukuAktarir()
    {
        var kat = new BasitProjeKat { Ad = "Zemin", KatNo = 0 };
        var ana = new BasitProjeEleman { Id = "ap", Tip = BpcElemanTipi.KatPanosu, Ad = "AP" };
        var katPano = new BasitProjeEleman { Id = "kp", Tip = BpcElemanTipi.KatPanosu, Ad = "KP" };
        var tali = new BasitProjeEleman { Id = "tp", Tip = BpcElemanTipi.KatPanosu, Ad = "TP" };
        var anahtar = new BasitProjeEleman { Id = "anahtar", Tip = BpcElemanTipi.Anahtar };
        var buat = new BasitProjeEleman { Id = "buat", Tip = BpcElemanTipi.Buat };
        var armatur = new BasitProjeEleman
            { Id = "l", Tip = BpcElemanTipi.Armatür, CustomGucW = 100 };
        foreach (var e in new[] { ana, katPano, tali, anahtar, buat, armatur }) kat.Elemanlar.Add(e);

        kat.Devreler.Add(new BpcDevre
        {
            Id = "devre", AnahtarElemanId = anahtar.Id, BuatId = buat.Id,
            ArmaturElemanIds = [armatur.Id]
        });
        kat.BuatPanoBaglantilari.Add(new BpcBuatPanoBaglanti
            { Id = "linye", BuatId = buat.Id, PanoId = tali.Id, LinyePrefix = "A" });

        // Bilerek önce TP→KP eklenir; liste sırası kök seçiminde kullanılmamalı.
        kat.PanoBeslemeHatlari.Add(new BpcPanoBeslemeHatti
            { AnaPanoId = katPano.Id, TaliPanoId = tali.Id, Rota = [new(0, 0), new(1, 0)] });
        kat.PanoBeslemeHatlari.Add(new BpcPanoBeslemeHatti
            { AnaPanoId = ana.Id, TaliPanoId = katPano.Id, Rota = [new(1, 0), new(2, 0)] });

        var proje = new BasitProjeCizProje();
        proje.Katlar.Add(kat);
        var sonuc = TekHatAktarimServisi.Donustur(proje, []);
        if (!sonuc.Basarili) throw new InvalidOperationException(sonuc.Hata);

        var kok = sonuc.Proje!.KokPano!;
        if (kok.AltPanolar.Count != 1 || !kok.AltPanolar[0].Ad.StartsWith("KP", StringComparison.Ordinal))
            throw new InvalidOperationException("TP→KP→AP zincirinde gerçek graf kökü AP olmalı.");
        if (Math.Abs(kok.ToplamGucW - 100) > 1e-6)
            throw new InvalidOperationException($"Alt pano yükü bir kez sayılmalı; bulunan {kok.ToplamGucW} W.");
    }
}
