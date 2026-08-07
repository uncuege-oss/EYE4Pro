using System.Linq;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Kst.Acoustics.Core.BuildingAcoustics.Calculation;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.App.BuildingAcoustics.ViewModels;

namespace Kst.Acoustics.App.BuildingAcoustics.Views;

public partial class MekanDuzenlemePanel : UserControl
{
    private bool _suppressSelectionChanged = false;

    public MekanDuzenlemePanel()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => GuncelleKullanimListesi();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        GuncelleKullanimListesi();
    }

    private void KullanimCombo_DropDownOpened(object? sender, System.EventArgs e)
        => GuncelleKullanimListesi();

    private void GuncelleKullanimListesi()
    {
        var cb = this.FindControl<ComboBox>("KullanimCombo");
        if (cb == null) return;

        var islevi = GetBinaIslevi();
        var adlar = EK2SiniflandirmaServisi.GetKategoriler(islevi).Select(k => k.Ad).ToArray();

        _suppressSelectionChanged = true;
        try
        {
            cb.ItemsSource = adlar;
            var mekan = DataContext as Mekan;
            cb.SelectedItem = (mekan != null && adlar.Contains(mekan.KullanimTipi))
                ? mekan.KullanimTipi
                : null;
        }
        finally
        {
            _suppressSelectionChanged = false;
        }
    }

    private void KullanimCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionChanged) return;

        var mekan = DataContext as Mekan;
        var secilenAd = (sender as ComboBox)?.SelectedItem as string;
        if (mekan == null || string.IsNullOrEmpty(secilenAd)) return;

        var islevi = GetBinaIslevi();
        var (kaynak, alici) = EK2SiniflandirmaServisi.KategoridekiDegerleri(islevi, secilenAd);

        mekan.KullanimTipi   = secilenAd;
        mekan.KaynakDerecesi = kaynak;
        mekan.AliciSinifi    = alici;
        if (EK2SiniflandirmaServisi.HesapDisiMi(secilenAd))
            mekan.Sinif = Kst.Acoustics.Core.BuildingAcoustics.Models.MekanSinifi.HesapDisi;
    }

    private BinaIslevi GetBinaIslevi()
    {
        var pencere = this.FindAncestorOfType<Window>();
        var vm = pencere?.DataContext as BinaProjesiViewModel;
        return vm?.SeciliBinaIslevi?.Islevi ?? BinaIslevi.Tanimsiz;
    }

    /// EK-5/EK-6 girdileri (tesisat düzeyleri, emilim alanı) değişince projeyi kirli
    /// işaretle — Mekan bu alanlar için PropertyChanged yükseltmez (sade set;), otomatik
    /// izleme yakalayamaz.
    private void TesisatGurultu_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        var pencere = this.FindAncestorOfType<Window>();
        (pencere?.DataContext as BinaProjesiViewModel)?.Kirlet();
    }
}
