using System;
using System.Linq;

namespace Kst.Acoustics.Core.Measurement.Processing;

/// <summary>
/// Schroeder geriye entegrasyonu (Energy Decay Curve - EDC).
/// </summary>
public static class SchroederIntegrator
{
    public static float[] CalculateEDC(float[] impulseResponse)
    {
        int n = impulseResponse.Length;
        float[] squared = impulseResponse.Select(x => x * x).ToArray();
        float[] edc = new float[n];

        // Gürültü tabanı tahmini (Sinyalin son %5'lik kısmı)
        int noiseWindow = n / 20;
        double noiseFloor = 0;
        if (noiseWindow > 0)
        {
            for (int i = n - noiseWindow; i < n; i++) noiseFloor += squared[i];
            noiseFloor /= noiseWindow;
        }

        // Geriye doğru kümülatif toplam (Gürültü tabanı çıkarılmış)
        double sum = 0;
        for (int i = n - 1; i >= 0; i--)
        {
            double val = Math.Max(0, squared[i] - noiseFloor);
            sum += val;
            edc[i] = (float)sum;
        }

        // Normalizasyon (0 dB seviyesine çekme)
        double totalEnergy = edc[0];
        if (totalEnergy > 0)
        {
            for (int i = 0; i < n; i++)
            {
                edc[i] = (float)(10 * Math.Log10(Math.Max(edc[i] / totalEnergy, 1e-12)));
            }
        }

        return edc;
    }
}
