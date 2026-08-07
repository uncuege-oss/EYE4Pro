using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;
using System.Linq;
using Kst.Acoustics.Core.SoundInsulation;

namespace Kst.Acoustics.App.Controls;

public sealed class SoundInsulationCrossSectionControl : Control
{
    public static readonly StyledProperty<WallAssembly?> AssemblyProperty =
        AvaloniaProperty.Register<SoundInsulationCrossSectionControl, WallAssembly?>(nameof(Assembly));

    static SoundInsulationCrossSectionControl()
    {
        AffectsRender<SoundInsulationCrossSectionControl>(AssemblyProperty);
    }

    public WallAssembly? Assembly
    {
        get => GetValue(AssemblyProperty);
        set => SetValue(AssemblyProperty, value);
    }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w < 50 || h < 50) return;

        var assembly = Assembly;
        if (assembly == null || assembly.Layers.Count == 0) return;

        double totalThickness = assembly.TotalThickness;
        if (totalThickness <= 0) return;

        // Draw scale and padding
        double padY = 40;
        double padX = 40;
        double drawW = w - 2 * padX;
        double drawH = h - 2 * padY;

        double currentX = padX;
        foreach (var layer in assembly.Layers)
        {
            double layerW = (layer.Thickness / totalThickness) * drawW;
            var rect = new Rect(currentX, padY, Math.Max(2, layerW), drawH);

            // Coloring based on material type
            var brush = GetLayerBrush(layer);
            ctx.FillRectangle(brush, rect);
            ctx.DrawRectangle(new Pen(Brushes.White, 0.5), rect);

            // Label
            if (layerW > 20)
            {
                DrawLabel(ctx, $"{layer.Thickness * 1000:F1}mm", currentX + layerW / 2, padY - 20, true);
                DrawLabel(ctx, layer.Material.Name, currentX + layerW / 2, padY + drawH + 5, true);
            }

            currentX += layerW;
        }
    }

    private IBrush GetLayerBrush(SoundInsulationLayer layer)
    {
        if (layer.IsAirGap)
        {
            return layer.HasPorousFill ? new SolidColorBrush(Color.Parse("#444422")) : Brushes.Transparent;
        }

        return layer.Material.Category switch
        {
            "Masif" => new SolidColorBrush(Color.Parse("#666666")),
            "Levha" => new SolidColorBrush(Color.Parse("#886644")),
            "Cam" => new SolidColorBrush(Color.Parse("#4488AA"), 0.6),
            _ => new SolidColorBrush(Color.Parse("#555555"))
        };
    }

    private void DrawLabel(DrawingContext ctx, string text, double x, double y, bool center = false)
    {
        var ft = new FormattedText(
            text,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Arial, sans-serif"),
            10,
            Brushes.White);
        ctx.DrawText(ft, new Point(center ? x - ft.Width / 2 : x, y));
    }
}
