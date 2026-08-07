using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Measurement.Data;
using Kst.Acoustics.Core.Measurement.Spatial;

namespace Kst.Acoustics.App.Controls;

public sealed class MeasurementRoomViewport : Control
{
    private const double MinZoom = 0.05;
    private const double MaxZoom = 5000;
    private const double EdgeMargin = 28;

    private double _azimuth = 0.78;
    private double _elevation = 0.45;
    private double _zoom = 60;
    private Point _pan;

    private bool _isRotating;
    private bool _isPanning;
    private Point _pointerDownPoint;
    private Point _panStart;
    private double _azStart;
    private double _elStart;

    public MeasurementRoomViewport()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    // ── Bindable properties ─────────────────────────────────────────────

    public static readonly StyledProperty<RoomGeometry?> GeometryProperty =
        AvaloniaProperty.Register<MeasurementRoomViewport, RoomGeometry?>(nameof(Geometry));

    public RoomGeometry? Geometry
    {
        get => GetValue(GeometryProperty);
        set => SetValue(GeometryProperty, value);
    }

    public static readonly StyledProperty<IReadOnlyList<SetupPoint>?> PointsProperty =
        AvaloniaProperty.Register<MeasurementRoomViewport, IReadOnlyList<SetupPoint>?>(nameof(Points));

    public IReadOnlyList<SetupPoint>? Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public static readonly StyledProperty<string?> SelectedPointLabelProperty =
        AvaloniaProperty.Register<MeasurementRoomViewport, string?>(nameof(SelectedPointLabel));

    public string? SelectedPointLabel
    {
        get => GetValue(SelectedPointLabelProperty);
        set => SetValue(SelectedPointLabelProperty, value);
    }

    public static readonly StyledProperty<bool> PlacementActiveProperty =
        AvaloniaProperty.Register<MeasurementRoomViewport, bool>(nameof(PlacementActive));

    public bool PlacementActive
    {
        get => GetValue(PlacementActiveProperty);
        set => SetValue(PlacementActiveProperty, value);
    }

    public event EventHandler<RoomPickedEventArgs>? RoomPicked;
    public event EventHandler<string>? PointClicked;

    static MeasurementRoomViewport()
    {
        AffectsRender<MeasurementRoomViewport>(
            GeometryProperty, PointsProperty, SelectedPointLabelProperty, PlacementActiveProperty);
    }

