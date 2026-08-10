using CommunityToolkit.Mvvm.Input;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Validation;

namespace Kst.Acoustics.App.ViewModels;

/// <summary>M01 Model Sağlığı ve Güvenli Onarım Merkezi bağlantıları.</summary>
public sealed partial class ProjectWindowViewModel
{
    private const string InitialHealthSummary = "Model sağlığı henüz analiz edilmedi.";
    private readonly ModelHealthService _modelHealthService = new();
    private long _healthPreviewRevision = -1;

    public IReadOnlyList<ModelHealthFinding> HealthFindings
    {
        get => _healthFindings;
        private set
        {
            if (SetProperty(ref _healthFindings, value))
            {
                OnPropertyChanged(nameof(HasHealthFindings));
                OnPropertyChanged(nameof(HealthErrorCount));
                OnPropertyChanged(nameof(HealthWarningCount));
            }
        }
    }
    private IReadOnlyList<ModelHealthFinding> _healthFindings = [];

    public bool HasHealthFindings => HealthFindings.Count > 0;
    public int HealthErrorCount => HealthFindings.Count(f => f.Severity == ModelHealthSeverity.Error);
    public int HealthWarningCount => HealthFindings.Count(f => f.Severity == ModelHealthSeverity.Warning);

    public string HealthSummary
    {
        get => _healthSummary;
        private set => SetProperty(ref _healthSummary, value);
    }
    private string _healthSummary = InitialHealthSummary;

    public bool IsHealthBusy
    {
        get => _isHealthBusy;
        private set => SetProperty(ref _isHealthBusy, value);
    }
    private bool _isHealthBusy;

    /// <summary>
    /// Seçilen bulgunun yüzey kimlikleri. XAML doğrudan bu kümeyi viewport seçim
    /// kaynağı olarak kullanabilir; setter ayrıca mevcut viewport seçimini eşitler.
    /// </summary>
    public IReadOnlyList<int> HealthSelectedSurfaceIds
    {
        get => _healthSelectedSurfaceIds;
        private set => SetProperty(ref _healthSelectedSurfaceIds, value);
    }
    private IReadOnlyList<int> _healthSelectedSurfaceIds = [];

    public ModelHealthFinding? SelectedHealthFinding
    {
        get => _selectedHealthFinding;
        set
        {
            if (!SetProperty(ref _selectedHealthFinding, value)) return;
            var ids = value?.AffectedSurfaceIds
                .Where(id => _workingSurfaces.Any(surface => surface.Id == id))
                .Distinct()
                .ToArray() ?? [];
            HealthSelectedSurfaceIds = ids;
            SelectedSurfaceIds = ids;
            HighlightedSurfaceId = ids.Length > 0 ? ids[0] : null;
        }
    }
    private ModelHealthFinding? _selectedHealthFinding;

    public ModelHealthRepairPreview? HealthRepairPreview
    {
        get => _healthRepairPreview;
        private set
        {
            if (SetProperty(ref _healthRepairPreview, value))
            {
                OnPropertyChanged(nameof(HasHealthRepairPreview));
                OnPropertyChanged(nameof(CanApplyHealthRepair));
                OnPropertyChanged(nameof(HealthRepairPreviewSummary));
            }
        }
    }
    private ModelHealthRepairPreview? _healthRepairPreview;

    public bool HasHealthRepairPreview => HealthRepairPreview is not null;
    public bool CanApplyHealthRepair =>
        HealthRepairPreview?.HasChanges == true && _healthPreviewRevision == _revision;
    public string HealthRepairPreviewSummary =>
        HealthRepairPreview?.Summary ?? "Önizleme hazırlanmadı.";

    /// <summary>Belge değiştiğinde eski bulgu/önizlemenin yeni modele uygulanmasını engeller.</summary>
    internal void InvalidateModelHealthAnalysis()
    {
        if (HealthFindings.Count == 0 && HealthRepairPreview is null &&
            string.Equals(HealthSummary, InitialHealthSummary, StringComparison.Ordinal))
            return;

        HealthFindings = [];
        if (_selectedHealthFinding is not null)
        {
            _selectedHealthFinding = null;
            OnPropertyChanged(nameof(SelectedHealthFinding));
        }
        HealthSelectedSurfaceIds = [];
        HealthRepairPreview = null;
        _healthPreviewRevision = -1;
        HealthSummary = "Proje değişti; model sağlığını yeniden analiz edin.";
    }

