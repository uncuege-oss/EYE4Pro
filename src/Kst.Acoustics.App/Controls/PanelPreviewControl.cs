using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.App.ViewModels;
using Kst.Acoustics.Core.PanelDesign;
using System.Globalization;

namespace Kst.Acoustics.App.Controls;

public class PanelPreviewControl : Control
{
    public static readonly StyledProperty<IEnumerable<LayerViewModel>?> LayersProperty =
        AvaloniaProperty.Register<PanelPreviewControl, IEnumerable<LayerViewModel>?>(nameof(Layers));

    public IEnumerable<LayerViewModel>? Layers
    {
        get => GetValue(LayersProperty);
        set => SetValue(LayersProperty, value);
    }

    static PanelPreviewControl()
    {
        AffectsRender<PanelPreviewControl>(LayersProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LayersProperty)
        {
            if (change.OldValue is System.Collections.Specialized.INotifyCollectionChanged oldColl)
                oldColl.CollectionChanged -= OnLayersCollectionChanged;
            
            if (change.NewValue is System.Collections.Specialized.INotifyCollectionChanged newColl)
                newColl.CollectionChanged += OnLayersCollectionChanged;

            UpdateLayerSubscriptions();
            InvalidateVisual();
        }
    }

    private void OnLayersCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        UpdateLayerSubscriptions();
        InvalidateVisual();
    }

    private void UpdateLayerSubscriptions()
    {
        if (Layers == null) return;
        foreach (var layer in Layers)
        {
            layer.LayerChanged -= OnLayerDataChanged;
            layer.LayerChanged += OnLayerDataChanged;
        }
    }

    private void OnLayerDataChanged(object? sender, EventArgs e)
    {
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        context.FillRectangle(new SolidColorBrush(Color.Parse("#08111B")), bounds);

        var layers = Layers?.ToList();
        if (layers == null || !layers.Any()) return;

        // Isometric projection settings
        double azimuth = 0.6;
        double elevation = 0.4;
        
        double cosA = Math.Cos(azimuth);
        double sinA = Math.Sin(azimuth);
        double cosE = Math.Cos(elevation);
        double sinE = Math.Sin(elevation);

        // Panel size in mm (fixed visual size)
        double panelSize = 400; 
        double centerX = bounds.Width / 2;
        double centerY = bounds.Height / 2;
        
        // Scale to fit
        double totalThickness = layers.Sum(l => l.Thickness);
        double scale = Math.Min(bounds.Width, bounds.Height) / (panelSize * 1.5);

        double currentZ = 0;

        foreach (var layer in layers)
        {
            var brush = GetLayerBrush(layer);
            var pen = new Pen(new SolidColorBrush(layer.IsIncludedInCalculation ? Colors.White : Colors.Gray, layer.IsIncludedInCalculation ? 0.2 : 0.45), 1);
            
            // Draw box (stacking layers along -Y in local space, which is depth)
            DrawLayerBox(context, layer, currentZ, panelSize, scale, centerX, centerY, cosA, sinA, cosE, sinE, brush, pen);
            
            currentZ += layer.Thickness;
        }
    }

    private void DrawLayerBox(DrawingContext context, LayerViewModel layer, double zStart, double size, double scale, 
        double cx, double cy, double cosA, double sinA, double cosE, double sinE, IBrush fill, IPen stroke)
    {
        double zEnd = zStart + layer.Thickness;
        
        // 8 corners of the box
        // Points: 0-3 front face, 4-7 back face
        Point3D[] corners = new Point3D[8];
        double hs = size / 2;
        
        // Local coordinates (X, Y, Z) where Z is depth
        corners[0] = new Point3D(-hs, -hs, zStart);
        corners[1] = new Point3D(hs, -hs, zStart);
        corners[2] = new Point3D(hs, hs, zStart);
        corners[3] = new Point3D(-hs, hs, zStart);
        
        corners[4] = new Point3D(-hs, -hs, zEnd);
        corners[5] = new Point3D(hs, -hs, zEnd);
        corners[6] = new Point3D(hs, hs, zEnd);
        corners[7] = new Point3D(-hs, hs, zEnd);

        Point[] screenPoints = corners.Select(p => Project(p, cx, cy, scale, cosA, sinA, cosE, sinE)).ToArray();

        // Draw faces (back to front or logic-based)
        // For a panel viewed from front-top-right:
        // Left, Bottom, Back, Right, Top, Front
        
        // Right face (1,2,6,5)
        DrawFace(context, new[] { screenPoints[1], screenPoints[2], screenPoints[6], screenPoints[5] }, fill, stroke);
        // Top face (0,1,5,4)
        DrawFace(context, new[] { screenPoints[0], screenPoints[1], screenPoints[5], screenPoints[4] }, fill, stroke);
        // Front face (0,1,2,3)
        DrawFace(context, new[] { screenPoints[0], screenPoints[1], screenPoints[2], screenPoints[3] }, fill, stroke);

        // If perforated, draw holes on front face
        if (layer.IsPerforated)
        {
            DrawHoles(context, screenPoints[0], screenPoints[1], screenPoints[2], screenPoints[3], layer, scale, cx, cy, cosA, sinA, cosE, sinE);
        }
    }

    private void DrawFace(DrawingContext context, Point[] points, IBrush fill, IPen stroke)
    {
        var geom = new StreamGeometry();
        using (var ctx = geom.Open())
        {
            ctx.BeginFigure(points[0], true);
            for (int i = 1; i < points.Length; i++) ctx.LineTo(points[i]);
            ctx.EndFigure(true);
        }
        context.DrawGeometry(fill, stroke, geom);
    }

    private void DrawHoles(DrawingContext context, Point p0, Point p1, Point p2, Point p3, LayerViewModel layer,
        double scale, double cx, double cy, double cosA, double sinA, double cosE, double sinE)
    {
        var holeBrush = new SolidColorBrush(Colors.Black, 0.7);
        
        // If we have custom DXF entities, draw them
        if (layer.DxfEntities != null && layer.DxfEntities.Any())
        {
            DrawCustomDxfEntities(context, layer.DxfEntities, size: 400, scale, cx, cy, cosA, sinA, cosE, sinE);
            return;
        }

        // Fallback: Draw a dynamic grid based on hole pitch and layout.
        double d = layer.HoleDiameter;
        double ratio = layer.OpenAreaRatio / 100.0;
        if (ratio <= 0) return;

        double spacing = layer.HoleSpacing;
        if (spacing <= 0)
        {
            spacing = Math.Sqrt(Math.PI * d * d / (4 * ratio));
        }
        if (spacing <= 0) spacing = 10;
        
        double panelWidth = 600; // Standard panel reference
        double rowSpacing = layer.HolePatternName == "Şaşırtmalı"
            ? spacing * Math.Sqrt(3.0) / 2.0
            : spacing;
        int countX = Math.Clamp((int)(panelWidth / spacing), 2, 30);
        int countY = Math.Clamp((int)(panelWidth / rowSpacing), 2, 30);
        
        double visualRadius = (d / 2.0) * scale * 2.0; // Scaled for visibility
        visualRadius = Math.Clamp(visualRadius, 1, 20);

        for (int row = 1; row < countY; row++)
        {
            for (int col = 1; col < countX; col++)
            {
                double offset = layer.HolePatternName == "Şaşırtmalı" && row % 2 == 1 ? 0.5 : 0.0;
                double u = (col + offset) / countX;
                double v = (double)row / countY;
                if (u >= 1.0) continue;
                
                double x = (1 - u) * (1 - v) * p0.X + u * (1 - v) * p1.X + u * v * p2.X + (1 - u) * v * p3.X;
                double y = (1 - u) * (1 - v) * p0.Y + u * (1 - v) * p1.Y + u * v * p2.Y + (1 - u) * v * p3.Y;
                
                context.DrawEllipse(holeBrush, null, new Point(x, y), visualRadius, visualRadius);
            }
        }
    }

    private void DrawCustomDxfEntities(DrawingContext context, IReadOnlyList<DxfEntity> entities, double size,
        double scale, double cx, double cy, double cosA, double sinA, double cosE, double sinE)
    {
        var shapes = entities
            .Select(e => new DxfPreviewShape(e.Type, GetEntityPoints(e), GetEntityClosed(e)))
            .Where(s => s.Points.Count >= 2)
            .ToList();
        if (shapes.Count == 0) return;

        var extents = GetExtents(shapes.SelectMany(s => s.Points));
        double width = Math.Max(extents.MaxX - extents.MinX, 1e-6);
        double height = Math.Max(extents.MaxY - extents.MinY, 1e-6);
        double rawToLocal = size / Math.Max(width, height);
        double midX = (extents.MinX + extents.MaxX) / 2.0;
        double midY = (extents.MinY + extents.MaxY) / 2.0;

        var closedShapes = shapes
            .Where(s => s.Closed && s.Points.Count >= 3)
            .Select(s => (Shape: s, Area: Math.Abs(CalculateArea(s.Points))))
            .Where(x => x.Area > 1e-6)
            .OrderByDescending(x => x.Area)
            .ToList();
        var boundary = closedShapes.FirstOrDefault().Shape;
        var boundaryPen = new Pen(new SolidColorBrush(Colors.White, 0.35), 1);
        var holeBrush = new SolidColorBrush(Colors.Black, 0.72);
        var holePen = new Pen(new SolidColorBrush(Colors.White, 0.18), 0.8);
        var guidePen = new Pen(new SolidColorBrush(Color.Parse("#0E2133"), 0.75), 1.1);

        foreach (var shape in shapes)
        {
            var projected = shape.Points
                .Select(p =>
                {
                    double x = (p.X - midX) * rawToLocal;
                    double y = (p.Y - midY) * rawToLocal;
                    return Project(new Point3D(x, y, 0), cx, cy, scale, cosA, sinA, cosE, sinE);
                })
                .ToArray();

            if (projected.Length < 2) continue;

            bool isBoundary = ReferenceEquals(shape, boundary);
            if (shape.Closed && projected.Length >= 3)
            {
                DrawPath(context, projected, close: true, fill: isBoundary ? null : holeBrush, stroke: isBoundary ? boundaryPen : holePen);
            }
            else
            {
                DrawPath(context, projected, close: false, fill: null, stroke: guidePen);
            }
        }
    }

    private static void DrawPath(DrawingContext context, IReadOnlyList<Point> points, bool close, IBrush? fill, IPen? stroke)
    {
        var geom = new StreamGeometry();
        using (var ctx = geom.Open())
        {
            ctx.BeginFigure(points[0], fill != null);
            for (int i = 1; i < points.Count; i++)
                ctx.LineTo(points[i]);
            ctx.EndFigure(close);
        }
        context.DrawGeometry(fill, stroke, geom);
    }

    private static (double MinX, double MaxX, double MinY, double MaxY) GetExtents(IEnumerable<(double X, double Y)> points)
    {
        var list = points.ToList();
        return (list.Min(p => p.X), list.Max(p => p.X), list.Min(p => p.Y), list.Max(p => p.Y));
    }

    private static double CalculateArea(IReadOnlyList<(double X, double Y)> points)
    {
        double area = 0;
        for (int i = 0; i < points.Count; i++)
        {
            int j = (i + 1) % points.Count;
            area += points[i].X * points[j].Y;
            area -= points[j].X * points[i].Y;
        }
        return area / 2.0;
    }

    private static bool GetEntityClosed(DxfEntity entity)
    {
        if (entity.Type is "CIRCLE") return true;
        if (entity.Type is "ELLIPSE")
        {
            double start = GetDxfDouble(entity.Pairs, "41");
            double end = GetDxfDouble(entity.Pairs, "42");
            return Math.Abs((end - start) - Math.PI * 2.0) < 0.01 || end <= start;
        }
        if (entity.Type is "SPLINE")
        {
            int flag = (int)GetDxfDouble(entity.Pairs, "70");
            var points = GetEntityPoints(entity);
            return (flag & 1) == 1 || IsNearlyClosed(points);
        }
        if (entity.Type is "LWPOLYLINE" or "POLYLINE")
        {
            int flag = (int)GetDxfDouble(entity.Pairs, "70");
            return (flag & 1) == 1 || IsNearlyClosed(GetEntityPoints(entity));
        }
        if (entity.Type is "3DFACE") return true;
        return false;
    }

    private static bool IsNearlyClosed(IReadOnlyList<(double X, double Y)> points)
    {
        if (points.Count < 3) return false;
        var first = points[0];
        var last = points[^1];
        double dx = first.X - last.X;
        double dy = first.Y - last.Y;
        return Math.Sqrt(dx * dx + dy * dy) < 1e-6;
    }

    private static List<(double X, double Y)> GetEntityPoints(DxfEntity entity)
    {
        return entity.Type switch
        {
            "CIRCLE" => GetCirclePoints(entity.Pairs),
            "ARC" => GetArcPoints(entity.Pairs),
            "ELLIPSE" => GetEllipsePoints(entity.Pairs),
            "SPLINE" => GetSplinePoints(entity.Pairs),
            _ => GetPolylinePoints(entity.Pairs)
        };
    }

    private static List<(double X, double Y)> GetCirclePoints(List<DxfPair> pairs)
    {
        double x = GetDxfDouble(pairs, "10");
        double y = GetDxfDouble(pairs, "20");
        double r = GetDxfDouble(pairs, "40");
        var points = new List<(double X, double Y)>();
        if (r <= 0) return points;
        const int segments = 48;
        for (int i = 0; i < segments; i++)
        {
            double angle = 2.0 * Math.PI * i / segments;
            points.Add((x + r * Math.Cos(angle), y + r * Math.Sin(angle)));
        }
        return points;
    }

    private static List<(double X, double Y)> GetArcPoints(List<DxfPair> pairs)
    {
        double x = GetDxfDouble(pairs, "10");
        double y = GetDxfDouble(pairs, "20");
        double r = GetDxfDouble(pairs, "40");
        double start = GetDxfDouble(pairs, "50") * Math.PI / 180.0;
        double end = GetDxfDouble(pairs, "51") * Math.PI / 180.0;
        var points = new List<(double X, double Y)>();
        if (r <= 0) return points;
        if (end < start) end += Math.PI * 2.0;
        int segments = Math.Clamp((int)Math.Ceiling((end - start) / (Math.PI / 24.0)), 6, 96);
        for (int i = 0; i <= segments; i++)
        {
            double angle = start + (end - start) * i / segments;
            points.Add((x + r * Math.Cos(angle), y + r * Math.Sin(angle)));
        }
        return points;
    }

    private static List<(double X, double Y)> GetEllipsePoints(List<DxfPair> pairs)
    {
        double cx = GetDxfDouble(pairs, "10");
        double cy = GetDxfDouble(pairs, "20");
        double majorX = GetDxfDouble(pairs, "11");
        double majorY = GetDxfDouble(pairs, "21");
        double ratio = GetDxfDouble(pairs, "40");
        double start = GetDxfDouble(pairs, "41");
        double end = GetDxfDouble(pairs, "42");
        if (ratio <= 0) ratio = 1.0;
        if (end <= start) end += Math.PI * 2.0;
        double minorX = -majorY * ratio;
        double minorY = majorX * ratio;
        int segments = Math.Clamp((int)Math.Ceiling((end - start) / (Math.PI / 48.0)), 24, 144);
        var points = new List<(double X, double Y)>();
        for (int i = 0; i <= segments; i++)
        {
            double t = start + (end - start) * i / segments;
            points.Add((
                cx + majorX * Math.Cos(t) + minorX * Math.Sin(t),
                cy + majorY * Math.Cos(t) + minorY * Math.Sin(t)));
        }
        return points;
    }

    private static List<(double X, double Y)> GetSplinePoints(List<DxfPair> pairs)
    {
        var fitPoints = GetPairedPoints(pairs, "11", "21");
        return fitPoints.Count >= 2 ? fitPoints : GetPairedPoints(pairs, "10", "20");
    }

    private static List<(double X, double Y)> GetPolylinePoints(List<DxfPair> pairs)
    {
        var points = new List<(double X, double Y)>();
        double? x = null;
        foreach (var p in pairs)
        {
            if (p.Code == "10")
                x = TryParseDxfDouble(p.Value, out var parsedX) ? parsedX : null;
            if (p.Code == "20" && x.HasValue)
            {
                if (TryParseDxfDouble(p.Value, out var parsedY))
                    points.Add((x.Value, parsedY));
                x = null;
            }
        }
        return points;
    }

    private static List<(double X, double Y)> GetPairedPoints(List<DxfPair> pairs, string xCode, string yCode)
    {
        var points = new List<(double X, double Y)>();
        double? x = null;
        foreach (var pair in pairs)
        {
            if (pair.Code == xCode && TryParseDxfDouble(pair.Value, out var parsedX))
                x = parsedX;
            else if (pair.Code == yCode && x.HasValue &&
                     TryParseDxfDouble(pair.Value, out var parsedY))
            {
                points.Add((x.Value, parsedY));
                x = null;
            }
        }
        return points;
    }

    private static double GetDxfDouble(List<DxfPair> pairs, string code)
    {
        var pair = pairs.FirstOrDefault(p => p.Code == code);
        return pair != null && TryParseDxfDouble(pair.Value, out var value)
            ? value
            : 0;
    }

    private static bool TryParseDxfDouble(string value, out double parsed)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) &&
            double.IsFinite(parsed))
        {
            return true;
        }

        parsed = 0;
        return false;
    }

    private sealed record DxfPreviewShape(string Type, List<(double X, double Y)> Points, bool Closed);

    private Point Project(Point3D p, double cx, double cy, double scale, double cosA, double sinA, double cosE, double sinE)
    {
        // Exaggerate Z (depth) for better visibility of thin layers
        double zScale = 2.0;
        double pZ = p.Z * zScale;

        // Rotate around Y
        double x1 = p.X * cosA - pZ * sinA;
        double z1 = p.X * sinA + pZ * cosA;
        
        // Rotate around X
        double y2 = p.Y * cosE - z1 * sinE;
        double z2 = p.Y * sinE + z1 * cosE;
        
        return new Point(cx + x1 * scale, cy + y2 * scale);
    }

    private IBrush GetLayerBrush(LayerViewModel layer)
    {
        double factor = layer.IsIncludedInCalculation ? 1.0 : 0.25;
        if (layer.IsAir) return new SolidColorBrush(Colors.LightBlue, 0.1 * factor);
        if (layer.IsPorous) return new SolidColorBrush(Color.Parse("#4A90E2"), 0.5 * factor);
        if (layer.IsFabric) return new SolidColorBrush(Color.Parse("#D8EEFF"), 0.35 * factor);
        if (layer.IsPerforated) return new SolidColorBrush(Color.Parse("#70C8A8"), 0.8 * factor);
        return new SolidColorBrush(Colors.Gray, 0.5 * factor);
    }

    private record Point3D(double X, double Y, double Z);
}
