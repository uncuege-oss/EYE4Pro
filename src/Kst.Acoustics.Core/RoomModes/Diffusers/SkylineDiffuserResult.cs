using System.Collections.Generic;
using System.Linq;

namespace Kst.Acoustics.Core.RoomModes.Diffusers;

public class SkylineDiffuserResult
{
    public DiffuserSuitability Suitability { get; set; } = DiffuserSuitability.Suitable;

    public string Recommendation { get; set; } = string.Empty;

    public double TargetFrequencyHz { get; set; }

    public double LowerFrequencyHz { get; set; }

    public double UpperFrequencyHz { get; set; }

    public int PrimeOrder { get; set; }

    public double CellSizeMm { get; set; }

    public double PanelWidthMm { get; set; }

    public double PanelHeightMm { get; set; }

    public double MaxDepthMm { get; set; }

    public List<SkylineCell> Cells { get; set; } = new();

    public List<string> Warnings { get; set; } = new();

    public string WorkingBandText => $"{LowerFrequencyHz:F0} - {UpperFrequencyHz:F0} Hz";

    public string PanelSizeText => $"{PanelWidthMm:F0} x {PanelHeightMm:F0} x {MaxDepthMm:F0} mm";

    public int CellCount => Cells.Count;

    public string SuitabilityText => Suitability switch
    {
        DiffuserSuitability.Suitable => "Uygun",
        DiffuserSuitability.Conditional => "Koşullu",
        _ => "Önerilmez"
    };

    public string WarningText => Warnings.Count == 0
        ? "Uyarı yok."
        : string.Join(" ", Warnings);

    public double AverageDepthMm => Cells.Count == 0 ? 0 : Cells.Average(c => c.DepthMm);
}
