using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.App.Bina.Views;

/// <summary>Üretim öncesindeki seçili CAD verisini salt okunur gösterir.</summary>
public sealed class MimariKatmanOnizlemeCanvas : Control
{
    private static readonly IBrush BackgroundBrush = Brush.Parse("#07111C");
    private static readonly IBrush LineBrush = Brush.Parse("#80AFC9");
    private static readonly IBrush TextBrush = Brush.Parse("#C9DCE9");
    private static readonly Typeface Font = new("Inter");
    // Skia'nın Windows GPU yolunda yüz binlerce ayrık çizgiyi tek path ile
    // çizmek render iş parçacığını kilitleyebilir; UI da compositor'ı bekler.
    // Küçük path grupları aynı çizgileri korur ve bu aşırı maliyeti sınırlar.
    private const int BatchSize = 256;
    private sealed record GeometryBatch(StreamGeometry Geometry, Rect Bounds);
    private readonly List<GeometryBatch> _geometry = new();
    private IReadOnlyList<(Point2D Pos, string Value)> _texts = Array.Empty<(Point2D, string)>();
    private double _minX, _minY, _maxX, _maxY;
    private double _scale = 1;
    private Vector _offset;
    private bool _fitPending = true, _everFitted, _panning;
    private Point _panStart;
    private Vector _panOffset;
    private bool _hasContent;

    public MimariKatmanOnizlemeCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    public void CizimiGoster(BinaKat kat)
    {
        var segments = kat.HamSegmentler.Where(s => Finite(s.A) && Finite(s.B)).ToList();
        _texts = kat.HamMetinler.Where(t => Finite(t.Pos) && !string.IsNullOrWhiteSpace(t.Value)).ToList();
        _hasContent = segments.Count > 0 || _texts.Count > 0;
        _geometry.Clear();
        for (int start = 0; start < segments.Count; start += BatchSize)
        {
            var geometry = new StreamGeometry();
            double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
            using (var context = geometry.Open())
            {
                for (int i = start; i < Math.Min(start + BatchSize, segments.Count); i++)
                {
                    var segment = segments[i];
                    var a = new Point(segment.A.X, -segment.A.Y);
                    var b = new Point(segment.B.X, -segment.B.Y);
                    context.BeginFigure(a, false);
                    context.LineTo(b);
                    context.EndFigure(false);
                    minX = Math.Min(minX, Math.Min(a.X, b.X)); maxX = Math.Max(maxX, Math.Max(a.X, b.X));
                    minY = Math.Min(minY, Math.Min(a.Y, b.Y)); maxY = Math.Max(maxY, Math.Max(a.Y, b.Y));
                }
            }
            _geometry.Add(new GeometryBatch(geometry, new Rect(minX, minY, maxX - minX, maxY - minY)));
        }
        if (_hasContent)
        {
            var points = segments.SelectMany(s => new[] { s.A, s.B }).Concat(_texts.Select(t => t.Pos));
            _minX = double.PositiveInfinity; _minY = double.PositiveInfinity;
            _maxX = double.NegativeInfinity; _maxY = double.NegativeInfinity;
            foreach (var p in points)
            {
                _minX = Math.Min(_minX, p.X); _maxX = Math.Max(_maxX, p.X);
                _minY = Math.Min(_minY, p.Y); _maxY = Math.Max(_maxY, p.Y);
            }
            if (!_everFitted) _fitPending = true;
        }
        InvalidateVisual();
    }

    public void Sigdir() { _fitPending = true; InvalidateVisual(); }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(BackgroundBrush, bounds);
        if (!_hasContent)
        {
            var label = Format("Görüntülenecek veri yok. Soldan katmanları açın.", 14);
            context.DrawText(label, new Point(Math.Max(12, (bounds.Width - label.Width) / 2), Math.Max(12, bounds.Height / 2 - 9)));
            return;
        }
        if (bounds.Width < 10 || bounds.Height < 10) return;
        if (_fitPending)
        {
            _scale = Math.Clamp(Math.Min(Math.Max(1, bounds.Width - 48) / Math.Max(.001, _maxX - _minX),
                Math.Max(1, bounds.Height - 48) / Math.Max(.001, _maxY - _minY)), .000001, 1000000);
            _offset = new Vector(bounds.Width / 2 - (_minX + _maxX) * .5 * _scale,
                bounds.Height / 2 + (_minY + _maxY) * .5 * _scale);
            _fitPending = false; _everFitted = true;
        }
        using (context.PushClip(bounds))
        {
            if (_geometry.Count > 0)
            {
                var visible = new Rect(-_offset.X / _scale, -_offset.Y / _scale,
                    bounds.Width / _scale, bounds.Height / _scale);
                var pen = new Pen(LineBrush, .85 / _scale);
                using (context.PushTransform(new Matrix(_scale, 0, 0, _scale, _offset.X, _offset.Y)))
                    foreach (var batch in _geometry)
                        if (visible.Intersects(batch.Bounds.Inflate(1 / _scale)))
                            context.DrawGeometry(null, pen, batch.Geometry);
            }
            // Uzak görünümde üst üste gelen yazılar yerine her ekran hücresinden
            // bir etiket gösterilir. Yakınlaştırma daha fazla etiketi açığa çıkarır.
            var occupied = new HashSet<(int, int)>();
            int count = 0;
            foreach (var text in _texts)
            {
                double x = text.Pos.X * _scale + _offset.X, y = -text.Pos.Y * _scale + _offset.Y;
                if (x < -50 || y < 0 || x > bounds.Width || y > bounds.Height) continue;
                if (!occupied.Add(((int)Math.Floor(x / 48), (int)Math.Floor(y / 14)))) continue;
                var value = text.Value.Length > 90 ? text.Value[..87] + "…" : text.Value;
                context.DrawText(Format(value, 10), new Point(x, y - 11));
                if (++count >= 1800) break;
            }
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var p = e.GetCurrentPoint(this);
        if (!p.Properties.IsLeftButtonPressed && !p.Properties.IsMiddleButtonPressed) return;
        Focus(); _panning = true; _panStart = p.Position; _panOffset = _offset;
        e.Pointer.Capture(this); Cursor = new Cursor(StandardCursorType.SizeAll); e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_panning) return;
        _offset = _panOffset + (e.GetPosition(this) - _panStart);
        InvalidateVisual(); e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_panning) return;
        _panning = false; e.Pointer.Capture(null); Cursor = new Cursor(StandardCursorType.Hand); e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _panning = false; Cursor = new Cursor(StandardCursorType.Hand);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (!_hasContent) return;
        var p = e.GetPosition(this);
        double next = Math.Clamp(_scale * Math.Pow(1.2, e.Delta.Y), .000001, 1000000);
        double ratio = next / _scale;
        _offset = new Vector(p.X - (p.X - _offset.X) * ratio, p.Y - (p.Y - _offset.Y) * ratio);
        _scale = next; _fitPending = false;
        InvalidateVisual(); e.Handled = true;
    }

    private static bool Finite(Point2D point) => double.IsFinite(point.X) && double.IsFinite(point.Y);
    private static FormattedText Format(string text, double size) => new(text,
        CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Font, size, TextBrush);
}
