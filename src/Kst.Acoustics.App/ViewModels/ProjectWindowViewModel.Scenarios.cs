using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Simulation;

namespace Kst.Acoustics.App.ViewModels;

public sealed partial class ProjectWindowViewModel
{
    private AcousticScenarioState _baselineScenarioState = new();
    private readonly List<AcousticScenario> _scenarioModels = [];
    private Guid? _activeScenarioId;
    private bool _isApplyingScenarioState;

    public ObservableCollection<AcousticScenario> Scenarios { get; } = [];

    public AcousticScenario? SelectedScenario
    {
        get => _selectedScenario;
        set => SetProperty(ref _selectedScenario, value);
    }
    private AcousticScenario? _selectedScenario;

    public string ScenarioNameInput
    {
        get => _scenarioNameInput;
        set => SetProperty(ref _scenarioNameInput, value);
    }
    private string _scenarioNameInput = string.Empty;

    public string ScenarioStatusText
    {
        get => _scenarioStatusText;
        private set => SetProperty(ref _scenarioStatusText, value);
    }
    private string _scenarioStatusText = "Ana model etkin.";

    public bool IsBaselineScenarioActive => _activeScenarioId is null;
    public Guid? ActiveScenarioId => _activeScenarioId;
    public string ActiveScenarioName => _activeScenarioId is { } active
        ? _scenarioModels.FirstOrDefault(s => s.Id == active)?.Name ?? "Bilinmeyen Senaryo"
        : "Ana Model";

    public IReadOnlyList<AcousticScenarioComparisonDto> ScenarioComparisons
    {
        get => _scenarioComparisons;
        private set => SetProperty(ref _scenarioComparisons, value);
    }
    private IReadOnlyList<AcousticScenarioComparisonDto> _scenarioComparisons = [];

    public IReadOnlyList<RoomTypeCriteria> RoomTypeOptions => RoomTypeRegistry.All;

    public RoomType CurrentRoomType
    {
        get => _currentRoomType;
        set
        {
            if (!SetProperty(ref _currentRoomType, value)) return;
            OnPropertyChanged(nameof(CurrentRoomTypeCriteria));
            RefreshTargetPerformance();
            if (!_isApplyingScenarioState)
            {
                MarkDirty();
                InvalidateSimulationResult("Oda kullanım tipi değiştirildi.");
            }
        }
    }
    private RoomType _currentRoomType = RoomType.General;

    public RoomTypeCriteria CurrentRoomTypeCriteria
    {
        get => RoomTypeRegistry.All.FirstOrDefault(c => c.Type == CurrentRoomType)
            ?? RoomTypeRegistry.All[0];
        set
        {
            if (value is not null) CurrentRoomType = value.Type;
        }
    }

    public TargetPerformanceReport TargetPerformance
    {
        get => _targetPerformance;
        private set
        {
            if (!SetProperty(ref _targetPerformance, value)) return;
            OnPropertyChanged(nameof(TargetPerformanceSummary));
            OnPropertyChanged(nameof(TargetPerformanceStatusText));
            OnPropertyChanged(nameof(TargetAbsorptionRecommendation));
            OnPropertyChanged(nameof(TargetMetricRows));
        }
    }
    private TargetPerformanceReport _targetPerformance = TargetPerformanceService.Evaluate(
        RoomType.General, 0, null);

    public string TargetPerformanceSummary => TargetPerformance.Summary;
    public string TargetPerformanceStatusText => TargetPerformance.OverallStatusText;
    public string TargetAbsorptionRecommendation => TargetPerformance.AbsorptionRecommendation;
    public IReadOnlyList<TargetMetricEvaluation> TargetMetricRows => TargetPerformance.Metrics;

    private void InitScenarios(AcousticsProject project)
    {
        _baselineScenarioState = AcousticScenarioCloner.FromProject(project);
        _scenarioModels.Clear();
        _scenarioModels.AddRange(project.Scenarios.Select(AcousticScenarioCloner.Clone));
        RefreshScenarioCollection();
        _currentRoomType = project.SelectedRoomType;
        OnPropertyChanged(nameof(CurrentRoomType));
        OnPropertyChanged(nameof(CurrentRoomTypeCriteria));

        if (project.ActiveScenarioId is { } active &&
            _scenarioModels.FirstOrDefault(s => s.Id == active) is { } scenario)
        {
            _activeScenarioId = active;
            SelectedScenario = Scenarios.FirstOrDefault(s => s.Id == active);
            ApplyScenarioState(scenario.State, "Proje ile kayıtlı etkin senaryo yüklendi.", markDirty: false);
        }
        else
        {
            _activeScenarioId = null;
            ScenarioStatusText = "Ana model etkin.";
            RefreshTargetPerformance();
            RefreshScenarioComparisons();
        }

        NotifyScenarioStateProperties();
    }

