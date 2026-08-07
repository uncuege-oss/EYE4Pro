using System;

namespace Kst.Acoustics.Core.Measurement.Processing;

/// <summary>
/// Oktav/1-3-oktav bant filtresi. Tek 2. derece bant-geçiren biquad çok geniş bir geçiş
/// bandına sahip olup komşu bantlardan (özellikle düşük frekanslarda) belirgin sızıntıya
/// yol açtığından iki özdeş biquad art arda (4. derece) uygulanır; bu, komşu oktav
/// bandındaki zayıflamayı ~2 katına çıkarır (tek kademede ~-6 dB/oktav iken kademeli
/// yapı ~-12 dB/oktav'a yaklaşır) ve RT/EDT gibi bant-bazlı parametrelerdeki
/// komşu-bant kirlenmesini azaltır.
/// </summary>
public class OctaveBandFilter
{
    private readonly double _a0, _a1, _a2, _b1, _b2;
    private double _z1a, _z2a; // 1. kademe durumu
    private double _z1b, _z2b; // 2. kademe durumu

    public OctaveBandFilter(double centerFreq, double sampleRate, double bandWidthOctaves = 1.0)
    {
        double fLow = centerFreq / Math.Pow(2, bandWidthOctaves / 2.0);
        double fHigh = centerFreq * Math.Pow(2, bandWidthOctaves / 2.0);

        // Audio EQ Cookbook BPF (constant 0 dB peak gain)
        double w0 = 2.0 * Math.PI * centerFreq / sampleRate;
        double Q = centerFreq / (fHigh - fLow);
        double alpha = Math.Sin(w0) / (2.0 * Q);
        double cosW0 = Math.Cos(w0);

        double a0 = 1.0 + alpha;
        _a0 = alpha / a0;
        _a1 = 0 / a0;
        _a2 = -alpha / a0;
        _b1 = -2.0 * cosW0 / a0;
        _b2 = (1.0 - alpha) / a0;
    }

    private float ProcessStage(float input, ref double z1, ref double z2)
    {
        double output = _a0 * input + z1;
        z1 = _a1 * input - _b1 * output + z2;
        z2 = _a2 * input - _b2 * output;
        return (float)output;
    }

    public float Process(float input)
    {
        float stage1 = ProcessStage(input, ref _z1a, ref _z2a);
        return ProcessStage(stage1, ref _z1b, ref _z2b);
    }

    public float[] Apply(float[] data)
    {
        float[] result = new float[data.Length];
        _z1a = 0; _z2a = 0; _z1b = 0; _z2b = 0;
        for (int i = 0; i < data.Length; i++)
        {
            result[i] = Process(data[i]);
        }
        return result;
    }
}
