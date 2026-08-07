using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Kst.Acoustics.Core.ImpactSurface;

namespace Kst.Acoustics.App.Controls
{
    public class ImpactLayerCanvas : Control
    {
        public static readonly StyledProperty<IEnumerable<ImpactLayer>?> LayersProperty =
            AvaloniaProperty.Register<ImpactLayerCanvas, IEnumerable<ImpactLayer>?>(nameof(Layers));

        public static readonly StyledProperty<ImpactLayer?> SelectedLayerProperty =
            AvaloniaProperty.Register<ImpactLayerCanvas, ImpactLayer?>(nameof(SelectedLayer));

        public static readonly StyledProperty<double[]?> AccelSeriesProperty =
            AvaloniaProperty.Register<ImpactLayerCanvas, double[]?>(nameof(AccelSeries));

        public static readonly StyledProperty<double> ViewRotationProperty =
            AvaloniaProperty.Register<ImpactLayerCanvas, double>(nameof(ViewRotation), 0.5);

        public static readonly StyledProperty<double> ViewZoomProperty =
            AvaloniaProperty.Register<ImpactLayerCanvas, double>(nameof(ViewZoom), 1.0);

        public IEnumerable<ImpactLayer>? Layers
        {
            get => GetValue(LayersProperty);
            set => SetValue(LayersProperty, value);
        }

        public ImpactLayer? SelectedLayer
        {
            get => GetValue(SelectedLayerProperty);
            set => SetValue(SelectedLayerProperty, value);
        }

        public double[]? AccelSeries
        {
            get => GetValue(AccelSeriesProperty);
            set => SetValue(AccelSeriesProperty, value);
        }

        public double ViewRotation
        {
            get => GetValue(ViewRotationProperty);
            set => SetValue(ViewRotationProperty, value);
        }

        public double ViewZoom
        {
            get => GetValue(ViewZoomProperty);
            set => SetValue(ViewZoomProperty, value);
        }

        static ImpactLayerCanvas()
        {
            AffectsRender<ImpactLayerCanvas>(LayersProperty, SelectedLayerProperty, AccelSeriesProperty, ViewRotationProperty, ViewZoomProperty);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var bounds = Bounds;
            if (bounds.Width < 50 || bounds.Height < 50) return;

            var activeLayers = Layers?.Where(l => l.IsEnabled).Reverse().ToList() ?? new List<ImpactLayer>();
            if (activeLayers.Count == 0)
            {
                DrawEmptyState(context, bounds);
                return;
            }

            double totalThickness = activeLayers.Sum(l => l.ThicknessMm);
            if (totalThickness <= 0) return;

            // 2.5D / Isometric Projection Settings
            double skewX = ViewRotation;
            double boxWidth = bounds.Width * 0.4 * ViewZoom;
            double boxDepth = bounds.Width * 0.2 * ViewZoom;
            
            // Scale height to fit bounds
            double maxDrawHeight = bounds.Height * 0.7;
            double scaleH = (maxDrawHeight / totalThickness) * ViewZoom;
            // Limit scale to prevent extreme stretching
            scaleH = Math.Min(scaleH, 5.0); 

            double centerX = bounds.Width * 0.45;
            double currentY = bounds.Height * 0.85;

            // Draw from bottom to top
            foreach (var layer in activeLayers)
            {
                double layerH = layer.ThicknessMm * scaleH;
                var color = GetFunctionColor(layer.FunctionTag);
                bool isSelected = SelectedLayer != null && 
                                  SelectedLayer.Index == layer.Index && 
                                  SelectedLayer.MaterialCode == layer.MaterialCode;

                DrawIsometricBox(context, centerX, currentY, boxWidth, boxDepth, layerH, color, layer, isSelected);
                
                currentY -= layerH;
            }

            DrawHeightRuler(context, bounds, centerX + boxWidth + boxDepth * skewX + 20, bounds.Height * 0.85, totalThickness, scaleH);

            if (AccelSeries != null && AccelSeries.Length > 0)
            {
                double chartWidth = Math.Min(200, Math.Max(120, bounds.Width - 145));
                DrawImpactChart(context, new Rect(20, 20, chartWidth, 120), AccelSeries);
            }
        }

