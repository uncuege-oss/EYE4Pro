using System;
using System.Collections.Generic;

namespace Kst.Acoustics.Core.Measurement.Processing;

public record TargetCurve(string Name, double[] Frequencies, double[] Levels);

public static class TargetCurveService
{
    public static TargetCurve GetHarmanCurve()
    {
        return new TargetCurve("Harman Target",
            new double[] { 20, 100, 1000, 20000 },
            new double[] { 6.0, 6.0, 0.0, -3.0 }); // Bas vurgulu, tiz sönümlü
    }

    public static TargetCurve GetFlatCurve()
    {
        return new TargetCurve("Flat",
            new double[] { 20, 20000 },
            new double[] { 0.0, 0.0 });
    }

    public static double GetTargetLevel(TargetCurve curve, double frequency)
    {
        if (frequency <= curve.Frequencies[0]) return curve.Levels[0];
        if (frequency >= curve.Frequencies[^1]) return curve.Levels[^1];

        for (int i = 0; i < curve.Frequencies.Length - 1; i++)
        {
            if (frequency >= curve.Frequencies[i] && frequency <= curve.Frequencies[i+1])
            {
                double t = (frequency - curve.Frequencies[i]) / (curve.Frequencies[i+1] - curve.Frequencies[i]);
                return curve.Levels[i] + t * (curve.Levels[i+1] - curve.Levels[i]);
            }
        }
        return 0;
    }
}
