using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Kst.Acoustics.Core.RoomModes;

namespace Kst.Acoustics.App.Controls;

public sealed class RoomModePressureControl : Control
{
    private double _zoom = 1.0;
    private double _azimuth = 0.8;
    private double _elevation = 0.5;
    private Point _pan;
    private bool _isRotating;
    private bool _isPanning;
    private Point _pointerDownPoint;
    private Point _panStart;

    public static readonly StyledProperty<List<PressurePoint>?> PointsProperty =
        AvaloniaProperty.Register<RoomModePressureControl, List<PressurePoint>?>(nameof(Points));

    public static readonly StyledProperty<double> RoomLengthXProperty =
        AvaloniaProperty.Register<RoomModePressureControl, double>(nameof(RoomLengthX), 5.0);

    public static readonly StyledProperty<double> RoomLengthYProperty =
        AvaloniaProperty.Register<RoomModePressureControl, double>(nameof(RoomLengthY), 4.0);

    public static readonly StyledProperty<double> RoomLengthZProperty =
        AvaloniaProperty.Register<RoomModePressureControl, double>(nameof(RoomLengthZ), 3.0);

    public List<PressurePoint>? Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public double RoomLengthX
    {
        get => GetValue(RoomLengthXProperty);
        set => SetValue(RoomLengthXProperty, value);
    }

    public double RoomLengthY
    {
        get => GetValue(RoomLengthYProperty);
        set => SetValue(RoomLengthYProperty, value);
    }

    public double RoomLengthZ
    {
        get => GetValue(RoomLengthZProperty);
        set => SetValue(RoomLengthZProperty, value);
    }

    static RoomModePressureControl()
    {
        AffectsRender<RoomModePressureControl>(PointsProperty, RoomLengthXProperty, RoomLengthYProperty, RoomLengthZProperty);
    }

    public RoomModePressureControl()
    {
        ClipToBounds = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        context.FillRectangle(new SolidColorBrush(Color.Parse("#0C1624")), bounds);

        if (RoomLengthX <= 0 || RoomLengthY <= 0 || RoomLengthZ <= 0) return;

        // Projeksiyon kurulumu
        double modelMax = Math.Max(RoomLengthX, Math.Max(RoomLengthY, RoomLengthZ));
        double scale = Math.Min(bounds.Width, bounds.Height) * 0.6 / modelMax * _zoom;
        Point center = bounds.Center + _pan;

        // 1. Wireframe Kutu (Oda)
        DrawRoomWireframe(context, center, scale);

        // 2. Basınç Noktaları
        if (Points != null)
        {
            DrawPressurePoints(context, center, scale);
        }
    }

    private void DrawRoomWireframe(DrawingContext context, Point center, double scale)
    {
        var pen = new Pen(new SolidColorBrush(Color.Parse("#446688")), 1.0);
        
        // 8 Köşe
        Point3D[] corners = {
            new(0, 0, 0), new(RoomLengthX, 0, 0), new(RoomLengthX, RoomLengthY, 0), new(0, RoomLengthY, 0),
            new(0, 0, RoomLengthZ), new(RoomLengthX, 0, RoomLengthZ), new(RoomLengthX, RoomLengthY, RoomLengthZ), new(0, RoomLengthY, RoomLengthZ)
        };

        Point[] projected = corners.Select(c => Project(c, center, scale)).ToArray();

        // Alt yüzey
        DrawLine(context, pen, projected[0], projected[1]);
        DrawLine(context, pen, projected[1], projected[2]);
        DrawLine(context, pen, projected[2], projected[3]);
        DrawLine(context, pen, projected[3], projected[0]);

        // Üst yüzey
        DrawLine(context, pen, projected[4], projected[5]);
        DrawLine(context, pen, projected[5], projected[6]);
        DrawLine(context, pen, projected[6], projected[7]);
        DrawLine(context, pen, projected[7], projected[4]);

        // Dikmeler
        DrawLine(context, pen, projected[0], projected[4]);
        DrawLine(context, pen, projected[1], projected[5]);
        DrawLine(context, pen, projected[2], projected[6]);
        DrawLine(context, pen, projected[3], projected[7]);
    }

