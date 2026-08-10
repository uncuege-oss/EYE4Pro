using Kst.Acoustics.Core;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Simulation;

namespace Kst.Acoustics.App.ViewModels;

public sealed partial class ProjectWindowViewModel
{
    private readonly RayTracingService _rayTracer = new();
    private CancellationTokenSource?   _simCts;
    private long _simulationInputRevision;
    private long _preflightCacheRevision = -1;
    private IReadOnlyList<string> _preflightBlockingIssues = [];

    /// <summary>Simülasyon başarıyla bitince Sonuçlar sekmesine geçmek için View tarafından dinlenir.</summary>
    public event Action? NavigateToResults;

    // ── Parametreler ──────────────────────────────────────────────────────────

    public int RayCount
    {
        get => _rayCount;
        set
        {
            if (SetProperty(ref _rayCount, Math.Clamp(value, 100, 100_000)))
                OnSimulationInputSettingChanged("Işın sayısı değişti.");
        }
    }
    private int _rayCount = 5_000;

    public int MaxOrder
    {
        get => _maxOrder;
        set
        {
            if (SetProperty(ref _maxOrder, Math.Clamp(value, 10, 500)))
                OnSimulationInputSettingChanged("Maksimum yansıma sırası değişti.");
        }
    }
    private int _maxOrder = 150;

    /// <summary>
    /// Kullanıcıya daima metre cinsinden gösterilir. Ray tracer sınırında proje
    /// birimine çevrilir; böylece mm/cm projelerinde 1 mm yarıçap tuzağı oluşmaz.
    /// </summary>
    public double ReceiverRadius
    {
        get => _receiverRadius;
        set
        {
            var normalized = double.IsFinite(value) ? Math.Clamp(value, 0.01, 10.0) : 1.0;
            if (SetProperty(ref _receiverRadius, normalized))
                OnSimulationInputSettingChanged("Alıcı yarıçapı değişti.");
        }
    }
    private double _receiverRadius = 1.0;

    public double AirTemperatureCelsius
    {
        get => _airTempC;
        set
        {
            var normalized = double.IsFinite(value) ? Math.Clamp(value, -20.0, 50.0) : 20.0;
            if (SetProperty(ref _airTempC, normalized))
                OnSimulationInputSettingChanged("Hava sıcaklığı değişti.");
        }
    }
    private double _airTempC = 20.0;

    public double RelativeHumidity
    {
        get => _relHumidity;
        set
        {
            var normalized = double.IsFinite(value) ? Math.Clamp(value, 10.0, 100.0) : 50.0;
            if (SetProperty(ref _relHumidity, normalized))
                OnSimulationInputSettingChanged("Bağıl nem değişti.");
        }
    }
    private double _relHumidity = 50.0;

    public int IsmOrder
    {
        get => _ismOrder;
        set
        {
            if (SetProperty(ref _ismOrder, Math.Clamp(value, 0, 2)))
                OnSimulationInputSettingChanged("Görüntü kaynak mertebesi değişti.");
        }
    }
    private int _ismOrder = 1;

    public IReadOnlyList<string> IsmOrderOptions { get; } =
        ["0 — Kapalı", "1 — Birinci mertebe", "2 — İkinci mertebe"];

    public IReadOnlyList<string> ConvergenceModeOptions { get; } =
        ["Hızlı", "Dengeli", "Hassas"];

    public IReadOnlyList<DirectivityPattern> DirectivityOptions { get; } =
        Enum.GetValues<DirectivityPattern>();

    public int ConvergenceModeIndex
    {
        get => _convergenceModeIndex;
        set
        {
            if (SetProperty(ref _convergenceModeIndex, Math.Clamp(value, 0, 2)))
                OnSimulationInputSettingChanged("Yakınsama profili değişti.");
        }
    }
    private int _convergenceModeIndex = 1;

    public int TransitionOrder
    {
        get => _transitionOrder;
        set
        {
            if (SetProperty(ref _transitionOrder, Math.Clamp(value, 5, 150)))
                OnSimulationInputSettingChanged("Geçiş mertebesi değişti.");
        }
    }
    private int _transitionOrder = 30;

    // ── Izgara harita ─────────────────────────────────────────────────────────

    public bool GridMapEnabled
    {
        get => _gridMapEnabled;
        set
        {
            if (!SetProperty(ref _gridMapEnabled, value)) return;
            if (value) AutoInitGridBounds();
            OnSimulationInputSettingChanged("Izgara harita ayarı değişti.");
        }
    }
    private bool _gridMapEnabled;

