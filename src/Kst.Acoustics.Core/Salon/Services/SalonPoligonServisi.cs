using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Salon.Services;

/// M51 poligon yardımcıları. Clipper2 fark/kesişim sonuçları delikli halka kümesi
/// döndürebilir; <see cref="ProjectSurface"/> yalnız basit (deliksiz) poligon taşır.
/// <see cref="BasitPoligonlaraAyir"/> delikleri "keyhole" tekniğiyle açar: deliğin
/// içinden geçen çok ince (±2 mm) düşey bir şerit çıkarılır, dış halka deliksiz
/// parçalara bölünür. Kaybolan alan ihmal edilebilir (akustik hesap toleransı altı).
public static class SalonPoligonServisi
{
    /// Keyhole şeridinin yarı genişliği (m). ClipperAdaptor 1 mm tamsayı ızgarasında
    /// çalıştığından şerit en az birkaç ızgara hücresi genişliğinde olmalı; daha darı
    /// yuvarlamada sıfır genişliğe çöker ve delik açılamaz.
    private const double SeritYarimM = 0.002;

    /// Halka kümesini (dış halkalar CCW+, delikler CW−) basit poligonlara ayırır.
    public static List<List<Point2D>> BasitPoligonlaraAyir(List<List<Point2D>> halkalar)
    {
        var valid=halkalar.Where(p=>p.Count>=3).ToList();
        if(!valid.Any(p=>ClipperAdaptor.IsaretliAlan(p)<0)) return valid;
        // Exact horizontal partition through hole vertices; no artificial slit or lost area.
        double lo=valid.SelectMany(p=>p).Min(p=>p.X)-1, hi=valid.SelectMany(p=>p).Max(p=>p.X)+1;
        var ys=valid.SelectMany(p=>p).Select(p=>p.Y).Distinct().Order().ToList();
        var result=new List<List<Point2D>>();
        for(int i=0;i+1<ys.Count;i++)
        {
            if(ys[i+1]-ys[i]<1e-9)continue;
            var band=new List<Point2D>{new(lo,ys[i]),new(hi,ys[i]),new(hi,ys[i+1]),new(lo,ys[i+1])};
            result.AddRange(ClipperAdaptor.Kesisim(valid,new[]{(IReadOnlyList<Point2D>)band}).Where(p=>ClipperAdaptor.IsaretliAlan(p)>1e-9));
        }
        return result;
    }

    /// Yatay paneli (sabit kotlu poligon) kabuk içine kırpar; kabuk yoksa panel aynen
    /// döner. Sonuç keyhole ile basit poligonlara açılır.
    public static List<List<Point2D>> KabugaKirp(
        List<List<Point2D>>? kabuk, IReadOnlyList<Point2D> panel)
    {
        if (kabuk is null || kabuk.Count == 0)
            return new List<List<Point2D>> { panel.ToList() };
        return BasitPoligonlaraAyir(ClipperAdaptor.Kesisim(kabuk, new[] { panel }));
    }

    /// Yatay doğru parçasının (y sabit, x1→x2) kabuk içinde kalan alt aralıkları.
    /// Düşey yüzeylerin (korniş/parapet/saçak) kabuğa kırpılması için: çok ince bir
    /// şeritle kesişim alınır, sonuç halkaların x-aralıkları döner. Kabuk yoksa tam aralık.
    public static List<(double X1, double X2)> YatayParcalar(
        List<List<Point2D>>? kabuk, double y, double x1, double x2)
    {
        if (x2 < x1) (x1, x2) = (x2, x1);
        if (kabuk is null || kabuk.Count == 0)
            return new List<(double, double)> { (x1, x2) };

        var serit = new List<Point2D>
        {
            new(x1, y - SeritYarimM), new(x2, y - SeritYarimM),
            new(x2, y + SeritYarimM), new(x1, y + SeritYarimM)
        };
        return ClipperAdaptor.Kesisim(kabuk, new[] { (IReadOnlyList<Point2D>)serit })
            .Select(h => (h.Min(p => p.X), h.Max(p => p.X)))
            .Where(a => a.Item2 - a.Item1 > 1e-6)
            .OrderBy(a => a.Item1)
            .ToList();
    }

