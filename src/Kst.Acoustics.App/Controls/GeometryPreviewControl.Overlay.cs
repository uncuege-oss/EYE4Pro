using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Kst.Acoustics.App.ViewModels;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Simulation;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Kst.Acoustics.App.Controls;

public sealed partial class GeometryPreviewControl
{
    private void DrawOdaEklePreview(DrawingContext context, SceneModel? scene)
    {
        if (OdaEkleModu == 0 || scene is null || Projection != GeometryProjectionMode.Top2D) return;

        var pts = OdaEkleNoktalar;
        if (pts is null || pts.Count == 0) return;

        var center = Bounds.Center;
        var setup  = scene.Setup;
        Point ToScreen(Point3D p)
        {
            var proj  = ProjectPoint(p);
            var baseP = new Point(
                setup.OffsetX + ((proj.X - setup.MinX) * setup.BaseScale),
                setup.OffsetY + ((setup.MaxY - proj.Y) * setup.BaseScale));
            return center + _pan + ((baseP - center) * _zoom);
        }

        var linePen   = new Pen(new SolidColorBrush(Color.Parse("#00E8B8")), 2.0);
        var dashPen   = new Pen(new SolidColorBrush(Color.Parse("#00E8B8"), 0.55), 1.5,
                            new DashStyle([6, 4], 0));
        var closePen  = new Pen(new SolidColorBrush(Color.Parse("#00E8B8"), 0.35), 1.2,
                            new DashStyle([3, 5], 0));
        var fillBrush = new SolidColorBrush(Color.Parse("#00C8A0"), 0.10);
        var dotFill   = new SolidColorBrush(Color.Parse("#00FFD0"));
        var dotStroke = new Pen(new SolidColorBrush(Colors.White), 1.5);

        var screenPts = pts.Select(ToScreen).ToList();

        // Dolgu (≥3 nokta varsa)
        if (screenPts.Count >= 3)
        {
            var geom = new StreamGeometry();
            using (var gc = geom.Open())
            {
                gc.BeginFigure(screenPts[0], true);
                for (int i = 1; i < screenPts.Count; i++) gc.LineTo(screenPts[i]);
                gc.EndFigure(true);
            }
            context.DrawGeometry(fillBrush, null, geom);
        }

        // Kenarlar
        for (int i = 0; i < screenPts.Count - 1; i++)
            context.DrawLine(linePen, screenPts[i], screenPts[i + 1]);

        // Kapanma çizgisi (son → ilk), noktalı
        if (screenPts.Count >= 2)
            context.DrawLine(closePen, screenPts[^1], screenPts[0]);

        // Rubber-band (son nokta → fare)
        var fare = OdaEkleFare;
        if (fare.HasValue)
        {
            var fareSp = ToScreen(fare.Value);
            context.DrawLine(dashPen, screenPts[^1], fareSp);
        }

        // Köşe noktaları
        foreach (var sp in screenPts)
            context.DrawEllipse(dotFill, dotStroke, sp, 5.0, 5.0);

        // İlk noktayı daha büyük göster (kapanma hedefi)
        context.DrawEllipse(dotFill, new Pen(new SolidColorBrush(Color.Parse("#FFFF80")), 2.0),
            screenPts[0], 7.0, 7.0);
    }
    private void DrawOdaEkleBanner(DrawingContext context, Rect bounds)
    {
        if (OdaEkleModu == 0) return;

        var accent  = Color.Parse("#00C8A0");
        var nokta   = OdaEkleNoktalar?.Count ?? 0;
        var label   = nokta == 0
            ? "⊕ Oda Çiz — tıklayarak ilk köşeyi ekleyin  |  ESC: iptal"
            : $"⊕ Oda Çiz — {nokta} köşe  |  devam için tıkla  |  ENTER / Bitir butonu ile kapat  |  ESC: iptal";

        context.DrawRectangle(null, new Pen(new SolidColorBrush(accent), 3), bounds.Deflate(1.5));

        var text = new FormattedText(
            label,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold),
            12, new SolidColorBrush(Colors.White));

