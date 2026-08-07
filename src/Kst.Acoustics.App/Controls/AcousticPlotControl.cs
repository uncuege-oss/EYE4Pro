using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Kst.Acoustics.Core.Simulation;

namespace Kst.Acoustics.App.Controls;

public enum AcousticPlotMode { DecayCurve, Echogram }

public sealed class AcousticPlotControl : Control
{
    private const double PadL = 46, PadR = 12, PadT = 12, PadB = 32;

    // ── Styled Properties ────────────────────────────────────────────────────

    public static readonly StyledProperty<BandParameters?> BandDataProperty =
        AvaloniaProperty.Register<AcousticPlotControl, BandParameters?>(nameof(BandData));

    public static readonly StyledProperty<IReadOnlyList<EchogramArrival>?> ArrivalsProperty =
        AvaloniaProperty.Register<AcousticPlotControl, IReadOnlyList<EchogramArrival>?>(nameof(Arrivals));

    public static readonly StyledProperty<int> BandIndexProperty =
        AvaloniaProperty.Register<AcousticPlotControl, int>(nameof(BandIndex), 3); // 500 Hz varsayılan (8-bant: index 3)

    public static readonly StyledProperty<AcousticPlotMode> PlotModeProperty =
        AvaloniaProperty.Register<AcousticPlotControl, AcousticPlotMode>(nameof(PlotMode));

    static AcousticPlotControl()
    {
        AffectsRender<AcousticPlotControl>(BandDataProperty, ArrivalsProperty, BandIndexProperty, PlotModeProperty);
    }

    public BandParameters?                    BandData  { get => GetValue(BandDataProperty);  set => SetValue(BandDataProperty,  value); }
    public IReadOnlyList<EchogramArrival>?    Arrivals  { get => GetValue(ArrivalsProperty);   set => SetValue(ArrivalsProperty,  value); }
    public int                                BandIndex { get => GetValue(BandIndexProperty);  set => SetValue(BandIndexProperty, value); }
    public AcousticPlotMode                   PlotMode  { get => GetValue(PlotModeProperty);   set => SetValue(PlotModeProperty,  value); }