    /// Düşey doğru parçasının (x sabit, y1→y2) kabuk içinde kalan alt aralıkları.
    public static List<(double Y1, double Y2)> DikeyParcalar(
        List<List<Point2D>>? kabuk, double x, double y1, double y2)
    {
        if (y2 < y1) (y1, y2) = (y2, y1);
        if (kabuk is null || kabuk.Count == 0)
            return new List<(double, double)> { (y1, y2) };

        var serit = new List<Point2D>
        {
            new(x - SeritYarimM, y1), new(x + SeritYarimM, y1),
            new(x + SeritYarimM, y2), new(x - SeritYarimM, y2)
        };
        return ClipperAdaptor.Kesisim(kabuk, new[] { (IReadOnlyList<Point2D>)serit })
            .Select(h => (h.Min(p => p.Y), h.Max(p => p.Y)))
            .Where(a => a.Item2 - a.Item1 > 1e-6)
            .OrderBy(a => a.Item1)
            .ToList();
    }

    /// Açık polyline'ı imzalı mesafeyle paralel öteler (offset). Pozitif mesafe, gidiş
    /// yönünün **soluna** ötelemedir. Köşelerde miter (doğru kesişimi) kullanılır; keskin
    /// köşelerde miter uzaması sınırlandırılır (4×mesafe). En az 2 nokta gerekir.
    public static List<Point2D> AcikOffset(IReadOnlyList<Point2D> pts, double mesafeM)
    {
        var sonuc = new List<Point2D>();
        if (pts.Count < 2 || Math.Abs(mesafeM) < 1e-9)
            return pts.ToList();

        // Segment sol normalleri
        var normaller = new List<(double X, double Y)>();
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            double dx = pts[i + 1].X - pts[i].X, dy = pts[i + 1].Y - pts[i].Y;
            double L = Math.Sqrt(dx * dx + dy * dy);
            normaller.Add(L < 1e-12 ? (0, 0) : (-dy / L, dx / L));
        }

        for (int i = 0; i < pts.Count; i++)
        {
            if (i == 0 || i == pts.Count - 1)
            {
                var n = normaller[Math.Min(i, normaller.Count - 1)];
                sonuc.Add(new Point2D(pts[i].X + n.X * mesafeM, pts[i].Y + n.Y * mesafeM));
                continue;
            }

            // İç köşe: iki ötelenmiş doğrunun kesişimi (miter)
            var n1 = normaller[i - 1];
            var n2 = normaller[i];
            double mx = n1.X + n2.X, my = n1.Y + n2.Y;
            double payda = mx * n1.X + my * n1.Y; // |m|²/… kosinüs terimi
            if (Math.Abs(payda) < 1e-9)
            {
                sonuc.Add(new Point2D(pts[i].X + n2.X * mesafeM, pts[i].Y + n2.Y * mesafeM));
                continue;
            }
            double olcek = mesafeM / payda;
            double uzama = Math.Sqrt(mx * mx + my * my) * Math.Abs(olcek);
            if (uzama > 4 * Math.Abs(mesafeM)) // keskin köşe: miter'i sınırla
                olcek = Math.Sign(olcek) * 4 * Math.Abs(mesafeM) / Math.Sqrt(mx * mx + my * my);
            sonuc.Add(new Point2D(pts[i].X + mx * olcek, pts[i].Y + my * olcek));
        }
        return sonuc;
    }

    /// İşaretli birinci Y-momenti: ∫y dA (shoelace türevi). Delikli halka kümesinde
    /// işaretler doğal olarak düşer; eğimli tavan hacim integrali ∫(a+b·y)dA = a·A + b·∫y dA
    /// için kullanılır.
    public static double IsaretliYMomenti(IReadOnlyList<Point2D> poly)
    {
        if (poly.Count < 3) return 0;
        double m = 0;
        for (int i = 0; i < poly.Count; i++)
        {
            var p = poly[i];
            var q = poly[(i + 1) % poly.Count];
            m += (p.Y + q.Y) * (p.X * q.Y - q.X * p.Y);
        }
        return m / 6.0;
    }
}
