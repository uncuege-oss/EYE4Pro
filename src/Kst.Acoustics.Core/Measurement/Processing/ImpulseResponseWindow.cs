using System;

namespace Kst.Acoustics.Core.Measurement.Processing;

/// <summary>
/// Sweep dekonvolüsyonundan çıkan uzun IR tamponunu peak'e göre hizalar, analiz için
/// makul bir uzunluğa kırpar ve yalnızca kuyruğa (tail) yumuşak bir pencere uygular.
/// Baş taraf (direkt ses + erken yansımalar) tam kazançta kalır — eski davranışta
/// tüm tampona uygulanan Tukey penceresi, peak tamponun başına yakınsa direkt sesi
/// -30 dB'den fazla zayıflatıyor, tam sıfıra denk gelirse tamamen siliyordu.
/// </summary>
public static class ImpulseResponseWindow
{
    public static float[] AlignAndWindow(
        float[] ir,
        int sampleRate,
        double preRollSeconds = 0.005,
        double maxAnalysisSeconds = 3.0,
        double tailFraction = 0.15)
    {
        ArgumentNullException.ThrowIfNull(ir);
        if (ir.Length == 0) return ir;

        int peakIndex = FindPeakIndex(ir, sampleRate);
        int preRoll = (int)(preRollSeconds * sampleRate);
        int start = Math.Max(0, peakIndex - preRoll);

        int maxLen = (int)(maxAnalysisSeconds * sampleRate);
        int length = Math.Min(maxLen, ir.Length - start);
        if (length <= 0) return Array.Empty<float>();

        var cropped = new float[length];
        Array.Copy(ir, start, cropped, 0, length);

        ApplyTailWindow(cropped, tailFraction);
        return cropped;
    }

    /// <summary>İlk 2 saniye (ya da tampon kısa ise tamamı) içinde en büyük genlikli örneği bulur.</summary>
    public static int FindPeakIndex(float[] ir, int sampleRate)
    {
        int searchLen = Math.Min(ir.Length, sampleRate * 2);
        int peakIndex = 0;
        float maxVal = 0;
        for (int i = 0; i < searchLen; i++)
        {
            float abs = Math.Abs(ir[i]);
            if (abs > maxVal) { maxVal = abs; peakIndex = i; }
        }
        return peakIndex;
    }

    private static void ApplyTailWindow(float[] data, double tailFraction)
    {
        tailFraction = Math.Clamp(tailFraction, 0.0, 1.0);
        int n = data.Length;
        int tailStart = (int)(n * (1.0 - tailFraction));
        int tailLen = n - tailStart;
        if (tailLen <= 1) return;

        for (int i = tailStart; i < n; i++)
        {
            double x = (double)(i - tailStart) / (tailLen - 1);
            double w = 0.5 * (1 + Math.Cos(Math.PI * x)); // raised-cosine fade-out
            data[i] *= (float)w;
        }
    }
}
