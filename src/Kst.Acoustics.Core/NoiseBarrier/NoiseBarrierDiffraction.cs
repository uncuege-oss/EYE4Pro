using System;

namespace Kst.Acoustics.Core.NoiseBarrier;

public static class NoiseBarrierDiffraction
{
    public const double DefaultSpeedOfSound = 343.0;

    public static double CalculateFresnelNumber(double pathDifferenceMeters, double frequencyHz, double speedOfSound = DefaultSpeedOfSound)
    {
        if (pathDifferenceMeters <= 0 || frequencyHz <= 0 || speedOfSound <= 0)
            return 0;

        return 2.0 * pathDifferenceMeters * frequencyHz / speedOfSound;
    }

    public static double CalculateMaekawaAttenuation(double pathDifferenceMeters, double frequencyHz, double speedOfSound = DefaultSpeedOfSound)
    {
        double fresnel = CalculateFresnelNumber(pathDifferenceMeters, frequencyHz, speedOfSound);
        if (fresnel <= 0)
            return 0;

        return 10.0 * Math.Log10(3.0 + 20.0 * fresnel);
    }

    public static double CalculateKurzeAndersonAttenuation(double pathDifferenceMeters, double frequencyHz, double speedOfSound = DefaultSpeedOfSound)
    {
        double fresnel = CalculateFresnelNumber(pathDifferenceMeters, frequencyHz, speedOfSound);
        if (fresnel <= 0)
            return 0;

        double x = Math.Sqrt(2.0 * Math.PI * fresnel);
        double tanh = Math.Tanh(x);
        if (tanh <= 1e-12)
            return 0;

        return 5.0 + 20.0 * Math.Log10(x / tanh);
    }

    public static double CalculateIso9613_2Attenuation(double pathDifferenceMeters, double frequencyHz, double speedOfSound = DefaultSpeedOfSound)
    {
        double lambda = speedOfSound / frequencyHz;
        // Dz = 10 log10 [3 + (20/lambda) * delta * Kmet] 
        // Kmet = 1.0 (neutral conditions for engineering preview)
        if (pathDifferenceMeters <= 0) return 0;
        return 10.0 * Math.Log10(3.0 + (20.0 / lambda) * pathDifferenceMeters);
    }

    public static double CalculateEngineeringPreviewAttenuation(double pathDifferenceMeters, double frequencyHz, double speedOfSound = DefaultSpeedOfSound)
    {
        return Math.Clamp(CalculateKurzeAndersonAttenuation(pathDifferenceMeters, frequencyHz, speedOfSound), 0.0, 30.0);
    }
}
