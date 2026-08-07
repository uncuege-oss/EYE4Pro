using Kst.Acoustics.Core.Electric.KesifHazirla;

namespace Kst.Acoustics.Tests;

public static class KesifHazirlaTests
{
    public static void IcmalButunPozlariVeTabloKiriliminiGosterir()
    {
        var proje = new KesifProje();
        var aBlok = new KesifTablosu { Ad = "A Blok" };
        var bBlok = new KesifTablosu { Ad = "B Blok" };

        aBlok.Satirlar.Add(new KesifSatir
        {
            PozNo = "ELK-001",
            BirimAdi = "LED armatür",
            Birim = "adet",
            Miktar = 10,
            BirimFiyat = 100
        });
        aBlok.Satirlar.Add(new KesifSatir
        {
            PozNo = "ELK-002",
            BirimAdi = "NYM kablo",
            Birim = "m",
            Miktar = 25,
            BirimFiyat = 20
        });
        bBlok.Satirlar.Add(new KesifSatir
        {
            PozNo = "ELK-001",
            BirimAdi = "LED armatür",
            Birim = "adet",
            Miktar = 5,
            BirimFiyat = 100
        });

        proje.Tablolar.Add(aBlok);
        proje.Tablolar.Add(bBlok);

        var icmal = KesifHesapServisi.IcmalOlustur(proje);
        if (icmal.Count != 2)
            throw new InvalidOperationException($"İcmalde bütün pozlar görünmeliydi. Gelen: {icmal.Count}");

        var armatur = icmal.Single(x => x.PozNo == "ELK-001");
        if (armatur.ToplamMiktar != 15 || armatur.ToplamTutar != 1500)
            throw new InvalidOperationException("Tekrarlanan poz miktar/tutar toplamı hatalı.");
        if (armatur.Kirilimlar.Count != 2)
            throw new InvalidOperationException("Tekrarlanan poz iki tablo kırılımı göstermeli.");

        var kablo = icmal.Single(x => x.PozNo == "ELK-002");
        if (kablo.ToplamMiktar != 25 || kablo.Kirilimlar.Count != 1 || kablo.Kirilimlar[0].TabloAdi != "A Blok")
            throw new InvalidOperationException("Tek tabloda kullanılan poz da icmalde tek kırılımla görünmeli.");
    }

    public static void IcmalAyniPozFarkliBirimFiyatUyarisiVerir()
    {
        var proje = new KesifProje();
        var tablo = new KesifTablosu { Ad = "Ortak" };
        tablo.Satirlar.Add(new KesifSatir { PozNo = "ELK-003", BirimAdi = "Buat", Birim = "adet", Miktar = 2, BirimFiyat = 40 });
        tablo.Satirlar.Add(new KesifSatir { PozNo = "ELK-003", BirimAdi = "Buat", Birim = "adet", Miktar = 3, BirimFiyat = 50 });
        proje.Tablolar.Add(tablo);

        var row = KesifHesapServisi.IcmalOlustur(proje).Single();
        if (!row.BirimFiyatFarkiVar)
            throw new InvalidOperationException("Aynı poz/birim farklı fiyatla girilince uyarı bayrağı beklenir.");
        if (row.ToplamTutar != 230)
            throw new InvalidOperationException("Farklı fiyatlı satırlar gerçek satır tutarlarıyla toplanmalı.");
    }

    public static void IcmalSecimiDegisinceTabloKirilimiDegisir()
    {
        var vm = new Kst.Acoustics.App.Electric.KesifHazirla.KesifHazirlaViewModel(
            new KesifPozEslemeServisi(Path.Combine(Path.GetTempPath(), $"kesif-icmal-secim-{Guid.NewGuid():N}.json")));
        var tablo = vm.SeciliTablo!;
        tablo.Satirlar.Add(new KesifSatir
        {
            PozNo = "A", BirimAdi = "A imalatı", Birim = "adet", Miktar = 2, BirimFiyat = 10
        });
        tablo.Satirlar.Add(new KesifSatir
        {
            PozNo = "B", BirimAdi = "B imalatı", Birim = "adet", Miktar = 7, BirimFiyat = 20
        });
        vm.HesaplariYenile();

        vm.SeciliIcmalSatiri = vm.IcmalSatirlari.Single(s => s.PozNo == "B");
        var kirilim = vm.SeciliIcmalKirilimlar?.Single();
        if (kirilim is null || kirilim.Miktar != 7 || kirilim.Tutar != 140)
            throw new InvalidOperationException("Ana icmal seçimi değişince tablo kırılımı seçilen pozu izlemeli.");
    }

    public static void PozImportTurkceTanimiBasliginiOkur()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kesif-poz-{Guid.NewGuid():N}.tsv");
        File.WriteAllText(path, "Poz No\tTanımı\tBirim\tBirim Fiyat\n35.710.1101\tAsansör imalatı\tAdet\t1.107.437,49");