    private void AutoInitGridBounds()
    {
        if (_workingSurfaces.Count == 0) return;

        var finiteVertices = _workingSurfaces.SelectMany(s => s.Vertices)
            .Where(v => double.IsFinite(v.X) && double.IsFinite(v.Y) && double.IsFinite(v.Z))
            .ToArray();
        var xs = finiteVertices.Select(v => v.X).ToList();
        var ys = finiteVertices.Select(v => v.Y).ToList();
        var zs = finiteVertices.Select(v => v.Z).ToList();
        if (xs.Count == 0) return;

        var margin = (xs.Max() - xs.Min()) * 0.05;
        GridXMin   = Math.Round(xs.Min() - margin, 2);
        GridXMax   = Math.Round(xs.Max() + margin, 2);
        GridYMin   = Math.Round(ys.Min() - margin, 2);
        GridYMax   = Math.Round(ys.Max() + margin, 2);
        GridPlaneZ = Math.Round((zs.Min() + zs.Max()) / 2.0, 2);

        var span = Math.Max(GridXMax - GridXMin, GridYMax - GridYMin);
        GridCellSize = Math.Round(Math.Max(span / 20.0, MinimumGridCellSize), 4);
    }

    public double GridPlaneZ
    {
        get => _gridPlaneZ;
        set
        {
            if (SetProperty(ref _gridPlaneZ, value)) OnSimulationInputSettingChanged("Izgara düzlemi değişti.");
        }
    }
    private double _gridPlaneZ;

    public double GridXMin
    {
        get => _gridXMin;
        set
        {
            if (SetProperty(ref _gridXMin, value)) OnSimulationInputSettingChanged("Izgara X sınırı değişti.");
        }
    }
    private double _gridXMin = -5;

    public double GridXMax
    {
        get => _gridXMax;
        set
        {
            if (SetProperty(ref _gridXMax, value)) OnSimulationInputSettingChanged("Izgara X sınırı değişti.");
        }
    }
    private double _gridXMax = 5;

    public double GridYMin
    {
        get => _gridYMin;
        set
        {
            if (SetProperty(ref _gridYMin, value)) OnSimulationInputSettingChanged("Izgara Y sınırı değişti.");
        }
    }
    private double _gridYMin = -5;

    public double GridYMax
    {
        get => _gridYMax;
        set
        {
            if (SetProperty(ref _gridYMax, value)) OnSimulationInputSettingChanged("Izgara Y sınırı değişti.");
        }
    }
    private double _gridYMax = 5;

    public double GridCellSize
    {
        get => _gridCellSize;
        set
        {
            var normalized = double.IsFinite(value) ? Math.Max(MinimumGridCellSize, value) : MinimumGridCellSize;
            if (SetProperty(ref _gridCellSize, normalized)) OnSimulationInputSettingChanged("Izgara hücre boyutu değişti.");
        }
    }
    private double _gridCellSize = 1.0;

    public double MinimumGridCellSize => 0.05 / ProjectScalingService.GetMetersPerUnit(_project.Unit);

    public bool MaskGridOutsideRoom
    {
        get => _maskGridOutsideRoom;
        set
        {
            if (SetProperty(ref _maskGridOutsideRoom, value))
                OnSimulationInputSettingChanged("Izgara oda dışı maskelemesi değişti.");
        }
    }
    private bool _maskGridOutsideRoom = true;

    // ── Çalışma durumu ────────────────────────────────────────────────────────

    public bool IsSimulating
    {
        get => _isSimulating;
        private set
        {
            if (SetProperty(ref _isSimulating, value))
                OnPropertyChanged(nameof(CanRunSimulation));
        }
    }
    private bool _isSimulating;

    public bool CanRunSimulation =>
        !IsSimulating && !IsConvergenceRunning && GetSimulationBlockingIssues().Count == 0;

    public bool HasSimulationBlockingIssues => GetSimulationBlockingIssues().Count > 0;

    public string SimulationPreflightText
    {
        get
        {
            var blockers = GetSimulationBlockingIssues();
            if (blockers.Count > 0)
                return "Simülasyon hazır değil:\n• " + string.Join("\n• ", blockers);

            var warnings = new List<string>();
            if (CategoryMaterialRows.Any(r => r.SelectedMaterial == MaterialLibrary.Default))
                warnings.Add("Bazı kategoriler varsayılan materyali kullanıyor.");
            if (RoomVolumeSourceText.Contains("tahmin", StringComparison.OrdinalIgnoreCase))
                warnings.Add("Oda hacmi kapalı kabuk yerine tahmine dayanıyor.");

            var ready = $"✓ Hazır: {SourcePoints.Count} kaynak, {ReceiverPoints.Count} alıcı, " +
                        $"{_workingSurfaces.Count} yüzey.";
            return warnings.Count == 0
                ? ready
                : ready + "\nUyarı:\n• " + string.Join("\n• ", warnings);
        }
    }

    public int GridCellCount
    {
        get
        {
            if (!TryGetGridDimensions(out var cols, out var rows)) return 0;
            var cells = (long)cols * rows;
            return cells > int.MaxValue ? int.MaxValue : (int)cells;
        }
    }

    public string GridEstimatedWorkText
    {
        get
        {
            if (!GridMapEnabled) return "Izgara haritası kapalı.";
            if (!TryGetGridDimensions(out var cols, out var rows)) return "Izgara maliyeti hesaplanamadı.";
            var work = EstimateGridWork(cols, rows);
            return $"{(long)cols * rows:N0} hücre • tahmini iş yükü {work:N0}" +
                   (MaskGridOutsideRoom ? " • oda dışı maskeli" : string.Empty);
        }
    }

