using Kst.Acoustics.App.BuildingAcoustics.ViewModels;
using Kst.Acoustics.Core.BuildingAcoustics.Calculation;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.BuildingAcoustics.Reports;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Tests;

public static class M08FinalRegressionTests
{
    public static void OriginNormalizationPreservesWorldCoordinates()
    {
        var kat = new Kat { KatNo = 1, OriginOffset = new Point2D(100, 200) };
        var mekan = new Mekan
        {
            Ad = "Oda",
            Poligon = [new(10, 20), new(14, 20), new(14, 23), new(10, 23)]
        };
        kat.Mekanlar.Add(mekan);
        var oncekiDunya = mekan.Poligon
            .Select(p => new Point2D(p.X + kat.OriginOffset.X, p.Y + kat.OriginOffset.Y))
            .ToArray();

        Kst.Acoustics.Core.BuildingAcoustics.Parsers.DxfKatPlaniParser.NormalizeOrigin(kat);

        if (Math.Abs(mekan.Poligon.Min(p => p.X)) > 1e-9 ||
            Math.Abs(mekan.Poligon.Min(p => p.Y)) > 1e-9)
            throw new InvalidOperationException("Yerel kat geometrisi normalize edilmedi.");
        for (var i = 0; i < oncekiDunya.Length; i++)
        {
            var simdiki = new Point2D(
                mekan.Poligon[i].X + kat.OriginOffset.X,
                mekan.Poligon[i].Y + kat.OriginOffset.Y);
            if (Math.Abs(simdiki.X - oncekiDunya[i].X) > 1e-9 ||
                Math.Abs(simdiki.Y - oncekiDunya[i].Y) > 1e-9)
                throw new InvalidOperationException("Origin normalizasyonu dünya koordinatını değiştirdi.");
        }
    }

    public static void IndependentUnitEditMarksProjectDirty()
    {
        using var vm = new BinaProjesiViewModel();
        var bolum = new BagimsizBolum { Ad = "Daire 1" };
        vm.BagimsizBolumler.Add(bolum);
        vm.TemizIsaretle();

        bolum.Ad = "Daire A";
        if (!vm.IsDirty)
            throw new InvalidOperationException("Bağımsız bölüm adı düzenlemesi projeyi kirli işaretlemeli.");
    }

    public static void NewEditAfterUndoClearsRedoBranch()
    {
        using var vm = new BinaProjesiViewModel();
        var kat = new Kat { KatNo = 1, Ad = "Zemin", KatYuksekligi = 3 };
        var mekan = Rect("Oda", 0, 0, 4, 4);
        kat.Mekanlar.Add(mekan);
        vm.Proje.Katlar.Add(kat);
        vm.Katlar.Add(kat);
        vm.TemizIsaretle();

        vm.BeginUndoSnapshot();
        mekan.Ad = "Düzenlenmiş Oda";
        vm.GeriAlCommand.Execute(null);
        if (!vm.YineleVarMi)
            throw new InvalidOperationException("Geri alma sonrasında yinele dalı oluşmalı.");

        vm.DisGurultuDb = 71;
        if (vm.YineleVarMi)
            throw new InvalidOperationException("Geri almadan sonraki yeni düzenleme eski yinele dalını temizlemeli.");
    }

    public static void IncompatibleWallMaterialBlocksPreflight()
    {
        using var vm = new BinaProjesiViewModel();
        var mekan = Rect("Oda", 0, 0, 4, 4);
        mekan.KullanimTipi = "Yatak Odası";
        mekan.KaynakDerecesi = "OG";
        mekan.AliciSinifi = "II";
        mekan.BagimsizBolumId = Guid.NewGuid();
        mekan.ZeminYapiElemani = new YapiElemani { Ad = "Döşeme", Tip = YapiElemaniTipi.Doseme };
        mekan.TavanYapiElemani = new YapiElemani { Ad = "Tavan", Tip = YapiElemaniTipi.Tavan };
        mekan.Bolmeler.Add(new Bolme
        {
            Ad = "Yanlış tipli duvar",
            Alan = 12,
            HasGeometry = true,
            KaynakMekan = mekan,
            YapiElemani = new YapiElemani { Ad = "Döşeme malzemesi", Tip = YapiElemaniTipi.Doseme }
        });
        var kat = new Kat { KatNo = 1, KatYuksekligi = 3 };
        kat.Mekanlar.Add(mekan);
        vm.Proje.Katlar.Add(kat);
        vm.Katlar.Add(kat);

        vm.GuncelleDogrulamaUyarisi();
        if (!vm.DogrulamaSatirlari.Any(s => s.KritikMi && s.Aciklama.Contains("uyumsuz", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Döşeme tipinin düşey duvara atanması kritik ön-kontrol hatası olmalı.");
    }

    public static void PastedSpectrumDoesNotInventMeasurementProvenance()
    {
        using var vm = new BinaProjesiViewModel();
        vm.Proje.DisGurultuSpektrumKaynagi = SpektrumVeriKaynagi.Varsayilan;
        vm.DisGurultuSpektrumMetni = string.Join("; ",
            Enumerable.Range(0, DisGurultuServisi.ExtendedThirdOctaveFreqler.Length).Select(i => 60 + i));

        if (vm.Proje.DisGurultuSpektrumKaynagi == SpektrumVeriKaynagi.Olcum)
            throw new InvalidOperationException("Yapıştırılan spektrum köken seçilmeden Ölçüm sayılamaz.");
        if (vm.Proje.DisGurultuSpektrumKaynagi != SpektrumVeriKaynagi.Belirsiz)
            throw new InvalidOperationException("Özel spektrum, açık köken seçilene kadar Belirsiz olmalı.");
    }

    public static void ReportSelectionMatchesCanonicalRowsIncludingMissingData()
    {
        using var vm = new BinaProjesiViewModel();
        var proje = new BinaProje
        {
            BinaIslevi = BinaIslevi.BuroIdari,
            AkustikPerformansSinifi = AkustikPerformansSinifi.C
        };
        var kat = new Kat { KatNo = 1, Ad = "Zemin", KatYuksekligi = 3 };
        var mekan = Rect("Toplantı Odası", 0, 0, 5, 4);
        mekan.KullanimTipi = "Toplantı Odası";
        mekan.EmilimAlani = 0; // kanonik RT satırı VeriEksik olmalı ama seçimden düşmemeli
        kat.Mekanlar.Add(mekan);
        proje.Katlar.Add(kat);
        vm.Proje = proje;

        var kanonik = new BinaAkustikRaporServisi().TabloVerisiOlustur(proje);
        vm.RefreshRaporDahilSatirlar();

        var beklenen = kanonik.Select(s => s.SonucId).ToHashSet();
        var gercek = vm.RaporDahilSatirlar.Select(s => s.SonucId).ToHashSet();
        if (!beklenen.SetEquals(gercek))
            throw new InvalidOperationException("Rapor seçim listesi kanonik dışa aktarım kimlikleriyle birebir eşleşmeli.");
        if (!vm.RaporDahilSatirlar.Any(s => s.Durum == "VERİ EKSİK"))
            throw new InvalidOperationException("VeriEksik kanonik satırlar rapor seçiminde görünür kalmalı.");
    }

    private static Mekan Rect(string ad, double x1, double y1, double x2, double y2) => new()
    {
        Ad = ad,
        Alan = Math.Abs((x2 - x1) * (y2 - y1)),
        Yukseklik = 3,
        Poligon = [new(x1, y1), new(x2, y1), new(x2, y2), new(x1, y2)]
    };
}
