using System;

namespace Kst.Acoustics.Core.Simulation;

/// <summary>
/// 1-oktav bandpass filtresi (RBJ Audio EQ Cookbook biquad, 0 dB peak gain, Q = √2).
/// Auralizasyonda per-band IR şekillendirmek için kullanılır. State biriken
/// (causal forward) IIR'dir; instance başına tek sinyal üzerinde çalışır.
/// </summary>
public sealed class OctaveBandFilter
{
    // Standart akustik oktav merkez frekansları (Hz). Auralizasyondaki 8 bantla uyumlu.
    public static readonly double[] CenterFrequencies = { 63, 125, 250, 500, 1000, 2000, 4000, 8000 };

    private readonly double _b0, _b1, _b2, _a1, _a2;
    private double _x1, _x2, _y1, _y2;

    /// <summary>
    /// Octave bandpass tasarlar. Q = √2 ≈ 1.414 (1-oktav genişlik).
    /// </summary>
    public OctaveBandFilter(double centerHz, int sampleRate)
    {
        if (centerHz <= 0) throw new ArgumentOutOfRangeException(nameof(centerHz));
        if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));

        // Nyquist üstü merkez frekansı için filtre devre dışı (pass-through olur)
        double nyquist = sampleRate * 0.5;
        if (centerHz >= nyquist * 0.95)
        {
            // Pass-through biquad
            _b0 = 1; _b1 = 0; _b2 = 0; _a1 = 0; _a2 = 0;
            return;
        }

        // RBJ bandpass (constant 0 dB peak gain):
        //   b0 =  α,  b1 = 0,  b2 = -α
        //   a0 = 1+α, a1 = -2cos(ω0), a2 = 1-α
        double w0 = 2.0 * Math.PI * centerHz / sampleRate;
        double cosw = Math.Cos(w0);
        double sinw = Math.Sin(w0);
        const double Q = 1.41421356237; // √2 → 1-oktav BW
        double alpha = sinw / (2.0 * Q);

        double a0 = 1.0 + alpha;
        _b0 =  alpha / a0;
        _b1 =  0.0;
        _b2 = -alpha / a0;
        _a1 = -2.0 * cosw / a0;
        _a2 = (1.0 - alpha) / a0;
    }

    /// <summary>Causal forward filtreyi yerinde uygular. Önceki state korunmaz.</summary>
    public void ProcessInPlace(float[] buffer)
    {
        _x1 = _x2 = _y1 = _y2 = 0;
        for (int n = 0; n < buffer.Length; n++)
        {
            double x = buffer[n];
            double y = _b0 * x + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
            _x2 = _x1; _x1 = x;
            _y2 = _y1; _y1 = y;
            buffer[n] = (float)y;
        }
    }

    /// <summary>
    /// Zero-phase (filtfilt) uygulama: forward + reverse → faz=0, grup gecikmesi sıfır.
    /// |H(f)|² magnitude verir; offline auralizasyonda bantbazlı IR'yi düz fazlı tutar
    /// ve bantlar arası dispersion'ı önler.
    /// </summary>
    public void ProcessZeroPhaseInPlace(float[] buffer)
    {
        // Forward
        ProcessInPlace(buffer);

        // Reverse
        Array.Reverse(buffer);
        ProcessInPlace(buffer);
        Array.Reverse(buffer);
    }
}
