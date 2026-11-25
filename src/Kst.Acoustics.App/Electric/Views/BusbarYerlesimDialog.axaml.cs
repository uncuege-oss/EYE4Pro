using System;
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

public partial class BusbarYerlesimDialog : Window
{
    private readonly TekHatSemasiViewModel _vm;
    private readonly BusbarHatti _busbar;
    private readonly Dictionary<string, TextBox> _katUzunluklari = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<BusbarCikisKutusu, TextBox> _kutuKonumlari = new();

    public BusbarYerlesimDialog()
    {
        InitializeComponent();
        _vm = null!;
        _busbar = null!;
    }

    public BusbarYerlesimDialog(TekHatSemasiViewModel vm, BusbarHatti busbar)
    {
        InitializeComponent();
        _vm = vm;
        _busbar = busbar;
        BaslikText.Text = $"{busbar.Ad} — Kat ve Çıkış Yerleşimi";
        KatlariOlustur();
        OzetGuncelle();
    }

    private void KatlariOlustur()
    {
        var katlar = BusbarYerlesimServisi.KatlariGetir(_vm.Proje, _busbar);
        foreach (var kat in katlar)
        {
            var panolar = _vm.Proje.TumPanolar
                .Where(p => p.BaglandigiBusbarId == _busbar.Id &&
                            string.Equals(p.Kat, kat, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.Ad)
                .ToList();
            double mevcutUzunluk = BusbarYerlesimServisi.KatUzunlugu(_busbar, kat);
            if (_busbar.KatBolumleri.Count == 0 && panolar.Count > 0)
            {
                mevcutUzunluk = panolar
                    .Select(p => _busbar.CikisKutulari.FirstOrDefault(k => k.BagliPanoAd == p.Ad)?.PozisyonM ?? 0)
                    .DefaultIfEmpty(0)
                    .Max() + 0.5;
            }

            KatlarPanel.Children.Add(KatKartiOlustur(kat, mevcutUzunluk, panolar));
        }
    }

    private Control KatKartiOlustur(string kat, double uzunlukM, IReadOnlyList<Pano> panolar)
    {
        var uzunlukBox = MetreKutusu(uzunlukM);
        uzunlukBox.TextChanged += (_, _) => OzetGuncelle();
        _katUzunluklari[kat] = uzunlukBox;

        var baslik = new Grid { ColumnDefinitions = new ColumnDefinitions("120,220,*") };
        baslik.Children.Add(Metin($"Kat {kat}", 15, "#C8E4B0", true));
        var uzunlukAlan = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        uzunlukAlan.Children.Add(Metin("Bu kattaki busbar", 12, "#82B86A"));
        uzunlukAlan.Children.Add(uzunlukBox);
        uzunlukAlan.Children.Add(Metin("m", 12, "#82B86A"));
        Grid.SetColumn(uzunlukAlan, 1);
        baslik.Children.Add(uzunlukAlan);
        var adet = Metin(panolar.Count == 0 ? "Çıkış kutusu yok" : $"{panolar.Count} çıkış kutusu", 12, "#6E9660");
        adet.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(adet, 2);
        baslik.Children.Add(adet);

        var govde = new Grid { ColumnDefinitions = new ColumnDefinitions("120,28,*"), Margin = new Thickness(0, 10, 0, 0) };
        var katNotu = Metin(panolar.Count == 0 ? "Uzunluk 0 bırakılırsa bu katta busbar kabul edilmez." : "Kutular bu katın başlangıcından ölçülür.", 11, "#6E9660");
        katNotu.TextWrapping = TextWrapping.Wrap;
        govde.Children.Add(katNotu);

        var busbarCizgisi = new Border
        {
            Width = 7,
            MinHeight = Math.Max(48, panolar.Count * 68),
            Background = new SolidColorBrush(Color.Parse("#5AAA38")),
            CornerRadius = new CornerRadius(3),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        Grid.SetColumn(busbarCizgisi, 1);
        govde.Children.Add(busbarCizgisi);

        var cikislar = new StackPanel { Spacing = 8 };
        Grid.SetColumn(cikislar, 2);
        if (panolar.Count == 0)
        {
            cikislar.Children.Add(Metin("—", 14, "#45613D"));
        }
        else
        {
            foreach (var pano in panolar)
                cikislar.Children.Add(CikisSatiriOlustur(pano));
        }
        govde.Children.Add(cikislar);

        var icerik = new StackPanel();
        icerik.Children.Add(baslik);
        icerik.Children.Add(govde);
        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#0B150C")),
            BorderBrush = new SolidColorBrush(Color.Parse("#203A1D")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 12),
            Child = icerik
        };
    }

    private Control CikisSatiriOlustur(Pano pano)
    {
        var kutu = _busbar.CikisKutulari.First(k =>
            string.Equals(k.BagliPanoAd, pano.Ad, StringComparison.OrdinalIgnoreCase));
        double yerel = kutu.KatPozisyonM ?? kutu.PozisyonM;
        var konumBox = MetreKutusu(yerel);
        _kutuKonumlari[kutu] = konumBox;

        var satir = new Grid { ColumnDefinitions = new ColumnDefinitions("24,190,*"), MinHeight = 58 };
        satir.Children.Add(new Border
        {
            Width = 16,
            Height = 16,
            Margin = new Thickness(-23, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Color.Parse("#1A3A20")),
            BorderBrush = new SolidColorBrush(Color.Parse("#80E060")),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(2)
        });

        var konumAlan = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, VerticalAlignment = VerticalAlignment.Center };
        konumAlan.Children.Add(Metin("Çıkış", 12, "#82B86A"));
        konumAlan.Children.Add(konumBox);
        konumAlan.Children.Add(Metin("m", 12, "#82B86A"));
        Grid.SetColumn(konumAlan, 1);
        satir.Children.Add(konumAlan);

        var panoKart = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#101D11")),
            BorderBrush = new SolidColorBrush(Color.Parse("#315229")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 7),
            Child = new StackPanel
            {
                Children =
                {
                    Metin(pano.Ad, 13, "#D0E8C0", true),
                    Metin($"İlk pano • {kutu.SigortaA} A • Kutu→pano {kutu.KabloMesafeM:0.##} m", 11, "#7FA56F")
                }
            }
        };
        Grid.SetColumn(panoKart, 2);
        satir.Children.Add(panoKart);
        return satir;
    }

    private static TextBox MetreKutusu(double deger) => new()
    {
        Width = 78,
        Text = deger.ToString("0.##"),
        HorizontalContentAlignment = HorizontalAlignment.Right
    };

    private static TextBlock Metin(string text, double boyut, string renk, bool kalin = false) => new()
    {
        Text = text,
        FontSize = boyut,
        Foreground = new SolidColorBrush(Color.Parse(renk)),
        FontWeight = kalin ? FontWeight.SemiBold : FontWeight.Normal,
        VerticalAlignment = VerticalAlignment.Center
    };

    private void OzetGuncelle()
    {
        double toplam = _katUzunluklari.Values
            .Where(b => NumberHelper.TryParse(b.Text, out _))
            .Sum(b => NumberHelper.TryParse(b.Text, out double d) ? Math.Max(0, d) : 0);
        OzetText.Text = $"{_busbar.BusbarTipiAd} • Besleyen pano: {_busbar.UstPanoAd} • Toplam {toplam:0.##} m • {_busbar.CikisKutulari.Count} çıkış";
    }

    private void Kaydet_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var bolumler = new List<BusbarKatBolumu>();
        foreach (var (kat, box) in _katUzunluklari)
        {
            if (!NumberHelper.TryParse(box.Text, out double uzunluk) || uzunluk < 0)
            {
                DurumText.Text = $"⚠ Kat {kat} busbar uzunluğu geçerli bir sayı olmalıdır.";
                return;
            }
            bolumler.Add(new BusbarKatBolumu { Kat = kat, UzunlukM = uzunluk });
        }

        var yerelKonumlar = new Dictionary<BusbarCikisKutusu, double>();
        foreach (var (kutu, box) in _kutuKonumlari)
        {
            var pano = _vm.Proje.TumPanolar.First(p => p.Ad == kutu.BagliPanoAd);
            if (!NumberHelper.TryParse(box.Text, out double yerel) || yerel < 0)
            {
                DurumText.Text = $"⚠ {pano.Ad} çıkış kutusu konumu geçerli bir sayı olmalıdır.";
                return;
            }
            double katUzunlugu = bolumler.First(b => string.Equals(b.Kat, pano.Kat, StringComparison.OrdinalIgnoreCase)).UzunlukM;
            if (katUzunlugu <= 0 || yerel > katUzunlugu)
            {
                DurumText.Text = $"⚠ {pano.Ad}: çıkış konumu 0–{katUzunlugu:0.##} m arasında olmalıdır.";
                return;
            }
            yerelKonumlar[kutu] = yerel;
        }

        foreach (var (kutu, yerel) in yerelKonumlar)
            kutu.KatPozisyonM = yerel;
        _vm.BusbarKatYerlesiminiUygula(_busbar, bolumler);
        Close(true);
    }

    private void Iptal_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(false);
}
