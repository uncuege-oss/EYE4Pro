using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Kst.Acoustics.Core.Simulation;

namespace Kst.Acoustics.App.Controls
{
    public class GridMapControl : Control
    {
        // ---------------------------------------------------------------------------
        // Styled properties
        // ---------------------------------------------------------------------------

        public static readonly StyledProperty<GridMapResult?> MapDataProperty =
            AvaloniaProperty.Register<GridMapControl, GridMapResult?>(nameof(MapData));

        public static readonly StyledProperty<GridMapParameter> SelectedParameterProperty =
            AvaloniaProperty.Register<GridMapControl, GridMapParameter>(
                nameof(SelectedParameter), GridMapParameter.RT60);

        public GridMapResult? MapData
        {
            get => GetValue(MapDataProperty);
            set => SetValue(MapDataProperty, value);
        }

        public GridMapParameter SelectedParameter
        {
            get => GetValue(SelectedParameterProperty);
            set => SetValue(SelectedParameterProperty, value);
        }

        // ---------------------------------------------------------------------------
        // Static constructor – wire AffectsRender
        // ---------------------------------------------------------------------------

        static GridMapControl()
        {
            AffectsRender<GridMapControl>(MapDataProperty, SelectedParameterProperty);
        }

        // ---------------------------------------------------------------------------
        // Layout constants
        // ---------------------------------------------------------------------------

        private const double PadL = 44;
        private const double PadR = 60;
        private const double PadT = 12;
        private const double PadB = 32;

        // ---------------------------------------------------------------------------
        // Render
        // ---------------------------------------------------------------------------

        public override void Render(DrawingContext ctx)
        {
            base.Render(ctx);

            double totalW = Bounds.Width;
            double totalH = Bounds.Height;

            // Background
            ctx.FillRectangle(new SolidColorBrush(Color.Parse("#0A1520")),
                new Rect(0, 0, totalW, totalH));

            double plotW = totalW - PadL - PadR;
            double plotH = totalH - PadT - PadB;

            // Guard: not enough space
            if (plotW <= 0 || plotH <= 0) return;

            // -----------------------------------------------------------------------
            // No data
            // -----------------------------------------------------------------------
            if (MapData is null)
            {
                var noDataText = new FormattedText(
                    "Harita verisi yok",
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    13,
                    new SolidColorBrush(Color.Parse("#607080")));

                ctx.DrawText(noDataText,
                    new Point(
                        PadL + (plotW - noDataText.Width) / 2.0,
                        PadT + (plotH - noDataText.Height) / 2.0));
                return;
            }

            int cols = MapData.Cols;
            int rows = MapData.Rows;

            if (cols <= 0 || rows <= 0) return;

            double cellW = plotW / cols;
            double cellH = plotH / rows;

            var param = SelectedParameter;
            var (minVal, maxVal) = MapData.GetRange(param);
            double range = maxVal - minVal;

            // -----------------------------------------------------------------------
            // Cell fill
            // -----------------------------------------------------------------------
            var nanBrush = new SolidColorBrush(Color.Parse("#1A2A3A"));

            for (int r = 0; r < rows; r++)
            {
                double y = PadT + r * cellH;
                for (int c = 0; c < cols; c++)
                {
                    double x = PadL + c * cellW;
                    double val = MapData.GetValue(param, r, c);
                    var rect = new Rect(x, y, cellW + 0.5, cellH + 0.5); // slight overlap avoids gaps

                    if (double.IsNaN(val))
                    {
                        ctx.FillRectangle(nanBrush, rect);
                    }
                    else
                    {
                        double t = (range > 0) ? Math.Clamp((val - minVal) / range, 0.0, 1.0) : 0.5;
                        if (param == GridMapParameter.STI) t = 1.0 - t; // high STI = green (cool end)
                        var brush = new SolidColorBrush(ValueToColor(t));
                        ctx.FillRectangle(brush, rect);
                    }
                }
            }

            // -----------------------------------------------------------------------
            // Grid lines
            // -----------------------------------------------------------------------
            var gridPen = new Pen(new SolidColorBrush(Color.Parse("#1C3050")), 0.5);

            // Vertical lines
            for (int c = 1; c < cols; c++)
            {
                double x = PadL + c * cellW;
                ctx.DrawLine(gridPen, new Point(x, PadT), new Point(x, PadT + plotH));
            }

            // Horizontal lines
            for (int r = 1; r < rows; r++)
            {
                double y = PadT + r * cellH;
                ctx.DrawLine(gridPen, new Point(PadL, y), new Point(PadL + plotW, y));
            }

            // -----------------------------------------------------------------------
            // Axis labels
            // -----------------------------------------------------------------------
            var axisLabelBrush = new SolidColorBrush(Color.Parse("#405870"));
            var axisTypeface = new Typeface("Segoe UI");
            const double axisFont = 8;

            int stepX = Math.Max(1, cols / 6);
            int stepY = Math.Max(1, rows / 6);

            double gridXMin = MapData.Grid.XMin;
            double gridXMax = MapData.Grid.XMax;
            double gridYMin = MapData.Grid.YMin;
            double gridYMax = MapData.Grid.YMax;
            double cellSize  = MapData.Grid.CellSize;

            // Bottom axis (X coordinates)
            for (int c = 0; c < cols; c += stepX)
            {
                double worldX = gridXMin + c * cellSize + cellSize * 0.5;
                string label = worldX.ToString("0.#");

                var ft = new FormattedText(label,
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    axisTypeface, axisFont, axisLabelBrush);

                double px = PadL + c * cellW + cellW / 2.0 - ft.Width / 2.0;
                double py = PadT + plotH + 3;
                ctx.DrawText(ft, new Point(px, py));
            }

            // Left axis (Y coordinates)
            for (int r = 0; r < rows; r += stepY)
            {
                double worldY = gridYMin + r * cellSize + cellSize * 0.5;
                string label = worldY.ToString("0.#");

                var ft = new FormattedText(label,
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    axisTypeface, axisFont, axisLabelBrush);

                double px = PadL - ft.Width - 3;
                double py = PadT + r * cellH + cellH / 2.0 - ft.Height / 2.0;
                ctx.DrawText(ft, new Point(px, py));
            }

            // -----------------------------------------------------------------------
            // Color legend (right side, inside PadR)
            // -----------------------------------------------------------------------
            const double legendW = 14;
            double legendX = PadL + plotW + 4;
            double legendY = PadT;
            const int bandCount = 20;
            double bandH = plotH / bandCount;

            var legendLabelBrush = new SolidColorBrush(Color.Parse("#7090B0"));
            var legendTitleBrush = new SolidColorBrush(Color.Parse("#8090A8"));

            // Parameter name above legend
            string paramName = ParameterLabel(param);
            var titleFt = new FormattedText(paramName,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                axisTypeface, axisFont, legendTitleBrush);

            ctx.DrawText(titleFt, new Point(legendX, legendY - titleFt.Height - 2));

            // Draw gradient bands (top = min value / blue, bottom = max value / red)
            // For STI the color mapping is inverted already via t, but legend shows raw min→max.
            for (int i = 0; i < bandCount; i++)
            {
                double tBand = (double)i / (bandCount - 1);
                // Top of legend = min value → t=0 → blue; bottom = max → t=1 → red
                // For STI colour is inverted in the cell render; invert here too so legend matches
                double tColor = (param == GridMapParameter.STI) ? (1.0 - tBand) : tBand;
                var bandBrush = new SolidColorBrush(ValueToColor(tColor));
                ctx.FillRectangle(bandBrush,
                    new Rect(legendX, legendY + i * bandH, legendW, bandH + 0.5));
            }

            // Min label at top
            string minLabel = minVal.ToString("0.##");
            var minFt = new FormattedText(minLabel,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                axisTypeface, axisFont, legendLabelBrush);
            ctx.DrawText(minFt, new Point(legendX + legendW + 2, legendY - minFt.Height / 2.0));

            // Max label at bottom
            string maxLabel = maxVal.ToString("0.##");
            var maxFt = new FormattedText(maxLabel,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                axisTypeface, axisFont, legendLabelBrush);
            ctx.DrawText(maxFt, new Point(legendX + legendW + 2,
                legendY + plotH - maxFt.Height / 2.0));

            // -----------------------------------------------------------------------
            // Parameter label – top-left corner of plot area
            // -----------------------------------------------------------------------
            string paramLabelFull = ParameterLabelFull(param);
            var paramFt = new FormattedText(paramLabelFull,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                9,
                new SolidColorBrush(Color.Parse("#6090C0")));
            ctx.DrawText(paramFt, new Point(PadL + 4, PadT + 3));
        }

