using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Architecture.Model3D;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Terrain.Models;

namespace Kst.Acoustics.Core.Terrain.Services;

/// M61 F1 — M50 `BinaGeometri` (`.e4bina`) → M61 `BinaAyakIzi` adaptörü.
/// Temel oturtma için bina geometrisinden ayak izi + kat sayısı türetir.
/// Bina geometrisini DEĞİŞTİRMEZ (salt-okunur tüketim).
///
/// MVP: ayak izi = en alt katın oda köşelerinin sınır kutusu (tek oda varsa o odanın
/// poligonu). Karmaşık (L/T) planlarda poligon birleşimi ileri faza bırakılmıştır.
public static class BinaAyakIziAdaptoru
{
    public static BinaAyakIzi Donustur(BinaGeometri geo)
    {
        var ayak = new BinaAyakIzi
        {
            BlokId = string.IsNullOrWhiteSpace(geo.BinaAdi) ? "Bina" : geo.BinaAdi,
            ZeminUstuKatSayisi = Math.Max(0, geo.Katlar.Count(k => k.KatNo >= 0)),
            BodrumKatSayisi = geo.Katlar.Count(k => k.KatNo < 0)
        };

        if (geo.Katlar.Count > 0)
            ayak.KatYuksekligiM = geo.Katlar.Average(k => k.KatYuksekligiM);

        var altKat = geo.Katlar.OrderBy(k => k.KatNo).FirstOrDefault();
        if (altKat is not null)
            ayak.Poligon = AyakIzi(altKat);

        return ayak;
    }

    /// Mimari M35 `Bim3DModel` → `BinaAyakIzi`. Ayak izi = en alt katın mahal kutularının
    /// sınır kutusu (M35 mahalleri eksen-hizalı X/Y/Genişlik/Derinlik prizmalarıdır).
    /// Kat sayımı: KatNo≥1 zemin üstü, KatNo≤0 bodrum. Bina geometrisi DEĞİŞTİRİLMEZ.
    public static BinaAyakIzi Donustur(Bim3DModel model)
    {
        var ayak = new BinaAyakIzi
        {
            BlokId = string.IsNullOrWhiteSpace(model.Ad) ? "Bina" : model.Ad,
            ZeminUstuKatSayisi = Math.Max(0, model.Katlar.Count(k => k.KatNo >= 1)),
            BodrumKatSayisi = model.Katlar.Count(k => k.KatNo <= 0)
        };

        if (model.Katlar.Count > 0)
            ayak.KatYuksekligiM = model.Katlar.Average(k => k.KatYuksekligiM);

        var altKat = model.Katlar.OrderBy(k => k.KatNo).FirstOrDefault();
        if (altKat is not null)
            ayak.Poligon = MahalKutulariSinir(altKat);

        return ayak;
    }

    // M35 katındaki mahal kutularının (X..X+Genişlik, Y..Y+Derinlik) sınır kutusu.
    private static List<Point2D> MahalKutulariSinir(Kat3DModel kat)
    {
        var mahaller = kat.Mahaller.Where(m => m.GenislikM > 0 && m.DerinlikM > 0).ToList();
        if (mahaller.Count == 0) return new List<Point2D>();

        double minX = mahaller.Min(m => m.X);
        double minY = mahaller.Min(m => m.Y);
        double maxX = mahaller.Max(m => m.X + m.GenislikM);
        double maxY = mahaller.Max(m => m.Y + m.DerinlikM);

        return new List<Point2D>
        {
            new(minX, minY), new(maxX, minY), new(maxX, maxY), new(minX, maxY)
        };
    }

    private static List<Point2D> AyakIzi(BinaKat kat)
    {
        double ox = kat.OriginOffset.X, oy = kat.OriginOffset.Y;

        var odalar = kat.Odalar.Where(o => o.Poligon.Count >= 3).ToList();

        // Tek oda → o odanın poligonu (ofsetli).
        if (odalar.Count == 1)
            return odalar[0].Poligon.Select(p => new Point2D(p.X + ox, p.Y + oy)).ToList();

        // Çok oda veya hiç → tüm köşelerin sınır kutusu.
        var tumNoktalar = odalar.SelectMany(o => o.Poligon).ToList();
        if (tumNoktalar.Count == 0) return new List<Point2D>();

        double minX = tumNoktalar.Min(p => p.X) + ox, minY = tumNoktalar.Min(p => p.Y) + oy;
        double maxX = tumNoktalar.Max(p => p.X) + ox, maxY = tumNoktalar.Max(p => p.Y) + oy;

        return new List<Point2D>
        {
            new(minX, minY), new(maxX, minY), new(maxX, maxY), new(minX, maxY)
        };
    }
}
