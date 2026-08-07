using System;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;
using Kst.Acoustics.Core.Electric.ZayifAkimTekHat;

namespace Kst.Acoustics.Tests;

/// M12/M16 kabuk ayrımı: disiplin ayıklama motorunun regresyon testleri.
public static class DisiplinAyiklamaTests
{
    private static BasitProjeCizProje KarmaProjeOlustur()
    {
        var oda = new BasitProjeOda { Id = "oda1", Ad = "Salon",
            Poligon = [new(0, 0), new(6, 0), new(6, 6), new(0, 6)] };
        var kat = new BasitProjeKat { Ad = "Zemin" };
        kat.Odalar.Add(oda);

        // Kuvvetli akım içeriği
        kat.Elemanlar.Add(new BasitProjeEleman { Id = "arm1", Tip = BpcElemanTipi.Armatür, OdaId = "oda1" });
        kat.Elemanlar.Add(new BasitProjeEleman { Id = "anh1", Tip = BpcElemanTipi.Anahtar, OdaId = "oda1" });
        kat.Elemanlar.Add(new BasitProjeEleman { Id = "pano1", Tip = BpcElemanTipi.KatPanosu, OdaId = "oda1" });
        kat.Elemanlar.Add(new BasitProjeEleman { Id = "priz1", Tip = BpcElemanTipi.Priz, OdaId = "oda1" });
        kat.Kablolar.Add(new BpcKablo { Tip = BpcKabloTipi.GucKablosu });
        kat.Devreler.Add(new BpcDevre { AnahtarElemanId = "anh1", ArmaturElemanIds = { "arm1" } });
        kat.MahalListesi.Add(new BpcMahalSatiri { Tip = BpcElemanTipi.Armatür });

        // Zayıf akım içeriği
        kat.Elemanlar.Add(new BasitProjeEleman { Id = "kam1", Tip = BpcElemanTipi.Kamera, OdaId = "oda1" });
        kat.Elemanlar.Add(new BasitProjeEleman { Id = "nvr1", Tip = BpcElemanTipi.CctvKayit, OdaId = "oda1" });
        kat.Elemanlar.Add(new BasitProjeEleman { Id = "ded1", Tip = BpcElemanTipi.DumanDedektor, OdaId = "oda1" });
        kat.Kablolar.Add(new BpcKablo { Tip = BpcKabloTipi.ZayifAkimKablosu, SistemAnahtar = BpcPaftaTipi.Cctv });
        kat.SistemDevreleri.Add(new BpcSistemDevre { Sistem = BpcPaftaTipi.Cctv, MerkezId = "nvr1", CihazIds = { "kam1" } });
        kat.MahalListesi.Add(new BpcMahalSatiri { Tip = BpcElemanTipi.Kamera });

        var proje = new BasitProjeCizProje();
        proje.Katlar.Add(kat);
        return proje;
    }

    public static void DisiplinSayimiKarmaProjeyiDogruSayar()
    {
        var sayim = DisiplinAyiklamaServisi.Say(KarmaProjeOlustur());
        if (sayim.KuvvetliEleman != 4)
            throw new InvalidOperationException($"4 kuvvetli eleman bekleniyordu, gelen: {sayim.KuvvetliEleman}");
        if (sayim.ZayifEleman != 3)
            throw new InvalidOperationException($"3 zayıf eleman bekleniyordu, gelen: {sayim.ZayifEleman}");
        if (!sayim.KuvvetliVar || !sayim.ZayifVar)
            throw new InvalidOperationException("Hem kuvvetli hem zayıf içerik var sayılmalıydı.");
    }

