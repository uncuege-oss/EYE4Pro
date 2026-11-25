using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Kst.Acoustics.Core.Bina.Models;

namespace Kst.Acoustics.App.Bina.Views;

/// <summary>Kat üretiminden ve geçici CAD dosyasından bağımsız kaynak çizim görünümü.</summary>
public sealed class MimariCizimIncelemeWindow : Window
{
    public MimariCizimIncelemeWindow(BinaKat cizim)
    {
        Title = "Mimari Proje — Çizimi İncele";
        Width = 1160; Height = 760; MinWidth = 480; MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush.Parse("#08121E");
        // Kaydedilmiş bina dosyalarında ham CAD verisi bulunmayabilir; o durumda
        // kayıtlı çizgileri göster. Kaynak çizime ve mevcut kata dokunma.
        var snapshot = new BinaKat {
            HamSegmentler = cizim.HamSegmentler.Count > 0 ? cizim.HamSegmentler.ToList()
                : cizim.DxfSegmentler.Select(s => (s.A, s.B)).ToList(),
            HamMetinler = cizim.HamMetinler.ToList()
        };
        var canvas = new MimariKatmanOnizlemeCanvas();
        canvas.CizimiGoster(snapshot);
        var header = new StackPanel { Spacing = 6, Margin = new Thickness(0, 0, 0, 12) };
        header.Children.Add(new TextBlock { Text = Path.GetFileName(cizim.DxfFilePath),
            FontSize = 20, Foreground = Brush.Parse("#E0F0FA"), TextWrapping = TextWrapping.Wrap });
        header.Children.Add(new TextBlock {
            Text = $"{snapshot.HamSegmentler.Count:N0} çizgi · {snapshot.HamMetinler.Count:N0} yazı"
                + (cizim.KaynakKatmanlari is { } layers ? $" · {layers.Count} seçili katman" : ""),
            Foreground = Brush.Parse("#A8C5D9"), TextWrapping = TextWrapping.Wrap });
        header.Children.Add(new TextBlock { Text = "Fare tekerleği: yakınlaştır · Sürükle: kaydır",
            Foreground = Brush.Parse("#A8C5D9"), TextWrapping = TextWrapping.Wrap });
        var fit = new Button { Content = "Ekrana sığdır" };
        fit.Click += (_, _) => canvas.Sigdir();
        var close = new Button { Content = "Rapora dön" };
        close.Click += (_, _) => Close();
        var footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        footer.Children.Add(fit); footer.Children.Add(close);
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), Margin = new Thickness(16) };
        grid.Children.Add(header);
        Grid.SetRow(canvas, 1); grid.Children.Add(canvas);
        Grid.SetRow(footer, 2); grid.Children.Add(footer);
        Content = grid;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
        Opened += (_, _) =>
        {
            var screen = (Owner is not null ? Screens.ScreenFromWindow(Owner) : null) ?? Screens.Primary;
            if (screen is null) return;
            double scale = screen.Scaling > 0 ? screen.Scaling : 1;
            double maxWidth = Math.Max(320, screen.WorkingArea.Width / scale - 24);
            double maxHeight = Math.Max(280, screen.WorkingArea.Height / scale - 64);
            MinWidth = Math.Min(MinWidth, maxWidth); MinHeight = Math.Min(MinHeight, maxHeight);
            Width = Math.Min(Width, maxWidth); Height = Math.Min(Height, maxHeight);
            Position = new PixelPoint(screen.WorkingArea.X + Math.Max(0, (screen.WorkingArea.Width - (int)(Width * scale)) / 2),
                screen.WorkingArea.Y + Math.Max(8, (screen.WorkingArea.Height - (int)((Height + 40) * scale)) / 2));
        };
    }
}
