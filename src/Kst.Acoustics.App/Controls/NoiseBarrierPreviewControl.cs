using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Kst.Acoustics.Core.NoiseBarrier;

namespace Kst.Acoustics.App.Controls;

public class NoiseBarrierPreviewControl : Control
{
    private double _zoom = 1.0;
    private Vector _pan = default;
    private Point? _lastPointer;
    private double _azimuth = -Math.PI / 4.0;
    private double _elevation = 0.62;

    public static readonly StyledProperty<NoiseBarrierProject?> ProjectProperty =
        AvaloniaProperty.Register<NoiseBarrierPreviewControl, NoiseBarrierProject?>(nameof(Project));

    public static readonly StyledProperty<int> SceneRevisionProperty =
        AvaloniaProperty.Register<NoiseBarrierPreviewControl, int>(nameof(SceneRevision));

    static NoiseBarrierPreviewControl()
    {
        AffectsRender<NoiseBarrierPreviewControl>(ProjectProperty, SceneRevisionProperty);
    }

    public NoiseBarrierProject? Project
    {
        get => GetValue(ProjectProperty);
        set => SetValue(ProjectProperty, value);
    }

    public int SceneRevision
    {
        get => GetValue(SceneRevisionProperty);
        set => SetValue(SceneRevisionProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        context.FillRectangle(new SolidColorBrush(Color.Parse("#050C14")), bounds);

        if (Project == null)
            return;

        double scale = 8.0 * _zoom;
        double centerX = bounds.Width / 2.0 + _pan.X;
        double centerY = bounds.Height * 0.70 + _pan.Y;

        Point ProjectPoint(double x, double y, double z)
        {
            double cosA = Math.Cos(_azimuth);
            double sinA = Math.Sin(_azimuth);
            double xr = x * cosA - y * sinA;
            double yr = x * sinA + y * cosA;
            double px = centerX + xr * scale;
            double py = centerY - (z * Math.Sin(_elevation) + yr * Math.Cos(_elevation)) * scale;
            return new Point(px, py);
        }

        DrawGround(context, ProjectPoint);
        DrawBuilding(context, ProjectPoint);
        DrawNoisePaths(context, ProjectPoint);
        DrawFacadeHeatmap(context, ProjectPoint);
        DrawReceivers(context, ProjectPoint);
        DrawBarrier(context, ProjectPoint);
        DrawSources(context, ProjectPoint);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        _zoom = Math.Clamp(_zoom * (e.Delta.Y > 0 ? 1.1 : 0.9), 0.35, 3.0);
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _lastPointer = e.GetPosition(this);
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_lastPointer is not Point last)
            return;

        var current = e.GetPosition(this);
        var delta = current - last;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            _pan += delta;
        }
        else
        {
            _azimuth += delta.X * 0.01;
            _elevation = Math.Clamp(_elevation - delta.Y * 0.008, 0.18, 1.25);
        }

