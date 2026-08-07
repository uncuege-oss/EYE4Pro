using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Kst.Acoustics.App.Controls;
using Kst.Acoustics.App.ViewModels;

namespace Kst.Acoustics.App.Views;

public partial class MainWindow : Window
{
    private bool _forceClose;
    private bool _closingPromptActive;
    private bool _destructiveOperationActive;

    public MainWindow()
    {
        InitializeComponent();

        // Viewport tıklama callback'ini bağla
        LeftGeometryPreview.OnPlacementPicked = (projX, projY, projection) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.OnViewportPlacementPicked(projX, projY, projection);
            }
        };
    }

    // --- ESC ile placement modu iptal ---
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && DataContext is MainWindowViewModel vm && vm.IsPlacementActive)
        {
            vm.CancelPlacement();
            e.Handled = true;
        }
    }

    // --- Kaydedilmemiş değişiklik koruması: pencere kapatılırken sorar ---
    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (!_forceClose && DataContext is MainWindowViewModel vm && vm.IsDirty)
        {
            e.Cancel = true;
            if (_closingPromptActive) return;

            _closingPromptActive = true;
            try
            {
                var result = await UnsavedChangesGate.ConfirmProceedAsync(this, true, () => vm.SaveProjectAsync(this));
                if (result != UnsavedProceedResult.Cancelled)
                {
                    _forceClose = true;
                    Close();
                }
            }
            finally { _closingPromptActive = false; }
            return;
        }

        base.OnClosing(e);
    }

    private async Task<UnsavedProceedResult> ConfirmDiscardAsync(MainWindowViewModel vm) =>
        await UnsavedChangesGate.ConfirmProceedAsync(this, vm.IsDirty, () => vm.SaveProjectAsync(this));

    // --- Dosya işlemleri ---
    private async void OpenFileButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_destructiveOperationActive || DataContext is not MainWindowViewModel viewModel) return;
        _destructiveOperationActive = true;
        try
        {
            if (await ConfirmDiscardAsync(viewModel) == UnsavedProceedResult.Cancelled) return;
            await viewModel.PickAndImportAsync(this);
        }
        finally { _destructiveOperationActive = false; }
    }

    private async void SaveProjectButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
            await viewModel.SaveProjectAsync(this);
    }

    private async void OpenProjectButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_destructiveOperationActive || DataContext is not MainWindowViewModel viewModel) return;
        _destructiveOperationActive = true;
        try
        {
            if (await ConfirmDiscardAsync(viewModel) == UnsavedProceedResult.Cancelled) return;
            await viewModel.OpenProjectAsync(this);
        }
        finally { _destructiveOperationActive = false; }
    }

    private async void SalonYukleButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_destructiveOperationActive || DataContext is not MainWindowViewModel viewModel) return;
        _destructiveOperationActive = true;
        try
        {
            if (await ConfirmDiscardAsync(viewModel) == UnsavedProceedResult.Cancelled) return;
            await viewModel.SalonYukleAsync(this);
        }
        finally { _destructiveOperationActive = false; }
    }

    // --- Kategori ---
    private void ApplySurfaceCategoryButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
            viewModel.ApplyCategoryToSelectedSurface();
    }

    private void ApplyBatchCategoryButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
            viewModel.ApplyCategoryToSelectedGroup();
    }

    // --- Viewport araçları ---
    private void ResetLeftViewButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        LeftGeometryPreview.ResetViewport();
    }

    private void FocusLeftViewButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
            LeftGeometryPreview.FocusSurface(viewModel.HighlightedSurfaceId);
    }

    private void SurfaceDataGrid_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel) return;
        var selected = SurfaceDataGrid.SelectedItems.OfType<SurfacePreviewViewModel>().ToArray();
        viewModel.ReplaceSelectionFromList(selected);
    }

    // --- Görünürlük ---
    private void HideSelectedButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel) viewModel.HideSelectedSurfaces();
    }

    private void IsolateSelectedButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel) viewModel.IsolateSelectedSurfaces();
    }

    private void ShowAllButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel) viewModel.ShowAllSurfaces();
    }

    // --- Araçlar ---
    private void StartProjectButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        var project = vm.BuildCurrentProject();
        if (project is null) return;

        var projectWindow = new ProjectWindow(project, vm);
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = projectWindow;
        }
        projectWindow.Show();
        // Proje durumu ProjectWindow'a taşındı (veri kaybı yok) — kapatma onayı atlanır.
        _forceClose = true;
        this.Close();
    }

    private async void BackToLauncher_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_destructiveOperationActive) return;
        _destructiveOperationActive = true;
        try
        {
            if (DataContext is MainWindowViewModel viewModel &&
                await ConfirmDiscardAsync(viewModel) == UnsavedProceedResult.Cancelled) return;

            var launcher = new LauncherWindow();
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = launcher;
            }
            launcher.Show();
            _forceClose = true;
            this.Close();
        }
        finally { _destructiveOperationActive = false; }
    }

    private async void RunLeakTestButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel) await viewModel.RunLeakTestAsync();
    }

    private void ClearLeakTestButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel) viewModel.ClearLeakTest();
    }

    private void ConvertUnitsButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel) viewModel.ConvertProjectUnits();
    }

    private void ApplyScaleButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel) viewModel.ApplyCustomScale();
    }

}