    private void DrawPressurePoints(DrawingContext context, Point center, double scale)
    {
        // Performans için noktaları derinliğe göre sıralayabiliriz ama şeffaflık kullanacağımız için
        // basitçe çizebiliriz. Yüzey noktaları olduğu için karışıklık az olacaktır.
        
        // Nokta boyutu (zoom ile ölçeklenir)
        double dotSize = Math.Max(2, 4 * _zoom);

        foreach (var p in Points!)
        {
            var screenPt = Project(new Point3D(p.X, p.Y, p.Z), center, scale);
            var color = GetPressureColor(p.Pressure);
            context.FillRectangle(new SolidColorBrush(color), new Rect(screenPt.X - dotSize/2, screenPt.Y - dotSize/2, dotSize, dotSize));
        }
    }

    private void DrawLine(DrawingContext context, IPen pen, Point p1, Point p2)
    {
        context.DrawLine(pen, p1, p2);
    }

    private Point Project(Point3D p, Point center, double scale)
    {
        // Ortaya al (model koordinatlarını merkeze çek)
        double x = p.X - RoomLengthX / 2;
        double y = p.Y - RoomLengthY / 2;
        double z = p.Z - RoomLengthZ / 2;

        // Azimuth (Y etrafında dönme)
        double cosA = Math.Cos(_azimuth);
        double sinA = Math.Sin(_azimuth);
        double x1 = x * cosA - y * sinA;
        double y1 = x * sinA + y * cosA;

        // Elevation (X etrafında dönme)
        double cosE = Math.Cos(_elevation);
        double sinE = Math.Sin(_elevation);
        double y2 = y1 * cosE - z * sinE;
        double z2 = y1 * sinE + z * cosE;

        return new Point(center.X + x1 * scale, center.Y - z2 * scale);
    }

    private Color GetPressureColor(double p)
    {
        // p: -1 ile +1 arası
        // +1 -> #E05C5C (kırmızı)
        //  0 -> #1A2632 (koyu)
        // -1 -> #3A88C8 (mavi)
        
        if (p > 0)
        {
            double t = Math.Min(p, 1.0);
            return Color.FromRgb(
                (byte)(0x1A + (0xE0 - 0x1A) * t),
                (byte)(0x26 + (0x5C - 0x26) * t),
                (byte)(0x32 + (0x5C - 0x32) * t) // Kırmızıya giderken biraz açılıyor
            );
        }
        else
        {
            double t = Math.Min(-p, 1.0);
            return Color.FromRgb(
                (byte)(0x1A + (0x3A - 0x1A) * t),
                (byte)(0x26 + (0x88 - 0x26) * t),
                (byte)(0x32 + (0xC8 - 0x32) * t)
            );
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        _pointerDownPoint = e.GetPosition(this);
        var props = e.GetCurrentPoint(this).Properties;
        
        if (props.IsRightButtonPressed) _isRotating = true;
        else if (props.IsLeftButtonPressed) { _isPanning = true; _panStart = _pan; }
        
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        var pos = e.GetPosition(this);
        var delta = pos - _pointerDownPoint;

        if (_isRotating)
        {
            _azimuth += delta.X * 0.01;
            _elevation = Math.Clamp(_elevation + delta.Y * 0.01, -Math.PI / 2 + 0.1, Math.PI / 2 - 0.1);
            _pointerDownPoint = pos;
            InvalidateVisual();
        }
        else if (_isPanning)
        {
            _pan = _panStart + delta;
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        _isRotating = false;
        _isPanning = false;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        double factor = e.Delta.Y > 0 ? 1.1 : 0.9;
        _zoom = Math.Clamp(_zoom * factor, 0.2, 5.0);
        InvalidateVisual();
        e.Handled = true;
    }

    private record struct Point3D(double X, double Y, double Z);
}