        _lastPointer = current;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _lastPointer = null;
        e.Pointer.Capture(null);
    }

    private void DrawGround(DrawingContext context, Func<double, double, double, Point> project)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(75, 44, 73, 96)), 1);
        for (int i = -10; i <= 10; i++)
        {
            context.DrawLine(pen, project(i * 5, -50, 0), project(i * 5, 25, 0));
            context.DrawLine(pen, project(-55, i * 5, 0), project(55, i * 5, 0));
        }
    }

    private void DrawBuilding(DrawingContext context, Func<double, double, double, Point> project)
    {
        if (Project == null) return;

        var b = Project.Building;
        var p1 = project(b.CenterX - b.Width / 2, b.CenterY, b.BaseZ);
        var p2 = project(b.CenterX + b.Width / 2, b.CenterY, b.BaseZ);
        var p3 = project(b.CenterX + b.Width / 2, b.CenterY + b.Depth, b.BaseZ);
        var p4 = project(b.CenterX - b.Width / 2, b.CenterY + b.Depth, b.BaseZ);
        var p1t = project(b.CenterX - b.Width / 2, b.CenterY, b.BaseZ + b.Height);
        var p2t = project(b.CenterX + b.Width / 2, b.CenterY, b.BaseZ + b.Height);
        var p3t = project(b.CenterX + b.Width / 2, b.CenterY + b.Depth, b.BaseZ + b.Height);
        var p4t = project(b.CenterX - b.Width / 2, b.CenterY + b.Depth, b.BaseZ + b.Height);

        var stroke = new Pen(new SolidColorBrush(Color.Parse("#76A7D4")), 1);
        DrawPolygon(context, [p2, p3, p3t, p2t], Color.FromArgb(170, 36, 55, 82), stroke);
        DrawPolygon(context, [p3, p4, p4t, p3t], Color.FromArgb(135, 28, 44, 68), stroke);
        DrawPolygon(context, [p1, p2, p2t, p1t], Color.FromArgb(225, 22, 43, 74), stroke);
        DrawPolygon(context, [p1t, p2t, p3t, p4t], Color.FromArgb(190, 48, 78, 116), stroke);
    }

    private void DrawReceivers(DrawingContext context, Func<double, double, double, Point> project)
    {
        if (Project == null) return;

        foreach (var receiver in Project.Receivers.Where(r => r.IsEnabled))
        {
            var p = project(receiver.X, receiver.Y, receiver.Z);
            double t = Math.Clamp((receiver.TotalDbAWithBarrier - 30.0) / 50.0, 0.0, 1.0);
            var color = Color.FromArgb(220, (byte)(t * 255), (byte)((1 - t) * 255), 20);
            context.DrawEllipse(new SolidColorBrush(color), null, p, 4, 4);
        }
    }

    private void DrawFacadeHeatmap(DrawingContext context, Func<double, double, double, Point> project)
    {
        if (Project == null || Project.Receivers.Count == 0) return;

        var b = Project.Building;
        double cellWidth = b.Width / Math.Max(1, b.FacadeGridColumns);
        double cellHeight = b.Height / Math.Max(1, b.FacadeGridRows + 1);

        foreach (var receiver in Project.Receivers.Where(r => r.IsEnabled))
        {
            var x1 = receiver.X - cellWidth * 0.42;
            var x2 = receiver.X + cellWidth * 0.42;
            var z1 = Math.Max(b.BaseZ, receiver.Z - cellHeight * 0.42);
            var z2 = Math.Min(b.BaseZ + b.Height, receiver.Z + cellHeight * 0.42);
            var p1 = project(x1, receiver.Y, z1);
            var p2 = project(x2, receiver.Y, z1);
            var p3 = project(x2, receiver.Y, z2);
            var p4 = project(x1, receiver.Y, z2);
            var color = LevelColor(receiver.TotalDbAWithBarrier, 95);
            DrawPolygon(context, [p1, p2, p3, p4], color, new Pen(new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)), 0.5));
        }
    }

    private void DrawNoisePaths(DrawingContext context, Func<double, double, double, Point> project)
    {
        if (Project == null) return;

        var sources = Project.Sources.Where(s => s.IsEnabled).Take(2).ToArray();
        var receivers = Project.Receivers.Where(r => r.IsEnabled).OrderByDescending(r => r.InsertionLoss).Take(8).ToArray();
        if (sources.Length == 0 || receivers.Length == 0)
            return;

        var directPen = new Pen(new SolidColorBrush(Color.FromArgb(70, 255, 230, 120)), 1);
        var diffractionPen = new Pen(new SolidColorBrush(Color.FromArgb(130, 90, 210, 255)), 1.4);
        var primaryBarrier = Project.Barriers
            .Where(b => b.IsEnabled)
            .OrderBy(b => b.PositionY)
            .FirstOrDefault();

        foreach (var source in sources)
        {
            foreach (var receiver in receivers)
            {
                var start = project(source.X, source.Y, source.Z);
                var end = project(receiver.X, receiver.Y, receiver.Z);
                context.DrawLine(directPen, start, end);
            }

            if (primaryBarrier == null) continue;

            var target = receivers[0];
            double denominator = target.Y - source.Y;
            if (Math.Abs(denominator) < 1e-9)
                continue;

            double t = (primaryBarrier.PositionY - source.Y) / denominator;
            if (t <= 0 || t >= 1)
                continue;

            double topZ = primaryBarrier.BaseZ + primaryBarrier.Height;
            double xTop = source.X + t * (target.X - source.X);
            xTop = Math.Clamp(xTop, primaryBarrier.CenterX - primaryBarrier.Length / 2, primaryBarrier.CenterX + primaryBarrier.Length / 2);
            var edge = project(xTop, primaryBarrier.PositionY, topZ);
            context.DrawLine(diffractionPen, project(source.X, source.Y, source.Z), edge);
            context.DrawLine(diffractionPen, edge, project(target.X, target.Y, target.Z));
        }
    }

    private void DrawBarrier(DrawingContext context, Func<double, double, double, Point> project)
    {
        if (Project == null) return;

        var palette = new[]
        {
            (Fill: Color.FromArgb(115, 120, 190, 255), Stroke: Color.Parse("#23A8FF")),
            (Fill: Color.FromArgb(115, 255, 180, 90), Stroke: Color.Parse("#FFB048")),
            (Fill: Color.FromArgb(115, 130, 230, 160), Stroke: Color.Parse("#48E090")),
            (Fill: Color.FromArgb(115, 220, 150, 230), Stroke: Color.Parse("#D070E0"))
        };
        var disabled = (Fill: Color.FromArgb(60, 100, 110, 120), Stroke: Color.FromArgb(150, 130, 140, 150));

        int index = 0;
        foreach (var barrier in Project.Barriers)
        {
            var colors = barrier.IsEnabled ? palette[index % palette.Length] : disabled;
            var p1 = project(barrier.CenterX - barrier.Length / 2, barrier.PositionY, barrier.BaseZ);
            var p2 = project(barrier.CenterX + barrier.Length / 2, barrier.PositionY, barrier.BaseZ);
            var p2t = project(barrier.CenterX + barrier.Length / 2, barrier.PositionY, barrier.BaseZ + barrier.Height);
            var p1t = project(barrier.CenterX - barrier.Length / 2, barrier.PositionY, barrier.BaseZ + barrier.Height);

            DrawPolygon(
                context,
                [p1, p2, p2t, p1t],
                colors.Fill,
                new Pen(new SolidColorBrush(colors.Stroke), 2));
            index++;
        }
    }

    private void DrawSources(DrawingContext context, Func<double, double, double, Point> project)
    {
        if (Project == null) return;

        var sourceBrush = new SolidColorBrush(Color.Parse("#FF7448"));
        var sourcePen = new Pen(sourceBrush, 2);

        foreach (var source in Project.Sources.Where(s => s.IsEnabled))
        {
            var p = project(source.X, source.Y, source.Z);
            if (source.Type == SourceType.Traffic)
            {
                DrawTrafficSource(context, project, source, p);
            }
            else if (source.Type == SourceType.Line)
            {
                var end = project(source.XEnd, source.YEnd, source.Z);
                context.DrawLine(sourcePen, p, end);
                context.DrawEllipse(new SolidColorBrush(Color.FromArgb(70, 255, 116, 72)), null, p, 12, 12);
                context.DrawEllipse(sourceBrush, null, p, 5, 5);
                context.DrawEllipse(sourceBrush, null, end, 4, 4);
            }
            else
            {
                context.DrawEllipse(new SolidColorBrush(Color.FromArgb(70, 255, 116, 72)), null, p, 12, 12);
                context.DrawEllipse(sourceBrush, null, p, 5, 5);
            }
        }
    }

    private static void DrawTrafficSource(
        DrawingContext context,
        Func<double, double, double, Point> project,
        NoiseSource source,
        Point start)
    {
        var end = project(source.XEnd, source.YEnd, source.Z);
        var roadPen = new Pen(new SolidColorBrush(Color.FromArgb(210, 52, 58, 62)), 9);
        var edgePen = new Pen(new SolidColorBrush(Color.FromArgb(230, 255, 172, 92)), 2);
        var centerPen = new Pen(new SolidColorBrush(Color.FromArgb(185, 255, 230, 150)), 1);

        context.DrawLine(roadPen, start, end);
        context.DrawLine(edgePen, start, end);

        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length > 1)
        {
            var nx = -dy / length * 3.0;
            var ny = dx / length * 3.0;
            context.DrawLine(centerPen, new Point(start.X + nx, start.Y + ny), new Point(end.X + nx, end.Y + ny));
            context.DrawLine(centerPen, new Point(start.X - nx, start.Y - ny), new Point(end.X - nx, end.Y - ny));
        }
    }

    private static Color LevelColor(double dbA, byte alpha)
    {
        double t = Math.Clamp((dbA - 35.0) / 45.0, 0.0, 1.0);
        if (t < 0.5)
        {
            double u = t / 0.5;
            return Color.FromArgb(alpha, (byte)(255 * u), 255, 35);
        }

        double v = (t - 0.5) / 0.5;
        return Color.FromArgb(alpha, 255, (byte)(255 * (1.0 - v)), 35);
    }

    private static void DrawPolygon(DrawingContext context, Point[] points, Color fillColor, IPen stroke)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(points[0], true);
            for (int i = 1; i < points.Length; i++)
                ctx.LineTo(points[i]);
            ctx.EndFigure(true);
        }

        context.DrawGeometry(new SolidColorBrush(fillColor), stroke, geometry);
    }
}
