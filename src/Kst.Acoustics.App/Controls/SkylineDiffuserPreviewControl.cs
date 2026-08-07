using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Kst.Acoustics.Core.RoomModes.Diffusers;

namespace Kst.Acoustics.App.Controls;

public sealed class SkylineDiffuserPreviewControl : Control
{
    public static readonly StyledProperty<IReadOnlyList<SkylineCell>?> CellsProperty =
        AvaloniaProperty.Register<SkylineDiffuserPreviewControl, IReadOnlyList<SkylineCell>?>(nameof(Cells));

    public IReadOnlyList<SkylineCell>? Cells
    {
        get => GetValue(CellsProperty);
        set => SetValue(CellsProperty, value);
    }

    static SkylineDiffuserPreviewControl()
    {
        AffectsRender<SkylineDiffuserPreviewControl>(CellsProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        context.FillRectangle(new SolidColorBrush(Color.Parse("#0F172A")), bounds);

        var cells = Cells;
        if (cells == null || cells.Count == 0)
        {
            DrawText(context, "Skyline verisi yok", bounds.Center.X, bounds.Center.Y, 13, "#64748B", centered: true);
            return;
        }

        var order = cells.Max(c => Math.Max(c.Row, c.Column));
        var maxDepth = Math.Max(1, cells.Max(c => c.DepthMm));
        var margin = 34.0;
        var titleHeight = 28.0;
        var plot = new Rect(margin, titleHeight + margin, Math.Max(1, bounds.Width - margin * 2), Math.Max(1, bounds.Height - titleHeight - margin * 2 - 18));

        var tile = Math.Min(plot.Width / (order + 1.8), plot.Height / (order + 1.8));
        var isoX = tile * 0.82;
        var isoY = tile * 0.44;
        var heightScale = tile * 1.35 / maxDepth;
        var origin = new Point(plot.Center.X, plot.Y + tile * 1.2);

        var topPen = new Pen(new SolidColorBrush(Color.Parse("#52637A")), 1);
        var sidePen = new Pen(new SolidColorBrush(Color.Parse("#233044")), 1);
        var ordered = cells.OrderBy(c => c.Row + c.Column).ThenBy(c => c.Row).ToList();

        foreach (var cell in ordered)
        {
            var x = (cell.Column - cell.Row) * isoX + origin.X;
            var y = (cell.Column + cell.Row) * isoY + origin.Y;
            var h = cell.DepthMm * heightScale;

            var top = Diamond(x, y - h, isoX, isoY);
            var left = new[]
            {
                top[3],
                top[0],
                new Point(top[0].X, top[0].Y + h),
                new Point(top[3].X, top[3].Y + h)
            };
            var right = new[]
            {
                top[1],
                top[2],
                new Point(top[2].X, top[2].Y + h),
                new Point(top[1].X, top[1].Y + h)
            };

            var ratio = cell.DepthMm / maxDepth;
            var topBrush = new SolidColorBrush(Interpolate("#38BDF8", "#C49BE4", ratio));
            var leftBrush = new SolidColorBrush(Interpolate("#1E40AF", "#6D28D9", ratio));
            var rightBrush = new SolidColorBrush(Interpolate("#0F766E", "#7C3AED", ratio));

            DrawPolygon(context, left, leftBrush, sidePen);
            DrawPolygon(context, right, rightBrush, sidePen);
            DrawPolygon(context, top, topBrush, topPen);
        }

        DrawText(context, $"Skyline 2D 3B önizleme - {order} x {order} hücre, maks. derinlik {maxDepth:F0} mm", margin, 8, 12, "#CBD5E1");
        DrawText(context, "Kare hücreler iki eksende değişen derinliklerle çizilir.", margin, bounds.Bottom - 20, 10, "#64748B");
    }

    private static Point[] Diamond(double x, double y, double isoX, double isoY)
    {
        return new[]
        {
            new Point(x, y - isoY),
            new Point(x + isoX, y),
            new Point(x, y + isoY),
            new Point(x - isoX, y)
        };
    }

    private static Color Interpolate(string from, string to, double amount)
    {
        var a = Color.Parse(from);
        var b = Color.Parse(to);
        amount = Math.Clamp(amount, 0, 1);
        return Color.FromRgb(
            (byte)Math.Round(a.R + (b.R - a.R) * amount),
            (byte)Math.Round(a.G + (b.G - a.G) * amount),
            (byte)Math.Round(a.B + (b.B - a.B) * amount));
    }

    private static void DrawPolygon(DrawingContext context, IReadOnlyList<Point> points, IBrush brush, IPen? pen)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(points[0], isFilled: true);
            for (var i = 1; i < points.Count; i++)
                ctx.LineTo(points[i]);
            ctx.EndFigure(isClosed: true);
        }

        context.DrawGeometry(brush, pen, geometry);
    }

    private static void DrawText(DrawingContext context, string text, double x, double y, double size, string color, bool centered = false)
    {
        var formatted = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            size,
            new SolidColorBrush(Color.Parse(color)));

        context.DrawText(formatted, new Point(centered ? x - formatted.Width / 2 : x, y));
    }
}
