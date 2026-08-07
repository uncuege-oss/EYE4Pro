using System;
using System.Collections.Generic;

namespace Kst.Acoustics.Core.AbsorptiveSurface;

public enum AbsorptiveLayerType
{
    PorousAbsorber,
    AirGap,
    PerforatedPanel,
    SlottedPanel,
    Fabric,
    RigidBacking,
    DecorativeFinish
}

public class AbsorptiveMaterial
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AbsorptiveLayerType LayerType { get; set; } = AbsorptiveLayerType.PorousAbsorber;
    public double Density { get; set; }
    public double FlowResistivity { get; set; }
    public double Porosity { get; set; } = 0.95;
    public double SurfaceResistance { get; set; }
    public string FireClass { get; set; } = string.Empty;
    public string EnvironmentUse { get; set; } = "İç mekan";
    public string Source { get; set; } = string.Empty;
    public int Confidence { get; set; } = 70;
    public Dictionary<double, double> AbsorptionBands { get; set; } = new();

    public double GetAlpha(double frequency)
    {
        if (AbsorptionBands.TryGetValue(frequency, out var value))
            return Math.Clamp(value, 0.0, 1.0);

        return LayerType switch
        {
            AbsorptiveLayerType.PorousAbsorber => 0.45,
            AbsorptiveLayerType.Fabric => 0.08,
            AbsorptiveLayerType.DecorativeFinish => 0.03,
            AbsorptiveLayerType.PerforatedPanel => 0.12,
            AbsorptiveLayerType.SlottedPanel => 0.10,
            _ => 0.0
        };
    }
}

public class AbsorptiveLayer
{
    public int Index { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public AbsorptiveLayerType LayerType { get; set; } = AbsorptiveLayerType.PorousAbsorber;
    public double ThicknessMm { get; set; } = 25;
    public bool IsEnabled { get; set; } = true;
    public double OpenAreaPercent { get; set; } = 15;
    public double HoleDiameterMm { get; set; } = 6;
    public double SlotWidthMm { get; set; } = 8;
    public double SlotSpacingMm { get; set; } = 40;
    public AbsorptiveMaterial? Material { get; set; }
}

public class AbsorptiveSurfaceProject
{
    public string Name { get; set; } = "Yeni Ses Emici Yüzey";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public List<AbsorptiveLayer> Layers { get; set; } = new();
    public List<AbsorptiveMaterial> CustomMaterials { get; set; } = new();
    public AbsorptiveSurfaceResult? LastResult { get; set; }
}

public class AbsorptiveBandResult
{
    public double FrequencyHz { get; set; }
    public double Absorption { get; set; }
}

public class AbsorptiveSurfaceResult
{
    public Dictionary<double, double> AbsorptionBands { get; set; } = new();
    public double AlphaW { get; set; }
    public double Nrc { get; set; }
    public double Saa { get; set; }
    public double TotalThicknessMm { get; set; }
    public string PerformanceSummary { get; set; } = string.Empty;
    public string RecommendedUse { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = new();
    public List<AbsorptiveBandResult> BandRows { get; set; } = new();
}
