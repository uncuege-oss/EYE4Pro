using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.App.ViewModels;

public sealed partial class ProjectWindowViewModel
{
    private const int AutomaticPerformanceSurfaceThreshold = 10_000;
    private const int AutomaticPerformanceVertexThreshold = 100_000;

    public bool LargeModelPerformanceMode
    {
        get => _largeModelPerformanceMode;
        set
        {
            if (!SetProperty(ref _largeModelPerformanceMode, value)) return;
            OnPropertyChanged(nameof(VisibleSurfaces));
            OnPropertyChanged(nameof(PerformanceModeSummary));
            StatusMessage = value
                ? "Büyük model modu açıldı. Yalnız viewport sadeleştirilir; proje ve simülasyon tam geometriyi kullanır."
                : "Tam detaylı viewport açıldı.";
        }
    }
    private bool _largeModelPerformanceMode;

    public int ViewportSurfaceLimit
    {
        get => _viewportSurfaceLimit;
        set
        {
            var normalized = Math.Clamp(value, 250, 50_000);
            if (!SetProperty(ref _viewportSurfaceLimit, normalized)) return;
            OnPropertyChanged(nameof(VisibleSurfaces));
            OnPropertyChanged(nameof(PerformanceModeSummary));
        }
    }
    private int _viewportSurfaceLimit = 4_000;

    public bool IsLargeModelDetected =>
        _workingSurfaces.Count >= AutomaticPerformanceSurfaceThreshold ||
        _workingSurfaces.Sum(surface => surface.Vertices.Count) >= AutomaticPerformanceVertexThreshold;

    public string PerformanceModeSummary
    {
        get
        {
            var visibleCount = GetCategoryVisibleSurfaces().Count;
            if (!LargeModelPerformanceMode)
                return $"Tam detay · {visibleCount:N0} yüzey";

            var shown = BuildViewportSurfaceList().Count;
            return $"Hızlı görünüm · {shown:N0}/{visibleCount:N0} yüzey (kayıt ve simülasyon tam)";
        }
    }

    private void InitializeLargeModelPerformanceMode()
    {
        _largeModelPerformanceMode = IsLargeModelDetected;
        OnPropertyChanged(nameof(LargeModelPerformanceMode));
        OnPropertyChanged(nameof(IsLargeModelDetected));
        OnPropertyChanged(nameof(PerformanceModeSummary));
    }

    private IReadOnlyList<ProjectSurface> BuildViewportSurfaceList()
    {
        var visible = GetCategoryVisibleSurfaces();
        if (!LargeModelPerformanceMode || visible.Count <= ViewportSurfaceLimit)
            return visible;

        var pinned = SelectedSurfaceIds
            .Concat(HighlightedSurfaceId is { } highlighted ? [highlighted] : [])
            .Distinct();
        return ViewportSurfaceDecimator.Reduce(visible, ViewportSurfaceLimit, pinned).Surfaces;
    }

    private IReadOnlyList<ProjectSurface> GetCategoryVisibleSurfaces() =>
        _workingSurfaces
            .Where(surface => CategoryNodes
                .FirstOrDefault(node => string.Equals(
                    node.Name, surface.Category, StringComparison.OrdinalIgnoreCase))
                ?.IsVisible != false)
            .ToArray();

    private void NotifyViewportPerformanceChanged()
    {
        OnPropertyChanged(nameof(VisibleSurfaces));
        OnPropertyChanged(nameof(IsLargeModelDetected));
        OnPropertyChanged(nameof(PerformanceModeSummary));
    }
}
