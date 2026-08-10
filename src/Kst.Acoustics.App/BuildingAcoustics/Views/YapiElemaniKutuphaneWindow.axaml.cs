using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Kst.Acoustics.App.BuildingAcoustics.ViewModels;
using Kst.Acoustics.Core.BuildingAcoustics.Database;
using Kst.Acoustics.Core.BuildingAcoustics.Models;

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

    private async void UrunBelgesiEkle_OnClick(
        object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not BinaProjesiViewModel vm || vm.SeciliKutuphaneElemani is null)
        {
            if (DataContext is BinaProjesiViewModel bosVm)
                bosVm.KutuphaneMesaji = "Önce belge bağlanacak yapı elemanını seçin.";
            return;
        }
        if (!vm.DuzenlemeEtkin)
        {
            vm.KutuphaneMesaji = "Kilitli projede ürün belgesi eklenemez.";
            return;
        }

        var dosyalar = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Ürün Belgesi Seç",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("Teknik belgeler")
                {
                    Patterns = ["*.pdf", "*.doc", "*.docx", "*.xls", "*.xlsx", "*.jpg", "*.jpeg", "*.png"]
                },
                FilePickerFileTypes.All
            ]
        });
        if (dosyalar.Count == 0) return;

        try
        {
            foreach (var dosya in dosyalar)
                UrunBelgesiServisi.Ekle(vm.SeciliKutuphaneElemani, dosya.Path.LocalPath);
            UrunBelgeleriniYenile(vm.SeciliKutuphaneElemani);
            vm.Kirlet();
            vm.KutuphaneMesaji = $"{dosyalar.Count} ürün belgesi yönetimli klasöre kopyalandı; kütüphaneyi kaydetmeyi unutmayın.";
        }
        catch (Exception ex)
        {
            vm.KutuphaneMesaji = $"Ürün belgesi eklenemedi: {ex.Message}";
        }
    }

    private void UrunBelgesiAc_OnClick(
        object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (UrunBelgesiGrid.SelectedItem is not UrunBelgesi belge) return;
        var hedef = File.Exists(belge.DosyaYolu) ? belge.DosyaYolu : belge.Url;
        if (string.IsNullOrWhiteSpace(hedef))
        {
            if (DataContext is BinaProjesiViewModel vm)
                vm.KutuphaneMesaji = "Seçili belgenin erişilebilir dosya veya URL bilgisi yok.";
            return;
        }
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = hedef,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            if (DataContext is BinaProjesiViewModel vm)
                vm.KutuphaneMesaji = $"Belge açılamadı: {ex.Message}";
        }
    }

    private void UrunBelgesiSil_OnClick(
        object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not BinaProjesiViewModel { DuzenlemeEtkin: true } vm ||
            vm.SeciliKutuphaneElemani is null ||
            UrunBelgesiGrid.SelectedItem is not UrunBelgesi belge)
            return;

        // Fiziksel arşiv dosyası veri kaybına karşı tutulur; yalnız katalog ilişkisi kaldırılır.
        vm.SeciliKutuphaneElemani.Belgeler.Remove(belge);
        UrunBelgeleriniYenile(vm.SeciliKutuphaneElemani);
        vm.Kirlet();
        vm.KutuphaneMesaji = "Belge ilişkisi kaldırıldı; yönetimli arşiv dosyası korunuyor.";
    }

    private void UrunBelgeleriniYenile(YapiElemani eleman)
    {
        UrunBelgesiGrid.ItemsSource = null;
        UrunBelgesiGrid.ItemsSource = eleman.Belgeler;
    }
}
