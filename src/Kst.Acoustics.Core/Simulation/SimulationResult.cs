using System.Text.Json.Serialization;

namespace Kst.Acoustics.Core.Simulation;

public sealed class SimulationResult
{
    // Default ctor + init-only property pattern — STJ deserialization için en güvenli
    // yöntem. [JsonConstructor] kullanıldığında ctor parametre adlarının property'lerle
    // case-insensitive eşleşmesi gerekir; .NET sürüm/optional parametre uyumsuzlukları
    // "must bind to an object property or field" hatasına yol açıyordu.
    public SimulationResult() { }

    public SimulationResult(IReadOnlyList<EchogramData> echograms, SimulationParameters parameters, TimeSpan elapsed)
    {
        Echograms  = echograms;
        Parameters = parameters;
        Elapsed    = elapsed;
        InputFingerprint = parameters.InputFingerprint;
    }

    public IReadOnlyList<EchogramData> Echograms { get; init; } = Array.Empty<EchogramData>();
    public SimulationParameters Parameters { get; init; } = new SimulationParameters();
    public TimeSpan Elapsed { get; init; }
    public DateTime ComputedAt { get; init; } = DateTime.UtcNow;
    public GridMapResult? GridMap { get; init; }
    public SimulationReliabilityReport? Reliability { get; init; }
    public IReadOnlyList<AcousticParameters>? ComputedParameters { get; init; }
    public string? InputFingerprint { get; init; }

    /// <summary>Girdi değiştirildikten sonra eski sonucun yanlışlıkla güncel sanılmasını engeller.</summary>
    public bool IsStale { get; set; }
    public string? StaleReason { get; set; }

    [JsonIgnore]
    public int TotalArrivals => Echograms.Sum(e => e.StoredArrivalCount);

    [JsonIgnore]
    public bool HasImpulseResponseData => Echograms.Any(e => e.HasImpulseResponseData);
}