    public static void M12AyiklamasiZayifAkimiSilerKuvvetliKorur()
    {
        var proje = KarmaProjeOlustur();
        var sonuc = DisiplinAyiklamaServisi.Ayikla(proje, BpcDisiplin.KuvvetliAkim);
        var kat = proje.Katlar[0];

        if (kat.Elemanlar.Any(e => BpcSistemKatalogu.Disiplini(e.Tip) == BpcDisiplin.ZayifAkim))
            throw new InvalidOperationException("Zayıf akım elemanları silinmeliydi.");
        if (kat.Elemanlar.Count != 4)
            throw new InvalidOperationException($"4 kuvvetli eleman korunmalıydı, kalan: {kat.Elemanlar.Count}");
        if (kat.SistemDevreleri.Count != 0)
            throw new InvalidOperationException("Zayıf akım sistem devreleri temizlenmeliydi.");
        if (kat.Kablolar.Any(k => k.Tip == BpcKabloTipi.ZayifAkimKablosu))
            throw new InvalidOperationException("Zayıf akım kabloları silinmeliydi.");
        if (kat.Devreler.Count != 1)
            throw new InvalidOperationException("Kuvvetli akım devresi korunmalıydı.");
        if (sonuc.SilinenEleman != 3)
            throw new InvalidOperationException($"3 eleman silinmeliydi, silinen: {sonuc.SilinenEleman}");
        if (proje.ModulDisiplini != BpcDisiplin.KuvvetliAkim)
            throw new InvalidOperationException("ModulDisiplini KuvvetliAkim damgalanmalıydı.");
        // Geometri korunmalı
        if (kat.Odalar.Count != 1 || kat.Odalar[0].Poligon.Count != 4)
            throw new InvalidOperationException("Oda geometrisi korunmalıydı.");
    }

    public static void M16AyiklamasiKuvvetliAkimiSilerZayifKorur()
    {
        var proje = KarmaProjeOlustur();
        DisiplinAyiklamaServisi.Ayikla(proje, BpcDisiplin.ZayifAkim);
        var kat = proje.Katlar[0];

        if (kat.Elemanlar.Any(e => BpcSistemKatalogu.Disiplini(e.Tip) == BpcDisiplin.KuvvetliAkim))
            throw new InvalidOperationException("Kuvvetli akım elemanları silinmeliydi.");
        if (kat.Elemanlar.Count != 3)
            throw new InvalidOperationException($"3 zayıf eleman korunmalıydı, kalan: {kat.Elemanlar.Count}");
        if (kat.Devreler.Count != 0 || kat.MekanikDevreleri.Count != 0)
            throw new InvalidOperationException("Kuvvetli akım devreleri temizlenmeliydi.");
        if (kat.SistemDevreleri.Count != 1)
            throw new InvalidOperationException("Zayıf akım sistem devresi korunmalıydı.");
        if (kat.MahalListesi.Count != 1 || kat.MahalListesi[0].Tip != BpcElemanTipi.Kamera)
            throw new InvalidOperationException("Yalnız zayıf akım mahal satırı korunmalıydı.");
        if (proje.ModulDisiplini != BpcDisiplin.ZayifAkim)
            throw new InvalidOperationException("ModulDisiplini ZayifAkim damgalanmalıydı.");
    }

    public static void AyiklamaSistemDevresindekiSilinenCihazReferansiniTemizler()
    {
        var proje = KarmaProjeOlustur();
        // Zayıf akım devresine, ayıklama sonrası silinecek bir kuvvetli referansı ekle
        proje.Katlar[0].SistemDevreleri[0].CihazIds.Add("arm1"); // kuvvetli eleman id
        DisiplinAyiklamaServisi.Ayikla(proje, BpcDisiplin.ZayifAkim);
        var devre = proje.Katlar[0].SistemDevreleri[0];
        if (devre.CihazIds.Contains("arm1"))
            throw new InvalidOperationException("Silinen kuvvetli elemanın referansı sistem devresinden temizlenmeliydi.");
        if (!devre.CihazIds.Contains("kam1"))
            throw new InvalidOperationException("Geçerli kamera referansı korunmalıydı.");
    }

