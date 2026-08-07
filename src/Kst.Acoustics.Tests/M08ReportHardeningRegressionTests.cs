using ClosedXML.Excel;
using Kst.Acoustics.Core.BuildingAcoustics.Calculation;
using Kst.Acoustics.Core.BuildingAcoustics.Geometry;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.BuildingAcoustics.Reports;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Tests;

public static class M08ReportHardeningRegressionTests
{
    [Xunit.Fact]
    public static void CombinedFacadeIsReportedOnceWithStableRoomId()
    {
        var room = Rect("Yatak Odası", 0, 0, 4, 4);
        room.KullanimTipi = "Yatak Odaları";
        room.AliciSinifi = "I";
        var material = new YapiElemani
        {
            Ad = "Cephe duvarı", Tip = YapiElemaniTipi.Duvar, Rw = 55,
            RThirdOctave = Enumerable.Repeat(55.0, 16).ToList()
        };
        room.Bolmeler.Add(Facade(room, material, "Kuzey cephe", 0));
        room.Bolmeler.Add(Facade(room, material, "Doğu cephe", 4));

        var project = ProjectWith(room);
        var service = new BinaAkustikRaporServisi();
        var facades = service.TabloVerisiOlustur(project)
            .Where(x => x.SonucTuru == AkustikRaporSonucTuru.DisCephe)
            .ToList();

        if (facades.Count != 1)
            throw new InvalidOperationException($"İki cephe parçası oda başına tek satır üretmeli; alınan {facades.Count}.");
        if (facades[0].SonucId != RaporSonucIdleri.DisCephe(room.Id))
            throw new InvalidOperationException("Birleşik cephe sonucu kararlı mekan-türü kimliği kullanmalı.");
        var ek3 = YonetmelikLimitleri.GetTablo31Limit(
            AkustikPerformansSinifi.C, room.AliciSinifi, project.DisGurultuDb).HedefDeger;
        var ek4 = EK4IcGurultuServisi.IcGurultuHedefiBul(project, room)
                  ?? throw new InvalidOperationException("Test odası EK-4 hedefiyle eşleşmeli.");
        var expectedTarget = Math.Max(ek3, project.DisGurultuDb - ek4);
        if (Math.Abs(facades[0].LimitDeger - expectedTarget) > 0.01)
            throw new InvalidOperationException(
                $"Cephe hedefi max(EK-3, Lgag−EK-4) olmalı; beklenen {expectedTarget}, alınan {facades[0].LimitDeger}.");

        var excluded = service.TabloVerisiOlustur(project,
            new HashSet<Guid> { RaporSonucIdleri.DisCephe(room.Id) });
        if (excluded.Any(x => x.SonucTuru == AkustikRaporSonucTuru.DisCephe))
            throw new InvalidOperationException("Kanonik cephe kimliği rapor filtresinden gerçekten çıkarılmalı.");
    }

    [Xunit.Fact]
    public static void IncompleteFacadeIsDataMissingWithoutNumericMargin()
    {
        var room = Rect("Yatak Odası", 0, 0, 4, 4);
        room.KullanimTipi = "Yatak Odaları";
        room.AliciSinifi = "I";
        var material = new YapiElemani
        {
            Ad = "Cephe duvarı", Tip = YapiElemaniTipi.Duvar, Rw = 55,
            RThirdOctave = Enumerable.Repeat(55.0, 16).ToList()
        };
        room.Bolmeler.Add(Facade(room, material, "Atanmış cephe", 0));
        room.Bolmeler.Add(Facade(room, null, "Atanmamış cephe", 4));

        var row = new BinaAkustikRaporServisi().TabloVerisiOlustur(ProjectWith(room))
            .Single(x => x.SonucTuru == AkustikRaporSonucTuru.DisCephe);

        if (row.Durum != nameof(UyumDurumu.VeriEksik))
            throw new InvalidOperationException($"Eksik birleşik cephe VeriEksik olmalı; alınan {row.Durum}.");
        if (row.Deger != 0 || !double.IsNaN(row.MarjDb))
            throw new InvalidOperationException("Eksik cephe eski tek-sayı değerine düşmemeli ve sayısal marj göstermemeli.");
        if (row.LimitDeger <= 0 || !row.YonetmelikEk.Contains("EK-4", StringComparison.Ordinal))
            throw new InvalidOperationException("Eksik sonuçta bile uygulanabilir ortak EK-3/EK-4 hedefi açıklanmalı.");
    }

