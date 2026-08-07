using System;
using System.Collections.Generic;
using System.Linq;

namespace Kst.Acoustics.Core.SoundInsulation;

public sealed record RwRatingResult(
    int Rw,
    int C,
    int Ctr,
    List<int> LimitingFrequencies,
    List<int>? MissingFrequencies = null,
    int? Stc = null,
    int? Oitc = null
);

public static class RwRatingCalculator
{
    // ISO 717-1 Reference curve for 1/3 octave bands (100 Hz to 3150 Hz)
    // Values relative to 500 Hz (which is 0 dB in this relative array)
    private static readonly int[] RwReferenceRelative = 
    { 
        -19, -16, -13, -10, -7, -4, -1, 0, 1, 2, 3, 4, 4, 4, 4, 4 
    };

    private static readonly int[] RatingFrequencies = 
    { 
        100, 125, 160, 200, 250, 315, 400, 500, 630, 800, 1000, 1250, 1600, 2000, 2500, 3150 
    };

    // ISO 717-1 Table 1: Spectrum adaptation terms (100-3150 Hz)
    private static readonly double[] Spectrum1_PinkNoise = 
    { 
        -29, -26, -23, -21, -19, -17, -15, -13, -12, -11, -10, -9, -9, -9, -9, -9 
    };

    private static readonly double[] Spectrum2_TrafficNoise = 
    { 
        -20, -20, -18, -16, -15, -14, -13, -12, -11, -9, -8, -9, -10, -11, -13, -15 
    };

    public static RwRatingResult Calculate(Dictionary<int, double> tlValues)
    {
        // 1. Extract measured values for the 16 rating bands
        double[] rMeasured = new double[RatingFrequencies.Length];
        var missingFrequencies = new List<int>();
        for (int i = 0; i < RatingFrequencies.Length; i++)
        {
            if (tlValues.TryGetValue(RatingFrequencies[i], out double val))
            {
                rMeasured[i] = val;
            }
            else
            {
                missingFrequencies.Add(RatingFrequencies[i]);
            }
        }
        if (missingFrequencies.Count > 0)
            return new RwRatingResult(0, 0, 0, [], missingFrequencies);

        // 2. Shift reference curve to find Rw
        // Sum of unfavorable deviations must be <= 32.0 dB
        int bestRw = 0;
        List<int> limitingFreqs = new List<int>();

        for (int rwCandidate = -20; rwCandidate < 100; rwCandidate++)
        {
            double sumDeviations = 0;
            for (int i = 0; i < RatingFrequencies.Length; i++)
            {
                double refVal = rwCandidate + RwReferenceRelative[i];
                double deviation = refVal - rMeasured[i];
                if (deviation > 0)
                {
                    sumDeviations += deviation;
                }
            }

            if (sumDeviations <= 32.0 + 1e-6)
            {
                bestRw = rwCandidate;
            }
            else
            {
                // The previous candidate was the highest valid one
                break;
            }
        }

        // Identify limiting frequencies (where deviation > 0 for the best Rw)
        for (int i = 0; i < RatingFrequencies.Length; i++)
        {
            if ((bestRw + RwReferenceRelative[i]) - rMeasured[i] > 0)
            {
                limitingFreqs.Add(RatingFrequencies[i]);
            }
        }

        // 3. Calculate C and Ctr
        int C = CalculateAdaptationTerm(rMeasured, bestRw, Spectrum1_PinkNoise);
        int Ctr = CalculateAdaptationTerm(rMeasured, bestRw, Spectrum2_TrafficNoise);

        var stc = CalculateStc(tlValues);
        var oitc = CalculateOitc(tlValues);
        return new RwRatingResult(bestRw, C, Ctr, limitingFreqs, [], stc, oitc);
    }

    private static int CalculateAdaptationTerm(double[] rMeasured, int rw, double[] spectrumWeighting)
    {
        // L_sum = 10 * log10( sum( 10^( (L_i - R_i) / 10 ) ) )
        double sumPower = 0;
        for (int i = 0; i < rMeasured.Length; i++)
        {
            sumPower += Math.Pow(10, (spectrumWeighting[i] - rMeasured[i]) / 10.0);
        }

        double lSum = 10 * Math.Log10(sumPower);
        
        // C_j = -Rw - L_sum
        // Round to nearest integer
        return (int)Math.Round(-rw - lSum, MidpointRounding.AwayFromZero);
    }

    private static int? CalculateStc(Dictionary<int, double> tlValues)
    {
        int[] stcFreqs = [125, 160, 200, 250, 315, 400, 500, 630, 800, 1000, 1250, 1600, 2000, 2500, 3150, 4000];
        int[] stcRelative = [-16, -13, -10, -7, -4, -1, 0, 1, 2, 3, 4, 4, 4, 4, 4, 4];
        if (stcFreqs.Any(f => !tlValues.ContainsKey(f))) return null;

        var measured = stcFreqs.Select(f => tlValues[f]).ToArray();
        var best = 0;
        for (int candidate = 0; candidate < 100; candidate++)
        {
            var sumDeficiencies = 0.0;
            var maxDeficiency = 0.0;
            for (int i = 0; i < stcFreqs.Length; i++)
            {
                var deficiency = candidate + stcRelative[i] - measured[i];
                if (deficiency <= 0) continue;
                sumDeficiencies += deficiency;
                maxDeficiency = Math.Max(maxDeficiency, deficiency);
            }

            if (sumDeficiencies <= 32.0 + 1e-6 && maxDeficiency <= 8.0 + 1e-6)
                best = candidate;
            else if (candidate > best)
                break;
        }
        return best;
    }

    private static int? CalculateOitc(Dictionary<int, double> tlValues)
    {
        int[] freqs = [80, 100, 125, 160, 200, 250, 315, 400, 500, 630, 800, 1000, 1250, 1600, 2000, 2500, 3150, 4000];
        if (freqs.Any(f => !tlValues.ContainsKey(f))) return null;

        // ASTM E1332-16 Normalized Transportation Noise Reference Spectrum (approximate)
        // Values from 80 Hz to 4000 Hz.
        // NOT: Aşağıda OITC = 10·log10(incident/transmitted) hesaplanır. incident terimi mutlak
        // referans seviyesini kendiliğinden normalize ettiğinden, bu form ASTM E1332'nin
        // OITC = 100.13 − 10·log10(Σ 10^((Li−TLi)/10)) formuna MATEMATİKSEL OLARAK EŞDEĞERDİR
        // (mutlak ofset oranı sadeleştirir). Yani 100.13 sabiti "eksik" değildir; yalnız GÖRELİ
        // spektrum şekli yaklaşıktır ve lisanslı ASTM tablosuyla doğrulanmalıdır (codex §4 OITC).
        double[] weights = [0, -1, -2, -5, -6, -8, -9, -10, -10, -12, -13, -14, -14, -15, -15, -16, -17, -18];
        double transmitted = 0;
        double incident = 0;
        for (int i = 0; i < freqs.Length; i++)
        {
            incident += Math.Pow(10, weights[i] / 10.0);
            transmitted += Math.Pow(10, (weights[i] - tlValues[freqs[i]]) / 10.0);
        }
        if (transmitted <= 0 || incident <= 0) return null;

        return (int)Math.Round(10.0 * Math.Log10(incident / transmitted), MidpointRounding.AwayFromZero);
    }
}
