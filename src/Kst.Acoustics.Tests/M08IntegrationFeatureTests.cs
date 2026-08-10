using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kst.Acoustics.Core.BuildingAcoustics.Field;
using Kst.Acoustics.Core.BuildingAcoustics.Interop;
using Kst.Acoustics.Core.BuildingAcoustics.Mapping;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.BuildingAcoustics.Portfolio;
using Kst.Acoustics.Core.BuildingAcoustics.Reports;
using Kst.Acoustics.Core.BuildingAcoustics.Workflow;
using Kst.Acoustics.Core.Common.Geometry;
using Xunit;

namespace Kst.Acoustics.Tests;

public sealed class M08IntegrationFeatureTests
{
    [Fact]
    public void SnapshotFingerprintPreflightAndApprovalAreTraceable()
    {
        var project = CompleteInternalProject();
        var at = new DateTimeOffset(2026, 7, 14, 8, 0, 0, TimeSpan.Zero);
        var first = AkustikRaporSnapshotServisi.Olustur(project, olusturulmaUtc: at);
        var second = AkustikRaporSnapshotServisi.Olustur(project, olusturulmaUtc: at.AddHours(1));

        Assert.NotEmpty(first.Sonuclar);
        Assert.Equal(first.RaporParmakIziSha256, second.RaporParmakIziSha256);
        Assert.True(AkustikRaporSnapshotServisi.ParmakIziGecerliMi(first));
        var preflight = RaporOnKontrolServisi.Denetle(first);
        Assert.True(preflight.OnaylanabilirMi, string.Join(" | ", preflight.Bulgular.Select(x => $"{x.Kod}:{x.Mesaj}")));

        var approval = RaporOnayServisi.Onayla(project, first, "Test Uzmanı", "EYE4PRO Test");
        Assert.NotNull(approval.OnayKaydi);
        Assert.Equal(first.RaporParmakIziSha256, approval.OnayKaydi!.GirdiParmakIzi);
        Assert.Single(project.DenetimKayitlari);

        project.ProjeAdi = "Değişen proje";
        var changed = AkustikRaporSnapshotServisi.Olustur(project, olusturulmaUtc: at);
        Assert.NotEqual(first.RaporParmakIziSha256, changed.RaporParmakIziSha256);
    }

    [Fact]
    public void E4R08PackageVerifiesHashesSignatureAndTampering()
    {
        var project = CompleteInternalProject();
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=EYE4PRO M08 Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(7));
        using var package = new MemoryStream();

        var created = E4R08RaporPaketServisi.Olustur(project, package, imzaSertifikasi: certificate,
            olusturulmaUtc: new DateTimeOffset(2026, 7, 14, 8, 0, 0, TimeSpan.Zero));
        Assert.True(created.KriptografikImzali);
        package.Position = 0;
        var verified = E4R08RaporPaketServisi.Dogrula(package);
        Assert.True(verified.Gecerli, string.Join(" | ", verified.Hatalar));
        Assert.True(verified.KriptografikImzaGecerli);
        Assert.Contains("nitelikli elektronik imza değildir", verified.Manifest!.ImzaAciklamasi, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(verified.Manifest.Dosyalar, x => x.Yol == "data/report-settings.json");
        Assert.Contains(verified.Manifest.Dosyalar, x => x.Yol == "data/measurements.json");
        Assert.Contains(verified.Manifest.Dosyalar, x => x.Yol == "data/products.json");
        Assert.Contains(verified.Manifest.Dosyalar, x => x.Yol == "data/junctions.json");

        package.Position = 0;
        using var tampered = TamperReport(package);
        var rejected = E4R08RaporPaketServisi.Dogrula(tampered);
        Assert.False(rejected.Gecerli);
        Assert.Contains(rejected.Hatalar, x => x.Contains("report.html", StringComparison.Ordinal));
    }

