using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System.Globalization;

namespace Kst.Acoustics.App.BuildingAcoustics.Views;

public partial class DxfOlcekModalWindow : Window
{
    /// Pencere kapanırken seçilen katsayı. null = iptal edildi.
    public double? SecilenKatsayi { get; private set; }

    public DxfOlcekModalWindow()
    {
        InitializeComponent();
    }

    public DxfOlcekModalWindow(double mevcutOlcek) : this()
    {
        MevcutOlcekLabel.Text = mevcutOlcek.ToString("0.######", CultureInfo.InvariantCulture);
        // Mevcut değere göre uygun radio butonu işaretle
        if (System.Math.Abs(mevcutOlcek - 0.001) < 1e-9) RbMm.IsChecked = true;
        else if (System.Math.Abs(mevcutOlcek - 0.01) < 1e-9) RbCm.IsChecked = true;
        else if (System.Math.Abs(mevcutOlcek - 1.0) < 1e-9) RbM.IsChecked = true;
        else if (System.Math.Abs(mevcutOlcek - 0.0254) < 1e-9) RbIn.IsChecked = true;
        else { RbOzel.IsChecked = true; OzelKatsayi.Value = (decimal)mevcutOlcek; }
    }

    private void Uygula_OnClick(object? sender, RoutedEventArgs e)
    {
        SecilenKatsayi = SeciliKatsayi();
        Close();
    }

    private void Iptal_OnClick(object? sender, RoutedEventArgs e)
    {
        SecilenKatsayi = null;
        Close();
    }

    private double SeciliKatsayi()
    {
        if (RbMm.IsChecked == true) return 0.001;
        if (RbCm.IsChecked == true) return 0.01;
        if (RbM.IsChecked == true) return 1.0;
        if (RbIn.IsChecked == true) return 0.0254;
        if (RbOzel.IsChecked == true) return (double)(OzelKatsayi.Value ?? 1.0m);
        return 1.0;
    }
}