        // ---------------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------------

        private static Color ValueToColor(double t)
        {
            // HSL: H = 240 - 240*t degrees, S = 0.85, L = 0.45
            double h = 240.0 - 240.0 * t;
            const double s = 0.85;
            const double l = 0.45;

            double c = (1.0 - Math.Abs(2.0 * l - 1.0)) * s;
            double hPrime = h / 60.0;
            double x = c * (1.0 - Math.Abs(hPrime % 2.0 - 1.0));
            double m = l - c / 2.0;

            double r1, g1, b1;

            if      (hPrime < 1) { r1 = c; g1 = x; b1 = 0; }
            else if (hPrime < 2) { r1 = x; g1 = c; b1 = 0; }
            else if (hPrime < 3) { r1 = 0; g1 = c; b1 = x; }
            else if (hPrime < 4) { r1 = 0; g1 = x; b1 = c; }
            else if (hPrime < 5) { r1 = x; g1 = 0; b1 = c; }
            else                 { r1 = c; g1 = 0; b1 = x; }

            byte R = (byte)Math.Clamp((int)Math.Round((r1 + m) * 255), 0, 255);
            byte G = (byte)Math.Clamp((int)Math.Round((g1 + m) * 255), 0, 255);
            byte B = (byte)Math.Clamp((int)Math.Round((b1 + m) * 255), 0, 255);

            return Color.FromRgb(R, G, B);
        }

        private static string ParameterLabel(GridMapParameter p) => p switch
        {
            GridMapParameter.RT60 => "RT60",
            GridMapParameter.C80  => "C80",
            GridMapParameter.C50  => "C50",
            GridMapParameter.STI  => "Tahmini STI",
            _                    => p.ToString()
        };

        private static string ParameterLabelFull(GridMapParameter p) => p switch
        {
            GridMapParameter.RT60 => "RT60 (s)",
            GridMapParameter.C80  => "C80 (dB)",
            GridMapParameter.C50  => "C50 (dB)",
            GridMapParameter.STI  => "Tahmini STI (0–1)",
            _                    => p.ToString()
        };
    }
}
