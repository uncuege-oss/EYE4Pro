using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Kst.Acoustics.Metraj.App;

public partial class ConfigWindow : Window
{
    public ConfigWindow()
    {
        InitializeComponent();
        DataContext = new ConfigViewModel();
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var filters = new List<FilePickerFileType>();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            filters.Add(new FilePickerFileType("Çalıştırılabilir Dosya") { Patterns = new[] { "ODAFileConverter.exe" } });
            filters.Add(new FilePickerFileType("Tüm EXE") { Patterns = new[] { "*.exe" } });
        }
        else
        {
            filters.Add(new FilePickerFileType("Tüm Dosyalar") { Patterns = new[] { "*" } });
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "ODA File Converter Çalıştırılabilir Dosyasını Seçin",
            AllowMultiple = false,
            FileTypeFilter = filters
        });

        if (files.Count > 0)
        {
            string selectedPath = files[0].Path.LocalPath;
            string fileName = System.IO.Path.GetFileName(selectedPath).ToLowerInvariant();

            // Validate selection
            if (!fileName.Contains("odafileconverter") && !fileName.Contains("oda"))
            {
                var vm = (ConfigViewModel)DataContext!;
                vm.ValidationMessage = "⚠ Seçilen dosya ODA File Converter gibi görünmüyor. Yine de kullanılsın mı?";
                vm.OdaPath = selectedPath;
            }
            else
            {
                var vm = (ConfigViewModel)DataContext!;
                vm.ValidationMessage = null;
                vm.OdaPath = selectedPath;
            }
        }
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e) => Close(true);
    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
