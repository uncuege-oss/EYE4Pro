using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Kst.Acoustics.Core.Salon.Services;

namespace Kst.Acoustics.App.Salon.Views;

public partial class SalonKomutPaletiWindow : Window
{
    public SalonKomutTanimi? SeciliKomut { get; private set; }
    public SalonKomutPaletiWindow()
    {
        InitializeComponent();
        Yenile(null);
        Opened += (_, _) => AramaText.Focus();
    }
    private void Yenile(string? q)
    {
        KomutList.ItemsSource = SalonKomutPaletiServisi.Ara(q, 50);
        KomutList.SelectedIndex = KomutList.ItemCount > 0 ? 0 : -1;
    }
    private void Arama_TextChanged(object? sender, TextChangedEventArgs e) => Yenile(AramaText.Text);
    private void Sec()
    {
        if (KomutList.SelectedItem is not SalonKomutTanimi k) return;
        SeciliKomut = k; Close();
    }
    private void KomutList_DoubleTapped(object? sender, TappedEventArgs e) => Sec();
    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { Sec(); e.Handled = true; }
        else if (e.Key == Key.Escape) { Close(); e.Handled = true; }
        else if (e.Key == Key.Down && KomutList.ItemCount > 0)
        { KomutList.SelectedIndex = Math.Min(KomutList.ItemCount - 1, KomutList.SelectedIndex + 1); e.Handled = true; }
        else if (e.Key == Key.Up && KomutList.ItemCount > 0)
        { KomutList.SelectedIndex = Math.Max(0, KomutList.SelectedIndex - 1); e.Handled = true; }
    }
}
