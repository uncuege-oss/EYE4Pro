using System;
using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Kst.Acoustics.App.ViewModels;
using Kst.Acoustics.Core.Measurement.Analysis;

namespace Kst.Acoustics.App.Controls;

public sealed class SpectrogramControl : Control
{
    private const double PadL = 50, PadR = 14, PadT = 16, PadB = 36;

    public static readonly StyledProperty<MeasurementItemViewModel?> SelectedMeasurementProperty =
        AvaloniaProperty.Register<SpectrogramControl, MeasurementItemViewModel?>(nameof(SelectedMeasurement));

    public MeasurementItemViewModel? SelectedMeasurement
    {
        get => GetValue(SelectedMeasurementProperty);
        set => SetValue(SelectedMeasurementProperty, value);
    }

    public static readonly StyledProperty<double> MinDbProperty =
        AvaloniaProperty.Register<SpectrogramControl, double>(nameof(MinDb), -90);

    public double MinDb { get => GetValue(MinDbProperty); set => SetValue(MinDbProperty, value); }

    public static readonly StyledProperty<double> MaxDbProperty =
        AvaloniaProperty.Register<SpectrogramControl, double>(nameof(MaxDb), -10);

    public double MaxDb { get => GetValue(MaxDbProperty); set => SetValue(MaxDbProperty, value); }

    static SpectrogramControl()
    {
        AffectsRender<SpectrogramControl>(SelectedMeasurementProperty, MinDbProperty, MaxDbProperty);
    }

    private WriteableBitmap? _bitmap;
    private SpectrogramData? _cachedSource;

    public override void Render(DrawingContext ctx)
    {
        var bg = new SolidColorBrush(Color.FromRgb(0x09, 0x0F, 0x15));
        ctx.FillRectangle(bg, new Rect(Bounds.Size));

        var data = SelectedMeasurement?.Data.Spectrogram;
        if (data == null)
        {
            DrawHint(ctx, "Spektrogram henüz hesaplanmadı. Yeni ölçüm alın veya canlı kayıt aktif olduğunda spektrogram güncellenir.");
            return;
        }

        if (!ReferenceEquals(_cachedSource, data))
        {
            _bitmap = BuildBitmap(data, MinDb, MaxDb);
            _cachedSource = data;
        }

        var plot = new Rect(PadL, PadT, Bounds.Width - PadL - PadR, Bounds.Height - PadT - PadB);
        if (_bitmap != null && plot.Width > 10 && plot.Height > 10)
            ctx.DrawImage(_bitmap, new Rect(0, 0, _bitmap.PixelSize.Width, _bitmap.PixelSize.Height), plot);

        DrawAxes(ctx, plot, data);
        DrawColorScale(ctx, plot);
    }

