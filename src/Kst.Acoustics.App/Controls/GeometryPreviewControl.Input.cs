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
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetCurrentPoint(this);
        _pointerDownPoint = e.GetPosition(this);
        _pointerDownModifiers = e.KeyModifiers;

        // B5 — Kat hizalama modu: sol tık + Top2D → kat offset drag başlatılır
        if (KatHizalamaModu && point.Properties.IsLeftButtonPressed &&
            Projection == GeometryProjectionMode.Top2D)
        {
            var scene = BuildScene(Bounds);
            if (scene is not null && scene.PixelsPerUnit > 1e-9)
            {
                _isAligning = true;
                _alignDragPressScreen = _pointerDownPoint;
                _alignDragPxPerUnit = scene.PixelsPerUnit;
                OnKatHizalamaDragBaslat?.Invoke();
                e.Pointer.Capture(this);
                e.Handled = true;
                return;
            }
        }

        if (point.Properties.IsRightButtonPressed)
        {
            if (OnRightClicked != null && OnRightClicked.Invoke())
            {
                e.Handled = true;
                return;
            }

            if (Projection == GeometryProjectionMode.Top2D &&
                EditablePolygon is { Count: > 3 } && OnVertexDeleted is not null)
            {
                // Top2D + düzenleme: sağ tık ile köşe sil (min 3 köşe kalmalı)
                int hitIdx = HitTestVertex(_pointerDownPoint);
                if (hitIdx >= 0)
                {
                    OnVertexDeleted(hitIdx);
                    e.Pointer.Capture(this);
                    e.Handled = true;
                    return;
                }
            }

            // Top2D'de boş zemine sağ-tık: host'a aktar (context menü için)
            if (Projection == GeometryProjectionMode.Top2D && OnViewportRightClicked is not null &&
                TryGetWorldPoint(_pointerDownPoint, out double rwx, out double rwy))
            {
                OnViewportRightClicked(rwx, rwy, _pointerDownPoint);
                e.Handled = true;
                return;
            }
        }

        if (point.Properties.IsRightButtonPressed && Projection == GeometryProjectionMode.Isometric3D)
        {
            // Right-click drag for 3D rotation
            _isRotating = true;
            e.Pointer.Capture(this);
            e.Handled = true;
        }
        else if (point.Properties.IsLeftButtonPressed)
        {
            // Çift tıklama
            if (e.ClickCount == 2)
            {
                bool edgeHandled = false;
                if (Projection == GeometryProjectionMode.Top2D &&
                    EditablePolygon is { Count: >= 2 } && OnEdgeDoubleClicked is not null)
                {
                    if (TryHitEdge(_pointerDownPoint, out int afterIdx, out double wxEdge, out double wyEdge))
                    {
                        OnEdgeDoubleClicked(afterIdx, wxEdge, wyEdge);
                        e.Pointer.Capture(this);
                        e.Handled = true;
                        edgeHandled = true;
                    }
                }
                
                if (!edgeHandled && OnViewportDoubleClicked != null)
                {
                    if (TryGetWorldPoint(_pointerDownPoint, out double wx, out double wy))
                    {
                        OnViewportDoubleClicked(wx, wy);
                        e.Handled = true;
                    }
                }
                if (e.Handled) return;
            }

            // Vertex / kenar sürükleme (Top2D + EditablePolygon).
            // Sürükleme başında ekran→dünya ölçeği dondurulur ki scene rebuild
            // dönüşümü kaydırıp imleci sıçratmasın.
            if (Projection == GeometryProjectionMode.Top2D && EditablePolygon is { Count: > 0 })
            {
                var dragScene = BuildScene(Bounds);
                if (dragScene is not null && dragScene.PixelsPerUnit > 1e-9)
                {
                    int hitIdx = HitTestVertex(_pointerDownPoint);
                    if (hitIdx >= 0)
                    {
                        OnEditDragStarted?.Invoke();
                        _draggingVertexIndex = hitIdx;
                        _dragPressScreen     = _pointerDownPoint;
                        _dragPxPerUnit       = dragScene.PixelsPerUnit;
                        _dragVertexStartX    = EditablePolygon[hitIdx].X;
                        _dragVertexStartY    = EditablePolygon[hitIdx].Y;
                        e.Pointer.Capture(this);
                        e.Handled = true;
                        return;
                    }

                    if (HitTestEdge(_pointerDownPoint, out int edgeIdx))
                    {
                        OnEditDragStarted?.Invoke();
                        int j = (edgeIdx + 1) % EditablePolygon.Count;
                        _draggingEdgeStart = edgeIdx;
                        _dragPressScreen   = _pointerDownPoint;
                        _dragPxPerUnit     = dragScene.PixelsPerUnit;
                        _edgeDragV0X = EditablePolygon[edgeIdx].X;
                        _edgeDragV0Y = EditablePolygon[edgeIdx].Y;
                        _edgeDragV1X = EditablePolygon[j].X;
                        _edgeDragV1Y = EditablePolygon[j].Y;
                        double edgeX = _edgeDragV1X - _edgeDragV0X;
                        double edgeY = _edgeDragV1Y - _edgeDragV0Y;
                        double edgeLen = Math.Sqrt(edgeX * edgeX + edgeY * edgeY);
                        if (edgeLen > 1e-9)
                        {
                            _edgeDragNormalX = -edgeY / edgeLen;
                            _edgeDragNormalY =  edgeX / edgeLen;
                        }
                        else
                        {
                            _edgeDragNormalX = 0;
                            _edgeDragNormalY = 0;
                        }
                        e.Pointer.Capture(this);
                        e.Handled = true;
                        return;
                    }
                }
            }

            // Bant seçim: Shift + sol sürükleme (host gate'i açıksa) — pan'i gölgelemez.
            if (_pointerDownModifiers.HasFlag(KeyModifiers.Shift) &&
                Projection == GeometryProjectionMode.Top2D &&
                OnBandSelected is not null && (BandSecimAktifMi?.Invoke() ?? false))
            {
                _isBanding = true;
                _bandStartScreen = _pointerDownPoint;
                _bandCurrentScreen = _pointerDownPoint;
                e.Pointer.Capture(this);
                e.Handled = true;
                return;
            }

            // Eleman sürükleme: pan'den önce kontrol et
            if (IsElemanAtPoint is not null
                && Projection == GeometryProjectionMode.Top2D
                && TryGetWorldPoint(_pointerDownPoint, out double ewx, out double ewy)
                && IsElemanAtPoint(ewx, ewy))
            {
                _isDraggingEleman = true;
                _elemanDragMoved  = false;
                bool elemanMultiSelect = _pointerDownModifiers.HasFlag(KeyModifiers.Control) ||
                                         _pointerDownModifiers.HasFlag(KeyModifiers.Meta);
                OnElemanDragStarted?.Invoke(ewx, ewy, elemanMultiSelect);
                e.Pointer.Capture(this);
                e.Handled = true;
            }
            else
            {
                // Left-click drag for panning
                _isPanning = true;
                _panStart = _pan;
                e.Pointer.Capture(this);
                e.Handled = true;
            }
        }
    }
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        OnHoverLeave?.Invoke();
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var position = e.GetPosition(this);
        var delta = position - _pointerDownPoint;

        if (_isBanding)
        {
            _bandCurrentScreen = position;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        // B5 — Kat hizalama drag: kümülatif dünya delta'sını ViewModel'e ilet
        if (_isAligning && _alignDragPxPerUnit > 1e-9)
        {
            double dxScreen = position.X - _alignDragPressScreen.X;
            double dyScreen = position.Y - _alignDragPressScreen.Y;
            double dxWorld = dxScreen / _alignDragPxPerUnit;
            double dyWorld = -dyScreen / _alignDragPxPerUnit;
            OnKatHizalamaDragGuncel?.Invoke(dxWorld, dyWorld);
            e.Handled = true;
            return;
        }

        if (_draggingVertexIndex >= 0 && _dragPxPerUnit > 1e-9)
        {
            // Donmuş ölçekle ekran-delta'yı dünya-delta'ya çevir (Top2D: y ekseni ters)
            double dxScreen = position.X - _dragPressScreen.X;
            double dyScreen = position.Y - _dragPressScreen.Y;
            double dxWorld = dxScreen / _dragPxPerUnit;
            double dyWorld = -dyScreen / _dragPxPerUnit;
            var rawVertex = new Point(_dragVertexStartX + dxWorld, _dragVertexStartY + dyWorld);
            var snappedVertex = SnapPointToBackground(rawVertex, SnapToleranceWorld);
            OnVertexDragged?.Invoke(
                _draggingVertexIndex,
                snappedVertex.X,
                snappedVertex.Y);
            e.Handled = true;
        }
        else if (_draggingEdgeStart >= 0 && EditablePolygon is { } edgePoly && _dragPxPerUnit > 1e-9)
        {
            double dxScreen = position.X - _dragPressScreen.X;
            double dyScreen = position.Y - _dragPressScreen.Y;
            double dxWorld = dxScreen / _dragPxPerUnit;
            double dyWorld = -dyScreen / _dragPxPerUnit;
            double normalDelta = dxWorld * _edgeDragNormalX + dyWorld * _edgeDragNormalY;
            dxWorld = _edgeDragNormalX * normalDelta;
            dyWorld = _edgeDragNormalY * normalDelta;
            (dxWorld, dyWorld) = SnapEdgeDeltaToBackground(dxWorld, dyWorld, SnapToleranceWorld);
            int i = _draggingEdgeStart;
            int j = (i + 1) % edgePoly.Count;
            OnEdgeDragged?.Invoke(
                i, _edgeDragV0X + dxWorld, _edgeDragV0Y + dyWorld,
                j, _edgeDragV1X + dxWorld, _edgeDragV1Y + dyWorld);
            e.Handled = true;
        }
        else if (_isDraggingEleman)
        {
            // Eşik altı hareketi taşıma sayma → salt tıklama elemanı kaydırmasın
            double elemanDist = Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
            if (_elemanDragMoved || elemanDist > ClickThreshold)
            {
                _elemanDragMoved = true;
                if (Projection == GeometryProjectionMode.Top2D
                    && TryGetWorldPoint(position, out double edx, out double edy))
                {
                    OnElemanDragMoved?.Invoke(edx, edy);
                    InvalidateVisual();
                }
            }
            e.Handled = true;
        }
        else if (_isRotating)
        {
            _azimuth += delta.X * RotationSensitivity;
            _elevation = Math.Clamp(_elevation - delta.Y * RotationSensitivity, -Math.PI / 2 + 0.05, Math.PI / 2 - 0.05);
            _pointerDownPoint = position;
            InvalidateVisual();
            e.Handled = true;
        }
        else if (_isPanning)
        {
            _pan = _panStart + delta;
            InvalidateVisual();
            e.Handled = true;
        }

        // Oda çizim modu: rubber-band için fare konumunu ilet
        if (OdaEkleModu > 0 && OnRoomDrawMouseMoved is not null &&
            Projection == GeometryProjectionMode.Top2D &&
            TryGetWorldPoint(position, out double rmx, out double rmy))
        {
            OnRoomDrawMouseMoved(rmx, rmy);
        }

        // UX — Hover hit-test (her zaman, Top2D'de)
        if (OnHoverWorldPoint != null && Projection == GeometryProjectionMode.Top2D &&
            TryGetWorldPoint(position, out double hvx, out double hvy))
        {
            OnHoverWorldPoint(hvx, hvy);
        }

        if (ManuelAciklikModu && OnManualOpeningMouseMoved is not null &&
            Projection == GeometryProjectionMode.Top2D &&
            TryGetWorldPoint(position, out double amx, out double amy))
        {
            OnManualOpeningMouseMoved(amx, amy);
        }
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        // Bant seçim bitişi: köşe çiftini dünya koordinatında host'a bildir.
        if (_isBanding)
        {
            _isBanding = false;
            e.Pointer.Capture(null);
            InvalidateVisual();
            var bandRel = e.GetPosition(this);
            if ((Math.Abs(bandRel.X - _bandStartScreen.X) > 4 || Math.Abs(bandRel.Y - _bandStartScreen.Y) > 4) &&
                TryGetWorldPoint(_bandStartScreen, out double bw1x, out double bw1y) &&
                TryGetWorldPoint(bandRel, out double bw2x, out double bw2y))
            {
                OnBandSelected?.Invoke(bw1x, bw1y, bw2x, bw2y);
            }
            e.Handled = true;
            return;
        }

        // B5 — Kat hizalama drag bitiş
        if (_isAligning)
        {
            _isAligning = false;
            OnKatHizalamaDragBitis?.Invoke();
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }

        if (_isDraggingEleman)
        {
            // Sadece gerçek taşımada commit et (salt tıklama konum/undo bozmasın)
            if (_elemanDragMoved)
            {
                var releasePos = e.GetPosition(this);
                if (Projection == GeometryProjectionMode.Top2D
                    && TryGetWorldPoint(releasePos, out double erx, out double ery))
                {
                    OnElemanDragCommitted?.Invoke(erx, ery);
                }
            }
            else
            {
                // Taşımasız tıklama: seçim ayıklaması için bildir
                bool ms = (e.KeyModifiers | _pointerDownModifiers).HasFlag(KeyModifiers.Control)
                       || (e.KeyModifiers | _pointerDownModifiers).HasFlag(KeyModifiers.Meta);
                OnElemanClickNoDrag?.Invoke(ms);
            }
            _isDraggingEleman = false;
            _elemanDragMoved  = false;
            e.Pointer.Capture(null);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_draggingVertexIndex >= 0)
        {
            _draggingVertexIndex = -1;
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }

        if (_draggingEdgeStart >= 0)
        {
            _draggingEdgeStart = -1;
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }

        if (_isRotating)
        {
            _isRotating = false;
            e.Pointer.Capture(null);
            e.Handled = true;
            // Döndürme bitti: LOD'da atlanan kablo/eleman katmanlarını tam detayla geri çiz.
            InvalidateVisual();
            return;
        }

        if (!_isPanning)
        {
            return;
        }

        var releasePoint = e.GetPosition(this);
        var dragDelta    = releasePoint - _pointerDownPoint;
        var dragDistance = Math.Sqrt((dragDelta.X * dragDelta.X) + (dragDelta.Y * dragDelta.Y));

        if (dragDistance <= ClickThreshold)
        {
            // Yerleştirme modu aktifse placement, değilse normal seçim
            var mode = PlacementMode;
            if (mode != PointPlacementMode.None && Projection != GeometryProjectionMode.Isometric3D)
            {
                if (TryGetWorldPoint(releasePoint, out var projX, out var projY))
                {
                    OnPlacementPicked?.Invoke(projX, projY, Projection);
                }
            }
            else if (mode == PointPlacementMode.None)
            {
                var hitSurfaceId = HitTestSurfaceId(releasePoint);
                var modifiers = e.KeyModifiers | _pointerDownModifiers;
                bool multiSelect =
                    modifiers.HasFlag(KeyModifiers.Control) ||
                    modifiers.HasFlag(KeyModifiers.Meta);
                if (multiSelect)
                {
                    var selected = SelectedSurfaceIds?.ToList() ?? new List<int>();
                    if (hitSurfaceId is { } hitId)
                    {
                        if (!selected.Remove(hitId))
                            selected.Add(hitId);
                        HighlightedSurfaceId = hitId;
                    }

                    SelectedSurfaceIds = selected;
                }
                else
                {
                    HighlightedSurfaceId = hitSurfaceId;
                    SelectedSurfaceIds = hitSurfaceId is { } singleId ? [singleId] : [];
                }

                // Top2D ve düzenleme modunda: viewport tıklamasını viewmodel'e ilet
                if (Projection == GeometryProjectionMode.Top2D && OnViewportClicked is not null &&
                    TryGetWorldPoint(releasePoint, out double wx, out double wy))
                {
                    OnViewportClicked(wx, wy, multiSelect);
                }
            }
        }

        _isPanning = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        var scene = BuildScene(Bounds);
        if (scene is null)
        {
            return;
        }

        var wheelDelta = e.Delta.Y;
        if (Math.Abs(wheelDelta) < double.Epsilon)
        {
            return;
        }

        var zoomFactor = wheelDelta > 0 ? 1.15d : 1d / 1.15d;
        var oldZoom = _zoom;
        var newZoom = Math.Clamp(_zoom * zoomFactor, MinZoom, MaxZoom);
        if (Math.Abs(newZoom - oldZoom) < 1e-9)
        {
            return;
        }

        var cursor = e.GetPosition(this);
        var sceneCenter = scene.ViewCenter;
        var ratio = newZoom / oldZoom;
        _pan = cursor - sceneCenter - ((cursor - sceneCenter - _pan) * ratio);
        _zoom = newZoom;
        InvalidateVisual();
        e.Handled = true;
    }
    private Point SnapPointToBackground(Point raw, double tolerance)
    {
        var segments = BackgroundSegments;
        if (segments is null || segments.Count == 0)
            return raw;

        Point best = raw;
        double bestDistance = tolerance;

        foreach (var (a3, b3) in segments)
        {
            var a = new Point(a3.X, a3.Y);
            var b = new Point(b3.X, b3.Y);

            TryCandidate(a, 0.82);
            TryCandidate(b, 0.82);
            TryCandidate(ClosestPointOnSegment(raw, a, b), 1.0);
        }

        return best;

        void TryCandidate(Point candidate, double priority)
        {
            double d = Distance(raw, candidate) * priority;
            if (d < bestDistance)
            {
                bestDistance = d;
                best = candidate;
            }
        }
    }
    private (double Dx, double Dy) SnapEdgeDeltaToBackground(double dxWorld, double dyWorld, double tolerance)
    {
        var segments = BackgroundSegments;
        if (segments is null || segments.Count == 0)
            return (dxWorld, dyWorld);

        double edgeX = _edgeDragV1X - _edgeDragV0X;
        double edgeY = _edgeDragV1Y - _edgeDragV0Y;
        double edgeLen = Math.Sqrt(edgeX * edgeX + edgeY * edgeY);
        if (edgeLen <= 1e-9)
            return (dxWorld, dyWorld);

        double tx = edgeX / edgeLen;
        double ty = edgeY / edgeLen;
        double nx = _edgeDragNormalX;
        double ny = _edgeDragNormalY;
        if (Math.Sqrt(nx * nx + ny * ny) <= 1e-9)
            return (dxWorld, dyWorld);

        double movedAx = _edgeDragV0X + dxWorld;
        double movedAy = _edgeDragV0Y + dyWorld;
        double bestSignedDistance = 0;
        double bestAbsDistance = tolerance;

        foreach (var (a3, b3) in segments)
        {
            double sx = b3.X - a3.X;
            double sy = b3.Y - a3.Y;
            double segLen = Math.Sqrt(sx * sx + sy * sy);
            if (segLen <= 1e-9) continue;

            double ux = sx / segLen;
            double uy = sy / segLen;
            if (Math.Abs(ux * tx + uy * ty) < 0.985) continue;

            double aT = (a3.X - movedAx) * tx + (a3.Y - movedAy) * ty;
            double bT = (b3.X - movedAx) * tx + (b3.Y - movedAy) * ty;
            double overlap = Math.Min(edgeLen, Math.Max(aT, bT)) - Math.Max(0, Math.Min(aT, bT));
            if (overlap < Math.Min(0.25, edgeLen * 0.25)) continue;

            double signedDistance = (a3.X - movedAx) * nx + (a3.Y - movedAy) * ny;
            double absDistance = Math.Abs(signedDistance);
            if (absDistance < bestAbsDistance)
            {
                bestAbsDistance = absDistance;
                bestSignedDistance = signedDistance;
            }
        }

        return bestAbsDistance < tolerance
            ? (dxWorld + nx * bestSignedDistance, dyWorld + ny * bestSignedDistance)
            : (dxWorld, dyWorld);
    }

}
