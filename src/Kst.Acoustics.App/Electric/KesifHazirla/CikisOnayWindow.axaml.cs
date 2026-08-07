using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Threading.Tasks;

namespace Kst.Acoustics.App.Electric.KesifHazirla;

public enum CikisSecimi
{
    Iptal,
    KaydetmedenCik,
    KaydetVeCik
}

public partial class CikisOnayWindow : Window
{
    public CikisSecimi Secim { get; private set; } = CikisSecimi.Iptal;

    public CikisOnayWindow()
    {
        InitializeComponent();
    }

    /// <summary>Kaydedilmemiş değişiklikler diyaloğunu modal gösterir.</summary>
    public static async Task<CikisSecimi> Goster(
        Window sahip,
        string baslik = "Kaydedilmemiş Değişiklikler",
        string mesaj = "Kaydedilmemiş değişiklikler var. Nasıl devam etmek istiyorsunuz?",
        string kaydetmedenMetni = "Kaydetmeden Çık",
        string kaydetMetni = "Kaydet ve Çık")
    {
        var win = new CikisOnayWindow();
        win.Title = baslik;
        win.MesajText.Text = mesaj;
        win.KaydetmedenButton.Content = kaydetmedenMetni;
        win.KaydetButton.Content = kaydetMetni;
        await win.ShowDialog(sahip);
        return win.Secim;
    }

    private void KaydetVeCik_Click(object? sender, RoutedEventArgs e)
    {
        Secim = CikisSecimi.KaydetVeCik;
        Close();
    }

    private void KaydetmedenCik_Click(object? sender, RoutedEventArgs e)
    {
        Secim = CikisSecimi.KaydetmedenCik;
        Close();
    }

    private void Iptal_Click(object? sender, RoutedEventArgs e)
    {
        Secim = CikisSecimi.Iptal;
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Secim = CikisSecimi.Iptal;
            Close();
        }
        base.OnKeyDown(e);
    }
}
