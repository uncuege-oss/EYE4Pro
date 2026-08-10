using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Kst.Acoustics.Core.Salon.Services;

namespace Kst.Acoustics.App.Salon.Views;

/// <summary>Gerçek koltuk planını C-değeri görünürlük puanıyla renklendirir.</summary>
public sealed class SalonGorusHaritasiCanvas : Control
{
    public static readonly StyledProperty<SalonKoltukYerlesimi?> YerlesimProperty =
        AvaloniaProperty.Register<SalonGorusHaritasiCanvas, SalonKoltukYerlesimi?>(nameof(Yerlesim));
    public static readonly StyledProperty<SalonGorusRaporu?> RaporProperty =
        AvaloniaProperty.Register<SalonGorusHaritasiCanvas, SalonGorusRaporu?>(nameof(Rapor));

    public SalonKoltukYerlesimi? Yerlesim { get => GetValue(YerlesimProperty); set => SetValue(YerlesimProperty, value); }
    public SalonGorusRaporu? Rapor { get => GetValue(RaporProperty); set => SetValue(RaporProperty, value); }

    static SalonGorusHaritasiCanvas() => AffectsRender<SalonGorusHaritasiCanvas>(YerlesimProperty, RaporProperty);

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#081019")), new Rect(Bounds.Size));
        var y = Yerlesim;
        if (y is null || y.Koltuklar.Count == 0 || Bounds.Width < 40 || Bounds.Height < 40) return;
        const double pad = 28;
        double minX = y.Koltuklar.Min(k => k.Konum.X), maxX = y.Koltuklar.Max(k => k.Konum.X);
        double minY = y.Koltuklar.Min(k => k.Konum.Y), maxY = y.Koltuklar.Max(k => k.Konum.Y);
        double scale = Math.Min((Bounds.Width - 2 * pad) / Math.Max(1, maxX - minX),
            (Bounds.Height - 2 * pad) / Math.Max(1, maxY - minY));
        Point D(double x, double yy) => new((Bounds.Width - (maxX - minX) * scale) / 2 + (x - minX) * scale,
            Bounds.Height - ((Bounds.Height - (maxY - minY) * scale) / 2 + (yy - minY) * scale));
        var sonuc = Rapor?.Koltuklar.ToDictionary(x => x.KoltukId, StringComparer.Ordinal) ?? [];

        foreach (var row in y.Siralar)
        {
            if (row.Koltuklar.Count < 2) continue;
            var a = row.Koltuklar.MinBy(x => x.Konum.X)!; var b = row.Koltuklar.MaxBy(x => x.Konum.X)!;
            ctx.DrawLine(new Pen(new SolidColorBrush(Color.Parse("#183044")), 1),
                D(a.Konum.X, a.Konum.Y), D(b.Konum.X, b.Konum.Y));
        }
        foreach (var k in y.Koltuklar)
        {
            var p = D(k.Konum.X, k.Konum.Y);
            Color c = k.TekerlekliSandalyeYeri ? Color.Parse("#4CAAE8")
                : sonuc.TryGetValue(k.Id, out var g) ? IsiRengi(g.GorunurlukPuani) : Color.Parse("#8093A2");
            if (k.TekerlekliSandalyeYeri)
                ctx.DrawRectangle(new SolidColorBrush(c), null, new Rect(p.X - 3.5, p.Y - 3.5, 7, 7));
            else
                ctx.DrawEllipse(new SolidColorBrush(c), null, p, 3, 3);
        }
        if (Rapor is { } r)
            Yazi(ctx, $"C ort. {r.OrtalamaCMm:0} mm · min {r.MinimumCMm:0} mm · kabul %{r.IyiGorusYuzdesi:0}",
                new Point(10, 7), new SolidColorBrush(Color.Parse("#D8E6F0")), 11);
    }

    private static Color IsiRengi(double p)
    {
        p = Math.Clamp(p, 0, 1);
        byte r = (byte)Math.Round(230 * (1 - p) + 65 * p);
        byte g = (byte)Math.Round(65 * (1 - p) + 205 * p);
        byte b = (byte)Math.Round(55 * (1 - p) + 95 * p);
        return Color.FromRgb(r, g, b);
    }

    private static void Yazi(DrawingContext ctx, string text, Point p, IBrush brush, double size)
        => ctx.DrawText(new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Inter, sans-serif"), size, brush), p);
}
