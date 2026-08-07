using System;
using System.Collections.Generic;
using Kst.Acoustics.Core.Measurement.Processing;

namespace Kst.Acoustics.Core.Measurement.Analysis;

/// <summary>
/// IEC 60268-16'ya göre IR tabanlı STI (Speech Transmission Index) hesabı.
/// 7 oktav bantta (125 Hz – 8 kHz) MTF dizisi (14 modülasyon frekansı) üretip
/// ağırlıklı toplama ile STI [0..1] değeri döner.
/// </summary>
public static class SpeechTransmissionIndex
{
    public static readonly int[] OctaveBandsHz = { 125, 250, 500, 1000, 2000, 4000, 8000 };

    public static readonly double[] ModulationFrequenciesHz =
    {
        0.63, 0.80, 1.00, 1.25, 1.60, 2.00, 2.50,
        3.15, 4.00, 5.00, 6.30, 8.00, 10.00, 12.50
    };

    // IEC 60268-16 ed.4 erkek ses ağırlıkları
    public static readonly double[] AlphaMale = { 0.085, 0.127, 0.230, 0.233, 0.309, 0.224, 0.173 };
    public static readonly double[] BetaMale  = { 0.085, 0.078, 0.065, 0.011, 0.047, 0.095 };

    // Kadın ses ağırlıkları
    public static readonly double[] AlphaFemale = { 0.000, 0.117, 0.223, 0.216, 0.328, 0.250, 0.194 };
    public static readonly double[] BetaFemale  = { 0.099, 0.066, 0.062, 0.025, 0.076, 0.103 };

    public sealed record StiResult(
        double Value,
        IReadOnlyList<double> ModulationTransferIndex,
        IReadOnlyDictionary<int, double[]> MtfPerBand);

    /// <summary>
    /// IR'den STI hesaplar. Arka plan gürültü dB'de oktav bandı başına verilirse
    /// SNR sınırlandırması uygulanır; null ise yalnız oda dekoru/yansıması STI etkisi alınır.
    /// </summary>
    public static StiResult Compute(
        float[] impulseResponse,
        int sampleRate,
        IReadOnlyList<double>? bandSignalLevelsDb = null,
        IReadOnlyList<double>? bandNoiseLevelsDb = null,
        bool useMaleWeights = true)
    {
        if (impulseResponse == null || impulseResponse.Length == 0)
            throw new ArgumentException("IR boş.", nameof(impulseResponse));

        var mtfPerBand = new Dictionary<int, double[]>(OctaveBandsHz.Length);
        var mti = new double[OctaveBandsHz.Length];

        for (int k = 0; k < OctaveBandsHz.Length; k++)
        {
            int freq = OctaveBandsHz[k];
            var filter = new OctaveBandFilter(freq, sampleRate);
            var filtered = filter.Apply(impulseResponse);

            var mtf = ComputeMtf(filtered, sampleRate);
            mtfPerBand[freq] = mtf;

            double sumTi = 0;
            int valid = 0;
            for (int n = 0; n < mtf.Length; n++)
            {
                var m = ApplyNoiseClamp(mtf[n], k, bandSignalLevelsDb, bandNoiseLevelsDb);
                if (double.IsNaN(m)) continue;
                var snrApp = ApparentSnrDb(m);
                var ti = (snrApp + 15.0) / 30.0;
                ti = Math.Clamp(ti, 0.0, 1.0);
                sumTi += ti;
                valid++;
            }
            mti[k] = valid > 0 ? sumTi / valid : 0;
        }

        var alpha = useMaleWeights ? AlphaMale : AlphaFemale;
        var beta  = useMaleWeights ? BetaMale  : BetaFemale;

        double sti = 0;
        for (int k = 0; k < alpha.Length; k++) sti += alpha[k] * mti[k];
        for (int k = 0; k < beta.Length; k++)
        {
            var product = mti[k] * mti[k + 1];
            if (product < 0) product = 0;
            sti -= beta[k] * Math.Sqrt(product);
        }
        sti = Math.Clamp(sti, 0.0, 1.0);

        return new StiResult(sti, mti, mtfPerBand);
    }

    /// <summary>
    /// IEC 60268-16 (B.1): MTF(F) = | Σ h²(t)·e^(-j2πFt) | / Σ h²(t).
    /// </summary>
    private static double[] ComputeMtf(float[] ir, int sampleRate)
    {
        double total = 0;
        for (int i = 0; i < ir.Length; i++) total += (double)ir[i] * ir[i];

        var mtf = new double[ModulationFrequenciesHz.Length];
        if (total <= 0)
        {
            for (int n = 0; n < mtf.Length; n++) mtf[n] = double.NaN;
            return mtf;
        }

        for (int n = 0; n < ModulationFrequenciesHz.Length; n++)
        {
            double f = ModulationFrequenciesHz[n];
            double real = 0, imag = 0;
            double w = -2.0 * Math.PI * f / sampleRate;
            for (int i = 0; i < ir.Length; i++)
            {
                double energy = (double)ir[i] * ir[i];
                double phase = w * i;
                real += energy * Math.Cos(phase);
                imag += energy * Math.Sin(phase);
            }
            mtf[n] = Math.Sqrt(real * real + imag * imag) / total;
            if (mtf[n] > 1.0) mtf[n] = 1.0;
        }
        return mtf;
    }

    private static double ApparentSnrDb(double m)
    {
        if (m <= 1e-6) return -15.0;
        if (m >= 1.0 - 1e-6) return 15.0;
        var snr = 10.0 * Math.Log10(m / (1.0 - m));
        return Math.Clamp(snr, -15.0, 15.0);
    }

    private static double ApplyNoiseClamp(double m, int bandIndex,
        IReadOnlyList<double>? bandSignal, IReadOnlyList<double>? bandNoise)
    {
        if (bandSignal == null || bandNoise == null) return m;
        if (bandIndex >= bandSignal.Count || bandIndex >= bandNoise.Count) return m;

        var snr = bandSignal[bandIndex] - bandNoise[bandIndex];
        var noiseFactor = 1.0 / (1.0 + Math.Pow(10.0, -snr / 10.0));
        return m * noiseFactor;
    }
}