    [Xunit.Fact]
    public static void ExcelOctaveSheetUsesTheSameResultSetAsSummary()
    {
        var source = Rect("Kaynak", 0, 0, 4, 4);
        source.KaynakDerecesi = "OG";
        var receiver = Rect("Alıcı", 4, 0, 8, 4);
        receiver.AliciSinifi = "II";
        var material = new YapiElemani { Ad = "Duvar", Tip = YapiElemaniTipi.Duvar, Rw = 55 };
        var passing = InternalWall(source, receiver, material, "Geçen duvar", 60);
        var failing = InternalWall(source, receiver, material, "Kalan duvar", 40);
        source.Bolmeler.Add(passing);
        source.Bolmeler.Add(failing);

        var project = ProjectWith(source, receiver);
        var path = Path.Combine(Path.GetTempPath(), $"m08-report-filter-{Guid.NewGuid():N}.xlsx");
        try
        {
            new BinaAkustikRaporServisi().ExcelRaporOlustur(
                project, path, BinaAkustikRaporServisi.RaporFiltresi.SadeceLimitAsan);

            using var workbook = new XLWorkbook(path);
            var summaryNames = UsedValues(workbook.Worksheet("Özet"), 4, 8);
            var octaveNames = UsedValues(workbook.Worksheet("Oktav Bantlar"), 3, 2);
            if (!summaryNames.Contains("Kalan duvar") || summaryNames.Contains("Geçen duvar"))
                throw new InvalidOperationException("Özet sayfası yalnız limit aşan sonucu içermeli.");
            if (!octaveNames.Contains("Kalan duvar") || octaveNames.Contains("Geçen duvar"))
                throw new InvalidOperationException("Oktav sayfası özetle aynı filtrelenmiş SonucId kümesini kullanmalı.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Xunit.Fact]
    public static void ThreeDimensionalResultUsesCanonicalMissingStatus()
    {
        var room = Rect("Yatak Odası", 0, 0, 4, 4);
        room.KullanimTipi = "Yatak Odaları";
        room.AliciSinifi = "I";
        room.Bolmeler.Add(Facade(room, null, "Eksik cephe", 0));
        var project = ProjectWith(room);

        var surfaces = new BinaGeometriServisi().BinaModelineCevir(project, sonuclariGoster: true);
        if (!surfaces.Any(s => s.Name == "Eksik cephe" && s.Category == "VERI_EKSIK"))
            throw new InvalidOperationException("3D sonuç görünümü eksik cepheyi kanonik VeriEksik durumuyla göstermeli.");
    }

    [Xunit.Fact]
    public static void DxfDoesNotRenderCalculationExcludedRooms()
    {
        var included = Rect("Hesaba Dahil Oda", 0, 0, 4, 4);
        var excluded = Rect("Hesap Dışı Balkon", 5, 0, 9, 4);
        excluded.Sinif = MekanSinifi.HesapDisi;
        excluded.Bolmeler.Add(Facade(excluded, null, "Balkon cephesi", 5));
        var project = ProjectWith(included, excluded);
        var path = Path.Combine(Path.GetTempPath(), $"m08-report-dxf-{Guid.NewGuid():N}.dxf");
        try
        {
            new DxfRenkliCiktiServisi().DxfRenkliRaporOlustur(project, path);
            var dxf = netDxf.DxfDocument.Load(path)
                      ?? throw new InvalidOperationException("Üretilen DXF geri okunamadı.");
            if (dxf.Entities.Texts.Any(t => t.Value.Contains("Hesap Dışı Balkon", StringComparison.Ordinal)) ||
                dxf.Entities.Lines.Any(l => l.Layer.Name == "VERI_EKSIK"))
                throw new InvalidOperationException("Hesap dışı mekan DXF uygunluk paftasına çizilmemeli.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static BinaProje ProjectWith(params Mekan[] rooms)
    {
        var floor = new Kat { KatNo = 1 };
        foreach (var room in rooms) floor.Mekanlar.Add(room);
        var project = new BinaProje
        {
            BinaSinifi = BinaSinifi.S1,
            BinaIslevi = BinaIslevi.Konut,
            AkustikPerformansSinifi = AkustikPerformansSinifi.C,
            DisGurultuDb = 65
        };
        project.Katlar.Add(floor);
        return project;
    }

    private static Bolme Facade(Mekan room, YapiElemani? material, string name, double x) => new()
    {
        Ad = name, Alan = 12, HasGeometry = true,
        Baslangic = new Point2D(x, 0), Bitis = new Point2D(x + 4, 0),
        KaynakMekan = room, YapiElemani = material
    };

    private static Bolme InternalWall(Mekan source, Mekan receiver, YapiElemani material,
        string name, double dntw) => new()
    {
        Ad = name, Alan = 12, KaynakMekan = source, AliciMekan = receiver,
        YapiElemani = material, Dntw = dntw, ApparentRw = dntw,
        OktavHavaSesiSonucu = new OktavBantSonucu(
            Enumerable.Repeat(dntw, 7).ToArray(), dntw, 0, 0, "ModelTahmin")
    };

    private static HashSet<string> UsedValues(IXLWorksheet sheet, int column, int firstRow)
    {
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? firstRow - 1;
        return Enumerable.Range(firstRow, Math.Max(0, lastRow - firstRow + 1))
            .Select(row => sheet.Cell(row, column).GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static Mekan Rect(string name, double x1, double y1, double x2, double y2) => new()
    {
        Ad = name,
        KullanimTipi = name,
        Alan = Math.Abs((x2 - x1) * (y2 - y1)),
        Yukseklik = 3,
        Poligon =
        [
            new Point2D(x1, y1),
            new Point2D(x2, y1),
            new Point2D(x2, y2),
            new Point2D(x1, y2)
        ]
    };
}