    public bool IsConvergenceRunning
    {
        get => _isConvergenceRunning;
        private set
        {
            if (SetProperty(ref _isConvergenceRunning, value))
                OnPropertyChanged(nameof(CanRunSimulation));
        }
    }
    private bool _isConvergenceRunning;

    public bool IsSimulationResultStale
    {
        get => _isSimulationResultStale;
        private set
        {
            if (SetProperty(ref _isSimulationResultStale, value))
            {
                OnPropertyChanged(nameof(HasCurrentSimulationResult));
                OnPropertyChanged(nameof(SimulationResultStateText));
                OnPropertyChanged(nameof(CanAuralizeSelectedPair));
            }
        }
    }
    private bool _isSimulationResultStale;

    public bool HasCurrentSimulationResult => LastResult is not null && !IsSimulationResultStale;

    public string SimulationResultStateText => LastResult switch
    {
        null => "Güncel simülasyon sonucu yok.",
        _ when IsSimulationResultStale => $"Eski sonuç — {LastResult.StaleReason ?? "girdiler değişti"}",
        _ => $"✓ Sonuç güncel • iz: {ShortFingerprint(LastResult.InputFingerprint)}"
    };

    public int SimulationProgress
    {
        get => _simProgress;
        private set => SetProperty(ref _simProgress, value);
    }
    private int _simProgress;

    public string SimulationStatusText
    {
        get => _simStatusText;
        private set => SetProperty(ref _simStatusText, value);
    }
    private string _simStatusText = "Simülasyon henüz çalıştırılmadı.";

    public string ConvergenceSummaryText
    {
        get => _convergenceSummaryText;
        private set => SetProperty(ref _convergenceSummaryText, value);
    }
    private string _convergenceSummaryText = "Yakınsama analizi henüz çalıştırılmadı.";

    public string MaterialAuditText
    {
        get => _materialAuditText;
        private set => SetProperty(ref _materialAuditText, value);
    }
    private string _materialAuditText = "Materyal denetimi simülasyon sonrası güncellenir.";

    public string ResultComparisonText
    {
        get => _resultComparisonText;
        private set => SetProperty(ref _resultComparisonText, value);
    }
    private string _resultComparisonText = "Önce/sonra karşılaştırması için en az iki simülasyon gerekir.";

    // ── Sonuç ──────────────────────────────────────────────────────────────────

    public SimulationResult? LastResult
    {
        get => _lastResult;
        private set
        {
            if (SetProperty(ref _lastResult, value))
            {
                OnPropertyChanged(nameof(HasCurrentSimulationResult));
                OnPropertyChanged(nameof(SimulationResultStateText));
                OnPropertyChanged(nameof(CanAuralizeSelectedPair));
            }
        }
    }
    private SimulationResult? _lastResult;
    private SimulationResult? _previousResult;

    // ── Komutlar ──────────────────────────────────────────────────────────────

    public async Task RunSimulationAsync()
    {
        if (!CanRunSimulation)
        {
            SimulationStatusText = SimulationPreflightText;
            StatusMessage = SimulationStatusText;
            return;
        }

        var cts = StartSimulationRun();
        var ct = cts.Token;
        var inputRevision = Interlocked.Read(ref _simulationInputRevision);

        IsSimulating      = true;
        SimulationProgress = 0;
        SimulationStatusText = $"Simülasyon hazırlanıyor — {RayCount:N0} ışın, {SourcePoints.Count} kaynak, {ReceiverPoints.Count} alıcı…";
        StatusMessage = SimulationStatusText;

        ReceiverGrid? grid = null;
        if (GridMapEnabled)
        {
            var gridError = ValidateGridMap();
            if (gridError is not null)
            {
                SimulationStatusText = gridError;
                StatusMessage = gridError;
                IsSimulating = false;
                SimulationProgress = 0;
                CompleteSimulationRun(cts);
                return;
            }

            grid = new ReceiverGrid(GridPlaneZ, GridXMin, GridXMax, GridYMin, GridYMax, GridCellSize);
        }

        var input = CreateSimulationRunInput(grid);

        var progress = new Progress<int>(v =>
        {
            SimulationProgress = Math.Clamp(v, 0, 100);
            SimulationStatusText = v switch
            {
                < 5 => "Geometri ve hızlandırma yapısı hazırlanıyor…",
                < 76 => $"Işınlar izleniyor… %{v}",
                < 92 => $"Izgara haritası hesaplanıyor… %{v}",
                < 100 => $"Akustik parametreler ve kalite denetimi hesaplanıyor… %{v}",
                _ => "Sonuç tamamlanıyor…"
            };
        });

        try
        {
            var result = await Task.Run(
                () => _rayTracer.Run(input.Project, input.Materials, input.Parameters, progress, ct),
                ct);

            if (result.TotalArrivals == 0)
            {
                SimulationStatusText =
                    $"Geliş algılanamadı. Önceki geçerli sonuç korundu. Alıcı yarıçapını " +
                    $"artırın (şu an: {ReceiverRadius:F2} m) veya kaynak/alıcı konumlarını kontrol edin.";
            }
            else if (inputRevision != Interlocked.Read(ref _simulationInputRevision))
            {
                SimulationStatusText = "Simülasyon sırasında girdiler değişti; yeni sonuç kullanılmadı. Önceki sonuç korundu.";
            }
            else
            {
                _previousResult = LastResult;
                ResultComparisonText = BuildResultComparison(_previousResult, result);
                MaterialAuditText = BuildMaterialAuditText(input.Materials);
                SimulationStatusText =
                    $"✓ Tamamlandı — {result.TotalArrivals:N0} geliş, {result.Elapsed.TotalSeconds:F1} sn";
                OnSimulationCompleted(result);
                MarkDirty("Simülasyon sonucu güncellendi");
                NavigateToResults?.Invoke();
            }

            StatusMessage = SimulationStatusText;
        }
        catch (OperationCanceledException)
        {
            SimulationStatusText = "Simülasyon iptal edildi.";
            StatusMessage = SimulationStatusText;
        }
        catch (Exception ex)
        {
            SimulationStatusText = $"Hata: {ex.Message}";
            StatusMessage        = SimulationStatusText;
        }
        finally
        {
            IsSimulating       = false;
            SimulationProgress = 0;
            CompleteSimulationRun(cts);
        }
    }

