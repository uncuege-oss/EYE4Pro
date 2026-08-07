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
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        using var _ = context.PushClip(bounds);
        context.FillRectangle(new SolidColorBrush(Color.Parse("#0C1624")), bounds);

        var surfaces = Surfaces;
        var hasBackground = BackgroundSegments is { Count: > 0 };
        var hasEditable = EditablePolygon is { Count: > 0 };
        
        if ((surfaces is null || surfaces.Count == 0) && !hasBackground && !hasEditable)
        {
            DrawEmptyState(context, bounds);
            return;
        }

        if (bounds.Width <= 1 || bounds.Height <= 1) return;

        var scene = BuildScene(bounds);
        if (scene is null)
        {
            DrawEmptyState(context, bounds);
            return;
        }

        // Top2D modunda metre ızgarası (en altta — referans cetvel)
        DrawMetricGrid(context, scene);

        // Yüzeylerin altına ham DXF segmentlerini soluk gri çiz (referans katmanı)
        DrawBackgroundSegments(context, scene);

        // Döndürme LOD: hafif modda, aktif döndürme sırasında kare-başı yeniden
        // projeksiyon yapan kablo/eleman katmanları atlanır; döndürme bitince
        // (OnPointerReleased → InvalidateVisual) tam detay geri çizilir.
        bool dondurmedeAtla = _isRotating && Lightweight3D;

        // Kablo rotaları (renkli, DXF segmentlerinin üstünde)
        if (!dondurmedeAtla) DrawKabloSegmentleri(context, scene);

        var hitMap   = HitIntensityMap;
        var mirrorIds   = MirrorSurfaceIds;
        var receiverIds = ReceiverSurfaceIds;
        bool lightMode = hitMap is not null || mirrorIds is not null || receiverIds is not null;

        foreach (var item in scene.Surfaces)
        {
            // View-Frustum Culling: Ekran dışındaki yüzeyleri çizmeyelim
            var itemBounds = GetScreenBounds(item.ScreenPoints);
            if (!bounds.Intersects(itemBounds)) continue;

            // LOD (Level of Detail): Çok küçük yüzeyleri (zoom düşükken) atlayalım
            if (_zoom < 0.5 && itemBounds.Width < 2 && itemBounds.Height < 2) continue;

            var geometry = new StreamGeometry();
            using (var geometryContext = geometry.Open())
            {
                bool isNewFigure = true;

                for (var i = 0; i < item.ScreenPoints.Count; i++)
                {
                    var p = item.ScreenPoints[i];
                    if (double.IsNaN(p.X))
                    {
                        if (!isNewFigure) geometryContext.EndFigure(true);
                        isNewFigure = true;
                        continue;
                    }

                    if (isNewFigure)
                    {
                        geometryContext.BeginFigure(p, true);
                        isNewFigure = false;
                    }
                    else
                    {
                        geometryContext.LineTo(p);
                    }
                }

                if (!isNewFigure) geometryContext.EndFigure(true);
            }

            IBrush fill;
            IPen   stroke;

            if (lightMode)
            {
                var sid = item.Surface.Id;

                if (receiverIds is not null && receiverIds.Contains(sid))
                {
                    fill   = new SolidColorBrush(Color.Parse("#126BFF"), 0.30);
                    stroke = new Pen(new SolidColorBrush(Color.Parse("#47D8FF"), 0.88), 2.2);
                }
                else if (hitMap is not null && hitMap.TryGetValue(sid, out var intensity))
                {
                    var visibleIntensity = Math.Pow(Math.Clamp(intensity, 0d, 1d), 0.58d);
                    var heatColor = HeatMapColor(visibleIntensity);
                    var alpha = 0.36 + visibleIntensity * 0.32;
                    fill   = new SolidColorBrush(heatColor, alpha);
                    stroke = new Pen(new SolidColorBrush(HeatMapStrokeColor(visibleIntensity)), 2.0);
                }
                else if (mirrorIds is not null && mirrorIds.Contains(sid))
                {
                    // Ayna yüzey — altın/sarı
                    fill   = new SolidColorBrush(Color.Parse("#FFD700"), 0.18);
                    stroke = new Pen(new SolidColorBrush(Color.Parse("#FFE980"), 0.72), 1.8);
                }
                else
                {
                    // Diğer yüzeyler — çok soluk
                    var baseColor = GetCategoryColor(item.Surface.Category);
                    fill   = new SolidColorBrush(baseColor, 0.05);
                    stroke = new Pen(new SolidColorBrush(baseColor, 0.15), 0.6);
                }
            }
            else
            {
                var baseColor     = GetCategoryColor(item.Surface.Category);
                var isSelected    = SelectedSurfaceIds?.Contains(item.Surface.Id) == true;
                var isHighlighted = HighlightedSurfaceId == item.Surface.Id || isSelected;
                var isCopied      = item.Surface.OwnerId != null && CopiedSurfaceIds?.Contains(item.Surface.OwnerId) == true;
                var isSelectedRoom = item.Surface.OwnerId != null && SelectedRoomIds?.Contains(item.Surface.OwnerId) == true;

                if (isCopied)
                {
                    fill   = new SolidColorBrush(Color.Parse("#FFFF00"), 0.5);
                    stroke = new Pen(new SolidColorBrush(Color.Parse("#FFFF80")), 3.0);
                }
                else if (isSelectedRoom)
                {
                    fill   = new SolidColorBrush(Color.Parse("#00E0FF"), 0.40);
                    stroke = new Pen(new SolidColorBrush(Color.Parse("#00E0FF")), 2.8);
                }
                else
                {
                    fill   = new SolidColorBrush(baseColor, isSelected ? 0.4 : (isHighlighted ? 0.32 : 0.14));
                    stroke = new Pen(
                        new SolidColorBrush(isHighlighted ? Colors.White : baseColor),
                        isSelected ? 3.0 : (isHighlighted ? 2.4 : 1.2));
                }
            }

            context.DrawGeometry(fill, stroke, geometry);
        }

        DrawNeighborSegments(context, scene);
        DrawFlankingPaths(context, scene);
        DrawSelectedWall(context, scene);
        // Açıklıklar yüzeylerin üstünde çizilir; manuel eklenen kapı/pencere hemen görünür.
        DrawAcikliklar(context, scene);
        DrawEditableHandles(context, scene);
        DrawLeakTestPath(context, scene);
        DrawRayPaths(context, scene);
        DrawReceiverHeatMap(context, scene);
        if (!dondurmedeAtla) DrawAcousticPoints(context, scene);
        DrawRuler(context, bounds, scene);
        DrawModeIndicator(context, bounds);
        if (lightMode) DrawColorLegend(context, bounds);
        if (scene is not null) DrawOdaEklePreview(context, scene);
        if (scene is not null) DrawManuelAciklikPreview(context, scene);
        DrawPlacementOverlay(context, bounds);
        DrawOdaEkleBanner(context, bounds);
        DrawManuelAciklikBanner(context, bounds);

        // Bant (rubber-band) seçim dikdörtgeni — en üstte çizilir.
        if (_isBanding)
        {
            var bandRect = new Rect(
                Math.Min(_bandStartScreen.X, _bandCurrentScreen.X),
                Math.Min(_bandStartScreen.Y, _bandCurrentScreen.Y),
                Math.Abs(_bandCurrentScreen.X - _bandStartScreen.X),
                Math.Abs(_bandCurrentScreen.Y - _bandStartScreen.Y));
            context.FillRectangle(new SolidColorBrush(Color.FromArgb(30, 78, 224, 154)), bandRect);
            context.DrawRectangle(
                new Pen(new SolidColorBrush(Color.Parse("#4EE09A")), 1.2)
                {
                    DashStyle = new DashStyle(new double[] { 4, 3 }, 0)
                }, bandRect);
        }
    }
    private void DrawEmptyState(DrawingContext context, Rect bounds)
    {
        var text = new FormattedText(
            "Görüntülenecek geometri yok",
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold),
            16,
            Brushes.White);

        var point = new Point(
            bounds.Center.X - (text.Width / 2d),
            bounds.Center.Y - (text.Height / 2d));

        context.DrawText(text, point);
    }
    private void DrawModeIndicator(DrawingContext context, Rect bounds)
    {
        var modeText = Projection switch
        {
            GeometryProjectionMode.Top2D => "Üst 2B",
            GeometryProjectionMode.Front2D => "Ön 2B",
            GeometryProjectionMode.Isometric3D => "3B Orbit (sağ tuş ile döndür)",
            _ => ""
        };

        var text = new FormattedText(
            modeText,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold),
            11,
            new SolidColorBrush(Color.Parse("#88AABB")));

        var point = new Point(bounds.Right - text.Width - 12, bounds.Top + 8);
        context.DrawText(text, point);
    }
    private static Color HeatMapColor(double t)
    {
        t = Math.Clamp(t, 0d, 1d);
        if (t <= 0.45d)
        {
            var u = t / 0.45d;
            return LerpColor(Color.Parse("#126BFF"), Color.Parse("#00F070"), u);
        }

        if (t <= 0.75d)
        {
            var u = (t - 0.45d) / 0.30d;
            return LerpColor(Color.Parse("#00F070"), Color.Parse("#FFE100"), u);
        }

        return LerpColor(Color.Parse("#FFE100"), Color.Parse("#FF0038"), (t - 0.75d) / 0.25d);
    }
    private static Color HeatMapStrokeColor(double t)
    {
        var baseColor = HeatMapColor(t);
        return LerpColor(baseColor, Colors.White, Math.Clamp(0.22 + t * 0.28, 0, 0.5));
    }
    private static Color LerpColor(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0d, 1d);
        return Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }
    private static Color GetCategoryColor(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return Color.Parse("#7AA2F7");
        }

        var normalized = category.Trim().ToUpperInvariant();
        // UX — 5 seviyeli yönetmelik renklendirme (tabloyla tutarlı)
        if (normalized is "UYGUN_GUVENLI")
            return Color.Parse("#27AE60");  // koyu yeşil (marj ≥ +3 dB)
        if (normalized is "UYGUN" or "UYGUN_SINIRDA")
            return Color.Parse("#2ECC71");  // parlak yeşil (marj ≥ +1 dB)
        if (normalized is "SINIR")
            return Color.Parse("#F39C12");  // amber (marj ±1 dB)
        if (normalized is "UYGUNSUZ_HAFIF")
            return Color.Parse("#E67E22");  // koyu turuncu (marj ≥ −3 dB)
        if (normalized is "UYGUNSUZ")
            return Color.Parse("#E74C3C");  // kırmızı (marj < −3 dB)
        if (normalized is "VERI_EKSIK")
            return Color.Parse("#F1C40F");
        // UX — Isı haritası: aynı paletten ama yarı saydam (alpha 96) overlay
        if (normalized is "HEAT_UYGUN_GUVENLI") return Color.FromArgb(96, 0x27, 0xAE, 0x60);
        if (normalized is "HEAT_UYGUN_SINIRDA") return Color.FromArgb(96, 0x2E, 0xCC, 0x71);
        if (normalized is "HEAT_SINIR")         return Color.FromArgb(96, 0xF3, 0x9C, 0x12);
        if (normalized is "HEAT_UYGUNSUZ_HAFIF")return Color.FromArgb(96, 0xE6, 0x7E, 0x22);
        if (normalized is "HEAT_UYGUNSUZ")      return Color.FromArgb(96, 0xE7, 0x4C, 0x3C);
        if (normalized is "HEAT_VERI_EKSIK")    return Color.FromArgb(96, 0xF1, 0xC4, 0x0F);
        if (normalized.Contains("DOSEME"))
            return Color.Parse("#7F8C8D");
        if (normalized.Contains("TAVAN"))
            return Color.Parse("#95A5A6");
        // B4 — Açıklık yüzeyleri: cam (açık mavi) / kapı (koyu amber)
        if (normalized == "PENCERE")
            return Color.Parse("#7FCFE8");
        if (normalized == "KAPI")
            return Color.Parse("#A0764A");

        unchecked
        {
            var hash = category.Aggregate(17, (current, ch) => (current * 31) + ch);
            var hue = Math.Abs(hash % 360);
            return ColorFromHsv(hue, 0.65, 0.92);
        }
    }
    private static Color ColorFromHsv(double hue, double saturation, double value)
    {
        var c = value * saturation;
        var x = c * (1 - Math.Abs(((hue / 60d) % 2) - 1));
        var m = value - c;

        (double r, double g, double b) = hue switch
        {
            < 60d => (c, x, 0d),
            < 120d => (x, c, 0d),
            < 180d => (0d, c, x),
            < 240d => (0d, x, c),
            < 300d => (x, 0d, c),
            _ => (c, 0d, x)
        };

        return Color.FromRgb(
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }
    private void DrawMetricGrid(DrawingContext context, SceneModel scene)
    {
        if (Projection != GeometryProjectionMode.Top2D) return;
        double ppu = scene.PixelsPerUnit;
        if (ppu <= 0 || !double.IsFinite(ppu)) return;

        var bounds = Bounds;
        // Aralık seçimi: zoom seviyesine göre 1m/5m/10m/50m...
        double rawStep = 80.0 / ppu; // ekranda ~80px arada bir ana çizgi
        double step = GetNiceLength(rawStep);
        if (step <= 0) return;
        double minorStep = step / 5.0;

        // Dünya → ekran dönüşümü için tek bir referans nokta (0,0) bulup adım çevir
        var setup = scene.Setup;
        var center = bounds.Center;
        Point WorldToScreen(double wx, double wy)
        {
            var baseP = new Point(
                setup.OffsetX + ((wx - setup.MinX) * setup.BaseScale),
                setup.OffsetY + ((setup.MaxY - wy) * setup.BaseScale));
            return center + _pan + ((baseP - center) * _zoom);
        }

        // Ekran köşelerinin dünya karşılığı
        double InvX(double sx) => setup.MinX + (((sx - center.X - _pan.X) / _zoom) + center.X - setup.OffsetX) / setup.BaseScale;
        double InvY(double sy) => setup.MaxY - (((sy - center.Y - _pan.Y) / _zoom) + center.Y - setup.OffsetY) / setup.BaseScale;
        double wxMin = InvX(bounds.Left), wxMax = InvX(bounds.Right);
        double wyMin = InvY(bounds.Bottom), wyMax = InvY(bounds.Top);

        var minorPen = new Pen(new SolidColorBrush(Color.Parse("#1E2A3A"), 0.7), 0.5);
        var majorPen = new Pen(new SolidColorBrush(Color.Parse("#2E4258"), 0.85), 1.0);

        // Dikey çizgiler
        double x0Minor = Math.Floor(wxMin / minorStep) * minorStep;
        for (double wx = x0Minor; wx <= wxMax; wx += minorStep)
        {
            bool isMajor = Math.Abs(wx / step - Math.Round(wx / step)) < 1e-4;
            var pen = isMajor ? majorPen : minorPen;
            var s1 = WorldToScreen(wx, wyMin);
            var s2 = WorldToScreen(wx, wyMax);
            context.DrawLine(pen, new Point(s1.X, bounds.Top), new Point(s2.X, bounds.Bottom));
        }
        // Yatay çizgiler
        double y0Minor = Math.Floor(wyMin / minorStep) * minorStep;
        for (double wy = y0Minor; wy <= wyMax; wy += minorStep)
        {
            bool isMajor = Math.Abs(wy / step - Math.Round(wy / step)) < 1e-4;
            var pen = isMajor ? majorPen : minorPen;
            var s1 = WorldToScreen(wxMin, wy);
            var s2 = WorldToScreen(wxMax, wy);
            context.DrawLine(pen, new Point(bounds.Left, s1.Y), new Point(bounds.Right, s2.Y));
        }

        // Üst ve sol kenarda ana çizgi etiketleri ("X m")
        var labelBrush = new SolidColorBrush(Color.Parse("#7AAACB"));
        double x0Major = Math.Floor(wxMin / step) * step;
        for (double wx = x0Major; wx <= wxMax; wx += step)
        {
            var sp = WorldToScreen(wx, wyMax);
            var label = $"{FormatNumber(wx)}";
            var ft = new FormattedText(label,
                System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default), 9.5, labelBrush);
            context.DrawText(ft, new Point(sp.X + 2, bounds.Top + 2));
        }
        double y0Major = Math.Floor(wyMin / step) * step;
        for (double wy = y0Major; wy <= wyMax; wy += step)
        {
            var sp = WorldToScreen(wxMin, wy);
            var label = $"{FormatNumber(wy)}";
            var ft = new FormattedText(label,
                System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default), 9.5, labelBrush);
            context.DrawText(ft, new Point(bounds.Left + 2, sp.Y - ft.Height - 1));
        }

        // Üst/sol kenarda adım göstergesi
        var hintBrush = new SolidColorBrush(Color.Parse("#88AABB"));
        var hint = new FormattedText($"Izgara: {FormatNumber(step)} {UnitLabel}",
            System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold), 11, hintBrush);
        context.FillRectangle(new SolidColorBrush(Color.Parse("#0A1520"), 0.7),
            new Rect(bounds.Left + 6, bounds.Top + 24, hint.Width + 12, hint.Height + 6));
        context.DrawText(hint, new Point(bounds.Left + 12, bounds.Top + 27));
    }
    private void DrawNeighborSegments(DrawingContext context, SceneModel scene)
    {
        var segs = NeighborSegments;
        if (segs is null || segs.Count == 0) return;

        var setup = scene.Setup;
        var center = Bounds.Center;
        Point ToScreen(Point3D p)
        {
            var proj  = ProjectPoint(p);
            var baseP = new Point(
                setup.OffsetX + ((proj.X - setup.MinX) * setup.BaseScale),
                setup.OffsetY + ((setup.MaxY - proj.Y) * setup.BaseScale));
            return center + _pan + ((baseP - center) * _zoom);
        }
        var normalPen   = new Pen(new SolidColorBrush(Color.Parse("#FFB300"), 0.95), 2.4);
        var selectedPen = new Pen(new SolidColorBrush(Color.Parse("#FFE680")),       4.0);
        var labelBrush  = new SolidColorBrush(Colors.White);

        foreach (var (a, b, selected) in segs)
        {
            var pa = ToScreen(a);
            var pb = ToScreen(b);
            context.DrawLine(selected ? selectedPen : normalPen, pa, pb);
            if (selected)
            {
                double dx = b.X - a.X, dy = b.Y - a.Y;
                double len = Math.Sqrt(dx * dx + dy * dy);
                var mid = new Point((pa.X + pb.X) / 2, (pa.Y + pb.Y) / 2);
                var ft = new FormattedText($"{len:0.00} {UnitLabel}",
                    System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold), 12, labelBrush);
                var bg = new Rect(mid.X - 4, mid.Y - ft.Height / 2 - 2, ft.Width + 8, ft.Height + 4);
                context.FillRectangle(new SolidColorBrush(Color.Parse("#7A4500"), 0.95), bg);
                context.DrawText(ft, new Point(mid.X, mid.Y - ft.Height / 2));
            }
        }
    }
    /// UX/B6 — Flanking yolu görselleştirmesi.
    /// SeciliSonucSatiri için en kötü 3 yolu, kaynak mekan ortasından alıcı mekan ortasına
    /// hafif Y-eksenli kayma ile çizer; renk her yolun katkı seviyesine göredir.
    private void DrawFlankingPaths(DrawingContext context, SceneModel scene)
    {
        var segs = FlankingYolSegmentleri;
        if (segs is null || segs.Count == 0) return;

        var setup = scene.Setup;
        var center = Bounds.Center;
        Point ToScreen(Point3D p)
        {
            var proj = ProjectPoint(p);
            var baseP = new Point(
                setup.OffsetX + ((proj.X - setup.MinX) * setup.BaseScale),
                setup.OffsetY + ((setup.MaxY - proj.Y) * setup.BaseScale));
            return center + _pan + ((baseP - center) * _zoom);
        }

        foreach (var (a, b, renk) in segs)
        {
            Color col;
            try { col = Color.Parse(renk); }
            catch { col = Color.Parse("#E67E22"); }

            var pa = ToScreen(a);
            var pb = ToScreen(b);

            var glowPen = new Pen(new SolidColorBrush(col, 0.30), 9.0);
            var mainPen = new Pen(new SolidColorBrush(col, 0.95), 3.0);
            context.DrawLine(glowPen, pa, pb);
            context.DrawLine(mainPen, pa, pb);

            // Alıcı ucuna ok başı
            double dx = pb.X - pa.X, dy = pb.Y - pa.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len > 1e-3)
            {
                double ux = dx / len, uy = dy / len;
                double headLen = 12.0;
                double headW = 6.0;
                var tip = pb;
                var basePt = new Point(pb.X - ux * headLen, pb.Y - uy * headLen);
                var left = new Point(basePt.X - uy * headW, basePt.Y + ux * headW);
                var right = new Point(basePt.X + uy * headW, basePt.Y - ux * headW);

                var sg = new StreamGeometry();
                using (var ctx = sg.Open())
                {
                    ctx.BeginFigure(tip, true);
                    ctx.LineTo(left);
                    ctx.LineTo(right);
                    ctx.EndFigure(true);
                }
                context.DrawGeometry(new SolidColorBrush(col, 0.95), null, sg);
            }
        }
    }
    private void DrawSelectedWall(DrawingContext context, SceneModel scene)
    {
        var a = SelectedWallA;
        var b = SelectedWallB;
        if (a is null || b is null) return;

        var setup  = scene.Setup;
        var center = Bounds.Center;
        Point ToScreen(Point3D p)
        {
            var proj  = ProjectPoint(p);
            var baseP = new Point(
                setup.OffsetX + ((proj.X - setup.MinX) * setup.BaseScale),
                setup.OffsetY + ((setup.MaxY - proj.Y) * setup.BaseScale));
            return center + _pan + ((baseP - center) * _zoom);
        }

        var pa = ToScreen(a.Value);
        var pb = ToScreen(b.Value);

        var glowPen = new Pen(new SolidColorBrush(Color.Parse("#FF6600"), 0.35), 10.0);
        var mainPen = new Pen(new SolidColorBrush(Color.Parse("#FF8C00")),        3.0);

        context.DrawLine(glowPen, pa, pb);
        context.DrawLine(mainPen, pa, pb);
    }
    private void DrawAcikliklar(DrawingContext context, SceneModel scene)
    {
        var hlSeg = HighlightAciklikSegment;
        if (_cAciklikAktifGeom is null && _cAciklikIptalGeom is null && !hlSeg.HasValue) return;

        var setup = scene.Setup;
        var m = ZoomPanMatrix(Bounds);
        using var _ = context.PushTransform(m);

        if (_cAciklikIptalGeom is not null)
        {
            var penIptal = new Pen(new SolidColorBrush(Color.Parse("#666666"), 0.85), 1.5 / _zoom);
            context.DrawGeometry(null, penIptal, _cAciklikIptalGeom);
        }
        if (_cAciklikAktifGeom is not null)
        {
            var penAktif = new Pen(new SolidColorBrush(Color.Parse("#FF3030"), 0.95), 2.5 / _zoom);
            context.DrawGeometry(null, penAktif, _cAciklikAktifGeom);
        }

        if (hlSeg.HasValue)
        {
            var pA = ProjectPoint(hlSeg.Value.Bas);
            var pB = ProjectPoint(hlSeg.Value.Bit);
            if (!double.IsNaN(pA.X) && !double.IsNaN(pB.X))
            {
                var ba = new Point(
                    setup.OffsetX + ((pA.X - setup.MinX) * setup.BaseScale),
                    setup.OffsetY + ((setup.MaxY - pA.Y) * setup.BaseScale));
                var bb = new Point(
                    setup.OffsetX + ((pB.X - setup.MinX) * setup.BaseScale),
                    setup.OffsetY + ((setup.MaxY - pB.Y) * setup.BaseScale));
                var penGlow = new Pen(new SolidColorBrush(Color.FromArgb(60, 255, 184, 48)), 8 / _zoom);
                context.DrawLine(penGlow, ba, bb);
                var penHl = new Pen(new SolidColorBrush(Color.Parse("#FFB830"), 0.98), 3.5 / _zoom);
                context.DrawLine(penHl, ba, bb);
            }
        }
    }
    private void DrawBackgroundSegments(DrawingContext context, SceneModel scene)
    {
        // Önbellekteki base-koordinat geometri tek seferde, zoom/pan transformuyla çizilir.
        // (Eski sürüm her segment için ayrı DrawLine + her karede yeniden projeksiyon
        //  yapıyordu → büyük DXF'lerde donma.) PushClip zaten Render'da uygulanmış.
        var geom = _cBgGeom;
        if (geom is null) return;

        // Çizgi kalınlığı transformla ölçeklendiği için _zoom'a bölerek ~sabit tutulur.
        var pen = new Pen(new SolidColorBrush(Color.Parse("#3A5060"), 0.85), 0.8 / _zoom);
        using (context.PushTransform(ZoomPanMatrix(Bounds)))
            context.DrawGeometry(null, pen, geom);
    }
    private void DrawKabloSegmentleri(DrawingContext context, SceneModel scene)
    {
        var segs    = KabloSegmentleri;
        var vurgu   = HighlightKabloSegmentleri;
        bool hasAny = (segs is { Count: > 0 }) || (vurgu is { Count: > 0 });
        if (!hasAny) return;

        var sceneSetup = scene.Setup;
        var center     = Bounds.Center;
        var bounds     = Bounds;

        Point ToScreen(Point3D p)
        {
            var proj  = ProjectPoint(p);
            var baseP = new Point(
                sceneSetup.OffsetX + ((proj.X - sceneSetup.MinX) * sceneSetup.BaseScale),
                sceneSetup.OffsetY + ((sceneSetup.MaxY - proj.Y) * sceneSetup.BaseScale));
            return center + _pan + ((baseP - center) * _zoom);
        }

        bool Clip(Point pa, Point pb) =>
            (pa.X < bounds.Left   && pb.X < bounds.Left)   ||
            (pa.X > bounds.Right  && pb.X > bounds.Right)  ||
            (pa.Y < bounds.Top    && pb.Y < bounds.Top)    ||
            (pa.Y > bounds.Bottom && pb.Y > bounds.Bottom);

        // Normal kablolar (normal opaklık)
        string? lastRenk = null;
        Pen? pen = null;
        foreach (var (a, b, renk) in segs ?? [])
        {
            if (renk != lastRenk)
            {
                pen = new Pen(new SolidColorBrush(Color.Parse(renk), vurgu is { Count: > 0 } ? 0.95 : 1.0), 1.8);
                lastRenk = renk;
            }
            var pa = ToScreen(new Point3D(a.X, a.Y, 0));
            var pb = ToScreen(new Point3D(b.X, b.Y, 0));
            if (Clip(pa, pb)) continue;
            context.DrawLine(pen!, pa, pb);
        }

        // Seçili hat (parlak + kalın + glow)
        lastRenk = null;
        Pen? glowPen = null;
        Pen? hPen    = null;
        foreach (var (a, b, renk) in vurgu ?? [])
        {
            if (renk != lastRenk)
            {
                var c = Color.Parse(renk);
                glowPen = new Pen(new SolidColorBrush(Color.FromArgb(80, c.R, c.G, c.B)), 7.0);
                hPen    = new Pen(new SolidColorBrush(c), 3.2);
                lastRenk = renk;
            }
            var pa = ToScreen(new Point3D(a.X, a.Y, 0));
            var pb = ToScreen(new Point3D(b.X, b.Y, 0));
            if (Clip(pa, pb)) continue;
            context.DrawLine(glowPen!, pa, pb);
            context.DrawLine(hPen!,    pa, pb);
        }

        foreach (var (position, text, renk) in KabloEtiketleri ?? [])
        {
            var p = ToScreen(position);
            var c = Color.Parse(renk);
            var ft = new FormattedText(
                text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                12,
                Brushes.White);
            var rect = new Rect(p.X - 13, p.Y - 10, ft.Width + 12, 20);
            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(220, 5, 18, 18)), new Pen(new SolidColorBrush(c), 1.2), rect, 3, 3);
            context.DrawText(ft, new Point(rect.X + 6, rect.Y + 2));
        }
    }
    private void DrawLeakTestPath(DrawingContext context, SceneModel scene)
    {
        if (Hafif3D) return; // hafif 3D: akustik katmanlar kapalı (yalnız Lightweight3D modülleri)
        var leakPath = LeakTestPath;
        if (leakPath is null || leakPath.Count < 2)
        {
            return;
        }

        var sceneSetup = scene.Setup;
        var center = Bounds.Center;

        var screenPoints = leakPath
            .Select(ProjectPoint)
            .Select(point =>
            {
                var basePoint = new Point(
                    sceneSetup.OffsetX + ((point.X - sceneSetup.MinX) * sceneSetup.BaseScale),
                    sceneSetup.OffsetY + ((sceneSetup.MaxY - point.Y) * sceneSetup.BaseScale));

                return center + _pan + ((basePoint - center) * _zoom);
            })
            .ToArray();

        // NaN noktalar ışınlar arası ayıraçtır; çizimde kopukluk sağlar.
        var rayPen = new Pen(new SolidColorBrush(Color.Parse("#FF5B5B")), 2.2);
        for (var i = 1; i < screenPoints.Length; i++)
        {
            if (double.IsNaN(screenPoints[i].X) || double.IsNaN(screenPoints[i - 1].X)) continue;
            context.DrawLine(rayPen, screenPoints[i - 1], screenPoints[i]);
        }

        var dotFill = new SolidColorBrush(Color.Parse("#FFD6D6"));
        var dotPen  = new Pen(new SolidColorBrush(Color.Parse("#FF5B5B")), 1.4);
        for (var i = 0; i < screenPoints.Length; i++)
        {
            if (double.IsNaN(screenPoints[i].X)) continue;
            context.DrawEllipse(dotFill, dotPen, screenPoints[i], 3.0, 3.0);
        }

        // Başlangıç (merkez) noktası: sarı, daha büyük — tüm ışınlar buradan çıkar.
        var first = screenPoints[0];
        if (!double.IsNaN(first.X))
            context.DrawEllipse(
                new SolidColorBrush(Color.Parse("#FFE000")),
                new Pen(new SolidColorBrush(Color.Parse("#FF8C00")), 2d),
                first, 7d, 7d);
    }
    private void DrawReceiverHeatMap(DrawingContext context, SceneModel scene)
    {
        if (Hafif3D) return; // hafif 3D: akustik katmanlar kapalı
        var hitPoints = ReceiverHitPoints;
        if (hitPoints is null || hitPoints.Count < 2) return;

        var sceneSetup = scene.Setup;
        var center     = Bounds.Center;
        var surfaceById = scene.Surfaces.ToDictionary(s => s.Surface.Id);

        Point ToScreen(Point3D p)
        {
            var proj  = ProjectPoint(p);
            var baseP = new Point(
                sceneSetup.OffsetX + ((proj.X - sceneSetup.MinX) * sceneSetup.BaseScale),
                sceneSetup.OffsetY + ((sceneSetup.MaxY - proj.Y) * sceneSetup.BaseScale));
            return center + _pan + ((baseP - center) * _zoom);
        }

        foreach (var group in hitPoints.GroupBy(h => h.SurfaceId))
        {
            if (!surfaceById.TryGetValue(group.Key, out var surface))
                continue;

            var polygon = surface.ScreenPoints.Where(p => !double.IsNaN(p.X)).ToArray();
            if (polygon.Length < 3)
                continue;

            var polyBounds = GetScreenBounds(polygon);
            var minX = polyBounds.Left;
            var maxX = polyBounds.Right;
            var minY = polyBounds.Top;
            var maxY = polyBounds.Bottom;
            var rangeX = maxX - minX;
            var rangeY = maxY - minY;
            if (rangeX < 1 || rangeY < 1)
                continue;

            var screenPts = group
                .Select(h => (Point: ToScreen(h.Position), Weight: Math.Max(0, h.Weight)))
                .Where(p => double.IsFinite(p.Point.X) &&
                            double.IsFinite(p.Point.Y) &&
                            IsPointInPolygon(p.Point, polygon))
                .ToArray();

            if (screenPts.Length == 0)
                continue;

            // Ekran uzayında, yüzey poligonu ile sınırlı yoğunluk ızgarası.
            const int MaxCells = 72;
            var cellPx = Math.Clamp(Math.Min(rangeX, rangeY) / 22.0, 6.0, 18.0);
            var cols   = Math.Min(MaxCells, Math.Max(1, (int)Math.Ceiling(rangeX / cellPx)));
            var rows   = Math.Min(MaxCells, Math.Max(1, (int)Math.Ceiling(rangeY / cellPx)));

            var grid = new double[rows, cols];
            foreach (var sp in screenPts)
            {
                var ci = Math.Clamp((int)((sp.Point.X - minX) / rangeX * cols), 0, cols - 1);
                var ri = Math.Clamp((int)((sp.Point.Y - minY) / rangeY * rows), 0, rows - 1);
                grid[ri, ci] += sp.Weight;
            }

            double minCount = double.MaxValue;
            double maxCount = 0;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    if (grid[r, c] > 0)
                    {
                        if (grid[r, c] < minCount) minCount = grid[r, c];
                        if (grid[r, c] > maxCount) maxCount = grid[r, c];
                    }

            if (maxCount <= 0)
                continue;
            if (!double.IsFinite(minCount)) minCount = 0;

            var cw      = rangeX / cols;
            var ch      = rangeY / rows;
            var splatR  = Math.Clamp(Math.Max(cw, ch) * 1.45, 7.0, 24.0);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (grid[r, c] <= 0) continue;

                    var cx = minX + (c + 0.5) * cw;
                    var cy = minY + (r + 0.5) * ch;
                    var cellCenter = new Point(cx, cy);
                    if (!IsPointInPolygon(cellCenter, polygon))
                        continue;

                    var normalized = maxCount > minCount
                        ? (grid[r, c] - minCount) / (maxCount - minCount)
                        : 1.0;
                    var intensity   = Math.Pow(Math.Clamp(normalized, 0d, 1d), 0.68d);
                    var color       = HeatMapColor(intensity);
                    var centerAlpha = (byte)(Math.Clamp(0.42 + intensity * 0.52, 0, 1) * 255);
                    var midAlpha    = (byte)(Math.Clamp(centerAlpha * 0.48, 0, 255));

                    // Merkezden kenara doğru şeffaflıkla yumuşayan, alıcı yüzey
                    // poligonunun içinde tutulan radyal leke.
                    var brush = new RadialGradientBrush
                    {
                        Center         = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
                        GradientOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
                        GradientStops  =
                        {
                            new GradientStop(Color.FromArgb(centerAlpha, color.R, color.G, color.B), 0.0),
                            new GradientStop(Color.FromArgb(midAlpha, color.R, color.G, color.B), 0.42),
                            new GradientStop(Color.FromArgb((byte)(centerAlpha / 7), color.R, color.G, color.B), 0.78),
                            new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1.0),
                        }
                    };

                    context.DrawEllipse(brush, null, cellCenter, splatR, splatR);
                    if (intensity > 0.70d)
                    {
                        var hotBrush = new SolidColorBrush(HeatMapStrokeColor(intensity), 0.82);
                        context.DrawEllipse(hotBrush, null, cellCenter, splatR * 0.25, splatR * 0.25);
                    }
                }
            }
        }
    }
    private void DrawRayPaths(DrawingContext context, SceneModel scene)
    {
        if (Hafif3D) return; // hafif 3D: akustik katmanlar kapalı
        var paths = RayPaths;
        if (paths is null || paths.Count == 0) return;

        var sceneSetup = scene.Setup;
        var center     = Bounds.Center;

        Point ToScreen(Point3D p)
        {
            var proj = ProjectPoint(p);
            var base2 = new Point(
                sceneSetup.OffsetX + ((proj.X - sceneSetup.MinX) * sceneSetup.BaseScale),
                sceneSetup.OffsetY + ((sceneSetup.MaxY - proj.Y) * sceneSetup.BaseScale));
            return center + _pan + ((base2 - center) * _zoom);
        }

        foreach (var seg in paths)
        {
            var weight = Math.Clamp(seg.Weight, 0.02, 1.0);
            var alpha = (byte)(Math.Clamp(28 + weight * 86, 0, 160));
            var width = 0.55 + weight * 1.15;
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(alpha, 255, 230, 80)), width);
            context.DrawLine(pen, ToScreen(seg.From), ToScreen(seg.To));
        }
    }
    private void DrawAcousticPoints(DrawingContext context, SceneModel scene)
    {
        var sources = SourcePoints;
        var receivers = ReceiverPoints;

        if ((sources is null || sources.Count == 0) && (receivers is null || receivers.Count == 0))
        {
            return;
        }

        var sceneSetup = scene.Setup;
        var center = Bounds.Center;

        Point ToScreen(Point3D p)
        {
            var proj = ProjectPoint(p);
            var basePoint = new Point(
                sceneSetup.OffsetX + ((proj.X - sceneSetup.MinX) * sceneSetup.BaseScale),
                sceneSetup.OffsetY + ((sceneSetup.MaxY - proj.Y) * sceneSetup.BaseScale));
            return center + _pan + ((basePoint - center) * _zoom);
        }

        // Hafif 3D modunda yazı etiketleri çizilmez (3D'de okunmuyor + kare-başı
        // metin ölçüm/çizim maliyeti kalkar); işaretçiler kalır.
        bool showLabels = !Hafif3D;

        // --- Sources: turuncu daire ---
        const double SourceRadius = 7d;
        var sourceFill = new SolidColorBrush(Color.Parse("#FF8C00"));
        var sourcePen = new Pen(new SolidColorBrush(Color.Parse("#FFF0D0")), 1.6);
        var aimPen = new Pen(new SolidColorBrush(Color.Parse("#FFB24A"), 0.82), 2.0);
        var aimSoftPen = new Pen(new SolidColorBrush(Color.Parse("#FFB24A"), 0.26), 7.0);
        var aimLength = Math.Clamp(96d / Math.Max(scene.PixelsPerUnit, 1e-6), 0.5d, 12d);

        foreach (var src in sources ?? [])
        {
            var sp = ToScreen(src.Position);
            if (src.Directivity != DirectivityPattern.Omnidirectional || !string.IsNullOrWhiteSpace(src.LoudspeakerId))
            {
                var end = Add(src.Position, DirectionFromAngles(src.DirectivityAzimuthDeg, src.DirectivityElevationDeg), aimLength);
                DrawAimingLine(context, sp, ToScreen(end), aimSoftPen, aimPen);
            }
            context.DrawEllipse(sourceFill, sourcePen, sp, SourceRadius, SourceRadius);
            if (showLabels) DrawPointLabel(context, sp, src.Label, SourceRadius, Color.Parse("#FF8C00"));
        }

        // --- Receivers: cyan elmas ---
        const double ReceiverRadius = 7d;
        var receiverFill = new SolidColorBrush(Color.Parse("#00C8C8"));
        var receiverPen = new Pen(new SolidColorBrush(Color.Parse("#D0FFFF")), 1.6);

        foreach (var rcv in receivers ?? [])
        {
            var rp = ToScreen(rcv.Position);
            DrawDiamond(context, rp, ReceiverRadius, receiverFill, receiverPen);
            if (showLabels) DrawPointLabel(context, rp, rcv.Label, ReceiverRadius, Color.Parse("#00C8C8"));
        }

        // --- Seçili eleman vurgusu: parlak beyaz halka ---
        var highlightPen = new Pen(new SolidColorBrush(Color.Parse("#FFFFFF")), 2.5);
        var glowPen     = new Pen(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), 6.0);
        var positions   = HighlightedPointPositions;
        if (positions is { Count: > 0 })
        {
            foreach (var pos in positions)
            {
                var hp = ToScreen(pos);
                context.DrawEllipse(null, highlightPen, hp, SourceRadius + 5, SourceRadius + 5);
                context.DrawEllipse(null, glowPen, hp, SourceRadius + 5, SourceRadius + 5);
            }
        }
        else
        {
            var highlightPos = HighlightedPointPosition;
            if (highlightPos.HasValue)
            {
                var hp = ToScreen(highlightPos.Value);
                context.DrawEllipse(null, highlightPen, hp, SourceRadius + 5, SourceRadius + 5);
                context.DrawEllipse(null, glowPen, hp, SourceRadius + 5, SourceRadius + 5);
            }
        }

        // --- Aday vurguları: soluk yeşil kesikli halkalar ("buraya tıklanabilir") ---
        var candidates = CandidatePointPositions;
        if (candidates is { Count: > 0 })
        {
            var candPen = new Pen(new SolidColorBrush(Color.FromArgb(170, 110, 220, 120)), 1.6)
            {
                DashStyle = new DashStyle(new double[] { 3, 3 }, 0)
            };
            foreach (var pos in candidates)
            {
                var cp = ToScreen(pos);
                context.DrawEllipse(null, candPen, cp, SourceRadius + 7, SourceRadius + 7);
            }
        }

        // --- Hover ön-vurgusu: turkuaz halka (tıklanacak hedef) ---
        var hoverPos = HoverPointPosition;
        if (hoverPos.HasValue)
        {
            var hoverPen = new Pen(new SolidColorBrush(Color.Parse("#40E0D0")), 2.0);
            var hoverGlow = new Pen(new SolidColorBrush(Color.FromArgb(70, 64, 224, 208)), 5.5);
            var hp2 = ToScreen(hoverPos.Value);
            context.DrawEllipse(null, hoverPen, hp2, SourceRadius + 6, SourceRadius + 6);
            context.DrawEllipse(null, hoverGlow, hp2, SourceRadius + 6, SourceRadius + 6);
        }
    }
    private static void DrawAimingLine(DrawingContext context, Point start, Point end, IPen softPen, IPen pen)
    {
        context.DrawLine(softPen, start, end);
        context.DrawLine(pen, start, end);

        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-6) return;

        var ux = dx / len;
        var uy = dy / len;
        var px = -uy;
        var py = ux;
        const double ArrowLength = 10d;
        const double ArrowWidth = 5d;

        var p1 = new Point(
            end.X - ux * ArrowLength + px * ArrowWidth,
            end.Y - uy * ArrowLength + py * ArrowWidth);
        var p2 = new Point(
            end.X - ux * ArrowLength - px * ArrowWidth,
            end.Y - uy * ArrowLength - py * ArrowWidth);

        context.DrawLine(pen, end, p1);
        context.DrawLine(pen, end, p2);
    }
    private static void DrawDiamond(DrawingContext context, Point center, double size, IBrush fill, IPen pen)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(center.X, center.Y - size), true);
            ctx.LineTo(new Point(center.X + size, center.Y));
            ctx.LineTo(new Point(center.X, center.Y + size));
            ctx.LineTo(new Point(center.X - size, center.Y));
            ctx.EndFigure(true);
        }

        context.DrawGeometry(fill, pen, geometry);
    }
    private static void DrawPointLabel(DrawingContext context, Point anchor, string label, double radius, Color color)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        var text = new FormattedText(
            label,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold),
            11,
            new SolidColorBrush(Colors.White));

        var labelOrigin = new Point(anchor.X + radius + 3d, anchor.Y - (text.Height / 2d));

        // Koyu arka plan + renkli kenar — her zemin renginde okunabilir kontrast sağlar
        var bg = new Rect(labelOrigin.X - 3d, labelOrigin.Y - 2d, text.Width + 6d, text.Height + 4d);
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(225, 8, 18, 30)), bg);
        context.DrawRectangle(null, new Pen(new SolidColorBrush(color, 0.85), 1.2), bg, 2, 2);
        context.DrawText(text, labelOrigin);
    }

}
