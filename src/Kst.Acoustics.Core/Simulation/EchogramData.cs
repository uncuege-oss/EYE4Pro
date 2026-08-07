using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace Kst.Acoustics.Core.Simulation;

/// <summary>Tek bir ışın–alıcı çarpışmasını temsil eder.</summary>
public readonly record struct EchogramArrival(
    double TimeSeconds,
    double E63,  double E125, double E250, double E500,
    double E1k,  double E2k,  double E4k,  double E8k,
    double DirX, double DirY, double DirZ);

/// <summary>Bir kaynak–alıcı çifti için bütün gelişlerin listesi.</summary>
public sealed class EchogramData
{
    public int    SourceId      { get; init; }
    public int    ReceiverId    { get; init; }
    public string SourceLabel   { get; init; } = "";
    public string ReceiverLabel { get; init; } = "";

    private readonly ConcurrentBag<EchogramArrival> _bag = new();
    private List<EchogramArrival>? _finalArrivals;
    private int _storedArrivalCount;

    /// <summary>
    /// JSON serialization/deserialization için public init-only property. Set sırasında
    /// _bag'a yazar; get sırasında sıralı liste veya bag snapshot'ı döner.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<EchogramArrival> Arrivals
    {
        get => _finalArrivals ?? _bag.ToList();
        init
        {
            if (value is null) return;
            foreach (var a in value) _bag.Add(a);
        }
    }

    /// <summary>
    /// Proje dosyasında ham gelişler saklanmasa bile sonucun kaç gelişten
    /// hesaplandığını korur. Bu sayı IR üretilebildiği anlamına gelmez.
    /// </summary>
    public int StoredArrivalCount
    {
        get => Math.Max(_storedArrivalCount, _finalArrivals?.Count ?? _bag.Count);
        init => _storedArrivalCount = Math.Max(0, value);
    }

    /// <summary>Ham gelişler bellekteyse IR/auralizasyon üretilebilir.</summary>
    [JsonIgnore]
    public bool HasImpulseResponseData => (_finalArrivals?.Count ?? _bag.Count) > 0;

    public EchogramData() { }

    public EchogramData(int sourceId, int receiverId, string sourceLabel, string receiverLabel,
        IEnumerable<EchogramArrival>? arrivals = null)
    {
        SourceId      = sourceId;
        ReceiverId    = receiverId;
        SourceLabel   = sourceLabel;
        ReceiverLabel = receiverLabel;
        if (arrivals is not null)
            foreach (var a in arrivals) _bag.Add(a);
    }

    internal void AddArrival(EchogramArrival a) => _bag.Add(a);

    internal void SortArrivals()
    {
        _finalArrivals = _bag.ToList();
        _finalArrivals.Sort((a, b) => a.TimeSeconds.CompareTo(b.TimeSeconds));
        _storedArrivalCount = _finalArrivals.Count;
    }
}
