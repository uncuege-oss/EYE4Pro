using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Kst.Acoustics.App.ViewModels;

namespace Kst.Acoustics.App.Controls;

public sealed class StepResponsePlot : Control
{
    private const double PadL = 50, PadR = 20, PadT = 20, PadB = 40;

    public static readonly StyledProperty<MeasurementItemViewModel?> SelectedMeasurementProperty =
        AvaloniaProperty.Register<StepResponsePlot, MeasurementItemViewModel?>(nameof(SelectedMeasurement));

    public MeasurementItemViewModel? SelectedMeasurement
    {
        get => GetValue(SelectedMeasurementProperty);
        set => SetValue(SelectedMeasurementProperty, value);
    }

    static StepResponsePlot()
    {
        AffectsRender<StepResponsePlot>(SelectedMeasurementProperty);
    }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w < 100 || h < 100) return;

        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#0B1118")), new Rect(0, 0, w, h));

        var step = SelectedMeasurement?.Data.StepResponse;
        if (step == null) return;

        var plotW = w - PadL - PadR;
        var plotH = h - PadT - PadB;

        var pen = new Pen(new SolidColorBrush(Color.Parse(SelectedMeasurement!.Color)), 1.5);
        
        // Normalize
        float max = step.Max(Math.Abs);
        if (max == 0) max = 1;

        Point? lastPt = null;
        int stepSize = Math.Max(1, step.Length / (int)plotW);

        for (int i = 0; i < step.Length; i += stepSize)
        {
            double x = PadL + ((double)i / step.Length) * plotW;
            double y = PadT + plotH / 2 - (step[i] / max * (plotH / 2));

            var pt = new Point(x, y);
            if (lastPt.HasValue) ctx.DrawLine(pen, lastPt.Value, pt);
            lastPt = pt;
        }

        DrawText(ctx, "Adım Yanıtı (Step Response)", PadL + 10, PadT + 10, "#E6EEF6");
    }

    private void DrawText(DrawingContext ctx, string text, double x, double y, string color)
    {
        var ft = new FormattedText(
            text,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter"),
            11,
            new SolidColorBrush(Color.Parse(color)));
        ctx.DrawText(ft, new Point(x, y));
    }
}
