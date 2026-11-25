using System;
using System.IO;
using System.Linq;
using Kst.Acoustics.Core.Architecture.Models;
using Kst.Acoustics.Core.Architecture.Rules;
using Kst.Acoustics.Core.Architecture.Services;
using Kst.Acoustics.Core.Bina.Services;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Landscape.Services;
using Kst.Acoustics.Core.Landscape.Models;
using Kst.Acoustics.Core.Terrain.Models;
using Kst.Acoustics.Core.Terrain.Services;
using Kst.Acoustics.Core.Salon.Services;
using Xunit;

namespace Kst.Acoustics.Tests;

internal static class ModuleAuditGeometryTests
{
    public static (string Name, Action Test)[] Cases =>
    [
        ("Module audit M30/M50/M60/M70: future schemas are rejected", FutureSchemasRejected),
        ("Module audit M30/M60/M70: failed saves preserve metadata", FailedSavePreservesMetadata),
        ("Module audit M30/M50/M51/M60/M70: saves keep last valid backup", SavesKeepBackup),
        ("Module audit M34: nonfinite dimensions are invalid", NonfiniteArchitectureRejected),
        ("Module audit M60: invalid excavation step is rejected", InvalidExcavationStep),
        ("Module audit M70: clockwise park boundary remains usable", ClockwisePark),
        ("Module audit M61: section matches blocks by identity", SectionUsesBlockIdentity),
        ("Module audit M70: full paving footprint respects excluded areas", PavingAvoidsExcludedAreas),
        ("Module audit M70: path width respects park boundary", PathWidthRespectsBoundary)
    ];

