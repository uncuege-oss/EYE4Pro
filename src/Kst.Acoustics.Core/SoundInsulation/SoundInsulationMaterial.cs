using System;

namespace Kst.Acoustics.Core.SoundInsulation;

/// <summary>
/// Represents a material with physical properties required for sound insulation (TL) calculations.
/// </summary>
public class SoundInsulationMaterial
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string SubCategory { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public string Source { get; set; } = "Literature";
    public double Density { get; set; } = 0;             // kg/m3
    public double YoungModulus { get; set; } = 0;        // Pa (Elasticity)
    public double PoissonRatio { get; set; } = 0.3;      // typical for most solids
    public double LossFactor { get; set; } = 0.01;       // internal damping (eta)
    public double FlowResistivity { get; set; } = 0;     // Pa.s/m2 (for porous materials)
    public double Porosity { get; set; } = 0.99;         // for porous materials
    public double Tortuosity { get; set; } = 1.0;        // for porous materials
    public double DynamicStiffness { get; set; } = 0;    // MN/m3 (for resilient layers)

    /// <summary>
    /// Johnson-Champoux-Allard viskoz karakteristik uzunluk (μm).
    /// 0 verilirse <see cref="GetViscousLengthUm"/> üzerinden FlowResistivity'den türetilir.
    /// </summary>
    public double ViscousLengthUm { get; set; } = 0;

    /// <summary>
    /// JCA termal karakteristik uzunluk (μm). 0 verilirse Λ′ ≈ 2·Λ varsayımı kullanılır.
    /// </summary>
    public double ThermalLengthUm { get; set; } = 0;
    public string Note { get; set; } = "";
    public string Reliability { get; set; } = "Orta";
    public string Reference { get; set; } = "";
    public bool IsFavorite { get; set; } = false;
    public bool IsBuiltIn { get; set; } = false;

    public SoundInsulationMaterial() { }

    public SoundInsulationMaterial(
        string id, string name, string category, string subCategory = "", 
        double density = 0, double youngModulus = 0, double poissonRatio = 0.3, double lossFactor = 0.01,
        double flowResistivity = 0, bool isFavorite = false)
    {
        Id = id;
        Name = name;
        Category = category;
        SubCategory = subCategory;
        Density = density;
        YoungModulus = youngModulus;
        PoissonRatio = poissonRatio;
        LossFactor = lossFactor;
        FlowResistivity = flowResistivity;
        IsFavorite = isFavorite;
    }

    public double GetSurfaceMass(double thickness) => Density * thickness;

    public double EstimateCriticalFrequency(double thickness)
    {
        if (thickness <= 0 || YoungModulus <= 0 || Density <= 0) return 0;

        const double c = 343.0; // speed of sound in air
        double B = (YoungModulus * Math.Pow(thickness, 3)) / (12.0 * (1.0 - Math.Pow(PoissonRatio, 2)));
        double surfaceMass = Density * thickness;

        return (Math.Pow(c, 2) / (2.0 * Math.PI)) * Math.Sqrt(surfaceMass / B);
    }

    /// <summary>
    /// JCA viskoz karakteristik uzunluğu döndürür (m). Kullanıcı 0 girdiyse
    /// FlowResistivity'den türetir: Λ ≈ sqrt(8·α∞·η / (σ·φ)), η_hava = 1.84e-5 Pa·s.
    /// </summary>
    public double GetViscousLengthMeters()
    {
        if (ViscousLengthUm > 0) return ViscousLengthUm * 1e-6;
        if (FlowResistivity <= 0 || Porosity <= 0) return 0;
        const double eta = 1.84e-5;
        var lambdaM = Math.Sqrt(8.0 * Tortuosity * eta / (FlowResistivity * Porosity));
        return lambdaM;
    }

    /// <summary>
    /// JCA termal karakteristik uzunluğu (m). Kullanıcı 0 girdiyse Λ′ ≈ 2·Λ varsayımı.
    /// </summary>
    public double GetThermalLengthMeters()
    {
        if (ThermalLengthUm > 0) return ThermalLengthUm * 1e-6;
        var lambda = GetViscousLengthMeters();
        return lambda > 0 ? 2.0 * lambda : 0;
    }
}
