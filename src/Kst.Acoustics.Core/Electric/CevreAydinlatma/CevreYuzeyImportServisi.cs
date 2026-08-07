using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Electric.Ortak;
using netDxf;
using netDxf.Units;

namespace Kst.Acoustics.Core.Electric.CevreAydinlatma;

/// <summary>
/// Park/çevre DXF'indeki kapalı polyline'ları yüzey (yeşil alan/yol/spor/bina) olarak çıkarır.
/// Kategori, polyline'ın DXF katman adından otomatik atanır (<see cref="YuzeyKategoriEsleyici"/>);
/// kullanıcı yüzey ekranında elle düzeltebilir. M15 Faz 2.
/// </summary>
public static class CevreYuzeyImportServisi
{
    /// <summary>Yüklü bir DXF dokümanındaki kapalı polyline'ları kategorili yüzeylere çevirir.</summary>
    public static List<CevreYuzey> KapaliPoligonlardanYuzeyler(DxfDocument dxf, double birimOlcek = 1.0)
    {
        var yuzeyler = new List<CevreYuzey>();
        int id = 1;

        foreach (var poly in dxf.Entities.Polylines2D.Where(p => p.IsClosed))
        {
            var poligon = poly.Vertexes
                .Select(v => new Point2D(v.Position.X * birimOlcek, v.Position.Y * birimOlcek))
                .ToList();
            if (poligon.Count < 3)
                continue;

            string katman = poly.Layer?.Name ?? "";
            yuzeyler.Add(new CevreYuzey
            {
                Id = id,
                Ad = string.IsNullOrWhiteSpace(katman) ? $"Yüzey {id}" : katman,
                Kategori = YuzeyKategoriEsleyici.Esle(katman),
                KaynakKatman = katman,
                Poligon = poligon,
                AlanM2 = PoligonAlani(poligon)
            });
            id++;
        }

        return yuzeyler;
    }

    /// <summary>DXF dosyasını yükler, birim ölçeğini belirler ve kapalı polyline'ları yüzeylere çevirir.</summary>
    public static List<CevreYuzey> DosyadanYukle(string dxfYolu, out string? birimUyarisi)
    {
        var dxf = DxfDocument.Load(dxfYolu)
            ?? throw new InvalidOperationException("DXF dosyası yüklenemedi.");
        double olcek = BirimOlcek(dxf.DrawingVariables.InsUnits, out birimUyarisi);
        return KapaliPoligonlardanYuzeyler(dxf, olcek);
    }

    /// <summary>
    /// DXF arka plan çizgileri: LINE entity'leri ve açık polyline kenarları, sadeleştirilmiş
    /// (büyük park DXF'i için kritik — bkz. <see cref="Kst.Acoustics.Core.Bina.Services.DxfSegmentYardimci.Sadelestir"/>).
    /// </summary>
    public static List<CizgiSegment> ArkaPlanCizgileri(DxfDocument dxf, double birimOlcek = 1.0)
    {
        var ham = new List<(Point2D A, Point2D B)>();

        foreach (var l in dxf.Entities.Lines)
            ham.Add((
                new Point2D(l.StartPoint.X * birimOlcek, l.StartPoint.Y * birimOlcek),
                new Point2D(l.EndPoint.X * birimOlcek, l.EndPoint.Y * birimOlcek)));

        foreach (var p in dxf.Entities.Polylines2D.Where(p => !p.IsClosed))
        {
            var v = p.Vertexes;
            for (int i = 0; i + 1 < v.Count; i++)
                ham.Add((
                    new Point2D(v[i].Position.X * birimOlcek, v[i].Position.Y * birimOlcek),
                    new Point2D(v[i + 1].Position.X * birimOlcek, v[i + 1].Position.Y * birimOlcek)));
        }

        return Kst.Acoustics.Core.Bina.Services.DxfSegmentYardimci.Sadelestir(ham)
            .Select(s => new CizgiSegment { A = s.A, B = s.B })
            .ToList();
    }

    /// <summary>DXF'i tek seferde yükler: kapalı poligon yüzeyleri + arka plan çizgileri.</summary>
    public static (List<CevreYuzey> Yuzeyler, List<CizgiSegment> ArkaPlan) DosyadanYukleTam(
        string dxfYolu, out string? birimUyarisi)
    {
        var dxf = DxfDocument.Load(dxfYolu)
            ?? throw new InvalidOperationException("DXF dosyası yüklenemedi.");
        double olcek = BirimOlcek(dxf.DrawingVariables.InsUnits, out birimUyarisi);
        return (KapaliPoligonlardanYuzeyler(dxf, olcek), ArkaPlanCizgileri(dxf, olcek));
    }

    private static double PoligonAlani(IReadOnlyList<Point2D> p)
    {
        double alan = 0;
        for (int i = 0; i < p.Count; i++)
        {
            var c = p[i];
            var n = p[(i + 1) % p.Count];
            alan += c.X * n.Y - n.X * c.Y;
        }
        return Math.Abs(alan) / 2.0;
    }

    /// <summary>
    /// Metre/çizim-birimi ölçeği. Park altlıkları genelde metre olduğundan, birim belirsizse
    /// (Unitless) 1 birim = 1 m varsayılır (mimari DXF'lerin mm varsayımının aksine).
    /// </summary>
    private static double BirimOlcek(DrawingUnits birim, out string? uyari)
    {
        uyari = null;
        return birim switch
        {
            DrawingUnits.Millimeters => 0.001,
            DrawingUnits.Centimeters => 0.01,
            DrawingUnits.Decimeters => 0.1,
            DrawingUnits.Meters => 1.0,
            DrawingUnits.Kilometers => 1000.0,
            DrawingUnits.Inches => 0.0254,
            DrawingUnits.Feet => 0.3048,
            DrawingUnits.Yards => 0.9144,
            _ => BelirsizVarsayim(out uyari)
        };
    }

    private static double BelirsizVarsayim(out string? uyari)
    {
        uyari = "DXF birimi belirsiz; 1 birim = 1 m varsayıldı. Gerekirse ölçeği düzeltin.";
        return 1.0;
    }
}
