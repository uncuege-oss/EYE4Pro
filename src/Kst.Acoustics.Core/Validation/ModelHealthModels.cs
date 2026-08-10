using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.Core.Validation;

/// <summary>Model sağlığı bulgusunun kullanıcı müdahalesi açısından önem düzeyi.</summary>
public enum ModelHealthSeverity
{
    Information,
    Warning,
    Error
}

/// <summary>Makinece filtrelenebilir model sağlığı bulgu kodları.</summary>
public enum ModelHealthIssueCode
{
    OpenEdge,
    NonManifoldEdge,
    DuplicateSurface,
    ConsecutiveDuplicateVertex,
    ZeroAreaSurface,
    MicroSurface,
    InconsistentWinding,
    MissingMaterial,
    PointOutsideRoom,
    SuspiciousUnit,
    SuspiciousVolume,
    ExcessiveGrid
}

/// <summary>
/// Tek bir sağlık bulgusu. Yüzey kimlikleri doğrudan viewport seçimine bağlanabilir;
/// kaynak/alıcı bulgularında <see cref="AffectedPointIds"/> kullanılır.
/// </summary>
public sealed record ModelHealthFinding(
    ModelHealthIssueCode Code,
    ModelHealthSeverity Severity,
    string Title,
    string Description,
    IReadOnlyList<int>? AffectedSurfaceIds = null,
    IReadOnlyList<int>? AffectedPointIds = null,
    int OccurrenceCount = 1)
{
    public IReadOnlyList<int> AffectedSurfaceIds { get; init; } =
        AffectedSurfaceIds?.Distinct().Order().ToArray() ?? [];

    public IReadOnlyList<int> AffectedPointIds { get; init; } =
        AffectedPointIds?.Distinct().Order().ToArray() ?? [];

    public string SeverityText => Severity switch
    {
        ModelHealthSeverity.Error => "Hata",
        ModelHealthSeverity.Warning => "Uyarı",
        _ => "Bilgi"
    };

    public bool HasAffectedSurfaces => AffectedSurfaceIds.Count > 0;
}

/// <summary>Bir sağlık analizi çalışmasının değişmez özeti.</summary>
public sealed record ModelHealthReport(IReadOnlyList<ModelHealthFinding> Findings)
{
    public IReadOnlyList<ModelHealthFinding> Findings { get; init; } = Findings?.ToArray() ?? [];
    public int ErrorCount => Findings.Count(f => f.Severity == ModelHealthSeverity.Error);
    public int WarningCount => Findings.Count(f => f.Severity == ModelHealthSeverity.Warning);
    public int InformationCount => Findings.Count(f => f.Severity == ModelHealthSeverity.Information);
    public bool IsHealthy => ErrorCount == 0 && WarningCount == 0;
    public bool IsReadyForSimulation => ErrorCount == 0;
    public string Summary => Findings.Count == 0
        ? "Model sağlıklı; incelenecek bulgu yok."
        : $"{ErrorCount} hata, {WarningCount} uyarı, {InformationCount} bilgi bulgusu.";
}

public enum ModelHealthRepairAction
{
    RemoveDuplicateSurface,
    RemoveDegenerateSurface,
    RemoveMicroSurface,
    RemoveConsecutiveDuplicateVertex,
    OrientSurfaceWinding
}

/// <summary>Önizlemede gösterilecek tek bir güvenli onarım değişikliği.</summary>
public sealed record ModelHealthRepairChange(
    ModelHealthRepairAction Action,
    string Description,
    IReadOnlyList<int>? AffectedSurfaceIds = null)
{
    public IReadOnlyList<int> AffectedSurfaceIds { get; init; } =
        AffectedSurfaceIds?.Distinct().Order().ToArray() ?? [];
}

/// <summary>
/// Güvenli onarımların önizlemesi. <see cref="RepairedProject"/> yeni bir snapshot'tır;
/// giriş projesi ve onun yüzey/köşe koleksiyonları mutasyona uğratılmaz.
/// </summary>
public sealed record ModelHealthRepairPreview(
    AcousticsProject RepairedProject,
    IReadOnlyList<ModelHealthRepairChange> Changes,
    ModelHealthReport RemainingReport,
    int OriginalSurfaceCount)
{
    public IReadOnlyList<ModelHealthRepairChange> Changes { get; init; } = Changes?.ToArray() ?? [];
    public bool HasChanges => Changes.Count > 0;
    public int RepairedSurfaceCount => RepairedProject.Surfaces.Count;
    public string Summary => !HasChanges
        ? "Uygulanabilir güvenli otomatik onarım bulunamadı."
        : $"{Changes.Count} güvenli işlem önizlendi; yüzey sayısı {OriginalSurfaceCount} → {RepairedSurfaceCount}.";
}

/// <summary>Fiziksel eşikler ve otomatik onarım kapsamı.</summary>
public sealed record ModelHealthOptions(
    double TopologyToleranceMeters = 1e-4,
    double ZeroAreaToleranceM2 = 1e-10,
    double MicroSurfaceAreaM2 = 1e-4,
    int MaximumGridCells = 4_000,
    double SuspiciousMinimumExtentMeters = 0.25,
    double SuspiciousMaximumExtentMeters = 500.0,
    IReadOnlySet<int>? ProtectedSurfaceIds = null)
{
    public IReadOnlySet<int> ProtectedSurfaceIds { get; init; } =
        ProtectedSurfaceIds is null ? new HashSet<int>() : new HashSet<int>(ProtectedSurfaceIds);
}