    [Fact]
    public void IfcAndFacadeExportsCarryCanonicalAcousticResults()
    {
        var project = CompleteFacadeProject();
        var instant = new DateTimeOffset(2026, 7, 14, 8, 0, 0, TimeSpan.Zero);
        var ifc = Ifc4AkustikPropertyExportServisi.Olustur(project, zamanUtc: instant);

        Assert.Contains("FILE_SCHEMA(('IFC4'))", ifc, StringComparison.Ordinal);
        Assert.Contains("Pset_EYE4PRO_Acoustic", ifc, StringComparison.Ordinal);
        Assert.Contains("EYE4PRO_ResultId", ifc, StringComparison.Ordinal);
        Assert.Equal(22, Ifc4AkustikPropertyExportServisi.GlobalId(project.Id, "project").Length);

        var map = CepheGurultuHaritasiServisi.Olustur(project, zamanUtc: instant);
        Assert.NotEmpty(map.Hucreler);
        Assert.All(map.Hucreler, x => Assert.Equal(RaporSonucIdleri.DisCephe(x.MekanId), x.SonucId));
        var svg = CepheGurultuHaritasiServisi.SvgOlustur(map);
        var csv = CepheGurultuHaritasiServisi.CsvOlustur(map);
        Assert.Contains("Cephe Gürültü Sonuç Haritası", svg, StringComparison.Ordinal);
        Assert.Contains("gürültü yayılım simülasyonu değildir", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DnT,A,tr", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void PortfolioScanIsFaultIsolatedAndFieldReturnMergesWithoutOverwritingCalculations()
    {
        var project = CompleteInternalProject();
        var folder = Path.Combine(Path.GetTempPath(), "m08-portfolio-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var options = new JsonSerializerOptions
            {
                ReferenceHandler = ReferenceHandler.Preserve,
                WriteIndented = true,
            };
            File.WriteAllText(Path.Combine(folder, "valid.e4p08"), JsonSerializer.Serialize(project, options));
            File.WriteAllText(Path.Combine(folder, "broken.e4p08"), "{not-json");
            var portfolio = M08PortfoyServisi.Tara(folder);
            Assert.Equal(2, portfolio.Projeler.Count);
            Assert.Equal(1, portfolio.GecerliDosyaSayisi);
            Assert.Equal(1, portfolio.SorunluDosyaSayisi);

            var package = M08SahaPaketServisi.PaketVerisiOlustur(project);
            var task = Assert.Single(package.Gorevler.Where(x => x.Tur == SahaOlcumTuru.BolmeHavaSesi));
            var returned = new M08SahaDonusBelgesi
            {
                ProjeId = project.Id,
                KaynakRaporParmakIzi = package.KaynakRaporParmakIzi,
                DisaAktarilmaUtc = DateTimeOffset.UtcNow,
                Kayitlar =
                [
                    new M08SahaDonusKaydi
                    {
                        Id = Guid.NewGuid(), SonucId = task.SonucId, Ad = "Saha doğrulaması", Tur = task.Tur,
                        KatKimligi = task.KatKimligi, KaynakMekanId = task.KaynakMekanId,
                        AliciMekanId = task.AliciMekanId, BolmeId = task.BolmeId,
                        Deger = 57.2, Birim = task.Birim, Belirsizlik = 1.1,
                        OlcumTarihi = DateTime.UtcNow, Standart = "ISO 16283", RaporlamaIcinOnayli = true,
                    }
                ],
            };
            var json = JsonSerializer.Serialize(returned, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } });
            var merged = M08SahaPaketServisi.GeriDonusuBirlesitir(project, json, kullanici: "Test");
            Assert.True(merged.Basarili, string.Join(" | ", merged.Hatalar));
            Assert.Equal(1, merged.Eklenen);
            Assert.Single(project.SahaOlcumleri);
            Assert.Equal(57.2, project.SahaOlcumleri[0].Deger, 3);
            Assert.True(project.SahaOlcumleri[0].RaporlamaIcinOnayli);

            var html = M08SahaPaketServisi.HtmlOlustur(project);
            Assert.Contains("Çevrimdışı Saha Formu", html, StringComparison.Ordinal);
            Assert.Contains("EYE4PRO-M08-OFFLINE-FIELD", html, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static BinaProje CompleteInternalProject()
    {
        var source = Room("Kaynak Oda", 0, 0, 4, 4);
        source.KaynakDerecesi = "OG";
        var receiver = Room("Yatak Odası", 4, 0, 8, 4);
        receiver.KullanimTipi = "Yatak Odaları";
        receiver.AliciSinifi = "I";
        receiver.EmilimAlani = 20;
        var material = new YapiElemani
        {
            Id = Guid.NewGuid(), Ad = "Laboratuvar duvarı", Tip = YapiElemaniTipi.Duvar, Rw = 58,
            SpektrumKaynagi = SpektrumVeriKaynagi.Uretici,
            RThirdOctave = Enumerable.Repeat(58.0, 16).ToList(),
        };
        source.Bolmeler.Add(new Bolme
        {
            Id = Guid.NewGuid(), Ad = "Ayırıcı duvar", Alan = 12, HasGeometry = true,
            Baslangic = new Point2D(4, 0), Bitis = new Point2D(4, 4), KaynakMekan = source,
            AliciMekan = receiver, YapiElemani = material, Dntw = 57, ApparentRw = 57,
            OktavHavaSesiSonucu = new Kst.Acoustics.Core.BuildingAcoustics.Models.OktavBantSonucu(
                Enumerable.Repeat(57.0, 7).ToArray(), 57, 0, 0, "Uretici"),
        });
        return Project(source, receiver);
    }

    private static BinaProje CompleteFacadeProject()
    {
        var room = Room("Yatak Odası & Çalışma", 0, 0, 4, 4);
        room.KullanimTipi = "Yatak Odaları";
        room.AliciSinifi = "I";
        var material = new YapiElemani
        {
            Id = Guid.NewGuid(), Ad = "Cephe", Tip = YapiElemaniTipi.Duvar, Rw = 58,
            SpektrumKaynagi = SpektrumVeriKaynagi.Uretici,
            RThirdOctave = Enumerable.Repeat(58.0, 16).ToList(),
        };
        room.Bolmeler.Add(new Bolme
        {
            Id = Guid.NewGuid(), Ad = "Kuzey cephesi", Alan = 12, HasGeometry = true,
            Baslangic = new Point2D(0, 0), Bitis = new Point2D(4, 0), KaynakMekan = room, YapiElemani = material,
        });
        var project = Project(room);
        project.DisGurultuKullaniciTarafindanDogrulandi = true;
        project.DisGurultuSpektrumKaynagi = SpektrumVeriKaynagi.Olcum;
        return project;
    }

    private static BinaProje Project(params Mekan[] rooms)
    {
        var floor = new Kat { KatNo = 1, Ad = "Zemin Kat", KatYuksekligi = 3 };
        foreach (var room in rooms) floor.Mekanlar.Add(room);
        var project = new BinaProje
        {
            Id = Guid.NewGuid(), ProjeAdi = "M08 Entegrasyon Projesi", Adres = "Ankara",
            MusteriAdi = "Test Müşteri", SorumluKisiKurulus = "Akustik Uzmanı / Test",
            BinaSinifi = BinaSinifi.S1, BinaIslevi = BinaIslevi.Konut,
            AkustikPerformansSinifi = AkustikPerformansSinifi.C, DisGurultuDb = 65,
        };
        project.Katlar.Add(floor);
        return project;
    }

    private static Mekan Room(string name, double x1, double y1, double x2, double y2) => new()
    {
        Id = Guid.NewGuid(), Ad = name, KullanimTipi = name, Alan = Math.Abs((x2 - x1) * (y2 - y1)), Yukseklik = 3,
        Poligon = [new(x1, y1), new(x2, y1), new(x2, y2), new(x1, y2)],
    };

    private static MemoryStream TamperReport(Stream source)
    {
        var destination = new MemoryStream();
        source.Position = 0;
        using (var input = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true))
        using (var output = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in input.Entries)
            {
                var copy = output.CreateEntry(entry.FullName);
                using var target = copy.Open();
                if (entry.FullName == "report.html")
                {
                    var bytes = Encoding.UTF8.GetBytes("tampered");
                    target.Write(bytes);
                }
                else
                {
                    using var original = entry.Open();
                    original.CopyTo(target);
                }
            }
        }
        destination.Position = 0;
        return destination;
    }
}
