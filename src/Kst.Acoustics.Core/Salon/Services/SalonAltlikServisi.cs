using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Salon.Models;
using netDxf;
using netDxf.Units;

namespace Kst.Acoustics.Core.Salon.Services;

public sealed record SalonAltlikImportSonucu(
    SalonReferansAltlik Altlik,
    IReadOnlyList<string> Uyarilar);

/// <summary>
/// DXF/PDF/görsel referans altlığını kalibre eder. DXF çizgileri dosyaya önbelleklenir;
/// PDF ve raster görüntüler kaynak yol + dönüşümle UI tarafından çizilir.
/// </summary>
public static class SalonAltlikServisi
{
    public static SalonAltlikImportSonucu DxfIceriAktar(string dosyaYolu, double? unitlessOlcekM = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dosyaYolu);
        var dxf = DxfDocument.Load(dosyaYolu)
                  ?? throw new InvalidDataException("DXF referans altlığı okunamadı.");
        var uyarilar = new List<string>();
        double olcek = MetreOlcegi(dxf.DrawingVariables.InsUnits, unitlessOlcekM, uyarilar);
        var seg = new List<SalonAltlikSegmenti>();

        void Ekle(double x1, double y1, double x2, double y2, string? katman)
        {
            var a = new Point2D(x1 * olcek, y1 * olcek);
            var b = new Point2D(x2 * olcek, y2 * olcek);
            if (Uzaklik(a, b) <= 1e-9) return;
            seg.Add(new SalonAltlikSegmenti { Baslangic = a, Bitis = b, Katman = katman ?? "0" });
        }

        foreach (var l in dxf.Entities.Lines)
            Ekle(l.StartPoint.X, l.StartPoint.Y, l.EndPoint.X, l.EndPoint.Y, l.Layer?.Name);
        foreach (var p in dxf.Entities.Polylines2D)
        {
            for (int i = 0; i + 1 < p.Vertexes.Count; i++)
                Ekle(p.Vertexes[i].Position.X, p.Vertexes[i].Position.Y,
                    p.Vertexes[i + 1].Position.X, p.Vertexes[i + 1].Position.Y, p.Layer?.Name);
            if (p.IsClosed && p.Vertexes.Count > 2)
                Ekle(p.Vertexes[^1].Position.X, p.Vertexes[^1].Position.Y,
                    p.Vertexes[0].Position.X, p.Vertexes[0].Position.Y, p.Layer?.Name);
        }
        foreach (var p in dxf.Entities.Polylines3D)
        {
            for (int i = 0; i + 1 < p.Vertexes.Count; i++)
                Ekle(p.Vertexes[i].X, p.Vertexes[i].Y, p.Vertexes[i + 1].X, p.Vertexes[i + 1].Y, p.Layer?.Name);
            if (p.IsClosed && p.Vertexes.Count > 2)
                Ekle(p.Vertexes[^1].X, p.Vertexes[^1].Y, p.Vertexes[0].X, p.Vertexes[0].Y, p.Layer?.Name);
        }
        foreach (var c in dxf.Entities.Circles)
            EgriEkle(c.Center.X, c.Center.Y, c.Radius, 0, 360, c.Layer?.Name, Ekle);
        foreach (var a in dxf.Entities.Arcs)
            EgriEkle(a.Center.X, a.Center.Y, a.Radius, a.StartAngle, a.EndAngle, a.Layer?.Name, Ekle);

