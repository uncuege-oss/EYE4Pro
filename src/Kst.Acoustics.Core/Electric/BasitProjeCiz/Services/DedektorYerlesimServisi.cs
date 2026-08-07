using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;

namespace Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

/// Yangın dedektörlerini TS EN 54 kapsamına göre oda poligonuna grid olarak dağıtır.
public static class DedektorYerlesimServisi
{
    // TS EN 54: duman/kombine ≤ 60 m² → 6 m adım, ısı ≤ 25 m² → 4 m adım
    private static double GridAdimM(BpcElemanTipi tip) => tip == BpcElemanTipi.IsiDedektor ? 4.0 : 6.0;

    public static List<BasitProjeEleman> YerlesimOlustur(BasitProjeOda oda, BpcElemanTipi tip)
    {
        // Asma tavanlı mahalde tek blok: AT dedektörü + normal dedektör + ikaz lambası
        if (oda.AsmaTavan && tip is BpcElemanTipi.DumanDedektor
                                or BpcElemanTipi.IsiDedektor
                                or BpcElemanTipi.KombineDedektor)
            tip = BpcElemanTipi.AsmaTavanDedektorGrubu;

        var tanim  = BasitProjeCizKutuphaneServisi.ZayifAkimTanim(tip)!;
        var points = Grid(oda, GridAdimM(tip));

        return points.Select((p, i) => new BasitProjeEleman
        {
            Tip       = tip,
            Ad        = $"{tanim.SembolAdi}-{i + 1}",
            EkranAdi  = string.IsNullOrWhiteSpace(tanim.EkranAdi) ? tanim.SembolAdi : tanim.EkranAdi,
            OdaId     = oda.Id,
            BlokAdi   = tanim.BlokAdi,
            X         = p.X,
            Y         = p.Y,
            PaftaTipi = BpcPaftaTipi.Yangin
        }).ToList();
    }

    private static List<Point2D> Grid(BasitProjeOda oda, double adim)
    {
        var (minX, minY, maxX, maxY) = Bounds(oda);
        var result = new List<Point2D>();

        for (double x = minX + adim / 2; x < maxX; x += adim)
        for (double y = minY + adim / 2; y < maxY; y += adim)
        {
            var p = new Point2D(x, y);
            if (PoligonYardimci.IsPointInside(p, oda.Poligon))
                result.Add(p);
        }

        return result.Count > 0 ? result : [ArmaturYerlesimServisi.PoligonIcMerkezi(oda)];
    }

    private static (double MinX, double MinY, double MaxX, double MaxY) Bounds(BasitProjeOda oda)
    {
        if (oda.Poligon.Count == 0) return (0, 0, 0, 0);
        return (
            oda.Poligon.Min(p => p.X),
            oda.Poligon.Min(p => p.Y),
            oda.Poligon.Max(p => p.X),
            oda.Poligon.Max(p => p.Y));
    }
}
