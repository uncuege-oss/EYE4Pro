using Kst.Acoustics.Core.Importing;

namespace Kst.Acoustics.Core.Projects;

public sealed record AcousticsProject(
    string Name,
    string? SourceFilePath,
    DateTime ImportedAtUtc,
    SurfaceImportFormat ImportFormat,
    SurfaceImportUnit Unit,
    IReadOnlyList<ProjectSurface> Surfaces,
    IReadOnlyList<AcousticPoint>? Sources = null,
    IReadOnlyList<AcousticPoint>? Receivers = null,
    IReadOnlyDictionary<string, string>? CategoryMaterials = null,
    RoomType SelectedRoomType = RoomType.General,
    double RoomVolume = 0,
    Kst.Acoustics.Core.Simulation.SimulationResult? LastResult = null,
    AcousticPanelKonfigurasyon? PanelKonfigurasyon = null,
    IReadOnlyDictionary<string, string>? SurfaceMaterials = null,
    RoomVolumeOrigin? VolumeOrigin = null,
    IReadOnlyList<string>? ImportWarnings = null,
    Kst.Acoustics.Core.Simulation.SimulationParameters? SimulationSettings = null,
    IReadOnlyList<AcousticScenario>? Scenarios = null,
    Guid? ActiveScenarioId = null)
{
    /// <summary>Proje dosyasının şema sürümü (migrasyon için kullanılır).</summary>
    public int SemaSurum { get; init; } = 1;

    /// <summary>
    /// Kaynak geometri artifact'ının (ör. M51 `.e4salon.json`) yüklendiği andaki
    /// `GeometriRevizyon` damgası. Proje açılırken kaynak dosyanın güncel revizyonuyla
    /// karşılaştırılır; fark varsa "geometri güncellenmiş" uyarısı gösterilir.
    /// null = revizyonlu bir kaynaktan gelmedi (DXF vb.).
    /// </summary>
    public int? KaynakGeometriRevizyon { get; init; }

    /// <summary>Eksik/eski JSON dosyalarında ad null gelebilir; domain modeli null taşımaz.</summary>
    public string Name { get; init; } = string.IsNullOrWhiteSpace(Name) ? "Adsız Proje" : Name.Trim();

    /// <summary>Hiçbir zaman null dönmez — JSON yoksa boş liste.</summary>
    public IReadOnlyList<ProjectSurface> Surfaces { get; init; } = Surfaces?.ToArray() ?? [];

    /// <summary>Hiçbir zaman null dönmez — JSON yoksa boş liste.</summary>
    public IReadOnlyList<AcousticPoint> Sources { get; init; } = Sources?.ToArray() ?? [];

    /// <summary>Hiçbir zaman null dönmez — JSON yoksa boş liste.</summary>
    public IReadOnlyList<AcousticPoint> Receivers { get; init; } = Receivers?.ToArray() ?? [];

    /// <summary>Kategori → materyal ID eşleşmesi. JSON yoksa boş sözlük.</summary>
    public IReadOnlyDictionary<string, string> CategoryMaterials { get; init; } =
        CategoryMaterials is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(CategoryMaterials, StringComparer.OrdinalIgnoreCase);

    /// <summary>Panel yerleşim konfigürasyonu. JSON yoksa null.</summary>
    public AcousticPanelKonfigurasyon? PanelKonfigurasyon { get; init; } = PanelKonfigurasyon;

    /// <summary>Oda hacminin kaynağı; kullanıcı override'ı ve kaynak model değeri yeniden açmada korunur.</summary>
    public RoomVolumeOrigin? VolumeOrigin { get; init; } = VolumeOrigin;

    /// <summary>İçe aktarma sırasında üretilen kalıcı geometri denetim notları.</summary>
    public IReadOnlyList<string> ImportWarnings { get; init; } =
        ImportWarnings?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray() ?? [];

    /// <summary>
    /// Yüzey ID → materyal ID override. Bir yüzey burada varsa kategori varsayılanı
    /// yerine bu materyal kullanılır. Anahtar string (Surface.Id.ToString()) —
    /// JSON sözlük anahtarı zorunluluğundan ve forward-compat için.
    /// </summary>
    public IReadOnlyDictionary<string, string> SurfaceMaterials { get; init; } =
        SurfaceMaterials is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(SurfaceMaterials, StringComparer.OrdinalIgnoreCase);

    /// <summary>Baseline'dan bağımsız, güvenli çalışma snapshot'ları.</summary>
    public IReadOnlyList<AcousticScenario> Scenarios { get; init; } =
        Scenarios?.ToArray() ?? [];
}
