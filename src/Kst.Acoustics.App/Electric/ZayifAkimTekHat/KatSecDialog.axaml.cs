using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Kst.Acoustics.Core.Electric.ZayifAkimTekHat;

namespace Kst.Acoustics.App.Electric.ZayifAkimTekHat;

public partial class KatSecDialog : Window
{
    public bool Onaylandi { get; private set; }
    public int SeciliKatNo { get; private set; }
    public string? SeciliUstRackId { get; private set; }

    public KatSecDialog() { InitializeComponent(); }

    public KatSecDialog(IEnumerable<ELVKatInfo> katlar, int? onSeciliKatNo, string baslik,
        IReadOnlyList<RackBaglantiSecenek>? baglantiSecenekleri = null, string? onSeciliUstRackId = null)
    {
        InitializeComponent();
        BaslikText.Text = baslik;

        var liste = katlar?.ToList() ?? new List<ELVKatInfo>();
        if (liste.Count == 0)
            liste.Add(new ELVKatInfo { KatNo = 0, Ad = "Zemin Kat" });

        KatCombo.ItemsSource = liste;
        KatCombo.SelectedItem = liste.FirstOrDefault(k => k.KatNo == onSeciliKatNo) ?? liste[0];

        if (baglantiSecenekleri is { Count: > 0 })
        {
            BaglantiCombo.ItemsSource = baglantiSecenekleri;
            BaglantiCombo.SelectedItem = baglantiSecenekleri.FirstOrDefault(o => o.Id == onSeciliUstRackId)
                                         ?? baglantiSecenekleri[0];
        }
        else
        {
            BaglantiLabel.IsVisible = false;
            BaglantiCombo.IsVisible = false;
        }
    }

    private void Iptal_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Onaylandi = false;
        Close();
    }

    private void Ekle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (KatCombo.SelectedItem is ELVKatInfo kat)
        {
            SeciliKatNo = kat.KatNo;
            SeciliUstRackId = (BaglantiCombo.SelectedItem as RackBaglantiSecenek)?.Id;
            Onaylandi = true;
        }
        Close();
    }
}
