using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Kst.Acoustics.Core.AbsorptiveSurface;
using Kst.Acoustics.Core.ImpactSurface;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Data.Converters;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace Kst.Acoustics.App.ViewModels
{
    public partial class ImpactSurfaceViewModel : ViewModelBase
    {
        private bool _isInitializing;
        
        [ObservableProperty]
        private ObservableCollection<ImpactLayerRowViewModel> _layers = new();

        [ObservableProperty]
        private ImpactCalculationResult _results = new();

        [ObservableProperty]
        private ImpactSurfaceTemplate? _selectedTemplate;

        [ObservableProperty]
        private ImpactLayerRowViewModel? _selectedLayer;

        [ObservableProperty]
        private double _dropHeight = 1.5;

        [ObservableProperty]
        private double _headMass = 5.0; // kg

        [ObservableProperty]
        private double _contactArea = 0.01; // m²

        [ObservableProperty]
        private string _newMatName = "Yeni Malzeme";
        [ObservableProperty]
        private double _newMatE = 5.0;
        [ObservableProperty]
        private double _newMatDensity = 600;
        [ObservableProperty]
        private double _newMatAbs = 60;

        [ObservableProperty]
        private double _newMatAlphaW = 0.25;

        [ObservableProperty]
        private double _viewRotation = 0.5; // SkewX
        [ObservableProperty]
        private double _viewZoom = 1.0;

        public ObservableCollection<ImpactMaterial> SharedMaterials { get; }
        public ObservableCollection<ImpactSurfaceTemplate> Templates { get; }
        public ObservableCollection<AbsorptiveMaterial> AbsorptiveMaterials { get; }
        public ObservableCollection<AbsorptiveLayerRowViewModel> AbsorptiveLayers { get; } = new();
        public AbsorptiveLayerType[] AbsorptiveLayerTypes { get; } = Enum.GetValues<AbsorptiveLayerType>();

        [ObservableProperty]
        private AbsorptiveLayerRowViewModel? _selectedAbsorptiveLayer;

        [ObservableProperty]
        private AbsorptiveSurfaceResult _absorptiveResults = new();

        [ObservableProperty]
        private string _absorptiveSurfaceName = "Yeni Ses Emici Yüzey";

        public ImpactSurfaceViewModel()
        {
            _isInitializing = true;
            SharedMaterials = new ObservableCollection<ImpactMaterial>(ImpactMaterialDatabase.GetAll());
            Templates = new ObservableCollection<ImpactSurfaceTemplate>(ImpactSurfaceTemplate.GetDefaultTemplates());
            AbsorptiveMaterials = new ObservableCollection<AbsorptiveMaterial>(AbsorptiveMaterialDatabase.GetAll());
            
            // Başlangıçta bir şablon yükle
            SelectedTemplate = Templates.FirstOrDefault();
            _isInitializing = false;
            Calculate();
            InitializeAbsorptiveSurface();
        }

        partial void OnSelectedTemplateChanged(ImpactSurfaceTemplate? value)
        {
            if (value != null) ApplyTemplate(value);
        }

        partial void OnDropHeightChanged(double value) => Calculate();
        partial void OnHeadMassChanged(double value) => Calculate();
        partial void OnContactAreaChanged(double value) => Calculate();

        public IEnumerable<ImpactLayer> LayersAsModel => Layers.Select(l => l.ToModel());
        public IEnumerable<AbsorptiveLayer> AbsorptiveLayersAsModel => AbsorptiveLayers.Select(l => l.ToModel());

        [RelayCommand]
        private void AddLayer()
        {
            var material = ImpactMaterialDatabase.GetAll().First(m => m.Category == "Impact");
            var layer = new ImpactLayer
            {
                Index = Layers.Count,
                MaterialCode = material.Code,
                ThicknessMm = 20,
                FunctionTag = material.Category,
                Material = material
            };
            var row = new ImpactLayerRowViewModel(layer, SharedMaterials);
            row.PropertyChanged += (_, _) => Calculate();
            Layers.Add(row);
            Calculate();
        }

        [RelayCommand]
        private void RemoveLayer(ImpactLayerRowViewModel row)
        {
            Layers.Remove(row);
            ReindexLayers();
            Calculate();
        }

        [RelayCommand]
        private void MoveUp(ImpactLayerRowViewModel row)
        {
            int idx = Layers.IndexOf(row);
            if (idx > 0)
            {
                Layers.Move(idx, idx - 1);
                ReindexLayers();
                Calculate();
            }
        }

        [RelayCommand]
        private void MoveDown(ImpactLayerRowViewModel row)
        {
            int idx = Layers.IndexOf(row);
            if (idx < Layers.Count - 1)
            {
                Layers.Move(idx, idx + 1);
                ReindexLayers();
                Calculate();
            }
        }

        private void ReindexLayers()
        {
            for (int i = 0; i < Layers.Count; i++)
                Layers[i].Index = i + 1;
        }

        [RelayCommand]
        private void ApplyTemplate(ImpactSurfaceTemplate template)
        {
            if (template == null) return;

            Layers.Clear();
            foreach (var layer in template.Layers)
            {
                var row = new ImpactLayerRowViewModel(layer, SharedMaterials);
                row.PropertyChanged += (_, _) => Calculate();
                Layers.Add(row);
            }
            Calculate();
        }

        [RelayCommand]
        private void Calculate()
        {
            if (_isInitializing) return;
            try
            {
                var stack = new ImpactLayerStack();
                foreach (var layer in Layers)
                    stack.Layers.Add(layer.ToModel());

                Results = ImpactCalculationService.Calculate(stack, DropHeight, HeadMass, ContactArea);
                StatusMessage = Results.IsValid ? Results.StatusMessage : $"Hesaplama uyarısı: {Results.StatusMessage}";
                OnPropertyChanged(nameof(LayersAsModel));
            }
            catch (Exception ex)
            {
                Results = new ImpactCalculationResult
                {
                    IsValid = false,
                    StatusMessage = "HATA: Hesaplama yapılamadı.",
                    Errors = { ex.Message }
                };
                StatusMessage = $"Hesaplama hatası: {ex.Message}";
            }
        }

        [RelayCommand]
        private void AddCustomMaterial()
        {
            if (string.IsNullOrWhiteSpace(NewMatName) ||
                !double.IsFinite(NewMatE) || NewMatE <= 0 ||
                !double.IsFinite(NewMatDensity) || NewMatDensity <= 0)
            {
                StatusMessage = "Malzeme eklenemedi: ad, E-modül ve yoğunluk geçerli olmalıdır.";
                return;
            }

            var mat = new ImpactMaterial
            {
                Code = CreateCustomMaterialCode(NewMatName),
                Name = NewMatName.Trim(),
                ElasticModulus = NewMatE,
                Density = NewMatDensity,
                ImpactAbsorption = Math.Clamp(NewMatAbs, 0.0, 100.0),
                AlphaW = Math.Clamp(NewMatAlphaW, 0.0, 1.0),
                Category = "Impact",
                Source = "Kullanıcı Tanımlı"
            };
            
            var existing = SharedMaterials.FirstOrDefault(m => m.Code == mat.Code);
            if (existing != null)
                SharedMaterials.Remove(existing);

            SharedMaterials.Add(mat);
            StatusMessage = $"Yeni malzeme eklendi: {mat.Name}";
        }

        [RelayCommand]
        private void AddAbsorptiveLayer()
        {
            var material = AbsorptiveMaterials.First(m => m.LayerType == AbsorptiveLayerType.PorousAbsorber);
            var row = new AbsorptiveLayerRowViewModel(new AbsorptiveLayer
            {
                Index = AbsorptiveLayers.Count + 1,
                MaterialCode = material.Code,
                Material = material,
                LayerType = material.LayerType,
                ThicknessMm = 40
            }, AbsorptiveMaterials);

            row.PropertyChanged += (_, _) => CalculateAbsorptiveSurface();
            AbsorptiveLayers.Add(row);
            SelectedAbsorptiveLayer = row;
            CalculateAbsorptiveSurface();
        }

        [RelayCommand]
        private void AddAbsorptiveAirGap()
        {
            var material = AbsorptiveMaterials.First(m => m.Code == "AIR");
            var row = new AbsorptiveLayerRowViewModel(new AbsorptiveLayer
            {
                Index = AbsorptiveLayers.Count + 1,
                MaterialCode = material.Code,
                Material = material,
                LayerType = AbsorptiveLayerType.AirGap,
                ThicknessMm = 50
            }, AbsorptiveMaterials);

            row.PropertyChanged += (_, _) => CalculateAbsorptiveSurface();
            AbsorptiveLayers.Add(row);
            SelectedAbsorptiveLayer = row;
            CalculateAbsorptiveSurface();
        }

        [RelayCommand]
        private void RemoveAbsorptiveLayer(AbsorptiveLayerRowViewModel row)
        {
            AbsorptiveLayers.Remove(row);
            ReindexAbsorptiveLayers();
            CalculateAbsorptiveSurface();
        }

        [RelayCommand]
        private void MoveAbsorptiveUp(AbsorptiveLayerRowViewModel row)
        {
            var index = AbsorptiveLayers.IndexOf(row);
            if (index <= 0)
                return;
            AbsorptiveLayers.Move(index, index - 1);
            ReindexAbsorptiveLayers();
            CalculateAbsorptiveSurface();
        }

        [RelayCommand]
        private void MoveAbsorptiveDown(AbsorptiveLayerRowViewModel row)
        {
            var index = AbsorptiveLayers.IndexOf(row);
            if (index < 0 || index >= AbsorptiveLayers.Count - 1)
                return;
            AbsorptiveLayers.Move(index, index + 1);
            ReindexAbsorptiveLayers();
            CalculateAbsorptiveSurface();
        }

        [RelayCommand]
        private void CalculateAbsorptiveSurface()
        {
            AbsorptiveResults = AbsorptiveSurfaceCalculationService.Calculate(
                AbsorptiveLayers.Select(l => l.ToModel()),
                AbsorptiveMaterials);
            OnPropertyChanged(nameof(AbsorptiveLayersAsModel));
            StatusMessage = $"Ses emici yüzey: {AbsorptiveResults.PerformanceSummary} / αw={AbsorptiveResults.AlphaW:F2}";
        }

        [RelayCommand]
        private void AddCustomAbsorptiveMaterial()
        {
            if (string.IsNullOrWhiteSpace(NewMatName))
            {
                StatusMessage = "Ses emici malzeme eklenemedi: malzeme adı gerekli.";
                return;
            }

            var material = new AbsorptiveMaterial
            {
                Code = CreateAbsorptiveMaterialCode(NewMatName),
                Name = NewMatName.Trim(),
                LayerType = AbsorptiveLayerType.PorousAbsorber,
                Density = Math.Max(1, NewMatDensity),
                FlowResistivity = Math.Max(1000, NewMatE * 5000),
                FireClass = "-",
                EnvironmentUse = "İç mekan",
                Source = "Kullanıcı Tanımlı",
                Confidence = 60
            };

            foreach (var band in AbsorptiveMaterialDatabase.OctaveBands)
                material.AbsorptionBands[band] = Math.Clamp(NewMatAlphaW, 0, 1);

            var existing = AbsorptiveMaterials.FirstOrDefault(m => m.Code == material.Code);
            if (existing != null)
                AbsorptiveMaterials.Remove(existing);

            AbsorptiveMaterials.Add(material);
            StatusMessage = $"Ses emici malzeme eklendi: {material.Name}";
        }

        [RelayCommand]
        private async Task SaveAsync(Avalonia.Controls.Window parent)
        {
            try
            {
                var storage = parent.StorageProvider;
                var file = await storage.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
                {
                    Title = "Projeyi Kaydet",
                    SuggestedFileName = "DarbeYuzey_Tasarim.e4p05",
                    DefaultExtension = "e4p05",
                    FileTypeChoices = new[] { new Avalonia.Platform.Storage.FilePickerFileType("EYE4PRO M05 Darbe Yüzey") { Patterns = new[] { "*.e4p05" } } }
                });

                if (file == null) return;

                var project = new ImpactSurfaceProject
                {
                    Name = SelectedTemplate?.Name ?? "Özel Tasarım",
                    TemplateName = SelectedTemplate?.Name,
                    DropHeight = DropHeight,
                    HeadMass = HeadMass,
                    ContactArea = ContactArea,
                    Layers = Layers.Select(l => l.ToModel()).ToList(),
                    CustomMaterials = SharedMaterials
                        .Where(m => string.Equals(m.Source, "Kullanıcı Tanımlı", StringComparison.OrdinalIgnoreCase) ||
                                    m.Code.StartsWith("CUSTOM_", StringComparison.OrdinalIgnoreCase))
                        .ToList(),
                    LastResult = Results
                };
                
                string json = System.Text.Json.JsonSerializer.Serialize(project, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                using var stream = await file.OpenWriteAsync();
                using var writer = new System.IO.StreamWriter(stream);
                await writer.WriteAsync(json);
                
                StatusMessage = $"Proje kaydedildi: {file.Name}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Kaydetme hatası: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task LoadAsync(Avalonia.Controls.Window parent)
        {
            try
            {
                var files = await parent.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Darbe Yüzey Projesi Aç",
                    AllowMultiple = false,
                    FileTypeFilter = new[] { new FilePickerFileType("EYE4PRO M05 Darbe Yüzey") { Patterns = new[] { "*.e4p05", "*.json" } } }
                });

                if (files.Count == 0)
                    return;

                using var stream = await files[0].OpenReadAsync();
                var project = await System.Text.Json.JsonSerializer.DeserializeAsync<ImpactSurfaceProject>(stream);
                if (project == null)
                {
                    StatusMessage = "Proje açılamadı: dosya içeriği geçersiz.";
                    return;
                }

                foreach (var material in project.CustomMaterials)
                {
                    var existing = SharedMaterials.FirstOrDefault(m => m.Code == material.Code);
                    if (existing != null)
                        SharedMaterials.Remove(existing);
                    SharedMaterials.Add(material);
                }

                _isInitializing = true;
                DropHeight = project.DropHeight;
                HeadMass = project.HeadMass > 0 ? project.HeadMass : 5.0;
                ContactArea = project.ContactArea > 0 ? project.ContactArea : 0.01;
                SelectedTemplate = Templates.FirstOrDefault(t => t.Name == project.TemplateName);
                Layers.Clear();
                foreach (var layer in project.Layers)
                {
                    var row = new ImpactLayerRowViewModel(layer, SharedMaterials);
                    row.PropertyChanged += (_, _) => Calculate();
                    Layers.Add(row);
                }
                ReindexLayers();
                _isInitializing = false;

                Calculate();
                StatusMessage = $"Proje açıldı: {files[0].Name}";
            }
            catch (Exception ex)
            {
                _isInitializing = false;
                StatusMessage = $"Açma hatası: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task ExportReportAsync(Avalonia.Controls.Window parent)
        {
            try
            {
                var storage = parent.StorageProvider;
                var file = await storage.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
                {
                    Title = "Raporu Kaydet",
                    SuggestedFileName = "DarbeYuzey_Rapor.md",
                    FileTypeChoices = new[] { new Avalonia.Platform.Storage.FilePickerFileType("Markdown") { Patterns = new[] { "*.md" } } }
                });

                if (file == null) return;

                var project = new ImpactSurfaceProject
                {
                    Name = "Darbe Emici Yüzey",
                    DropHeight = DropHeight,
                    HeadMass = HeadMass,
                    ContactArea = ContactArea,
                    TemplateName = SelectedTemplate?.Name,
                    Layers = LayersAsModel.ToList()
                };

                var stack = new ImpactLayerStack();
                foreach (var l in project.Layers) stack.Layers.Add(l);

                var compliance = ImpactCalculationService.EvaluateCompliance(stack, HeadMass, ContactArea);
                var markdown = ImpactReportBuilder.Build(project, Results, compliance);

                using var stream = await file.OpenWriteAsync();
                using var writer = new System.IO.StreamWriter(stream);
                await writer.WriteAsync(markdown);

                StatusMessage = $"Rapor oluşturuldu (CFH={compliance.CertifiedCriticalFallHeightM:F2} m): {file.Name}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Rapor hatası: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task ExportAbsorptiveReportAsync(Avalonia.Controls.Window parent)
        {
            try
            {
                var storage = parent.StorageProvider;
                var file = await storage.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
                {
                    Title = "Ses Emici Yüzey Raporu Kaydet",
                    SuggestedFileName = "SesEmiciYuzey_Rapor.md",
                    FileTypeChoices = new[] { new Avalonia.Platform.Storage.FilePickerFileType("Markdown") { Patterns = new[] { "*.md" } } }
                });
                if (file == null) return;

                var markdown = AbsorptiveReportBuilder.Build(
                    AbsorptiveSurfaceName,
                    AbsorptiveLayersAsModel,
                    AbsorptiveResults);

                using var stream = await file.OpenWriteAsync();
                using var writer = new System.IO.StreamWriter(stream);
                await writer.WriteAsync(markdown);

                StatusMessage = $"Ses emici raporu oluşturuldu (αw={AbsorptiveResults.AlphaW:F2}): {file.Name}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Rapor hatası: {ex.Message}";
            }
        }

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        private void InitializeAbsorptiveSurface()
        {
            AbsorptiveLayers.Clear();
            AddAbsorptiveRow("FAB-AC", 2);
            AddAbsorptiveRow("MW-48", 50);
            AddAbsorptiveRow("AIR", 50);
            AddAbsorptiveRow("CONC-BACK", 150);
            ReindexAbsorptiveLayers();
            SelectedAbsorptiveLayer = AbsorptiveLayers.FirstOrDefault();
            CalculateAbsorptiveSurface();
        }

        private void AddAbsorptiveRow(string materialCode, double thicknessMm)
        {
            var material = AbsorptiveMaterials.First(m => m.Code == materialCode);
            var row = new AbsorptiveLayerRowViewModel(new AbsorptiveLayer
            {
                Index = AbsorptiveLayers.Count + 1,
                MaterialCode = material.Code,
                Material = material,
                LayerType = material.LayerType,
                ThicknessMm = thicknessMm
            }, AbsorptiveMaterials);
            row.PropertyChanged += (_, _) => CalculateAbsorptiveSurface();
            AbsorptiveLayers.Add(row);
        }

        private void ReindexAbsorptiveLayers()
        {
            for (int i = 0; i < AbsorptiveLayers.Count; i++)
                AbsorptiveLayers[i].Index = i + 1;
        }

        private static string CreateCustomMaterialCode(string name)
        {
            var slug = new string(name.Trim().ToUpperInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
                .ToArray()).Trim('_');
            while (slug.Contains("__", StringComparison.Ordinal))
                slug = slug.Replace("__", "_", StringComparison.Ordinal);

            return $"CUSTOM_{(string.IsNullOrWhiteSpace(slug) ? Guid.NewGuid().ToString("N")[..6].ToUpperInvariant() : slug)}";
        }

        private static string CreateAbsorptiveMaterialCode(string name)
        {
            var slug = new string(name.Trim().ToUpperInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
                .ToArray()).Trim('_');
            while (slug.Contains("__", StringComparison.Ordinal))
                slug = slug.Replace("__", "_", StringComparison.Ordinal);

            return $"ABS_{(string.IsNullOrWhiteSpace(slug) ? Guid.NewGuid().ToString("N")[..6].ToUpperInvariant() : slug)}";
        }
    }

    /// <summary>
    /// Canvas için ViewModel katmanlarını Model katmanlarına dönüştüren basit çevirici.
    /// </summary>
    public class LayersToModelConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is IEnumerable<ImpactLayerRowViewModel> rows)
            {
                return rows.Select(r => r.ToModel()).ToList();
            }
            if (value is ImpactLayerRowViewModel row)
            {
                return row.ToModel();
            }
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            return Avalonia.AvaloniaProperty.UnsetValue;
        }
    }

    public class PassFailColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool passed)
            {
                return passed ? Avalonia.Media.Brushes.DarkGreen : Avalonia.Media.Brushes.DarkRed;
            }
            return Avalonia.Media.Brushes.Gray;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            return Avalonia.AvaloniaProperty.UnsetValue;
        }
    }
}
