using Avalonia.Controls;
using Kst.Acoustics.App.BuildingAcoustics.ViewModels;

namespace Kst.Acoustics.App.BuildingAcoustics.Views;

public partial class YapiElemaniKutuphaneWindow : Window
{
    public YapiElemaniKutuphaneWindow()
    {
        InitializeComponent();
    }

    private void KutuphaneGrid_OnCellEditEnded(
        object? sender,
        DataGridCellEditEndedEventArgs e)
    {
        if (e.EditAction == DataGridEditAction.Commit &&
            DataContext is BinaProjesiViewModel { DuzenlemeEtkin: true } vm)
        {
            // Kütüphane nesneleri proje bölmelerinde canlı referans olarak tutulur.
            // Hücre değişir değişmez mevcut hesabı bayat ve belgeyi kirli işaretle.
            vm.Kirlet();
            vm.KutuphaneDuvarListesiniYenile();
        }
    }

    private void OnayarGrid_OnCellEditEnded(
        object? sender,
        DataGridCellEditEndedEventArgs e)
    {
        if (e.EditAction == DataGridEditAction.Commit &&
            DataContext is BinaProjesiViewModel { DuzenlemeEtkin: true } vm)
        {
            vm.Kirlet();
        }
    }
}
