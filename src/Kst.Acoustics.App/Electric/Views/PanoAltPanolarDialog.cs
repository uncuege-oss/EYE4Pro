using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Kst.Acoustics.App.Electric.ViewModels;
using Kst.Acoustics.Core.Electric.Calculation;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.App.Electric.Views;

/// <summary>Seçili panonun doğrudan alt panoları, eşzamanlılık katsayıları ve kablo metrajları.</summary>
public sealed class PanoAltPanolarDialog : Window
{
    public PanoAltPanolarDialog(TekHatSemasiViewModel vm, Pano ustPano)
    {
        Title = $"{ustPano.Ad} — Alt Panolar";
        Width = 980;
        Height = 540;
        MinWidth = 860;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush("#080F0A");

        var panolar = vm.Proje.TumPanolar.Where(p => p.UstPano == ustPano)
            .OrderBy(p => p.Kat).ThenBy(p => p.Ad).ToList();
        var metrajlar = new Dictionary<Pano, TextBox>();
        var katsayilar = new Dictionary<Pano, TextBox>();
        var liste = new StackPanel { Spacing = 8 };
        var durum = Text("", "#E8B56A");
        durum.TextWrapping = TextWrapping.Wrap;

        Grid Satir(string ad, string guc, string baglanti, Control metraj, Control katsayi)
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("2*,110,2*,100,110"), Margin = new Thickness(10, 8) };
            grid.Children.Add(Text(ad));
            var gucMetni = Text(guc);
            Grid.SetColumn(gucMetni, 1);
            grid.Children.Add(gucMetni);
            var baglantiMetni = Text(baglanti, "#82B86A");
            baglantiMetni.TextWrapping = TextWrapping.Wrap;
            Grid.SetColumn(baglantiMetni, 2);
            grid.Children.Add(baglantiMetni);
            Grid.SetColumn(metraj, 3);
            grid.Children.Add(metraj);
            Grid.SetColumn(katsayi, 4);
            grid.Children.Add(katsayi);
            return grid;
        }

        liste.Children.Add(Satir("Alt pano / Kat", "Güç", "Besleme", Text("Kablo (m)"), Text("Eşzamanlılık")));
        foreach (var pano in panolar)
        {
            var box = new TextBox
            {
                Text = pano.KabloMesafeM.ToString("0.##"),
                Background = Brush("#101C10"), Foreground = Brush("#D0E8C0"),
                BorderBrush = Brush("#2A4A20"), HorizontalContentAlignment = HorizontalAlignment.Right
            };
            metrajlar[pano] = box;
            var katsayiBox = new TextBox
            {
                Text = pano.EszamanlilikFaktoru.ToString("0.####"),
                Background = Brush("#101C10"), Foreground = Brush("#D0E8C0"),
                BorderBrush = Brush("#2A4A20"), HorizontalContentAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(8, 0, 0, 0)
            };
            ToolTip.SetTip(katsayiBox, "Eşzamanlılık katsayısı (ks): 0'dan büyük, en fazla 1. Örnek: 0,7");
            katsayilar[pano] = katsayiBox;
            var busbar = vm.Proje.TumBusbarlar.FirstOrDefault(b => b.Id == pano.BaglandigiBusbarId);
            string baglanti = pano.BaglantiTipi == BaglantiTipi.Busbar
                ? $"{busbar?.Ad ?? "Busbar"} • çıkış kutusu → pano"
                : $"{ustPano.Ad} → pano";
            string guc = pano.PanoTipi == PanoTipi.Kompanzasyon
                ? $"{pano.ToplamReaktifVAR / 1000:0.##} kVAr"
                : $"{pano.ToplamGucW / 1000:0.##} kW";
            liste.Children.Add(new Border
            {
                Background = Brush("#0B150C"), BorderBrush = Brush("#203A1D"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5),
                Child = Satir($"{pano.Ad} • Kat {pano.Kat}", guc, baglanti, box, katsayiBox)
            });
        }
        if (panolar.Count == 0)
            liste.Children.Add(Text("Bu panoya doğrudan bağlı alt pano bulunmuyor.", "#82B86A"));

        var iptal = new Button { Content = "İptal" };
        iptal.Click += (_, _) => Close(false);
        var kaydet = new Button { Content = "Değişiklikleri Kaydet", IsEnabled = panolar.Count > 0,
            Background = Brush("#3A7A18"), Foreground = Brushes.White };
        kaydet.Click += (_, _) =>
        {
            var degerler = new Dictionary<Pano, double>();
            var katsayiDegerleri = new Dictionary<Pano, double>();
            foreach (var (pano, box) in metrajlar)
            {
                if (!NumberHelper.TryParse(box.Text, out double mesafe) || !double.IsFinite(mesafe) || mesafe < 0)
                {
                    durum.Text = $"{pano.Ad}: kablo metrajı sıfır veya pozitif bir sayı olmalıdır.";
                    box.Focus();
                    return;
                }
                degerler[pano] = mesafe;
            }
            foreach (var (pano, box) in katsayilar)
            {
                if (!NumberHelper.TryParse(box.Text, out double ks) || !double.IsFinite(ks) || ks <= 0 || ks > 1)
                {
                    durum.Text = $"{pano.Ad}: eşzamanlılık katsayısı 0'dan büyük ve en fazla 1 olmalıdır.";
                    box.Focus();
                    return;
                }
                katsayiDegerleri[pano] = ks;
            }
            foreach (var (pano, ks) in katsayiDegerleri)
                pano.EszamanlilikFaktoru = ks;
            foreach (var (pano, mesafe) in degerler)
            {
                if (pano.KabloMesafeM == mesafe) continue;
                pano.KabloMesafeM = mesafe;
                pano.KabloMesafeKullaniciAyarladi = true;
                var kutu = vm.Proje.TumBusbarlar.FirstOrDefault(b => b.Id == pano.BaglandigiBusbarId)?
                    .CikisKutulari.FirstOrDefault(k => k.BagliPanoAd == pano.Ad);
                if (kutu != null) kutu.KabloMesafeM = mesafe;
            }
            vm.TumunuHesapla();
            Close(true);
        };

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,Auto"), Margin = new Thickness(18) };
        var baslik = Text($"{ustPano.Ad} — Alt Panolar");
        baslik.FontSize = 20;
        root.Children.Add(baslik);
        var aciklama = Text("Alt panoların toplam gücü, kablo metrajı ve eşzamanlılık katsayısı (ks). Katsayı 0'dan büyük ve en fazla 1 olmalıdır. Busbar beslemede kablo metrajı çıkış kutusu ile pano arasındadır.", "#82B86A");
        aciklama.TextWrapping = TextWrapping.Wrap;
        aciklama.Margin = new Thickness(0, 8, 0, 14);
        Grid.SetRow(aciklama, 1);
        root.Children.Add(aciklama);
        var scroll = new ScrollViewer { Content = liste, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 2);
        root.Children.Add(scroll);
        Grid.SetRow(durum, 3);
        root.Children.Add(durum);
        var butonlar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new Thickness(0, 14, 0, 0) };
        butonlar.Children.Add(iptal);
        butonlar.Children.Add(kaydet);
        Grid.SetRow(butonlar, 4);
        root.Children.Add(butonlar);
        Content = root;
    }

    private static SolidColorBrush Brush(string renk) => new(Color.Parse(renk));
    private static TextBlock Text(string metin, string renk = "#D0E8C0") => new()
    {
        Text = metin, Foreground = Brush(renk), FontSize = 13,
        VerticalAlignment = VerticalAlignment.Center
    };
}
