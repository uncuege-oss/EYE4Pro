using Kst.Acoustics.Core.BuildingAcoustics.Database;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.BuildingAcoustics.Parsers;
using Kst.Acoustics.Core.BuildingAcoustics.Reports;
using Kst.Acoustics.Core.BuildingAcoustics.Workflow;
using Kst.Acoustics.Core.Common.Geometry;
using Xunit;

namespace Kst.Acoustics.Tests;

public sealed class M08ProfessionalWorkflowTests
{
    [Fact]
    public void PublishedStandardEditionsAreDefaultedAndLegacyBlanksAreNormalized()
    {
        var ayarlar = new M08RaporAyarlari();
        Assert.Contains("12354-1:2017", ayarlar.HesapStandartlariSurumu, StringComparison.Ordinal);
        Assert.Contains("717-1:2020", ayarlar.DerecelendirmeStandartlariSurumu, StringComparison.Ordinal);
        Assert.Contains("16283-2:2020", ayarlar.OlcumStandartlariSurumu, StringComparison.Ordinal);
        Assert.Contains("16032:2024", ayarlar.ServisOlcumStandartlariSurumu, StringComparison.Ordinal);
        Assert.Equal("ISO 12999-1:2020", ayarlar.BelirsizlikStandardiSurumu);

        var proje = Project();
        proje.RaporAyarlari.HesapStandartlariSurumu = "";
        proje.RaporAyarlari.DerecelendirmeStandartlariSurumu = "";
        proje.RaporAyarlari.OlcumStandartlariSurumu = "";
        proje.RaporAyarlari.ServisOlcumStandartlariSurumu = "";
        proje.RaporAyarlari.BelirsizlikStandardiSurumu = "";
        BinaProjeMigrator.Normalize(proje);

        Assert.Equal(M08GuncelStandartSurumleri.Hesap, proje.RaporAyarlari.HesapStandartlariSurumu);
        Assert.Equal(M08GuncelStandartSurumleri.ServisOlcumu, proje.RaporAyarlari.ServisOlcumStandartlariSurumu);
    }

    [Fact]
    public void ScenarioRestoresAcousticAssignmentsWithoutReplacingGeometry()
    {
        var p = Project(); var room = p.Katlar[0].Mekanlar[0]; var wall = room.Bolmeler[0];
        var originalRoom = room; var originalWall = wall; var service = new M08SenaryoServisi();
        var scenario = service.Kaydet(p, "C çözümü");
        room.KullanimTipi = "Değişti"; room.EmilimAlani = 99; wall.YapiElemani = null;

        var count = service.Uygula(p, scenario);

        Assert.Same(originalRoom, p.Katlar[0].Mekanlar[0]);
        Assert.Same(originalWall, p.Katlar[0].Mekanlar[0].Bolmeler[0]);
        Assert.Equal("Oturma Odaları", room.KullanimTipi);
        Assert.Equal(8, room.EmilimAlani, 3);
        Assert.NotNull(wall.YapiElemani);
        Assert.True(count > 5);
    }

    [Fact]
    public void BulkCsvValidatesEverythingBeforeChangingProject()
    {
        var p = Project(); var room = p.Katlar[0].Mekanlar[0]; var csv = M08TopluCsvServisi.Olustur(p);
        room.Ad = "Sonradan değişen";
        var restored = M08TopluCsvServisi.MetindenUygula(p, csv);
        Assert.True(restored.Basarili, string.Join(" | ", restored.Hatalar));
        Assert.Equal("Kaynak", room.Ad);

        var before = room.Ad;
        var invalid = csv.Replace(room.Id.ToString(), Guid.NewGuid().ToString(), StringComparison.Ordinal);
        var rejected = M08TopluCsvServisi.MetindenUygula(p, invalid);
        Assert.False(rejected.Basarili);
        Assert.Equal(before, room.Ad);
    }

