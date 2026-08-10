using Kst.Acoustics.Core.Simulation;

namespace Kst.Acoustics.Core.Projects;

/// <summary>
/// Bir akustik alternatifin güvenli ve kendi kendine yeterli çalışma snapshot'ı.
/// Tam snapshot tercih edilmiştir: senaryo geçişinde mutable panel/sonuç nesneleri
/// baseline ile referans paylaşmaz ve ileride eklenecek geometri düzenlemeleri de
/// kullanıcının ana modelini sessizce ezmez.
/// </summary>
public sealed class AcousticScenarioState
{
    public IReadOnlyList<ProjectSurface> Surfaces { get; init; } = [];
    public IReadOnlyList<AcousticPoint> Sources { get; init; } = [];
    public IReadOnlyList<AcousticPoint> Receivers { get; init; } = [];
    public IReadOnlyDictionary<string, string> CategoryMaterials { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, string> SurfaceMaterials { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public AcousticPanelKonfigurasyon? PanelKonfigurasyon { get; init; }
    public SimulationParameters? SimulationSettings { get; init; }
    public SimulationResult? LastResult { get; init; }
    public RoomType SelectedRoomType { get; init; } = RoomType.General;
    public double RoomVolume { get; init; }
    public RoomVolumeOrigin? VolumeOrigin { get; init; }
}

public sealed record AcousticScenario(
    Guid Id,
    string Name,
    AcousticScenarioState State,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>Senaryoları oluşturma ve güvenli kopyalama için saf domain işlemleri.</summary>
public static class AcousticScenarioManager
{
    public static AcousticScenario Create(
        string name,
        AcousticScenarioState state,
        DateTime? utcNow = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        var now = utcNow ?? DateTime.UtcNow;
        return new AcousticScenario(
            Guid.NewGuid(), NormalizeName(name), AcousticScenarioCloner.Clone(state), now, now);
    }

    public static AcousticScenario Duplicate(
        AcousticScenario source,
        string? name = null,
        DateTime? utcNow = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        var now = utcNow ?? DateTime.UtcNow;
        return new AcousticScenario(
            Guid.NewGuid(),
            NormalizeName(name, $"{source.Name} Kopyası"),
            AcousticScenarioCloner.Clone(source.State),
            now,
            now);
    }

    public static AcousticScenario Rename(
        AcousticScenario source,
        string name,
        DateTime? utcNow = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source with
        {
            Name = NormalizeName(name),
            UpdatedAtUtc = utcNow ?? DateTime.UtcNow
        };
    }

    public static IReadOnlyList<AcousticScenario> Delete(
        IEnumerable<AcousticScenario> scenarios,
        Guid scenarioId) =>
        scenarios.Where(s => s.Id != scenarioId)
            .Select(AcousticScenarioCloner.Clone)
            .ToArray();

    public static AcousticScenario UpdateState(
        AcousticScenario source,
        AcousticScenarioState state,
        DateTime? utcNow = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(state);
        return source with
        {
            State = AcousticScenarioCloner.Clone(state),
            UpdatedAtUtc = utcNow ?? DateTime.UtcNow
        };
    }

    public static AcousticScenario? Find(
        IEnumerable<AcousticScenario> scenarios,
        Guid? scenarioId) =>
        scenarioId is null ? null : scenarios.FirstOrDefault(s => s.Id == scenarioId.Value);

    private static string NormalizeName(string? name, string fallback = "Yeni Senaryo") =>
        string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();
}

/// <summary>
/// Snapshot sınırında tüm mutable koleksiyonları ayırır. Bu yardımcı hem dosya
/// servisinde hem de ViewModel senaryo geçişlerinde aynı kopyalama semantiğini sağlar.
/// </summary>
public static class AcousticScenarioCloner
{
    public static AcousticScenarioState FromProject(AcousticsProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return Clone(new AcousticScenarioState
        {
            Surfaces = project.Surfaces,
            Sources = project.Sources,
            Receivers = project.Receivers,
            CategoryMaterials = project.CategoryMaterials,
            SurfaceMaterials = project.SurfaceMaterials,
            PanelKonfigurasyon = project.PanelKonfigurasyon,
            SimulationSettings = project.SimulationSettings,
            LastResult = project.LastResult,
            SelectedRoomType = project.SelectedRoomType,
            RoomVolume = project.RoomVolume,
            VolumeOrigin = project.VolumeOrigin
        });
    }

    public static AcousticsProject ApplyToProject(
        AcousticsProject project,
        AcousticScenarioState state)
    {
        ArgumentNullException.ThrowIfNull(project);
        var snapshot = Clone(state);
        return project with
        {
            Surfaces = snapshot.Surfaces,
            Sources = snapshot.Sources,
            Receivers = snapshot.Receivers,
            CategoryMaterials = snapshot.CategoryMaterials,
            SurfaceMaterials = snapshot.SurfaceMaterials,
            PanelKonfigurasyon = snapshot.PanelKonfigurasyon,
            SimulationSettings = snapshot.SimulationSettings,
            LastResult = snapshot.LastResult,
            SelectedRoomType = snapshot.SelectedRoomType,
            RoomVolume = snapshot.RoomVolume,
            VolumeOrigin = snapshot.VolumeOrigin
        };
    }

    public static AcousticScenario Clone(AcousticScenario source) =>
        source with { State = Clone(source.State) };

    public static AcousticScenarioState Clone(AcousticScenarioState source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new AcousticScenarioState
        {
            Surfaces = source.Surfaces?.Where(s => s is not null)
                .Select(s => s with { Vertices = s.Vertices?.ToArray() ?? [] }).ToArray() ?? [],
            Sources = source.Sources?.Where(p => p is not null).ToArray() ?? [],
            Receivers = source.Receivers?.Where(p => p is not null).ToArray() ?? [],
            CategoryMaterials = new Dictionary<string, string>(
                source.CategoryMaterials ?? new Dictionary<string, string>(),
                StringComparer.OrdinalIgnoreCase),
            SurfaceMaterials = new Dictionary<string, string>(
                source.SurfaceMaterials ?? new Dictionary<string, string>(),
                StringComparer.OrdinalIgnoreCase),
            PanelKonfigurasyon = ClonePanelConfiguration(source.PanelKonfigurasyon),
            SimulationSettings = CloneSimulationParameters(source.SimulationSettings),
            LastResult = CloneSimulationResult(source.LastResult),
            SelectedRoomType = source.SelectedRoomType,
            RoomVolume = source.RoomVolume,
            VolumeOrigin = source.VolumeOrigin
        };
    }

    public static AcousticPanelKonfigurasyon? ClonePanelConfiguration(AcousticPanelKonfigurasyon? source)
    {
        if (source is null) return null;
        return new AcousticPanelKonfigurasyon
        {
            OturumId = source.OturumId,
            SahneOnuYuzeyId = source.SahneOnuYuzeyId,
            PanelEniM = source.PanelEniM,
            PanelEniModu = source.PanelEniModu,
            PanelEnleriM = source.PanelEnleriM?.ToList() ?? [],
            SarkmaCikmaM = source.SarkmaCikmaM,
            AktifTipler = source.AktifTipler?.ToArray() ?? [],
            TavanYuzeyId = source.TavanYuzeyId,
            SolDuvarYuzeyId = source.SolDuvarYuzeyId,
            SagDuvarYuzeyId = source.SagDuvarYuzeyId,
            Paneller = source.Paneller?.Where(p => p is not null).Select(panel => new AcousticPanel
            {
                Id = panel.Id,
                Tip = panel.Tip,
                DilimNo = panel.DilimNo,
                PanelEniM = panel.PanelEniM,
                SarkmaCikmaM = panel.SarkmaCikmaM,
                AciDerece = panel.AciDerece,
                MaterialId = panel.MaterialId,
                Vertices = panel.Vertices?.ToList() ?? []
            }).ToList() ?? []
        };
    }

    public static SimulationResult? CloneSimulationResult(SimulationResult? source)
    {
        if (source is null) return null;
        return new SimulationResult
        {
            Echograms = source.Echograms?.Where(e => e is not null).Select(e => new EchogramData(
                e.SourceId, e.ReceiverId, e.SourceLabel, e.ReceiverLabel, e.Arrivals)
            {
                StoredArrivalCount = e.StoredArrivalCount
            }).ToArray() ?? [],
            Parameters = CloneSimulationParameters(source.Parameters) ?? new SimulationParameters(),
            Elapsed = source.Elapsed,
            ComputedAt = source.ComputedAt,
            GridMap = CloneGridMap(source.GridMap),
            Reliability = CloneReliability(source.Reliability),
            ComputedParameters = source.ComputedParameters?.Where(p => p is not null)
                .Select(CloneAcousticParameters).ToArray(),
            InputFingerprint = source.InputFingerprint,
            IsStale = source.IsStale,
            StaleReason = source.StaleReason
        };
    }

    private static SimulationParameters? CloneSimulationParameters(SimulationParameters? source) =>
        source is null
            ? null
            : source with
            {
                ReceiverGrid = source.ReceiverGrid is null ? null : source.ReceiverGrid with { }
            };

    private static AcousticParameters CloneAcousticParameters(AcousticParameters source) => new()
    {
        SourceId = source.SourceId,
        ReceiverId = source.ReceiverId,
        SourceLabel = source.SourceLabel,
        ReceiverLabel = source.ReceiverLabel,
        IACC = source.IACC,
        STI = source.STI,
        Bands = source.Bands.Select(b => b with
        {
            SchroederCurve = b.SchroederCurve?.ToArray() ?? []
        }).ToArray()
    };

    private static GridMapResult? CloneGridMap(GridMapResult? source)
    {
        if (source is null) return null;
        return new GridMapResult(
            source.Grid with { }, source.Cols, source.Rows,
            CloneJagged(source.Rt60), CloneJagged(source.C80),
            CloneJagged(source.C50), CloneJagged(source.Sti));
    }

    private static double[][] CloneJagged(double[][]? values) =>
        values?.Select(row => row?.ToArray() ?? []).ToArray() ?? [];

    private static SimulationReliabilityReport? CloneReliability(SimulationReliabilityReport? source) =>
        source is null
            ? null
            : new SimulationReliabilityReport(
                source.ConfidenceScore,
                source.ConfidenceLabel,
                source.Summary,
                source.Warnings?.ToArray(),
                source.Recommendations?.ToArray(),
                source.Metrics is null
                    ? null
                    : new Dictionary<string, double>(source.Metrics, StringComparer.OrdinalIgnoreCase));
}

public sealed record AcousticScenarioComparisonDto(
    Guid? ScenarioId,
    string Name,
    bool IsBaseline,
    bool HasResult,
    bool IsStale,
    DateTime? ComputedAt,
    double? Rt60Mid,
    double? Sti,
    double? C50,
    double? C80,
    double? G);

public static class AcousticScenarioComparisonService
{
    public static IReadOnlyList<AcousticScenarioComparisonDto> Compare(AcousticsProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var rows = new List<AcousticScenarioComparisonDto>
        {
            Build(null, "Ana Model", true, project.LastResult)
        };
        rows.AddRange(project.Scenarios.Select(s => Build(s.Id, s.Name, false, s.State.LastResult)));
        return rows;
    }

    private static AcousticScenarioComparisonDto Build(
        Guid? id,
        string name,
        bool baseline,
        SimulationResult? result)
    {
        var values = result?.ComputedParameters?.Where(p => p is not null).ToArray() ?? [];
        return new AcousticScenarioComparisonDto(
            id,
            name,
            baseline,
            result is not null && values.Length > 0,
            result?.IsStale ?? false,
            result?.ComputedAt,
            Average(values.Select(p => p.RT60_Mid)),
            Average(values.Select(p => p.STI)),
            AverageMidBands(values, b => b.C50),
            AverageMidBands(values, b => b.C80),
            AverageMidBands(values, b => b.G));
    }

    private static double? AverageMidBands(
        IEnumerable<AcousticParameters> parameters,
        Func<BandParameters, double> selector) =>
        Average(parameters.SelectMany(p => p.Bands)
            .Where(b => b.FrequencyHz is 500 or 1000)
            .Select(selector));

    private static double? Average(IEnumerable<double> values)
    {
        var finite = values.Where(double.IsFinite).ToArray();
        return finite.Length == 0 ? null : finite.Average();
    }
}
