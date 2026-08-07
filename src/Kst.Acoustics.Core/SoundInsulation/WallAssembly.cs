using System.Collections.Generic;
using System.Linq;

namespace Kst.Acoustics.Core.SoundInsulation;

/// <summary>
/// Represents a complete wall or panel system composed of multiple layers.
/// </summary>
public sealed record WallAssembly(
    string Id,
    string Name,
    IReadOnlyList<SoundInsulationLayer> Layers,
    string Description = "",
    double StudSpacing = 0,          // meters (0 = no mechanical bridge)
    string ConnectionType = "None",  // None, Direct, ResilientChannel, Clips
    CalculationMethod Method = CalculationMethod.Empirical
)
{
    public double TotalThickness => Layers.Sum(l => l.Thickness);
    
    public double TotalSurfaceMass => Layers.Sum(l => l.SurfaceMass);

    public bool IsDoubleWall => Layers.Any(l => l.IsAirGap) &&
                                Layers.TakeWhile(l => !l.IsAirGap).Any() &&
                                Layers.SkipWhile(l => !l.IsAirGap).Skip(1).Any(l => !l.IsAirGap);
}