    /// <summary>
    /// Belge geçmişi gibi dış snapshot geri yükleyicilerinin senaryo baseline'ını,
    /// listesini ve etkin kimliği birlikte geri almasını sağlar. History replay'de
    /// mevcut sonuç durumu aynen korunur; kullanıcı aktivasyonundaki stale kuralı
    /// burada yeniden uygulanmaz.
    /// </summary>
    internal void RestoreScenarioPersistence(
        AcousticsProject snapshot,
        bool applyWorkingState = false)
    {
        _baselineScenarioState = AcousticScenarioCloner.FromProject(snapshot);
        _scenarioModels.Clear();
        _scenarioModels.AddRange(snapshot.Scenarios.Select(AcousticScenarioCloner.Clone));
        _activeScenarioId = snapshot.ActiveScenarioId is { } active &&
            _scenarioModels.Any(s => s.Id == active)
                ? active
                : null;
        RefreshScenarioCollection(_activeScenarioId);

        if (applyWorkingState && _activeScenarioId is { } activeId &&
            _scenarioModels.FirstOrDefault(s => s.Id == activeId) is { } scenario)
        {
            ApplyScenarioState(
                scenario.State,
                $"Geçmişten '{scenario.Name}' senaryosu geri yüklendi.",
                markDirty: false,
                staleResult: false);
            return;
        }

        _currentRoomType = snapshot.SelectedRoomType;
        OnPropertyChanged(nameof(CurrentRoomType));
        OnPropertyChanged(nameof(CurrentRoomTypeCriteria));
        RefreshTargetPerformance();
        RefreshScenarioComparisons();
        NotifyScenarioStateProperties();
    }

    [RelayCommand]
    public void CreateScenario()
    {
        if (!CanLeaveCurrentScenario()) return;
        CaptureCurrentWorkingState();

        var name = UniqueScenarioName(ScenarioNameInput, "Yeni Senaryo");
        var scenario = AcousticScenarioManager.Create(name, CaptureScenarioState());
        _scenarioModels.Add(scenario);
        RefreshScenarioCollection(scenario.Id);
        ActivateScenarioCore(scenario.Id, $"'{scenario.Name}' oluşturuldu ve etkinleştirildi.");
        ScenarioNameInput = string.Empty;
    }

    [RelayCommand]
    public void DuplicateSelectedScenario()
    {
        if (SelectedScenario is null)
        {
            ScenarioStatusText = "Kopyalamak için bir senaryo seçin.";
            return;
        }
        if (!CanLeaveCurrentScenario()) return;
        CaptureCurrentWorkingState();

        var source = _scenarioModels.FirstOrDefault(s => s.Id == SelectedScenario.Id);
        if (source is null) return;
        var requested = string.IsNullOrWhiteSpace(ScenarioNameInput)
            ? $"{source.Name} Kopyası"
            : ScenarioNameInput;
        var duplicate = AcousticScenarioManager.Duplicate(
            source, UniqueScenarioName(requested, $"{source.Name} Kopyası"));
        _scenarioModels.Add(duplicate);
        RefreshScenarioCollection(duplicate.Id);
        ActivateScenarioCore(duplicate.Id, $"'{source.Name}' kopyalandı.");
        ScenarioNameInput = string.Empty;
    }

    [RelayCommand]
    public void RenameSelectedScenario()
    {
        if (SelectedScenario is null)
        {
            ScenarioStatusText = "Yeniden adlandırmak için bir senaryo seçin.";
            return;
        }
        if (string.IsNullOrWhiteSpace(ScenarioNameInput))
        {
            ScenarioStatusText = "Yeni senaryo adını yazın.";
            return;
        }

        var index = _scenarioModels.FindIndex(s => s.Id == SelectedScenario.Id);
        if (index < 0) return;
        var name = UniqueScenarioName(ScenarioNameInput, "Senaryo", SelectedScenario.Id);
        _scenarioModels[index] = AcousticScenarioManager.Rename(_scenarioModels[index], name);
        RefreshScenarioCollection(_scenarioModels[index].Id);
        ScenarioNameInput = string.Empty;
        ScenarioStatusText = $"Senaryo adı '{name}' olarak değiştirildi.";
        MarkDirty();
        RefreshScenarioComparisons();
        NotifyScenarioStateProperties();
    }

