using System.Collections.ObjectModel;
using Avalonia.Threading;
using Kst.Acoustics.App.Controls;
using Kst.Acoustics.Core;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Importing;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.App.ViewModels;

public sealed partial class ProjectWindowViewModel : ViewModelBase, IDisposable
{
    private readonly AcousticsProject _project;
    private readonly IReadOnlyList<string> _sessionWarnings;

    public ProjectWindowViewModel(AcousticsProject project, IReadOnlyList<string>? sessionWarnings = null)
    {
        _project = project;
        _sessionWarnings = sessionWarnings?
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];
        ProjectName   = project.Name;
        UnitShortName = FormatUnit(project.Unit);

        var initialNodes = project.Surfaces
            .GroupBy(s => s.Category, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var surfaces = g.Select(s => new SurfaceNodeViewModel(s.Id, s.Name, SelectSurfaceById));
                var node     = new CategoryNodeViewModel(g.Key, surfaces);
                node.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(CategoryNodeViewModel.IsVisible))
                        OnPropertyChanged(nameof(VisibleSurfaces));
                };
                return node;
            })
            .ToArray();

        CategoryNodes = initialNodes;

        _originalSurfaces = project.Surfaces;
        _workingSurfaces  = project.Surfaces.ToList();
        LoadPoints(project.Sources, project.Receivers);

        // Materyal satırları — her kategori için bir VM
        CategoryMaterialRows = new ObservableCollection<CategoryMaterialViewModel>(
            CategoryNodes.Select(node =>
            {
                project.CategoryMaterials.TryGetValue(node.Name, out var matId);
                var resolved = matId is not null ? MaterialDatabase.Instance.FindById(matId) : null;
                var material = resolved ?? MaterialLibrary.Default;
                var row = new CategoryMaterialViewModel(
                    node.Name, node.Count, material,
                    unresolvedMaterialId: matId is not null && resolved is null ? matId : null);
                row.PropertyChanged += (s, e) =>
                {
                    RefreshMaterialStatus();
                    if (e.PropertyName == nameof(CategoryMaterialViewModel.SelectedMaterial)
                        && s is CategoryMaterialViewModel changed)
                        OnCategoryMaterialChanged(changed.Category);
                };
                return row;
            }));

        RefreshMaterialStatus();
        InitLightTracingOptions();
        RestoreSimulationSettings(project.SimulationSettings ?? project.LastResult?.Parameters);

        // Başlangıç hacmi: projede kayıtlı değer varsa (kullanıcı override'ı dahil) o
        // kullanılır; yoksa RoomVolumeCalculator ile hesaplanır (ProjectFactory ile aynı
        // Core mantığı — kapalı kabukta gerçek hacim, değilse bounding-box tahmini).
        var estimate = RoomVolumeCalculator.Estimate(_workingSurfaces, project.Unit);
        RoomVolume = project.RoomVolume > 0 ? project.RoomVolume : estimate.VolumeM3;
        _roomVolumeOrigin = project.VolumeOrigin
            ?? (project.ImportFormat == SurfaceImportFormat.Salon
                ? RoomVolumeOrigin.SourceModel
                : estimate.Origin);
        RoomVolumeSourceText = BuildRoomVolumeSourceText(_roomVolumeOrigin);
        _volumeInitialized = true;

        StatusMessage = $"{project.Surfaces.Count} yüzey, {CategoryNodes.Count} kategori, " +
                        $"{project.Sources.Count} kaynak, {project.Receivers.Count} alıcı yüklendi.";

        // Varsayılan auralizasyon dosyası
        var defaultAudio = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "guitar.wav");
        if (File.Exists(defaultAudio))
        {
            CustomAudioPath = defaultAudio;
        }

        // Simülasyon sonuçlarını geri yükle (eğer varsa)
        if (project.LastResult is not null)
        {
            OnSimulationCompleted(project.LastResult);
        }

        // Kütüphane değişikliklerini takip et
        MaterialDatabase.Instance.Changed += OnMaterialDatabaseChanged;

        InitPanels();
        InitSurfaceMaterials();
    }

    public void Dispose()
    {
        CancelActiveSimulation();
        MaterialDatabase.Instance.Changed -= OnMaterialDatabaseChanged;
    }

    private static string BuildRoomVolumeSourceText(RoomVolumeOrigin origin) => origin switch
    {
        RoomVolumeOrigin.ClosedMesh => "Hacim kaynağı: kapalı kabuktan hesaplanan gerçek hacim.",
        RoomVolumeOrigin.BoundingBoxEstimate =>
            "Hacim kaynağı: sınır kutusu tahmini (kabuk kapalı değil — RT60/Schroeder güvenilirliği düşük).",
        RoomVolumeOrigin.UserProvided => "Hacim kaynağı: kullanıcı tarafından girildi.",
        RoomVolumeOrigin.SourceModel => "Hacim kaynağı: kaynak salon/model tarafından sağlandı.",
        _ => "Hacim kaynağı: bilinmiyor."
    };

    // --- Proje bilgisi ---

    public string ProjectName { get; }
    public string UnitShortName { get; }
    public IReadOnlyList<string> ImportWarnings => _project.ImportWarnings
        .Concat(_sessionWarnings)
        .Distinct(StringComparer.Ordinal)
        .ToArray();
    public bool HasImportWarnings => ImportWarnings.Count > 0;
    public string ImportWarningsSummary => HasImportWarnings
        ? $"{ImportWarnings.Count} içe aktarma/geometri uyarısı:\n" +
          string.Join("\n", ImportWarnings.Select(w => $"• {w}"))
        : string.Empty;

    // --- Sol panel ağacı ---

    public IReadOnlyList<CategoryNodeViewModel> CategoryNodes
    {
        get => _categoryNodes;
        private set => SetProperty(ref _categoryNodes, value);
    }
    private IReadOnlyList<CategoryNodeViewModel> _categoryNodes = [];

    // --- Materyal ---

    public ObservableCollection<CategoryMaterialViewModel> CategoryMaterialRows { get; }


    private void OnMaterialDatabaseChanged()
    {
        Dispatcher.UIThread.Post(() =>
        {
            RefreshMaterialAssignmentLists();
            InvalidateSimulationResult("Materyal kütüphanesi değişti.");
        });
    }

    public void RefreshMaterialAssignmentLists()
    {
        foreach (var row in CategoryMaterialRows)
            row.RefreshAvailableMaterials();
    }

    public string MaterialStatusText
    {
        get => _materialStatusText;
        private set => SetProperty(ref _materialStatusText, value);
    }
    private string _materialStatusText = string.Empty;

    /// <summary>Tüm kategorilere materyal atanmışsa true.</summary>
    public bool AllMaterialsAssigned
    {
        get => _allMaterialsAssigned;
        private set => SetProperty(ref _allMaterialsAssigned, value);
    }
    private bool _allMaterialsAssigned;

    private void RefreshMaterialStatus()
    {
        var total    = CategoryMaterialRows.Count;
        var assigned = CategoryMaterialRows.Count(r => r.SelectedMaterial != MaterialLibrary.Default);
        var unresolved = CategoryMaterialRows.Count(r => r.HasUnresolvedMaterial);
        MaterialStatusText = unresolved > 0
            ? $"{assigned}/{total} kategori özelleştirildi · {unresolved} eksik materyal kimliği korunuyor"
            : $"{assigned}/{total} kategori özelleştirildi";
        AllMaterialsAssigned = total > 0;   // her kategori en az default'a sahip
        
        RefreshSabineCalculation();
    }

    // --- Akustik Hesaplama ---

    public double RoomVolume
    {
        get => _roomVolume;
        set
        {
            if (SetProperty(ref _roomVolume, value))
            {
                if (_volumeInitialized)
                {
                    _roomVolumeOrigin = RoomVolumeOrigin.UserProvided;
                    RoomVolumeSourceText = BuildRoomVolumeSourceText(_roomVolumeOrigin);
                    MarkDirty();
                    InvalidateSimulationResult("Oda hacmi değiştirildi.");
                }
                RefreshSabineCalculation();
            }
        }
    }
    private double _roomVolume;
    private bool _volumeInitialized;
    private RoomVolumeOrigin _roomVolumeOrigin = RoomVolumeOrigin.BoundingBoxEstimate;

    internal RoomVolumeOrigin VolumeOrigin => _roomVolumeOrigin;

    /// <summary>Hacmin nereden geldiğini açıklayan Türkçe metin (kapalı kabuk / bounding-box / kullanıcı girdisi).</summary>
    public string RoomVolumeSourceText
    {
        get => _roomVolumeSourceText;
        private set => SetProperty(ref _roomVolumeSourceText, value);
    }
    private string _roomVolumeSourceText = "Hacim kaynağı: bilinmiyor.";

    public IReadOnlyList<SabineBandViewModel> SabineBands { get; } =
    [
        new("63 Hz"), new("125 Hz"), new("250 Hz"),
        new("500 Hz"), new("1 kHz"), new("2 kHz"),
        new("4 kHz"), new("8 kHz"), new("Ortalama")
    ];

    public IReadOnlyList<SabineBandViewModel> EyringBands { get; } =
    [
        new("63 Hz"), new("125 Hz"), new("250 Hz"),
        new("500 Hz"), new("1 kHz"), new("2 kHz"),
        new("4 kHz"), new("8 kHz"), new("Ortalama")
    ];

    public string SchroederFrequencyText
    {
        get => _schroederFreqText;
        private set => SetProperty(ref _schroederFreqText, value);
    }
    private string _schroederFreqText = "—";

    public void RefreshSabineCalculation()
    {
        if (RoomVolume <= 0 || _workingSurfaces.Count == 0)
        {
            foreach (var b in SabineBands) b.ValueText = "—";
            foreach (var b in EyringBands)  b.ValueText = "—";
            SchroederFrequencyText = "—";
            return;
        }

        var metersPerUnit = ProjectScalingService.GetMetersPerUnit(_project.Unit);
        var areaScale = metersPerUnit * metersPerUnit;
        double totalArea = 0;
        double[] A     = new double[8]; // Sabine: Σ(αᵢ × Sⱼ)
        double[] alphaS = new double[8]; // Eyring: Σ(αᵢ × Sⱼ) / S_total → ᾱᵢ

        foreach (var surf in _workingSurfaces)
        {
            var area = SurfaceTransformService.ComputeArea(surf.Vertices) * areaScale;
            totalArea += area;

            var catRow   = CategoryMaterialRows.FirstOrDefault(r => string.Equals(r.Category, surf.Category, StringComparison.OrdinalIgnoreCase));
            var material = catRow?.SelectedMaterial ?? MaterialLibrary.Default;

            for (int i = 0; i < 8; i++)
                A[i] += area * material.Alpha[i];
        }

        double sabineSum = 0, eyringSum = 0;
        int sabineCount = 0, eyringCount = 0;
        for (int i = 0; i < 8; i++)
        {
            // Sabine
            var rtSabine = A[i] > 0 ? (AcousticConstants.SabineConstant * RoomVolume / A[i]) : 0;
            SabineBands[i].ValueText = rtSabine > 0 ? $"{rtSabine:F2}s" : "—";
            if (rtSabine > 0) { sabineSum += rtSabine; sabineCount++; }

            // Eyring-Norris: RT60 = 0.161 × V / (−S × ln(1 − ᾱ))
            var alphaMean = totalArea > 0 ? A[i] / totalArea : 0;
            alphaMean = Math.Min(alphaMean, 0.9999);
            var rtEyring = alphaMean > 0 && totalArea > 0
                ? (AcousticConstants.SabineConstant * RoomVolume / (-totalArea * Math.Log(1 - alphaMean)))
                : 0;
            EyringBands[i].ValueText = rtEyring > 0 ? $"{rtEyring:F2}s" : "—";
            if (rtEyring > 0) { eyringSum += rtEyring; eyringCount++; }
        }

        SabineBands[8].ValueText = sabineCount > 0 ? $"{sabineSum/sabineCount:F2}s" : "—";
        EyringBands[8].ValueText  = eyringCount  > 0 ? $"{eyringSum/eyringCount:F2}s"  : "—";

        // Schroeder frequency: f_S = 2000 × √(RT60_mid / V), using Eyring mid-freq average
        var rtMidEyring = 0.0;
        int midCount = 0;
        for (int i = 3; i <= 4; i++) // 500 Hz (idx 3) and 1 kHz (idx 4)
        {
            var alphaMean = totalArea > 0 ? A[i] / totalArea : 0;
            alphaMean = Math.Min(alphaMean, 0.9999);
            var rt = alphaMean > 0 && totalArea > 0
                ? (AcousticConstants.SabineConstant * RoomVolume / (-totalArea * Math.Log(1 - alphaMean)))
                : 0;
            if (rt > 0) { rtMidEyring += rt; midCount++; }
        }
        if (midCount > 0)
        {
            rtMidEyring /= midCount;
            var fs = 2000 * Math.Sqrt(rtMidEyring / RoomVolume);
            SchroederFrequencyText = $"{fs:F0} Hz";
        }
        else
        {
            SchroederFrequencyText = "—";
        }
    }

    /// <summary>Mevcut atamaları proje kaydına dönüştürür.</summary>
    public IReadOnlyDictionary<string, string> BuildCategoryMaterialMap() =>
        CategoryMaterialRows.ToDictionary(r => r.Category, r => r.PersistedMaterialId);

    // --- Viewport ---

    private readonly IReadOnlyList<ProjectSurface> _originalSurfaces;
    private List<ProjectSurface> _workingSurfaces = [];

    public IReadOnlyList<ProjectSurface> ProjectSurfaces => _workingSurfaces;

    internal void NotifySurfacesChanged()
    {
        OnPropertyChanged(nameof(ProjectSurfaces));
        OnPropertyChanged(nameof(VisibleSurfaces));
        MarkDirty();
        RefreshRoomVolumeAfterGeometryChange();
        InvalidateSimulationResult("Geometri değiştirildi.");
        RefreshSabineCalculation();
    }

    private void RefreshRoomVolumeAfterGeometryChange()
    {
        if (_roomVolumeOrigin is RoomVolumeOrigin.UserProvided or RoomVolumeOrigin.SourceModel)
        {
            RoomVolumeSourceText = BuildRoomVolumeSourceText(_roomVolumeOrigin) +
                " Geometri değişti; hacmi doğrulayın.";
            return;
        }

        var estimate = RoomVolumeCalculator.Estimate(_workingSurfaces, _project.Unit);
        _volumeInitialized = false;
        try
        {
            RoomVolume = estimate.VolumeM3 > 0 ? estimate.VolumeM3 : RoomVolume;
            _roomVolumeOrigin = estimate.Origin;
            RoomVolumeSourceText = BuildRoomVolumeSourceText(_roomVolumeOrigin);
        }
        finally
        {
            _volumeInitialized = true;
        }
    }

    /// <summary>Viewport'a yalnızca görünür kategorilerin yüzeylerini gönderir.</summary>
    public IReadOnlyList<ProjectSurface> VisibleSurfaces =>
        _workingSurfaces
            .Where(s => CategoryNodes
                .FirstOrDefault(n => string.Equals(n.Name, s.Category, StringComparison.OrdinalIgnoreCase))
                ?.IsVisible != false)
            .ToList();

    private void SelectSurfaceById(int id) => HighlightedSurfaceId = id;

    public IReadOnlyList<AcousticPoint> SourcePoints
    {
        get => _sourcePoints;
        private set => SetProperty(ref _sourcePoints, value);
    }
    private IReadOnlyList<AcousticPoint> _sourcePoints = [];

    public IReadOnlyList<AcousticPoint> ReceiverPoints
    {
        get => _receiverPoints;
        private set => SetProperty(ref _receiverPoints, value);
    }
    private IReadOnlyList<AcousticPoint> _receiverPoints = [];

    public IReadOnlyList<GeometryProjectionMode> ProjectionModes { get; } =
    [
        GeometryProjectionMode.Top2D,
        GeometryProjectionMode.Front2D,
        GeometryProjectionMode.Isometric3D
    ];

    public GeometryProjectionMode ProjectionMode
    {
        get => _projectionMode;
        set => SetProperty(ref _projectionMode, value);
    }
    private GeometryProjectionMode _projectionMode = GeometryProjectionMode.Isometric3D;

    public int? HighlightedSurfaceId
    {
        get => _highlightedSurfaceId;
        set
        {
            if (SetProperty(ref _highlightedSurfaceId, value))
            {
                RefreshSelectedSurface(value);
                OnPropertyChanged(nameof(CanDeleteSelectedSurface));
            }
        }
    }
    private int? _highlightedSurfaceId;

    public IReadOnlyList<int> SelectedSurfaceIds
    {
        get => _selectedSurfaceIds;
        set
        {
            var normalized = value.Distinct().ToArray();
            if (SetProperty(ref _selectedSurfaceIds, normalized))
            {
                RefreshSelectedSurfaces();
                SyncPanelRowsFromSelectedSurfaceIds(normalized);
                OnPropertyChanged(nameof(CanApplyTransform));
                OnPropertyChanged(nameof(CanDeleteSelectedSurface));
            }
        }
    }
    private IReadOnlyList<int> _selectedSurfaceIds = [];

    // --- Durum ---

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }
    private string _statusMessage = string.Empty;

    // --- Yardımcı ---

    private static string FormatUnit(Kst.Acoustics.Core.Importing.SurfaceImportUnit unit) => unit switch
    {
        Kst.Acoustics.Core.Importing.SurfaceImportUnit.Meters      => "m",
        Kst.Acoustics.Core.Importing.SurfaceImportUnit.Centimeters  => "cm",
        Kst.Acoustics.Core.Importing.SurfaceImportUnit.Millimeters  => "mm",
        Kst.Acoustics.Core.Importing.SurfaceImportUnit.Inches       => "in",
        Kst.Acoustics.Core.Importing.SurfaceImportUnit.Feet         => "ft",
        _ => "m"
    };
}

