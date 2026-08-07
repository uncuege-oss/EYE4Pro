using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.Ortak;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

/// Tüm katları (oda poligonları + kat yüksekliği) ProjectSurface listesine çevirir.
/// GeometryPreviewControl bu listeyi 2D/3D render eder.
public class BasitProjeCizGeometriServisi
{
    public List<ProjectSurface> BinaModelineCevir(IReadOnlyList<BasitProjeKat> katlar)
    {
        var surfaces = new List<ProjectSurface>();
        int id = 1;
        double tabanZ = 0.0;

        foreach (var kat in katlar.Reverse())
        {
            if (!kat.BloklariGoster)
            {
                tabanZ += kat.KatYuksekligiM;
                continue;
            }

            double off_x = kat.OriginOffset.X;
            double off_y = kat.OriginOffset.Y;

            foreach (var oda in kat.Odalar)
            {
                if (oda.Poligon.Count < 3) continue;

                var dosemeVerts = oda.Poligon
                    .Select(p => new Point3D(p.X + off_x, p.Y + off_y, tabanZ))
                    .ToList();
                surfaces.Add(new ProjectSurface(id++, $"{oda.Ad} Döşeme", "Doseme", dosemeVerts, oda.Id));

                var tavanVerts = oda.Poligon
                    .Select(p => new Point3D(p.X + off_x, p.Y + off_y, tabanZ + kat.KatYuksekligiM))
                    .ToList();
                surfaces.Add(new ProjectSurface(id++, $"{oda.Ad} Tavan", "Tavan", tavanVerts, oda.Id));

                for (int i = 0; i < oda.Poligon.Count; i++)
                {
                    var a = oda.Poligon[i];
                    var b = oda.Poligon[(i + 1) % oda.Poligon.Count];
                    var duvar = new List<Point3D>
                    {
                        new(a.X + off_x, a.Y + off_y, tabanZ),
                        new(b.X + off_x, b.Y + off_y, tabanZ),
                        new(b.X + off_x, b.Y + off_y, tabanZ + kat.KatYuksekligiM),
                        new(a.X + off_x, a.Y + off_y, tabanZ + kat.KatYuksekligiM)
                    };
                    surfaces.Add(new ProjectSurface(id++, $"{oda.Ad} Duvar", "Duvar", duvar, oda.Id));
                }
            }

            tabanZ += kat.KatYuksekligiM;
        }

        return surfaces;
    }

    /// DxfGoster işaretli katların ham DXF çizgilerini, kat Z kotunda ve OriginOffset
    /// uygulanmış olarak döner. GeometryPreviewControl bunları arka plan referansı çizer.
    public List<(Point3D A, Point3D B)> ArkaPlanSegmentleri(IReadOnlyList<BasitProjeKat> katlar)
    {
        var segs = new List<(Point3D, Point3D)>();
        double tabanZ = 0.0;

        foreach (var kat in katlar.Reverse())
        {
            if (!kat.DxfGoster)
            {
                tabanZ += kat.KatYuksekligiM;
                continue;
            }

            double off_x = kat.OriginOffset.X;
            double off_y = kat.OriginOffset.Y;
            double z = tabanZ;

            foreach (var s in kat.DxfSegmentleri)
            {
                segs.Add((
                    new Point3D(s.A.X + off_x, s.A.Y + off_y, z),
                    new Point3D(s.B.X + off_x, s.B.Y + off_y, z)));
            }

            tabanZ += kat.KatYuksekligiM;
        }

        return segs;
    }

    // ── Yerleştirme Geometrisi ──────────────────────────────────────────────────
    // Saf geometri üretimi Ortak/YerlesimGeometri'ye taşındı (M15 ile paylaşılır);
    // bu metotlar uyumluluk için delegasyon yapar.

    /// Poligon kenarları boyunca <adim> aralıklı, içe <giris> metre kaydırılmış aday noktalar üretir.
    public List<Point2D> DuvarAdaylariUret(List<Point2D> poligon, double adim, double giris)
        => YerlesimGeometri.DuvarAdaylariUret(poligon, adim, giris);

    /// Bounding box ızgarasında, poligon içinde ve duvara <duvarMin> metre uzakta olan noktaları döner.
    public List<Point2D> IzgaraAdaylariUret(List<Point2D> poligon, double adim, double duvarMin)
        => YerlesimGeometri.IzgaraAdaylariUret(poligon, adim, duvarMin);

    /// X ve Y adımları ayrı verilen ızgara üretimi (dikdörtgen grid).
    public List<Point2D> IzgaraAdaylariUret(List<Point2D> poligon, double adimX, double adimY, double duvarMin)
        => YerlesimGeometri.IzgaraAdaylariUret(poligon, adimX, adimY, duvarMin);

    /// Nokta herhangi bir iptal edilmemiş açıklık segmentine <koruma> metreden yakınsa true döner.
    public bool AciklikBolgesindeMi(Point2D nokta, IEnumerable<BpcAciklik> acikliklar, double koruma)
    {
        foreach (var a in acikliklar)
        {
            if (a.IptalEdildi) continue;
            if (PoligonYardimci.SegmentMesafesi(nokta, a.Bas, a.Bit) < koruma)
                return true;
        }
        return false;
    }

    // ── 3D Model / Render ───────────────────────────────────────────────────────

    /// Seçili katın yerleşim elemanlarını (armatür/anahtar/pano) 2D düzleminde
    /// kat Z kotunda nokta listesi olarak döner.
    public List<Point3D> ElemanNoktalari(BasitProjeKat? seciliKat, IReadOnlyList<BasitProjeKat> tumKatlar)
    {
        if (seciliKat == null) return new();
        double z = 0.0;
        foreach (var k in tumKatlar.Reverse())
        {
            if (k == seciliKat) break;
            z += k.KatYuksekligiM;
        }
        double off_x = seciliKat.OriginOffset.X;
        double off_y = seciliKat.OriginOffset.Y;
        return seciliKat.Elemanlar
            .Select(e => new Point3D(e.X + off_x, e.Y + off_y, z))
            .ToList();
    }
}
