using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kst.Acoustics.Core.PanelDesign;
using Kst.Acoustics.Core.Projects;
using System.Threading.Tasks;

namespace Kst.Acoustics.App.ViewModels;

public partial class PanelDesignViewModel : ViewModelBase
{
    private const string DefaultDxfQualitySummary = "DXF kalite analizi: özel desen yüklenmedi.";
    private const string DefaultCalculationAssumptionSummary = "Varsayım: TMM, rijit arkalık ve lokal reaksiyon yaklaşımı.";

    [ObservableProperty]
    private ObservableCollection<LayerViewModel> _layers = new();

    [ObservableProperty]
    private double _nrc;

    [ObservableProperty]
    private double _alphaW;

    [ObservableProperty]
    private string _alphaWLabel = "0.00";

    [ObservableProperty]
    private string _absorptionClass = "-";

    [ObservableProperty]
    private double _helmholtzResonance;

    [ObservableProperty]
    private List<GraphPoint> _plotPoints = new(); // Normal incidence

    [ObservableProperty]
    private List<GraphPoint> _plotPointsRandom = new();

    [ObservableProperty]
    private bool _showRandomIncidence;

    public List<GraphPoint> DisplayedPlotPoints => ShowRandomIncidence ? PlotPointsRandom : PlotPoints;

    public string PlotModeLabel => ShowRandomIncidence ? "Grafik: Rastgele Geliş" : "Grafik: Normal Geliş";

    public string CalculationLabel => "Yöntem: TMM (Lokal Reaksiyon Tahmini)";

    private readonly Avalonia.Threading.DispatcherTimer _debounceTimer;

    [ObservableProperty]
    private string _warningMessage = string.Empty;