    // ── Pointer interaction ─────────────────────────────────────────────

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);
        _pointerDownPoint = pos;
        var props = e.GetCurrentPoint(this).Properties;

        if (props.IsRightButtonPressed)
        {
            _isRotating = true;
            _azStart = _azimuth;
            _elStart = _elevation;
            e.Pointer.Capture(this);
        }
        else if (props.IsMiddleButtonPressed || (props.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Shift)))
        {
            _isPanning = true;
            _panStart = _pan;
            e.Pointer.Capture(this);
        }
        else if (props.IsLeftButtonPressed)
        {
            var hitLabel = HitTestPoint(pos);
            if (hitLabel != null)
            {
                PointClicked?.Invoke(this, hitLabel);
            }
            else if (PlacementActive && TryPickGround(pos, out var world))
            {
                RoomPicked?.Invoke(this, new RoomPickedEventArgs(world));
            }
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_isRotating && !_isPanning) return;
        var pos = e.GetPosition(this);
        var dx = pos.X - _pointerDownPoint.X;
        var dy = pos.Y - _pointerDownPoint.Y;

        if (_isRotating)
        {
            _azimuth = _azStart + dx * 0.01;
            _elevation = Math.Clamp(_elStart - dy * 0.01, -Math.PI / 2 + 0.02, Math.PI / 2 - 0.02);
            InvalidateVisual();
        }
        else if (_isPanning)
        {
            _pan = new Point(_panStart.X + dx, _panStart.Y + dy);
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _isRotating = false;
        _isPanning = false;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var factor = e.Delta.Y > 0 ? 1.15 : 1.0 / 1.15;
        _zoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);
        InvalidateVisual();
    }

    // ── Rendering ───────────────────────────────────────────────────────

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = new Rect(Bounds.Size);
        var bg = new SolidColorBrush(Color.FromRgb(0x09, 0x0F, 0x15));
        context.FillRectangle(bg, bounds);

        var geometry = Geometry;
        if (geometry == null || bounds.Width < 10 || bounds.Height < 10)
        {
            DrawHint(context, bounds, "Oda boyutu girip 'Kutu Üret' veya DXF yükleyin.");
            return;
        }

        FitZoomIfNeeded(geometry, bounds);

        var center = geometry.Center;
        var camera = new Camera(_azimuth, _elevation, center, _zoom, bounds, _pan);

        DrawGroundGrid(context, geometry, camera);
        DrawSurfaces(context, geometry, camera);
        DrawAxes(context, geometry, camera);
        DrawSetupPoints(context, camera);
        DrawDistanceLine(context, camera);
        DrawLegend(context, bounds, geometry);
    }

    private bool _zoomFit;

    private void FitZoomIfNeeded(RoomGeometry geom, Rect bounds)
    {
        if (_zoomFit) return;
        var diag = Math.Max(geom.DiagonalLength, 1e-3);
        var avail = Math.Min(bounds.Width, bounds.Height) - EdgeMargin * 2;
        if (avail > 50)
        {
            _zoom = Math.Clamp(avail / diag, MinZoom, MaxZoom);
            _zoomFit = true;
        }
    }

    public void ResetView()
    {
        _azimuth = 0.78;
        _elevation = 0.45;
        _zoomFit = false;
        _pan = default;
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GeometryProperty)
        {
            _zoomFit = false;
            _pan = default;
        }
    }

    private void DrawSurfaces(DrawingContext ctx, RoomGeometry geom, Camera cam)
    {
        var fillBrush = new SolidColorBrush(Color.FromArgb(64, 0x4E, 0xA8, 0xDE));
        var edgePen = new Pen(new SolidColorBrush(Color.FromArgb(150, 0x4E, 0xA8, 0xDE)), 1);

        var depths = new (Point2D A, Point2D B, Point2D C, double Depth)[geom.Triangles.Count];
        for (int i = 0; i < geom.Triangles.Count; i++)
        {
            var t = geom.Triangles[i];
            var a = cam.Project(t.A);
            var b = cam.Project(t.B);
            var c = cam.Project(t.C);
            var d = (a.Depth + b.Depth + c.Depth) / 3.0;
            depths[i] = (a, b, c, d);
        }
        Array.Sort(depths, (x, y) => y.Depth.CompareTo(x.Depth));

        var sg = new StreamGeometry();
        using (var dc = sg.Open())
        {
            foreach (var t in depths)
            {
                dc.BeginFigure(new Point(t.A.X, t.A.Y), true);
                dc.LineTo(new Point(t.B.X, t.B.Y));
                dc.LineTo(new Point(t.C.X, t.C.Y));
                dc.EndFigure(true);
            }
        }
        ctx.DrawGeometry(fillBrush, edgePen, sg);
    }

    private void DrawGroundGrid(DrawingContext ctx, RoomGeometry geom, Camera cam)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(40, 0x7E, 0x94, 0xA7)), 1);
        double step = ChooseGridStep(Math.Max(geom.Length, geom.Width));
        double x0 = Math.Floor(geom.Min.X / step) * step;
        double x1 = Math.Ceiling(geom.Max.X / step) * step;
        double y0 = Math.Floor(geom.Min.Y / step) * step;
        double y1 = Math.Ceiling(geom.Max.Y / step) * step;

        for (double x = x0; x <= x1 + 1e-6; x += step)
        {
            var p1 = cam.Project(new Point3D(x, y0, geom.Min.Z));
            var p2 = cam.Project(new Point3D(x, y1, geom.Min.Z));
            ctx.DrawLine(pen, new Point(p1.X, p1.Y), new Point(p2.X, p2.Y));
        }
        for (double y = y0; y <= y1 + 1e-6; y += step)
        {
            var p1 = cam.Project(new Point3D(x0, y, geom.Min.Z));
            var p2 = cam.Project(new Point3D(x1, y, geom.Min.Z));
            ctx.DrawLine(pen, new Point(p1.X, p1.Y), new Point(p2.X, p2.Y));
        }
    }

    private static double ChooseGridStep(double span)
    {
        if (span <= 0) return 1.0;
        if (span < 5) return 0.5;
        if (span < 20) return 1.0;
        if (span < 50) return 2.0;
        return 5.0;
    }

    private void DrawAxes(DrawingContext ctx, RoomGeometry geom, Camera cam)
    {
        var len = Math.Max(geom.DiagonalLength * 0.08, 0.5);
        var origin = new Point3D(geom.Min.X, geom.Min.Y, geom.Min.Z);
        var px = geom.Min with { X = origin.X + len };
        var py = geom.Min with { Y = origin.Y + len };
        var pz = geom.Min with { Z = origin.Z + len };

        DrawAxis(ctx, cam, origin, px, "X", Colors.IndianRed);
        DrawAxis(ctx, cam, origin, py, "Y", Colors.MediumSeaGreen);
        DrawAxis(ctx, cam, origin, pz, "Z", Colors.CornflowerBlue);
    }

    private static void DrawAxis(DrawingContext ctx, Camera cam, Point3D o, Point3D p, string label, Color color)
    {
        var pen = new Pen(new SolidColorBrush(color), 1.5);
        var a = cam.Project(o);
        var b = cam.Project(p);
        ctx.DrawLine(pen, new Point(a.X, a.Y), new Point(b.X, b.Y));
        var fmt = new FormattedText(label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            Typeface.Default, 11, new SolidColorBrush(color));
        ctx.DrawText(fmt, new Point(b.X + 3, b.Y + 3));
    }

    private void DrawSetupPoints(DrawingContext ctx, Camera cam)
    {
        var pts = Points;
        if (pts == null || pts.Count == 0) return;

        var selected = SelectedPointLabel;
        foreach (var pt in pts)
        {
            var proj = cam.Project(new Point3D(pt.X, pt.Y, pt.Z));
            DrawMarker(ctx, proj, pt, isSelected: pt.Label == selected);
        }
    }

    private static void DrawMarker(DrawingContext ctx, Point2D proj, SetupPoint pt, bool isSelected)
    {
        var brush = pt.Type == PointType.Loudspeaker
            ? (IBrush)new SolidColorBrush(Color.FromRgb(0xFB, 0x92, 0x3C))
            : new SolidColorBrush(Color.FromRgb(0x4E, 0xA8, 0xDE));

        var pen = isSelected
            ? new Pen(new SolidColorBrush(Color.FromRgb(0xEA, 0xB3, 0x08)), 2.5)
            : new Pen(new SolidColorBrush(Color.FromRgb(0xDC, 0xE7, 0xF0)), 1);

        var p = new Point(proj.X, proj.Y);
        if (pt.Type == PointType.Loudspeaker)
        {
            var size = 9.0;
            var sg = new StreamGeometry();
            using (var dc = sg.Open())
            {
                dc.BeginFigure(new Point(p.X, p.Y - size), true);
                dc.LineTo(new Point(p.X - size * 0.866, p.Y + size * 0.5));
                dc.LineTo(new Point(p.X + size * 0.866, p.Y + size * 0.5));
                dc.EndFigure(true);
            }
            ctx.DrawGeometry(brush, pen, sg);
        }
        else
        {
            ctx.DrawEllipse(brush, pen, p, 7, 7);
        }

        var fmt = new FormattedText(pt.Label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            Typeface.Default, 11,
            new SolidColorBrush(Color.FromRgb(0xDC, 0xE7, 0xF0)));
        ctx.DrawText(fmt, new Point(p.X + 9, p.Y - 6));
    }

    private void DrawDistanceLine(DrawingContext ctx, Camera cam)
    {
        var pts = Points;
        if (pts == null || pts.Count == 0) return;
        var src = pts.FirstOrDefault(p => p.Type == PointType.Loudspeaker);
        var mic = pts.FirstOrDefault(p => p.Type == PointType.Microphone);
        if (src == null || mic == null) return;

        var a3 = new Point3D(src.X, src.Y, src.Z);
        var b3 = new Point3D(mic.X, mic.Y, mic.Z);
        var a = cam.Project(a3);
        var b = cam.Project(b3);

        var pen = new Pen(new SolidColorBrush(Color.FromArgb(180, 0xE0, 0xE0, 0xE0)), 1.2)
        {
            DashStyle = new DashStyle(new double[] { 4, 4 }, 0)
        };
        ctx.DrawLine(pen, new Point(a.X, a.Y), new Point(b.X, b.Y));

        var dx = src.X - mic.X;
        var dy = src.Y - mic.Y;
        var dz = src.Z - mic.Z;
        var dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        var mid = new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);

        var label = $"{dist:F2} m";
        var fmt = new FormattedText(label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            Typeface.Default, 11,
            new SolidColorBrush(Color.FromRgb(0xE6, 0xEE, 0xF6)));
        ctx.DrawText(fmt, new Point(mid.X + 4, mid.Y - 14));
    }

    private static void DrawLegend(DrawingContext ctx, Rect bounds, RoomGeometry geom)
    {
        var fg = new SolidColorBrush(Color.FromRgb(0x7E, 0x94, 0xA7));
        var info = $"{geom.Source}  ·  {geom.Length:F2} × {geom.Width:F2} × {geom.Height:F2} m  ·  {geom.Triangles.Count} üçgen";
        var fmt = new FormattedText(info, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            Typeface.Default, 11, fg);
        ctx.DrawText(fmt, new Point(10, bounds.Height - 22));

        var hint = "Sağ tuş: döndür · Tekerlek: zoom · Shift+sol veya orta: pan";
        var fmt2 = new FormattedText(hint, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            Typeface.Default, 10, fg);
        ctx.DrawText(fmt2, new Point(10, 8));
    }

    private static void DrawHint(DrawingContext ctx, Rect bounds, string text)
    {
        var fmt = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            Typeface.Default, 13,
            new SolidColorBrush(Color.FromRgb(0x7E, 0x94, 0xA7)));
        var pt = new Point(bounds.Width / 2 - fmt.Width / 2, bounds.Height / 2 - fmt.Height / 2);
        ctx.DrawText(fmt, pt);
    }

    // ── Hit testing & picking ───────────────────────────────────────────

    private string? HitTestPoint(Point screen)
    {
        var pts = Points;
        var geom = Geometry;
        if (pts == null || geom == null) return null;

        var cam = new Camera(_azimuth, _elevation, geom.Center, _zoom, new Rect(Bounds.Size), _pan);
        const double radius = 10;
        foreach (var pt in pts)
        {
            var proj = cam.Project(new Point3D(pt.X, pt.Y, pt.Z));
            var dx = proj.X - screen.X;
            var dy = proj.Y - screen.Y;
            if (dx * dx + dy * dy <= radius * radius)
                return pt.Label;
        }
        return null;
    }

    private bool TryPickGround(Point screen, out Point3D world)
    {
        world = default;
        var geom = Geometry;
        if (geom == null) return false;

        var cam = new Camera(_azimuth, _elevation, geom.Center, _zoom, new Rect(Bounds.Size), _pan);
        if (!cam.UnprojectGround(screen, geom.Min.Z, out world)) return false;
        return true;
    }

    // ── Math helpers ────────────────────────────────────────────────────

    private readonly record struct Point2D(double X, double Y, double Depth);

    private sealed class Camera
    {
        private readonly Point3D _center;
        private readonly double _zoom;
        private readonly double _cx;
        private readonly double _cy;
        private readonly (double X, double Y, double Z) _right;
        private readonly (double X, double Y, double Z) _up;
        private readonly (double X, double Y, double Z) _forward;

        public Camera(double azimuth, double elevation, Point3D center, double zoom, Rect bounds, Point pan)
        {
            _center = center;
            _zoom = zoom;
            _cx = bounds.Width / 2 + pan.X;
            _cy = bounds.Height / 2 + pan.Y;

            double ca = Math.Cos(azimuth), sa = Math.Sin(azimuth);
            double ce = Math.Cos(elevation), se = Math.Sin(elevation);

            _forward = (ce * ca, ce * sa, se);
            _right = (-sa, ca, 0);
            _up = (-se * ca, -se * sa, ce);
        }

        public Point2D Project(Point3D p)
        {
            var dx = p.X - _center.X;
            var dy = p.Y - _center.Y;
            var dz = p.Z - _center.Z;
            var sx = _right.X * dx + _right.Y * dy + _right.Z * dz;
            var sy = _up.X * dx + _up.Y * dy + _up.Z * dz;
            var depth = _forward.X * dx + _forward.Y * dy + _forward.Z * dz;
            return new Point2D(_cx + sx * _zoom, _cy - sy * _zoom, depth);
        }

        public bool UnprojectGround(Point screen, double groundZ, out Point3D world)
        {
            world = default;
            var sx = (screen.X - _cx) / _zoom;
            var sy = -(screen.Y - _cy) / _zoom;

            var ox = _center.X + _right.X * sx + _up.X * sy;
            var oy = _center.Y + _right.Y * sx + _up.Y * sy;
            var oz = _center.Z + _right.Z * sx + _up.Z * sy;

            if (Math.Abs(_forward.Z) < 1e-4) return false;
            var t = (groundZ - oz) / _forward.Z;
            world = new Point3D(ox + _forward.X * t, oy + _forward.Y * t, groundZ);
            return true;
        }
    }
}

public sealed class RoomPickedEventArgs : EventArgs
{
    public Point3D World { get; }
    public RoomPickedEventArgs(Point3D world) => World = world;
}
