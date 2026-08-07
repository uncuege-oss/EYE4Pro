using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.SoundInsulation;

namespace Kst.Acoustics.App.Controls;

public sealed class SoundInsulationPlotControl : Control
{
    private const double PadL = 50, PadR = 20, PadT = 20, PadB = 40;

    public static readonly StyledProperty<SoundInsulationResult?> ResultProperty =
        AvaloniaProperty.Register<SoundInsulationPlotControl, SoundInsulationResult?>(nameof(Result));

    public static readonly StyledProperty<SoundInsulationResult?> OverlayResultProperty =
        AvaloniaProperty.Register<SoundInsulationPlotControl, SoundInsulationResult?>(nameof(OverlayResult));

    public static readonly StyledProperty<string> OverlayLabelProperty =
        AvaloniaProperty.Register<SoundInsulationPlotControl, string>(nameof(OverlayLabel), "TMM");

    static SoundInsulationPlotControl()
    {
        AffectsRender<SoundInsulationPlotControl>(ResultProperty, OverlayResultProperty, OverlayLabelProperty);
    }

    public SoundInsulationResult? Result
    {
        get => GetValue(ResultProperty);
        set => SetValue(ResultProperty, value);
    }

    public SoundInsulationResult? OverlayResult
    {
        get => GetValue(OverlayResultProperty);
        set => SetValue(OverlayResultProperty, value);
    }