public sealed class CategoryNodeViewModel : ViewModelBase
{
    public string Name  { get; }
    public int    Count { get; }
    public string Label => $"{Name}  ({Count})";

    public IReadOnlyList<SurfaceNodeViewModel> Surfaces { get; }

    // ── Görünürlük ────────────────────────────────────────────────────────────
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (SetProperty(ref _isVisible, value))
                OnPropertyChanged(nameof(VisibilityGlyph));
        }
    }
    private bool _isVisible = true;
    public string VisibilityGlyph => IsVisible ? "●" : "○";

    // ── Genişlet/daralt ───────────────────────────────────────────────────────
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
                OnPropertyChanged(nameof(ExpandGlyph));
        }
    }
    private bool _isExpanded;
    public string ExpandGlyph => IsExpanded ? "▼" : "▶";

    public void ToggleExpanded()   => IsExpanded   = !IsExpanded;
    public void ToggleVisibility() => IsVisible    = !IsVisible;

    public CategoryNodeViewModel(string name, IEnumerable<SurfaceNodeViewModel> surfaces)
    {
        Name     = name;
        Surfaces = surfaces.ToArray();
        Count    = Surfaces.Count;
    }
}

public sealed class SurfaceNodeViewModel
{
    public int    Id   { get; }
    public string Name { get; }

