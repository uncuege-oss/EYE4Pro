using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Kst.Acoustics.App.ViewModels;

namespace Kst.Acoustics.App.Views;

public class AbsorptionPlotControl : Control
{
    public static readonly StyledProperty<List<ViewModels.GraphPoint>> PointsProperty =
        AvaloniaProperty.Register<AbsorptionPlotControl, List<ViewModels.GraphPoint>>(nameof(Points));

    public List<ViewModels.GraphPoint> Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    static AbsorptionPlotControl()
    {
        AffectsRender<AbsorptionPlotControl>(PointsProperty);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        double padding = 40;
        double plotWidth = bounds.Width - padding * 2;
        double plotHeight = bounds.Height - padding * 2;

        // Draw background and grid
        context.FillRectangle(new SolidColorBrush(Color.Parse("#050C14")), bounds);
        var gridPen = new Pen(new SolidColorBrush(Color.Parse("#1B3045")), 0.5);
        
        // Horizontal lines (Alpha)
        for (int i = 0; i <= 10; i++)
        {
            double y = padding + plotHeight * (1 - i / 10.0);
            context.DrawLine(gridPen, new Avalonia.Point(padding, y), new Avalonia.Point(bounds.Width - padding, y));
            // Label
            var text = new FormattedText($"{i/10.0:0.0}", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 10, Brushes.Gray);
            context.DrawText(text, new Avalonia.Point(padding - 25, y - 5));
        }

        // Frequencies to draw vertical lines (Standard centers)
        double[] centers = { 125, 250, 500, 1000, 2000, 4000, 8000 };
        double minF = 63;
        double maxF = 8000;

        foreach (var f in centers)
        {
            double x = padding + plotWidth * Math.Log(f / minF) / Math.Log(maxF / minF);
            context.DrawLine(gridPen, new Avalonia.Point(x, padding), new Avalonia.Point(x, bounds.Height - padding));
            var text = new FormattedText($"{f}", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 10, Brushes.Gray);
            context.DrawText(text, new Avalonia.Point(x - 10, bounds.Height - padding + 5));
        }

        // Draw Plot
        if (Points != null && Points.Count > 1)
        {
            var plotPen = new Pen(new SolidColorBrush(Color.Parse("#70C8A8")), 2);
            var lastPoint = new Avalonia.Point(0, 0);

            for (int i = 0; i < Points.Count; i++)
            {
                double f = Points[i].X;
                double alpha = Points[i].Y;

                double x = padding + plotWidth * Math.Log(f / minF) / Math.Log(maxF / minF);
                double y = padding + plotHeight * (1 - alpha);

                var currentPoint = new Avalonia.Point(x, y);
                if (i > 0)
                {
                    context.DrawLine(plotPen, lastPoint, currentPoint);
                }
                lastPoint = currentPoint;
            }
        }
    }
}
