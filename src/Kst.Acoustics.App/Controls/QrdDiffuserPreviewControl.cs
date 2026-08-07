using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Kst.Acoustics.Core.RoomModes.Diffusers;

namespace Kst.Acoustics.App.Controls;

public sealed class QrdDiffuserPreviewControl : Control
{
    public static readonly StyledProperty<IReadOnlyList<DiffuserWell>?> WellsProperty =
        AvaloniaProperty.Register<QrdDiffuserPreviewControl, IReadOnlyList<DiffuserWell>?>(nameof(Wells));

    public IReadOnlyList<DiffuserWell>? Wells
    {
        get => GetValue(WellsProperty);
        set => SetValue(WellsProperty, value);
    }

    static QrdDiffuserPreviewControl()
    {
        AffectsRender<QrdDiffuserPreviewControl>(WellsProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        context.FillRectangle(new SolidColorBrush(Color.Parse("#0F172A")), bounds);

        var wells = Wells;
        if (wells == null || wells.Count == 0)
        {
            DrawCenteredText(context, "Panel verisi yok", bounds, "#64748B", 13);
            return;
        }

        var maxDepth = Math.Max(1, wells.Max(w => w.DepthMm));
        var margin = 28.0;
        var titleHeight = 28.0;
        var labelHeight = 26.0;
        var plot = new Rect(
            margin,
            titleHeight + margin,
            Math.Max(1, bounds.Width - margin * 2),
            Math.Max(1, bounds.Height - titleHeight - margin * 2 - labelHeight));

        var outlinePen = new Pen(new SolidColorBrush(Color.Parse("#233044")), 1);
        var edgePen = new Pen(new SolidColorBrush(Color.Parse("#52637A")), 1);
        var textBrush = new SolidColorBrush(Color.Parse("#CBD5E1"));
        var backBrush = new SolidColorBrush(Color.Parse("#3B82F6"));
        var recessBrush = new SolidColorBrush(Color.Parse("#C49BE4"));
        var sideBrush = new SolidColorBrush(Color.Parse("#7C3AED"));
        var frontBrush = new SolidColorBrush(Color.Parse("#1E293B"));

        var cellWidth = plot.Width / wells.Count;
        var panelHeight = Math.Max(70, plot.Height - 48);
        var depthScale = Math.Min(plot.Height * 0.22, Math.Max(34, cellWidth * 0.72)) / maxDepth;
        var skewX = Math.Min(28, cellWidth * 0.42);
        var skewY = Math.Min(24, panelHeight * 0.16);
        var baseY = plot.Bottom - 28;
        var topY = baseY - panelHeight;
        var startX = plot.X;

        DrawPanelShadow(context, wells.Count, cellWidth, panelHeight, maxDepth * depthScale, skewX, skewY, startX, baseY);

        for (var i = wells.Count - 1; i >= 0; i--)
        {
            var well = wells[i];
            var x0 = startX + i * cellWidth;
            var x1 = x0 + cellWidth - 2;
            var depth = well.DepthMm * depthScale;
            var dx = depth / Math.Max(maxDepth * depthScale, 1) * skewX;
            var dy = depth / Math.Max(maxDepth * depthScale, 1) * skewY;

            var front = new[]
            {
                new Point(x0, topY),
                new Point(x1, topY),
                new Point(x1, baseY),
                new Point(x0, baseY)
            };
            var recess = new[]
            {
                new Point(x0 + dx, topY - dy),
                new Point(x1 + dx, topY - dy),
                new Point(x1 + dx, baseY - dy),
                new Point(x0 + dx, baseY - dy)
            };
            var leftSide = new[]
            {
                new Point(x0, topY),
                new Point(x0 + dx, topY - dy),
                new Point(x0 + dx, baseY - dy),
                new Point(x0, baseY)
            };
            var rightSide = new[]
            {
                new Point(x1, topY),
                new Point(x1 + dx, topY - dy),
                new Point(x1 + dx, baseY - dy),
                new Point(x1, baseY)
            };
            var topSide = new[]
            {
                new Point(x0, topY),
                new Point(x1, topY),
                new Point(x1 + dx, topY - dy),
                new Point(x0 + dx, topY - dy)
            };

            DrawPolygon(context, recess, backBrush, outlinePen);
            DrawPolygon(context, leftSide, sideBrush, outlinePen);
            DrawPolygon(context, rightSide, sideBrush, outlinePen);
            DrawPolygon(context, topSide, recessBrush, edgePen);
            DrawPolygon(context, front, frontBrush, edgePen);

            var depthText = well.DepthMm <= 0.05 ? "0" : $"{well.DepthMm:F0}";
            DrawText(context, depthText, x0 + cellWidth * 0.5, topY - dy - 17, 10, "#E2E8F0", centered: true);
            DrawText(context, well.Residue.ToString(), x0 + cellWidth * 0.5, baseY + 7, 11, "#CBD5E1", centered: true);
        }

        var title = new FormattedText(
            $"QRD 3B önizleme - maksimum derinlik {maxDepth:F0} mm",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            12,
            textBrush);
        context.DrawText(title, new Point(margin, 8));

        DrawText(context, "Üst değerler kuyu derinliği (mm), alt değerler QRD kalıntısıdır.", margin, bounds.Bottom - 20, 10, "#64748B");
    }

    private static void DrawPanelShadow(
        DrawingContext context,
        int count,
        double cellWidth,
        double cellHeight,
        double depth,
        double skewX,
        double skewY,
        double startX,
        double baseY)
    {
        var x0 = startX - 8;
        var x1 = startX + count * cellWidth + skewX + 8;
        var y0 = baseY - cellHeight - skewY - 10;
        var y1 = baseY + 10;
        var shadow = new[]
        {
            new Point(x0, y0),
            new Point(x1, y0),
            new Point(x1, y1),
            new Point(x0, y1)
        };
        DrawPolygon(context, shadow, new SolidColorBrush(Color.Parse("#111827")), null);
    }

    private static void DrawPolygon(DrawingContext context, IReadOnlyList<Point> points, IBrush brush, IPen? pen)
    {
        if (points.Count == 0)
            return;

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

    private static void DrawText(
        DrawingContext context,
        string text,
        double x,
        double y,
        double size,
        string color,
        bool centered = false)
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

    private static void DrawCenteredText(DrawingContext context, string text, Rect bounds, string color, double size)
    {
        var formatted = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            size,
            new SolidColorBrush(Color.Parse(color)));

        context.DrawText(formatted, new Point(
            bounds.X + (bounds.Width - formatted.Width) / 2,
            bounds.Y + (bounds.Height - formatted.Height) / 2));
    }
}