    private static WriteableBitmap? BuildBitmap(SpectrogramData data, double minDb, double maxDb)
    {
        int width = data.FrameCount;
        int height = data.BinCount;
        if (width < 2 || height < 2) return null;

        var bmp = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96),
            PixelFormat.Bgra8888, AlphaFormat.Premul);

        int totalPixels = width * height;
        var pixels = new int[totalPixels];

        double range = maxDb - minDb;
        if (range <= 0) range = 1;

        for (int y = 0; y < height; y++)
        {
            int destY = height - 1 - y;
            for (int x = 0; x < width; x++)
            {
                double db = data.MagnitudeDb[x, y];
                double t = (db - minDb) / range;
                if (t < 0) t = 0; else if (t > 1) t = 1;
                pixels[destY * width + x] = ViridisBgra(t);
            }
        }

        using var fb = bmp.Lock();
        Marshal.Copy(pixels, 0, fb.Address, totalPixels);
        return bmp;
    }

    private static int ViridisBgra(double t)
    {
        // Beş anahtar nokta arasında doğrusal interpolasyon — Viridis benzeri.
        // (r, g, b) 0–255
        var stops = new (double t, byte r, byte g, byte b)[]
        {
            (0.00,  68,   1,  84),
            (0.25,  59,  82, 139),
            (0.50,  33, 145, 140),
            (0.75,  94, 201,  98),
            (1.00, 253, 231,  37),
        };
        for (int i = 0; i < stops.Length - 1; i++)
        {
            if (t <= stops[i + 1].t)
            {
                double f = (t - stops[i].t) / (stops[i + 1].t - stops[i].t);
                byte r = (byte)(stops[i].r + (stops[i + 1].r - stops[i].r) * f);
                byte g = (byte)(stops[i].g + (stops[i + 1].g - stops[i].g) * f);
                byte b = (byte)(stops[i].b + (stops[i + 1].b - stops[i].b) * f);
                return (255 << 24) | (r << 16) | (g << 8) | b;
            }
        }
        var last = stops[^1];
        return (255 << 24) | (last.r << 16) | (last.g << 8) | last.b;
    }

    private static void DrawAxes(DrawingContext ctx, Rect plot, SpectrogramData data)
    {
        var axisPen = new Pen(new SolidColorBrush(Color.FromRgb(0x2A, 0x3B, 0x4D)), 1);
        var fg = new SolidColorBrush(Color.FromRgb(0x7E, 0x94, 0xA7));
        ctx.DrawRectangle(null, axisPen, plot);

        // X ekseni — zaman
        var maxTime = data.TimeAxisSeconds[^1];
        int xTicks = 6;
        for (int i = 0; i <= xTicks; i++)
        {
            double f = (double)i / xTicks;
            double x = plot.X + f * plot.Width;
            ctx.DrawLine(axisPen, new Point(x, plot.Bottom), new Point(x, plot.Bottom + 4));
            var label = $"{f * maxTime:0.00} s";
            DrawTextAt(ctx, label, x - 18, plot.Bottom + 6, fg, 10);
        }

        // Y ekseni — log frekans
        var fMax = data.FrequencyAxisHz[^1];
        var fMin = Math.Max(20, data.FrequencyAxisHz[1]);
        double[] yTicks = { 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000 };
        foreach (var f in yTicks)
        {
            if (f < fMin || f > fMax) continue;
            double y = plot.Bottom - (Math.Log(f / fMin) / Math.Log(fMax / fMin)) * plot.Height;
            ctx.DrawLine(axisPen, new Point(plot.X - 4, y), new Point(plot.X, y));
            var label = f >= 1000 ? $"{f / 1000:0.#}k" : $"{f:0}";
            DrawTextAt(ctx, label, plot.X - 38, y - 6, fg, 10);
        }
    }

    private void DrawColorScale(DrawingContext ctx, Rect plot)
    {
        // Sağ üstte mini renk skalası
        var w = 12.0; var h = 100.0;
        var x = plot.Right - w - 6;
        var y = plot.Y + 6;
        var grad = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        };
        for (int i = 0; i <= 5; i++)
        {
            double t = i / 5.0;
            int bgra = ViridisBgra(t);
            byte r = (byte)((bgra >> 16) & 0xFF);
            byte g = (byte)((bgra >> 8) & 0xFF);
            byte b = (byte)(bgra & 0xFF);
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(r, g, b), t));
        }
        ctx.FillRectangle(grad, new Rect(x, y, w, h));
        var fg = new SolidColorBrush(Color.FromRgb(0x7E, 0x94, 0xA7));
        DrawTextAt(ctx, $"{MaxDb:0}", x - 18, y - 4, fg, 9);
        DrawTextAt(ctx, $"{MinDb:0}", x - 18, y + h - 6, fg, 9);
        DrawTextAt(ctx, "dB", x - 10, y + h + 4, fg, 9);
    }

    private static void DrawTextAt(DrawingContext ctx, string text, double x, double y, IBrush brush, double size)
    {
        var fmt = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            Typeface.Default, size, brush);
        ctx.DrawText(fmt, new Point(x, y));
    }

    private static void DrawHint(DrawingContext ctx, string text)
    {
        var fg = new SolidColorBrush(Color.FromRgb(0x7E, 0x94, 0xA7));
        DrawTextAt(ctx, text, 20, 20, fg, 12);
    }
}
