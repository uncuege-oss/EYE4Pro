using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Kst.Acoustics.Core.Salon.Services;

namespace Kst.Acoustics.App.Salon.Views;

/// <summary>M51 canlı boyuna/enine kesit önizlemesi.</summary>
public sealed class SalonKesitCanvas : Control
{
    public static readonly StyledProperty<SalonKesitSonucu?> KesitProperty =
        AvaloniaProperty.Register<SalonKesitCanvas, SalonKesitSonucu?>(nameof(Kesit));

    public SalonKesitSonucu? Kesit { get => GetValue(KesitProperty); set => SetValue(KesitProperty, value); }

    static SalonKesitCanvas() => AffectsRender<SalonKesitCanvas>(KesitProperty);

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#081019")), new Rect(Bounds.Size));
        var k = Kesit;
        if (k is null || k.Cizgiler.Count == 0 || Bounds.Width < 40 || Bounds.Height < 40) return;
        const double l = 52, r = 20, t = 24, b = 34;
        double minX = k.Cizgiler.SelectMany(x => x.Noktalar).Min(p => p.X);
        double maxX = k.Cizgiler.SelectMany(x => x.Noktalar).Max(p => p.X);
        double minZ = Math.Min(0, k.Cizgiler.SelectMany(x => x.Noktalar).Min(p => p.Y));
        double maxZ = Math.Max(1, k.Cizgiler.SelectMany(x => x.Noktalar).Max(p => p.Y));
        double sx = (Bounds.Width - l - r) / Math.Max(1e-6, maxX - minX);
        double sy = (Bounds.Height - t - b) / Math.Max(1e-6, maxZ - minZ);
        double scale = Math.Min(sx, sy);
        Point D(double x, double z) => new(l + (x - minX) * scale, Bounds.Height - b - (z - minZ) * scale);

        var gridPen = new Pen(new SolidColorBrush(Color.Parse("#193044")), 1);
        var textBrush = new SolidColorBrush(Color.Parse("#7892A8"));
        for (int i = 0; i <= 5; i++)
        {
            double z = minZ + (maxZ - minZ) * i / 5;
            double y = D(minX, z).Y;
            ctx.DrawLine(gridPen, new Point(l, y), new Point(Bounds.Width - r, y));
            Yazi(ctx, $"{z:0.#} m", new Point(4, y - 7), textBrush, 10);
        }

        foreach (var c in k.Cizgiler)
        {
            if (c.Noktalar.Count < 2) continue;
            var pen = new Pen(new SolidColorBrush(Renk(c.Tip)), c.Tip is SalonKesitCizgiTipi.Zemin or SalonKesitCizgiTipi.Tavan ? 2.2 : 1.6);
            for (int i = 1; i < c.Noktalar.Count; i++)
                ctx.DrawLine(pen, D(c.Noktalar[i - 1].X, c.Noktalar[i - 1].Y), D(c.Noktalar[i].X, c.Noktalar[i].Y));
            if (c.Kapali)
                ctx.DrawLine(pen, D(c.Noktalar[^1].X, c.Noktalar[^1].Y), D(c.Noktalar[0].X, c.Noktalar[0].Y));
        }
        Yazi(ctx, k.Boyuna ? $"Boyuna kesit  x={k.KesitKotu:0.##} m" : $"Enine kesit  y={k.KesitKotu:0.##} m",
            new Point(l, 5), new SolidColorBrush(Color.Parse("#D8E6F0")), 12);
    }

    private static Color Renk(SalonKesitCizgiTipi t) => Color.Parse(t switch
    {
        SalonKesitCizgiTipi.Zemin => "#9DAAB5",
        SalonKesitCizgiTipi.Tavan => "#B993E8",
        SalonKesitCizgiTipi.Sahne => "#DB9A45",
        SalonKesitCizgiTipi.Seyirci => "#62C87A",
        SalonKesitCizgiTipi.Balkon => "#CF78B1",
        SalonKesitCizgiTipi.Orkestra => "#4BC1CB",
        _ => "#F0C64E"
    });

    private static void Yazi(DrawingContext ctx, string text, Point p, IBrush brush, double size)
        => ctx.DrawText(new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Inter, sans-serif"), size, brush), p);
}
