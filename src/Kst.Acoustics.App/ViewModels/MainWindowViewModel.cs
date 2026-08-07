using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Kst.Acoustics.App.Controls;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Importing;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Validation;

namespace Kst.Acoustics.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly SurfaceImportService _importService = new();
    private readonly ProjectFileService _projectFileService = new();
    private readonly LeakTestService _leakTestService = new();
    private AcousticsProject? _currentProject;
    private string? _savedProjectPath;
    private string? _backupPreservationPath;
    private long _revision;
    private long _savedRevision;
    private bool _suppressDirtyTracking;

    public MainWindowViewModel()
    {
        CategorySummaries = [];
        SurfacePreviews = [];
        SelectedSurfaces = [];
        UnitOptions =
        [
            new UnitOptionViewModel(SurfaceImportUnit.Meters, "Metre"),
            new UnitOptionViewModel(SurfaceImportUnit.Centimeters, "Santimetre"),
            new UnitOptionViewModel(SurfaceImportUnit.Millimeters, "Milimetre"),
            new UnitOptionViewModel(SurfaceImportUnit.Inches, "Inch"),
            new UnitOptionViewModel(SurfaceImportUnit.Feet, "Feet")
        ];
        TargetUnitOption = UnitOptions[0];
        SelectedRoomType = RoomTypeRegistry.All[0];
        ReloadLoudspeakerOptions();
        StatusMessage = "DXF dosyası seçerek içe aktarma önizlemesini görebilirsiniz.";
    }

    // --- Collections ---

    public ObservableCollection<CategorySummaryViewModel> CategorySummaries { get; }

    public ObservableCollection<SurfacePreviewViewModel> SurfacePreviews { get; }

    public ObservableCollection<SurfacePreviewViewModel> VisibleSurfacePreviews { get; } = [];

    public ObservableCollection<SurfacePreviewViewModel> SelectedSurfaces { get; }

    public IReadOnlyList<UnitOptionViewModel> UnitOptions { get; }

    public IReadOnlyList<GeometryProjectionMode> ProjectionModes { get; } =
    [
        GeometryProjectionMode.Top2D,
        GeometryProjectionMode.Front2D,
        GeometryProjectionMode.Isometric3D
    ];

    // --- Project State ---

    public IReadOnlyList<ProjectSurface> ProjectSurfaces
    {
        get => _projectSurfaces;
        private set => SetProperty(ref _projectSurfaces, value);
    }
    private IReadOnlyList<ProjectSurface> _projectSurfaces = [];

    public IReadOnlyList<ProjectSurface> VisibleProjectSurfaces
    {
        get => _visibleProjectSurfaces;
        private set => SetProperty(ref _visibleProjectSurfaces, value);
    }
    private IReadOnlyList<ProjectSurface> _visibleProjectSurfaces = [];

    // --- UI State ---

    public GeometryProjectionMode LeftProjectionMode
    {
        get => _leftProjectionMode;
        set => SetProperty(ref _leftProjectionMode, value);
    }
    private GeometryProjectionMode _leftProjectionMode = GeometryProjectionMode.Front2D;

    public GeometryProjectionMode RightProjectionMode
    {
        get => _rightProjectionMode;
        set => SetProperty(ref _rightProjectionMode, value);
    }
    private GeometryProjectionMode _rightProjectionMode = GeometryProjectionMode.Isometric3D;

    public string ProjectName
    {
        get => _projectName;
        set
        {
            if (SetProperty(ref _projectName, value))
            {
                MarkDirty();
            }
        }
    }
    private string _projectName = "Yeni Proje";

    public string SelectedFilePath
    {
        get => _selectedFilePath;
        private set => SetProperty(ref _selectedFilePath, value);
    }
    private string _selectedFilePath = "Dosya seçilmedi";

    public string ImporterName
    {
        get => _importerName;
        private set => SetProperty(ref _importerName, value);
    }
    private string _importerName = "-";

    public string UnitName
    {
        get => _unitName;
        private set => SetProperty(ref _unitName, value);
    }
    private string _unitName = "-";

    public string UnitShortName
    {
        get => _unitShortName;
        private set => SetProperty(ref _unitShortName, value);
    }
    private string _unitShortName = "m";

    public string ProjectSizeSummary
    {
        get => _projectSizeSummary;
        private set => SetProperty(ref _projectSizeSummary, value);
    }
    private string _projectSizeSummary = "-";

    public int SurfaceCount
    {
        get => _surfaceCount;
        private set
        {
            if (SetProperty(ref _surfaceCount, value))
            {
                OnPropertyChanged(nameof(CanStartProject));
            }
        }
    }
    private int _surfaceCount;

    public bool CanStartProject => _surfaceCount > 0;

    public AcousticsProject? BuildCurrentProject()
    {
        if (_currentProject is null) return null;
        try
        {
            return BuildProjectSnapshot();
        }
        catch (InvalidDataException ex)
        {
            StatusMessage = ex.Message;
            return null;
        }
    }

    public int CategoryCount
    {
        get => _categoryCount;
        private set => SetProperty(ref _categoryCount, value);
    }
    private int _categoryCount;

    public UnitOptionViewModel? TargetUnitOption
    {
        get => _targetUnitOption;
        set => SetProperty(ref _targetUnitOption, value);
    }
    private UnitOptionViewModel? _targetUnitOption;

    public string CustomScaleFactor
    {
        get => _customScaleFactor;
        set => SetProperty(ref _customScaleFactor, value);
    }
    private string _customScaleFactor = "1.0";

    public IReadOnlyList<RoomTypeCriteria> RoomTypes => RoomTypeRegistry.All;

    public RoomTypeCriteria SelectedRoomType
    {
        get => _selectedRoomType;
        set
        {
            // _currentProject henüz atanmamışsa (ör. yapıcı içindeki ilk varsayılan atama)
            // dirty işaretlenmez; proje yüklüyken yapılan gerçek değişiklikler işaretlenir.
            if (SetProperty(ref _selectedRoomType, value) && _currentProject is not null)
                MarkDirty();
        }
    }
    private RoomTypeCriteria _selectedRoomType = RoomTypeRegistry.All[0];

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }
    private string _statusMessage = string.Empty;

    // --- Dirty Flag (4.5) ---

    public bool IsDirty
    {
        get => _isDirty;
        private set => SetProperty(ref _isDirty, value);
    }
    private bool _isDirty;

    public string? SavedProjectPath => _savedProjectPath;
    internal bool RecoveredFromBackup => _backupPreservationPath is not null;

    private void MarkDirty()
    {
        if (_suppressDirtyTracking) return;
        _revision++;
        IsDirty = true;
    }

    private void MarkClean()
    {
        _savedRevision = _revision;
        IsDirty = false;
    }

    private void InvalidateStoredSimulationResult(string reason)
    {
        if (_currentProject?.LastResult is not { } result) return;
        _currentProject = _currentProject with
        {
            LastResult = CreateStaleSimulationResult(result, reason)
        };
    }

    private static Kst.Acoustics.Core.Simulation.SimulationResult CreateStaleSimulationResult(
        Kst.Acoustics.Core.Simulation.SimulationResult result,
        string reason) => new()
    {
        Echograms = result.Echograms,
        Parameters = result.Parameters,
        Elapsed = result.Elapsed,
        ComputedAt = result.ComputedAt,
        GridMap = result.GridMap,
        Reliability = result.Reliability,
        ComputedParameters = result.ComputedParameters,
        InputFingerprint = result.InputFingerprint,
        IsStale = true,
        StaleReason = reason
    };

    // --- Surface Search (4.7) ---

    public string SurfaceSearchText
    {
        get => _surfaceSearchText;
        set
        {
            if (SetProperty(ref _surfaceSearchText, value))
            {
                ApplyVisibilityFilter();
            }
        }
    }
    private string _surfaceSearchText = string.Empty;

    // --- Selection ---

    public SurfacePreviewViewModel? SelectedSurface
    {
        get => _selectedSurface;
        set
        {
            if (SetProperty(ref _selectedSurface, value))
            {
                HighlightedSurfaceId = value?.Id;
                if (value is not null)
                {
                    NewCategoryName = value.Category;
                }
            }
        }
    }
    private SurfacePreviewViewModel? _selectedSurface;

    public int? HighlightedSurfaceId
    {
        get => _highlightedSurfaceId;
        set
        {
            if (!SetProperty(ref _highlightedSurfaceId, value))
            {
                return;
            }

            var matchedSurface = value.HasValue
                ? SurfacePreviews.FirstOrDefault(x => x.Id == value.Value)
                : null;

            if (!ReferenceEquals(_selectedSurface, matchedSurface))
            {
                _selectedSurface = matchedSurface;
                OnPropertyChanged(nameof(SelectedSurface));

                if (matchedSurface is not null)
                {
                    NewCategoryName = matchedSurface.Category;
                    ReplaceSelection(matchedSurface);
                }
            }
        }
    }
    private int? _highlightedSurfaceId;

    public IReadOnlyList<int> SelectedSurfaceIds => SelectedSurfaces.Select(x => x.Id).ToArray();

    public CategorySummaryViewModel? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value) && value is not null)
            {
                BatchTargetCategoryName = value.Name;
            }
        }
    }
    private CategorySummaryViewModel? _selectedCategory;

    public string NewCategoryName
    {
        get => _newCategoryName;
        set => SetProperty(ref _newCategoryName, value);
    }
    private string _newCategoryName = string.Empty;

    public string BatchTargetCategoryName
    {
        get => _batchTargetCategoryName;
        set => SetProperty(ref _batchTargetCategoryName, value);
    }
    private string _batchTargetCategoryName = string.Empty;

    // --- İçe Aktarma Denetim Raporu ---

    /// <summary>DXF import sırasında sadeleştirilen/atlanan geometri uyarıları — kalıcı panelde gösterilir.</summary>
    public IReadOnlyList<string> ImportWarnings
    {
        get => _importWarnings;
        private set => SetProperty(ref _importWarnings, value);
    }
    private IReadOnlyList<string> _importWarnings = [];

    private IReadOnlyList<string> _sessionWarnings = [];
    internal IReadOnlyList<string> SessionWarnings => _sessionWarnings;

    public string ImportWarningsSummary
    {
        get => _importWarningsSummary;
        private set => SetProperty(ref _importWarningsSummary, value);
    }
    private string _importWarningsSummary = string.Empty;

    private void SetImportWarnings(IReadOnlyList<string>? warnings)
    {
        ImportWarnings = warnings ?? [];
        ImportWarningsSummary = ImportWarnings.Count == 0
            ? string.Empty
            : $"⚠ {ImportWarnings.Count} geometri uyarısı:\n" + string.Join("\n", ImportWarnings.Select(w => $"- {w}"));
    }

    private void ApplySessionWarnings(IReadOnlyList<string>? warnings)
    {
        _sessionWarnings = warnings?.Where(w => !string.IsNullOrWhiteSpace(w)).ToArray() ?? [];
        SetImportWarnings((_currentProject?.ImportWarnings ?? [])
            .Concat(_sessionWarnings)
            .Distinct(StringComparer.Ordinal)
            .ToArray());
    }

    // --- Leak Test ---

    public string LeakTestSummary
    {
        get => _leakTestSummary;
        private set => SetProperty(ref _leakTestSummary, value);
    }
    private string _leakTestSummary = "Sızdırmazlık testi henüz çalıştırılmadı.";

    public IReadOnlyList<Point3D> LeakTestPath
    {
        get => _leakTestPath;
        private set => SetProperty(ref _leakTestPath, value);
    }
    private IReadOnlyList<Point3D> _leakTestPath = [];

    // --- Category Filter ---

    public ObservableCollection<string> CategoryFilterOptions { get; } = [];

    public string SelectedCategoryFilter
    {
        get => _selectedCategoryFilter;
        set
        {
            if (SetProperty(ref _selectedCategoryFilter, value))
            {
                ApplyVisibilityFilter();
            }
        }
    }
    private string _selectedCategoryFilter = "Tüm Kategoriler";

    // --- Public Methods ---

    public void ReplaceSelectionFromList(IReadOnlyList<SurfacePreviewViewModel> surfaces)
    {
        SelectedSurfaces.Clear();

        foreach (var surface in surfaces)
        {
            SelectedSurfaces.Add(surface);
        }

        OnPropertyChanged(nameof(SelectedSurfaceIds));

        var first = SelectedSurfaces.FirstOrDefault();
        if (!ReferenceEquals(_selectedSurface, first))
        {
            _selectedSurface = first;
            OnPropertyChanged(nameof(SelectedSurface));
        }

        HighlightedSurfaceId = first?.Id;
    }

    public void UpdateProject(AcousticsProject project)
    {
        ApplyProject(project, project.ImportFormat, markDirty: false, savedProjectPath: _savedProjectPath);
    }

    public void UpdateProject(
        AcousticsProject project,
        bool isDirty,
        string? savedProjectPath,
        bool recoveredFromBackup = false) =>
        ApplyProject(project, project.ImportFormat, isDirty, savedProjectPath, recoveredFromBackup);

    // --- Internal Helpers ---

    private void ApplyProject(
        AcousticsProject project,
        SurfaceImportFormat format,
        bool markDirty = false,
        string? savedProjectPath = null,
        bool recoveredFromBackup = false)
    {
        _suppressDirtyTracking = true;
        try
        {
            _currentProject = project;
            _savedProjectPath = savedProjectPath;
            _backupPreservationPath = recoveredFromBackup && !string.IsNullOrWhiteSpace(savedProjectPath)
                ? Path.GetFullPath(savedProjectPath)
                : null;
            SelectedFilePath = savedProjectPath
                ?? project.SourceFilePath
                ?? "Dosya seçilmedi";
            ProjectName = project.Name;
            ImporterName = format.ToString();
            UnitName = FormatUnitName(project.Unit);
            UnitShortName = FormatUnitShortName(project.Unit);
            SurfaceCount = project.Surfaces.Count;
            ProjectSurfaces = project.Surfaces;
            ProjectSizeSummary = BuildProjectSizeSummary(project.Surfaces, project.Unit);
            TargetUnitOption = UnitOptions.FirstOrDefault(x => x.Value == project.Unit) ?? UnitOptions[0];
            SelectedRoomType = RoomTypeRegistry.All.FirstOrDefault(x => x.Type == project.SelectedRoomType) ?? RoomTypeRegistry.All[0];

            SurfacePreviews.Clear();
            foreach (var surface in project.Surfaces)
            {
                SurfacePreviews.Add(new SurfacePreviewViewModel
                {
                    Id = surface.Id,
                    Name = surface.Name,
                    Category = surface.Category,
                    VertexCount = surface.Vertices.Count,
                    IsVisible = true
                });
            }

            SelectedSurface = null;
            SelectedCategory = null;
            HighlightedSurfaceId = null;
            SelectedSurfaces.Clear();
            OnPropertyChanged(nameof(SelectedSurfaceIds));
            NewCategoryName = string.Empty;
            BatchTargetCategoryName = string.Empty;
            SurfaceSearchText = string.Empty;
            LeakTestSummary = "Sızdırmazlık testi henüz çalıştırılmadı.";
            LeakTestPath = [];
            _sessionWarnings = [];
            SetImportWarnings(project.ImportWarnings);
            RebuildCategorySummaries();
            RefreshCategoryFilters();
            ApplyVisibilityFilter();
            UpdateModelZRangeHint(project.Surfaces, project.Unit);
            LoadPoints(project.Sources, project.Receivers);
        }
        finally
        {
            _suppressDirtyTracking = false;
        }

        if (markDirty) MarkDirty();
        else MarkClean();
    }

    private void ClearPreview()
    {
        _suppressDirtyTracking = true;
        try
        {
        _currentProject = null;
        _savedProjectPath = null;
        _backupPreservationPath = null;
        ProjectSurfaces = [];
        VisibleProjectSurfaces = [];
        ProjectName = "Yeni Proje";
        ImporterName = "-";
        UnitName = "-";
        UnitShortName = "m";
        ProjectSizeSummary = "-";
        SurfaceCount = 0;
        CategoryCount = 0;
        CategorySummaries.Clear();
        SurfacePreviews.Clear();
        SelectedSurface = null;
        SelectedCategory = null;
        HighlightedSurfaceId = null;
        SelectedSurfaces.Clear();
        OnPropertyChanged(nameof(SelectedSurfaceIds));
        VisibleSurfacePreviews.Clear();
        CategoryFilterOptions.Clear();
        SelectedCategoryFilter = "Tüm Kategoriler";
        NewCategoryName = string.Empty;
        BatchTargetCategoryName = string.Empty;
        SurfaceSearchText = string.Empty;
        LeakTestSummary = "Sızdırmazlık testi henüz çalıştırılmadı.";
        LeakTestPath = [];
        _sessionWarnings = [];
        SetImportWarnings([]);
        ClearPoints();
        }
        finally
        {
            _suppressDirtyTracking = false;
        }
        _revision = 0;
        _savedRevision = 0;
        MarkClean();
    }

    private void ReplaceSelection(SurfacePreviewViewModel surface)
    {
        if (SelectedSurfaces.Count == 1 && ReferenceEquals(SelectedSurfaces[0], surface))
        {
            OnPropertyChanged(nameof(SelectedSurfaceIds));
            return;
        }

        SelectedSurfaces.Clear();
        SelectedSurfaces.Add(surface);
        OnPropertyChanged(nameof(SelectedSurfaceIds));
    }

    private AcousticsProject BuildProjectSnapshot()
    {
        var vertexLookup = ProjectSurfaces.ToDictionary(x => x.Id);

        var surfaces = SurfacePreviews
            .Select(surface => vertexLookup.TryGetValue(surface.Id, out var projectSurface)
                ? projectSurface with
                {
                    Name = surface.Name,
                    Category = surface.Category,
                    Vertices = projectSurface.Vertices.ToArray()
                }
                : new ProjectSurface(surface.Id, surface.Name, surface.Category, []))
            .ToArray();

        if (_currentProject is null) return ProjectFactory.CreateEmpty(ProjectName.Trim());

        return _currentProject with
        {
            Name = ProjectName.Trim(),
            Surfaces = surfaces,
            Sources = BuildSourceSnapshot(),
            Receivers = BuildReceiverSnapshot(),
            SelectedRoomType = SelectedRoomType.Type
        };
    }

    private bool ApplyTransformedSurfaces(
        IReadOnlyList<ProjectSurface> surfaces,
        SurfaceImportUnit unit,
        double factor,
        bool scalePhysicalVolume)
    {
        ArgumentNullException.ThrowIfNull(surfaces);

        // Koordinatlar her iki işlemde de ölçeklenir. Oda hacmi ise daima m³'tür:
        // salt birim dönüşümünde sabit kalır, yalnız gerçek fiziksel ölçeklemede factor³
        // değişir.
        var scaledSources = ProjectScalingService.ScalePoints(BuildSourceSnapshot(), factor);
        var scaledReceivers = ProjectScalingService.ScalePoints(BuildReceiverSnapshot(), factor);
        var currentVolume   = _currentProject?.RoomVolume ?? 0;
        var scaledVolume    = scalePhysicalVolume
            ? ProjectScalingService.ScaleVolume(currentVolume, factor)
            : currentVolume;

        // Dönüşümün her parçasını ayrı bir aday snapshot üzerinde tamamla. Yüzey,
        // nokta, panel, ızgara veya hacimde taşma oluşursa canlı UI/model durumuna
        // hiçbir atama yapılmaz.
        var snapshot = BuildProjectSnapshot();
        var staleReason = scalePhysicalVolume
            ? "Proje fiziksel olarak ölçeklendi."
            : "Proje birimi dönüştürüldü.";
        var candidate = snapshot with
        {
            Unit = unit,
            Surfaces = surfaces.ToArray(),
            Sources = scaledSources,
            Receivers = scaledReceivers,
            RoomVolume = scaledVolume,
            PanelKonfigurasyon = ScalePanelConfiguration(
                snapshot.PanelKonfigurasyon, factor, scalePhysicalVolume),
            SimulationSettings = ScaleSimulationSettings(
                snapshot.SimulationSettings ?? snapshot.LastResult?.Parameters,
                factor,
                unit,
                scalePhysicalVolume),
            LastResult = snapshot.LastResult is { } result
                ? CreateStaleSimulationResult(result, staleReason)
                : null
        };
        var validationIssues = AcousticsProjectValidator.Validate(candidate);
        var firstError = validationIssues.FirstOrDefault(
            issue => issue.Severity == ProjectValidationSeverity.Error);
        if (firstError != default)
        {
            StatusMessage = $"Dönüşüm uygulanamadı; proje değiştirilmedi. {firstError.Message}";
            return false;
        }

        ProjectSurfaces = surfaces.ToArray();
        UnitName = FormatUnitName(unit);
        UnitShortName = FormatUnitShortName(unit);
        SurfaceCount = ProjectSurfaces.Count;
        ProjectSizeSummary = BuildProjectSizeSummary(ProjectSurfaces, unit);
        LeakTestSummary = "Sızdırmazlık testi henüz çalıştırılmadı.";
        LeakTestPath = [];

        _currentProject = candidate;

        LoadPoints(scaledSources, scaledReceivers);
        UpdateModelZRangeHint(ProjectSurfaces, unit);
        ApplyVisibilityFilter();
        return true;
    }

    private static AcousticPanelKonfigurasyon? ScalePanelConfiguration(
        AcousticPanelKonfigurasyon? source,
        double coordinateFactor,
        bool scalePhysicalDimensions)
    {
        if (source is null) return null;
        var physicalFactor = scalePhysicalDimensions ? coordinateFactor : 1.0;
        return new AcousticPanelKonfigurasyon
        {
            OturumId = source.OturumId,
            SahneOnuYuzeyId = source.SahneOnuYuzeyId,
            PanelEniM = source.PanelEniM * physicalFactor,
            PanelEniModu = source.PanelEniModu,
            PanelEnleriM = (source.PanelEnleriM ?? [])
                .Select(value => value * physicalFactor)
                .ToList(),
            SarkmaCikmaM = source.SarkmaCikmaM * physicalFactor,
            AktifTipler = source.AktifTipler?.ToArray() ?? [],
            TavanYuzeyId = source.TavanYuzeyId,
            SolDuvarYuzeyId = source.SolDuvarYuzeyId,
            SagDuvarYuzeyId = source.SagDuvarYuzeyId,
            Paneller = (source.Paneller ?? []).Select(panel => new AcousticPanel
            {
                Id = panel.Id,
                Tip = panel.Tip,
                DilimNo = panel.DilimNo,
                PanelEniM = panel.PanelEniM * physicalFactor,
                SarkmaCikmaM = panel.SarkmaCikmaM * physicalFactor,
                AciDerece = panel.AciDerece,
                MaterialId = panel.MaterialId,
                Vertices = (panel.Vertices ?? [])
                    .Select(v => new Point3D(
                        v.X * coordinateFactor,
                        v.Y * coordinateFactor,
                        v.Z * coordinateFactor))
                    .ToList()
            }).ToList()
        };
    }

    private static Kst.Acoustics.Core.Simulation.SimulationParameters? ScaleSimulationSettings(
        Kst.Acoustics.Core.Simulation.SimulationParameters? settings,
        double coordinateFactor,
        SurfaceImportUnit targetUnit,
        bool scalePhysicalDimensions)
    {
        if (settings is null) return null;
        var grid = settings.ReceiverGrid;
        var scaledGrid = grid is null
            ? null
            : new ReceiverGrid(
                grid.PlaneZ * coordinateFactor,
                grid.XMin * coordinateFactor,
                grid.XMax * coordinateFactor,
                grid.YMin * coordinateFactor,
                grid.YMax * coordinateFactor,
                grid.CellSize * coordinateFactor);

        return settings with
        {
            ReceiverRadius = settings.ReceiverRadius *
                (scalePhysicalDimensions ? 1.0 : coordinateFactor),
            ReceiverGrid = scaledGrid,
            UnitScale = ProjectScalingService.GetMetersPerUnit(targetUnit),
            InputFingerprint = null
        };
    }

    private static string BuildProjectSizeSummary(IReadOnlyList<ProjectSurface> surfaces, SurfaceImportUnit unit)
    {
        if (surfaces.Count == 0)
        {
            return "-";
        }

        var points = surfaces.SelectMany(x => x.Vertices)
            .Where(p => double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z))
            .ToArray();
        if (points.Length == 0)
        {
            return "-";
        }

        var minX = points.Min(x => x.X);
        var maxX = points.Max(x => x.X);
        var minY = points.Min(x => x.Y);
        var maxY = points.Max(x => x.Y);
        var minZ = points.Min(x => x.Z);
        var maxZ = points.Max(x => x.Z);

        var width = maxX - minX;
        var depth = maxY - minY;
        var height = maxZ - minZ;
        var unitLabel = FormatUnitShortName(unit);

        return $"Genişlik: {FormatDimension(width)} {unitLabel}   Derinlik: {FormatDimension(depth)} {unitLabel}   Yükseklik: {FormatDimension(height)} {unitLabel}";
    }

    private static string FormatDimension(double value)
    {
        return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string FormatUnitName(SurfaceImportUnit unit)
    {
        return unit switch
        {
            SurfaceImportUnit.Meters => "Metre",
            SurfaceImportUnit.Centimeters => "Santimetre",
            SurfaceImportUnit.Millimeters => "Milimetre",
            SurfaceImportUnit.Inches => "Inch",
            SurfaceImportUnit.Feet => "Feet",
            _ => unit.ToString()
        };
    }

    private static string FormatUnitShortName(SurfaceImportUnit unit)
    {
        return unit switch
        {
            SurfaceImportUnit.Meters => "m",
            SurfaceImportUnit.Centimeters => "cm",
            SurfaceImportUnit.Millimeters => "mm",
            SurfaceImportUnit.Inches => "in",
            SurfaceImportUnit.Feet => "ft",
            _ => "m"
        };
    }

    private void RebuildCategorySummaries()
    {
        var categoryGroups = SurfacePreviews
            .GroupBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.Count())
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => new CategorySummaryViewModel
            {
                Name = x.Key,
                SurfaceCount = x.Count()
            })
            .ToArray();

        CategoryCount = categoryGroups.Length;

        CategorySummaries.Clear();
        foreach (var item in categoryGroups)
        {
            CategorySummaries.Add(item);
        }
    }

    private void RefreshCategoryFilters()
    {
        var current = SelectedCategoryFilter;
        var categories = SurfacePreviews
            .Select(x => x.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        CategoryFilterOptions.Clear();
        CategoryFilterOptions.Add("Tüm Kategoriler");
        foreach (var category in categories)
        {
            CategoryFilterOptions.Add(category);
        }

        SelectedCategoryFilter = CategoryFilterOptions.Contains(current) ? current : "Tüm Kategoriler";
    }

    private void ApplyVisibilityFilter()
    {
        var searchText = SurfaceSearchText?.Trim() ?? string.Empty;
        var hasSearch = !string.IsNullOrEmpty(searchText);

        var visiblePreviews = SurfacePreviews
            .Where(x => x.IsVisible)
            .Where(x => SelectedCategoryFilter == "Tüm Kategoriler" || string.Equals(x.Category, SelectedCategoryFilter, StringComparison.OrdinalIgnoreCase))
            .Where(x => !hasSearch || MatchesSearch(x, searchText))
            .ToArray();

        VisibleSurfacePreviews.Clear();
        foreach (var surface in visiblePreviews)
        {
            VisibleSurfacePreviews.Add(surface);
        }

        var visibleIds = visiblePreviews.Select(x => x.Id).ToHashSet();
        VisibleProjectSurfaces = ProjectSurfaces.Where(x => visibleIds.Contains(x.Id)).ToArray();
        OnPropertyChanged(nameof(SelectedSurfaceIds));
    }

    private static bool MatchesSearch(SurfacePreviewViewModel surface, string searchText)
    {
        if (surface.Id.ToString().Contains(searchText, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (surface.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (surface.Category.Contains(searchText, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
