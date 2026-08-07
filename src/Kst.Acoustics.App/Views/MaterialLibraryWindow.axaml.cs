using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Kst.Acoustics.App.ViewModels;

namespace Kst.Acoustics.App.Views;

public partial class MaterialLibraryWindow : Window
{
    public MaterialLibraryWindow()
    {
        DataContext = new MaterialLibraryWindowViewModel();
        InitializeComponent();
    }

    private MaterialLibraryWindowViewModel Vm =>
        (MaterialLibraryWindowViewModel)DataContext!;

    // ── Liste seçimi ──────────────────────────────────────────────────────────

    private void Row_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: MaterialRowViewModel row })
            Vm.SelectedRow = row;
    }

    // ── Araç çubuğu ───────────────────────────────────────────────────────────

    private void NewButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => Vm.StartNew();

    private void SaveButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => Vm.SaveCurrent();

    private void DeleteButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => Vm.DeleteSelected();

    private void FavoriteButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MaterialRowViewModel row })
            row.ToggleFavorite();
    }

    // ── Excel ─────────────────────────────────────────────────────────────────

    private async void ExportButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickSaveFile("Excel'e Aktar", "materyaller.xlsx");
        if (path is null) return;
        try   { Vm.ExportAll(path); }
        catch (Exception ex) { Vm.StatusText = $"Hata: {ex.Message}"; }
    }

    private async void ImportButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickOpenFile("Excel'den Al");
        if (path is null) return;
        try   { Vm.ImportFromExcel(path); }
        catch (Exception ex) { Vm.StatusText = $"Hata: {ex.Message}"; }
    }

    private async void TemplateButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = await PickSaveFile("Şablon İndir", "materyal_sablon.xlsx");
        if (path is null) return;
        try   { Vm.ExportTemplate(path); }
        catch (Exception ex) { Vm.StatusText = $"Hata: {ex.Message}"; }
    }

    private void CloseButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        this.Close();
    }

    // ── Dosya diyalogları ─────────────────────────────────────────────────────

    private async Task<string?> PickSaveFile(string title, string suggestedName)
    {
        var opts = new FilePickerSaveOptions
        {
            Title             = title,
            SuggestedFileName = suggestedName,
            FileTypeChoices   = [new FilePickerFileType("Excel") { Patterns = ["*.xlsx"] }]
        };
        var file = await StorageProvider.SaveFilePickerAsync(opts);
        return file?.TryGetLocalPath();
    }

    private async Task<string?> PickOpenFile(string title)
    {
        var opts = new FilePickerOpenOptions
        {
            Title         = title,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Excel") { Patterns = ["*.xlsx"] }]
        };
        var files = await StorageProvider.OpenFilePickerAsync(opts);
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    // StatusText'i public yapıyoruz (code-behind'dan da yazabilmek için)
    public string StatusText
    {
        set => Vm.StatusText = value;
    }
}