    public SimulationParameters BuildStoredSimulationSettings()
    {
        ReceiverGrid? grid = GridMapEnabled && ValidateGridMap() is null
            ? new ReceiverGrid(GridPlaneZ, GridXMin, GridXMax, GridYMin, GridYMax, GridCellSize)
            : null;
        return BuildSimulationParameters(grid);
    }

    private SimulationParameters BuildSimulationParameters(ReceiverGrid? grid = null, int? rayCountOverride = null) =>
        new SimulationParameters(
            RayCount:               RayCount,
            MaxOrder:               MaxOrder,
            ReceiverRadius:         ReceiverRadius / ProjectScalingService.GetMetersPerUnit(_project.Unit),
            AirTemperatureCelsius:  AirTemperatureCelsius,
            RelativeHumidity:       RelativeHumidity,
            IsmOrder:               IsmOrder,
            TransitionOrder:        TransitionOrder,
            ReceiverGrid:           grid,
            UnitScale:              ProjectScalingService.GetMetersPerUnit(_project.Unit),
            MaskGridOutsideRoom:    MaskGridOutsideRoom,
            ConvergenceModeIndex:   ConvergenceModeIndex) with { RayCount = rayCountOverride ?? RayCount };

    internal void RestoreSimulationSettings(SimulationParameters? settings)
    {
        var metersPerUnit = ProjectScalingService.GetMetersPerUnit(_project.Unit);
        settings ??= new SimulationParameters(
            ReceiverRadius: 1.0 / metersPerUnit,
            UnitScale: metersPerUnit);

        _rayCount = Math.Clamp(settings.RayCount, 100, 100_000);
        _maxOrder = Math.Clamp(settings.MaxOrder, 10, 500);
        var radiusMeters = settings.ReceiverRadius * metersPerUnit;
        _receiverRadius = double.IsFinite(radiusMeters) ? Math.Clamp(radiusMeters, 0.01, 10.0) : 1.0;
        _airTempC = double.IsFinite(settings.AirTemperatureCelsius)
            ? Math.Clamp(settings.AirTemperatureCelsius, -20.0, 50.0)
            : 20.0;
        _relHumidity = double.IsFinite(settings.RelativeHumidity)
            ? Math.Clamp(settings.RelativeHumidity, 10.0, 100.0)
            : 50.0;
        _ismOrder = Math.Clamp(settings.IsmOrder, 0, 2);
        _transitionOrder = Math.Clamp(settings.TransitionOrder, 5, 150);
        _convergenceModeIndex = Math.Clamp(settings.ConvergenceModeIndex, 0, 2);
        _maskGridOutsideRoom = settings.MaskGridOutsideRoom;

        _gridMapEnabled = false;
        if (settings.ReceiverGrid is { } grid)
        {
            _gridMapEnabled = true;
            _gridPlaneZ = grid.PlaneZ;
            _gridXMin = grid.XMin;
            _gridXMax = grid.XMax;
            _gridYMin = grid.YMin;
            _gridYMax = grid.YMax;
            _gridCellSize = Math.Max(MinimumGridCellSize, grid.CellSize);
        }

        foreach (var property in new[]
                 {
                     nameof(RayCount), nameof(MaxOrder), nameof(ReceiverRadius),
                     nameof(AirTemperatureCelsius), nameof(RelativeHumidity), nameof(IsmOrder),
                     nameof(TransitionOrder), nameof(ConvergenceModeIndex), nameof(GridMapEnabled),
                     nameof(GridPlaneZ), nameof(GridXMin), nameof(GridXMax), nameof(GridYMin),
                     nameof(GridYMax), nameof(GridCellSize), nameof(MaskGridOutsideRoom),
                     nameof(GridCellCount), nameof(GridEstimatedWorkText), nameof(SimulationPreflightText)
                 })
            OnPropertyChanged(property);
        OnPropertyChanged(nameof(CanRunSimulation));
    }