        private void DrawIsometricBox(DrawingContext ctx, double x, double y, double w, double d, double h, Color baseColor, ImpactLayer layer, bool isSelected)
        {
            double skewX = ViewRotation;
            double skewY = -0.3;

            double opacity = isSelected ? 1.0 : layer.Opacity * 0.85;
            
            // Colors for sides (shading)
            if (isSelected)
            {
                // Highlight color (Bright Cyan)
                baseColor = Color.Parse("#00FFFF");
            }

            var topBrush = new SolidColorBrush(baseColor, opacity);
            var frontBrush = new SolidColorBrush(Darken(baseColor, 0.8), opacity);
            var sideBrush = new SolidColorBrush(Darken(baseColor, 0.6), opacity);
            
            var pen = isSelected
                ? new Pen(Brushes.White, 1.5)
                : new Pen(new SolidColorBrush(Color.Parse("#FFFFFF"), 0.2), 0.5);

            // Front Face
            var frontPoints = new[]
            {
                new Point(x, y),
                new Point(x + w, y),
                new Point(x + w, y - h),
                new Point(x, y - h)
            };
            ctx.DrawGeometry(frontBrush, pen, CreatePolygon(frontPoints));

            // Side Face (Right)
            var sidePoints = new[]
            {
                new Point(x + w, y),
                new Point(x + w + d * skewX, y + d * skewY),
                new Point(x + w + d * skewX, y + d * skewY - h),
                new Point(x + w, y - h)
            };
            ctx.DrawGeometry(sideBrush, pen, CreatePolygon(sidePoints));

            // Top Face
            var topPoints = new[]
            {
                new Point(x, y - h),
                new Point(x + w, y - h),
                new Point(x + w + d * skewX, y + d * skewY - h),
                new Point(x + d * skewX, y + d * skewY - h)
            };
            ctx.DrawGeometry(topBrush, pen, CreatePolygon(topPoints));

            // Label (Only if layer is thick enough)
            if (h > 15)
            {
                string label = $"{layer.MaterialCode} ({layer.ThicknessMm}mm)";
                DrawText(ctx, label, x - 10, y - h / 2, Brushes.White, 11, TextAlignment.Right);
            }
        }

        private void DrawImpactChart(DrawingContext ctx, Rect rect, double[] series)
        {
            var bgBrush = new SolidColorBrush(Color.Parse("#CC0A1A2A"));
            var pen = new Pen(new SolidColorBrush(Color.Parse("#3E6880")), 1);
            var pulsePen = new Pen(Brushes.Cyan, 1.5);
            
            ctx.DrawRectangle(bgBrush, pen, rect);
            
            double maxG = series.Max();
            if (maxG <= 0) maxG = 1;

            double stepX = rect.Width / series.Length;
            double scaleY = rect.Height / (maxG * 1.2);

            var points = new List<Point>();
            for (int i = 0; i < series.Length; i++)
            {
                double px = rect.X + i * stepX;
                double py = rect.Bottom - series[i] * scaleY;
                points.Add(new Point(px, py));
            }

            for (int i = 0; i < points.Count - 1; i++)
            {
                ctx.DrawLine(pulsePen, points[i], points[i + 1]);
            }

            DrawText(ctx, "Darbe İvme Eğrisi (a/t)", rect.X + 5, rect.Y + 10, Brushes.Gray, 9, TextAlignment.Left);
            DrawText(ctx, $"{maxG:F0} g", rect.X + rect.Width - 5, rect.Y + 10, Brushes.Cyan, 9, TextAlignment.Right);
        }

        private void DrawHeightRuler(DrawingContext ctx, Rect bounds, double x, double bottomY, double totalMm, double scaleH)
        {
            var pen = new Pen(new SolidColorBrush(Color.Parse("#7E94A7")), 1);
            double totalH = totalMm * scaleH;

            // Main line
            ctx.DrawLine(pen, new Point(x, bottomY), new Point(x, bottomY - totalH));
            
            // Ticks
            ctx.DrawLine(pen, new Point(x, bottomY), new Point(x + 5, bottomY));
            ctx.DrawLine(pen, new Point(x, bottomY - totalH), new Point(x + 5, bottomY - totalH));

            // Total Text
            DrawText(ctx, $"Toplam: {totalMm:F0} mm", x + 10, bottomY - totalH / 2, new SolidColorBrush(Color.Parse("#E6EEF6")), 12, TextAlignment.Left);
        }

        private void DrawEmptyState(DrawingContext ctx, Rect bounds)
        {
            DrawText(ctx, "Katman ekleyerek başlayın", bounds.Width / 2, bounds.Height / 2, Brushes.Gray, 14, TextAlignment.Center);
        }

        private StreamGeometry CreatePolygon(Point[] points)
        {
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(points[0], true);
                for (int i = 1; i < points.Length; i++)
                {
                    context.LineTo(points[i]);
                }
                context.EndFigure(true);
            }
            return geometry;
        }

        private void DrawText(DrawingContext ctx, string text, double x, double y, IBrush brush, double fontSize, TextAlignment alignment)
        {
            var ft = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.Medium),
                fontSize,
                brush);

            double drawX = x;
            if (alignment == TextAlignment.Center) drawX -= ft.Width / 2;
            else if (alignment == TextAlignment.Right) drawX -= ft.Width;

            ctx.DrawText(ft, new Point(drawX, y - ft.Height / 2));
        }

        private Color GetFunctionColor(string tag)
        {
            return tag switch
            {
                "Wear" => Color.Parse("#8B7355"),
                "Acoustic" => Color.Parse("#4A90D9"),
                "Impact" => Color.Parse("#E8A020"),
                "Drainage" => Color.Parse("#6DBF6D"),
                "Structural" => Color.Parse("#808080"),
                _ => Color.Parse("#555555")
            };
        }

        private Color Darken(Color color, double factor)
        {
            return Color.FromRgb(
                (byte)(color.R * factor),
                (byte)(color.G * factor),
                (byte)(color.B * factor));
        }
    }
}