        try
        {
            var poz = KesifPozImportServisi.DosyadanOku(path).Single();
            if (poz.PozNo != "35.710.1101" || poz.BirimAdi != "Asansör imalatı" || poz.Birim != "Adet")
                throw new InvalidOperationException("Türkçe birim fiyat başlıkları doğru okunamadı.");
            if (poz.BirimFiyat != 1107437.49m)
                throw new InvalidOperationException($"Türkçe fiyat formatı doğru okunamadı: {poz.BirimFiyat}");
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    public static void PozImportKusursuzBinlikFiyatiDogruOkur()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kesif-poz-{Guid.NewGuid():N}.tsv");
        File.WriteAllText(path, "Poz No\tTanım\tBirim\tBirim Fiyat\n35.710.1101\tAsansör imalatı\tAdet\t1.107.437");

        try
        {
            var poz = KesifPozImportServisi.DosyadanOku(path).Single();
            if (poz.BirimFiyat != 1107437m)
                throw new InvalidOperationException($"Küsuratsız binlik fiyat doğru okunamadı: {poz.BirimFiyat}");
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    public static void PozImportQuotedNoktaliVirgulCsvOkur()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kesif-poz-{Guid.NewGuid():N}.csv");
        File.WriteAllText(
            path,
            "Poz No;Tanım;Birim;Birim Fiyat;Not\n\"ELK-CSV\";\"Kablo; NYM\";m;\"1.234,50\";\"Açıklama; test\"");

        try
        {
            var poz = KesifPozImportServisi.DosyadanOku(path).Single();
            if (poz.PozNo != "ELK-CSV" || poz.BirimAdi != "Kablo; NYM" || poz.Aciklama != "Açıklama; test")
                throw new InvalidOperationException("Quoted/noktalı virgüllü CSV alanları doğru okunamadı.");
            if (poz.BirimFiyat != 1234.50m)
                throw new InvalidOperationException($"Noktalı virgüllü CSV fiyatı doğru okunamadı: {poz.BirimFiyat}");
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    public static void IcmalPozNoBosFarkliImalatlariAyriGosterir()
    {
        var proje = new KesifProje();
        var tablo = new KesifTablosu { Ad = "Manuel" };
        tablo.Satirlar.Add(new KesifSatir { PozNo = "", BirimAdi = "Kablo çekimi", Birim = "adet", Miktar = 1, BirimFiyat = 100 });
        tablo.Satirlar.Add(new KesifSatir { PozNo = "", BirimAdi = "Pano montajı", Birim = "adet", Miktar = 1, BirimFiyat = 200 });
        proje.Tablolar.Add(tablo);

        var icmal = KesifHesapServisi.IcmalOlustur(proje);
        if (icmal.Count != 2)
            throw new InvalidOperationException($"Poz No boş olan farklı imalatlar ayrı icmal satırı olmalıydı. Gelen: {icmal.Count}");
    }


    public static void BirimFiyatListeleriPasifeAlinipTekrarAktifEdilir()
    {
        var proje = new KesifProje();
        proje.PozKutuphane.Add(new KesifPoz { PozNo = "MAN-001", BirimAdi = "Manuel poz", Birim = "adet" });

        var eski = KesifBirimFiyatListesiServisi.ListeEkle(
            proje,
            "2026/04",
            "eski.xlsx",
            [new KesifPoz { PozNo = "OLD-001", BirimAdi = "Eski poz", Birim = "m", BirimFiyat = 10 }],
            digerListeleriPasifeAl: false);

        var yeni = KesifBirimFiyatListesiServisi.ListeEkle(
            proje,
            "2026/05",
            "yeni.xlsx",
            [new KesifPoz { PozNo = "NEW-001", BirimAdi = "Yeni poz", Birim = "m", BirimFiyat = 20 }],
            digerListeleriPasifeAl: true);

        if (eski.AktifMi || !yeni.AktifMi)
            throw new InvalidOperationException("Yeni liste yüklenince eski liste pasife alınmalı, yeni liste aktif kalmalı.");
        if (proje.PozKutuphane.Any(p => p.PozNo == "OLD-001") || proje.PozKutuphane.All(p => p.PozNo != "NEW-001"))
            throw new InvalidOperationException("Pasif listedeki poz görünmemeli, aktif listedeki poz görünmeli.");
        if (proje.PozKutuphane.All(p => p.PozNo != "MAN-001"))
            throw new InvalidOperationException("Manuel pozlar liste aktif/pasif değişiminden etkilenmemeli.");

        KesifBirimFiyatListesiServisi.ListeyiAktifYap(proje, eski);
        if (proje.PozKutuphane.All(p => p.PozNo != "OLD-001") || proje.PozKutuphane.All(p => p.PozNo != "NEW-001"))
            throw new InvalidOperationException("Pasif liste tekrar aktif edildiğinde pozları kütüphaneye geri dönmeli.");

        KesifBirimFiyatListesiServisi.ListeyiPasifeAl(proje, yeni);
        if (proje.PozKutuphane.Any(p => p.PozNo == "NEW-001") || proje.PozKutuphane.All(p => p.PozNo != "OLD-001"))
            throw new InvalidOperationException("Aktif liste pasife alınınca pozları gizlenmeli, diğer aktif liste kalmalı.");
    }

    public static void OzelPozTarihGuncellemeTesti()
    {
        var proje = new KesifProje();
        var poz = new KesifPoz 
        { 
            PozNo = "OZL-101", 
            BirimAdi = "Özel İmalat", 
            Birim = "adet", 
            BirimFiyat = 150,
            ListePozuMu = false
        };

        if ((DateTime.Now - poz.SonGuncellemeTarihi).TotalMinutes > 1)
            throw new InvalidOperationException("Yeni oluşturulan pozun güncelleme tarihi güncel olmalı.");

        proje.PozKutuphane.Add(poz);
        KesifBirimFiyatListesiServisi.PozKutuphaneYenile(proje);

        var pozInLibrary = proje.PozKutuphane.Single(p => p.PozNo == "OZL-101");
        
        var oldDate = pozInLibrary.SonGuncellemeTarihi;
        System.Threading.Thread.Sleep(15);
        pozInLibrary.SonGuncellemeTarihi = DateTime.Now;

        if (pozInLibrary.SonGuncellemeTarihi <= oldDate)
            throw new InvalidOperationException("Güncelleme tarihi güncellendiğinde ilerlemeli.");
    }

    public static void SonOzelPozSilininceGeriDonmez()
    {
        var vm = new Kst.Acoustics.App.Electric.KesifHazirla.KesifHazirlaViewModel
        {
            OzelPozModuAktif = true
        };

        vm.OzelPozEkleCommand.Execute(null);
        var poz = vm.Proje.ManuelPozlar.Single();
        vm.SeciliPoz = poz;
        vm.SeciliOzelPoz = poz;

        vm.OzelPozSilCommand.Execute(null);
        vm.PozKutuphaneYenile();

        if (vm.Proje.ManuelPozlar.Count != 0 || vm.Proje.PozKutuphane.Any(p => !p.ListePozuMu))
            throw new InvalidOperationException("Son özel poz silindikten sonra yeniden kütüphaneye dönmemeli.");
    }

    public static void PozKutuphaneAramaVeFiltrelemeTesti()
    {
        var vm = new Kst.Acoustics.App.Electric.KesifHazirla.KesifHazirlaViewModel();
        vm.OzelPozModuAktif = true;
        // Clear default ones if any
        vm.Proje.PozKutuphane.Clear();
        
        var poz1 = new KesifPoz { PozNo = "ELK-001", BirimAdi = "LED Armatür", Birim = "adet", BirimFiyat = 100 };
        var poz2 = new KesifPoz { PozNo = "ELK-002", BirimAdi = "Bakır Kablo", Birim = "m", BirimFiyat = 20 };
        var poz3 = new KesifPoz { PozNo = "INS-001", BirimAdi = "Tuğla Duvar", Birim = "m2", BirimFiyat = 150 };

        vm.Proje.PozKutuphane.Add(poz1);
        vm.Proje.PozKutuphane.Add(poz2);
        vm.Proje.PozKutuphane.Add(poz3);
        vm.PozKutuphaneYenile(); // this triggers FiltrelenmisPozKutuphaneYenile

        if (vm.FiltrelenmisPozKutuphane.Count != 3)
            throw new InvalidOperationException("Boş aramada tüm pozlar listelenmeli.");

        // PozNo search
        vm.AramaMetni = "elk";
        if (vm.FiltrelenmisPozKutuphane.Count != 2 || !vm.FiltrelenmisPozKutuphane.Contains(poz1) || !vm.FiltrelenmisPozKutuphane.Contains(poz2))
            throw new InvalidOperationException("PozNo ile arama filtrelemesi hatalı.");

        // BirimAdi search
        vm.AramaMetni = "armatür"; // Turkish characters
        if (vm.FiltrelenmisPozKutuphane.Count != 1 || vm.FiltrelenmisPozKutuphane[0] != poz1)
            throw new InvalidOperationException("BirimAdi ile arama filtrelemesi hatalı.");

        // Case insensitivity and Turkish chars
        vm.AramaMetni = "ARMAtür";
        if (vm.FiltrelenmisPozKutuphane.Count != 1 || vm.FiltrelenmisPozKutuphane[0] != poz1)
            throw new InvalidOperationException("BirimAdi büyük/küçük harf arama filtrelemesi hatalı.");

        // Reset search
        vm.AramaMetni = "";
        if (vm.FiltrelenmisPozKutuphane.Count != 3)
            throw new InvalidOperationException("Arama temizlenince tüm pozlar geri gelmeli.");
    }

    public static void JsonRoundTripManuelVeListePozlariniKorur()
    {
        var proje = new KesifProje { Ad = "Round-Trip" };

        // Manuel poz (hiçbir listeye ait değil)
        proje.PozKutuphane.Add(new KesifPoz { PozNo = "MAN-9", BirimAdi = "Manuel imalat", Birim = "adet", BirimFiyat = 5, ListePozuMu = false });

        // Birim fiyat listesi (2 poz)
        KesifBirimFiyatListesiServisi.ListeEkle(
            proje, "Liste A", "a.xlsx",
            [
                new KesifPoz { PozNo = "L-1", BirimAdi = "Liste poz 1", Birim = "m", BirimFiyat = 10 },
                new KesifPoz { PozNo = "L-2", BirimAdi = "Liste poz 2", Birim = "m", BirimFiyat = 20 }
            ],
            digerListeleriPasifeAl: false);

        // Bir keşif tablosu + satır
        var tablo = new KesifTablosu { Ad = "T1" };
        tablo.Satirlar.Add(new KesifSatir { PozNo = "L-1", BirimAdi = "Liste poz 1", Birim = "m", Miktar = 4, BirimFiyat = 10 });
        proje.Tablolar.Add(tablo);

        var path = Path.Combine(Path.GetTempPath(), $"kesif-rt-{Guid.NewGuid():N}.e4p14");
        try
        {
            KesifJsonServisi.Kaydet(proje, path);

            // Liste pozları dosyada iki kez yazılmamalı (PozKutuphane JsonIgnore).
            var json = File.ReadAllText(path);
            var manuelYazim = CountOccurrences(json, "\"MAN-9\"");
            var listeYazim = CountOccurrences(json, "\"L-1\"");
            if (manuelYazim != 1)
                throw new InvalidOperationException($"Manuel poz tam 1 kez yazılmalı, {manuelYazim} kez yazıldı.");
            if (listeYazim != 2) // biri liste pozunda, biri keşif satırında — PozKutuphane kopyası OLMAMALI
                throw new InvalidOperationException($"Liste pozu beklenenden farklı yazıldı (liste + satır = 2), gelen: {listeYazim}.");

            var yuklenen = KesifJsonServisi.Ac(path);
            KesifBirimFiyatListesiServisi.PozKutuphaneYenile(yuklenen);

            if (yuklenen.PozKutuphane.Count(p => p.PozNo == "MAN-9") != 1)
                throw new InvalidOperationException("Manuel poz yükleme sonrası kütüphanede tam 1 kez olmalı.");
            if (yuklenen.PozKutuphane.Count(p => p.PozNo == "L-1") != 1 ||
                yuklenen.PozKutuphane.Count(p => p.PozNo == "L-2") != 1)
                throw new InvalidOperationException("Liste pozları yükleme sonrası kütüphanede birer kez olmalı.");
            if (yuklenen.Tablolar.Single().Toplam != 40)
                throw new InvalidOperationException("Keşif satır toplamı round-trip sonrası korunmadı.");
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static int CountOccurrences(string text, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }
        return count;
    }

    public static void TabloIsimDegistirmeVeIcmalGuncellemeTesti()
    {
        var vm = new Kst.Acoustics.App.Electric.KesifHazirla.KesifHazirlaViewModel();
        var tablo = new KesifTablosu { Ad = "Eski Tablo Adı" };
        tablo.Satirlar.Add(new KesifSatir
        {
            PozNo = "ELK-101",
            BirimAdi = "Test Poz",
            Birim = "adet",
            Miktar = 5,
            BirimFiyat = 10
        });
        vm.Proje.Tablolar.Add(tablo);
        vm.HesaplariYenile();

        if (vm.IcmalSatirlari.Count != 1)
            throw new InvalidOperationException("İcmal satırı eklenmeliydi.");
        var icmalSatir = vm.IcmalSatirlari[0];
        if (icmalSatir.Kirilimlar.Count != 1 || icmalSatir.Kirilimlar[0].TabloAdi != "Eski Tablo Adı")
            throw new InvalidOperationException("Kırılımda eski tablo adı görünmeli.");

        // Rename table
        tablo.Ad = "Yeni Tablo Adı";
        vm.HesaplariYenile();

        if (vm.IcmalSatirlari.Count != 1)
            throw new InvalidOperationException("İcmal satırı güncellenmeliydi.");
        icmalSatir = vm.IcmalSatirlari[0];
        if (icmalSatir.Kirilimlar.Count != 1 || icmalSatir.Kirilimlar[0].TabloAdi != "Yeni Tablo Adı")
            throw new InvalidOperationException("Kırılımda yeni tablo adı görünmeli.");
    }

    // ======================= Faz 2: Otomatik metraj / Poz Atama / Referanslar =======================

    public static void EslemeAnahtariNormalizeTutarli()
    {
        var a = KesifEslemeAnahtar.Uret("Kablo", "Güç kablosu", "NHXMH 4x16 mm²", "m");
        var b = KesifEslemeAnahtar.Uret("  kablo ", "GÜÇ  KABLOSU", "nhxmh 4x16 mm²  ", " M ");
        if (a != b)
            throw new InvalidOperationException($"Aynı kalem farklı yazımda aynı anahtarı vermeli.\nA={a}\nB={b}");

        var c = KesifEslemeAnahtar.Uret("Kablo", "Güç kablosu", "NHXMH 4x25 mm²", "m");
        if (a == c)
            throw new InvalidOperationException("Farklı teknik özellik farklı anahtar vermeli.");
    }

    public static void PozEslemeServisiRoundTrip()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kesif-esleme-{Guid.NewGuid():N}.json");
        try
        {
            var kalem = new KesifMetrajSatiri
            {
                AnaGrup = "Kablo", AltGrup = "Güç kablosu", TeknikOzellik = "NHXMH 4x16 mm²", Birim = "m"
            };

            var servis = new KesifPozEslemeServisi(path);
            servis.EkleVeyaGuncelle(kalem, "ELK-KBL-16", "NHXMH 4x16 mm² güç kablosu");

            // Yeni örnek diskten okur — kalıcılık doğrulanır.
            var yeni = new KesifPozEslemeServisi(path);
            var bulunan = yeni.EslemeBul(kalem);
            if (bulunan is null || bulunan.PozNo != "ELK-KBL-16")
                throw new InvalidOperationException("Eşleşme diske yazılıp geri okunamadı.");

            // Güncelleme aynı anahtarda tekille kalmalı.
            yeni.EkleVeyaGuncelle(kalem, "ELK-KBL-16B", "Güncel poz");
            if (yeni.Tum().Count != 1)
                throw new InvalidOperationException("Aynı anahtar tekrar yazılınca tekilleşmeli.");

            yeni.Sil(bulunan.Id);
            var bos = new KesifPozEslemeServisi(path);
            if (bos.Tum().Count != 0)
                throw new InvalidOperationException("Silinen eşleşme diskten de kaldırılmalı.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public static void BirimFiyatElleDegistiTespitiVeRoundTrip()
    {
        var satir = new KesifSatir
        {
            PozNo = "ELK-1", BirimAdi = "Kablo", Birim = "m",
            BirimFiyat = 100, OrijinalBirimFiyat = 100, Miktar = 5
        };
        if (satir.BirimFiyatElleDegisti)
            throw new InvalidOperationException("Fiyat değişmemişken elle-değişti bayrağı false olmalı.");

        satir.BirimFiyat = 130;
        if (!satir.BirimFiyatElleDegisti)
            throw new InvalidOperationException("Birim fiyat değişince elle-değişti bayrağı true olmalı.");

        // Orijinal fiyat JSON round-trip'te korunmalı.
        var path = Path.Combine(Path.GetTempPath(), $"kesif-fiyat-{Guid.NewGuid():N}.e4p14");
        try
        {
            var proje = new KesifProje();
            var tablo = new KesifTablosu { Ad = "T" };
            tablo.Satirlar.Add(satir);
            proje.Tablolar.Add(tablo);
            KesifJsonServisi.Kaydet(proje, path);
            var geri = KesifJsonServisi.Ac(path).Tablolar.Single().Satirlar.Single();
            if (geri.OrijinalBirimFiyat != 100 || !geri.BirimFiyatElleDegisti)
                throw new InvalidOperationException("Orijinal birim fiyat ve elle-değişti durumu round-trip korunmalı.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public static void PozEslemeKontrolEdildiRoundTrip()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kesif-esleme-kontrol-{Guid.NewGuid():N}.json");
        try
        {
            var kalem = new KesifMetrajSatiri { AnaGrup = "Kablo", TeknikOzellik = "NHXMH 4x16 mm²", Birim = "m" };
            var servis = new KesifPozEslemeServisi(path);
            var esleme = servis.EkleVeyaGuncelle(kalem, "ELK-1", "Kablo");
            if (esleme.KontrolEdildi)
                throw new InvalidOperationException("Yeni eşleşme varsayılan olarak kontrol edilmemiş olmalı.");

            esleme.KontrolEdildi = true;
            servis.Kaydet();

            var yeni = new KesifPozEslemeServisi(path);
            var bulunan = yeni.EslemeBul(kalem);
            if (bulunan is null || !bulunan.KontrolEdildi)
                throw new InvalidOperationException("Kontrol edildi tiki diske yazılıp geri okunmalı.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public static void MetrajImportSablonRoundTrip()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kesif-metraj-sablon-{Guid.NewGuid():N}.xlsx");
        try
        {
            KesifMetrajImportServisi.SablonOlustur(path);
            var referans = KesifMetrajImportServisi.DosyadanOku(path);
            if (referans.MetrajSatirlari.Count != 1)
                throw new InvalidOperationException($"Şablon 1 örnek satır vermeli. Gelen: {referans.MetrajSatirlari.Count}");

            var s = referans.MetrajSatirlari[0];
            if (s.AnaGrup != "Kablo" || s.TeknikOzellik != "NHXMH 4x16 mm²" || s.Birim != "m" || s.Miktar != 120)
                throw new InvalidOperationException($"Şablon satırı doğru okunamadı: {s.AnaGrup}/{s.TeknikOzellik}/{s.Birim}/{s.Miktar}");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public static void MetrajImportBaslikYedinciSatirdaOkur()
    {
        // M13 Metraj Özeti gibi: başlık 1. satırda değil; üstte başlık bloğu var.
        var path = Path.Combine(Path.GetTempPath(), $"kesif-metraj-r7-{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var wb = new ClosedXML.Excel.XLWorkbook())
            {
                var ws = wb.Worksheets.Add("M13 Metraj Özeti");
                ws.Cell(1, 1).Value = "Proje Metraj Özeti";
                ws.Cell(2, 1).Value = "Açıklama satırı";
                ws.Cell(7, 1).Value = "Ana Grup";
                ws.Cell(7, 2).Value = "Alt Grup";
                ws.Cell(7, 3).Value = "Teknik Özellik";
                ws.Cell(7, 4).Value = "Birim";
                ws.Cell(7, 5).Value = "Miktar";
                ws.Cell(7, 6).Value = "Açıklama";
                ws.Cell(8, 1).Value = "Kablo"; ws.Cell(8, 2).Value = "Güç kablosu";
                ws.Cell(8, 3).Value = "NHXMH 4x16 mm²"; ws.Cell(8, 4).Value = "m";
                ws.Cell(8, 5).Value = 290; ws.Cell(8, 6).Value = "Çeşitli";
                ws.Cell(9, 1).Value = "Koruma Cihazı"; ws.Cell(9, 2).Value = "Otomatik sigorta (MCB)";
                ws.Cell(9, 3).Value = "Sigorta B 16A 10kA"; ws.Cell(9, 4).Value = "adet";
                ws.Cell(9, 5).Value = 76;
                wb.SaveAs(path);
            }

            var referans = KesifMetrajImportServisi.DosyadanOku(path);
            if (referans.MetrajSatirlari.Count != 2)
                throw new InvalidOperationException($"7. satır başlığı bulunup 2 satır okunmalı. Gelen: {referans.MetrajSatirlari.Count}");

            var kablo = referans.MetrajSatirlari[0];
            if (kablo.TeknikOzellik != "NHXMH 4x16 mm²" || kablo.Birim != "m" || kablo.Miktar != 290)
                throw new InvalidOperationException("Kablo metraj satırı yanlış okundu.");

            var sigorta = referans.MetrajSatirlari[1];
            if (sigorta.AnaGrup != "Koruma Cihazı" || sigorta.Miktar != 76 || sigorta.Birim != "adet")
                throw new InvalidOperationException("Cihaz metraj satırı yanlış okundu.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public static void OtomatikSatirUretimVeReferansSilEslemeyiKorur()
    {
        var eslemePath = Path.Combine(Path.GetTempPath(), $"kesif-esleme-vm-{Guid.NewGuid():N}.json");
        try
        {
            var vm = new Kst.Acoustics.App.Electric.KesifHazirla.KesifHazirlaViewModel(
                new KesifPozEslemeServisi(eslemePath));

            // Poz kütüphanesine bir poz al.
            vm.BirimFiyatListesiIceAktar("Test Liste", "x", new[]
            {
                new KesifPoz { PozNo = "ELK-KBL-16", BirimAdi = "NHXMH 4x16 güç kablosu", Birim = "m", BirimFiyat = 50 }
            }, digerListeleriPasifeAl: true);

            var referans = new KesifReferansDosyasi { DosyaAdi = "metraj.xlsx", KaynakModul = "M13" };
            referans.MetrajSatirlari.Add(new KesifMetrajSatiri
            {
                AnaGrup = "Kablo", AltGrup = "Güç kablosu", TeknikOzellik = "NHXMH 4x16 mm²", Birim = "m", Miktar = 120
            });

            vm.ReferansEkle(referans);

            // Eşleşme yokken otomatik satır oluşmamalı; kalem Poz Atama'da görünmeli.
            if (vm.SeciliTablo!.Satirlar.Count != 0)
                throw new InvalidOperationException("Eşleşme yokken otomatik satır oluşmamalı.");
            if (vm.EslenmemisKalemler.Count != 1)
                throw new InvalidOperationException("Eşlenmemiş kalem Poz Atama listesinde olmalı.");

            var poz = vm.PozKutuphane.First(p => p.PozNo == "ELK-KBL-16");
            vm.EslemeAtaVeUygula(referans.MetrajSatirlari[0], poz);

            if (vm.SeciliTablo!.Satirlar.Count != 1)
                throw new InvalidOperationException("Eşleşme sonrası otomatik satır seçili tabloya eklenmeli.");
            var satir = vm.SeciliTablo.Satirlar[0];
            if (satir.KaynakTipi != KesifKaynakTipi.Otomatik || satir.Miktar != 120 || satir.BirimFiyat != 50)
                throw new InvalidOperationException($"Otomatik satır değerleri hatalı: {satir.KaynakTipi}/{satir.Miktar}/{satir.BirimFiyat}");
            if (satir.KaynakReferansId != referans.Id)
                throw new InvalidOperationException("Otomatik satır referans Id ile etiketlenmeli.");
            if (vm.EslenmemisKalemler.Count != 0)
                throw new InvalidOperationException("Eşlenen kalem listeden çıkmalı.");

            // Referansı sil → otomatik satır gitmeli; öğrenilen eşleşme global DB'de KALMALI.
            vm.ReferansSil(referans);
            if (vm.SeciliTablo!.Satirlar.Count != 0)
                throw new InvalidOperationException("Referans silinince otomatik satırlar kaldırılmalı.");

            var dbYeni = new KesifPozEslemeServisi(eslemePath);
            if (dbYeni.Tum().Count != 1)
                throw new InvalidOperationException("Referans silinse de öğrenilen eşleşme korunmalı.");
        }
        finally
        {
            if (File.Exists(eslemePath)) File.Delete(eslemePath);
        }
    }

    public static void JsonRoundTripReferansVeKaynakReferansIdKorur()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kesif-ref-{Guid.NewGuid():N}.e4p14");
        try
        {
            var proje = new KesifProje { Ad = "Ref Test" };
            var tablo = new KesifTablosu { Ad = "Tablo 1" };
            var referansId = Guid.NewGuid();
            tablo.Satirlar.Add(new KesifSatir
            {
                PozNo = "ELK-OTO", BirimAdi = "Oto poz", Birim = "m", Miktar = 10, BirimFiyat = 5,
                KaynakTipi = KesifKaynakTipi.Otomatik, KaynakModul = "M13", KaynakReferansId = referansId
            });
            proje.Tablolar.Add(tablo);

            var referans = new KesifReferansDosyasi { Id = referansId, DosyaAdi = "metraj.xlsx", KaynakModul = "M13" };
            referans.MetrajSatirlari.Add(new KesifMetrajSatiri
            {
                AnaGrup = "Kablo", TeknikOzellik = "NHXMH 4x16 mm²", Birim = "m", Miktar = 10
            });
            proje.ReferansDosyalari.Add(referans);

            KesifJsonServisi.Kaydet(proje, path);
            var geri = KesifJsonServisi.Ac(path);

            if (geri.ReferansDosyalari.Count != 1 || geri.ReferansDosyalari[0].MetrajSatirlari.Count != 1)
                throw new InvalidOperationException("Referans dosyaları round-trip korunmadı.");
            var s = geri.Tablolar[0].Satirlar[0];
            if (s.KaynakReferansId != referansId || s.KaynakTipi != KesifKaynakTipi.Otomatik)
                throw new InvalidOperationException("KaynakReferansId / KaynakTipi round-trip korunmadı.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public static void AsenkronKaydetRoundTripCalisir()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kesif-async-{Guid.NewGuid():N}.e4p14");
        var backupPath = path + ".bak";
        try
        {
            var proje = new KesifProje { Ad = "Asenkron Kayıt" };
            proje.Tablolar.Add(new KesifTablosu { Ad = "A Blok" });
            var ilkParmakIzi = KesifJsonServisi.ParmakIziOlustur(proje);

            KesifJsonServisi.KaydetAsync(proje, path).GetAwaiter().GetResult();
            proje.Ad = "Güncel Kayıt";
            KesifJsonServisi.KaydetAsync(proje, path).GetAwaiter().GetResult();
            var geri = KesifJsonServisi.Ac(path);
            var yedek = KesifJsonServisi.Ac(backupPath);

            if (geri.Ad != "Güncel Kayıt" || geri.Tablolar.Single().Ad != "A Blok")
                throw new InvalidOperationException("Asenkron kayıt round-trip proje verisini korumadı.");
            if (yedek.Ad != "Asenkron Kayıt")
                throw new InvalidOperationException("Atomik kayıt önceki proje sürümünü .bak olarak korumadı.");
            if (ilkParmakIzi == KesifJsonServisi.ParmakIziOlustur(proje))
                throw new InvalidOperationException("Proje parmak izi veri değişikliğini algılamadı.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(backupPath)) File.Delete(backupPath);
        }
    }

    public static void PozAtamaAramasiPozNoVeAdaGoreFiltreler()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"kesif-filter-{Guid.NewGuid():N}.json");
        try
        {
            var eslemeServisi = new KesifPozEslemeServisi(dbPath);
            var vm = new Kst.Acoustics.App.Electric.KesifHazirla.KesifHazirlaViewModel(eslemeServisi);
            vm.BirimFiyatListesiIceAktar("Test", "x", new[]
            {
                new KesifPoz { PozNo = "ELK-001", BirimAdi = "LED armatür", Birim = "adet" },
                new KesifPoz { PozNo = "ELK-002", BirimAdi = "NHXMH kablo", Birim = "m" }
            }, digerListeleriPasifeAl: true);

            vm.PozAtamaAramaMetni = "kablo";
            if (vm.FiltrelenmisAtamaPozlari.Count != 1 || vm.FiltrelenmisAtamaPozlari[0].PozNo != "ELK-002")
                throw new InvalidOperationException("Poz Atama araması poz adına göre filtrelemedi.");

            vm.PozAtamaAramaMetni = "001";
            if (vm.FiltrelenmisAtamaPozlari.Count != 1 || vm.AtanacakPoz?.PozNo != "ELK-001")
                throw new InvalidOperationException("Poz Atama araması poz numarasına göre filtrelemedi.");

            var kablo = new KesifMetrajSatiri
            {
                AnaGrup = "Kablo", AltGrup = "Güç", TeknikOzellik = "NHXMH 4x16 mm²", Birim = "m"
            };
            var armatur = new KesifMetrajSatiri
            {
                AnaGrup = "Aydınlatma", AltGrup = "Armatür", TeknikOzellik = "LED panel", Birim = "adet"
            };
            var sigorta = new KesifMetrajSatiri
            {
                AnaGrup = "Koruma", AltGrup = "Sigorta", TeknikOzellik = "MCB 16 A", Birim = "adet"
            };
            vm.Proje.ReferansDosyalari.Add(new KesifReferansDosyasi
            {
                DosyaAdi = "metraj.xlsx",
                MetrajSatirlari = { kablo, armatur, sigorta }
            });
            eslemeServisi.EkleVeyaGuncelle(kablo, "ELK-002", "NHXMH kablo");
            vm.EslemeleriYenile();
            vm.EslenmemisKalemleriYenile();

            vm.PozAtamaKalemAramaMetni = "armatür";
            if (vm.FiltrelenmisEslenmemisKalemler.Count != 1
                || vm.FiltrelenmisEslenmemisKalemler[0] != armatur
                || vm.FiltrelenmisPozEslemeleri.Count != 0)
                throw new InvalidOperationException("Poz Atama metraj araması eşlenmemiş kalemleri filtrelemedi.");

            vm.PozAtamaKalemAramaMetni = "ELK-002";
            if (vm.FiltrelenmisEslenmemisKalemler.Count != 0
                || vm.FiltrelenmisPozEslemeleri.Count != 1
                || vm.FiltrelenmisPozEslemeleri[0].PozNo != "ELK-002")
                throw new InvalidOperationException("Poz Atama eşleşme araması poz numarasına göre filtrelemedi.");
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    public static void OtomatikPozEslemeModelKesitVeAnmaDegeriniKorur()
    {
        var kablo = new KesifMetrajSatiri
        {
            AnaGrup = "Kablo", AltGrup = "Güç kablosu", TeknikOzellik = "NHXMH 4x16 mm²", Birim = "m"
        };
        var sigorta = new KesifMetrajSatiri
        {
            AnaGrup = "Koruma Cihazı", AltGrup = "Otomatik sigorta (MCB)", TeknikOzellik = "Sigorta 10A 6kA", Birim = "adet"
        };
        var pozlar = new[]
        {
            new KesifPoz { PozNo = "YANLIS-MODEL", BirimAdi = "4x16 mm² N2XH kablo", Birim = "m", BirimFiyat = 100 },
            new KesifPoz { PozNo = "DOGRU-KABLO", BirimAdi = "4x16 mm² NHXMH tipi halojensiz kablo", Birim = "m", BirimFiyat = 100 },
            new KesifPoz { PozNo = "YANLIS-KA", BirimAdi = "16 A'e kadar 10 kA otomatik sigorta", Birim = "Adet", BirimFiyat = 100 },
            new KesifPoz { PozNo = "DOGRU-SIGORTA", BirimAdi = "16 A'e kadar 6 kA otomatik sigorta", Birim = "Adet", BirimFiyat = 100 }
        };

        var sonuc = KesifOtomatikPozEslemeServisi.Esle(new[] { kablo, sigorta }, pozlar);
        if (sonuc.Count != 2 || sonuc.Single(s => s.Kalem == kablo).Poz.PozNo != "DOGRU-KABLO")
            throw new InvalidOperationException("Otomatik eşleme kablo model/kesit kimliğini korumadı.");
        if (sonuc.Single(s => s.Kalem == sigorta).Poz.PozNo != "DOGRU-SIGORTA")
            throw new InvalidOperationException("Otomatik eşleme sigorta kesme kapasitesini korumadı.");
    }

    public static void OtomatikPozEslemeAlanDisiAdayiReddeder()
    {
        var pano = new KesifMetrajSatiri
        {
            AnaGrup = "Pano", AltGrup = "Duvar Tipi", TeknikOzellik = "0,12 m² standart ölçü", Birim = "adet"
        };
        var jenerator = new KesifMetrajSatiri
        {
            AnaGrup = "Jeneratör Sistemi", AltGrup = "Jeneratör", TeknikOzellik = "Aksa APD-1100 / Standby", Birim = "adet"
        };
        var pozlar = new[]
        {
            new KesifPoz { PozNo = "MOTOR", BirimAdi = "0,12 kW elektrik motoru", Birim = "adet", BirimFiyat = 100 },
            new KesifPoz { PozNo = "PANO", BirimAdi = "0,10-0,20 m² gömme tip galvaniz sac panolar", Birim = "adet", BirimFiyat = 100 },
            new KesifPoz { PozNo = "TELEFON", BirimAdi = "1100 iç hat telefon santrali", Birim = "adet", BirimFiyat = 100 }
        };

        var sonuc = KesifOtomatikPozEslemeServisi.Esle(new[] { pano, jenerator }, pozlar);
        if (sonuc.Count != 1 || sonuc[0].Poz.PozNo != "PANO")
            throw new InvalidOperationException("Alan filtresi pano/jeneratör için ilgisiz pozları ayıklamadı.");
    }

    public static void OtomatikPozAtamaEslemeyiOgrenirVeSatirUretir()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"kesif-auto-{Guid.NewGuid():N}.json");
        try
        {
            var eslemeServisi = new KesifPozEslemeServisi(dbPath);
            var vm = new Kst.Acoustics.App.Electric.KesifHazirla.KesifHazirlaViewModel(eslemeServisi);
            vm.BirimFiyatListesiIceAktar("Test", "x", new[]
            {
                new KesifPoz { PozNo = "35.153.2020", BirimAdi = "4x16 mm² NHXMH tipi halojensiz kablo", Birim = "m", BirimFiyat = 250 }
            }, digerListeleriPasifeAl: true);

            var referans = new KesifReferansDosyasi { DosyaAdi = "metraj.xlsx", KaynakModul = "M13" };
            referans.MetrajSatirlari.Add(new KesifMetrajSatiri
            {
                AnaGrup = "Kablo", AltGrup = "Güç kablosu", TeknikOzellik = "NHXMH 4x16 mm²", Birim = "m", Miktar = 25
            });
            vm.ReferansEkle(referans);
            vm.TumEslenmemisPozlariOtomatikAta();

            if (vm.EslenmemisKalemler.Count != 0 || vm.SeciliTablo!.Satirlar.Count != 1)
                throw new InvalidOperationException("Otomatik atama eşlenmemiş kalemi keşif satırına dönüştürmedi.");
            if (vm.SeciliTablo.Satirlar[0].PozNo != "35.153.2020" || eslemeServisi.Tum().Count != 1)
                throw new InvalidOperationException("Otomatik atama sonucu öğrenme veritabanına yazılmadı.");
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    public static void ExcelRaporuIcmalOzetVeTabloSayfalariniUretir()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kesif-excel-{Guid.NewGuid():N}.xlsx");
        try
        {
            var proje = new KesifProje { Ad = "Örnek Keşif" };
            var a = new KesifTablosu { Ad = "A Blok" };
            var b = new KesifTablosu { Ad = "B Blok" };
            a.Satirlar.Add(new KesifSatir
            {
                PozNo = "35.100", BirimAdi = "LED armatür", Birim = "adet", Miktar = 10, BirimFiyat = 100
            });
            b.Satirlar.Add(new KesifSatir
            {
                PozNo = "35.100", BirimAdi = "LED armatür", Birim = "adet", Miktar = 5, BirimFiyat = 110
            });
            proje.Tablolar.Add(a);
            proje.Tablolar.Add(b);

            KesifExcelExportServisi.RaporOlustur(proje, path);
            using var workbook = new ClosedXML.Excel.XLWorkbook(path);

            var adlar = workbook.Worksheets.Select(s => s.Name).ToArray();
            if (!adlar.SequenceEqual(new[] { "Genel İcmal", "Tablo Özeti", "A Blok", "B Blok" }))
                throw new InvalidOperationException($"Excel sayfa sırası hatalı: {string.Join(", ", adlar)}");

            var genel = workbook.Worksheet("Genel İcmal");
            if (genel.Cell(5, 2).GetString() != "35.100" || genel.Cell(5, 5).GetValue<decimal>() != 15)
                throw new InvalidOperationException("İlk sayfadaki konsolide poz/miktar bilgisi hatalı.");
            if (!genel.Cell(6, 7).HasFormula)
                throw new InvalidOperationException("Genel toplam Excel formülü olmalı.");

            var ozet = workbook.Worksheet("Tablo Özeti");
            if (ozet.Cell(5, 2).GetString() != "A Blok" || ozet.Cell(6, 2).GetString() != "B Blok")
                throw new InvalidOperationException("İkinci sayfada tablo adları listelenmedi.");
            if (!ozet.Cell(5, 4).HasFormula || !ozet.Cell(6, 4).HasFormula)
                throw new InvalidOperationException("Tablo özeti toplamları ayrıntı sayfalarına bağlı formül olmalı.");

            if (!workbook.Worksheet("A Blok").Cell(5, 7).HasFormula ||
                workbook.Worksheet("A Blok").Cell(5, 3).GetString() != "LED armatür")
                throw new InvalidOperationException("Ayrıntı sayfası satır/formül yapısı hatalı.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public static void ExcelAcikDosyaninUzerineYazmayiReddeder()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kesif-excel-lock-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "rapor.xlsx");
        var lockPath = Path.Combine(dir, "~$rapor.xlsx");
        File.WriteAllText(lockPath, "lock");
        try
        {
            if (!KesifExcelExportServisi.DosyaAcikMi(path))
                throw new InvalidOperationException("Excel geçici kilit dosyası algılanmadı.");

            var proje = new KesifProje();
            proje.Tablolar.Add(new KesifTablosu { Ad = "Test" });
            try
            {
                KesifExcelExportServisi.RaporOlustur(proje, path);
                throw new InvalidOperationException("Açık Excel dosyasının üzerine yazma reddedilmeliydi.");
            }
            catch (IOException)
            {
                // Beklenen.
            }
        }
        finally
        {
            if (File.Exists(lockPath)) File.Delete(lockPath);
            if (File.Exists(path)) File.Delete(path);
            Directory.Delete(dir);
        }
    }

    public static void AyniPozIkinciKezEkleninceMiktariArtar()
    {
        var vm = new Kst.Acoustics.App.Electric.KesifHazirla.KesifHazirlaViewModel(
            new KesifPozEslemeServisi(Path.Combine(Path.GetTempPath(), $"kesif-tekil-{Guid.NewGuid():N}.json")));
        vm.BirimFiyatListesiIceAktar("Test", "x", new[]
        {
            new KesifPoz { PozNo = "35.001", BirimAdi = "Test imalatı", Birim = "adet", BirimFiyat = 100 }
        }, digerListeleriPasifeAl: true);
        vm.SeciliPoz = vm.PozKutuphane.Single();

        vm.PozuTabloyaEkleCommand.Execute(null);
        vm.PozuTabloyaEkleCommand.Execute(null);

        if (vm.SeciliTablo!.Satirlar.Count != 1 || vm.SeciliTablo.Satirlar[0].Miktar != 2)
            throw new InvalidOperationException("Aynı poz ikinci kez eklenince yeni satır yerine miktar artmalı.");
    }

    public static void MevcutProjedeYinelenenPozlarToplanir()
    {
        var proje = new KesifProje { Ad = "Yinelenen Test" };
        var tablo = new KesifTablosu { Ad = "A Blok" };
        tablo.Satirlar.Add(new KesifSatir
        {
            PozNo = "35.002", BirimAdi = "Kablo", Birim = "m", Miktar = 2, BirimFiyat = 10
        });
        tablo.Satirlar.Add(new KesifSatir
        {
            PozNo = "35.002", BirimAdi = "Kablo", Birim = "m", Miktar = 3, BirimFiyat = 20
        });
        proje.Tablolar.Add(tablo);

        var vm = new Kst.Acoustics.App.Electric.KesifHazirla.KesifHazirlaViewModel(
            new KesifPozEslemeServisi(Path.Combine(Path.GetTempPath(), $"kesif-eski-{Guid.NewGuid():N}.json")));
        vm.ProjeYukle(proje);

        var tek = vm.SeciliTablo!.Satirlar.Single();
        if (tek.Miktar != 5 || tek.BirimFiyat != 16 || tek.SatirToplam != 80)
            throw new InvalidOperationException($"Yinelenen poz miktar/tutar koruyarak birleşmedi: {tek.Miktar}/{tek.BirimFiyat}/{tek.SatirToplam}");
    }

    public static void BirlesenOtomatikPozdaReferansSilmeYalnizKendiMiktariniDusurur()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"kesif-katki-{Guid.NewGuid():N}.json");
        var projePath = Path.Combine(Path.GetTempPath(), $"kesif-katki-{Guid.NewGuid():N}.e4p14");
        try
        {
            var vm = new Kst.Acoustics.App.Electric.KesifHazirla.KesifHazirlaViewModel(new KesifPozEslemeServisi(dbPath));
            vm.BirimFiyatListesiIceAktar("Test", "x", new[]
            {
                new KesifPoz { PozNo = "35.003", BirimAdi = "NHXMH 4x16 kablo", Birim = "m", BirimFiyat = 50 }
            }, digerListeleriPasifeAl: true);

            KesifReferansDosyasi Referans(string ad, decimal miktar)
            {
                var referans = new KesifReferansDosyasi { DosyaAdi = ad, KaynakModul = "M13" };
                referans.MetrajSatirlari.Add(new KesifMetrajSatiri
                {
                    AnaGrup = "Kablo", AltGrup = "Güç kablosu", TeknikOzellik = "NHXMH 4x16 mm²", Birim = "m", Miktar = miktar
                });
                return referans;
            }

            var r1 = Referans("r1.xlsx", 10);
            var r2 = Referans("r2.xlsx", 20);
            vm.ReferansEkle(r1);
            vm.EslemeAtaVeUygula(r1.MetrajSatirlari[0], vm.PozKutuphane.Single());
            vm.ReferansEkle(r2);

            var birlesik = vm.SeciliTablo!.Satirlar.Single();
            if (birlesik.Miktar != 30 || birlesik.OtomatikKatkilar.Count != 2)
                throw new InvalidOperationException("İki referans aynı pozda tek satır ve iki katkı üretmeli.");

            KesifJsonServisi.Kaydet(vm.Proje, projePath);
            var geri = KesifJsonServisi.Ac(projePath).Tablolar.Single().Satirlar.Single();
            if (geri.Miktar != 30 || geri.OtomatikKatkilar.Count != 2)
                throw new InvalidOperationException("Birleştirilmiş referans katkıları JSON round-trip korunmalı.");

            vm.ReferansSil(r1);
            var kalan = vm.SeciliTablo.Satirlar.Single();
            if (kalan.Miktar != 20 || kalan.OtomatikKatkilar.Single().KaynakReferansId != r2.Id)
                throw new InvalidOperationException("Referans silme yalnız silinen referansın miktarını düşürmeli.");

            vm.ReferansSil(r2);
            if (vm.SeciliTablo.Satirlar.Count != 0)
                throw new InvalidOperationException("Son otomatik referans silinince boşalan satır kaldırılmalı.");
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
            if (File.Exists(projePath)) File.Delete(projePath);
        }
    }

    // ======================= Alt keşif tabloları (hiyerarşi) =======================

    public static void HiyerarsikTabloToplamiVeIcmal()
    {
        var proje = new KesifProje();
        var ust = new KesifTablosu { Ad = "Kuvvetli Akım" };
        var altA = new KesifTablosu { Ad = "Aydınlatma" };
        var altB = new KesifTablosu { Ad = "Priz" };
        altA.Satirlar.Add(new KesifSatir { PozNo = "A1", BirimAdi = "Armatür", Birim = "adet", Miktar = 10, BirimFiyat = 5 });
        altB.Satirlar.Add(new KesifSatir { PozNo = "P1", BirimAdi = "Priz", Birim = "adet", Miktar = 2, BirimFiyat = 100 });
        ust.AltTablolar.Add(altA);
        ust.AltTablolar.Add(altB);
        proje.Tablolar.Add(ust);

        if (ust.Toplam != 250)
            throw new InvalidOperationException($"Üst tablo toplamı alt tabloları kapsamalı. Gelen: {ust.Toplam}");
        if (proje.GenelToplam != 250)
            throw new InvalidOperationException("Genel toplam hiyerarşik toplamı yansıtmalı.");
        if (!ust.AltTabloVarMi || ust.YaprakMi)
            throw new InvalidOperationException("Alt tablosu olan tablo başlık (yaprak değil) sayılmalı.");

        var icmal = KesifHesapServisi.IcmalOlustur(proje);
        if (icmal.Count != 2)
            throw new InvalidOperationException($"İcmal alt tablolardaki tüm pozları kapsamalı. Gelen: {icmal.Count}");
        var priz = icmal.Single(x => x.PozNo == "P1");
        if (priz.Kirilimlar.Count != 1 || priz.Kirilimlar[0].TabloAdi != "Priz")
            throw new InvalidOperationException("Kırılım yaprak tablo adını göstermeli.");
    }

    public static void HiyerarsiJsonRoundTrip()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kesif-hiyerarsi-{Guid.NewGuid():N}.e4p14");
        try
        {
            var proje = new KesifProje { Ad = "Hiyerarşi" };
            proje.Bilgiler.Isveren = "Belediye";
            proje.Bilgiler.Yuklenici = "ABC İnşaat";
            var ust = new KesifTablosu { Ad = "Zayıf Akım" };
            var alt = new KesifTablosu { Ad = "Kamera" };
            var altAlt = new KesifTablosu { Ad = "Dış Kamera" };
            altAlt.Satirlar.Add(new KesifSatir { PozNo = "K1", BirimAdi = "Dome kamera", Birim = "adet", Miktar = 4, BirimFiyat = 2500 });
            alt.AltTablolar.Add(altAlt);
            ust.AltTablolar.Add(alt);
            proje.Tablolar.Add(ust);

            KesifJsonServisi.Kaydet(proje, path);
            var geri = KesifJsonServisi.Ac(path);

            if (geri.Bilgiler.Isveren != "Belediye" || geri.Bilgiler.Yuklenici != "ABC İnşaat")
                throw new InvalidOperationException("Proje bilgileri round-trip korunmadı.");
            var gUst = geri.Tablolar.Single();
            var gAlt = gUst.AltTablolar.Single();
            var gAltAlt = gAlt.AltTablolar.Single();
            if (gAltAlt.Satirlar.Count != 1 || gAltAlt.Satirlar[0].PozNo != "K1")
                throw new InvalidOperationException("3 seviyeli alt tablo hiyerarşisi round-trip korunmadı.");
            if (geri.GenelToplam != 10000)
                throw new InvalidOperationException($"Hiyerarşik toplam round-trip sonrası hatalı: {geri.GenelToplam}");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public static void AltTabloDerinlikVeBaslikKurallari()
    {
        var eslemePath = Path.Combine(Path.GetTempPath(), $"kesif-derinlik-{Guid.NewGuid():N}.json");
        try
        {
            var vm = new Kst.Acoustics.App.Electric.KesifHazirla.KesifHazirlaViewModel(
                new KesifPozEslemeServisi(eslemePath));
            var ust = vm.SeciliTablo!;  // varsayılan tek tablo (seviye 1)

            vm.AltTabloEkleCommand.Execute(null);   // seviye 2 oluşur ve seçilir
            var seviye2 = vm.SeciliTablo!;
            if (KesifTabloAgaci.Derinlik(vm.Proje, seviye2) != 2)
                throw new InvalidOperationException("Alt tablo 2. seviyede olmalı.");

            vm.AltTabloEkleCommand.Execute(null);   // seviye 3
            var seviye3 = vm.SeciliTablo!;
            if (KesifTabloAgaci.Derinlik(vm.Proje, seviye3) != 3)
                throw new InvalidOperationException("Alt-alt tablo 3. seviyede olmalı.");

            vm.AltTabloEkleCommand.Execute(null);   // 4. seviye engellenmeli
            if (seviye3.AltTablolar.Count != 0)
                throw new InvalidOperationException("3 seviyeden derin alt tablo eklenememeli.");

            // Üst tablo başlık olduğundan satır eklenememeli.
            vm.SeciliTablo = ust;
            vm.SeciliPoz = new KesifPoz { PozNo = "X", BirimAdi = "X imalat", Birim = "adet", BirimFiyat = 1 };
            vm.PozuTabloyaEkleCommand.Execute(null);
            if (ust.Satirlar.Count != 0)
                throw new InvalidOperationException("Alt tablosu olan başlık tabloya doğrudan satır eklenememeli.");

            // Yaprak tabloya satır eklenebilmeli; satırı olan tabloya alt tablo eklenememeli.
            var yaprak = seviye3;
            vm.SeciliTablo = yaprak;
            vm.SeciliPoz = new KesifPoz { PozNo = "Y", BirimAdi = "Y imalat", Birim = "adet", BirimFiyat = 1 };
            vm.PozuTabloyaEkleCommand.Execute(null);
            if (yaprak.Satirlar.Count != 1)
                throw new InvalidOperationException("Yaprak tabloya satır eklenebilmeli.");
            vm.AltTabloEkleCommand.Execute(null);
            if (yaprak.AltTablolar.Count != 0)
                throw new InvalidOperationException("Satırı olan tabloya alt tablo eklenememeli (üst tablo yalnız başlık).");
        }
        finally
        {
            if (File.Exists(eslemePath)) File.Delete(eslemePath);
        }
    }
}