    private SimulationRunInput CreateSimulationRunInput(ReceiverGrid? grid = null, int? rayCountOverride = null)
    {
        var materialMap = new Dictionary<string, string>(BuildCategoryMaterialMap(), StringComparer.OrdinalIgnoreCase);
        var surfaceMaterialMap = new Dictionary<string, string>(BuildSurfaceMaterialMap(), StringComparer.OrdinalIgnoreCase);
        var surfaces = _workingSurfaces
            .Select(s => s with { Vertices = s.Vertices.ToArray() })
            .ToArray();
        var sources = SourcePoints.ToArray();
        var receivers = ReceiverPoints.ToArray();

        var project = _project with
        {
            Surfaces = surfaces,
            CategoryMaterials = materialMap,
            SurfaceMaterials = surfaceMaterialMap,
            RoomVolume = RoomVolume,
            Sources = sources,
            Receivers = receivers
        };
        var parameters = BuildSimulationParameters(grid, rayCountOverride);
        var fingerprint = SimulationInputFingerprint.Compute(project, parameters);
        parameters = parameters with { InputFingerprint = fingerprint };
        return new SimulationRunInput(project, materialMap, parameters);
    }

    private readonly record struct SimulationRunInput(
        AcousticsProject Project,
        IReadOnlyDictionary<string, string> Materials,
        SimulationParameters Parameters);

    public async Task RunConvergenceAnalysisAsync()
    {
        if (!CanRunSimulation)
        {
            ConvergenceSummaryText = SimulationPreflightText;
            StatusMessage = ConvergenceSummaryText;
            return;
        }

        var cts = StartSimulationRun();
        var ct = cts.Token;
        var inputRevision = Interlocked.Read(ref _simulationInputRevision);
        IsConvergenceRunning = true;
        IsSimulating = true;
        SimulationProgress = 0;
        ConvergenceSummaryText = "Yakınsama analizi çalışıyor...";
        StatusMessage = ConvergenceSummaryText;

        var rayCounts = BuildConvergenceRayCounts();
        var runs = new List<(int Rays, SimulationResult Result, ParameterSnapshot Snapshot)>();
        var skippedHeavyRun = false;

        try
        {
            for (var i = 0; i < rayCounts.Count; i++)
            {
                var rays = rayCounts[i];
                SimulationStatusText = $"Yakınsama analizi — {rays:N0} ışın/kaynak ({i + 1}/{rayCounts.Count})";
                var progress = new Progress<int>(v =>
                {
                    SimulationProgress = (int)Math.Clamp((i * 100.0 + v) / rayCounts.Count, 0, 100);
                });
                var input = CreateSimulationRunInput(rayCountOverride: rays);
                var result = await Task.Run(
                    () => _rayTracer.Run(input.Project, input.Materials, input.Parameters, progress, ct), ct);
                runs.Add((rays, result, ParameterSnapshot.From(result)));

                if (ShouldStopConvergenceEarly(runs))
                {
                    skippedHeavyRun = i < rayCounts.Count - 1;
                    break;
                }
            }

            ConvergenceSummaryText = BuildConvergenceSummary(runs, skippedHeavyRun);
            var finalRun = runs.LastOrDefault(r => r.Result.TotalArrivals > 0 && r.Snapshot.ValidMetricCount >= 3);
            if (finalRun.Result is not null && inputRevision == Interlocked.Read(ref _simulationInputRevision))
            {
                _previousResult = LastResult;
                ResultComparisonText = BuildResultComparison(_previousResult, finalRun.Result);
                MaterialAuditText = BuildMaterialAuditText(BuildCategoryMaterialMap());
                OnSimulationCompleted(finalRun.Result);
                MarkDirty("Yakınsama simülasyonu sonucu güncellendi");
                NavigateToResults?.Invoke();
                SimulationStatusText = "✓ Yakınsama analizi tamamlandı.";
            }
            else if (inputRevision != Interlocked.Read(ref _simulationInputRevision))
            {
                SimulationStatusText = "Yakınsama sırasında girdiler değişti; yeni sonuç kullanılmadı.";
            }
            else
            {
                SimulationStatusText = "Yakınsama geçerli metrik üretmedi; önceki sonuç korundu.";
            }
            StatusMessage = ConvergenceSummaryText;
        }
        catch (OperationCanceledException)
        {
            SimulationStatusText = "Yakınsama analizi iptal edildi.";
            StatusMessage = SimulationStatusText;
        }
        catch (Exception ex)
        {
            SimulationStatusText = $"Yakınsama hatası: {ex.Message}";
            StatusMessage = SimulationStatusText;
        }
        finally
        {
            IsConvergenceRunning = false;
            IsSimulating = false;
            SimulationProgress = 0;
            CompleteSimulationRun(cts);
        }
    }

    public void CancelSimulation() => CancelActiveSimulation();

    public void CancelActiveSimulation()
    {
        var cts = Volatile.Read(ref _simCts);
        if (cts is { IsCancellationRequested: false }) cts.Cancel();
    }