        if (seg.Count == 0) uyarilar.Add("DXF içinde gösterilebilir LINE/polyline/arc/circle bulunamadı.");
        return new SalonAltlikImportSonucu(new SalonReferansAltlik
        {
            Ad = Path.GetFileNameWithoutExtension(dosyaYolu),
            DosyaYolu = Path.GetFullPath(dosyaYolu),
            Tip = SalonAltlikTipi.Dxf,
            Segmentler = seg
        }, uyarilar);
    }

    public static SalonReferansAltlik DosyaAltligiOlustur(string dosyaYolu)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dosyaYolu);
        string ext = Path.GetExtension(dosyaYolu).ToLowerInvariant();
        var tip = ext switch
        {
            ".pdf" => SalonAltlikTipi.Pdf,
            ".png" or ".jpg" or ".jpeg" or ".webp" or ".bmp" or ".tif" or ".tiff" => SalonAltlikTipi.Gorsel,
            ".dxf" => SalonAltlikTipi.Dxf,
            _ => throw new NotSupportedException($"'{ext}' referans altlığı desteklenmiyor.")
        };
        if (tip == SalonAltlikTipi.Dxf) return DxfIceriAktar(dosyaYolu).Altlik;
        return new SalonReferansAltlik
        {
            Ad = Path.GetFileNameWithoutExtension(dosyaYolu),
            DosyaYolu = Path.GetFullPath(dosyaYolu),
            Tip = tip
        };
    }

    /// Görsel/PDF üzerinde işaretlenen iki nokta arasını bilinen metreye kalibre eder.
    public static void IkiNoktaylaOlceklendir(
        SalonReferansAltlik altlik, Point2D kaynakA, Point2D kaynakB, double gercekMesafeM)
    {
        ArgumentNullException.ThrowIfNull(altlik);
        if (!double.IsFinite(gercekMesafeM) || gercekMesafeM <= 0)
            throw new ArgumentOutOfRangeException(nameof(gercekMesafeM));
        double kaynak = Uzaklik(kaynakA, kaynakB);
        if (kaynak <= 1e-9) throw new ArgumentException("Kalibrasyon noktaları birbirinden farklı olmalıdır.");
        altlik.Olcek = gercekMesafeM / kaynak;
    }

    public static Point2D Donustur(SalonReferansAltlik altlik, Point2D kaynak)
    {
        double a = altlik.DonusDerece * Math.PI / 180.0;
        double x = kaynak.X * altlik.Olcek, y = kaynak.Y * altlik.Olcek;
        return new Point2D(
            altlik.OfsetX + x * Math.Cos(a) - y * Math.Sin(a),
            altlik.OfsetY + x * Math.Sin(a) + y * Math.Cos(a));
    }

    public static IReadOnlyList<(Point2D A, Point2D B, string Katman)> DonusmusSegmentler(
        SalonReferansAltlik altlik)
        => altlik.Segmentler.Select(s =>
            (Donustur(altlik, s.Baslangic), Donustur(altlik, s.Bitis), s.Katman)).ToList();

    private static void EgriEkle(double cx, double cy, double r, double basDeg, double bitDeg,
        string? katman, Action<double, double, double, double, string?> ekle)
    {
        double span = bitDeg - basDeg;
        while (span <= 0) span += 360;
        int n = Math.Clamp((int)Math.Ceiling(span / 7.5), 4, 96);
        Point2D Once(int i)
        {
            double a = (basDeg + span * i / n) * Math.PI / 180.0;
            return new Point2D(cx + r * Math.Cos(a), cy + r * Math.Sin(a));
        }
        var onceki = Once(0);
        for (int i = 1; i <= n; i++)
        {
            var simdi = Once(i);
            ekle(onceki.X, onceki.Y, simdi.X, simdi.Y, katman);
            onceki = simdi;
        }
    }

    private static double MetreOlcegi(DrawingUnits u, double? unitless, List<string> uyarilar)
        => u switch
        {
            DrawingUnits.Millimeters => 0.001,
            DrawingUnits.Centimeters => 0.01,
            DrawingUnits.Decimeters => 0.1,
            DrawingUnits.Meters => 1,
            DrawingUnits.Kilometers => 1000,
            DrawingUnits.Inches => 0.0254,
            DrawingUnits.Feet => 0.3048,
            DrawingUnits.Yards => 0.9144,
            DrawingUnits.Miles => 1609.344,
            DrawingUnits.USSurveyFeet => 1200.0 / 3937.0,
            _ => Unitless(unitless, uyarilar)
        };

    private static double Unitless(double? olcek, List<string> uyarilar)
    {
        double s = olcek is > 0 ? olcek.Value : 1.0;
        uyarilar.Add($"DXF birimi belirsiz; 1 çizim birimi = {s:G} m kabul edildi. İki noktayla kalibre edebilirsiniz.");
        return s;
    }

    private static double Uzaklik(Point2D a, Point2D b)
        => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
}
