using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kst.Acoustics.Core.SoundInsulation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Threading.Tasks;
using System.IO;

namespace Kst.Acoustics.App.ViewModels;

public partial class SoundInsulationViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _name = "Yeni Duvar Sistemi";

    private readonly string _designId = Guid.NewGuid().ToString();

    [ObservableProperty]
    private WallAssembly? _currentAssembly;

    [ObservableProperty]
    private SoundInsulationResult? _calculationResult;

    [ObservableProperty]
    private bool _showOnlyFavorites = false;

    [ObservableProperty]
    private double _studSpacingMm;

    [ObservableProperty]
    private string _connectionType = "None";

    [ObservableProperty]
    private CompositeResult? _compositeResult;

    public bool HasCompositeResult => CompositeResult is not null;

    [ObservableProperty]
    private CalculationMethod _selectedMethod = CalculationMethod.Empirical;

    [ObservableProperty]
    private SoundInsulationResult? _tmmOverlayResult;

    [ObservableProperty]
    private bool _showTmmOverlay;

    public IReadOnlyList<CalculationMethod> MethodOptions { get; } =
    [
        CalculationMethod.Empirical,
        CalculationMethod.TMM,
        CalculationMethod.Hybrid,
    ];

    partial void OnSelectedMethodChanged(CalculationMethod value)
    {
        CalculateImmediate();
    }

    partial void OnShowTmmOverlayChanged(bool value)
    {
        CalculateImmediate();
    }

    public ObservableCollection<SoundInsulationLayerViewModel> Layers { get; } = new();
    public ObservableCollection<SoundInsulationInfoRow> WarningRows { get; } = new();
    public ObservableCollection<SoundInsulationInfoRow> CriticalFrequencyRows { get; } = new();
    public ObservableCollection<SoundInsulationComparisonRow> ComparisonRows { get; } = new();
    public ObservableCollection<CompositeElementViewModel> CompositeElements { get; } = new();

    public IReadOnlyList<string> ConnectionTypeOptions { get; } =
    [
        "None",
        "Direct",
        "ResilientChannel",
        "Clips"
    ];
    
    private List<SoundInsulationMaterial> _allMaterials = new();
    public ObservableCollection<SoundInsulationMaterial> FilteredMaterials { get; } = new();

    public ICommand AddLayerCommand { get; }
    public ICommand RemoveLayerCommand { get; }
    public ICommand CalculateCommand { get; }
    public ICommand ToggleFavoriteCommand { get; }
    public ICommand OpenLibraryCommand { get; }
    public ICommand ImportExcelCommand { get; }
    public ICommand ExportExcelCommand { get; }
    public ICommand ExportTextReportCommand { get; }
    public ICommand MoveLayerUpCommand { get; }
    public ICommand MoveLayerDownCommand { get; }
    public ICommand AddComparisonCommand { get; }
    public ICommand AddCompositeElementCommand { get; }
    public ICommand RemoveCompositeElementCommand { get; }
    public ICommand CalculateCompositeCommand { get; }

    private readonly Avalonia.Threading.DispatcherTimer _debounceTimer;

    public SoundInsulationViewModel()
    {
        LoadLibrary();

        AddLayerCommand = new RelayCommand(AddLayer);
        RemoveLayerCommand = new RelayCommand<SoundInsulationLayerViewModel>(RemoveLayer);
        CalculateCommand = new RelayCommand(CalculateImmediate);
        ToggleFavoriteCommand = new RelayCommand<SoundInsulationMaterial>(ToggleFavorite);
        ImportExcelCommand = new AsyncRelayCommand(ImportFromExcelAsync);
        ExportExcelCommand = new AsyncRelayCommand(ExportExcelTemplateAsync);
        ExportTextReportCommand = new AsyncRelayCommand(ExportTextReportAsync);
        MoveLayerUpCommand = new RelayCommand<SoundInsulationLayerViewModel>(MoveLayerUp);
        MoveLayerDownCommand = new RelayCommand<SoundInsulationLayerViewModel>(MoveLayerDown);
        AddComparisonCommand = new RelayCommand(AddComparison);
        AddCompositeElementCommand = new RelayCommand(AddCompositeElement);
        RemoveCompositeElementCommand = new RelayCommand<CompositeElementViewModel>(RemoveCompositeElement);
        CalculateCompositeCommand = new RelayCommand(CalculateComposite);
        OpenLibraryCommand = new RelayCommand(() => { /* Bu aşamada ana ekran üzerinden yönetimi sağlıyoruz */ });

        _debounceTimer = new Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        _debounceTimer.Tick += (s, e) =>
        {
            _debounceTimer.Stop();
            CalculateImmediate();
        };

        // Start with default layers
        if (Layers.Count == 0) AddLayer();
    }

    private void LoadLibrary()
    {
        _allMaterials = SoundInsulationLibraryService.LoadLibrary();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredMaterials.Clear();
        var query = ShowOnlyFavorites 
            ? _allMaterials.Where(m => m.IsFavorite) 
            : _allMaterials;

        foreach (var m in query.OrderBy(x => x.Category).ThenBy(x => x.Name))
        {
            FilteredMaterials.Add(m);
        }
    }

    partial void OnShowOnlyFavoritesChanged(bool value) => ApplyFilter();

    partial void OnStudSpacingMmChanged(double value) => RequestCalculation();

    partial void OnConnectionTypeChanged(string value) => RequestCalculation();

    partial void OnCompositeResultChanged(CompositeResult? value) => OnPropertyChanged(nameof(HasCompositeResult));

    private void ToggleFavorite(SoundInsulationMaterial? material)
    {
        if (material != null)
        {
            material.IsFavorite = !material.IsFavorite;
            SaveLibrary();
            ApplyFilter();
        }
    }

    public void SaveLibrary() 
    {
        // Only save user-defined materials (not built-in)
        var userMaterials = _allMaterials.Where(m => !m.IsBuiltIn).ToList();
        SoundInsulationLibraryService.SaveLibrary(userMaterials);
    }

    private void AddLayer()
    {
        var defaultMaterial = FilteredMaterials.FirstOrDefault() ?? _allMaterials.FirstOrDefault();
        if (defaultMaterial != null)
        {
            var layerVm = new SoundInsulationLayerViewModel(defaultMaterial, 0.0125);
            layerVm.PropertyChanged += (s, e) => RequestCalculation();
            Layers.Add(layerVm);
            CalculateImmediate();
        }
    }

    private void RemoveLayer(SoundInsulationLayerViewModel? layer)
    {
        if (layer != null && Layers.Count > 1)
        {
            Layers.Remove(layer);
            CalculateImmediate();
        }
    }

    private void RequestCalculation()
    {
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private void CalculateImmediate()
    {
        var assembly = new WallAssembly(
            _designId,
            Name,
            Layers.Where(l => l.Material is not null).Select(l => l.ToModel()).ToList(),
            StudSpacing: Math.Max(0, StudSpacingMm) / 1000.0,
            ConnectionType: ConnectionType,
            Method: SelectedMethod
        );

        CurrentAssembly = assembly;
        CalculationResult = SoundInsulationCalculator.Calculate(assembly);

        // Karşılaştırma için TMM overlay'i, ana mod TMM değilse ve toggle açıksa hesaplanır.
        if (ShowTmmOverlay && SelectedMethod != CalculationMethod.TMM)
        {
            TmmOverlayResult = SoundInsulationCalculator.Calculate(
                assembly with { Method = CalculationMethod.TMM });
        }
        else
        {
            TmmOverlayResult = null;
        }

        RefreshResultRows();
    }

    private void RefreshResultRows()
    {
        WarningRows.Clear();
        CriticalFrequencyRows.Clear();

        if (CalculationResult is null) return;

        foreach (var warning in CalculationResult.Warnings)
            WarningRows.Add(new SoundInsulationInfoRow("Uyarı", warning));

        if (CalculationResult.RwRating?.MissingFrequencies is { Count: > 0 } missing)
            WarningRows.Add(new SoundInsulationInfoRow("Eksik Bant", $"{string.Join(", ", missing)} Hz bantları eksik olduğu için tek sayı değerlendirme sınırlı."));

        foreach (var cf in CalculationResult.CriticalFrequencies?.OrderBy(x => x.Value) ?? Enumerable.Empty<KeyValuePair<string, double>>())
            CriticalFrequencyRows.Add(new SoundInsulationInfoRow(cf.Key, $"{cf.Value:F0} Hz"));
    }

    private void MoveLayerUp(SoundInsulationLayerViewModel? layer)
    {
        if (layer is null) return;
        var index = Layers.IndexOf(layer);
        if (index <= 0) return;
        Layers.Move(index, index - 1);
        CalculateImmediate();
    }

    private void MoveLayerDown(SoundInsulationLayerViewModel? layer)
    {
        if (layer is null) return;
        var index = Layers.IndexOf(layer);
        if (index < 0 || index >= Layers.Count - 1) return;
        Layers.Move(index, index + 1);
        CalculateImmediate();
    }

    private void AddComparison()
    {
        if (CalculationResult?.RwRating is null || CurrentAssembly is null) return;
        ComparisonRows.Add(new SoundInsulationComparisonRow(
            Name,
            CalculationResult.RwRating.Rw,
            CalculationResult.RwRating.C,
            CalculationResult.RwRating.Ctr,
            CurrentAssembly.TotalThickness,
            CurrentAssembly.TotalSurfaceMass));
    }

    private void AddCompositeElement()
    {
        if (CurrentAssembly is null || CalculationResult?.RwRating is null) return;
        var element = new CompositeElementViewModel(CurrentAssembly, 10.0, CalculationResult.RwRating.Rw);
        element.PropertyChanged += (_, _) => CalculateComposite();
        CompositeElements.Add(element);
        CalculateComposite();
    }

    private void RemoveCompositeElement(CompositeElementViewModel? element)
    {
        if (element is null) return;
        CompositeElements.Remove(element);
        CalculateComposite();
    }

    private void CalculateComposite()
    {
        if (CompositeElements.Count == 0)
        {
            CompositeResult = null;
            return;
        }

        CompositeResult = SoundInsulationCalculator.CalculateComposite(
            CompositeElements.Select(e => new CompositeElement(e.Assembly, Math.Max(0.01, e.AreaM2))).ToList());
    }

    private async Task ImportFromExcelAsync()
    {
        if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            var files = await desktop.MainWindow!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Excelden Malzeme Aktar",
                FileTypeFilter = new[] { new FilePickerFileType("Excel Dosyası") { Patterns = new[] { "*.xlsx" } } }
            });

            if (files.Count > 0)
            {
                var imported = SoundInsulationLibraryService.ImportFromExcel(files[0].Path.LocalPath);
                foreach (var m in imported)
                {
                    if (!_allMaterials.Any(x => x.Id == m.Id))
                        _allMaterials.Add(m);
                }
                SaveLibrary();
                ApplyFilter();
            }
        }
    }

    private async Task ExportExcelTemplateAsync()
    {
        if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            var file = await desktop.MainWindow!.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Excel Şablonunu Kaydet",
                DefaultExtension = "xlsx",
                SuggestedFileName = "Malzeme_Sablonu.xlsx"
            });

            if (file != null)
            {
                SoundInsulationLibraryService.GenerateExcelTemplate(file.Path.LocalPath);
            }
        }
    }

    private async Task ExportTextReportAsync()
    {
        if (CalculationResult == null) return;

        if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            var file = await desktop.MainWindow!.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Raporu Kaydet",
                DefaultExtension = "txt",
                SuggestedFileName = $"Rapor_{Name}.txt"
            });

            if (file != null)
            {
                var report = SoundInsulationReportService.GenerateTextReport(CalculationResult);
                await File.WriteAllTextAsync(file.Path.LocalPath, report);
            }
        }
    }
}

