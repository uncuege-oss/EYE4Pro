using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Kst.Acoustics.Core.Electric.KesifHazirla;

namespace Kst.Acoustics.App.Electric.KesifHazirla;

public partial class ReferanslarWindow : Window
{
    private KesifHazirlaViewModel ViewModel => (KesifHazirlaViewModel)DataContext!;

    public ReferanslarWindow()
    {
        InitializeComponent();
    }

    private async void ExcelYukle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Metraj excel'i yükle",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Metraj Excel") { Patterns = ["*.xlsx"] },
                FilePickerFileTypes.All
            ]
        });

        var path = files.FirstOrDefault()?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            var referans = await Task.Run(() => KesifMetrajImportServisi.DosyadanOku(path));
            if (referans.MetrajSatirlari.Count == 0)
            {
                ViewModel.DurumMesaji = "Metraj satırı bulunamadı; dosya yapısını kontrol edin.";
                return;
            }

            ViewModel.ReferansEkle(referans);
        }
        catch (Exception ex)
        {
            ViewModel.DurumMesaji = $"Metraj excel'i yüklenemedi: {ex.Message}";
        }
    }

    private void ExcelSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel.SeciliReferans is null)
        {
            ViewModel.DurumMesaji = "Silmek için bir referans seçin.";
            return;
        }

        ViewModel.ReferansSil(ViewModel.SeciliReferans);
    }

    private async void ExcelSablonu_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Metraj şablonu kaydet",
            SuggestedFileName = "metraj_sablonu.xlsx",
            FileTypeChoices = [new FilePickerFileType("Excel") { Patterns = ["*.xlsx"] }]
        });

        var path = file?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            await Task.Run(() => KesifMetrajImportServisi.SablonOlustur(path));
            ViewModel.DurumMesaji = "Metraj şablonu kaydedildi.";
        }
        catch (Exception ex)
        {
            ViewModel.DurumMesaji = $"Şablon kaydedilemedi: {ex.Message}";
        }
    }

    private void Kapat_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
}
