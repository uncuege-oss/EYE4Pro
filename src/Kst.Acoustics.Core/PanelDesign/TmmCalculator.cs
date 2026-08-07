using System;
using System.Collections.Generic;
using System.Numerics;

namespace Kst.Acoustics.Core.PanelDesign;

/// <summary>
/// Calculates acoustic absorption and impedance using the Transfer Matrix Method (TMM).
/// </summary>
public static class TmmCalculator
{
    // Characteristic impedance of air: Z0 = rho0 * c0 (approx. 1.2 * 343 = 411.6 to 415 Rayl)
    // We will use 413.5 Rayl as a standard reference at 20 deg C.
    public const double Z0 = 413.5; 
    private const double ImpedanceEpsilon = 1e-12;
    private const double RigidImpedance = 1e30;
    private static readonly int[] OctaveCenters = { 63, 125, 250, 500, 1000, 2000, 4000, 8000 };
    private static readonly int[] ThirdOctaveCenters = { 100, 125, 160, 200, 250, 315, 400, 500, 630, 800, 1000, 1250, 1600, 2000, 2500, 3150, 4000, 5000 };

    public readonly record struct AlphaWResult(double Value, string ShapeIndicators);

    /// <summary>
    /// Calculates the normal-incidence absorption coefficient for a stack of layers 
    /// backed by a rigid wall at a specific frequency.
    /// </summary>
    /// <param name="layers">The ordered list of layers, from front (incident) to back (rigid wall).</param>
    /// <param name="frequency">The frequency in Hertz.</param>
    /// <returns>Normal incidence absorption coefficient (alpha) between 0 and 1.</returns>
    public static double CalculateNormalAbsorption(IEnumerable<AcousticLayer> layers, double frequency)
    {
        if (frequency <= 0 || double.IsNaN(frequency) || double.IsInfinity(frequency))
        {
            return 0.0;
        }

        var surfaceImpedance = CalculateSurfaceImpedance(layers, frequency);

        if (!IsFinite(surfaceImpedance))
        {
            return 0.0;
        }
        
        // Calculate reflection coefficient: R = (Zs - Z0) / (Zs + Z0)
        var denominator = surfaceImpedance + Z0;
        if (denominator.Magnitude < ImpedanceEpsilon)
        {
            return 0.0;
        }

        var R = (surfaceImpedance - Z0) / denominator;
        if (!IsFinite(R))
        {
            return 0.0;
        }

        // Alpha = 1 - |R|^2
        var alpha = 1.0 - Math.Pow(R.Magnitude, 2);
        
        // Clamp to 0..1 to handle tiny float errors
        return Math.Clamp(alpha, 0.0, 1.0);
    }

    /// <summary>
    /// Calculates a diffuse-field/random-incidence estimate using a locally reacting surface approximation.
    /// </summary>
    public static double CalculateRandomIncidenceAbsorption(IEnumerable<AcousticLayer> layers, double frequency)
    {
        if (frequency <= 0 || double.IsNaN(frequency) || double.IsInfinity(frequency))
        {
            return 0.0;
        }

        var surfaceImpedance = CalculateSurfaceImpedance(layers, frequency);
        if (!IsFinite(surfaceImpedance))
        {
            return 0.0;
        }

        return CalculateRandomIncidenceAbsorptionFromSurfaceImpedance(surfaceImpedance);
    }

    public static double CalculateRandomIncidenceAbsorptionFromSurfaceImpedance(Complex surfaceImpedance)
    {
        if (!IsFinite(surfaceImpedance))
        {
            return 0.0;
        }

        var normalizedImpedance = surfaceImpedance / Z0;
        const int steps = 36;
        double weightedAbsorption = 0.0;
        double weightSum = 0.0;

        for (int i = 0; i < steps; i++)
        {
            double theta = (i + 0.5) * Math.PI / (2.0 * steps);
            double cosTheta = Math.Cos(theta);
            double sinTheta = Math.Sin(theta);
            double weight = sinTheta * cosTheta;

            var denominator = normalizedImpedance * cosTheta + 1.0;
            if (denominator.Magnitude < ImpedanceEpsilon)
            {
                continue;
            }

            var reflection = (normalizedImpedance * cosTheta - 1.0) / denominator;
            double absorption = IsFinite(reflection)
                ? Math.Clamp(1.0 - reflection.Magnitude * reflection.Magnitude, 0.0, 1.0)
                : 0.0;

            weightedAbsorption += absorption * weight;
            weightSum += weight;
        }

        return weightSum > 0 ? Math.Clamp(weightedAbsorption / weightSum, 0.0, 1.0) : 0.0;
    }

