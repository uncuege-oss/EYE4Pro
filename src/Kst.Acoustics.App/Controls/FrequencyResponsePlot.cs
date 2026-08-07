using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Kst.Acoustics.App.ViewModels;
using Kst.Acoustics.Core.Measurement.Data;
using Kst.Acoustics.Core.Measurement.Processing;

namespace Kst.Acoustics.App.Controls;

public sealed class FrequencyResponsePlot : Control
{
    private const double PadL = 50, PadR = 20, PadT = 20, PadB = 40;

    public static readonly StyledProperty<IEnumerable<MeasurementItemViewModel>?> MeasurementsProperty =
        AvaloniaProperty.Register<FrequencyResponsePlot, IEnumerable<MeasurementItemViewModel>?>(nameof(Measurements));

    public static readonly StyledProperty<FrequencyResponseData?> PredictedResponseProperty =
        AvaloniaProperty.Register<FrequencyResponsePlot, FrequencyResponseData?>(nameof(PredictedResponse));

    public static readonly StyledProperty<TargetCurve?> TargetCurveProperty =
        AvaloniaProperty.Register<FrequencyResponsePlot, TargetCurve?>(nameof(TargetCurve));

    public IEnumerable<MeasurementItemViewModel>? Measurements
    {
        get => GetValue(MeasurementsProperty);
        set => SetValue(MeasurementsProperty, value);
    }

    public FrequencyResponseData? PredictedResponse
    {
        get => GetValue(PredictedResponseProperty);
        set => SetValue(PredictedResponseProperty, value);
    }

    public TargetCurve? TargetCurve
    {
        get => GetValue(TargetCurveProperty);
        set => SetValue(TargetCurveProperty, value);
    }

    static FrequencyResponsePlot()
    {
        AffectsRender<FrequencyResponsePlot>(MeasurementsProperty, PredictedResponseProperty, TargetCurveProperty);
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

        if (Measurements != null)
        {
            foreach (var m in Measurements.Where(x => x.IsVisible && x.Data.FrequencyResponse != null))
            {
                DrawCurve(ctx, m.Data.FrequencyResponse!, m.Color, plotW, plotH, 1.5);
            }
        }

        if (PredictedResponse != null)
        {
            DrawCurve(ctx, PredictedResponse, "#FFFFFF", plotW, plotH, 2.0, DashStyle.Dash);
        }

        if (TargetCurve != null)
        {
            DrawTargetCurve(ctx, TargetCurve, plotW, plotH);
        }
    }

    private void DrawGrid(DrawingContext ctx, double plotW, double plotH)
    {
        var gridPen = new Pen(new SolidColorBrush(Color.Parse("#1D2B38")), 0.5);
        var axisPen = new Pen(new SolidColorBrush(Color.Parse("#2A3B4D")), 1);

        // Y Eksen (dB)
        for (int db = -60; db <= 20; db += 10)
        {
            double y = PadT + plotH - ((db + 60) / 80.0) * plotH;
            ctx.DrawLine(gridPen, new Point(PadL, y), new Point(PadL + plotW, y));
            DrawText(ctx, db.ToString(), PadL - 10, y - 7, "#536879", right: true);
        }

        // X Eksen (Log Frekans: 20 Hz - 20 kHz)
        int[] majorFreqs = { 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000 };
        foreach (var f in majorFreqs)
        {
            double x = PadL + GetLogX(f, 20, 20000) * plotW;
            ctx.DrawLine(gridPen, new Point(x, PadT), new Point(x, PadT + plotH));
            string label = f >= 1000 ? $"{f / 1000}k" : f.ToString();
            DrawText(ctx, label, x, PadT + plotH + 5, "#536879", center: true);
        }

        // Ana Çerçeve
        ctx.DrawLine(axisPen, new Point(PadL, PadT), new Point(PadL, PadT + plotH));
        ctx.DrawLine(axisPen, new Point(PadL, PadT + plotH), new Point(PadL + plotW, PadT + plotH));
    }

    private void DrawCurve(DrawingContext ctx, FrequencyResponseData fr, string color, double plotW, double plotH, double thickness, IDashStyle? dash = null)
    {
        var pen = new Pen(new SolidColorBrush(Color.Parse(color)), thickness, dash);
        var geometry = new StreamGeometry();
        
        using (var context = geometry.Open())
        {
            bool first = true;
            for (int i = 0; i < fr.Frequencies.Length; i++)
            {
                float f = fr.Frequencies[i];
                if (f < 20 || f > 20000) continue;

                double x = PadL + GetLogX(f, 20, 20000) * plotW;
                double y = PadT + plotH - ((fr.Magnitudes[i] + 60) / 80.0) * plotH;
                y = Math.Clamp(y, PadT, PadT + plotH);

                var pt = new Point(x, y);
                if (first)
                {
                    context.BeginFigure(pt, false);
                    first = false;
                }
                else
                {
                    context.LineTo(pt);
                }
            }
        }

        ctx.DrawGeometry(null, pen, geometry);
    }

    private void DrawTargetCurve(DrawingContext ctx, TargetCurve tc, double plotW, double plotH)
    {
        var pen = new Pen(new SolidColorBrush(Color.Parse("#A0A0A0")), 1, DashStyle.Dot);
        Point? lastPt = null;

        for (int i = 0; i < tc.Frequencies.Length; i++)
        {
            double x = PadL + GetLogX(tc.Frequencies[i], 20, 20000) * plotW;
            double y = PadT + plotH - ((tc.Levels[i] + 60) / 80.0) * plotH;

            var pt = new Point(x, y);
            if (lastPt.HasValue) ctx.DrawLine(pen, lastPt.Value, pt);
            lastPt = pt;
        }
    }

    private double GetLogX(double f, double fMin, double fMax)
    {
        return Math.Log(f / fMin) / Math.Log(fMax / fMin);
    }

    private void DrawText(DrawingContext ctx, string text, double x, double y, string color, bool right = false, bool center = false)
    {
        var ft = new FormattedText(
            text,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter"),
            10,
            new SolidColorBrush(Color.Parse(color)));

        double ox = x;
        if (right) ox -= ft.Width;
        else if (center) ox -= ft.Width / 2;

        ctx.DrawText(ft, new Point(ox, y));
    }
}