    public void InvalidateSimulationResult(string reason)
    {
        Interlocked.Increment(ref _simulationInputRevision);
        CancelActiveSimulation();

        if (LastResult is not null)
        {
            var staleResult = CloneSimulationResult(LastResult);
            staleResult.IsStale = true;
            staleResult.StaleReason = string.IsNullOrWhiteSpace(reason) ? "Simülasyon girdileri değişti." : reason.Trim();
            LastResult = staleResult;
            IsSimulationResultStale = true;
            ResultsSummaryText = $"Eski sonuç: {LastResult.StaleReason}";
            GenerateReport();
        }

        OnPropertyChanged(nameof(CanRunSimulation));
        OnPropertyChanged(nameof(HasSimulationBlockingIssues));
        OnPropertyChanged(nameof(SimulationPreflightText));
        RefreshTargetPerformance();
        RefreshScenarioComparisons();
    }

    private IReadOnlyList<int> BuildConvergenceRayCounts()
    {
        var counts = ConvergenceModeIndex switch
        {
            0 => new[] { Math.Max(5_000, Math.Min(RayCount, 20_000)), 40_000, 70_000 },
            2 => new[] { Math.Max(20_000, Math.Min(RayCount, 50_000)), 100_000 },
            _ => new[] { Math.Max(10_000, Math.Min(RayCount, 30_000)), 60_000, 100_000 }
        };

        return counts
            .Select(v => Math.Clamp(v, 5_000, 100_000))
            .Where(v => v <= 100_000)
            .Distinct()
            .Order()
            .ToArray();
    }

    private bool ShouldStopConvergenceEarly(IReadOnlyList<(int Rays, SimulationResult Result, ParameterSnapshot Snapshot)> runs)
    {
        if (runs.Count < 2) return false;
        if (ConvergenceModeIndex == 2) return false;

        var lo = runs[^2];
        var hi = runs[^1];
        var stable = IsConvergenceStable(lo.Snapshot, hi.Snapshot, out _, out _, out _, out _, out _);
        return stable;
    }

    private static string BuildConvergenceSummary(
        IReadOnlyList<(int Rays, SimulationResult Result, ParameterSnapshot Snapshot)> runs,
        bool skippedHeavyRun = false)
    {
        if (runs.Count < 2) return "Yakınsama için en az iki koşu gerekli.";

        var lo = runs[^2];
        var hi = runs[^1];
        var stable = IsConvergenceStable(lo.Snapshot, hi.Snapshot, out var drt, out var dc80, out var dsti, out var dedt, out var dg);
        if (lo.Snapshot.ValidMetricCount < 3 || hi.Snapshot.ValidMetricCount < 3 || !double.IsFinite(drt))
        {
            return $"{lo.Rays:N0} → {hi.Rays:N0} yakınsama: yeterli sonlu akustik metrik üretilemedi; " +
                   "sonuç stabil kabul edilmedi. Kaynak/alıcı konumlarını, yarıçapı ve ışın sayısını kontrol edin.";
        }

        return $"{lo.Rays:N0} → {hi.Rays:N0} yakınsama: " +
               $"RT60 Δ={Fmt(drt, "F2")} s, C80 Δ={Fmt(dc80, "F1")} dB, STI Δ={Fmt(dsti, "F2")}, " +
               $"EDT Δ={Fmt(dedt, "F2")} s, G Δ={Fmt(dg, "F1")} dB. " +
               (stable
                   ? $"Sonuç stabil görünüyor.{(skippedHeavyRun ? " Ağır son koşu otomatik atlandı." : "")}"
                   : "Sonuç kararsız; ışın sayısı, materyal/scattering ve alıcı yarıçapı kontrol edilmeli.");
    }

    private static bool IsConvergenceStable(
        ParameterSnapshot lo,
        ParameterSnapshot hi,
        out double drt,
        out double dc80,
        out double dsti,
        out double dedt,
        out double dg)
    {
        var comparison = SimulationConvergenceEvaluator.Compare(
            new SimulationConvergenceMetrics(lo.RtMid, lo.C80, lo.Sti, lo.EdtMid, lo.G),
            new SimulationConvergenceMetrics(hi.RtMid, hi.C80, hi.Sti, hi.EdtMid, hi.G));
        drt = comparison.Rt60Difference;
        dc80 = comparison.C80Difference;
        dsti = comparison.StiDifference;
        dedt = comparison.EdtDifference;
        dg = comparison.StrengthDifference;
        return comparison.IsStable;
    }

    private static string BuildResultComparison(SimulationResult? previous, SimulationResult current)
    {
        if (previous is null)
            return "Önce/sonra karşılaştırması için bir sonraki simülasyon bekleniyor.";

        var a = ParameterSnapshot.From(previous);
        var b = ParameterSnapshot.From(current);
        return $"Önce/sonra: RT60 {Fmt(a.RtMid, "F2")}→{Fmt(b.RtMid, "F2")} s, " +
               $"C80 {Fmt(a.C80, "F1")}→{Fmt(b.C80, "F1")} dB, " +
               $"STI {Fmt(a.Sti, "F2")}→{Fmt(b.Sti, "F2")}, " +
               $"güven {previous.Reliability?.ConfidenceScore.ToString("F0") ?? "—"}→{current.Reliability?.ConfidenceScore.ToString("F0") ?? "—"}.";
    }