    /// <summary>
    /// Calculates absorption for standard 1/1 octave bands (63Hz to 8kHz).
    /// </summary>
    public static Dictionary<int, double> CalculateOctaveBands(IEnumerable<AcousticLayer> layers)
    {
        return CalculateBands(layers, OctaveCenters, CalculateNormalAbsorption);
    }

    /// <summary>
    /// Calculates diffuse-field/random-incidence octave-band estimates for room-acoustics material use.
    /// </summary>
    public static Dictionary<int, double> CalculateRandomIncidenceOctaveBands(IEnumerable<AcousticLayer> layers)
    {
        return CalculateBands(layers, OctaveCenters, CalculateRandomIncidenceAbsorption);
    }

    /// <summary>
    /// Calculates absorption at standard third-octave center frequencies from 100 Hz to 5 kHz.
    /// </summary>
    public static Dictionary<int, double> CalculateThirdOctaveBands(IEnumerable<AcousticLayer> layers)
    {
        return CalculateBands(layers, ThirdOctaveCenters, CalculateNormalAbsorption);
    }

    public static Dictionary<int, double> CalculateRandomIncidenceThirdOctaveBands(IEnumerable<AcousticLayer> layers)
    {
        return CalculateBands(layers, ThirdOctaveCenters, CalculateRandomIncidenceAbsorption);
    }

    /// <summary>
    /// Calculates the Noise Reduction Coefficient (NRC).
    /// Average of absorption at 250, 500, 1000, and 2000 Hz, rounded to nearest 0.05.
    /// </summary>
    public static double CalculateNRC(IEnumerable<AcousticLayer> layers)
    {
        double a250 = CalculateRandomIncidenceAbsorption(layers, 250);
        double a500 = CalculateRandomIncidenceAbsorption(layers, 500);
        double a1000 = CalculateRandomIncidenceAbsorption(layers, 1000);
        double a2000 = CalculateRandomIncidenceAbsorption(layers, 2000);

        double average = (a250 + a500 + a1000 + a2000) / 4.0;
        
        // Round to nearest 0.05
        return Math.Round(average * 20) / 20.0;
    }

    /// <summary>
    /// Calculates a pragmatic ISO 11654-style weighted absorption coefficient from third-octave data.
    /// </summary>
    public static double CalculateAlphaW(IEnumerable<AcousticLayer> layers)
    {
        return CalculateAlphaWResult(layers).Value;
    }

    public static AlphaWResult CalculateAlphaWResult(IEnumerable<AcousticLayer> layers)
    {
        var thirdOctaves = CalculateRandomIncidenceThirdOctaveBands(layers);
        return CalculateAlphaWResultFromThirdOctaves(thirdOctaves);
    }

    public static AlphaWResult CalculateAlphaWResultFromThirdOctaves(IReadOnlyDictionary<int, double> thirdOctaves)
    {
        var octaveValues = IsoReferenceFrequencies
            .Select(f => CalculatePracticalCoefficient(thirdOctaves, f))
            .ToArray();

        return CalculateAlphaWResultFromPracticalOctaves(octaveValues);
    }

    private static AlphaWResult CalculateAlphaWResultFromPracticalOctaves(IReadOnlyList<double> octaveValues)
    {
        double alphaW = 0.0;
        for (double candidate = 1.0; candidate >= 0.0; candidate -= 0.05)
        {
            double unfavorableSum = 0.0;
            for (int i = 0; i < IsoReferenceCurve.Length; i++)
            {
                double reference = IsoReferenceCurve[i] + candidate;
                double deficit = reference - octaveValues[i];
                if (deficit > 0)
                {
                    unfavorableSum += deficit;
                }
            }

            if (unfavorableSum <= 0.10 + 1e-9)
            {
                alphaW = Math.Round(candidate * 20.0) / 20.0;
                break;
            }
        }

        return new AlphaWResult(alphaW, CalculateShapeIndicators(octaveValues, alphaW));
    }