        var bannerH = text.Height + 12d;
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(220, accent.R, accent.G, accent.B)),
            new Rect(bounds.Left, bounds.Top, bounds.Width, bannerH));
        context.DrawText(text, new Point(
            bounds.Left + (bounds.Width - text.Width) / 2d,
            bounds.Top  + (bannerH - text.Height) / 2d));
    }
    private void DrawManuelAciklikPreview(DrawingContext context, SceneModel scene)
    {
        if (!ManuelAciklikModu || Projection != GeometryProjectionMode.Top2D) return;

        var center = Bounds.Center;
        var setup = scene.Setup;
        Point ToScreen(Point3D p)
        {
            var proj = ProjectPoint(p);
            var baseP = new Point(
                setup.OffsetX + ((proj.X - setup.MinX) * setup.BaseScale),
                setup.OffsetY + ((setup.MaxY - proj.Y) * setup.BaseScale));
            return center + _pan + ((baseP - center) * _zoom);
        }

        var pts = ManuelAciklikNoktalar;
        if (pts == null || pts.Count == 0) return;
        var first = pts[0];

        var start = ToScreen(first);
        var end = ManuelAciklikFare is { } fare ? ToScreen(fare) : start;
        var glow = new Pen(new SolidColorBrush(Color.FromArgb(130, 255, 160, 80)), 8.0);
        var main = new Pen(new SolidColorBrush(Color.Parse("#FF7040")), 3.0);
        var dash = new Pen(new SolidColorBrush(Color.Parse("#FFD0A0")), 1.5, DashStyle.Dash);

        context.DrawLine(glow, start, end);
        context.DrawLine(main, start, end);
        context.DrawEllipse(new SolidColorBrush(Color.Parse("#FF7040")), new Pen(Brushes.White, 1.4), start, 5.5, 5.5);
        context.DrawEllipse(new SolidColorBrush(Color.Parse("#331A14")), dash, end, 5.0, 5.0);
    }
    private void DrawManuelAciklikBanner(DrawingContext context, Rect bounds)
    {
        if (!ManuelAciklikModu) return;

        var accent = Color.Parse("#FF7040");
        var nokta = ManuelAciklikNoktalar?.Count ?? 0;
        var label = Projection == GeometryProjectionMode.Isometric3D
            ? "Manuel açıklık için Top2D görünümü seçin"
            : nokta == 0
                ? "+ Manuel Açıklık — başlangıç noktasını tıklayın  |  ESC: iptal"
                : "+ Manuel Açıklık — bitiş noktasını tıklayın  |  en yakın duvara bağlanır  |  ESC: iptal";

        context.DrawRectangle(null, new Pen(new SolidColorBrush(accent), 3), bounds.Deflate(1.5));

        var text = new FormattedText(
            label,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold),
            12, new SolidColorBrush(Colors.White));

        var bannerH = text.Height + 12d;
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(225, accent.R, accent.G, accent.B)),
            new Rect(bounds.Left, bounds.Top, bounds.Width, bannerH));
        context.DrawText(text, new Point(
            bounds.Left + (bounds.Width - text.Width) / 2d,
            bounds.Top  + (bannerH - text.Height) / 2d));
    }
    private void DrawPlacementOverlay(DrawingContext context, Rect bounds)
    {
        var mode = PlacementMode;
        if (mode == PointPlacementMode.None) return;

        // Renkli çerçeve
        var accentHex = mode == PointPlacementMode.PlacingSource ? "#FF8C00" : "#00C8C8";
        var accent    = Color.Parse(accentHex);
        context.DrawRectangle(null, new Pen(new SolidColorBrush(accent), 3), bounds.Deflate(1.5));

        // 3B modda uyarı
        var label = Projection == GeometryProjectionMode.Isometric3D
            ? "⚠ Yerleştirme için Top2D veya Ön 2B modunu seçin"
            : (mode == PointPlacementMode.PlacingSource
                ? "📍 Kaynak yerleştir — tıklayın  |  ESC: iptal"
                : "📍 Alıcı yerleştir — tıklayın  |  ESC: iptal");

        var text = new FormattedText(
            label,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold),
            12,
            new SolidColorBrush(Colors.White));

        var bannerHeight = text.Height + 12d;
        var bannerRect   = new Rect(bounds.Left, bounds.Top, bounds.Width, bannerHeight);
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(210, accent.R, accent.G, accent.B)), bannerRect);

        var textPt = new Point(
            bounds.Left + (bounds.Width - text.Width) / 2d,
            bounds.Top  + (bannerHeight - text.Height) / 2d);
        context.DrawText(text, textPt);
    }
    private void DrawEditableHandles(DrawingContext context, SceneModel scene)
    {
        var poly = EditablePolygon;
        if (poly is null || poly.Count == 0) return;
        if (Projection != GeometryProjectionMode.Top2D) return;

        var center = Bounds.Center;
        var setup  = scene.Setup;
        var fill   = new SolidColorBrush(Color.Parse("#FFD600"));
        var stroke = new Pen(new SolidColorBrush(Colors.White), 1.5);

        foreach (var p in poly)
        {
            var proj = ProjectPoint(p);
            var basePt = new Point(
                setup.OffsetX + ((proj.X - setup.MinX) * setup.BaseScale),
                setup.OffsetY + ((setup.MaxY - proj.Y) * setup.BaseScale));
            var sp = center + _pan + ((basePt - center) * _zoom);
            context.DrawEllipse(fill, stroke, sp, 5.0, 5.0);
        }
    }
    private void DrawColorLegend(DrawingContext context, Rect bounds)
    {
        const double BottomMargin = 10d;

        // Isı haritası gradyan barı (alıcı üçgen yoğunluğu varsa)
        if (ReceiverHitPoints is { Count: > 0 })
        {
            const double GW = 160d, GH = 11d;
            var gx = bounds.Right - GW - 12d;
            var gy = bounds.Bottom - BottomMargin - GH - 22d;
            var gradBrush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint   = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#254BFF"), 0.0),
                    new GradientStop(Color.Parse("#00F070"), 0.45),
                    new GradientStop(Color.Parse("#FFE100"), 0.75),
                    new GradientStop(Color.Parse("#FF0038"), 1.0)
                }
            };
            context.FillRectangle(new SolidColorBrush(Color.Parse("#0A1520"), 0.75),
                new Rect(gx - 8, gy - 14, GW + 16, GH + 24));
            context.FillRectangle(gradBrush, new Rect(gx, gy, GW, GH));
            context.DrawRectangle(null, new Pen(new SolidColorBrush(Color.Parse("#304858"), 0.8), 1),
                new Rect(gx, gy, GW, GH));

            void GLabel(string txt, double x, bool right = false)
            {
                var ft = new FormattedText(txt,
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold),
                    9, new SolidColorBrush(Colors.White));
                context.DrawText(ft, new Point(right ? x - ft.Width : x, gy - ft.Height - 2));
            }
            GLabel("Az", gx);
            GLabel("Enerji", gx + GW / 2d - 18);
            GLabel("Yüksek", gx + GW, right: true);
        }

        var items = new (string Label, string ColorHex)[]
        {
            ("Ayna yüzey", "#FFD700"),
            ("Alıcı yüzey", "#126BFF"),
            ("Işın yolu", "#FFE650"),
        };

        const double BoxW = 14d, BoxH = 12d, Gap = 6d, TextGap = 4d;
        double totalW = 0;
        var fmtList = new List<FormattedText>();
        foreach (var item in items)
        {
            var ft = new FormattedText(item.Label,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold),
                10, new SolidColorBrush(Colors.White));
            fmtList.Add(ft);
            totalW += BoxW + TextGap + ft.Width + Gap;
        }
        totalW -= Gap;

        var startX = bounds.Left + (bounds.Width - totalW) / 2d;
        var y      = bounds.Bottom - BottomMargin - BoxH;

        // Arka plan
        context.FillRectangle(
            new SolidColorBrush(Color.Parse("#0A1520"), 0.80),
            new Rect(startX - 10, y - 6, totalW + 20, BoxH + 12));

        double cx = startX;
        for (int i = 0; i < items.Length; i++)
        {
            var color = Color.Parse(items[i].ColorHex);
            context.FillRectangle(new SolidColorBrush(color, 0.85), new Rect(cx, y, BoxW, BoxH));
            context.DrawRectangle(null, new Pen(new SolidColorBrush(Colors.White, 0.3), 1),
                new Rect(cx, y, BoxW, BoxH));
            context.DrawText(fmtList[i], new Point(cx + BoxW + TextGap, y + (BoxH - fmtList[i].Height) / 2d));
            cx += BoxW + TextGap + fmtList[i].Width + Gap;
        }
    }
    private void DrawRuler(DrawingContext context, Rect bounds, SceneModel scene)
    {
        if (scene.PixelsPerUnit <= 0d || !double.IsFinite(scene.PixelsPerUnit))
        {
            return;
        }

        var worldLength = GetNiceLength(RulerTargetPixelLength / scene.PixelsPerUnit);
        if (worldLength <= 0d || !double.IsFinite(worldLength))
        {
            return;
        }

        var pixelLength = worldLength * scene.PixelsPerUnit;
        var start = new Point(bounds.Left + 18d, bounds.Bottom - 18d);
        var end = new Point(start.X + pixelLength, start.Y);
        var pen = new Pen(new SolidColorBrush(Color.Parse("#F5F9FF")), 2d);

        context.DrawLine(pen, start, end);
        context.DrawLine(pen, new Point(start.X, start.Y - 6d), new Point(start.X, start.Y + 6d));
        context.DrawLine(pen, new Point(end.X, end.Y - 6d), new Point(end.X, end.Y + 6d));

        var label = $"{FormatNumber(worldLength)} {UnitLabel}";
        var text = new FormattedText(
            label,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold),
            12,
            Brushes.White);

        var textPoint = new Point(start.X, start.Y - text.Height - 8d);
        var textBackground = new Rect(textPoint.X - 6d, textPoint.Y - 4d, text.Width + 12d, text.Height + 8d);
        context.FillRectangle(new SolidColorBrush(Color.Parse("#111A27"), 0.82), textBackground);
        context.DrawText(text, textPoint);
    }
    private static double GetNiceLength(double rawLength)
    {
        if (rawLength <= 0d || !double.IsFinite(rawLength))
        {
            return 0d;
        }

        var exponent = Math.Floor(Math.Log10(rawLength));
        var magnitude = Math.Pow(10d, exponent);
        var normalized = rawLength / magnitude;

        var rounded = normalized switch
        {
            <= 1d => 1d,
            <= 2d => 2d,
            <= 5d => 5d,
            _ => 10d
        };

        return rounded * magnitude;
    }
    private static string FormatNumber(double value)
    {
        if (Math.Abs(value) >= 100d)
        {
            return value.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        }

        if (Math.Abs(value) >= 10d)
        {
            return value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        }

        return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }

}
