using System.Collections.Generic;

namespace Kst.Acoustics.Core.SoundInsulation;

/// <summary>
/// Result of a sound insulation calculation.
/// </summary>
public sealed record SoundInsulationResult(
    WallAssembly Assembly,
    Dictionary<int, double> TransmissionLossValues, // Frequency -> dB
    List<string> Warnings,
    double MassAirMassResonance = 0,
    Dictionary<string, double>? CriticalFrequencies = null, // Layer index/name -> Hz
    RwRatingResult? RwRating = null,
    double ReliabilityScore = 100 // 0 to 100
)
{
    public bool HasMassAirMassResonance => MassAirMassResonance > 0;
}
