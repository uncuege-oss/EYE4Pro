using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Kst.Acoustics.App.ViewModels;
using Kst.Acoustics.Core.Measurement.Analysis;

namespace Kst.Acoustics.App.Controls;

public sealed class WaterfallPlot : Control
{
    private const double PadL = 50, PadR = 50, PadT = 50, PadB = 50;

    public static readonly StyledProperty<MeasurementItemViewModel?> SelectedMeasurementProperty =
        AvaloniaProperty.Register<WaterfallPlot, MeasurementItemViewModel?>(nameof(SelectedMeasurement));

    public MeasurementItemViewModel? SelectedMeasurement
    {
        get => GetValue(SelectedMeasurementProperty);
        set => SetValue(SelectedMeasurementProperty, value);
    }

    static WaterfallPlot()
    {
        AffectsRender<WaterfallPlot>(SelectedMeasurementProperty);
    }

    private readonly IBrush _backgroundBrush = new SolidColorBrush(Color.Parse("#0B1118"));
    private readonly IBrush _sliceFillBrush = new SolidColorBrush(Color.FromRgb(11, 17, 24), 0.8);
    private readonly Dictionary<Color, IPen> _penCache = new();

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w < 100 || h < 100) return;

        ctx.FillRectangle(_backgroundBrush, new Rect(0, 0, w, h));

        var waterfall = SelectedMeasurement?.Data.Waterfall;
        if (waterfall == null) return;

        var plotW = w - PadL - PadR;
        var plotH = h - PadT - PadB;

        double offsetX = 40;
        double offsetY = 40;

        var baseColor = Color.Parse(SelectedMeasurement!.Color);

        for (int s = waterfall.Data.Length - 1; s >= 0; s--)
        {
            double sliceProgress = (double)s / waterfall.Data.Length;
            double sx = PadL + (1.0 - sliceProgress) * offsetX;
            double sy = PadT + sliceProgress * offsetY;

            var sliceColor = Color.FromRgb(
                (byte)(baseColor.R * (1.0 - sliceProgress * 0.5)),
                (byte)(baseColor.G * (1.0 - sliceProgress * 0.5)),
                (byte)(baseColor.B * (1.0 - sliceProgress * 0.5)));
            
            if (!_penCache.TryGetValue(sliceColor, out var pen))
            {
                pen = new Pen(new SolidColorBrush(sliceColor), 1);
                _penCache[sliceColor] = pen;
            }

            float[] magnitudes = waterfall.Data[s];
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                bool first = true;
                Point lastPt = default;

                for (int i = 0; i < waterfall.Frequencies.Length; i++)
                {
                    float f = waterfall.Frequencies[i];
                    if (f < 20 || f > 20000) continue;

                    double x = sx + GetLogX(f, 20, 20000) * (plotW - offsetX);
                    double db = magnitudes[i];
                    double y = sy + plotH - offsetY - ((db + 60) / 80.0) * (plotH - offsetY);
                    
                    var pt = new Point(x, y);
                    if (first)
                    {
                        context.BeginFigure(new Point(x, sy + plotH - offsetY), true);
                        context.LineTo(pt);
                        first = false;
                    }
                    else
                    {
                        context.LineTo(pt);
                    }
                    lastPt = pt;
                }

                if (!first)
                {
                    context.LineTo(new Point(lastPt.X, sy + plotH - offsetY));
                    context.EndFigure(true);
                    ctx.DrawGeometry(_sliceFillBrush, pen, geometry);
                }
            }
        }

        // Eksen etiketleri (Basit)
        DrawText(ctx, "Frekans (Hz)", PadL + plotW / 2, PadT + plotH + 5, "#536879", center: true);
        DrawText(ctx, "Zaman (ms)", PadL + plotW - 20, PadT + 10, "#536879");
    }

    private double GetLogX(double f, double fMin, double fMax)
    {
        return Math.Log(f / fMin) / Math.Log(fMax / fMin);
    }

    private void DrawText(DrawingContext ctx, string text, double x, double y, string color, bool center = false)
    {
        var ft = new FormattedText(
            text,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter"),
            10,
            new SolidColorBrush(Color.Parse(color)));

        double ox = center ? x - ft.Width / 2 : x;
        ctx.DrawText(ft, new Point(ox, y));
    }
}