    [Fact]
    public void TemplateAndProductDocumentRoundTripAreTraceable()
    {
        var p = Project(); var root = Path.Combine(Path.GetTempPath(), "m08-prof-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var templatePath = Path.Combine(root, "template.e4t08.json");
            var template = M08SablonServisi.ProjedenOlustur(p, "Konut standardı");
            M08SablonServisi.Kaydet(template, templatePath);
            var loaded = M08SablonServisi.Oku(templatePath);
            p.Katlar[0].Mekanlar[0].KullanimTipi = "Bozuk";
            var applied = M08SablonServisi.Uygula(p, loaded);
            Assert.True(applied.MekanGuncellemeSayisi > 0);
            Assert.Equal("Oturma Odaları", p.Katlar[0].Mekanlar[0].KullanimTipi);

            var source = Path.Combine(root, "test-report.pdf");
            File.WriteAllText(source, "laboratuvar sonucu");
            var product = p.YapiElemaniKutuphanesi[0];
            var doc = UrunBelgesiServisi.Ekle(product, source, UrunBelgesiTuru.AkustikTestRaporu, Path.Combine(root, "archive"));
            Assert.True(UrunBelgesiServisi.Dogrula(doc, out var detail), detail);
            Assert.Equal(64, doc.Sha256.Length);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void QuantityAndHtmlReportContainProfessionalSections()
    {
        var p = Project();
        var rows = new AkustikMetrajServisi().Hesapla(p);
        Assert.NotEmpty(rows);
        Assert.All(rows, x => Assert.True(x.Miktar > 0));
        var html = new MimariAkustikRaporServisi().HtmlOlustur(p);
        Assert.Contains("Saha Ölçümü ile Model Doğrulaması", html, StringComparison.Ordinal);
        Assert.Contains("Akustik Metraj ve Yaklaşık Maliyet", html, StringComparison.Ordinal);
        Assert.Contains("Onay, Denetim İzi ve Ürün Belgeleri", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportProfilesApplicabilityExclusionsAndMeasurementFilterAreVisible()
    {
        var p = Project();
        p.RaporAyarlari.RaporNumarasi = "M08-2026-001";
        p.RaporAyarlari.RevizyonKodu = "01";
        p.RaporAyarlari.HazirlayanAdi = "Uzman Adı";
        p.RaporAyarlari.HazirlayanUnvani = "Bina Akustiği Uzmanı";
        p.RaporAyarlari.KapsamaEsasKatSayisi = 8;
        p.RaporAyarlari.ToplamInsaatAlaniM2 = 2500;

        var sonucId = new BinaAkustikRaporServisi().TabloVerisiOlustur(p).First().SonucId;
        p.RaporKapsamDisiKayitlari.Add(new M08RaporKapsamDisiKaydi
        {
            SonucId = sonucId, Gerekce = "Temsilî eleman başka katta ölçülecek", Kullanici = "Uzman Adı",
        });
        p.SahaOlcumleri.Add(new SahaOlcumKaydi
        {
            Ad = "Onaylı ölçüm", Tur = SahaOlcumTuru.BolmeHavaSesi, BolmeId = sonucId,
            Deger = 56, Durum = SahaOlcumDurumu.Dogrulandi, RaporlamaIcinOnayli = true,
            Standart = "TS EN ISO 16283-1", Cihaz = "Cihaz", Operator = "Operatör", Belirsizlik = 1,
        });
        p.SahaOlcumleri.Add(new SahaOlcumKaydi
        {
            Ad = "Taslak ölçüm rapora girmemeli", Tur = SahaOlcumTuru.BolmeHavaSesi,
            BolmeId = sonucId, Deger = 40, Durum = SahaOlcumDurumu.Taslak, RaporlamaIcinOnayli = true,
        });

        var kontrol = M08RaporUygulanabilirlikServisi.Degerlendir(p, new HashSet<Guid> { sonucId });
        Assert.True(kontrol.AkustikProjeGerekli);
        var html = new MimariAkustikRaporServisi().HtmlOlustur(p, new HashSet<Guid> { sonucId });
        Assert.Contains("Belge Kontrolü ve Uygulanabilirlik Kararı", html, StringComparison.Ordinal);
        Assert.Contains("Temsilî eleman başka katta ölçülecek", html, StringComparison.Ordinal);
        Assert.Contains("Onaylı ölçüm", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Taslak ölçüm rapora girmemeli", html, StringComparison.Ordinal);

        p.RaporAyarlari.YayinDurumu = M08RaporYayinDurumu.OnayaHazir;
        Assert.Throws<InvalidOperationException>(() => new MimariAkustikRaporServisi().HtmlOlustur(p));
    }

    private static BinaProje Project()
    {
        var wallMaterial = new YapiElemani { Id = Guid.NewGuid(), Ad = "Duvar 58", Tip = YapiElemaniTipi.Duvar,
            Rw = 58, BirimFiyat = 950, ParaBirimi = "TRY", FireYuzdesi = 5 };
        var floorMaterial = new YapiElemani { Id = Guid.NewGuid(), Ad = "Döşeme", Tip = YapiElemaniTipi.Doseme,
            Rw = 55, Lnw = 70, DeltaLw = 15, BirimFiyat = 600 };
        var ceilingMaterial = new YapiElemani { Id = Guid.NewGuid(), Ad = "Tavan", Tip = YapiElemaniTipi.Tavan,
            Rw = 45, BirimFiyat = 400 };
        var source = Room("Kaynak", 0, 4); source.KullanimTipi = "Oturma Odaları"; source.KaynakDerecesi = "OG";
        source.EmilimAlani = 8; source.ZeminYapiElemani = floorMaterial; source.TavanYapiElemani = ceilingMaterial;
        var receiver = Room("Alıcı", 4, 8); receiver.KullanimTipi = "Yatak Odaları"; receiver.AliciSinifi = "I";
        receiver.ZeminYapiElemani = floorMaterial; receiver.TavanYapiElemani = ceilingMaterial;
        source.Bolmeler.Add(new Bolme { Id = Guid.NewGuid(), Ad = "Ayırıcı", Alan = 12, HasGeometry = true,
            Baslangic = new Point2D(4, 0), Bitis = new Point2D(4, 4), KaynakMekan = source,
            AliciMekan = receiver, YapiElemani = wallMaterial });
        var floor = new Kat { KatNo = 0, Ad = "Zemin", KatYuksekligi = 3 }; floor.Mekanlar.Add(source); floor.Mekanlar.Add(receiver);
        return new BinaProje { ProjeAdi = "Profesyonel M08", Adres = "Ankara", MusteriAdi = "Test",
            SorumluKisiKurulus = "Uzman", DisGurultuKullaniciTarafindanDogrulandi = true,
            YapiElemaniKutuphanesi = [wallMaterial, floorMaterial, ceilingMaterial], Katlar = [floor] };
    }

    private static Mekan Room(string name, double x1, double x2) => new()
    {
        Id = Guid.NewGuid(), Ad = name, Alan = 16, Yukseklik = 3,
        Poligon = [new(x1, 0), new(x2, 0), new(x2, 4), new(x1, 4)],
    };
}
