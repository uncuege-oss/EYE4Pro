using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Kst.Acoustics.App.Controls;

public sealed class RtaBarControl : Control
{
    private const double PadL = 40, PadR = 10, PadT = 10, PadB = 30;

    public static readonly StyledProperty<float[]> LevelsProperty =
        AvaloniaProperty.Register<RtaBarControl, float[]>(nameof(Levels));

    public float[] Levels
    {
        get => GetValue(LevelsProperty);
        set => SetValue(LevelsProperty, value);
    }

    static RtaBarControl()
    {
        AffectsRender<RtaBarControl>(LevelsProperty);
    }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w < 50 || h < 50) return;

        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#0B1118")), new Rect(0, 0, w, h));

        if (Levels == null || Levels.Length == 0) return;

        var plotW = w - PadL - PadR;
        var plotH = h - PadT - PadB;

        double barWidth = plotW / Levels.Length;
        var barBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(Color.Parse("#EF4444"), 0.0), // Red top
                new GradientStop(Color.Parse("#EAB308"), 0.3), // Yellow mid
                new GradientStop(Color.Parse("#22C55E"), 1.0)  // Green bottom
            }
        };

        for (int i = 0; i < Levels.Length; i++)
        {
            double db = Math.Clamp(Levels[i], -100, 0);
            double barHeight = ((db + 100) / 100.0) * plotH;
            
            var rect = new Rect(
                PadL + i * barWidth + 1,
                PadT + plotH - barHeight,
                Math.Max(1, barWidth - 2),
                barHeight);

            ctx.FillRectangle(barBrush, rect);
        }

        // Y Eksen
        var textBrush = new SolidColorBrush(Color.Parse("#536879"));
        for (int db = -100; db <= 0; db += 20)
        {
            double y = PadT + plotH - ((db + 100) / 100.0) * plotH;
            DrawText(ctx, db.ToString(), PadL - 5, y - 7, textBrush, right: true);
        }
    }

    private void DrawText(DrawingContext ctx, string text, double x, double y, IBrush brush, bool right = false)
    {
        var ft = new FormattedText(
            text,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter"),
            9,
            brush);
        double ox = right ? x - ft.Width : x;
        ctx.DrawText(ft, new Point(ox, y));
    }
}
