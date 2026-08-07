using System.Collections.Generic;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Salon.Models;

namespace Kst.Acoustics.Core.Salon.Services;

/// Balkon/loca platformu geometrisi: footprint ve 3D yüzeyler
/// (döşeme + alt soffit + çevre saçak + çevre parapet).
public static class SalonBalkonServisi
{
    public static List<Point2D> Footprint(BalkonGeometri b)
    {
        double xL = b.MerkezX - b.GenislikM / 2, xR = b.MerkezX + b.GenislikM / 2;
        double yA = b.MerkezY - b.DerinlikM / 2, yB = b.MerkezY + b.DerinlikM / 2;
        return new List<Point2D> { new(xL, yA), new(xR, yA), new(xR, yB), new(xL, yB) };
    }

    /// 3D yüzeyler. <paramref name="kabuk"/> verilirse döşeme/soffit kabuk içine
    /// kırpılır; saçak/parapet kenarları kabuk içinde kalan alt aralıklara bölünür.
    public static List<ProjectSurface> Yuzeyler(
        BalkonGeometri b, ref int id, List<List<Point2D>>? kabuk = null)
    {
        var surfaces = new List<ProjectSurface>();
        var poly = Footprint(b);
        double zDoseme = b.TabanKotuM;
        double zAlt = b.TabanKotuM - (b.KalinlikM > 0 ? b.KalinlikM : 0);

        // Döşeme (üst yüzey)
        foreach (var p in SalonPoligonServisi.KabugaKirp(kabuk, poly))
            surfaces.Add(new ProjectSurface(id++, $"{b.Ad} Döşeme", "Balkon",
                p.Select(q => new Point3D(q.X, q.Y, zDoseme)).ToList()));

        // Alt yüzey (soffit) — kalınlık varsa
        if (zDoseme - zAlt > 1e-6)
        {
            foreach (var p in SalonPoligonServisi.KabugaKirp(kabuk, poly))
                surfaces.Add(new ProjectSurface(id++, $"{b.Ad} Alt", "Balkon-Alt",
                    p.Select(q => new Point3D(q.X, q.Y, zAlt)).ToList()));

            // Çevre saçak (döşeme kenarı)
            KenarYuzeyleri(surfaces, ref id, b, kabuk, zAlt, zDoseme, $"{b.Ad} Saçak", "Balkon");
        }

        // Çevre parapet (korkuluk)
        if (b.ParapetM > 1e-6)
            KenarYuzeyleri(surfaces, ref id, b, kabuk, zDoseme, zDoseme + b.ParapetM, $"{b.Ad} Parapet", "Balkon-Parapet");

        return surfaces;
    }

    /// Dikdörtgen footprint kenarlarında (kabuğa kırpılmış) düşey yüzey bantları üretir.
    private static void KenarYuzeyleri(List<ProjectSurface> surfaces, ref int id,
        BalkonGeometri b, List<List<Point2D>>? kabuk, double zLow, double zHigh, string ad, string kategori)
    {
        double xL = b.MerkezX - b.GenislikM / 2, xR = b.MerkezX + b.GenislikM / 2;
        double yA = b.MerkezY - b.DerinlikM / 2, yB = b.MerkezY + b.DerinlikM / 2;

        foreach (double y in new[] { yA, yB })
            foreach (var (px1, px2) in SalonPoligonServisi.YatayParcalar(kabuk, y, xL, xR))
                surfaces.Add(Yuzey(id++, new Point2D(px1, y), new Point2D(px2, y), zLow, zHigh, ad, kategori));

        foreach (double x in new[] { xL, xR })
            foreach (var (py1, py2) in SalonPoligonServisi.DikeyParcalar(kabuk, x, yA, yB))
                surfaces.Add(Yuzey(id++, new Point2D(x, py1), new Point2D(x, py2), zLow, zHigh, ad, kategori));
    }

    private static ProjectSurface Yuzey(int id, Point2D a, Point2D b, double zLow, double zHigh, string ad, string kategori)
        => new(id, ad, kategori, new List<Point3D>
        {
            new(a.X, a.Y, zLow),
            new(b.X, b.Y, zLow),
            new(b.X, b.Y, zHigh),
            new(a.X, a.Y, zHigh)
        });
}