    private string BuildMaterialAuditText(IReadOnlyDictionary<string, string> matMap)
    {
        var groups = _workingSurfaces
            .GroupBy(s => s.Category, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                Category = g.Key,
                Count = g.Count(),
                Area = g.Sum(s => SurfaceTransformService.ComputeArea(s.Vertices)),
                MaterialId = matMap.TryGetValue(g.Key, out var id) ? id : MaterialLibrary.Default.Id
            })
            .OrderByDescending(g => g.Area)
            .ToArray();

        var missing = groups
            .Where(g => string.Equals(g.MaterialId, MaterialLibrary.Default.Id, StringComparison.OrdinalIgnoreCase))
            .Take(6)
            .Select(g => $"{g.Category} ({g.Count} yüzey)")
            .ToArray();

        if (missing.Length == 0)
            return "Materyal denetimi: tüm kategoriler özelleştirilmiş görünüyor. Scattering verilerini de kontrol edin.";

        return $"Materyal denetimi: varsayılan kalan öncelikli kategoriler: {string.Join(", ", missing)}.";
    }

    private static double AbsDiff(double a, double b) =>
        double.IsFinite(a) && double.IsFinite(b) ? Math.Abs(a - b) : double.NaN;

    private static string Fmt(double value, string format) =>
        double.IsFinite(value) ? value.ToString(format, System.Globalization.CultureInfo.InvariantCulture) : "—";

    private readonly record struct ParameterSnapshot(double RtMid, double C80, double Sti, double EdtMid, double G)
    {
        public int ValidMetricCount => new[] { RtMid, C80, Sti, EdtMid, G }.Count(double.IsFinite);

        public static ParameterSnapshot From(SimulationResult result)
        {
            var aps = result.ComputedParameters is { Count: > 0 } computed
                ? computed.ToArray()
                : result.Echograms.Select(e => AcousticParameterCalculator.Compute(e, result.Parameters)).ToArray();
            return new ParameterSnapshot(
                Avg(aps.Select(a => a.RT60_Mid)),
                Avg(aps.Select(a => a.Bands.FirstOrDefault(b => b.FrequencyHz == 1000)?.C80 ?? double.NaN)),
                Avg(aps.Select(a => a.STI)),
                Avg(aps.Select(a => Mid(a, b => b.EDT))),
                Avg(aps.Select(a => a.Bands.FirstOrDefault(b => b.FrequencyHz == 1000)?.G ?? double.NaN)));
        }

        private static double Mid(AcousticParameters a, Func<BandParameters, double> selector)
        {
            var v500 = a.Bands.FirstOrDefault(b => b.FrequencyHz == 500);
            var v1k = a.Bands.FirstOrDefault(b => b.FrequencyHz == 1000);
            if (v500 is null || v1k is null) return double.NaN;
            var a0 = selector(v500);
            var a1 = selector(v1k);
            return double.IsFinite(a0) && double.IsFinite(a1) ? (a0 + a1) / 2.0 : double.NaN;
        }

        private static double Avg(IEnumerable<double> values)
        {
            var finite = values.Where(double.IsFinite).ToArray();
            return finite.Length == 0 ? double.NaN : finite.Average();
        }
    }

    private void OnSimulationInputSettingChanged(string reason)
    {
        InvalidateSimulationResult(reason);
        MarkDirty(reason.TrimEnd('.'));
        OnPropertyChanged(nameof(GridCellCount));
        OnPropertyChanged(nameof(GridEstimatedWorkText));
        OnPropertyChanged(nameof(CanRunSimulation));
        OnPropertyChanged(nameof(HasSimulationBlockingIssues));
        OnPropertyChanged(nameof(SimulationPreflightText));
    }

    private CancellationTokenSource StartSimulationRun()
    {
        var next = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _simCts, next);
        if (previous is { IsCancellationRequested: false }) previous.Cancel();
        return next;
    }

    private void CompleteSimulationRun(CancellationTokenSource completed)
    {
        Interlocked.CompareExchange(ref _simCts, null, completed);
        completed.Dispose();
    }

    private IReadOnlyList<string> GetSimulationBlockingIssues()
    {
        var revision = Interlocked.Read(ref _simulationInputRevision);
        if (_preflightCacheRevision == revision) return _preflightBlockingIssues;

        var issues = new List<string>();
        if (_workingSurfaces.Count == 0)
            issues.Add("En az bir geçerli oda yüzeyi gerekli.");
        if (HasInvalidPoints)
            issues.Add("Kaynak/alıcı tablosunda geçersiz koordinat veya yön açısı var.");
        if (SourcePoints.Count == 0)
            issues.Add("En az bir ses kaynağı tanımlayın.");
        if (ReceiverPoints.Count == 0)
            issues.Add("En az bir alıcı tanımlayın.");
        if (!double.IsFinite(RoomVolume) || RoomVolume <= 0)
            issues.Add("Oda hacmi pozitif ve sonlu olmalı.");
        if (!double.IsFinite(ReceiverRadius) || ReceiverRadius is < 0.01 or > 10.0)
            issues.Add("Alıcı yarıçapı 0,01–10 m aralığında olmalı.");
        if (!double.IsFinite(AirTemperatureCelsius) || AirTemperatureCelsius is < -20.0 or > 50.0)
            issues.Add("Hava sıcaklığı -20–50 °C aralığında olmalı.");
        if (!double.IsFinite(RelativeHumidity) || RelativeHumidity is < 10.0 or > 100.0)
            issues.Add("Bağıl nem %10–100 aralığında olmalı.");

        var malformedSurfaces = _workingSurfaces.Count(s =>
            s.Vertices.Count < 3 || s.Vertices.Any(v =>
                !double.IsFinite(v.X) || !double.IsFinite(v.Y) || !double.IsFinite(v.Z)));
        if (malformedSurfaces > 0)
            issues.Add($"{malformedSurfaces} yüzeyde geçersiz geometri var.");

        if (_workingSurfaces.Count > 0 && malformedSurfaces == 0 &&
            RoomVolumeCalculator.Estimate(_workingSurfaces, _project.Unit).Origin == RoomVolumeOrigin.ClosedMesh)
        {
            var outsideSources = SourcePoints.Count(p => !RoomPointLocator.IsInside(p.Position, _workingSurfaces));
            var outsideReceivers = ReceiverPoints.Count(p => !RoomPointLocator.IsInside(p.Position, _workingSurfaces));
            if (outsideSources > 0) issues.Add($"{outsideSources} kaynak kapalı oda kabuğunun dışında.");
            if (outsideReceivers > 0) issues.Add($"{outsideReceivers} alıcı kapalı oda kabuğunun dışında.");
        }

        if (GridMapEnabled && ValidateGridMap() is { } gridError)
            issues.Add(gridError);
        _preflightBlockingIssues = issues;
        _preflightCacheRevision = revision;
        return _preflightBlockingIssues;
    }

    private bool TryGetGridDimensions(out int cols, out int rows)
    {
        cols = rows = 0;
        if (!double.IsFinite(GridXMin) || !double.IsFinite(GridXMax) ||
            !double.IsFinite(GridYMin) || !double.IsFinite(GridYMax) ||
            !double.IsFinite(GridCellSize) || GridCellSize <= 0 ||
            GridXMax <= GridXMin || GridYMax <= GridYMin)
            return false;

        var colValue = Math.Round((GridXMax - GridXMin) / GridCellSize) + 1;
        var rowValue = Math.Round((GridYMax - GridYMin) / GridCellSize) + 1;
        if (colValue is < 1 or > int.MaxValue || rowValue is < 1 or > int.MaxValue)
            return false;
        cols = (int)colValue;
        rows = (int)rowValue;
        return true;
    }

    private long EstimateGridWork(int cols, int rows)
    {
        var cells = Math.Min((long)cols * rows, 1_000_000_000L);
        var rays = Math.Max(500L, RayCount / 5L);
        var segments = Math.Max(1L, Math.Min(MaxOrder + 1L, TransitionOrder + 1L));
        var sources = Math.Max(1L, SourcePoints.Count);
        try
        {
            return checked(cells * rays * segments * sources);
        }
        catch (OverflowException)
        {
            return long.MaxValue;
        }
    }

    private static string ShortFingerprint(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "eski sürüm" : value[..Math.Min(10, value.Length)];

    private string? ValidateGridMap()
    {
        if (!double.IsFinite(GridXMin) || !double.IsFinite(GridXMax) ||
            !double.IsFinite(GridYMin) || !double.IsFinite(GridYMax) ||
            !double.IsFinite(GridPlaneZ) || !double.IsFinite(GridCellSize))
        {
            return "Izgara harita değerleri geçerli sayılar olmalıdır.";
        }

        if (GridXMax <= GridXMin || GridYMax <= GridYMin)
        {
            return "Izgara harita için maksimum sınırlar minimum sınırlardan büyük olmalıdır.";
        }

        if (GridCellSize <= 0)
        {
            return "Izgara hücre boyutu sıfırdan büyük olmalıdır.";
        }

        if (!TryGetGridDimensions(out var cols, out var rows))
        {
            return "Izgara harita boyutu hesaplanamadı.";
        }

        const int MaxGridCells = 4_000;
        if ((long)cols * rows > MaxGridCells)
        {
            return $"Izgara harita en fazla {MaxGridCells:N0} hücre olabilir; mevcut ayar {(long)cols * rows:N0} hücre üretiyor.";
        }

        const long MaxEstimatedWork = 80_000_000;
        var work = EstimateGridWork(cols, rows);
        if (work > MaxEstimatedWork)
        {
            var suggestedCellSize = GridCellSize * Math.Sqrt(work / (double)MaxEstimatedWork);
            return $"Izgara ayarı tahmini {work:N0} iş birimi üretiyor. Hücre boyutunu en az " +
                   $"{suggestedCellSize:F2} {UnitShortName} yapın veya ışın/yansıma sayısını azaltın.";
        }

        return null;
    }
}
