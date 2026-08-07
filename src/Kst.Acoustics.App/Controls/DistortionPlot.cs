using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Kst.Acoustics.App.ViewModels;

namespace Kst.Acoustics.App.Controls;

public sealed class DistortionPlot : Control
{
    private const double PadL = 50, PadR = 20, PadT = 20, PadB = 40;

    public static readonly StyledProperty<MeasurementItemViewModel?> SelectedMeasurementProperty =
        AvaloniaProperty.Register<DistortionPlot, MeasurementItemViewModel?>(nameof(SelectedMeasurement));

    public MeasurementItemViewModel? SelectedMeasurement
    {
        get => GetValue(SelectedMeasurementProperty);
        set => SetValue(SelectedMeasurementProperty, value);
    }

    static DistortionPlot()
    {
        AffectsRender<DistortionPlot>(SelectedMeasurementProperty);
    }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w < 100 || h < 100) return;

        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#0B1118")), new Rect(0, 0, w, h));

        var distortion = SelectedMeasurement?.Data.Distortion;
        if (distortion == null || distortion.HarmonicsDb.Length == 0)
        {
            DrawCenteredText(ctx, "Bozulma verisi yok. THD icin sabit ton veya multitone olcumu gerekir.", w, h);
            return;
        }

        var plotW = w - PadL - PadR;
        var plotH = h - PadT - PadB;
        var axisBrush = new SolidColorBrush(Color.Parse("#2A3B4D"));
        var textBrush = new SolidColorBrush(Color.Parse("#536879"));
        ctx.DrawLine(new Pen(axisBrush, 1), new Point(PadL, PadT), new Point(PadL, PadT + plotH));
        ctx.DrawLine(new Pen(axisBrush, 1), new Point(PadL, PadT + plotH), new Point(PadL + plotW, PadT + plotH));

        var title = new FormattedText(
            $"THD: {distortion.THD:F2}%",
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            16,
            new SolidColorBrush(Color.Parse("#4EA8DE")));
        ctx.DrawText(title, new Point(PadL, 8));

        var values = distortion.HarmonicsDb.Take(5).ToArray();
        var barW = plotW / Math.Max(1, values.Length) * 0.62;
        for (int i = 0; i < values.Length; i++)
        {
            var db = Math.Clamp(values[i], -100, 0);
            var barH = ((db + 100) / 100.0) * plotH;
            var x = PadL + (i + 0.2) * (plotW / values.Length);
            var y = PadT + plotH - barH;
            var brush = new SolidColorBrush(i == 1 ? Color.Parse("#EAB308") : Color.Parse("#22C55E"));
            ctx.FillRectangle(brush, new Rect(x, y, barW, barH));

            DrawText(ctx, $"H{i + 2}", x + barW / 2, PadT + plotH + 8, textBrush, center: true);
            DrawText(ctx, $"{values[i]:F1}", x + barW / 2, y - 16, textBrush, center: true);
        }
    }

    private static void DrawCenteredText(DrawingContext ctx, string text, double w, double h)
    {
        var textBrush = new SolidColorBrush(Color.Parse("#536879"));
        var ft = new FormattedText(
            text,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter"),
            14,
            textBrush);

        ctx.DrawText(ft, new Point(w / 2 - ft.Width / 2, h / 2 - ft.Height / 2));
    }

    private static void DrawText(DrawingContext ctx, string text, double x, double y, IBrush brush, bool center = false)
    {
        var ft = new FormattedText(
            text,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter"),
            10,
            brush);
        ctx.DrawText(ft, new Point(center ? x - ft.Width / 2 : x, y));
    }
}
