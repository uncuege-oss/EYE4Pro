using System;

namespace Kst.Acoustics.Core.Measurement.Signals;

/// <summary>
/// Logaritmik Sweep (TSP - Time Stretched Pulse) sinyali üretici.
/// Akustik impuls yanıtı (IR) ölçümü için standart sinyal.
/// </summary>
public static class LogSweepGenerator
{
    /// <summary>
    /// Logaritmik sweep sinyali üretir.
    /// </summary>
    /// <param name="f1">Başlangıç frekansı (Hz)</param>
    /// <param name="f2">Bitiş frekansı (Hz)</param>
    /// <param name="durationSeconds">Sinyal süresi (saniye)</param>
    /// <param name="sampleRate">Örnekleme hızı (Hz)</param>
    /// <param name="amplitude">Genlik (0.0 - 1.0)</param>
    /// <returns>Sinyal örnekleri (float[])</returns>
    public static float[] Generate(double f1, double f2, double durationSeconds, int sampleRate, float amplitude = 0.5f)
    {
        int numSamples = (int)(durationSeconds * sampleRate);
        float[] samples = new float[numSamples];

        double L = durationSeconds / Math.Log(f2 / f1);
        
        for (int n = 0; n < numSamples; n++)
        {
            double t = (double)n / sampleRate;
            // Logaritmik sweep formülü: sin(2 * pi * f1 * L * (exp(t / L) - 1))
            double phase = 2 * Math.PI * f1 * L * (Math.Exp(t / L) - 1);
            samples[n] = (float)(amplitude * Math.Sin(phase));
        }

        // Sinyal sonuna çok kısa bir fade-out ekleyerek klik sesini önleyelim
        ApplyFade(samples, sampleRate, 0.05);

        return samples;
    }

    private static void ApplyFade(float[] samples, int sampleRate, double fadeDuration)
    {
        int fadeSamples = (int)(fadeDuration * sampleRate);
        if (fadeSamples > samples.Length / 2) fadeSamples = samples.Length / 2;

        for (int i = 0; i < fadeSamples; i++)
        {
            float factor = (float)i / fadeSamples;
            samples[i] *= factor; // Fade-in
            samples[samples.Length - 1 - i] *= factor; // Fade-out
        }
    }
}