    [ObservableProperty]
    private string _dxfQualitySummary = DefaultDxfQualitySummary;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDxfQualityDetails))]
    private string _dxfQualityDetails = string.Empty;

    public bool HasDxfQualityDetails => !string.IsNullOrWhiteSpace(DxfQualityDetails);

    [ObservableProperty]
    private string _calculationAssumptionSummary = DefaultCalculationAssumptionSummary;

    private DxfQualityMetrics? _lastDxfQuality;

    partial void OnShowRandomIncidenceChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayedPlotPoints));
        OnPropertyChanged(nameof(PlotModeLabel));
    }

    partial void OnPlotPointsChanged(List<GraphPoint> value) => OnPropertyChanged(nameof(DisplayedPlotPoints));

    partial void OnPlotPointsRandomChanged(List<GraphPoint> value) => OnPropertyChanged(nameof(DisplayedPlotPoints));

    public PanelDesignViewModel()
    {
        _debounceTimer = new Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        _debounceTimer.Tick += (s, e) =>
        {
            _debounceTimer.Stop();
            UpdateCalculationsImmediate();
        };

        ApplyStudioBassTrapPreset();
    }

    [RelayCommand]
    public void ApplyStudioBassTrapPreset()
    {
        Layers.Clear();
        AddLayer("Delikli Panel", CreatePerforatedLayer(0.012, 0.006, 0.05, HolePattern.Square));
        AddLayer("Kaya Yünü (100kg/m3)", new PorousLayer { Thickness = 0.050, FlowResistivity = 35000 });
        AddLayer("Hava Boşluğu", new AirLayer { Thickness = 0.200 });
        UpdateCalculationsImmediate();
    }

    [RelayCommand]
    public void ApplyVocalBoothPreset()
    {
        Layers.Clear();
        AddLayer("Delikli Panel", CreatePerforatedLayer(0.004, 0.003, 0.20, HolePattern.Staggered));
        AddLayer("Kumaş", new FabricLayer { SpecificFlowResistance = 500, SurfaceDensity = 0.06 });
        AddLayer("Akustik Sünger", new PorousLayer { Thickness = 0.040, FlowResistivity = 8000 });
        AddLayer("Hava Boşluğu", new AirLayer { Thickness = 0.020 });
        UpdateCalculationsImmediate();
    }

    [RelayCommand]
    public void ApplyCeilingTilePreset()
    {
        Layers.Clear();
        AddLayer("Tavan Paneli (Metal)", CreatePerforatedLayer(0.001, 0.002, 0.18, HolePattern.Square));
        AddLayer("SoundTex S 100", new FabricLayer { SpecificFlowResistance = 500, SurfaceDensity = 0.06 });
        AddLayer("Cam Yünü", new PorousLayer { Thickness = 0.020, FlowResistivity = 15000 });
        UpdateCalculationsImmediate();
    }

    [RelayCommand]
    private void AddPorousLayer() => AddLayer("Yeni Gözenekli Katman", new PorousLayer { Thickness = 0.050, FlowResistivity = 5000 });

    [RelayCommand]
    private void AddAirLayer() => AddLayer("Yeni Hava Boşluğu", new AirLayer { Thickness = 0.050 });

    [RelayCommand]
    private void AddFabricLayer() => AddLayer("SoundTex Kumaş", new FabricLayer { Thickness = 0.0002, SpecificFlowResistance = 500, SurfaceDensity = 0.06 });

    [RelayCommand]
    private void AddPerforatedLayer() => AddLayer("Yeni Delikli Panel", CreatePerforatedLayer(0.012, 0.008, 0.15, HolePattern.Square));

    [RelayCommand]
    private void RemoveLayer(LayerViewModel layer)
    {
        if (layer != null)
        {
            layer.LayerChanged -= OnLayerChanged;
            Layers.Remove(layer);
            UpdateCalculations();
        }
    }

    [RelayCommand]
    private async Task ImportDxfAsync()
    {
        var topLevel = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop 
                       ? desktop.MainWindow : null;
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Desen Dosyası Seç (DXF)",
            FileTypeFilter = new[] { new Avalonia.Platform.Storage.FilePickerFileType("DXF Files") { Patterns = new[] { "*.dxf" } } }
        });

        if (files.Count > 0)
        {
            using var stream = await files[0].OpenReadAsync();
            using var reader = new System.IO.StreamReader(stream);
            string content = await reader.ReadToEndAsync();

            var parser = new DxfPanelParser();
            var result = parser.Parse(content);

            if (result.OpenAreaRatio > 0 || result.HoleCount > 0)
            {
                // Birim onay diyaloğu — kullanıcı yanlış birim seçimini engellemek için.
                if (topLevel is Avalonia.Controls.Window owner)
                {
                    string summary =
                        $"Algılanan: {result.HoleCount} delik, " +
                        $"toplam panel alanı {result.TotalAreaSquareMeters:F3} m², " +
                        $"%{result.OpenAreaRatio * 100:F2} boşluk oranı, " +
                        $"eşdeğer çap {result.EquivalentRadiusMeters * 2000:F2} mm, " +
                        $"hidrolik çap dağılımı {(result.Openings?.Count ?? 0)} açıklık " +
                        $"(seçili birime göre).";
                    var chosen = await Views.DxfUnitConfirmDialog.ShowAsync(
                        owner,
                        summary,
                        result.UnitScaleToMeters,
                        result.UnitDetected,
                        BuildDxfQualityItems(result));
                    if (chosen == null) return; // İptal
                    result = result.WithUnitScale(chosen.Value);
                }

                // Find first perforated layer or add one
                var perfLayer = Layers.FirstOrDefault(l => l.IsPerforated);
                if (perfLayer == null)
                {
                    AddLayer("Özel Desenli Panel", new PerforatedLayer());
                    perfLayer = Layers.Last();
                }

                perfLayer.HoleDiameter = result.EquivalentRadiusMeters * 2000.0; // radius(m) -> diameter(mm)
                perfLayer.OpenAreaRatio = result.OpenAreaRatio * 100.0;
                perfLayer.Name = "Özel Desenli Panel (" + result.HoleCount + " delik)";
                perfLayer.ApplyDxfImport(result.Entities, CreateCustomOpenings(result));
                _lastDxfQuality = result.Quality;
                DxfQualitySummary = BuildDxfQualitySummary(result);
                DxfQualityDetails = BuildDxfQualityDetails(result);
                CalculationAssumptionSummary = BuildCalculationAssumptionSummary(result.Quality);

                UpdateCalculations();
            }
        }
    }

    private static IReadOnlyList<PerforationOpening>? CreateCustomOpenings(DxfAnalysisResult result)
    {
        if (result.TotalArea <= 0 || result.Openings is not { Count: > 0 })
            return null;

        var openings = result.Openings
            .Where(o => o.Area > 0 && o.HydraulicDiameterMeters > 0)
            .Select(o => new PerforationOpening(
                o.Area / result.TotalArea,
                o.HydraulicDiameterMeters,
                o.Circularity,
                o.AspectRatio,
                o.ShapeType))
            .ToArray();

        return openings.Length > 0 ? openings : null;
    }

    [RelayCommand]
    private void SaveToLibrary()
    {
        var models = GetIncludedModels();
        if (!models.Any()) return;

        var alphaDict = TmmCalculator.CalculateRandomIncidenceOctaveBands(models);
        double[] alpha = {
            alphaDict.GetValueOrDefault(63),
            alphaDict.GetValueOrDefault(125),
            alphaDict.GetValueOrDefault(250),
            alphaDict.GetValueOrDefault(500),
            alphaDict.GetValueOrDefault(1000),
            alphaDict.GetValueOrDefault(2000),
            alphaDict.GetValueOrDefault(4000),
            alphaDict.GetValueOrDefault(8000)
        };

        // Estimate scattering for perforated panels
        // Default base scattering is 0.10. 
        // For perforated panels, we estimate higher scattering in mid-high frequencies.
        double[] scattering = Enumerable.Repeat(0.10, 8).ToArray();
        var perf = models.OfType<PerforatedLayer>().FirstOrDefault();
        if (perf != null)
        {
            // Perforated panels have higher scattering due to structural periodicity
            // Higher at 1k-4k range (0.2 - 0.4)
            scattering[4] = 0.20; // 1 kHz
            scattering[5] = 0.30; // 2 kHz
            scattering[6] = 0.40; // 4 kHz
            scattering[7] = 0.35; // 8 kHz
        }

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string id = $"custom_panel_{timestamp}";
        string name = Layers.First().Name + " Tasarımı";

        var material = new AcousticMaterial(id, name, alpha, scattering, "Özel Tasarım");
        
        if (MaterialDatabase.Instance.Add(material))
        {
            MaterialDatabase.Instance.SetFavorite(id, true);
        }
    }

    [RelayCommand]
    private async Task DownloadTemplateAsync()
    {
        var topLevel = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop 
                       ? desktop.MainWindow : null;
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "DXF Şablonunu Kaydet",
            DefaultExtension = "dxf",
            SuggestedFileName = "Panel_Sablonu_600x600.dxf",
            FileTypeChoices = new[] { new Avalonia.Platform.Storage.FilePickerFileType("DXF Files") { Patterns = new[] { "*.dxf" } } }
        });

        if (file != null)
        {
            var path = EnsureDxfExtension(file.Path.LocalPath);
            await System.IO.File.WriteAllTextAsync(path, PanelTemplateDxfBuilder.Build600x600MillimeterTemplate());
            WarningMessage = path.Equals(file.Path.LocalPath, StringComparison.OrdinalIgnoreCase)
                ? $"DXF şablonu kaydedildi: {System.IO.Path.GetFileName(path)}"
                : $"DWG üretilemediği için şablon DXF olarak kaydedildi: {System.IO.Path.GetFileName(path)}";
        }
    }

    [RelayCommand]
    private async Task ExportReportAsync()
    {
        var topLevel = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop 
                       ? desktop.MainWindow : null;
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Akustik Tasarım Raporu Kaydet",
            DefaultExtension = "txt",
            SuggestedFileName = $"Tasarim_Raporu_{DateTime.Now:yyyyMMdd}.txt",
            FileTypeChoices =
            [
                new Avalonia.Platform.Storage.FilePickerFileType("Metin Raporu") { Patterns = ["*.txt"] },
                new Avalonia.Platform.Storage.FilePickerFileType("HTML Raporu") { Patterns = ["*.html"] },
                new Avalonia.Platform.Storage.FilePickerFileType("Markdown Raporu") { Patterns = ["*.md"] }
            ]
        });

        if (file != null)
        {
            string path = file.Path.LocalPath;
            string extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
            string report = extension switch
            {
                ".html" => BuildHtmlReport(),
                ".md" => BuildMarkdownReport(),
                _ => BuildTextReport()
            };

            using var stream = await file.OpenWriteAsync();
            using var writer = new System.IO.StreamWriter(stream);
            await writer.WriteAsync(report);
        }
    }

    private void AddLayer(string name, AcousticLayer model)
    {
        if (Layers.Count >= 10)
        {
            WarningMessage = "Maksimum 10 katman desteklenir.";
            return;
        }

        var vm = new LayerViewModel(model, name);
        vm.LayerChanged += OnLayerChanged;
        Layers.Add(vm);
        UpdateCalculations();
    }

    private static PerforatedLayer CreatePerforatedLayer(double thickness, double holeDiameter, double openAreaRatio, HolePattern pattern)
    {
        return new PerforatedLayer
        {
            Thickness = thickness,
            HoleDiameter = holeDiameter,
            OpenAreaRatio = openAreaRatio,
            HoleSpacing = PerforatedLayer.CalculateSpacingForOpenAreaRatio(holeDiameter, openAreaRatio, pattern),
            HolePattern = pattern
        };
    }

    private void OnLayerChanged(object? sender, EventArgs e)
    {
        UpdateCalculations();
    }

    private void UpdateCalculations()
    {
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private void UpdateCalculationsImmediate()
    {
        var models = GetIncludedModels();
        if (!models.Any())
        {
            Nrc = 0;
            AlphaW = 0;
            AlphaWLabel = "0.00";
            AbsorptionClass = "-";
            PlotPoints = new List<GraphPoint>();
            PlotPointsRandom = new List<GraphPoint>();
            HelmholtzResonance = 0;
            WarningMessage = string.Empty;
            ResetDxfQualityState();
            return;
        }

        // Geometric Warnings
        var warnings = new List<string>();
        foreach (var firstPerf in models.OfType<PerforatedLayer>())
        {
            if (firstPerf.CustomOpenings is { Count: > 0 })
            {
                warnings.AddRange(BuildCustomOpeningWarnings(firstPerf));
                if (_lastDxfQuality != null)
                    warnings.AddRange(_lastDxfQuality.Warnings.Select(w => $"⚠️ {w}"));
                continue;
            }

            if (firstPerf.HoleSpacing < firstPerf.HoleDiameter)
                warnings.Add("⚠️ Çakışan Delikler: Aks aralığı delik çapından küçük olamaz.");
            if (firstPerf.OpenAreaRatio > 0.45)
                warnings.Add("⚠️ Yüksek Boşluk Oranı: %45 üzeri değerlerde model doğruluğu azalır.");
            if (firstPerf.HoleSpacing < 0.002)
                warnings.Add("⚠️ Dar Aks Aralığı: 2mm altı üretimde zorluk yaratabilir.");
        }

        // Porous geçerlilik aralığı (Delany-Bazley/Miki: ρf/σ ∈ [0.01, 1.0])
        // saf sorguyla kontrol edilir; hesap motoru katman üzerinde state bırakmaz.
        var porousLayers = models.OfType<PorousLayer>().ToList();

        Nrc = TmmCalculator.CalculateNRC(models);
        var alphaWResult = TmmCalculator.CalculateAlphaWResult(models);
        AlphaW = alphaWResult.Value;
        AlphaWLabel = alphaWResult.ShapeIndicators.Length > 0
            ? $"{alphaWResult.Value:0.00}({alphaWResult.ShapeIndicators})"
            : $"{alphaWResult.Value:0.00}";
        AbsorptionClass = TmmCalculator.CalculateAbsorptionClass(AlphaW);

        // Helmholtz estimate follows the same neck/end-correction assumptions as PerforatedLayer.
        var perf = models.OfType<PerforatedLayer>().FirstOrDefault();
        var air = models.OfType<AirLayer>().FirstOrDefault();
        HelmholtzResonance = perf != null && air != null
            ? perf.EstimateHelmholtzResonance(air.Thickness)
            : 0;

        // Generate plot points (63Hz to 8000Hz, adaptive logarithmic grid)
        var pointsNormal = new List<GraphPoint>();
        var pointsRandom = new List<GraphPoint>();
        double minF = 63;
        double maxF = 8000;
        int numSteps = 60;

        for (int i = 0; i <= numSteps; i++)
        {
            double t = (double)i / numSteps;
            double tAdaptive = (t + t * t) / 2.0; 
            double f = minF * Math.Pow(maxF / minF, tAdaptive);
            
            pointsNormal.Add(new GraphPoint(f, TmmCalculator.CalculateNormalAbsorption(models, f)));
            pointsRandom.Add(new GraphPoint(f, TmmCalculator.CalculateRandomIncidenceAbsorption(models, f)));
        }
        PlotPoints = pointsNormal;
        PlotPointsRandom = pointsRandom;

        // Sweep sonrası: gözenekli katmanlardan en az biri Delany-Bazley/Miki
        // ampirik aralığının dışına çıktıysa kullanıcıya bildir.
        if (porousLayers.Any(p =>
                p.IsFrequencyOutsideEmpiricalRange(minF) ||
                p.IsFrequencyOutsideEmpiricalRange(maxF)))
        {
            warnings.Add("⚠️ Gözenekli katman geçerlilik dışı: ρf/σ ∈ [0.01, 1.0] aralığı dışında frekanslar var. Çok düşük frekans + yüksek akış direnci kombinasyonunda α değerleri yaklaşıktır.");
        }
        if (!models.OfType<PerforatedLayer>().Any(p => p.CustomOpenings is { Count: > 0 }))
        {
            ResetDxfQualityState();
        }
        WarningMessage = string.Join("\n", warnings);
    }

    private List<AcousticLayer> GetIncludedModels()
    {
        return Layers
            .Where(l => l.IsIncludedInCalculation)
            .Select(l => l.Model)
            .ToList();
    }

    private string BuildTextReport()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("================================================================");
        sb.AppendLine("            EYE4PRO ACOUSTICS - PANEL TASARIM RAPORU            ");
        sb.AppendLine("================================================================");
        sb.AppendLine($"Tarih: {DateTime.Now:dd.MM.yyyy HH:mm}");
        sb.AppendLine();
        sb.AppendLine("KATMAN YAPISI (Önden Arkaya):");
        foreach (var l in Layers)
        {
            string includedText = l.IsIncludedInCalculation ? "" : " (hesap dışı)";
            sb.AppendLine($"- {l.Name}: {l.Thickness:F1} mm{includedText}");
            if (l.IsPorous) sb.AppendLine($"  (Gözenekli, Akış Direnci: {l.FlowResistivity} Rayl/m)");
            if (l.IsFabric) sb.AppendLine($"  (Akustik Kumaş, Yüzey Akış Direnci: {l.SpecificFlowResistance} Rayl, Yüzey Kütlesi: {l.SurfaceDensity:F3} kg/m²)");
            if (l.IsPerforated) sb.AppendLine($"  (Delikli, {l.HolePatternName}, {l.HoleSpacing:F1} mm aks, %{l.OpenAreaRatio:F1} Boşluk Oranı, {l.HoleDiameter:F1} mm Delik Çapı)");
        }
        sb.AppendLine();
        AppendTextReliabilitySection(sb);
        sb.AppendLine();
        sb.AppendLine("AKUSTİK PERFORMANS:");
        sb.AppendLine($"NRC:    {Nrc:F2}");
        sb.AppendLine($"alphaW: {AlphaWLabel}");
        sb.AppendLine($"Sınıf:   {AbsorptionClass}");
        sb.AppendLine();
        AppendTextFrequencySection(sb);
        sb.AppendLine("================================================================");
        return sb.ToString();
    }

    private string BuildMarkdownReport()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# EYE4PRO Acoustics - Panel Tasarım Raporu");
        sb.AppendLine();
        sb.AppendLine($"Tarih: {DateTime.Now:dd.MM.yyyy HH:mm}");
        sb.AppendLine();
        sb.AppendLine("## Akustik Performans");
        sb.AppendLine();
        sb.AppendLine("| NRC | alphaW | Sınıf |");
        sb.AppendLine("|---:|---:|:---:|");
        sb.AppendLine($"| {Nrc:F2} | {AlphaWLabel} | {AbsorptionClass} |");
        sb.AppendLine();
        sb.AppendLine("## Katman Yapısı");
        sb.AppendLine();
        sb.AppendLine("| Katman | Kalınlık | Durum | Detay |");
        sb.AppendLine("|---|---:|---|---|");
        foreach (var l in Layers)
        {
            sb.AppendLine($"| {EscapeMarkdown(l.Name)} | {l.Thickness:F1} mm | {(l.IsIncludedInCalculation ? "Hesapta" : "Hesap dışı")} | {EscapeMarkdown(DescribeLayer(l))} |");
        }
        sb.AppendLine();
        AppendMarkdownReliabilitySection(sb);
        sb.AppendLine();
        AppendMarkdownFrequencySection(sb);
        return sb.ToString();
    }

    private string BuildHtmlReport()
    {
        static string H(string value) => System.Net.WebUtility.HtmlEncode(value);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!doctype html><html lang=\"tr\"><head><meta charset=\"utf-8\"><title>Panel Tasarım Raporu</title>");
        sb.AppendLine("<style>body{font-family:Arial,sans-serif;margin:32px;color:#17212b}table{border-collapse:collapse;width:100%;margin:16px 0}th,td{border:1px solid #cbd5df;padding:8px;text-align:left}th{background:#eef4f8}.num{text-align:right}</style>");
        sb.AppendLine("</head><body>");
        sb.AppendLine("<h1>EYE4PRO Acoustics - Panel Tasarım Raporu</h1>");
        sb.AppendLine($"<p><strong>Tarih:</strong> {DateTime.Now:dd.MM.yyyy HH:mm}</p>");
        sb.AppendLine("<h2>Akustik Performans</h2>");
        sb.AppendLine("<table><tr><th>NRC</th><th>alphaW</th><th>Sınıf</th></tr>");
        sb.AppendLine($"<tr><td class=\"num\">{Nrc:F2}</td><td class=\"num\">{H(AlphaWLabel)}</td><td>{H(AbsorptionClass)}</td></tr></table>");
        sb.AppendLine("<h2>Katman Yapısı</h2><table><tr><th>Katman</th><th>Kalınlık</th><th>Durum</th><th>Detay</th></tr>");
        foreach (var l in Layers)
        {
            sb.AppendLine($"<tr><td>{H(l.Name)}</td><td class=\"num\">{l.Thickness:F1} mm</td><td>{(l.IsIncludedInCalculation ? "Hesapta" : "Hesap dışı")}</td><td>{H(DescribeLayer(l))}</td></tr>");
        }
        sb.AppendLine("</table>");
        AppendHtmlReliabilitySection(sb, H);
        AppendHtmlFrequencySection(sb, H);
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private IReadOnlyList<KeyValuePair<int, double>> GetReportOctaves()
    {
        var models = GetIncludedModels();
        if (models.Count == 0)
            return Array.Empty<KeyValuePair<int, double>>();

        return TmmCalculator.CalculateRandomIncidenceOctaveBands(models)
            .OrderBy(x => x.Key)
            .ToArray();
    }

    private void AppendTextFrequencySection(System.Text.StringBuilder sb)
    {
        sb.AppendLine("FREKANS BAZLI SOĞURMA KATSAYILARI (Rastgele Geliş Yaklaşımı):");
        var octaves = GetReportOctaves();
        if (octaves.Count == 0)
        {
            sb.AppendLine("Hesapta katman yok; frekans tablosu üretilmedi.");
            return;
        }

        foreach (var kv in octaves)
            sb.AppendLine($"{kv.Key,5} Hz : {kv.Value:F2}");
    }

    private void AppendMarkdownFrequencySection(System.Text.StringBuilder sb)
    {
        sb.AppendLine("## Frekans Bazlı Soğurma");
        sb.AppendLine();
        var octaves = GetReportOctaves();
        if (octaves.Count == 0)
        {
            sb.AppendLine("Hesapta katman yok; frekans tablosu üretilmedi.");
            return;
        }

        sb.AppendLine("| Frekans | α |");
        sb.AppendLine("|---:|---:|");
        foreach (var kv in octaves)
            sb.AppendLine($"| {kv.Key} Hz | {kv.Value:F2} |");
    }

    private void AppendHtmlFrequencySection(System.Text.StringBuilder sb, Func<string, string> h)
    {
        sb.AppendLine("<h2>Frekans Bazlı Soğurma</h2>");
        var octaves = GetReportOctaves();
        if (octaves.Count == 0)
        {
            sb.AppendLine($"<p>{h("Hesapta katman yok; frekans tablosu üretilmedi.")}</p>");
            return;
        }

        sb.AppendLine("<table><tr><th>Frekans</th><th>α</th></tr>");
        foreach (var kv in octaves)
            sb.AppendLine($"<tr><td class=\"num\">{kv.Key} Hz</td><td class=\"num\">{kv.Value:F2}</td></tr>");
        sb.AppendLine("</table>");
    }

    private static string DescribeLayer(LayerViewModel l)
    {
        if (l.IsPorous) return $"Gözenekli, akış direnci {l.FlowResistivity} Rayl/m";
        if (l.IsFabric) return $"Akustik kumaş, yüzey direnci {l.SpecificFlowResistance} Rayl, yüzey kütlesi {l.SurfaceDensity:F3} kg/m²";
        if (l.IsPerforated && l.UsesCustomOpenings) return $"Özel DXF, {l.CustomOpeningCount} açıklık, %{l.OpenAreaRatio:F1} boşluk, hidrolik çap dağılımı";
        if (l.IsPerforated) return $"Delikli, {l.HolePatternName}, {l.HoleSpacing:F1} mm aks, %{l.OpenAreaRatio:F1} boşluk, {l.HoleDiameter:F1} mm çap";
        return "Hava boşluğu";
    }

    private static string EscapeMarkdown(string value) => value.Replace("|", "\\|");

    private static IReadOnlyList<string> BuildCustomOpeningWarnings(PerforatedLayer layer)
    {
        var warnings = new List<string>();
        var openings = layer.CustomOpenings;
        if (openings is not { Count: > 0 }) return warnings;

        double minDh = openings.Min(o => o.HydraulicDiameter);
        double maxDh = openings.Max(o => o.HydraulicDiameter);
        double maxAspect = openings.Max(o => o.AspectRatio);
        double minCircularity = openings.Min(o => o.Circularity);
        double tOverDh = minDh > 0 ? layer.Thickness / minDh : double.PositiveInfinity;

        if (layer.OpenAreaRatio > 0.45)
            warnings.Add("⚠️ Özel DXF: Açık alan oranı %45 üzeri; model doğruluğu azalır.");
        if (minDh < 0.001)
            warnings.Add("⚠️ Özel DXF: Minimum hidrolik çap 1 mm altında; üretim toleransı ve viskoz kayıplar kritik.");
        if (maxDh / Math.Max(minDh, 1e-9) > 6.0)
            warnings.Add("⚠️ Özel DXF: Açıklık boyut dağılımı 6x üzerinde; panel davranışı heterojen olabilir.");
        if (tOverDh < 0.1 || tOverDh > 5.0)
            warnings.Add($"⚠️ Özel DXF: t/Dh={tOverDh:F2}; delikli levha yaklaşımı bu aralıkta daha yaklaşık sonuç verir.");
        if (maxAspect >= 8.0)
            warnings.Add("⚠️ Özel DXF: Çok uzun slot/yarık açıklık var; slot modeli yaklaşık katsayılarla kullanıldı.");
        if (minCircularity < 0.20)
            warnings.Add("⚠️ Özel DXF: Çok düşük dairesellikli açıklık var; FEA/BEM veya ölçüm doğrulaması önerilir.");

        return warnings;
    }

    private static string BuildDxfQualitySummary(DxfAnalysisResult result)
    {
        var q = result.Quality;
        if (q == null) return DefaultDxfQualitySummary;

        return $"DXF kalite analizi: {q.ConfidenceLabel} ({q.ConfidenceScore}/100) | " +
               $"{result.HoleCount} açıklık | " +
               $"boşluk %{result.OpenAreaRatio * 100:F2} | " +
               $"Dh {q.MinHydraulicDiameterMeters * 1000:F1}-{q.MaxHydraulicDiameterMeters * 1000:F1} mm | " +
               $"slot/serbest {q.SlotLikeOpeningCount}";
    }

    public static IReadOnlyList<string> BuildDxfQualityItems(DxfAnalysisResult result)
    {
        var q = result.Quality;
        if (q == null) return Array.Empty<string>();

        var items = new List<string>
        {
            $"- Güven skoru: {q.ConfidenceLabel} ({q.ConfidenceScore}/100).",
            $"- Geometri: {result.HoleCount} açıklık, %{result.OpenAreaRatio * 100:F2} boşluk, {result.TotalAreaSquareMeters:F3} m² panel alanı."
        };

        if (result.Openings is { Count: > 0 })
        {
            items.Add(
                $"- Hidrolik çap: min {q.MinHydraulicDiameterMeters * 1000:F2} mm, " +
                $"ort {q.MeanHydraulicDiameterMeters * 1000:F2} mm, " +
                $"max {q.MaxHydraulicDiameterMeters * 1000:F2} mm.");
        }

        if (q.SlotLikeOpeningCount > 0 || q.FreeShapeOpeningCount > 0 || q.OpenGuideEntityCount > 0)
        {
            items.Add(
                $"- Şekil kontrolü: {q.SlotLikeOpeningCount} slot/serbest, " +
                $"{q.FreeShapeOpeningCount} serbest biçim, " +
                $"{q.OpenGuideEntityCount} açık rehber çizgi hesap dışı.");
        }

        if (result.InvalidNumericValueCount > 0)
            items.Add($"- Okuma kontrolü: {result.InvalidNumericValueCount} sayısal DXF değeri okunamadı.");

        foreach (var warning in q.Warnings.Take(5))
            items.Add($"- Kontrol: {warning}");

        if (q.Warnings.Count > 5)
            items.Add($"- Kontrol: {q.Warnings.Count - 5} ek uyarı raporda yer alıyor.");

        return items;
    }

    private static string BuildDxfQualityDetails(DxfAnalysisResult result)
        => string.Join("\n", BuildDxfQualityItems(result));

    private static string BuildCalculationAssumptionSummary(DxfQualityMetrics? quality)
    {
        string confidence = quality == null ? "Parametrik geometri" : $"{quality.ConfidenceLabel} güven";
        return $"Varsayım: Hidrolik çap dağılımlı delikli panel + TMM, lokal reaksiyon ve rijit arkalık. Güven: {confidence}.";
    }

    private void ResetDxfQualityState()
    {
        _lastDxfQuality = null;
        DxfQualitySummary = DefaultDxfQualitySummary;
        DxfQualityDetails = string.Empty;
        CalculationAssumptionSummary = DefaultCalculationAssumptionSummary;
    }

    private void AppendTextReliabilitySection(System.Text.StringBuilder sb)
    {
        sb.AppendLine("HESAP GÜVENİ VE VARSAYIMLAR:");
        sb.AppendLine(CalculationAssumptionSummary);
        sb.AppendLine(DxfQualitySummary);
        if (!string.IsNullOrWhiteSpace(DxfQualityDetails))
        {
            sb.AppendLine("DXF kalite karar maddeleri:");
            foreach (var item in EnumerateDxfQualityDetailLines(DxfQualityDetails))
                sb.AppendLine(item);
        }
        if (!string.IsNullOrWhiteSpace(WarningMessage))
        {
            sb.AppendLine("Uyarılar:");
            foreach (var warning in WarningMessage.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                sb.AppendLine($"- {warning.Replace("⚠️ ", "")}");
        }
    }

    private void AppendMarkdownReliabilitySection(System.Text.StringBuilder sb)
    {
        sb.AppendLine("## Hesap Güveni ve Varsayımlar");
        sb.AppendLine();
        sb.AppendLine($"- {EscapeMarkdown(CalculationAssumptionSummary)}");
        sb.AppendLine($"- {EscapeMarkdown(DxfQualitySummary)}");
        foreach (var item in EnumerateDxfQualityDetailLines(DxfQualityDetails))
            sb.AppendLine($"  {EscapeMarkdown(item)}");
        foreach (var warning in WarningMessage.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            sb.AppendLine($"- {EscapeMarkdown(warning.Replace("⚠️ ", ""))}");
    }

    private void AppendHtmlReliabilitySection(System.Text.StringBuilder sb, Func<string, string> h)
    {
        sb.AppendLine("<h2>Hesap Güveni ve Varsayımlar</h2>");
        sb.AppendLine("<ul>");
        sb.AppendLine($"<li>{h(CalculationAssumptionSummary)}</li>");
        sb.AppendLine($"<li>{h(DxfQualitySummary)}</li>");
        foreach (var item in EnumerateDxfQualityDetailLines(DxfQualityDetails))
            sb.AppendLine($"<li>{h(item.TrimStart('-', ' '))}</li>");
        foreach (var warning in WarningMessage.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            sb.AppendLine($"<li>{h(warning.Replace("⚠️ ", ""))}</li>");
        sb.AppendLine("</ul>");
    }

    private static IReadOnlyList<string> EnumerateDxfQualityDetailLines(string details)
    {
        if (string.IsNullOrWhiteSpace(details)) return Array.Empty<string>();

        return details
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
    }

    private static string EnsureDxfExtension(string path)
    {
        var extension = System.IO.Path.GetExtension(path);
        return extension.Equals(".dxf", StringComparison.OrdinalIgnoreCase)
            ? path
            : System.IO.Path.ChangeExtension(path, ".dxf");
    }
}

public record struct GraphPoint(double X, double Y);