    public static void DisiplinHaritasiElemanTiplerinDogruSiniflar()
    {
        if (BpcSistemKatalogu.Disiplini(BpcElemanTipi.Armatür) != BpcDisiplin.KuvvetliAkim)
            throw new InvalidOperationException("Armatür kuvvetli akım olmalı.");
        if (BpcSistemKatalogu.Disiplini(BpcElemanTipi.Kamera) != BpcDisiplin.ZayifAkim)
            throw new InvalidOperationException("Kamera zayıf akım olmalı.");
        if (BpcSistemKatalogu.Disiplini(BpcElemanTipi.YanginSantrali) != BpcDisiplin.ZayifAkim)
            throw new InvalidOperationException("Yangın santrali zayıf akım olmalı.");
        if (BpcSistemKatalogu.Disiplini(BpcElemanTipi.Buat) != BpcDisiplin.KuvvetliAkim)
            throw new InvalidOperationException("Buat (ortak) kuvvetli akıma düşmeli.");
    }

    /// Faz 6: M16 tam zinciri — karma proje → M16 ayıklama → M13 Donustur.
    /// Ayıklanmış zayıf akım içeriğinin M13 sistemlerine eksiksiz aktarıldığını doğrular.
    public static void M16AyiklananProjeM13eDogruAktarilir()
    {
        var proje = KarmaProjeOlustur();
        // M16 kabuğu davranışı: kuvvetli akımı ayıkla, yalnız zayıf akım kalsın.
        DisiplinAyiklamaServisi.Ayikla(proje, BpcDisiplin.ZayifAkim);

        var m13 = ZayifAkimTekHatImportServisi.Donustur(proje, "test.e4p16");

        // Karma projede tek CCTV devresi (1 kamera) vardı → bir CCTV sayfası beklenir.
        var cctvSayfa = m13.Sayfalar.FirstOrDefault(s => s.Sistem == BpcPaftaTipi.Cctv);
        if (cctvSayfa is null)
            throw new InvalidOperationException("M13'te CCTV sayfası üretilmeliydi.");
        if (cctvSayfa.ToplamCihazSayisi != 1)
            throw new InvalidOperationException($"CCTV sayfasında 1 cihaz bekleniyordu, gelen: {cctvSayfa.ToplamCihazSayisi}");

        // Kamera sistemi rack-kabin modeline de yansımalı (1 kamera).
        int toplamKamera = m13.KameraSistemi.RackKabinler.Sum(r => r.KameraSayisi);
        if (toplamKamera != 1)
            throw new InvalidOperationException($"M13 kamera sisteminde 1 kamera bekleniyordu, gelen: {toplamKamera}");

        // Kuvvetli akım hiçbir M13 sayfası üretmemeli (aydınlatma/priz/pano sızmamalı).
        if (m13.Sayfalar.Any(s => BpcSistemKatalogu.Get(s.Sistem).Disiplin == BpcDisiplin.KuvvetliAkim))
            throw new InvalidOperationException("M13'e kuvvetli akım sistemi sızmamalıydı.");

        if (m13.ProjeAdi != proje.ProjeAdi)
            throw new InvalidOperationException("Proje adı M13'e taşınmalıydı.");
    }

    /// Faz 6: zayıf akım içeriği olmayan (yalnız kuvvetli) bir M16 ayıklaması
    /// M13'e boş aktarım üretir — UI tarafı bunu "aktarılacak sistem yok" diye yakalar.
    public static void M13AktarimiZayifAkimYoksaBosUretir()
    {
        var proje = KarmaProjeOlustur();
        DisiplinAyiklamaServisi.Ayikla(proje, BpcDisiplin.KuvvetliAkim); // zayıf akımı sil
        var m13 = ZayifAkimTekHatImportServisi.Donustur(proje);
        if (m13.ToplamCihazSayisi != 0 || m13.Sayfalar.Count != 0)
            throw new InvalidOperationException("Zayıf akım yokken M13 aktarımı boş olmalıydı.");
    }
}