    private readonly Action<int> _onSelect;
    public void Select() => _onSelect(Id);

    public SurfaceNodeViewModel(int id, string name, Action<int> onSelect)
    {
        Id        = id;
        Name      = name;
        _onSelect = onSelect;
    }
}

public sealed class CategoryMaterialViewModel : ViewModelBase
{
    public string Category   { get; }
    public int    SurfCount  { get; }
    public string CategoryLabel => $"{Category}  ({SurfCount})";

    public IReadOnlyList<AcousticMaterial> AvailableMaterials
    {
        get => _availableMaterials;
        private set => SetProperty(ref _availableMaterials, value);
    }
    private IReadOnlyList<AcousticMaterial> _availableMaterials = [];

    public AcousticMaterial SelectedMaterial
    {
        get => _selectedMaterial;
        set
        {
            if (SetProperty(ref _selectedMaterial, value))
            {
                _unresolvedMaterialId = null;
                OnPropertyChanged(nameof(HasUnresolvedMaterial));
                OnPropertyChanged(nameof(PersistedMaterialId));
                OnPropertyChanged(nameof(AlphaRow));
                OnPropertyChanged(nameof(ScatteringText));
            }
        }
    }
    private AcousticMaterial _selectedMaterial;

    private string? _unresolvedMaterialId;
    public bool HasUnresolvedMaterial => !string.IsNullOrWhiteSpace(_unresolvedMaterialId);
    public string PersistedMaterialId => _unresolvedMaterialId ?? SelectedMaterial.Id;