    [RelayCommand]
    public void DeleteSelectedScenario()
    {
        if (SelectedScenario is null)
        {
            ScenarioStatusText = "Silmek için bir senaryo seçin.";
            return;
        }
        if (!CanLeaveCurrentScenario()) return;

        CaptureCurrentWorkingState();
        var id = SelectedScenario.Id;
        var name = SelectedScenario.Name;
        var wasActive = _activeScenarioId == id;
        _scenarioModels.RemoveAll(s => s.Id == id);
        RefreshScenarioCollection();

        if (wasActive)
        {
            _activeScenarioId = null;
            ApplyScenarioState(_baselineScenarioState,
                $"'{name}' silindi; ana model etkinleştirildi.", markDirty: true);
        }
        else
        {
            ScenarioStatusText = $"'{name}' silindi.";
            MarkDirty();
            RefreshScenarioComparisons();
            NotifyScenarioStateProperties();
        }
    }

    [RelayCommand]
    public void ActivateSelectedScenario()
    {
        if (SelectedScenario is null)
        {
            ScenarioStatusText = "Etkinleştirmek için bir senaryo seçin.";
            return;
        }
        if (_activeScenarioId == SelectedScenario.Id)
        {
            ScenarioStatusText = $"'{SelectedScenario.Name}' zaten etkin.";
            return;
        }
        if (!CanLeaveCurrentScenario()) return;

        CaptureCurrentWorkingState();
        ActivateScenarioCore(SelectedScenario.Id, $"'{SelectedScenario.Name}' etkinleştirildi.");
    }

    [RelayCommand]
    public void ActivateBaselineScenario()
    {
        if (_activeScenarioId is null)
        {
            ScenarioStatusText = "Ana model zaten etkin.";
            return;
        }
        if (!CanLeaveCurrentScenario()) return;

        CaptureCurrentWorkingState();
        _activeScenarioId = null;
        SelectedScenario = null;
        ApplyScenarioState(_baselineScenarioState, "Ana model etkinleştirildi.", markDirty: true);
    }

    internal AcousticsProject BuildScenarioAwareProject(AcousticsProject workingProject)
    {
        var current = CaptureScenarioState();
        if (_activeScenarioId is { } active)
        {
            UpdateScenarioState(active, current, touchTimestamp: false);
            workingProject = AcousticScenarioCloner.ApplyToProject(workingProject, _baselineScenarioState);
        }
        else
        {
            _baselineScenarioState = AcousticScenarioCloner.Clone(current);
        }

        return workingProject with
        {
            Scenarios = _scenarioModels.Select(AcousticScenarioCloner.Clone).ToArray(),
            ActiveScenarioId = _activeScenarioId
        };
    }

    internal void RefreshTargetPerformance()
    {
        TargetPerformance = TargetPerformanceService.Evaluate(
            CurrentRoomType, RoomVolume, LastResult);
    }

    internal void RefreshScenarioComparisons()
    {
        var current = CaptureScenarioState();
        var scenarios = _scenarioModels.Select(AcousticScenarioCloner.Clone).ToList();
        AcousticScenarioState baseline;
        if (_activeScenarioId is { } active)
        {
            var index = scenarios.FindIndex(s => s.Id == active);
            if (index >= 0)
                scenarios[index] = AcousticScenarioManager.UpdateState(scenarios[index], current);
            baseline = _baselineScenarioState;
        }
        else
        {
            baseline = current;
        }

        var projection = AcousticScenarioCloner.ApplyToProject(_project, baseline) with
        {
            Scenarios = scenarios,
            ActiveScenarioId = _activeScenarioId
        };
        ScenarioComparisons = AcousticScenarioComparisonService.Compare(projection);
    }

    private void ActivateScenarioCore(Guid id, string message)
    {
        var scenario = _scenarioModels.FirstOrDefault(s => s.Id == id);
        if (scenario is null)
        {
            ScenarioStatusText = "Etkinleştirilecek senaryo bulunamadı.";
            return;
        }

        _activeScenarioId = id;
        RefreshScenarioCollection(id);
        ApplyScenarioState(scenario.State, message, markDirty: true);
    }

