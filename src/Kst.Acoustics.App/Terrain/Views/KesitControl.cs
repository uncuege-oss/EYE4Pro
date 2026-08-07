using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Kst.Acoustics.Core.Terrain.Models;

namespace Kst.Acoustics.App.Terrain.Views;

/// M61 F5 — bina-arazi kotlu kesit çizen hafif kontrol. Arazi profilini, bina bloklarını
/// ve ±0.00 / çatı / bodrum kot etiketlerini gerçek (üniform) ölçekle çizer.
public class KesitControl : Control
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly Typeface Font = new("Inter, sans-serif");

    public static readonly StyledProperty<KesitModeli?> ModelProperty =
        AvaloniaProperty.Register<KesitControl, KesitModeli?>(nameof(Model));

    public KesitModeli? Model
    {
        get => GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    static KesitControl()
    {
        AffectsRender<KesitControl>(ModelProperty);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(new SolidColorBrush(Color.Parse("#EAF1FA")), bounds);

        var m = Model;
        if (m is null || m.ZeminProfili.Count < 2) return;

        const double padL = 64, padR = 24, padT = 28, padB = 40;
        double w = bounds.Width - padL - padR;
        double h = bounds.Height - padT - padB;
        if (w <= 10 || h <= 10) return;

        double wX = Math.Max(1e-6, m.MaxX - m.MinX);
        double wK = Math.Max(1e-6, m.MaxKot - m.MinKot);
        double pay = wK * 0.12 + 1.0; // kot payı
        double minK = m.MinKot - pay, maxK = m.MaxKot + pay;
        wK = maxK - minK;

        double scale = Math.Min(w / wX, h / wK); // üniform (gerçek oran)
        double Sx(double x) => padL + (x - m.MinX) * scale;
        double Sy(double k) => bounds.Height - padB - (k - minK) * scale;

        // Kot ekseni (sol) — min, ±0.00 referansları + ızgara çizgileri.
        var eksenKalem = new Pen(new SolidColorBrush(Color.Parse("#B8C6D8")), 1);
        var yaziFircasi = new SolidColorBrush(Color.Parse("#33475B"));
        for (int i = 0; i <= 4; i++)
        {
            double k = minK + wK * i / 4.0;
            double y = Sy(k);
            context.DrawLine(eksenKalem, new Point(padL, y), new Point(bounds.Width - padR, y));
            DrawText(context, $"{k:0.#}", new Point(6, y - 7), yaziFircasi, 11);
        }

        // Arazi profili — dolgu + çizgi.
        var zemin = new StreamGeometry();
        using (var gc = zemin.Open())
        {
            gc.BeginFigure(new Point(Sx(m.ZeminProfili[0].X), Sy(m.ZeminProfili[0].Y)), true);
            for (int i = 1; i < m.ZeminProfili.Count; i++)
                gc.LineTo(new Point(Sx(m.ZeminProfili[i].X), Sy(m.ZeminProfili[i].Y)));
            gc.LineTo(new Point(Sx(m.ZeminProfili[^1].X), bounds.Height - padB));
            gc.LineTo(new Point(Sx(m.ZeminProfili[0].X), bounds.Height - padB));
            gc.EndFigure(true);
        }
        context.DrawGeometry(new SolidColorBrush(Color.Parse("#C9B89A")), null, zemin);

        var zeminKalem = new Pen(new SolidColorBrush(Color.Parse("#6B5836")), 2);
        for (int i = 1; i < m.ZeminProfili.Count; i++)
            context.DrawLine(zeminKalem,
                new Point(Sx(m.ZeminProfili[i - 1].X), Sy(m.ZeminProfili[i - 1].Y)),
                new Point(Sx(m.ZeminProfili[i].X), Sy(m.ZeminProfili[i].Y)));

        // Bina blokları.
        var blokDolgu = new SolidColorBrush(Color.Parse("#5B8FB0"), 0.45);
        var blokKalem = new Pen(new SolidColorBrush(Color.Parse("#2C5870")), 1.5);
        var sifirKalem = new Pen(new SolidColorBrush(Color.Parse("#C0392B")), 1.5) { DashStyle = DashStyle.Dash };
        var etiketFircasi = new SolidColorBrush(Color.Parse("#1B2A38"));

        foreach (var b in m.Bloklar)
        {
            double x0 = Sx(b.XMin), x1 = Sx(b.XMax);
            double yTaban = Sy(b.TabanKotM), yCati = Sy(b.CatiKotM);
            var rect = new Rect(x0, yCati, Math.Max(2, x1 - x0), Math.Max(2, yTaban - yCati));
            context.DrawRectangle(blokDolgu, blokKalem, rect);

            // ±0.00 çizgisi.
            double ySifir = Sy(b.SifirKotM);
            context.DrawLine(sifirKalem, new Point(x0, ySifir), new Point(x1, ySifir));

            // Kot etiketleri.
            DrawText(context, $"{b.BlokId}", new Point(x0, yCati - 16), etiketFircasi, 12, true);
            DrawText(context, $"±0.00 = {b.SifirKotM:0.##}", new Point(x1 + 4, ySifir - 7), etiketFircasi, 10);
            DrawText(context, $"+çatı {b.CatiKotM:0.##}", new Point(x1 + 4, yCati - 7), etiketFircasi, 10);
            DrawText(context, $"bodrum {b.TabanKotM:0.##}", new Point(x1 + 4, yTaban - 7), etiketFircasi, 10);
        }
    }

    private static void DrawText(DrawingContext ctx, string text, Point p, IBrush brush, double size, bool bold = false)
    {
        var ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            bold ? new Typeface("Inter, sans-serif", weight: FontWeight.Bold) : Font, size, brush);
        ctx.DrawText(ft, p);
    }
}
