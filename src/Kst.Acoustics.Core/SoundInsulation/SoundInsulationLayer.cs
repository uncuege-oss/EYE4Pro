namespace Kst.Acoustics.Core.SoundInsulation;

/// <summary>
/// Represents a specific layer in a wall assembly with a defined thickness.
/// </summary>
public sealed record SoundInsulationLayer(
    SoundInsulationMaterial Material,
    double Thickness,               // in meters
    bool IsAirGap = false,
    bool HasPorousFill = false
)
{
    public double SurfaceMass => Material.GetSurfaceMass(Thickness);
    
    public string DisplayThickness => $"{Thickness * 1000:F1} mm";
}
