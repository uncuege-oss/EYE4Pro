using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Threading.Tasks;

namespace Kst.Acoustics.App.Electric.KesifHazirla;

public partial class OnayWindow : Window
{
    public bool Onaylandi { get; private set; }

    public OnayWindow()
    {
        InitializeComponent();
    }

    public OnayWindow(string baslik, string mesaj) : this()
    {
        Title = baslik;
        var txt = this.FindControl<TextBlock>("TxtMesaj");
        if (txt is not null)
            txt.Text = mesaj;
    }

    /// <summary>Onay diyaloğunu modal gösterir; Tamam'a basılırsa true döner.</summary>
    public static async Task<bool> Goster(Window sahip, string baslik, string mesaj)
    {
        var win = new OnayWindow(baslik, mesaj);
        await win.ShowDialog(sahip);
        return win.Onaylandi;
    }

    private void Tamam_Click(object? sender, RoutedEventArgs e)
    {
        Onaylandi = true;
        Close();
    }

    private void Iptal_Click(object? sender, RoutedEventArgs e)
    {
        Onaylandi = false;
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Onaylandi = true;
            Close();
        }
        else if (e.Key == Key.Escape)
        {
            Onaylandi = false;
            Close();
        }
        base.OnKeyDown(e);
    }
}
