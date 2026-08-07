using System;

namespace Kst.Acoustics.Core.Measurement.Signals;

public static class NoiseGenerator
{
    private static readonly Random _random = new();

    public static float[] White(double durationSeconds, int sampleRate, float amplitude = 0.5f)
    {
        int numSamples = (int)(durationSeconds * sampleRate);
        float[] samples = new float[numSamples];
        for (int i = 0; i < numSamples; i++)
        {
            samples[i] = (float)((_random.NextDouble() * 2.0 - 1.0) * amplitude);
        }
        return samples;
    }

    public static float[] Pink(double durationSeconds, int sampleRate, float amplitude = 0.5f)
    {
        int numSamples = (int)(durationSeconds * sampleRate);
        float[] samples = new float[numSamples];
        
        // Voss-McCartney algoritması (basitleştirilmiş)
        double[] b = new double[7];
        for (int i = 0; i < numSamples; i++)
        {
            double white = _random.NextDouble() * 2.0 - 1.0;
            b[0] = 0.99886 * b[0] + white * 0.0555179;
            b[1] = 0.99332 * b[1] + white * 0.0750759;
            b[2] = 0.96900 * b[2] + white * 0.1538520;
            b[3] = 0.86650 * b[3] + white * 0.3104856;
            b[4] = 0.55000 * b[4] + white * 0.5329522;
            b[5] = -0.7616 * b[5] - white * 0.0168980;
            double pink = b[0] + b[1] + b[2] + b[3] + b[4] + b[5] + b[6] + white * 0.5362;
            b[6] = white * 0.115926;
            samples[i] = (float)(pink * 0.11 * amplitude); // Ölçeklendirme
        }
        return samples;
    }

    public static float[] Multitone(int[] freqs, double durationSeconds, int sampleRate, float amplitude = 0.5f)
    {
        int numSamples = (int)(durationSeconds * sampleRate);
        float[] samples = new float[numSamples];
        float ampPerTone = amplitude / freqs.Length;

        foreach (var f in freqs)
        {
            double phase = _random.NextDouble() * 2.0 * Math.PI; // Rastgele faz ile Crest Factor'ü düşür
            for (int i = 0; i < numSamples; i++)
            {
                double t = (double)i / sampleRate;
                samples[i] += (float)(ampPerTone * Math.Sin(2 * Math.PI * f * t + phase));
            }
        }
        return samples;
    }
}