    [RelayCommand]
    private async Task AnalyzeModelHealthAsync()
    {
        if (IsHealthBusy) return;
        IsHealthBusy = true;
        StatusMessage = "Model sağlığı analiz ediliyor...";
        try
        {
            var snapshot = BuildCurrentProject();
            var report = await Task.Run(() => _modelHealthService.Analyze(snapshot));
            HealthFindings = report.Findings;
            SelectedHealthFinding = null;
            HealthSummary = report.Summary;
            HealthRepairPreview = null;
            _healthPreviewRevision = -1;
            StatusMessage = report.Findings.Count == 0
                ? "Model sağlığı analizi tamamlandı: bulgu yok."
                : $"Model sağlığı analizi tamamlandı: {report.Summary}";
        }
        catch (Exception ex)
        {
            HealthSummary = "Model sağlığı analizi tamamlanamadı.";
            StatusMessage = $"Model sağlığı analizi yapılamadı: {ex.Message}";
        }
        finally
        {
            IsHealthBusy = false;
        }
    }

    [RelayCommand]
    private async Task PreviewSafeHealthRepairsAsync()
    {
        if (IsHealthBusy) return;
        IsHealthBusy = true;
        StatusMessage = "Güvenli onarım önizlemesi hazırlanıyor...";
        try
        {
            var snapshot = BuildCurrentProject();
            var revision = _revision;
            var protectedIds = _workingSurfaces
                .Where(IsGeneratedPanelSurface)
                .Select(surface => surface.Id)
                .ToHashSet();
            var options = new ModelHealthOptions(ProtectedSurfaceIds: protectedIds);
            var preview = await Task.Run(() =>
                _modelHealthService.CreateSafeRepairPreview(snapshot, options));

            // Hesap sürerken belge değiştiyse eski önizlemeyi uygulanabilir kılma.
            if (_revision != revision)
            {
                HealthRepairPreview = null;
                _healthPreviewRevision = -1;
                StatusMessage = "Onarım önizlemesi hazırlanırken proje değişti; yeniden önizleyin.";
                return;
            }

            HealthRepairPreview = preview;
            _healthPreviewRevision = revision;
            HealthFindings = preview.RemainingReport.Findings;
            SelectedHealthFinding = null;
            HealthSummary = preview.RemainingReport.Summary;
            OnPropertyChanged(nameof(CanApplyHealthRepair));
            StatusMessage = preview.Summary;
        }
        catch (Exception ex)
        {
            HealthRepairPreview = null;
            _healthPreviewRevision = -1;
            StatusMessage = $"Onarım önizlemesi hazırlanamadı: {ex.Message}";
        }
        finally
        {
            IsHealthBusy = false;
        }
    }

    [RelayCommand]
    private void ApplySafeHealthRepairs()
    {
        var preview = HealthRepairPreview;
        if (preview is null || !preview.HasChanges)
        {
            StatusMessage = "Uygulanacak güvenli onarım yok.";
            return;
        }
        if (_healthPreviewRevision != _revision)
        {
            StatusMessage = "Proje önizlemeden sonra değişti; onarımları yeniden önizleyin.";
            HealthRepairPreview = null;
            _healthPreviewRevision = -1;
            return;
        }

        using var historyChange = BeginDocumentChange("Güvenli model onarımları uygulandı");
        var validIds = preview.RepairedProject.Surfaces.Select(s => s.Id).ToHashSet();
        _workingSurfaces = preview.RepairedProject.Surfaces
            .Select(s => s with { Vertices = s.Vertices.ToArray() })
            .ToList();
        foreach (var key in _surfaceMaterialOverrides.Keys.ToArray())
        {
            if (!int.TryParse(key, out var id) || !validIds.Contains(id))
                _surfaceMaterialOverrides.Remove(key);
        }

        SelectedHealthFinding = null;
        SelectedSurfaceIds = [];
        HighlightedSurfaceId = null;
        RebuildCategoryNodes();
        RebuildSurfaceMaterialRows();
        NotifySurfacesChanged();

        HealthFindings = preview.RemainingReport.Findings;
        HealthSummary = preview.RemainingReport.Summary;
        HealthRepairPreview = null;
        _healthPreviewRevision = -1;
        OnPropertyChanged(nameof(CanApplyHealthRepair));
        StatusMessage = $"{preview.Changes.Count} güvenli onarım uygulandı. {HealthSummary}";
    }
}
