using System;
using System.Numerics;
using Kst.Acoustics.Core.Measurement.Processing;

namespace Kst.Acoustics.Core.Measurement.Analysis;

public sealed record SpectrogramData(
    float[] TimeAxisSeconds,
    float[] FrequencyAxisHz,
    float[,] MagnitudeDb)
{
    public int FrameCount => TimeAxisSeconds.Length;
    public int BinCount => FrequencyAxisHz.Length;
}

public static class SpectrogramAnalyzer
{
    /// <summary>
    /// Sinyalden STFT tabanlı spektrogram üretir.
    /// </summary>
    /// <param name="windowSize">Pencere boyutu (2'nin kuvveti önerilir; ör. 2048).</param>
    /// <param name="overlap">0–0.99 örtüşme oranı.</param>
    public static SpectrogramData Analyze(
        float[] signal,
        int sampleRate,
        int windowSize = 2048,
        double overlap = 0.5)
    {
        if (signal == null || signal.Length < windowSize)
            throw new ArgumentException("Sinyal pencere boyutundan kısa.", nameof(signal));

        overlap = Math.Clamp(overlap, 0.0, 0.95);
        int hopSize = Math.Max(1, (int)(windowSize * (1.0 - overlap)));
        int frames = Math.Max(1, (signal.Length - windowSize) / hopSize + 1);
        int bins = windowSize / 2;

        var window = HannWindow(windowSize);
        var data = new float[frames, bins];
        var timeAxis = new float[frames];
        var freqAxis = new float[bins];
        for (int b = 0; b < bins; b++) freqAxis[b] = (float)(b * sampleRate / (double)windowSize);

        var buffer = new float[windowSize];

        for (int f = 0; f < frames; f++)
        {
            int start = f * hopSize;
            for (int i = 0; i < windowSize; i++)
                buffer[i] = signal[start + i] * window[i];

            Complex[] spectrum = FftService.Forward(buffer);

            for (int b = 0; b < bins; b++)
            {
                double mag = spectrum[b].Magnitude / (windowSize / 2.0);
                double db = 20.0 * Math.Log10(Math.Max(mag, 1e-10));
                data[f, b] = (float)db;
            }
            timeAxis[f] = (float)((start + windowSize / 2.0) / sampleRate);
        }

        return new SpectrogramData(timeAxis, freqAxis, data);
    }

    private static float[] HannWindow(int size)
    {
        var w = new float[size];
        for (int i = 0; i < size; i++)
            w[i] = (float)(0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (size - 1))));
        return w;
    }
}
