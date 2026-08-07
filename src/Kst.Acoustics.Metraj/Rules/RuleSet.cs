using System.Text.Json;
using System.Text.Json.Serialization;
using Kst.Acoustics.Metraj.Filters;
using Kst.Acoustics.Metraj.Metrics;

namespace Kst.Acoustics.Metraj.Rules;

public class MetrajRule
{
    public string Name { get; set; } = "Yeni Kural";
    public string? LayerPattern { get; set; }
    public short? ColorAci { get; set; }
    public byte? ColorR { get; set; }
    public byte? ColorG { get; set; }
    public byte? ColorB { get; set; }
    public string? TextPattern { get; set; }
    public FilterLogic Combinator { get; set; } = FilterLogic.And;
}

public class RuleSet
{
    public List<string> TemplatePaths { get; set; } = new();
    public List<MetrajRule> Rules { get; set; } = new();
    public FilterLogic RulesCombinator { get; set; } = FilterLogic.Or;
    public double LengthTolerance { get; set; } = 2.0;
    public double AngleTolerance { get; set; } = 5.0;
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MetrajUnit TargetUnit { get; set; } = MetrajUnit.Millimeters;

    public bool OnlyTemplates { get; set; } = false;

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    public static RuleSet? FromJson(string json) => JsonSerializer.Deserialize<RuleSet>(json);
}
