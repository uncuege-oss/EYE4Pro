using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Kst.Acoustics.App.ViewModels;

namespace Kst.Acoustics.App.Views;

public partial class LoudspeakerLibraryWindow : Window
{
    public LoudspeakerLibraryWindow()
    {
        DataContext = new LoudspeakerLibraryWindowViewModel();
        InitializeComponent();
    }

    private LoudspeakerLibraryWindowViewModel Vm =>
        (LoudspeakerLibraryWindowViewModel)DataContext!;

    private void Row_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: LoudspeakerRowViewModel row })
            Vm.SelectedRow = row;
    }

    private async void ImportButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickOpenFile("CF1/CLF Hoparlör Verisi Al");
        if (path is null) return;

        try { Vm.ImportCf1(path); }
        catch (Exception ex) { Vm.StatusText = $"Hata: {ex.Message}"; }
    }

    private void DeleteButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => Vm.DeleteSelected();

    private async Task<string?> PickOpenFile(string title)
    {
        var opts = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("CLF/CF1") { Patterns = ["*.cf1", "*.clf", "*.txt"] },
                new FilePickerFileType("Tüm dosyalar") { Patterns = ["*"] }
            ]
        };
        var files = await StorageProvider.OpenFilePickerAsync(opts);
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    private void CloseButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        this.Close();
    }
}
