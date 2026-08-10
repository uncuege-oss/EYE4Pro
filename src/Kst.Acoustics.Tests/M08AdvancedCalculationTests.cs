using Kst.Acoustics.Core.BuildingAcoustics.Calculation;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.BuildingAcoustics.Reports;
using Kst.Acoustics.Core.Common.Geometry;
using Xunit;

namespace Kst.Acoustics.Tests;

public sealed class M08AdvancedCalculationTests
{
    [Fact]
    public void CanonicalEvaluationAndFrequencyAnalysisDoNotMutateLiveProject()
    {
        var (project, wall, _) = Project(rw: 34);
        var originalMaterial = wall.YapiElemani;

        var evaluation = new BinaAkustikDegerlendirmeServisi().Hesapla(project);
        var analysis = new FrekansYolAnaliziServisi().AnalizEt(project, wall.Id);

        Assert.Equal(0, wall.ApparentRw);
        Assert.Null(wall.UcuncuOktavHavaSesiSonucu);
        Assert.Same(originalMaterial, wall.YapiElemani);
        Assert.NotEmpty(evaluation.Satirlar);
        Assert.Equal(16, analysis.Bantlar.Count);
        Assert.All(analysis.Bantlar, band =>
        {
            var total = analysis.Yollar.Sum(path =>
                path.Bantlar.Single(x => x.FrekansHz == band.FrekansHz).KatkiYuzde);
            Assert.InRange(total, 99.999999, 100.000001);
        });
    }

    [Fact]
    public void FailureExplanationAndOptimizationUseCanonicalClone()
    {
        var (project, wall, baseMaterial) = Project(rw: 24);
        var result = new BinaAkustikDegerlendirmeServisi().Hesapla(project)
            .SonucBul(wall.Id, AkustikRaporSonucTuru.HavaSesi);
        Assert.NotNull(result);

        var explanation = new BasarisizlikAciklamaServisi().Acikla(
            project, wall.Id, AkustikRaporSonucTuru.HavaSesi);
        Assert.NotEmpty(explanation.Nedenler);
        Assert.Contains(explanation.Nedenler, x => x.Kod == BasarisizlikNedeniKodu.LimitAcigi);

        var stronger = new YapiElemani
        {
            Id = Guid.NewGuid(), Ad = "Yüksek performanslı duvar", Tip = YapiElemaniTipi.Duvar,
            Rw = 68, BirimFiyat = 1250, ParaBirimi = "TRY",
            RThirdOctave = Enumerable.Repeat(68d, 16).ToList(),
            SpektrumKaynagi = SpektrumVeriKaynagi.Uretici,
        };
        var optimized = new CozumOptimizasyonServisi().OptimizeEt(
            project, [baseMaterial, stronger],
            new CozumOptimizasyonIstegi(wall.Id, AkustikRaporSonucTuru.HavaSesi));

        var candidate = Assert.Single(optimized.Cozumler);
        Assert.Contains(candidate.Degisiklikler, x => x.YeniYapiElemaniId == stronger.Id);
        Assert.Same(baseMaterial, wall.YapiElemani);
        Assert.Equal(24, wall.YapiElemani!.Rw);

        var appliedClone = new CozumOptimizasyonServisi().KlonUzerindeUygula(project, candidate);
        var cloneWall = appliedClone.Katlar.SelectMany(x => x.Mekanlar)
            .SelectMany(x => x.Bolmeler).Single(x => x.Id == wall.Id);
        Assert.Equal(68, cloneWall.YapiElemani!.Rw);
        Assert.Equal(24, wall.YapiElemani.Rw);
    }