    public string OverlayLabel
    {
        get => GetValue(OverlayLabelProperty);
        set => SetValue(OverlayLabelProperty, value);
    }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w < 100 || h < 100) return;

        // Background
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#111111")), new Rect(0, 0, w, h));

        var plotW = w - PadL - PadR;
        var plotH = h - PadT - PadB;

        var result = Result;
        if (result == null || result.TransmissionLossValues.Count == 0)
        {
            DrawAxes(ctx, plotW, plotH, 0, 80);
            return;
        }

        var tlData = result.TransmissionLossValues;
        double maxTl = Math.Max(80, tlData.Values.Max() + 10);
        DrawAxes(ctx, plotW, plotH, 0, maxTl);

        // Draw TL Curve
        var bands = SoundInsulationConstants.ThirdOctaveBands;
        var points = new List<Point>();
        for (int i = 0; i < bands.Count; i++)
        {
            if (tlData.TryGetValue(bands[i], out double tl))
            {
                double x = XForFrequency(bands[i], plotW);
                double y = PadT + plotH - (tl / maxTl) * plotH;
                points.Add(new Point(x, Math.Clamp(y, PadT, PadT + plotH)));
            }
        }

        if (points.Count > 1)
        {
            var pen = new Pen(new SolidColorBrush(Color.Parse("#00FFFF")), 2);
            for (int i = 0; i < points.Count - 1; i++)
                ctx.DrawLine(pen, points[i], points[i + 1]);

            // Draw points
            var dotBrush = new SolidColorBrush(Color.Parse("#00FFFF"));
            foreach (var p in points)
                ctx.DrawEllipse(dotBrush, null, p, 3, 3);
        }

        // Overlay seri (TMM karşılaştırma)
        var overlay = OverlayResult;
        if (overlay != null && overlay.TransmissionLossValues.Count > 0)
        {
            var olPoints = new List<Point>();
            for (int i = 0; i < bands.Count; i++)
            {
                if (overlay.TransmissionLossValues.TryGetValue(bands[i], out double tl))
                {
                    double x = XForFrequency(bands[i], plotW);
                    double y = PadT + plotH - (tl / maxTl) * plotH;
                    olPoints.Add(new Point(x, Math.Clamp(y, PadT, PadT + plotH)));
                }
            }
            if (olPoints.Count > 1)
            {
                var olPen = new Pen(new SolidColorBrush(Color.Parse("#44E58A")), 1.8, dashStyle: new DashStyle(new double[] { 4, 3 }, 0));
                for (int i = 0; i < olPoints.Count - 1; i++)
                    ctx.DrawLine(olPen, olPoints[i], olPoints[i + 1]);
                DrawLabel(ctx, OverlayLabel, PadL + plotW - 60, PadT + 6, "#44E58A");
            }
        }

        // Draw Rw Reference Curve (if exists)
        if (result.RwRating != null)
        {
            var rwPen = new Pen(new SolidColorBrush(Color.Parse("#FFCC00")), 1.5, dashStyle: DashStyle.Dash);
            var rwBands = new[] { 100, 125, 160, 200, 250, 315, 400, 500, 630, 800, 1000, 1250, 1600, 2000, 2500, 3150 };
            var rwRel = new[] { -19, -16, -13, -10, -7, -4, -1, 0, 1, 2, 3, 4, 4, 4, 4, 4 };
            
            var rwPoints = new List<Point>();
            for (int i = 0; i < rwBands.Length; i++)
            {
                if (bands.Contains(rwBands[i]))
                {
                    double x = XForFrequency(rwBands[i], plotW);
                    double y = PadT + plotH - ((result.RwRating.Rw + rwRel[i]) / maxTl) * plotH;
                    rwPoints.Add(new Point(x, Math.Clamp(y, PadT, PadT + plotH)));
                }
            }

            for (int i = 0; i < rwPoints.Count - 1; i++)
                ctx.DrawLine(rwPen, rwPoints[i], rwPoints[i + 1]);
        }

        DrawMarkers(ctx, result, plotW, plotH);
    }

    private void DrawMarkers(DrawingContext ctx, SoundInsulationResult result, double plotW, double plotH)
    {
        var markerPen = new Pen(new SolidColorBrush(Color.Parse("#FF6666")), 1, dashStyle: DashStyle.Dash);
        foreach (var marker in result.CriticalFrequencies?.Values ?? Enumerable.Empty<double>())
        {
            DrawFrequencyMarker(ctx, marker, "fc", markerPen, plotW, plotH);
        }

        if (result.MassAirMassResonance > 0)
        {
            DrawFrequencyMarker(ctx, result.MassAirMassResonance, "f0", new Pen(new SolidColorBrush(Color.Parse("#FFAA44")), 1.2, dashStyle: DashStyle.Dash), plotW, plotH);
        }
    }

    private void DrawFrequencyMarker(DrawingContext ctx, double frequency, string label, Pen pen, double plotW, double plotH)
    {
        var bands = SoundInsulationConstants.ThirdOctaveBands;
        if (frequency < bands[0] || frequency > bands[^1]) return;

        double x = XForFrequency(frequency, plotW);
        ctx.DrawLine(pen, new Point(x, PadT), new Point(x, PadT + plotH));
        DrawLabel(ctx, label, x + 3, PadT + 3, "#FFCCAA");
    }

    private void DrawAxes(DrawingContext ctx, double plotW, double plotH, double yMin, double yMax)
    {
        var axisPen = new Pen(new SolidColorBrush(Color.Parse("#333333")), 1);
        var gridPen = new Pen(new SolidColorBrush(Color.Parse("#222222")), 1);

        // Y Axis Grid
        for (double val = 0; val <= yMax; val += 10)
        {
            double sy = PadT + plotH - (val / yMax) * plotH;
            ctx.DrawLine(gridPen, new Point(PadL, sy), new Point(PadL + plotW, sy));
            DrawLabel(ctx, val.ToString(), PadL - 10, sy - 7, "#BBBBBB", true);
        }

        // X Axis Grid (Frequencies)
        var bands = SoundInsulationConstants.ThirdOctaveBands;
        for (int i = 0; i < bands.Count; i++)
        {
            double sx = XForFrequency(bands[i], plotW);
            ctx.DrawLine(gridPen, new Point(sx, PadT), new Point(sx, PadT + plotH));
            
            // Show only main labels to avoid clutter
            int f = bands[i];
            if (f == 125 || f == 250 || f == 500 || f == 1000 || f == 2000 || f == 4000)
            {
                string label = f >= 1000 ? $"{f / 1000}k" : f.ToString();
                DrawLabel(ctx, label, sx - 10, PadT + plotH + 5, "#CCCCCC");
            }
        }

        // Main Axis Lines
        ctx.DrawLine(axisPen, new Point(PadL, PadT), new Point(PadL, PadT + plotH));
        ctx.DrawLine(axisPen, new Point(PadL, PadT + plotH), new Point(PadL + plotW, PadT + plotH));
    }

    private void DrawLabel(DrawingContext ctx, string text, double x, double y, string color, bool right = false)
    {
        var ft = new FormattedText(
            text,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Arial, sans-serif"),
            10,
            new SolidColorBrush(Color.Parse(color)));
        ctx.DrawText(ft, new Point(right ? x - ft.Width : x, y));
    }

    private static double XForFrequency(double frequency, double plotW)
    {
        var bands = SoundInsulationConstants.ThirdOctaveBands;
        double min = bands[0];
        double max = bands[^1];
        double t = Math.Log(frequency / min) / Math.Log(max / min);
        return PadL + Math.Clamp(t, 0.0, 1.0) * plotW;
    }
}
