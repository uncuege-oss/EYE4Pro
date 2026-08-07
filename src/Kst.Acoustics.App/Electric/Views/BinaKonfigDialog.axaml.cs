using Avalonia.Controls;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.App.Electric.Views;

public partial class BinaKonfigDialog : Window
{
    private readonly BinaKonfigurasyonu _bina;
    public bool Onaylandi { get; private set; }

    public BinaKonfigDialog() { InitializeComponent(); _bina = null!; }

    public BinaKonfigDialog(BinaKonfigurasyonu bina)
    {
        InitializeComponent();
        _bina = bina;
        BodrumCheck.IsChecked = bina.BodrumSayisi > 0;
        BodrumBox.Value = Math.Max(1, bina.BodrumSayisi);
        BodrumBox.IsEnabled = BodrumCheck.IsChecked ?? false;
        NormalBox.Value = bina.NormalKatSayisi;
        CatiCheck.IsChecked = bina.CatiKati > 0;
        Onizle();

        BodrumCheck.IsCheckedChanged += (_, _) =>
        {
            BodrumBox.IsEnabled = BodrumCheck.IsChecked ?? false;
            Onizle();
        };
        BodrumBox.ValueChanged += (_, _) => Onizle();
        NormalBox.ValueChanged += (_, _) => Onizle();
        CatiCheck.IsCheckedChanged += (_, _) => Onizle();
    }

    private void Onizle()
    {
        var prov = new BinaKonfigurasyonu
        {
            BodrumSayisi = (BodrumCheck.IsChecked ?? false) ? (int)(BodrumBox.Value ?? 1) : 0,
            ZeminVar = true,
            NormalKatSayisi = (int)(NormalBox.Value ?? 0),
            CatiKati = (CatiCheck.IsChecked ?? false) ? 1 : 0
        };
        OnizlemeText.Text = string.Join("  →  ", prov.KatEtiketleri());
    }

    private void Iptal_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) { Onaylandi = false; Close(); }

    private void Kaydet_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _bina.BodrumSayisi = (BodrumCheck.IsChecked ?? false) ? (int)(BodrumBox.Value ?? 1) : 0;
        _bina.ZeminVar = true;
        _bina.NormalKatSayisi = (int)(NormalBox.Value ?? 0);
        _bina.CatiKati = (CatiCheck.IsChecked ?? false) ? 1 : 0;
        Onaylandi = true;
        Close();
    }
}