public partial class SoundInsulationLayerViewModel : ObservableObject
{
    [ObservableProperty]
    private SoundInsulationMaterial _material;

    [ObservableProperty]
    private double _thicknessMm;

    [ObservableProperty]
    private bool _isAirGap;

    [ObservableProperty]
    private bool _hasPorousFill;

    public SoundInsulationLayerViewModel(SoundInsulationMaterial material, double thicknessMeters)
    {
        _material = material;
        _thicknessMm = thicknessMeters * 1000;
        _isAirGap = material.Category is "Boşluk" or "Dolgu";
        _hasPorousFill = material.Category == "Dolgu";
    }

    public SoundInsulationLayer ToModel()
    {
        return new SoundInsulationLayer(Material, ThicknessMm / 1000.0, IsAirGap, HasPorousFill);
    }

    partial void OnMaterialChanged(SoundInsulationMaterial value)
    {
        if (value is null) return;
        IsAirGap = value.Category is "Boşluk" or "Dolgu";
        HasPorousFill = value.Category == "Dolgu";
    }
}

public sealed record SoundInsulationInfoRow(string Label, string Value);

public sealed record SoundInsulationComparisonRow(
    string Name,
    int Rw,
    int C,
    int Ctr,
    double Thickness,
    double SurfaceMass)
{
    public string RatingText => $"{Rw} ({C}; {Ctr}) dB";
    public string ThicknessText => $"{Thickness * 1000:F1} mm";
    public string MassText => $"{SurfaceMass:F1} kg/m²";
}

public partial class CompositeElementViewModel : ObservableObject
{
    public WallAssembly Assembly { get; }

    public string Name => Assembly.Name;

    public int Rw { get; }

    [ObservableProperty]
    private double _areaM2;

    public CompositeElementViewModel(WallAssembly assembly, double areaM2, int rw)
    {
        Assembly = assembly;
        _areaM2 = areaM2;
        Rw = rw;
    }
}
