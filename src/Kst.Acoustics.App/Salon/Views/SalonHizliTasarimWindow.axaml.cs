using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Kst.Acoustics.Core.Salon.Models;
using Kst.Acoustics.Core.Salon.Services;

namespace Kst.Acoustics.App.Salon.Views;

public partial class SalonHizliTasarimWindow : Window
{
    private readonly IReadOnlyList<(string Ad, SalonHizliTasarimIstegi Istek)> _ayarlar =
        SalonHizliTasarimServisi.HazirOnAyarlar();
    public SalonHizliTasarimSonucu? Sonuc { get; private set; }

    public SalonHizliTasarimWindow()
    {
        InitializeComponent();
        OnAyarCombo.ItemsSource = _ayarlar.Select(x => x.Ad).ToList();
        OnAyarCombo.SelectedIndex = 1;
    }

    private void OnAyar_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        int i = OnAyarCombo.SelectedIndex;
        if (i < 0 || i >= _ayarlar.Count) return;
        var x = _ayarlar[i].Istek;
        KoltukValue.Value = x.HedefKoltuk;
        KullanimCombo.SelectedIndex = (int)x.Kullanim;
        PlanCombo.SelectedIndex = (int)x.PlanTipi;
        BalkonCheck.IsChecked = x.BalkonEkle;
        YansiticiCheck.IsChecked = x.YansiticiEkle;
    }

    private void Olustur_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var istek = new SalonHizliTasarimIstegi(
                (int)(KoltukValue.Value ?? 300),
                (SalonKullanimTipi)Math.Clamp(KullanimCombo.SelectedIndex, 0, 3),
                (SalonTipi)Math.Clamp(PlanCombo.SelectedIndex, 0, 3),
                BalkonEkle: BalkonCheck.IsChecked == true,
                YansiticiEkle: YansiticiCheck.IsChecked == true);
            Sonuc = SalonHizliTasarimServisi.Olustur(istek);
            Close();
        }
        catch (Exception ex) { DurumText.Text = "Tasarım üretilemedi: " + ex.Message; }
    }

    private void Iptal_OnClick(object? sender, RoutedEventArgs e) => Close();
}
