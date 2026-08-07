using System;
using System.Collections.Generic;
using System.Numerics;
using Kst.Acoustics.Core.Measurement.Processing;

namespace Kst.Acoustics.Core.Measurement.Analysis;

public record DistortionResult(double THD, double[] HarmonicsDb);

public sealed record ImdComponent(double FrequencyHz, double LevelDbr);

public sealed record ImdResult(
    double ImdPercent,
    double[] FundamentalFrequenciesHz,
    double[] FundamentalLevelsDbr,
    ImdComponent[] IntermodulationProducts,
    double NoiseFloorDbr);

/// <summary>
/// FFT tabanlı Harmonik Bozulma (THD) analizörü.
/// </summary>
public static class DistortionAnalyzer
{
    public static DistortionResult Analyze(float[] samples, int sampleRate, double fundamentalFreq)
    {
        int fftSize = FftService.GetNextPowerOfTwo(samples.Length);
        Complex[] fft = FftService.Forward(samples);
        double binWidth = (double)sampleRate / fftSize;

        // 1. Temel frekans genliğini bul
        int fundamentalBin = (int)Math.Round(fundamentalFreq / binWidth);
        double fundamentalMag = GetPeakMagnitude(fft, fundamentalBin);

        if (fundamentalMag < 1e-12) return new DistortionResult(0, new double[0]);

        // 2. Harmonikleri bul (2. - 6. harmonik, 5 değer)
        double[] harmonicsMag = new double[5];
        double sumHarmonicsSq = 0;

        for (int h = 2; h <= 6; h++)
        {
            double hFreq = fundamentalFreq * h;
            if (hFreq > sampleRate / 2.0) break;

            int hBin = (int)Math.Round(hFreq / binWidth);
            double hMag = GetPeakMagnitude(fft, hBin);
            
            harmonicsMag[h - 2] = 20 * Math.Log10(Math.Max(hMag / fundamentalMag, 1e-12));
            sumHarmonicsSq += hMag * hMag;
        }

        double thd = Math.Sqrt(sumHarmonicsSq) / fundamentalMag * 100.0; // Yüzde (%)

        return new DistortionResult(thd, harmonicsMag);
    }

    private static double GetPeakMagnitude(Complex[] fft, int centerBin)
    {
        // Enerji sızıntısını önlemek için komşu binleri de kontrol et
        double maxMag = 0;
        for (int b = centerBin - 2; b <= centerBin + 2; b++)
        {
            if (b >= 0 && b < fft.Length)
            {
                maxMag = Math.Max(maxMag, fft[b].Magnitude);
            }
        }
        return maxMag;
    }

    /// <summary>
    /// Multitone kayıttan IMD hesaplar. Temel tonlar dışı bileşenleri (f_i ± f_j, 2f_i ± f_j)
    /// toplar ve toplam IMD'yi yüzde olarak verir.
    /// </summary>
    public static ImdResult AnalyzeIntermodulation(float[] samples, int sampleRate, double[] fundamentalFreqs)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(fundamentalFreqs);
        if (fundamentalFreqs.Length < 2)
            throw new ArgumentException("IMD için en az 2 temel ton gerekli.", nameof(fundamentalFreqs));

        int fftSize = FftService.GetNextPowerOfTwo(samples.Length);
        Complex[] fft = FftService.Forward(samples);
        double binWidth = (double)sampleRate / fftSize;
        double nyquist = sampleRate / 2.0;

        // 1) Temel ton seviyeleri
        var fundamentalLevels = new double[fundamentalFreqs.Length];
        double fundamentalSumSq = 0;
        double maxFundamental = 0;
        for (int i = 0; i < fundamentalFreqs.Length; i++)
        {
            int bin = (int)Math.Round(fundamentalFreqs[i] / binWidth);
            var mag = GetPeakMagnitude(fft, bin);
            fundamentalLevels[i] = mag;
            fundamentalSumSq += mag * mag;
            if (mag > maxFundamental) maxFundamental = mag;
        }
        if (maxFundamental < 1e-12)
            return new ImdResult(0, fundamentalFreqs, new double[fundamentalFreqs.Length], Array.Empty<ImdComponent>(), 0);

        // 2) Intermodülasyon ürünleri: |f_i ± f_j| ve |2f_i ± f_j|
        var products = new List<ImdComponent>();
        double imdSumSq = 0;
        var seenBins = new HashSet<int>();
        foreach (var fi in fundamentalFreqs) seenBins.Add((int)Math.Round(fi / binWidth));

        for (int i = 0; i < fundamentalFreqs.Length; i++)
        {
            for (int j = 0; j < fundamentalFreqs.Length; j++)
            {
                if (i == j) continue;
                AddProduct(fundamentalFreqs[i] + fundamentalFreqs[j], fft, binWidth, nyquist, seenBins, products, ref imdSumSq, maxFundamental);
                AddProduct(Math.Abs(fundamentalFreqs[i] - fundamentalFreqs[j]), fft, binWidth, nyquist, seenBins, products, ref imdSumSq, maxFundamental);
                AddProduct(2 * fundamentalFreqs[i] + fundamentalFreqs[j], fft, binWidth, nyquist, seenBins, products, ref imdSumSq, maxFundamental);
                AddProduct(Math.Abs(2 * fundamentalFreqs[i] - fundamentalFreqs[j]), fft, binWidth, nyquist, seenBins, products, ref imdSumSq, maxFundamental);
            }
        }

        var imdPercent = 100.0 * Math.Sqrt(imdSumSq) / Math.Sqrt(fundamentalSumSq);

        var fundamentalDbr = new double[fundamentalLevels.Length];
        for (int i = 0; i < fundamentalLevels.Length; i++)
            fundamentalDbr[i] = 20 * Math.Log10(Math.Max(fundamentalLevels[i] / maxFundamental, 1e-12));

        // Gürültü tabanı: temel ve ürün dışı binlerin medyanı
        var noiseFloorDbr = EstimateNoiseFloorDbr(fft, seenBins, maxFundamental);

        return new ImdResult(imdPercent, fundamentalFreqs, fundamentalDbr, products.ToArray(), noiseFloorDbr);
    }

    private static void AddProduct(double freq, Complex[] fft, double binWidth, double nyquist,
        HashSet<int> seenBins, List<ImdComponent> products, ref double sumSq, double reference)
    {
        if (freq < binWidth * 2 || freq >= nyquist - binWidth) return;
        int bin = (int)Math.Round(freq / binWidth);
        if (!seenBins.Add(bin)) return;
        var mag = GetPeakMagnitude(fft, bin);
        sumSq += mag * mag;
        products.Add(new ImdComponent(freq, 20 * Math.Log10(Math.Max(mag / reference, 1e-12))));
    }

    private static double EstimateNoiseFloorDbr(Complex[] fft, HashSet<int> excludeBins, double reference)
    {
        var samples = new List<double>();
        for (int b = 4; b < fft.Length; b++)
        {
            if (excludeBins.Contains(b)) continue;
            samples.Add(fft[b].Magnitude);
        }
        if (samples.Count == 0) return -120;
        samples.Sort();
        var median = samples[samples.Count / 2];
        return 20 * Math.Log10(Math.Max(median / reference, 1e-12));
    }
}
