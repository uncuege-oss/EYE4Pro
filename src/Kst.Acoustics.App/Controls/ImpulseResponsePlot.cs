using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Kst.Acoustics.App.ViewModels;

namespace Kst.Acoustics.App.Controls;

public sealed class ImpulseResponsePlot : Control
{
    private const double PadL = 50, PadR = 20, PadT = 20, PadB = 40;

    public static readonly StyledProperty<MeasurementItemViewModel?> SelectedMeasurementProperty =
        AvaloniaProperty.Register<ImpulseResponsePlot, MeasurementItemViewModel?>(nameof(SelectedMeasurement));

    public MeasurementItemViewModel? SelectedMeasurement
    {
        get => GetValue(SelectedMeasurementProperty);
        set => SetValue(SelectedMeasurementProperty, value);
    }

    static ImpulseResponsePlot()
    {
        AffectsRender<ImpulseResponsePlot>(SelectedMeasurementProperty);
    }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w < 100 || h < 100) return;

        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#0B1118")), new Rect(0, 0, w, h));

        var plotW = w - PadL - PadR;
        var plotH = h - PadT - PadB;

        DrawGrid(ctx, plotW, plotH);

        if (SelectedMeasurement?.Data.ImpulseResponse == null) return;

        DrawIR(ctx, SelectedMeasurement.Data.ImpulseResponse, plotW, plotH);
    }

    private void DrawGrid(DrawingContext ctx, double plotW, double plotH)
    {
        var gridPen = new Pen(new SolidColorBrush(Color.Parse("#1D2B38")), 0.5);
        var axisPen = new Pen(new SolidColorBrush(Color.Parse("#2A3B4D")), 1);

        // Y Eksen (Genlik -1.0 to 1.0)
        for (double a = -1.0; a <= 1.0; a += 0.5)
        {
            double y = PadT + plotH / 2 - (a * (plotH / 2));
            ctx.DrawLine(gridPen, new Point(PadL, y), new Point(PadL + plotW, y));
            DrawText(ctx, a.ToString("F1"), PadL - 10, y - 7, "#536879", right: true);
        }

        // Ana Çerçeve
        ctx.DrawLine(axisPen, new Point(PadL, PadT), new Point(PadL, PadT + plotH));
        ctx.DrawLine(axisPen, new Point(PadL, PadT + plotH / 2), new Point(PadL + plotW, PadT + plotH / 2));
    }

    private void DrawIR(DrawingContext ctx, float[] ir, double plotW, double plotH)
    {
        var pen = new Pen(new SolidColorBrush(Color.Parse(SelectedMeasurement!.Color)), 1);
        
        // IR genelde çok uzundur (48k+ örnek), render için örnekleyelim
        int step = Math.Max(1, ir.Length / (int)plotW);
        Point? lastPt = null;

        for (int i = 0; i < ir.Length; i += step)
        {
            double x = PadL + ((double)i / ir.Length) * plotW;
            double y = PadT + plotH / 2 - (ir[i] * (plotH / 2));

            var pt = new Point(x, y);
            if (lastPt.HasValue)
            {
                ctx.DrawLine(pen, lastPt.Value, pt);
            }
            lastPt = pt;
        }
    }

    private void DrawText(DrawingContext ctx, string text, double x, double y, string color, bool right = false)
    {
        var ft = new FormattedText(
            text,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter"),
            10,
            new SolidColorBrush(Color.Parse(color)));

        double ox = right ? x - ft.Width : x;
        ctx.DrawText(ft, new Point(ox, y));
    }
}
