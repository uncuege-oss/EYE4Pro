using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.App.ViewModels;

public sealed partial class ProjectWindowViewModel
{
    public IReadOnlyList<string> AssistedPointTypeOptions { get; } = ["Alıcı", "Kaynak"];
    public IReadOnlyList<string> AssistedPatternOptions { get; } =
        ["Çizgisel", "Dikdörtgen ızgara", "Dairesel", "Seçili yüzey sınırı"];

    public int AssistedPointTypeIndex { get => _assistedPointTypeIndex; set => SetProperty(ref _assistedPointTypeIndex, Math.Clamp(value, 0, 1)); }
    private int _assistedPointTypeIndex;
    public int AssistedPatternIndex { get => _assistedPatternIndex; set => SetProperty(ref _assistedPatternIndex, Math.Clamp(value, 0, 3)); }
    private int _assistedPatternIndex = 1;
    public double AssistedOriginX { get => _assistedOriginX; set => SetProperty(ref _assistedOriginX, value); }
    private double _assistedOriginX;
    public double AssistedOriginY { get => _assistedOriginY; set => SetProperty(ref _assistedOriginY, value); }
    private double _assistedOriginY;
    public double AssistedPlaneZ { get => _assistedPlaneZ; set => SetProperty(ref _assistedPlaneZ, value); }
    private double _assistedPlaneZ = 1.2;
    public int AssistedCountX { get => _assistedCountX; set => SetProperty(ref _assistedCountX, Math.Clamp(value, 1, 200)); }
    private int _assistedCountX = 5;
    public int AssistedCountY { get => _assistedCountY; set => SetProperty(ref _assistedCountY, Math.Clamp(value, 1, 200)); }
    private int _assistedCountY = 4;
    public double AssistedSpacingMeters { get => _assistedSpacingMeters; set => SetProperty(ref _assistedSpacingMeters, double.IsFinite(value) ? Math.Clamp(value, 0.05, 100) : 1.0); }
    private double _assistedSpacingMeters = 1.0;
    public double AssistedRadiusMeters { get => _assistedRadiusMeters; set => SetProperty(ref _assistedRadiusMeters, double.IsFinite(value) ? Math.Clamp(value, 0.05, 1_000) : 2.0); }
    private double _assistedRadiusMeters = 2.0;
    public double AssistedAzimuthDegrees { get => _assistedAzimuthDegrees; set => SetProperty(ref _assistedAzimuthDegrees, double.IsFinite(value) ? value : 0); }
    private double _assistedAzimuthDegrees;
    public bool AssistedKeepInsideRoom { get => _assistedKeepInsideRoom; set => SetProperty(ref _assistedKeepInsideRoom, value); }
    private bool _assistedKeepInsideRoom = true;

    public string AssistedPlacementSummary
    {
        get => _assistedPlacementSummary;
        private set => SetProperty(ref _assistedPlacementSummary, value);
    }
    private string _assistedPlacementSummary = "Ayarları girip önce önizleyin.";

    public int AssistedPreviewCount
    {
        get => _assistedPreviewCount;
        private set => SetProperty(ref _assistedPreviewCount, value);
    }
    private int _assistedPreviewCount;

    private AcousticPointPlacementResult? _assistedPreview;

    public IReadOnlyList<AcousticPoint> ViewportSourcePoints => SourcePoints
        .Concat(_assistedPreview?.Points.Where(point => point.Type == AcousticPointType.Source) ?? [])
        .ToArray();

    public IReadOnlyList<AcousticPoint> ViewportReceiverPoints => ReceiverPoints
        .Concat(_assistedPreview?.Points.Where(point => point.Type == AcousticPointType.Receiver) ?? [])
        .ToArray();

    public void PreviewAssistedPointPlacement()
    {
        try
        {
            _assistedPreview = BuildAssistedPlacement();
            AssistedPreviewCount = _assistedPreview.Points.Count;
            AssistedPlacementSummary = "Önizleme: " + _assistedPreview.Summary;
            NotifyAssistedViewportChanged();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or OverflowException)
        {
            _assistedPreview = null;
            AssistedPreviewCount = 0;
            AssistedPlacementSummary = $"Yerleştirme hazırlanamadı: {ex.Message}";
            NotifyAssistedViewportChanged();
        }
    }

    public void ApplyAssistedPointPlacement()
    {
        PreviewAssistedPointPlacement();
        if (_assistedPreview is not { Points.Count: > 0 } preview)
        {
            StatusMessage = AssistedPlacementSummary;
            return;
        }

        foreach (var point in preview.Points)
        {
            var row = AcousticPointViewModel.FromModel(point);
            if (point.Type == AcousticPointType.Source) Sources.Add(row);
            else Receivers.Add(row);
        }
        RefreshPointSnapshots();
        StatusMessage = preview.Summary;
        _assistedPreview = null;
        AssistedPreviewCount = 0;
        NotifyAssistedViewportChanged();
    }

    private AcousticPointPlacementResult BuildAssistedPlacement()
    {
        var type = AssistedPointTypeIndex == 1 ? AcousticPointType.Source : AcousticPointType.Receiver;
        var pattern = (AcousticPointArrayPattern)AssistedPatternIndex;
        var existing = type == AcousticPointType.Source ? Sources : Receivers;
        var nextId = existing.Count == 0 ? 1 : existing.Max(p => p.Id) + 1;
        var boundary = pattern == AcousticPointArrayPattern.BoundaryGrid
            ? GetAssistedBoundary()
            : null;
        var request = new AcousticPointPlacementRequest(
            type,
            pattern,
            new Point3D(AssistedOriginX, AssistedOriginY,
                AssistedPlaneZ / ProjectScalingService.GetMetersPerUnit(_project.Unit)),
            StartId: nextId,
            LabelPrefix: type == AcousticPointType.Source ? "S" : "R",
            CountX: AssistedCountX,
            CountY: AssistedCountY,
            SpacingMeters: AssistedSpacingMeters,
            RadiusMeters: AssistedRadiusMeters,
            AzimuthDegrees: AssistedAzimuthDegrees,
            Unit: _project.Unit,
            Boundary: boundary,
            KeepInsideRoom: AssistedKeepInsideRoom);
        return AcousticPointPlacementService.Generate(request, _workingSurfaces);
    }

    private IReadOnlyList<Point3D> GetAssistedBoundary()
    {
        var selectedId = HighlightedSurfaceId ?? SelectedSurfaceIds.FirstOrDefault();
        var selected = _workingSurfaces.FirstOrDefault(s => s.Id == selectedId);
        if (selected is not null) return selected.Vertices;

        var audience = _workingSurfaces.FirstOrDefault(s =>
            s.Category.Contains("SEYİRCİ", StringComparison.OrdinalIgnoreCase) ||
            s.Category.Contains("SEYIRCI", StringComparison.OrdinalIgnoreCase) ||
            s.Category.Contains("AUDIENCE", StringComparison.OrdinalIgnoreCase));
        return audience?.Vertices ?? throw new InvalidDataException(
            "Sınır içi yerleştirme için bir yüzey seçin veya seyirci yüzeyi yükleyin.");
    }

    private void NotifyAssistedViewportChanged()
    {
        OnPropertyChanged(nameof(ViewportSourcePoints));
        OnPropertyChanged(nameof(ViewportReceiverPoints));
    }
}
