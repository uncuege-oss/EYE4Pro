using System;
using System.Collections.Generic;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Electric.Ortak;

/// <summary>
/// Poligon kenarı/iç ızgara aday noktası üreten saf geometri yardımcıları. M12 (Basit Proje Çiz)
/// eleman yerleştirmesi ve M15 (Çevre Aydınlatma) direk dizilimi tarafından ortak kullanılır.
/// M12/M15 model tiplerine bağımlı değildir; yalnızca <see cref="Point2D"/> + <see cref="PoligonYardimci"/>.
/// </summary>
public static class YerlesimGeometri
{
    /// <summary>
    /// Poligon kenarları boyunca <paramref name="adim"/> aralıklı, içe <paramref name="giris"/> metre
    /// kaydırılmış aday noktalar üretir. Yeşil alan/yol kenarına direk dizilimi için kullanılır.
    /// </summary>
    /// <summary>
    /// <paramref name="x"/>,<paramref name="y"/> noktasına en yakın poligon kenarının (duvarın)
    /// bilgisi: kenar üzerindeki en yakın nokta, kenar birim yön vektörü (Ux,Uy), poligon İÇİNE
    /// bakan birim normal (Nx,Ny) ve noktaya uzaklık (Mesafe). Duvara snap edilmiş sembolleri
    /// duvara paralel döndürmek için kullanılır.
    /// </summary>
    public static (double Px, double Py, double Ux, double Uy, double Nx, double Ny, double Mesafe)
        EnYakinDuvar(IReadOnlyList<Point2D> poligon, double x, double y)
    {
        if (poligon.Count < 2)
            return (x, y, 1.0, 0.0, 0.0, 1.0, double.MaxValue);

        double minDist = double.MaxValue;
        double bestPx = x, bestPy = y, bestUx = 1.0, bestUy = 0.0;

        for (int i = 0; i < poligon.Count; i++)
        {
            var a = poligon[i];
            var b = poligon[(i + 1) % poligon.Count];
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double l2 = dx * dx + dy * dy;
            double t = l2 < 1e-12 ? 0.0 : Math.Clamp(((x - a.X) * dx + (y - a.Y) * dy) / l2, 0.0, 1.0);
            double px = a.X + t * dx, py = a.Y + t * dy;
            double d = Math.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
            if (d < minDist)
            {
                minDist = d;
                bestPx = px; bestPy = py;
                double len = Math.Sqrt(l2);
                bestUx = len > 1e-6 ? dx / len : 1.0;
                bestUy = len > 1e-6 ? dy / len : 0.0;
            }
        }

        // İç normal: kenar yönüne dik iki adaydan poligon içine bakanı seç.
        double nx = -bestUy, ny = bestUx;
        var icAday = new Point2D(bestPx + nx * 0.05, bestPy + ny * 0.05);
        if (!PoligonYardimci.IsPointInside(icAday, poligon))
        {
            // Ters normal poligon içinde mi? Değilse eleman noktasına doğru olanı kullan.
            var icAday2 = new Point2D(bestPx - nx * 0.05, bestPy - ny * 0.05);
            if (PoligonYardimci.IsPointInside(icAday2, poligon) ||
                ((x - bestPx) * (-nx) + (y - bestPy) * (-ny)) > 0)
            {
                nx = -nx; ny = -ny;
            }
        }
        return (bestPx, bestPy, bestUx, bestUy, nx, ny, minDist);
    }

    public static List<Point2D> DuvarAdaylariUret(List<Point2D> poligon, double adim, double giris)
    {
        if (poligon.Count < 3 || adim <= 0)
            return [];

        double imzaliAlan = 0;
        for (int i = 0; i < poligon.Count; i++)
        {
            var a = poligon[i];
            var b = poligon[(i + 1) % poligon.Count];
            imzaliAlan += a.X * b.Y - b.X * a.Y;
        }
        bool ccw = imzaliAlan > 0;

        var adaylar = new List<Point2D>();
        for (int i = 0; i < poligon.Count; i++)
        {
            var p1 = poligon[i];
            var p2 = poligon[(i + 1) % poligon.Count];
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double uzunluk = Math.Sqrt(dx * dx + dy * dy);
            if (uzunluk < 0.01)
                continue;

            double ux = dx / uzunluk, uy = dy / uzunluk;
            // CCW poligonda sol normal (-uy, ux) içeri bakar.
            double nx = ccw ? -uy : uy;
            double ny = ccw ? ux : -ux;

            for (double t = 0; t <= uzunluk + 1e-9; t += adim)
            {
                var aday = new Point2D(p1.X + ux * t + nx * giris,
                                       p1.Y + uy * t + ny * giris);
                if (PoligonYardimci.IsPointInside(aday, poligon))
                    adaylar.Add(aday);
            }
        }
        return adaylar;
    }

    /// <summary>
    /// Bounding box ızgarasında, poligon içinde ve kenara <paramref name="kenarMin"/> metre uzakta
    /// olan noktaları döner.
    /// </summary>
    public static List<Point2D> IzgaraAdaylariUret(List<Point2D> poligon, double adim, double kenarMin)
        => IzgaraAdaylariUret(poligon, adim, adim, kenarMin);

    /// <summary>
    /// X ve Y adımları ayrı verilen ızgara üretimi (dikdörtgen grid).
    /// </summary>
    public static List<Point2D> IzgaraAdaylariUret(List<Point2D> poligon, double adimX, double adimY, double kenarMin)
    {
        if (poligon.Count < 3 || adimX <= 0 || adimY <= 0)
            return [];

        double minX = double.MaxValue, maxX = double.MinValue;
        double minY = double.MaxValue, maxY = double.MinValue;
        foreach (var p in poligon)
        {
            if (p.X < minX) minX = p.X;
            if (p.X > maxX) maxX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.Y > maxY) maxY = p.Y;
        }

        var adaylar = new List<Point2D>();
        for (double x = minX + adimX * 0.5; x <= maxX; x += adimX)
        {
            for (double y = minY + adimY * 0.5; y <= maxY; y += adimY)
            {
                var nokta = new Point2D(x, y);
                if (!PoligonYardimci.IsPointInside(nokta, poligon))
                    continue;
                if (KenarlardanMinMesafe(nokta, poligon) < kenarMin)
                    continue;
                adaylar.Add(nokta);
            }
        }
        return adaylar;
    }

    /// <summary>Noktanın poligon kenarlarına en kısa mesafesi.</summary>
    public static double KenarlardanMinMesafe(Point2D nokta, List<Point2D> poligon)
    {
        double min = double.MaxValue;
        for (int i = 0; i < poligon.Count; i++)
        {
            double d = PoligonYardimci.SegmentMesafesi(nokta, poligon[i], poligon[(i + 1) % poligon.Count]);
            if (d < min)
                min = d;
        }
        return min;
    }
}