    private void ApplyScenarioState(
        AcousticScenarioState source,
        string message,
        bool markDirty,
        bool staleResult = true)
    {
        var state = AcousticScenarioCloner.Clone(source);
        _isApplyingScenarioState = true;
        try
        {
            CancelActiveSimulation();
            _workingSurfaces = state.Surfaces.ToList();
            RebuildCategoryNodes();
            ApplyCategoryMaterialSnapshot(state.CategoryMaterials);
            ApplySurfaceMaterialSnapshot(state.SurfaceMaterials);
            LoadPoints(state.Sources, state.Receivers);
            ApplyPanelUiSnapshot(state.PanelKonfigurasyon);
            RestoreSimulationSettings(state.SimulationSettings);

            _volumeInitialized = false;
            RoomVolume = state.RoomVolume;
            _roomVolumeOrigin = state.VolumeOrigin ?? RoomVolumeOrigin.BoundingBoxEstimate;
            RoomVolumeSourceText = BuildRoomVolumeSourceText(_roomVolumeOrigin);
            _volumeInitialized = true;
            CurrentRoomType = state.SelectedRoomType;

            if (state.LastResult is { } storedResult)
            {
                var result = AcousticScenarioCloner.CloneSimulationResult(storedResult)!;
                if (staleResult)
                {
                    result.IsStale = true;
                    result.StaleReason = "Senaryo etkinleştirildi; sonuçları bu çalışma durumunda yeniden doğrulayın.";
                }
                OnSimulationCompleted(result);
            }
            else
            {
                ClearSimulationResults("Bu senaryo için simülasyon sonucu yok.");
            }

            HighlightedSurfaceId = null;
            SelectedSurfaceIds = [];
            OnPropertyChanged(nameof(ProjectSurfaces));
            NotifyViewportPerformanceChanged();
            RefreshMaterialStatus();
            RefreshSabineCalculation();
            RefreshTargetPerformance();
            ScenarioStatusText = message;
        }
        finally
        {
            _isApplyingScenarioState = false;
        }

        Interlocked.Increment(ref _simulationInputRevision);
        if (markDirty) MarkDirty();
        RefreshScenarioComparisons();
        NotifyScenarioStateProperties();
    }

    private AcousticScenarioState CaptureScenarioState() =>
        AcousticScenarioCloner.Clone(new AcousticScenarioState
        {
            Surfaces = _workingSurfaces,
            Sources = SourcePoints,
            Receivers = ReceiverPoints,
            CategoryMaterials = BuildCategoryMaterialMap(),
            SurfaceMaterials = BuildSurfaceMaterialMap(),
            PanelKonfigurasyon = PanelKonfigurasyon,
            SimulationSettings = BuildStoredSimulationSettings(),
            LastResult = LastResult,
            SelectedRoomType = CurrentRoomType,
            RoomVolume = RoomVolume,
            VolumeOrigin = VolumeOrigin
        });

    private void CaptureCurrentWorkingState()
    {
        var state = CaptureScenarioState();
        if (_activeScenarioId is { } active)
            UpdateScenarioState(active, state);
        else
            _baselineScenarioState = AcousticScenarioCloner.Clone(state);
    }

    private void UpdateScenarioState(Guid id, AcousticScenarioState state, bool touchTimestamp = true)
    {
        var index = _scenarioModels.FindIndex(s => s.Id == id);
        if (index >= 0)
            _scenarioModels[index] = touchTimestamp
                ? AcousticScenarioManager.UpdateState(_scenarioModels[index], state)
                : _scenarioModels[index] with { State = AcousticScenarioCloner.Clone(state) };
    }

    private bool CanLeaveCurrentScenario()
    {
        if (!HasInvalidPoints) return true;
        ScenarioStatusText = "Geçersiz kaynak/alıcı satırları düzeltilmeden senaryo değiştirilemez.";
        StatusMessage = ScenarioStatusText;
        return false;
    }

