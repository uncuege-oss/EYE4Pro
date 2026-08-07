using System.Collections.Generic;

namespace Kst.Acoustics.Core.AbsorptiveSurface;

public static class AbsorptiveMaterialDatabase
{
    public static readonly double[] OctaveBands = { 63, 125, 250, 500, 1000, 2000, 4000, 8000 };

    private static readonly List<AbsorptiveMaterial> Materials =
    [
        Create("MW-48", "Taş Yünü 48 kg/m3", AbsorptiveLayerType.PorousAbsorber, 48, 18000, "A1", "İç mekan", "Tipik üretici verisi", 75,
            [0.10, 0.25, 0.55, 0.85, 0.95, 1.00, 1.00, 0.95]),
        Create("GW-32", "Cam Yünü 32 kg/m3", AbsorptiveLayerType.PorousAbsorber, 32, 12000, "A1", "İç mekan", "Tipik üretici verisi", 70,
            [0.08, 0.20, 0.50, 0.80, 0.95, 1.00, 1.00, 0.95]),
        Create("PET-40", "Polyester Elyaf Panel", AbsorptiveLayerType.PorousAbsorber, 40, 9000, "B-s1,d0", "İç mekan", "Tipik üretici verisi", 65,
            [0.05, 0.15, 0.40, 0.75, 0.90, 0.95, 0.95, 0.90]),
        Create("MEL-9", "Melamin Köpük", AbsorptiveLayerType.PorousAbsorber, 9, 8000, "B-s1,d0", "İç mekan", "Tipik üretici verisi", 65,
            [0.04, 0.12, 0.35, 0.70, 0.90, 0.95, 0.95, 0.90]),
        Create("FAB-AC", "Akustik Kumaş", AbsorptiveLayerType.Fabric, 0.3, 0, "B-s1,d0", "İç mekan", "Geçirgen kumaş varsayımı", 60,
            [0.01, 0.02, 0.04, 0.06, 0.08, 0.10, 0.10, 0.08]),
        Create("GY-PERF", "Delikli Alçıpan", AbsorptiveLayerType.PerforatedPanel, 750, 0, "A2-s1,d0", "İç mekan", "Mühendislik önizleme", 55,
            [0.03, 0.05, 0.12, 0.18, 0.22, 0.18, 0.12, 0.08]),
        Create("WOOD-SLOT", "Slotlu Ahşap Lamel", AbsorptiveLayerType.SlottedPanel, 600, 0, "D-s2,d0", "İç mekan", "Mühendislik önizleme", 50,
            [0.02, 0.04, 0.10, 0.16, 0.20, 0.18, 0.12, 0.08]),
        Create("CONC-BACK", "Beton/Rijit Arka Yüzey", AbsorptiveLayerType.RigidBacking, 2400, 0, "A1", "İç/dış mekan", "Malzeme varsayımı", 80,
            [0.01, 0.01, 0.01, 0.02, 0.02, 0.02, 0.02, 0.02]),
        Create("AIR", "Hava Boşluğu", AbsorptiveLayerType.AirGap, 1.2, 0, "-", "İç mekan", "Geometri katmanı", 90,
            [0, 0, 0, 0, 0, 0, 0, 0])
    ];

    public static List<AbsorptiveMaterial> GetAll() => new(Materials);

    public static AbsorptiveMaterial? GetByCode(string code) => Materials.Find(m => m.Code == code);

    private static AbsorptiveMaterial Create(
        string code,
        string name,
        AbsorptiveLayerType type,
        double density,
        double flowResistivity,
        string fireClass,
        string environmentUse,
        string source,
        int confidence,
        double[] alpha)
    {
        var material = new AbsorptiveMaterial
        {
            Code = code,
            Name = name,
            LayerType = type,
            Density = density,
            FlowResistivity = flowResistivity,
            FireClass = fireClass,
            EnvironmentUse = environmentUse,
            Source = source,
            Confidence = confidence
        };

        for (int i = 0; i < OctaveBands.Length && i < alpha.Length; i++)
            material.AbsorptionBands[OctaveBands[i]] = alpha[i];

        return material;
    }
}