    // ── Render ───────────────────────────────────────────────────────────────

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w < 10 || h < 10) return;

        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#0A1520")), new Rect(0, 0, w, h));

        var plotW = w - PadL - PadR;
        var plotH = h - PadT - PadB;
        if (plotW < 10 || plotH < 10) return;

        if (PlotMode == AcousticPlotMode.DecayCurve)
            DrawDecayCurve(ctx, plotW, plotH);
        else
            DrawEchogram(ctx, plotW, plotH);
    }

    // ── Bozunma eğrisi (Schroeder) ────────────────────────────────────────────

    private void DrawDecayCurve(DrawingContext ctx, double plotW, double plotH)
    {
        var band = BandData;
        DrawAxes(ctx, plotW, plotH, "Zaman (s)", "dB", 0, 2.5, -70, 0);

        if (band is null || band.SchroederCurve.Length < 2) return;

        var pts  = band.SchroederCurve;
        var maxT = Math.Max(pts[^1].Time * 1.05, 0.5);
        var minDb = -70.0;

        var pen = new Pen(new SolidColorBrush(Color.Parse("#40C080")), 1.5);

        var screenPts = pts
            .Select(p => ToScreen(p.Time, p.Db, maxT, minDb, 0, plotW, plotH))
            .ToArray();

        for (var i = 0; i < screenPts.Length - 1; i++)
            ctx.DrawLine(pen, screenPts[i], screenPts[i + 1]);

        if (!double.IsNaN(band.RT60))
        {
            var rx = PadL + (band.RT60 / maxT) * plotW;
            if (rx >= PadL && rx <= PadL + plotW)
            {
                var rtPen = new Pen(new SolidColorBrush(Color.Parse("#E06020")), 1, dashStyle: DashStyle.Dash);
                ctx.DrawLine(rtPen, new Point(rx, PadT), new Point(rx, PadT + plotH));
                DrawLabel(ctx, $"RT60={band.RT60:F2}s", rx + 3, PadT + 4, "#E06020");
            }
        }

        if (!double.IsNaN(band.EDT))
        {
            var ex = PadL + (band.EDT / maxT) * plotW;
            if (ex >= PadL && ex <= PadL + plotW)
            {
                var edtPen = new Pen(new SolidColorBrush(Color.Parse("#8080E0")), 1, dashStyle: DashStyle.Dash);
                ctx.DrawLine(edtPen, new Point(ex, PadT), new Point(ex, PadT + plotH));
            }
        }
    }

    // ── Echogram ─────────────────────────────────────────────────────────────

    private void DrawEchogram(DrawingContext ctx, double plotW, double plotH)
    {
        var arrivals = Arrivals;
        if (arrivals is null || arrivals.Count == 0)
        {
            DrawAxes(ctx, plotW, plotH, "Zaman (s)", "Enerji (dB)", 0, 1, -60, 0);
            return;
        }

        var bi    = BandIndex;
        var maxT  = arrivals.Max(a => a.TimeSeconds) * 1.1;
        var minDb = -60.0;

        DrawAxes(ctx, plotW, plotH, "Zaman (s)", "Enerji (dB)", 0, maxT, minDb, 0);

        var maxE = arrivals.Max(a => BandE(a, bi));
        if (maxE < 1e-30) return;

        var barPen = new Pen(new SolidColorBrush(Color.Parse("#2080C8")), 1);

        foreach (var arr in arrivals)
        {
            var e = BandE(arr, bi);
            if (e < 1e-30) continue;

            var db = 10 * Math.Log10(e / maxE);
            if (db < minDb) continue;

            var sx  = PadL + (arr.TimeSeconds / maxT) * plotW;
            var sy  = ToScreen(0, db, maxT, minDb, 0, plotW, plotH).Y;
            var sy0 = PadT + plotH;

            ctx.DrawLine(barPen, new Point(sx, sy0), new Point(sx, sy));
        }
    }

    // ── Eksenler ─────────────────────────────────────────────────────────────

    private void DrawAxes(DrawingContext ctx,
        double plotW, double plotH,
        string xLabel, string yLabel,
        double xMin, double xMax, double yMin, double yMax)
    {
        var axisPen = new Pen(new SolidColorBrush(Color.Parse("#2A4060")), 1);
        var gridPen = new Pen(new SolidColorBrush(Color.Parse("#1A2A3A")), 1);

        ctx.DrawLine(axisPen, new Point(PadL, PadT), new Point(PadL, PadT + plotH));
        ctx.DrawLine(axisPen, new Point(PadL, PadT + plotH), new Point(PadL + plotW, PadT + plotH));

        for (var db = yMin; db <= yMax; db += 10)
        {
            var sy = PadT + plotH - ((db - yMin) / (yMax - yMin)) * plotH;
            ctx.DrawLine(gridPen, new Point(PadL, sy), new Point(PadL + plotW, sy));
            DrawLabel(ctx, db.ToString("F0"), PadL - 6, sy - 6, "#3A6080", right: true);
        }

        var xStep = xMax <= 1 ? 0.2 : xMax <= 3 ? 0.5 : 1.0;
        for (var t = 0.0; t <= xMax + 1e-6; t += xStep)
        {
            var sx = PadL + (t / xMax) * plotW;
            ctx.DrawLine(gridPen, new Point(sx, PadT), new Point(sx, PadT + plotH));
            DrawLabel(ctx, t.ToString("F1"), sx - 8, PadT + plotH + 4, "#3A6080");
        }

        DrawLabel(ctx, xLabel, PadL + plotW / 2 - 20, PadT + plotH + 18, "#5080A0");
        DrawLabel(ctx, yLabel, 2, PadT + plotH / 2 - 10, "#5080A0");
    }

    private static Point ToScreen(double t, double db, double maxT, double minDb, double maxDb,
                                  double plotW, double plotH)
    {
        var sx = PadL + (t / maxT) * plotW;
        var sy = PadT + plotH - ((db - minDb) / (maxDb - minDb)) * plotH;
        return new Point(sx, sy);
    }

    private void DrawLabel(DrawingContext ctx, string text, double x, double y,
                           string color, bool right = false)
    {
        var ft = new FormattedText(
            text,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter"),
            9,
            new SolidColorBrush(Color.Parse(color)));

        var ox = right ? x - ft.Width : x;
        ctx.DrawText(ft, new Point(ox, y));
    }

    private static double BandE(EchogramArrival a, int bi) => bi switch
    {
        0 => a.E63,  1 => a.E125, 2 => a.E250, 3 => a.E500,
        4 => a.E1k,  5 => a.E2k,  6 => a.E4k,  7 => a.E8k,
        _ => 0
    };
}
