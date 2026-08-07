using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Kst.Acoustics.Core.Electric.Calculation;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.App.Electric.Views;

public partial class KoskYerlesimWindow : Window
{
    private readonly KoskYerlesimServisi.Yerlesim _yerlesim = null!;

    public KoskYerlesimWindow() { InitializeComponent(); }

    public KoskYerlesimWindow(OrtaGerilimSistemi og, ElektrikKutuphaneServisi kutuphane)
    {
        InitializeComponent();
        _yerlesim = KoskYerlesimServisi.Hesapla(og, kutuphane);
        OlcuText.Text = $"Köşk: {_yerlesim.GenislikMm:N0} × {_yerlesim.DerinlikMm:N0} mm  •  Köşk tipi: {KoskAd(og.KoskTipi)}";

        YerlesimCanvas.SizeChanged += (_, _) => Ciz();
        Opened += (_, _) => Ciz();
    }

    private void Ciz()
    {
        if (_yerlesim == null || YerlesimCanvas.Bounds.Width < 10 || YerlesimCanvas.Bounds.Height < 10) return;
        YerlesimCanvas.Children.Clear();

        double pad = 50;
        double vw = YerlesimCanvas.Bounds.Width - 2 * pad;
        double vh = YerlesimCanvas.Bounds.Height - 2 * pad;
        if (_yerlesim.GenislikMm <= 0 || _yerlesim.DerinlikMm <= 0) return;

        double s = Math.Min(vw / _yerlesim.GenislikMm, vh / _yerlesim.DerinlikMm);
        if (s <= 0) return;

        double ox = pad + (vw - _yerlesim.GenislikMm * s) / 2.0;
        double oy = pad + (vh - _yerlesim.DerinlikMm * s) / 2.0;

        // Köşk dış duvarı
        var dis = new Avalonia.Controls.Shapes.Rectangle
        {
            Width = _yerlesim.GenislikMm * s,
            Height = _yerlesim.DerinlikMm * s,
            Stroke = new SolidColorBrush(Color.Parse("#C8702E")),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.Parse("#10180E"))
        };
        Canvas.SetLeft(dis, ox); Canvas.SetTop(dis, oy);
        YerlesimCanvas.Children.Add(dis);

        // Ölçü etiketleri (üst genişlik, sol derinlik)
        EkleMetin($"{_yerlesim.GenislikMm:N0} mm", ox + _yerlesim.GenislikMm * s / 2.0 - 28, oy - 22, "#A0C888");
        var derinlikLbl = new TextBlock { Text = $"{_yerlesim.DerinlikMm:N0} mm", FontSize = 11, Foreground = new SolidColorBrush(Color.Parse("#A0C888")) };
        derinlikLbl.RenderTransform = new RotateTransform(-90);
        Canvas.SetLeft(derinlikLbl, ox - 40); Canvas.SetTop(derinlikLbl, oy + _yerlesim.DerinlikMm * s / 2.0);
        YerlesimCanvas.Children.Add(derinlikLbl);

        // Elemanlar
        foreach (var el in _yerlesim.Elemanlar)
        {
            bool trafo = el.Tur == "Trafo";
            var rect = new Avalonia.Controls.Shapes.Rectangle
            {
                Width = Math.Max(2, el.Genislik * s),
                Height = Math.Max(2, el.Derinlik * s),
                Stroke = new SolidColorBrush(Color.Parse(trafo ? "#E8A040" : "#8AB060")),
                StrokeThickness = 1.5,
                Fill = new SolidColorBrush(Color.Parse(trafo ? "#3A2410" : "#1A2A12"))
            };
            Canvas.SetLeft(rect, ox + el.X * s);
            Canvas.SetTop(rect, oy + el.Y * s);
            YerlesimCanvas.Children.Add(rect);

            var lbl = new TextBlock
            {
                Text = el.Etiket,
                FontSize = 9.5,
                Foreground = new SolidColorBrush(Color.Parse(trafo ? "#F0C088" : "#C0D8A0")),
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Width = Math.Max(20, el.Genislik * s - 4),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Canvas.SetLeft(lbl, ox + el.X * s + 2);
            Canvas.SetTop(lbl, oy + el.Y * s + Math.Max(2, el.Derinlik * s / 2.0 - 10));
            YerlesimCanvas.Children.Add(lbl);

            // Eleman genişlik ölçüsü (küçük)
            EkleMetin($"{el.Genislik:N0}", ox + el.X * s + el.Genislik * s / 2.0 - 12, oy + el.Y * s - 14, "#6A8A50", 8.5);
        }
    }

    private void EkleMetin(string t, double x, double y, string renk, double boyut = 11)
    {
        var lbl = new TextBlock { Text = t, FontSize = boyut, Foreground = new SolidColorBrush(Color.Parse(renk)) };
        Canvas.SetLeft(lbl, x); Canvas.SetTop(lbl, y);
        YerlesimCanvas.Children.Add(lbl);
    }

    private static string KoskAd(KoskTipi k) => k switch
    {
        KoskTipi.BinaAlti => "Bina Altı",
        KoskTipi.DisMekanBetonKosk => "Dış Mekan Beton Köşk",
        KoskTipi.HariciYapi => "Harici Yapı",
        _ => k.ToString()
    };

    private void Kapat_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
}
