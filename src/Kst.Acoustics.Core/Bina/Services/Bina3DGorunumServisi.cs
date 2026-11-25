using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.Core.Bina.Services;

/// <summary>
/// M50 — BinaGeometri'yi 3D önizleme yüzeylerine çevirir (GeometryPreviewControl girdisi).
/// Katlar varsa mutlak proje kotuna, yoksa M50 sol panelindeki sıraya göre dizilir:
/// listedeki son kat tabanda, listedeki ilk kat üsttedir. OriginOffset uygulanır.
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
            tabanZ = EtkinTabanKotu(kat, tabanZ);
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
                double odaTabanZ = EtkinOdaTabanKotu(oda, tabanZ);

                bool dosemeAcik = BinaDuseyBirlesimServisi.DosemeAcikMi(geo, oda.Id);
                bool tavanAcik  = BinaDuseyBirlesimServisi.TavanAcikMi(geo, oda.Id);

                if (!dosemeAcik)
                {
                    var doseme = oda.Poligon
                        .Select(p => new Point3D(p.X + ox, p.Y + oy, odaTabanZ))
                        .ToList();
                    surfaces.Add(new ProjectSurface(id++, $"{oda.Ad} Döşeme", "Doseme", doseme, oda.Id));
                }

                if (!tavanAcik)
                {
                    var tavan = oda.Poligon
                        .Select(p => new Point3D(p.X + ox, p.Y + oy, odaTabanZ + h))
                        .ToList();
                    surfaces.Add(new ProjectSurface(id++, $"{oda.Ad} Tavan", "Tavan", tavan, oda.Id));
                }

                for (int i = 0; i < oda.Poligon.Count; i++)
                {
                    var a = oda.Poligon[i];
                    var b = oda.Poligon[(i + 1) % oda.Poligon.Count];
                    DuvarYuzeyleriniEkle(surfaces, ref id, kat, oda, a, b, ox, oy, odaTabanZ, h);
                }
            }

            tabanZ += kat.KatYuksekligiM;
        }

        return surfaces;
    }

    private const double GeometriEpsilon = 1e-7;
    // Boşluk taramasının raster sınırı ve CAD duvar yüzü arasındaki küçük fark.
    private const double DuvarEslemeToleransiM = 0.15;

    private sealed record DuvarAcikligi(double Bas, double Bit, double Alt, double Ust, BinaAciklikTipi Tip);

    private static void DuvarYuzeyleriniEkle(
        List<ProjectSurface> surfaces, ref int id, BinaKat kat, BinaOda oda,
        Point2D a, Point2D b, double ox, double oy, double tabanZ, double h)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double uzunluk = Math.Sqrt(dx * dx + dy * dy);
        if (!double.IsFinite(uzunluk) || uzunluk < GeometriEpsilon || !double.IsFinite(h) || h <= 0) return;
        double ux = dx / uzunluk, uy = dy / uzunluk;
        var acikliklar = new List<DuvarAcikligi>();
        foreach (var aciklik in kat.Acikliklar)
        {
            var yansima = DuvaraYansit(aciklik, a, b, h);
            if (yansima is not null) acikliklar.Add(yansima);
        }

        // Kenar boyunca şeritler, her şeritte yalnız gerekli düşey kesimler:
        // çakışan/yinelenen açıklıklar aynı alanı iki defa üretmez; katı duvar
        // hiçbir kapı/pencere yüzeyinin arkasında kalmaz.
        var yatayKesimler = acikliklar.SelectMany(x => new[] { x.Bas, x.Bit })
            .Append(0).Append(uzunluk).Distinct().OrderBy(x => x).ToArray();
        for (int i = 0; i < yatayKesimler.Length - 1; i++)
        {
            double x0 = yatayKesimler[i], x1 = yatayKesimler[i + 1];
            if (x1 - x0 <= GeometriEpsilon) continue;
            double ortaX = (x0 + x1) / 2;
            var kesenler = acikliklar.Where(x => ortaX > x.Bas && ortaX < x.Bit).ToList();
            var duseyKesimler = kesenler.SelectMany(x => new[] { x.Alt, x.Ust })
                .Append(0).Append(h).Distinct().OrderBy(x => x).ToArray();
            for (int j = 0; j < duseyKesimler.Length - 1; j++)
            {
                double z0 = duseyKesimler[j], z1 = duseyKesimler[j + 1];
                if (z1 - z0 <= GeometriEpsilon) continue;
                double ortaZ = (z0 + z1) / 2;
                var aciklik = kesenler.FirstOrDefault(x => ortaZ > x.Alt && ortaZ < x.Ust);
                string kategori = aciklik is null ? "Duvar" : aciklik.Tip == BinaAciklikTipi.Pencere ? "Pencere" : "Kapi";
                string ad = kategori == "Kapi" ? "Kapı" : kategori;
                var noktalar = new List<Point3D>
                {
                    new(a.X + ux * x0 + ox, a.Y + uy * x0 + oy, tabanZ + z0),
                    new(a.X + ux * x1 + ox, a.Y + uy * x1 + oy, tabanZ + z0),
                    new(a.X + ux * x1 + ox, a.Y + uy * x1 + oy, tabanZ + z1),
                    new(a.X + ux * x0 + ox, a.Y + uy * x0 + oy, tabanZ + z1)
                };
                surfaces.Add(new ProjectSurface(id++, $"{oda.Ad} {ad}", kategori, noktalar, oda.Id));
            }
        }
    }

    private static DuvarAcikligi? DuvaraYansit(BinaAciklik aciklik, Point2D a, Point2D b, double h)
    {
        if (!AciklikBoyutlari(aciklik, out double alt, out double yukseklik)) return null;
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double uzunluk = Math.Sqrt(dx * dx + dy * dy);
        if (!double.IsFinite(uzunluk) || uzunluk < GeometriEpsilon || !double.IsFinite(h) || h <= 0) return null;
        double ux = dx / uzunluk, uy = dy / uzunluk;
        double adx = aciklik.Bit.X - aciklik.Bas.X, ady = aciklik.Bit.Y - aciklik.Bas.Y;
        double aciklikUzunlugu = Math.Sqrt(adx * adx + ady * ady);
        if (!double.IsFinite(aciklikUzunlugu) || aciklikUzunlugu < GeometriEpsilon) return null;
        // Her iki uç da aynı duvar düzleminde ve açıklık duvara paralel olmalı.
        if (Math.Abs(adx * uy - ady * ux) / aciklikUzunlugu > 0.15) return null;
        if (Math.Abs((aciklik.Bas.X - a.X) * uy - (aciklik.Bas.Y - a.Y) * ux) > DuvarEslemeToleransiM ||
            Math.Abs((aciklik.Bit.X - a.X) * uy - (aciklik.Bit.Y - a.Y) * ux) > DuvarEslemeToleransiM) return null;
        double t0 = (aciklik.Bas.X - a.X) * ux + (aciklik.Bas.Y - a.Y) * uy;
        double t1 = (aciklik.Bit.X - a.X) * ux + (aciklik.Bit.Y - a.Y) * uy;
        double bas = Math.Max(0, Math.Min(t0, t1));
        double bit = Math.Min(uzunluk, Math.Max(t0, t1));
        double ust = Math.Min(h, alt + yukseklik);
        if (bit - bas <= GeometriEpsilon || ust - alt <= GeometriEpsilon) return null;
        return new DuvarAcikligi(bas, bit, alt, ust, aciklik.Tip);
    }

    private static bool AciklikBoyutlari(BinaAciklik aciklik, out double alt, out double yukseklik)
    {
        alt = aciklik.EtkinEsikYuksekligiM;
        yukseklik = aciklik.YukseklikM == 0 ? BinaAciklik.VarsayilanYukseklik(aciklik.Tip) : aciklik.YukseklikM;
        return !aciklik.IptalEdildi &&
            double.IsFinite(aciklik.Bas.X) && double.IsFinite(aciklik.Bas.Y) &&
            double.IsFinite(aciklik.Bit.X) && double.IsFinite(aciklik.Bit.Y) &&
            double.IsFinite(aciklik.EsikYuksekligiM) && aciklik.EsikYuksekligiM >= 0 &&
            double.IsFinite(alt) && double.IsFinite(yukseklik) && yukseklik > 0;
    }

    /// DXF arka plan segmentlerini kat taban Z'sinde 3D çizgilere çevirir.
    public static List<(Point3D A, Point3D B)> ArkaPlanSegmentleri(BinaGeometri geo)
    {
        var sonuc = new List<(Point3D, Point3D)>();
        double tabanZ = 0.0;
        foreach (var kat in KatlariZTabandanUste(geo))
        {
            tabanZ = EtkinTabanKotu(kat, tabanZ);
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

    /// Açıklık alt kenarlarını eşik kotunda 3D segmentlere çevirir.
    public static List<(Point3D Bas, Point3D Bit, bool Iptal, BinaAciklikTipi Tip)> Acikliklar3D(BinaGeometri geo)
    {
        var sonuc = new List<(Point3D, Point3D, bool, BinaAciklikTipi)>();
        double tabanZ = 0.0;
        foreach (var kat in KatlariZTabandanUste(geo))
        {
            tabanZ = EtkinTabanKotu(kat, tabanZ);
            double ox = kat.OriginOffset.X, oy = kat.OriginOffset.Y;
            foreach (var a in kat.Acikliklar)
            {
                if (!AciklikBoyutlari(a, out double esik, out _)) continue;
                if (!kat.BloklariGoster) continue;
                var kotlar = kat.Odalar.Where(oda => oda.Poligon.Count >= 3 &&
                    Enumerable.Range(0, oda.Poligon.Count).Any(i => DuvaraYansit(a,
                        oda.Poligon[i], oda.Poligon[(i + 1) % oda.Poligon.Count],
                        oda.YukseklikOverrideM ?? kat.KatYuksekligiM) is not null))
                    .Select(oda => EtkinOdaTabanKotu(oda, tabanZ)).Distinct();
                foreach (var z in kotlar)
                    sonuc.Add((
                        new Point3D(a.Bas.X + ox, a.Bas.Y + oy, z + esik),
                        new Point3D(a.Bit.X + ox, a.Bit.Y + oy, z + esik),
                        a.IptalEdildi,
                        a.Tip));
            }
            tabanZ += kat.KatYuksekligiM;
        }
        return sonuc;
    }

    private static IEnumerable<BinaKat> KatlariZTabandanUste(BinaGeometri geo)
        => geo.Katlar.Reverse();

    private static double EtkinTabanKotu(BinaKat kat, double yiginKotu)
        => kat.TabanKotuM is { } kot && double.IsFinite(kot) ? kot : yiginKotu;

    private static double EtkinOdaTabanKotu(BinaOda oda, double katKotu)
        => oda.TabanKotuOverrideM is { } kot && double.IsFinite(kot) ? kot : katKotu;
}