    public int OverrideCount
    {
        get => _overrideCount;
        set
        {
            if (SetProperty(ref _overrideCount, value))
                OnPropertyChanged(nameof(OverrideBadgeText));
        }
    }
    private int _overrideCount;

    public string OverrideBadgeText => _overrideCount > 0 ? $"({_overrideCount} özel)" : "";

    public string AlphaRow
    {
        get
        {
            var a = SelectedMaterial.Alpha;
            return $"α  {a[0]:F2}  {a[1]:F2}  {a[2]:F2}  {a[3]:F2}  {a[4]:F2}  {a[5]:F2}  {a[6]:F2}  {a[7]:F2}";
        }
    }

    public string ScatteringText => $"s̄ = {SelectedMaterial.ScatteringMid:F2}";

    public CategoryMaterialViewModel(
        string category,
        int surfCount,
        AcousticMaterial initial,
        string? unresolvedMaterialId = null)
    {
        Category          = category;
        SurfCount         = surfCount;
        _selectedMaterial = initial;
        _unresolvedMaterialId = unresolvedMaterialId;
        RefreshAvailableMaterials();
    }

    public void RefreshAvailableMaterials()
    {
        var favorites = MaterialDatabase.Instance.Favorites.ToList();
        if (!favorites.Any(m => string.Equals(m.Id, SelectedMaterial.Id, StringComparison.OrdinalIgnoreCase)))
            favorites.Insert(0, SelectedMaterial);

        AvailableMaterials = favorites
            .GroupBy(m => m.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToArray();
    }
}

public sealed class SabineBandViewModel : ViewModelBase
{
    public string Label { get; }
    
    public string ValueText
    {
        get => _valueText;
        set => SetProperty(ref _valueText, value);
    }
    private string _valueText = "—";

    public SabineBandViewModel(string label)
    {
        Label = label;
    }
}