    private static string TempRoot()
    {
        string path = Path.Combine(Path.GetTempPath(), $"module-audit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void FutureSchemasRejected()
    {
        var root = TempRoot();
        try
        {
            var path = Path.Combine(root, "future.json");
            File.WriteAllText(path, "{\"SemaSurum\":9999}");
            Assert.Throws<InvalidDataException>(() => MimariProjeServisi.Ac(path));
            Assert.Throws<InvalidDataException>(() => BinaGeometriServisi.Ac(path));
            Assert.Throws<InvalidDataException>(() => AraziGeometriServisi.Ac(path));
            Assert.Throws<InvalidDataException>(() => ParkGeometriServisi.Ac(path));
        }
        finally { Directory.Delete(root, true); }
    }

    private static void FailedSavePreservesMetadata()
    {
        var root = TempRoot();
        try
        {
            var architecture = MimariProjeServisi.YeniOlustur();
            architecture.DosyaYolu = "old.json";
            var timestamp = architecture.GuncellemeZamani;
            Assert.NotNull(Record.Exception(() => MimariProjeServisi.Kaydet(architecture, root)));
            Assert.Equal("old.json", architecture.DosyaYolu);
            Assert.Equal(timestamp, architecture.GuncellemeZamani);
            var terrain = AraziGeometriServisi.YeniOlustur();
            Assert.NotNull(Record.Exception(() => AraziGeometriServisi.Kaydet(terrain, root)));
            Assert.Equal(0, terrain.GeometriRevizyon);
            var park = ParkGeometriServisi.YeniOlustur();
            Assert.NotNull(Record.Exception(() => ParkGeometriServisi.Kaydet(park, root)));
            Assert.Equal(0, park.GeometriRevizyon);
        }
        finally { Directory.Delete(root, true); }
    }

    private static void SavesKeepBackup()
    {
        var root = TempRoot();
        try
        {
            Action<string>[] savers = [
                path => MimariProjeServisi.Kaydet(MimariProjeServisi.YeniOlustur(), path),
                path => BinaGeometriServisi.Kaydet(BinaGeometriServisi.YeniOlustur(), path),
                path => SalonGeometriServisi.Kaydet(SalonGeometriServisi.YeniOlustur(), path),
                path => AraziGeometriServisi.Kaydet(AraziGeometriServisi.YeniOlustur(), path),
                path => ParkGeometriServisi.Kaydet(ParkGeometriServisi.YeniOlustur(), path)
            ];
            for (var i = 0; i < savers.Length; i++)
            {
                var path = Path.Combine(root, $"{i}.json");
                savers[i](path);
                string first = File.ReadAllText(path);
                File.WriteAllText(path + ".tmp", "unrelated temporary file");
                savers[i](path);
                Assert.Equal(first, File.ReadAllText(path + ".bak"));
                Assert.Equal("unrelated temporary file", File.ReadAllText(path + ".tmp"));
            }
        }
        finally { Directory.Delete(root, true); }
    }

    private static void NonfiniteArchitectureRejected()
    {
        var project = new MimariProje { IhtiyacProgrami = new() { BinaIslevi = BinaIslevi.Ofis } };
        Assert.True(MimariProjeDogrulamaServisi.GecerliMi(project));
        project.IhtiyacProgrami.VarsayilanKatYuksekligiM = double.NaN;
        Assert.False(MimariProjeDogrulamaServisi.GecerliMi(project));
    }

    private static void InvalidExcavationStep()
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.Epsilon })
        {
            var service = new HafriyatHesabiServisi { GridAraligiM = invalid };
            Assert.Throws<ArgumentException>(() => service.Hesapla(new AraziYuzeyi(), new TasarimYuzeyi()));
        }
    }

    private static void ClockwisePark()
    {
        var park = ParkGeometriServisi.YeniOlustur();
        park.Sinir.Poligon = [new(0, 0), new(0, 10), new(10, 10), new(10, 0)];
        var usable = ParkAlanServisi.KullanilabilirAlan(park);
        Assert.True(ParkAlanServisi.KullanilabilirIcinde(new Point2D(5, 5), usable.Halkalar));
        Assert.Equal(100, usable.KullanilabilirM2);
    }

    private static void SectionUsesBlockIdentity()
    {
        var terrain = new AraziTuketim { Arazi = new(), Yuzey = new() { Dugumler = [new(0, 0, 0), new(30, 0, 0), new(0, 30, 0)], Ucgenler = [new(0, 1, 2)] } };
        var a = new BinaAyakIzi { BlokId = "A", Poligon = [new(1, 1), new(2, 1), new(2, 2), new(1, 2)] };
        var b = new BinaAyakIzi { BlokId = "B", Poligon = [new(10, 1), new(12, 1), new(12, 2), new(10, 2)] };
        var model = new TemelOturtmaServisi().Oturt(terrain, [a, b]);
        var section = KesitServisi.Olustur(terrain, model, [b, a]);
        Assert.Equal(1, section.Bloklar.Single(x => x.BlokId == "A").XMin);
        Assert.Equal(10, section.Bloklar.Single(x => x.BlokId == "B").XMin);
    }

    private static void PavingAvoidsExcludedAreas()
    {
        // Outer CCW ring + inner CW hole. All paving corners are outside the hole,
        // but the paving covers it, so corner-only checks are insufficient.
        System.Collections.Generic.IReadOnlyList<Point2D>[] rings = [
            new Point2D[] { new(0, 0), new(10, 0), new(10, 10), new(0, 10) },
            new Point2D[] { new(4, 4), new(4, 6), new(6, 6), new(6, 4) }
        ];
        var paving = new DosemeAlani { Poligon = [new(2, 2), new(8, 2), new(8, 8), new(2, 8)] };
        Assert.False(ParkYerlesimServisi.Dogrula(paving, rings).Gecerli);
        paving.Poligon = [new(1, 1), new(3, 1), new(3, 3), new(1, 3)];
        Assert.True(ParkYerlesimServisi.Dogrula(paving, rings).Gecerli);
    }

    private static void PathWidthRespectsBoundary()
    {
        System.Collections.Generic.IReadOnlyList<Point2D>[] rings = [new Point2D[] { new(0, 0), new(10, 0), new(10, 10), new(0, 10) }];
        var path = new YuruyusYolu { Guzergah = [new(1, 0.25), new(9, 0.25)], GenislikM = 2 };
        Assert.False(ParkYerlesimServisi.Dogrula(path, rings).Gecerli);
        path.Guzergah = [new(1, 5), new(9, 5)];
        Assert.True(ParkYerlesimServisi.Dogrula(path, rings).Gecerli);
    }
}