    private void ApplyCategoryMaterialSnapshot(IReadOnlyDictionary<string, string> map)
    {
        CategoryMaterialRows.Clear();
        foreach (var node in CategoryNodes)
        {
            map.TryGetValue(node.Name, out var materialId);
            var resolved = !string.IsNullOrWhiteSpace(materialId)
                ? MaterialDatabase.Instance.FindById(materialId) ?? MaterialLibrary.FindById(materialId)
                : null;
            var row = new CategoryMaterialViewModel(
                node.Name,
                node.Count,
                resolved ?? MaterialLibrary.Default,
                unresolvedMaterialId: materialId is not null && resolved is null ? materialId : null);
            row.PropertyChanged += (sender, args) =>
            {
                RefreshMaterialStatus();
                if (args.PropertyName == nameof(CategoryMaterialViewModel.SelectedMaterial) &&
                    sender is CategoryMaterialViewModel changed)
                    OnCategoryMaterialChanged(changed.Category);
            };
            CategoryMaterialRows.Add(row);
        }
    }

    private void ApplySurfaceMaterialSnapshot(IReadOnlyDictionary<string, string> map)
    {
        _surfaceMaterialOverrides.Clear();
        foreach (var (key, value) in map)
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                _surfaceMaterialOverrides[key] = value;
        RebuildSurfaceMaterialRows();
    }

    private void ApplyPanelUiSnapshot(AcousticPanelKonfigurasyon? source)
    {
        PanelKonfigurasyon = ClonePanelConfiguration(source);
        PanelListesi.Clear();
        if (PanelKonfigurasyon is null)
        {
            TavanPanelAktif = false;
            SolDuvarPanelAktif = false;
            SagDuvarPanelAktif = false;
            DikeyDuvarPanelAktif = false;
            return;
        }

        PanelEniM = PanelKonfigurasyon.PanelEniM;
        SiraliPanelEniAktif = PanelKonfigurasyon.PanelEniModu == PanelEniModu.Sirali;
        SetPanelEniRows(PanelKonfigurasyon.PanelEnleriM.Count > 0
            ? PanelKonfigurasyon.PanelEnleriM
            : [PanelKonfigurasyon.PanelEniM]);
        SarkmaCikmaM = PanelKonfigurasyon.SarkmaCikmaM;
        TavanPanelAktif = PanelKonfigurasyon.AktifTipler.Contains(PanelTipi.Tavan);
        SolDuvarPanelAktif = PanelKonfigurasyon.AktifTipler.Contains(PanelTipi.DuvarSol);
        SagDuvarPanelAktif = PanelKonfigurasyon.AktifTipler.Contains(PanelTipi.DuvarSag);
        DikeyDuvarPanelAktif = SolDuvarPanelAktif || SagDuvarPanelAktif;
        SeciliSahneOnu = PanelKonfigurasyon.SahneOnuYuzeyId;
        SeciliTavanYuzey = PanelKonfigurasyon.TavanYuzeyId ?? HedefYuzeyOtomatikEtiket;
        SeciliSolDuvarYuzey = PanelKonfigurasyon.SolDuvarYuzeyId ?? HedefYuzeyOtomatikEtiket;
        SeciliSagDuvarYuzey = PanelKonfigurasyon.SagDuvarYuzeyId ?? HedefYuzeyOtomatikEtiket;
        foreach (var panel in PanelKonfigurasyon.Paneller)
            AddPanelRow(panel);
    }

    private void RefreshScenarioCollection(Guid? selectedId = null)
    {
        selectedId ??= SelectedScenario?.Id;
        Scenarios.Clear();
        foreach (var scenario in _scenarioModels.OrderBy(s => s.CreatedAtUtc))
            Scenarios.Add(scenario);
        SelectedScenario = selectedId is { } id
            ? Scenarios.FirstOrDefault(s => s.Id == id)
            : null;
    }

    private string UniqueScenarioName(string? requested, string fallback, Guid? exceptId = null)
    {
        var baseName = string.IsNullOrWhiteSpace(requested) ? fallback : requested.Trim();
        var candidate = baseName;
        var suffix = 2;
        while (_scenarioModels.Any(s => s.Id != exceptId &&
               string.Equals(s.Name, candidate, StringComparison.OrdinalIgnoreCase)))
            candidate = $"{baseName} {suffix++}";
        return candidate;
    }

    private void NotifyScenarioStateProperties()
    {
        OnPropertyChanged(nameof(IsBaselineScenarioActive));
        OnPropertyChanged(nameof(ActiveScenarioId));
        OnPropertyChanged(nameof(ActiveScenarioName));
    }
}
