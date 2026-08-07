using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.Core.Bina.Services;

/// <summary>
/// M50 — BinaGeometri'yi 3D önizleme yüzeylerine çevirir (GeometryPreviewControl girdisi).
/// Katlar M50 sol panelindeki görsel sıraya göre dizilir: listedeki son kat tabanda,
/// listedeki ilk kat üstte gösterilir. OriginOffset dünya koordinatına uygulanır.
/// Düşey birleşimde açık olan tavan/döşeme yüzeyleri ATLANIR — patlatma 3D'de görünür.
/// </summary>
public static class Bina3DGorunumServisi
{
    public static List<ProjectSurface> YuzeyleriOlustur(BinaGeometri geo)
    {
        var surfaces = new List<ProjectSurface>();
        int id = 1;
        double tabanZ = 0.0;

        foreach (var kat in KatlariZTabandanUste(geo))
        {
            if (!kat.BloklariGoster)
            {
                tabanZ += kat.KatYuksekligiM;
                continue;
            }

            double ox = kat.OriginOffset.X, oy = kat.OriginOffset.Y;

            foreach (var oda in kat.Odalar)
            {
                if (oda.Poligon.Count < 3) continue;
                double h = oda.YukseklikOverrideM ?? kat.KatYuksekligiM;

                bool dosemeAcik = BinaDuseyBirlesimServisi.DosemeAcikMi(geo, oda.Id);
                bool tavanAcik  = BinaDuseyBirlesimServisi.TavanAcikMi(geo, oda.Id);

                if (!dosemeAcik)
                {
                    var doseme = oda.Poligon
                        .Select(p => new Point3D(p.X + ox, p.Y + oy, tabanZ))
                        .ToList();
                    surfaces.Add(new ProjectSurface(id++, $"{oda.Ad} Döşeme", "Doseme", doseme, oda.Id));
                }

                if (!tavanAcik)
                {
                    var tavan = oda.Poligon
                        .Select(p => new Point3D(p.X + ox, p.Y + oy, tabanZ + h))
                        .ToList();
                    surfaces.Add(new ProjectSurface(id++, $"{oda.Ad} Tavan", "Tavan", tavan, oda.Id));
                }

                for (int i = 0; i < oda.Poligon.Count; i++)
                {
                    var a = oda.Poligon[i];
                    var b = oda.Poligon[(i + 1) % oda.Poligon.Count];
                    var duvar = new List<Point3D>
                    {
                        new(a.X + ox, a.Y + oy, tabanZ),
                        new(b.X + ox, b.Y + oy, tabanZ),
                        new(b.X + ox, b.Y + oy, tabanZ + h),
                        new(a.X + ox, a.Y + oy, tabanZ + h)
                    };
                    surfaces.Add(new ProjectSurface(id++, $"{oda.Ad} Duvar", "Duvar", duvar, oda.Id));
                }
            }

            tabanZ += kat.KatYuksekligiM;
        }

        return surfaces;
    }

    /// DXF arka plan segmentlerini kat taban Z'sinde 3D çizgilere çevirir.
    public static List<(Point3D A, Point3D B)> ArkaPlanSegmentleri(BinaGeometri geo)
    {
        var sonuc = new List<(Point3D, Point3D)>();
        double tabanZ = 0.0;
        foreach (var kat in KatlariZTabandanUste(geo))
        {
            if (kat.DxfGoster)
            {
                double ox = kat.OriginOffset.X, oy = kat.OriginOffset.Y;
                foreach (var s in kat.DxfSegmentler)
                    sonuc.Add((
                        new Point3D(s.A.X + ox, s.A.Y + oy, tabanZ),
                        new Point3D(s.B.X + ox, s.B.Y + oy, tabanZ)));
            }
            tabanZ += kat.KatYuksekligiM;
        }
        return sonuc;
    }

    /// Açıklıkları kat taban Z'sinde 3D segmentlere çevirir (tip + iptal bilgisiyle).
    public static List<(Point3D Bas, Point3D Bit, bool Iptal, BinaAciklikTipi Tip)> Acikliklar3D(BinaGeometri geo)
    {
        var sonuc = new List<(Point3D, Point3D, bool, BinaAciklikTipi)>();
        double tabanZ = 0.0;
        foreach (var kat in KatlariZTabandanUste(geo))
        {
            double ox = kat.OriginOffset.X, oy = kat.OriginOffset.Y;
            foreach (var a in kat.Acikliklar)
                sonuc.Add((
                    new Point3D(a.Bas.X + ox, a.Bas.Y + oy, tabanZ),
                    new Point3D(a.Bit.X + ox, a.Bit.Y + oy, tabanZ),
                    a.IptalEdildi,
                    a.Tip));
            tabanZ += kat.KatYuksekligiM;
        }
        return sonuc;
    }

    private static IEnumerable<BinaKat> KatlariZTabandanUste(BinaGeometri geo)
        => geo.Katlar.Reverse();
}
