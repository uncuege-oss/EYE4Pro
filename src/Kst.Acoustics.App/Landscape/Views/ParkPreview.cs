using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Landscape.Models;
using Kst.Acoustics.Core.Landscape.Services;

namespace Kst.Acoustics.App.Landscape.Views;

/// M70 park planını 2D çizen önizleme kontrolü: park sınırı, kullanılabilir alan
/// halkaları (dış = yeşil, delik = bina+setback) ve sert zemin öğeleri (yol/döşeme/
/// oturma/süs havuzu/bank). Fit-to-view, Y ekseni yukarı. Etkileşim yok (F8).
/// NOT: görsel çıktı headless ortamda doğrulanmadı.
public class ParkPreview : Control
{
    public static readonly StyledProperty<IReadOnlyList<Point2D>?> SinirProperty =
        AvaloniaProperty.Register<ParkPreview, IReadOnlyList<Point2D>?>(nameof(Sinir));

    public static readonly StyledProperty<IReadOnlyList<IReadOnlyList<Point2D>>?> HalkalarProperty =
        AvaloniaProperty.Register<ParkPreview, IReadOnlyList<IReadOnlyList<Point2D>>?>(nameof(Halkalar));

    public static readonly StyledProperty<IReadOnlyList<ParkOgesi>?> OgelerProperty =
        AvaloniaProperty.Register<ParkPreview, IReadOnlyList<ParkOgesi>?>(nameof(Ogeler));

    public IReadOnlyList<Point2D>? Sinir
    {
        get => GetValue(SinirProperty);
        set => SetValue(SinirProperty, value);
    }

    public IReadOnlyList<IReadOnlyList<Point2D>>? Halkalar
    {
        get => GetValue(HalkalarProperty);
        set => SetValue(HalkalarProperty, value);
    }

    public IReadOnlyList<ParkOgesi>? Ogeler
    {
        get => GetValue(OgelerProperty);
        set => SetValue(OgelerProperty, value);
    }

    static ParkPreview()
    {
        AffectsRender<ParkPreview>(SinirProperty, HalkalarProperty, OgelerProperty);
    }

    public override void Render(DrawingContext ctx)
    {
        var size = Bounds.Size;
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#0A140D")), new Rect(size));

        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        bool varMi = false;
        void Genislet(double x, double y)
        {
            varMi = true;
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        if (Sinir != null) foreach (var p in Sinir) Genislet(p.X, p.Y);
        if (Halkalar != null) foreach (var h in Halkalar) foreach (var p in h) Genislet(p.X, p.Y);
        if (Ogeler != null) foreach (var o in Ogeler) foreach (var p in OgeNoktalari(o)) Genislet(p.X, p.Y);

        if (!varMi || size.Width < 10 || size.Height < 10) return;

        double dunyaW = maxX - minX, dunyaH = maxY - minY;
        if (dunyaW < 1e-6) dunyaW = 1;
        if (dunyaH < 1e-6) dunyaH = 1;

        const double kenar = 24;
        double olcek = System.Math.Min(
            (size.Width - 2 * kenar) / dunyaW, (size.Height - 2 * kenar) / dunyaH);
        double ofsetX = (size.Width - dunyaW * olcek) / 2;
        double ofsetY = (size.Height - dunyaH * olcek) / 2;

        Point D(double wx, double wy) => new(
            (wx - minX) * olcek + ofsetX,
            size.Height - ((wy - minY) * olcek + ofsetY)); // Y yukarı

        StreamGeometry Poly(IReadOnlyList<Point2D> pts)
        {
            var geo = new StreamGeometry();
            using var g = geo.Open();
            g.BeginFigure(D(pts[0].X, pts[0].Y), true);
            for (int i = 1; i < pts.Count; i++) g.LineTo(D(pts[i].X, pts[i].Y));
            g.EndFigure(true);
            return geo;
        }

        // Kullanılabilir alan halkaları: dış = yeşil dolgu, delik (bina+setback) = mavi/gri.
        if (Halkalar != null)
            foreach (var h in Halkalar)
            {
                if (h.Count < 3) continue;
                bool delik = ClipperAdaptor.IsaretliAlan(h) < 0;
                var dolgu = delik ? Color.Parse("#55264D6E") : Color.Parse("#3327632E");
                var cizgi = delik ? Color.Parse("#5C8AC7") : Color.Parse("#4FB07A");
                ctx.DrawGeometry(new SolidColorBrush(dolgu), new Pen(new SolidColorBrush(cizgi), 1.4), Poly(h));
            }

        // Park sınırı (kalın çerçeve).
        if (Sinir != null && Sinir.Count >= 3)
            ctx.DrawGeometry(null, new Pen(new SolidColorBrush(Color.Parse("#7BD3A0")), 2.0), Poly(Sinir));

        // Sert zemin öğeleri.
        if (Ogeler != null)
            foreach (var o in Ogeler) OgeCiz(ctx, o, D, olcek);
    }

    private static void OgeCiz(DrawingContext ctx, ParkOgesi oge, System.Func<double, double, Point> D, double olcek)
    {
        switch (oge)
        {
            case YuruyusYolu y when y.Guzergah.Count >= 2:
            {
                double kalinlik = System.Math.Max(2, y.GenislikM * olcek);
                var kalem = new Pen(new SolidColorBrush(Color.Parse("#9AA7B0")), kalinlik)
                { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
                for (int i = 1; i < y.Guzergah.Count; i++)
                    ctx.DrawLine(kalem, D(y.Guzergah[i - 1].X, y.Guzergah[i - 1].Y), D(y.Guzergah[i].X, y.Guzergah[i].Y));
                break;
            }
            case DosemeAlani d: DolguPoligon(ctx, d.Poligon, "#66A1887A", "#C8A77A", D); break;
            case OturmaAlani o: DolguPoligon(ctx, o.Poligon, "#668D6E63", "#A1887F", D); break;
            case SusHavuzu s:   DolguPoligon(ctx, s.Poligon, "#772E86C1", "#5DADE2", D); break;
            case Bank b:
            {
                var c = D(b.Konum.X, b.Konum.Y);
                ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#E0C84E")),
                    new Pen(new SolidColorBrush(Color.Parse("#7A6A1E")), 1), c, 3.5, 3.5);
                break;
            }
        }
    }

    private static void DolguPoligon(DrawingContext ctx, IReadOnlyList<Point2D> pts,
        string dolgu, string cizgi, System.Func<double, double, Point> D)
    {
        if (pts.Count < 3) return;
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(D(pts[0].X, pts[0].Y), true);
            for (int i = 1; i < pts.Count; i++) g.LineTo(D(pts[i].X, pts[i].Y));
            g.EndFigure(true);
        }
        ctx.DrawGeometry(new SolidColorBrush(Color.Parse(dolgu)), new Pen(new SolidColorBrush(Color.Parse(cizgi)), 1.2), geo);
    }

    private static IEnumerable<Point2D> OgeNoktalari(ParkOgesi oge) => oge switch
    {
        Bank b        => new[] { b.Konum },
        YuruyusYolu y => y.Guzergah,
        DosemeAlani d => d.Poligon,
        OturmaAlani o => o.Poligon,
        SusHavuzu s   => s.Poligon,
        _             => new List<Point2D>()
    };
}
