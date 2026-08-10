using System.Text.Json.Serialization;

namespace Kst.Acoustics.Core.Projects;

/// <summary>
/// Frekansa bağlı yüzey emilim materyali.
/// Alpha dizisi: [63, 125, 250, 500, 1k, 2k, 4k, 8k] Hz.
/// Scattering dizisi: [63, 125, 250, 500, 1k, 2k, 4k, 8k] Hz.
/// </summary>
[method: JsonConstructor]
public sealed record AcousticMaterial(
    string Id,
    string DisplayName,
    double[] Alpha,
    double[] Scattering,
    string Category = "",
    string Note = "",
    string Reference = "",
    string ScatteringDataType = "")
{
    // Geriye-dönük uyumluluk: tek scattering değeri → 8 banda tekrar
    public AcousticMaterial(
        string id,
        string displayName,
        double[] alpha,
        double scattering,
        string category = "",
        string note = "",
        string reference = "",
        string scatteringDataType = "")
        : this(
            id,
            displayName,
            alpha,
            Enumerable.Repeat(scattering, 8).ToArray(),
            category,
            note,
            reference,
            scatteringDataType) { }

    public double ScatteringMid => Scattering.Length > 4 ? Scattering[4] : Scattering[0];

    public override string ToString() => DisplayName;
}
