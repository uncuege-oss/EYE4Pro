using Avalonia.Controls;

namespace Kst.Acoustics.App.Electric.KesifHazirla;

public partial class PozAtamaWindow : Window
{
    private KesifHazirlaViewModel ViewModel => (KesifHazirlaViewModel)DataContext!;

    public PozAtamaWindow()
    {
        InitializeComponent();
    }

    private void PozAta_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel.SeciliEslenmemisKalem is null)
        {
            ViewModel.DurumMesaji = "Eşlenecek metraj kalemini seçin.";
            return;
        }

        if (ViewModel.AtanacakPoz is null)
        {
            ViewModel.DurumMesaji = "Eşlenecek pozu seçin.";
            return;
        }

        ViewModel.EslemeAtaVeUygula(ViewModel.SeciliEslenmemisKalem, ViewModel.AtanacakPoz);
    }

    private void EslemeGuncelle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel.SeciliEsleme is null)
        {
            ViewModel.DurumMesaji = "Güncellenecek eşleşmeyi alt tablodan seçin.";
            return;
        }

        if (ViewModel.AtanacakPoz is null)
        {
            ViewModel.DurumMesaji = "Yeni pozu seçin.";
            return;
        }

        ViewModel.EslemeGuncelle(ViewModel.SeciliEsleme, ViewModel.AtanacakPoz);
    }

    private void EslemeSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel.SeciliEsleme is null)
        {
            ViewModel.DurumMesaji = "Silinecek eşleşmeyi seçin.";
            return;
        }

        ViewModel.EslemeSil(ViewModel.SeciliEsleme);
    }

    private void OtomatikAta_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ViewModel.TumEslenmemisPozlariOtomatikAta();

    private void Kontrol_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ViewModel.EslemeKontrolDurumuKaydet();

    private void Kapat_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
}
