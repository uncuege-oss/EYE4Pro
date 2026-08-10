using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Kst.Acoustics.App.ViewModels;
using Kst.Acoustics.Core.Projects;
using CategoryNodeViewModel  = Kst.Acoustics.App.ViewModels.CategoryNodeViewModel;
using SurfaceNodeViewModel   = Kst.Acoustics.App.ViewModels.SurfaceNodeViewModel;

namespace Kst.Acoustics.App.Views;

public partial class ProjectWindow : Window
    {
        private readonly MainWindowViewModel? _parentVm;
        private bool _forceClose;
        private bool _closeConfirmationInProgress;

        public ProjectWindow()
        {
            var vm = new ProjectWindowViewModel(ProjectFactory.CreateEmpty("Yeni Proje"));
            DataContext = vm;
            InitializeComponent();
            vm.StartAutoSave();
        }

        public ProjectWindow(
            AcousticsProject project,
            MainWindowViewModel? parentVm = null,
            IReadOnlyList<string>? sessionWarnings = null)
        {
            try
            {
                _parentVm = parentVm;
                var vm = new ProjectWindowViewModel(
                    project,
                    sessionWarnings ?? parentVm?.SessionWarnings);
                DataContext = vm;
                InitializeComponent();
                if (parentVm is not null)
                {
                    vm.SetDocumentState(
                        parentVm.SavedProjectPath,
                        parentVm.IsDirty,
                        parentVm.RecoveredFromBackup);
                    vm.SavedSnapshotAvailable += (snapshot, path) =>
                        parentVm.UpdateProject(
                            snapshot,
                            isDirty: false,
                            savedProjectPath: path,
                            recoveredFromBackup: false);
                }

                GeometryPreview.OnPlacementPicked = (projX, projY, projection) =>
                {
                    if (DataContext is ProjectWindowViewModel pointVm)
                        pointVm.OnViewportPlacementPicked(projX, projY, projection);
                };

                // Simülasyon bitince Sonuçlar sekmesine otomatik geç
                vm.NavigateToResults += () => ModuleTabControl.SelectedIndex = 5;
                vm.StartAutoSave();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ProjectWindow constructor error: {ex}");
                throw;
            }
        }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is ProjectWindowViewModel simulationVm)
            simulationVm.CancelActiveSimulation();
        if (DataContext is IDisposable disposable)
            disposable.Dispose();

        base.OnClosed(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (DataContext is not ProjectWindowViewModel vm) return;

        var primaryModifier = e.KeyModifiers.HasFlag(KeyModifiers.Control) ||
                              e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (primaryModifier && e.Key == Key.Z)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) vm.RedoDocument();
            else vm.UndoDocument();
            e.Handled = true;
            return;
        }
        if (primaryModifier && e.Key == Key.Y)
        {
            vm.RedoDocument();
            e.Handled = true;
            return;
        }
        if (primaryModifier && e.Key == Key.S)
        {
            _ = vm.SaveProjectAsync(this);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && vm.IsPlacementActive)
        {
            vm.CancelPlacement();
            e.Handled = true;
        }
    }

    // --- Kaydedilmemiş değişiklik koruması: pencere kapatılırken sorar ---
    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (_forceClose)
        {
            base.OnClosing(e);
            return;
        }

        if (DataContext is not ProjectWindowViewModel vm || !vm.IsDirty)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        if (_closeConfirmationInProgress) return;

        _closeConfirmationInProgress = true;
        try
        {
            var result = await UnsavedChangesGate.ConfirmProceedAsync(this, true, () => vm.SaveProjectAsync(this));
            if (result != UnsavedProceedResult.Cancelled)
            {
                _forceClose = true;
                Close();
            }
        }
        finally
        {
            _closeConfirmationInProgress = false;
        }
    }

    private async Task<UnsavedProceedResult> ConfirmDiscardAsync()
    {
        if (DataContext is not ProjectWindowViewModel vm) return UnsavedProceedResult.Unchanged;
        return await UnsavedChangesGate.ConfirmProceedAsync(this, vm.IsDirty, () => vm.SaveProjectAsync(this));
    }

    private readonly ProjectFileService _fileService = new();
    private readonly ProjectPackageService _packageService = new();
    private bool _isOpeningProject;

    private async void OpenProjectButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isOpeningProject) return; // çift tıklama koruması — açma zaten sürüyor
        _isOpeningProject = true;
        try
        {
            var proceed = await ConfirmDiscardAsync();
            if (proceed == UnsavedProceedResult.Cancelled) return;

            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "EYE4PRO proje dosyasını açın",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("EYE4PRO Akustik Proje") { Patterns = ["*.e4p01", "*.e4pkg"] },
                    new FilePickerFileType("EYE4PRO Taşınabilir Paket") { Patterns = ["*.e4pkg"] },
                    new FilePickerFileType("JSON Dosyası") { Patterns = ["*.json"] }
                ]
            });

            var path = files.FirstOrDefault()?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path)) return;

            if (DataContext is ProjectWindowViewModel current)
                current.ReportError("Proje açılıyor...");

            // Büyük proje/paket dosyalarında UI thread'ini kilitlememek için
            // senkron Load yerine asenkron, doğrulamalı akış kullanılır.
            var isPackage = string.Equals(Path.GetExtension(path), ".e4pkg", StringComparison.OrdinalIgnoreCase);
            ProjectLoadResult loadResult;
            if (isPackage)
            {
                var package = await _packageService.LoadProjectAsync(path);
                var libraryNote = ImportMissingPackageLibraries(package);
                loadResult = new ProjectLoadResult(
                    package.Project,
                    false,
                    [$"Taşınabilir paket doğrulandı ve açıldı.{libraryNote}"]);
            }
            else
            {
                loadResult = await _fileService.LoadWithStatusAsync(path);
            }
            var project = loadResult.Project;
            var newWindow = new ProjectWindow(project, _parentVm, loadResult.SessionWarnings);
            if (newWindow.DataContext is ProjectWindowViewModel newVm)
            {
                newVm.SetDocumentState(
                    isPackage ? null : path,
                    isPackage || loadResult.RecoveredFromBackup,
                    loadResult.RecoveredFromBackup);
                if (loadResult.RecoveredFromBackup)
                    newVm.ReportError("Proje son sağlam yedekten kurtarıldı. İçeriği doğrulayıp yeniden kaydedin.");
                else if (isPackage)
                    newVm.ReportError("Taşınabilir paket açıldı. Çalışma kopyasını .e4p01 olarak kaydedin.");
            }

            // Yeni açılan belge artık "Kaydetmeden Çık" için de son sağlam baseline'dır.
            // Ebeveyn senkronlanmazsa kullanıcı daha sonra yaptığı bir düzenlemeyi atarken
            // açtığı projeden önceki belgeye geri döner.
            if (!loadResult.RecoveredFromBackup)
                _parentVm?.UpdateProject(
                    project,
                    isDirty: isPackage,
                    savedProjectPath: isPackage ? null : path);
            newWindow.Show();
            _forceClose = true;
            Close();
        }
        catch (Exception ex)
        {
            if (DataContext is ProjectWindowViewModel vm)
                vm.ReportError(BuildOpenErrorMessage(ex));
            Debug.WriteLine($"ProjectWindow Open Error: {ex}");
        }
        finally
        {
            _isOpeningProject = false;
        }
    }

    private async void SaveProjectButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm) await vm.SaveProjectAsync(this);
    }

    private async void SaveAsProjectButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm) await vm.SaveAsProjectAsync(this);
    }

    private void UndoDocumentButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm) vm.UndoDocument();
    }

    private void RedoDocumentButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm) vm.RedoDocument();
    }

    private async void CreateProjectPackageButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not ProjectWindowViewModel vm || IsCreatingPackagePickerOpen) return;
        IsCreatingPackagePickerOpen = true;
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Taşınabilir proje paketini kaydedin",
                SuggestedFileName = MakePackageFileName(vm.ProjectName),
                DefaultExtension = "e4pkg",
                FileTypeChoices =
                [
                    new FilePickerFileType("EYE4PRO Taşınabilir Proje Paketi")
                    {
                        Patterns = ["*.e4pkg"]
                    }
                ]
            });
            var path = file?.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(path))
                await vm.CreateProjectPackageAsync(path);
        }
        catch (Exception ex)
        {
            vm.ReportError($"Paket konumu seçilemedi: {ex.Message}");
        }
        finally
        {
            IsCreatingPackagePickerOpen = false;
        }
    }

    private bool IsCreatingPackagePickerOpen { get; set; }

    private static string MakePackageFileName(string projectName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(projectName
            .Select(character => invalid.Contains(character) ? '_' : character)
            .ToArray()).Trim();
        return $"{(string.IsNullOrWhiteSpace(cleaned) ? "YeniProje" : cleaned)}.e4pkg";
    }

    private static string ImportMissingPackageLibraries(ProjectPackageLoadResult package)
    {
        var materialCount = 0;
        foreach (var material in package.Materials)
        {
            if (MaterialDatabase.Instance.FindById(material.Id) is null &&
                MaterialDatabase.Instance.Add(material))
                materialCount++;
        }

        var loudspeakerCount = 0;
        foreach (var loudspeaker in package.Loudspeakers)
        {
            if (LoudspeakerDatabase.Instance.FindById(loudspeaker.Id) is null &&
                LoudspeakerDatabase.Instance.Add(loudspeaker))
                loudspeakerCount++;
        }

        return materialCount + loudspeakerCount == 0
            ? string.Empty
            : $" {materialCount} materyal ve {loudspeakerCount} hoparlör tanımı yerel kütüphaneye eklendi.";
    }

    private void PreviewAssistedPlacementButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm) vm.PreviewAssistedPointPlacement();
    }

    private void ApplyAssistedPlacementButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm) vm.ApplyAssistedPointPlacement();
    }

    private void ApplyMaterialMappingButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm) vm.ApplySelectedMaterialMappingTemplate();
    }

    private async void SaveMaterialMappingButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm)
            await vm.SaveCurrentMaterialMappingTemplateAsync();
    }

    private async void DeleteMaterialMappingButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm)
            await vm.DeleteSelectedMaterialMappingTemplateAsync();
    }

    private async void CreateProjectVersionButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm)
            await vm.CreateProjectVersionAsync("Elle oluşturulan kontrol noktası");
    }

    private async void RefreshProjectVersionsButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm)
            await vm.RefreshProjectVersionHistoryAsync();
    }

    private async void RestoreProjectVersionButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm &&
            ProjectVersionDataGrid.SelectedItem is ProjectVersionInfo version)
            await vm.RestoreProjectVersionAsync(version);
    }

    private async void DeleteProjectVersionButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm &&
            ProjectVersionDataGrid.SelectedItem is ProjectVersionInfo version)
            await vm.DeleteProjectVersionAsync(version);
    }

    private void ResetViewButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        GeometryPreview.ResetViewport();
    }

    private void CategoryExpand_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Avalonia.Controls.Button { DataContext: CategoryNodeViewModel node })
            node.ToggleExpanded();
    }

    private void CategoryVisibility_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Avalonia.Controls.Button { DataContext: CategoryNodeViewModel node })
            node.ToggleVisibility();
    }

    private void SurfaceNode_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Avalonia.Controls.Button { DataContext: SurfaceNodeViewModel surf })
            surf.Select();
    }

    private async void OpenMaterialLibraryButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var win = new MaterialLibraryWindow();
        await win.ShowDialog(this);
        if (DataContext is ProjectWindowViewModel vm)
            vm.RefreshMaterialAssignmentLists();
    }

    private void AssignMaterialToSurfacesButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm) vm.AssignMaterialToSelectedSurfaces();
    }

    private void ResetSurfaceMaterialsButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm) vm.ResetSelectedSurfacesToDefault();
    }

    private void SurfaceMaterialDataGrid_OnSelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
    {
        if (DataContext is not ProjectWindowViewModel vm || sender is not Avalonia.Controls.DataGrid grid)
            return;

        var rows = grid.SelectedItems?
            .OfType<SurfaceMaterialRowViewModel>()
            .ToList() ?? new List<SurfaceMaterialRowViewModel>();

        vm.UpdateSurfaceMaterialSelection(rows);
    }

    private async void LoudspeakerLibraryButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var window = new LoudspeakerLibraryWindow();
        await window.ShowDialog(this);
    }

    private void AddSourceButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm) vm.AddSource();
    }

    private void RemoveSourceButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm) vm.RemoveSelectedSource();
    }

    private void PlaceSourceButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm) vm.BeginPlaceSource();
    }

    private void SnapSourceButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm) vm.SnapSourceToSelectedSurface();
    }

    private void AddReceiverButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm) vm.AddReceiver();
    }

    private void RemoveReceiverButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm) vm.RemoveSelectedReceiver();
    }

    private void PlaceReceiverButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm) vm.BeginPlaceReceiver();
    }

    private void SnapReceiverButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm) vm.SnapReceiverToSelectedSurface();
    }

    private void PointDataGrid_OnCellEditEnded(object? sender, DataGridCellEditEndedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm) vm.OnPointEdited();
    }

    private void PointDirection_OnCommit(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm) vm.OnPointEdited();
    }

    private async void RunLightTracingButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm) await vm.RunLightTracing();
    }

    private void ClearLightTracingButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm) vm.ClearLightTracing();
    }


    private void ApplyRotationButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm) vm.ApplyRotation();
    }

    private void ApplyTranslationButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm) vm.ApplyTranslation();
    }

    private void UndoRotationButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm) vm.UndoLastTransform();
    }

    private void ResetTransformsButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm) vm.ResetAllTransforms();
    }

    private void DeleteSelectedSurfaceButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm) vm.DeleteSelectedSurfaces();
    }

    private async void RunSimulationButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm)
            await vm.RunSimulationAsync();
    }

    private async void RunConvergenceButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm)
            await vm.RunConvergenceAnalysisAsync();
    }

    private void CancelSimulationButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm)
            vm.CancelSimulation();
    }
    private async void AuralizeCustomWavButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not ProjectWindowViewModel vm) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Auralizasyon için kaynak ses dosyası seçin",
            AllowMultiple = false,
            FileTypeFilter = new[] { new Avalonia.Platform.Storage.FilePickerFileType("WAV Audio") { Patterns = new[] { "*.wav" } } }
        });

        if (files.Count >= 1)
        {
            vm.CustomAudioPath = files[0].Path.LocalPath;
        }
    }

    private async void BackToMain_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Ebeveyn pencere yoksa (bu ProjectWindow doğrudan açıldıysa) proje ana ekrana
        // aktarılmaz — gerçekten kaybolur, bu yüzden onay burada özellikle önemli.
        var proceed = await ConfirmDiscardAsync();
        if (proceed == UnsavedProceedResult.Cancelled) return;

        if (proceed != UnsavedProceedResult.Discarded &&
            DataContext is ProjectWindowViewModel vm && _parentVm is not null)
        {
            _parentVm.UpdateProject(
                vm.BuildCurrentProject(),
                vm.IsDirty,
                vm.SavedFilePath,
                vm.RecoveredFromBackup);
        }

        var mainWin = new MainWindow { DataContext = _parentVm ?? new MainWindowViewModel() };
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = mainWin;
        }
        mainWin.Show();
        _forceClose = true;
        this.Close();
    }

    private static string BuildOpenErrorMessage(Exception ex) => ex switch
    {
        UnauthorizedAccessException => "Proje açılamadı: dosyayı okumak için yetkiniz yok.",
        FileNotFoundException => "Proje açılamadı: dosya bulunamadı veya taşınmış.",
        IOException => "Proje açılamadı: dosya okunamıyor ya da başka bir uygulama tarafından kullanılıyor.",
        System.Text.Json.JsonException => "Proje açılamadı: dosya içeriği bozuk veya desteklenmeyen biçimde.",
        InvalidDataException => $"Proje açılamadı: {ex.Message}",
        NotSupportedException => $"Proje açılamadı: {ex.Message}",
        _ => "Proje açılamadı. Dosyayı ve proje sürümünü kontrol edin."
    };
}