    public static string CalculateAbsorptionClass(double alphaW)
    {
        return alphaW switch
        {
            >= 0.90 => "A",
            >= 0.80 => "B",
            >= 0.60 => "C",
            >= 0.30 => "D",
            >= 0.15 => "E",
            _ => "-"
        };
    }

    /// <summary>
    /// Calculates the surface impedance of a stack of layers backed by a rigid wall.
    /// </summary>
    public static Complex CalculateSurfaceImpedance(IEnumerable<AcousticLayer> layers, double frequency)
    {
        // For a single boundary, total transfer matrix T_total = T_front * T_next * ... * T_back
        // Identity matrix to start
        var totalMatrix = new TransferMatrix(1, 0, 0, 1);

        foreach (var layer in layers)
        {
            var layerMatrix = layer.GetTransferMatrix(frequency);
            totalMatrix *= layerMatrix;
        }

        // For rigid backing, particle velocity at the end is 0 (v = 0).
        // Transfer matrix equation: [P_front, V_front]^T = T_total * [P_back, 0]^T
        // Thus:
        // P_front = T11 * P_back
        // V_front = T21 * P_back
        // Surface impedance Zs = P_front / V_front = T11 / T21

        // Handle division by zero edge-cases (e.g. perfectly rigid surface with 0 thickness)
        if (totalMatrix.T21.Magnitude < ImpedanceEpsilon)
        {
            return new Complex(RigidImpedance, 0);
        }

        var impedance = totalMatrix.T11 / totalMatrix.T21;
        return IsFinite(impedance) ? impedance : new Complex(RigidImpedance, 0);
    }

    private static readonly int[] IsoReferenceFrequencies = { 250, 500, 1000, 2000, 4000 };
    private static readonly double[] IsoReferenceCurve = { -0.20, 0.00, 0.00, 0.00, -0.10 };

    private static Dictionary<int, double> CalculateBands(
        IEnumerable<AcousticLayer> layers,
        IEnumerable<int> centers,
        Func<IEnumerable<AcousticLayer>, double, double> calculator)
    {
        var layerList = layers.ToList();
        var results = new Dictionary<int, double>();

        foreach (var f in centers)
        {
            results[f] = calculator(layerList, f);
        }

        return results;
    }

    private static double CalculatePracticalCoefficient(IReadOnlyDictionary<int, double> thirdOctaves, int octaveCenter)
    {
        int lower = (int)Math.Round(octaveCenter / Math.Pow(2, 1.0 / 3.0));
        int upper = (int)Math.Round(octaveCenter * Math.Pow(2, 1.0 / 3.0));

        int[] standard = octaveCenter switch
        {
            250 => new[] { 200, 250, 315 },
            500 => new[] { 400, 500, 630 },
            1000 => new[] { 800, 1000, 1250 },
            2000 => new[] { 1600, 2000, 2500 },
            4000 => new[] { 3150, 4000, 5000 },
            _ => new[] { lower, octaveCenter, upper }
        };

        var values = standard
            .Where(thirdOctaves.ContainsKey)
            .Select(f => thirdOctaves[f])
            .ToArray();

        if (values.Length == 0)
        {
            return 0.0;
        }

        double roundedMean = Math.Round(values.Average(), 2, MidpointRounding.AwayFromZero);
        return Math.Min(1.0, Math.Round(roundedMean * 20.0, MidpointRounding.AwayFromZero) / 20.0);
    }

    private static string CalculateShapeIndicators(IReadOnlyList<double> octaveValues, double alphaW)
    {
        var indicators = new List<char>();

        for (int i = 0; i < octaveValues.Count && i < IsoReferenceCurve.Length; i++)
        {
            double reference = IsoReferenceCurve[i] + alphaW;
            if (octaveValues[i] - reference < 0.25)
            {
                continue;
            }

            char indicator = i switch
            {
                0 => 'L',
                1 or 2 => 'M',
                _ => 'H'
            };

            if (!indicators.Contains(indicator))
            {
                indicators.Add(indicator);
            }
        }

        return new string(indicators.ToArray());
    }

    private static bool IsFinite(Complex value)
    {
        return double.IsFinite(value.Real) && double.IsFinite(value.Imaginary);
    }
}
