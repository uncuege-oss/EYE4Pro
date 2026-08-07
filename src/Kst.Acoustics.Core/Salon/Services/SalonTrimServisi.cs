using System;
using System.Collections.Generic;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Salon.Services;

/// 2D çizgi düzenleme yardımcıları: segment-segment kesişim + çizgi trim (bölme/kısaltma).
public static class SalonTrimServisi
{
    private const double Eps = 1e-9;

    /// İki segmentin (a1→a2, b1→b2) gerçek kesişimini bulur. Kesişiyorsa true döner;
    /// <paramref name="tA"/> kesişimin A segmenti üzerindeki parametresidir (0..1).
    /// Paralel/çakışık segmentler kesişmez sayılır.
    public static bool SegmentKesisim(Point2D a1, Point2D a2, Point2D b1, Point2D b2,
        out Point2D nokta, out double tA)
    {
        nokta = default;
        tA = 0;

        double dx = a2.X - a1.X, dy = a2.Y - a1.Y;
        double ex = b2.X - b1.X, ey = b2.Y - b1.Y;
        double payda = dx * ey - dy * ex;
        if (Math.Abs(payda) < Eps) return false; // paralel

        double rx = b1.X - a1.X, ry = b1.Y - a1.Y;
        double t = (rx * ey - ry * ex) / payda;
        double u = (rx * dy - ry * dx) / payda;
        if (t < -Eps || t > 1 + Eps || u < -Eps || u > 1 + Eps) return false;

        tA = t;
        nokta = new Point2D(a1.X + t * dx, a1.Y + t * dy);
        return true;
    }

    /// Bir çizgiyi (a1→a2) kesici segmentlerle böler ve <paramref name="tTiklama"/>
    /// parametresini içeren aralığı çıkarır. <paramref name="parcalar"/> kalan parçalardır
    /// (0 = segment tümüyle silindi, 1 veya 2 = kalan çizgiler).
    /// Dönüş: kesim yapıldıysa true. Hiçbir kesici değmiyorsa false (değişiklik yok).
    /// Uzat (extend): <paramref name="uc"/> ucunu, <paramref name="sabit"/>→<paramref name="uc"/>
    /// doğrultusunda ileri giderek kesicilerle İLK kesişime taşır. Kesişim yoksa null.
    /// Trim'in tamamlayıcısı — AutoCAD EXTEND davranışı.
    public static Point2D? Uzat(Point2D sabit, Point2D uc,
        IEnumerable<(Point2D A, Point2D B)> kesiciler)
    {
        double dx = uc.X - sabit.X, dy = uc.Y - sabit.Y;
        double boy2 = dx * dx + dy * dy;
        if (boy2 < Eps) return null;

        double enKucukT = double.MaxValue;
        foreach (var (b1, b2) in kesiciler)
        {
            double ex = b2.X - b1.X, ey = b2.Y - b1.Y;
            double payda = dx * ey - dy * ex;
            if (Math.Abs(payda) < Eps) continue; // paralel

            double rx = b1.X - sabit.X, ry = b1.Y - sabit.Y;
            double t = (rx * ey - ry * ex) / payda; // ışın parametresi (uc'ta t=1)
            double u = (rx * dy - ry * dx) / payda; // kesici parametresi
            if (u < -Eps || u > 1 + Eps) continue;  // kesicinin dışında
            if (t <= 1 + 1e-6) continue;            // ucun gerisinde/üzerinde
            if (t < enKucukT) enKucukT = t;
        }

        if (enKucukT == double.MaxValue) return null;
        return new Point2D(sabit.X + enKucukT * dx, sabit.Y + enKucukT * dy);
    }

    /// Not: Uçlarda (t≈0 / t≈1) değen kesiciler de "kesim var" sayılır — iki ucu kesicilerle
    /// sınırlı bir çizgiye tıklanınca tüm segment silinir (AutoCAD trim davranışı).
    public static bool CizgiTrim(
        Point2D a1, Point2D a2,
        IEnumerable<(Point2D A, Point2D B)> kesiciler,
        double tTiklama,
        out List<(Point2D A, Point2D B)> parcalar)
    {
        parcalar = new List<(Point2D, Point2D)>();

        var ts = new List<double> { 0.0, 1.0 };
        bool kesisimVar = false;
        foreach (var (b1, b2) in kesiciler)
        {
            if (!SegmentKesisim(a1, a2, b1, b2, out _, out double t)) continue;
            if (t < -1e-6 || t > 1 + 1e-6) continue;
            kesisimVar = true;
            if (t > 1e-4 && t < 1 - 1e-4) ts.Add(t); // iç kesişim = kırılma noktası
        }

        if (!kesisimVar) return false; // hiçbir kesici değmiyor → trim yapılamaz

        ts.Sort();
        Point2D Nokta(double t) => new(a1.X + t * (a2.X - a1.X), a1.Y + t * (a2.Y - a1.Y));
        double tk = Math.Clamp(tTiklama, 0, 1);

        // Tıklamayı içeren aralığı deterministik seç (sınırdaysa orta noktası en yakın olan).
        int sel = -1;
        double best = double.MaxValue;
        for (int i = 0; i + 1 < ts.Count; i++)
        {
            double lo = ts[i], hi = ts[i + 1];
            if (hi - lo < 1e-7) continue;
            double orta = (lo + hi) / 2.0;
            bool icinde = tk >= lo && tk <= hi;
            double skor = (icinde ? 0 : 1e6) + Math.Abs(tk - orta);
            if (skor < best) { best = skor; sel = i; }
        }
        if (sel < 0) return true; // tüm aralık sıfır uzunlukta → parça yok (silinir)

        for (int i = 0; i + 1 < ts.Count; i++)
        {
            if (i == sel) continue;
            double lo = ts[i], hi = ts[i + 1];
            if (hi - lo < 1e-7) continue;
            parcalar.Add((Nokta(lo), Nokta(hi)));
        }
        return true;
    }
}