    [Fact]
    public void SensitivityAndMonteCarloAreRepeatableAndLeaveInputsUntouched()
    {
        var (project, wall, _) = Project(rw: 45);
        var parameter = new BelirsizlikParametresi(
            "Ayırıcı Rw", BelirsizlikParametreTuru.BolmeRwOfsetiDb, 1.5, wall.Id,
            MinimumOfset: -4, MaksimumOfset: 4);
        var service = new BelirsizlikAnalizServisi();

        var sensitivity = service.DuyarlilikAnalizi(
            project, wall.Id, [parameter], AkustikRaporSonucTuru.HavaSesi);
        var first = service.MonteCarlo(
            project, wall.Id, [parameter], tekrar: 20, seed: 31415,
            sonucTuru: AkustikRaporSonucTuru.HavaSesi);
        var second = service.MonteCarlo(
            project, wall.Id, [parameter], tekrar: 20, seed: 31415,
            sonucTuru: AkustikRaporSonucTuru.HavaSesi);

        Assert.Single(sensitivity.Parametreler);
        Assert.True(sensitivity.Parametreler[0].EtkiBuyuklugu > 0);
        Assert.Equal(20, first.GecerliTekrar);
        Assert.Equal(first.OrnekSonuclar, second.OrnekSonuclar);
        Assert.Equal(first.P05, second.P05, 10);
        Assert.Equal(45, wall.YapiElemani!.Rw);
        Assert.Equal(0, wall.YapiElemani.DeltaRDb);
    }

    [Fact]
    public void GeneratedTaskStatePersistsByStableKey()
    {
        var (project, _, _) = Project(rw: 50);
        var service = new AkustikIsListesiServisi();
        var first = service.Uret(project);
        var task = first.First(x => x.Otomatik);

        service.DurumKaydet(project, task.Anahtar, GorevDurumu.DevamEdiyor,
            sorumlu: "Akustik ekip", not: "Saha doğrulaması bekleniyor");
        var refreshed = service.Uret(project).Single(x => x.Anahtar == task.Anahtar);

        Assert.Equal(GorevDurumu.DevamEdiyor, refreshed.Durum);
        Assert.Equal("Akustik ekip", refreshed.Sorumlu);
        Assert.Equal("Saha doğrulaması bekleniyor", refreshed.Not);
    }

    private static (BinaProje Project, Bolme Wall, YapiElemani Material) Project(double rw)
    {
        var source = Room("Oturma Odası", 0, 4);
        source.KullanimTipi = "Oturma Odaları";
        source.KaynakDerecesi = "OG";
        source.EmilimAlani = 18;
        var receiver = Room("Yatak Odası", 4, 8);
        receiver.KullanimTipi = "Yatak Odaları";
        receiver.AliciSinifi = "I";
        receiver.EmilimAlani = 18;
        var material = new YapiElemani
        {
            Id = Guid.NewGuid(), Ad = $"Duvar Rw {rw:F0}", Tip = YapiElemaniTipi.Duvar,
            Rw = rw, BirimFiyat = 700, ParaBirimi = "TRY",
            RThirdOctave = Enumerable.Repeat(rw, 16).ToList(),
            SpektrumKaynagi = SpektrumVeriKaynagi.Uretici,
        };
        var wall = new Bolme
        {
            Id = Guid.NewGuid(), Ad = "Ayırıcı duvar", Alan = 12, HasGeometry = true,
            Baslangic = new Point2D(4, 0), Bitis = new Point2D(4, 4),
            KaynakMekan = source, AliciMekan = receiver, YapiElemani = material,
        };
        source.Bolmeler.Add(wall);
        var floor = new Kat { KatNo = 0, Ad = "Zemin Kat", KatYuksekligi = 3 };
        floor.Mekanlar.Add(source);
        floor.Mekanlar.Add(receiver);
        var project = new BinaProje
        {
            Id = Guid.NewGuid(), ProjeAdi = "M08 ileri analiz testi", Adres = "Ankara",
            MusteriAdi = "Test", SorumluKisiKurulus = "Akustik Uzmanı",
            BinaSinifi = BinaSinifi.S1, BinaIslevi = BinaIslevi.Konut,
            AkustikPerformansSinifi = AkustikPerformansSinifi.C,
            DisGurultuDb = 65, DisGurultuKullaniciTarafindanDogrulandi = true,
        };
        project.Katlar.Add(floor);
        project.YapiElemaniKutuphanesi.Add(material);
        return (project, wall, material);
    }

    private static Mekan Room(string name, double x1, double x2) => new()
    {
        Id = Guid.NewGuid(), Ad = name, Alan = 16, Yukseklik = 3,
        Poligon = [new(x1, 0), new(x2, 0), new(x2, 4), new(x1, 4)],
    };
}
