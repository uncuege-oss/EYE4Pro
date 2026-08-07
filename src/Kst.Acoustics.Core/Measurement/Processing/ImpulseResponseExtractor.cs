using System;
using System.Numerics;
using Kst.Acoustics.Core.Measurement.Signals;

namespace Kst.Acoustics.Core.Measurement.Processing;

/// <summary>
/// Kaydedilen sweep sinyalinden İmpuls Yanıtı (IR) çıkaran servis.
/// </summary>
public static class ImpulseResponseExtractor
{
    public static float[] Extract(float[] recorded, float[] original)
    {
        // 1. FFT boyutu (Linear convolution için en az recorded + original - 1)
        int fftSize = FftService.GetNextPowerOfTwo(recorded.Length + original.Length - 1);

        // 2. FFT'leri hesapla
        Complex[] recordedFft = FftService.Forward(Pad(recorded, fftSize));
        Complex[] originalFft = FftService.Forward(Pad(original, fftSize));

        // 3. Frekans domeninde dekonvolüsyon: H(f) = Y(f) / X(f)
        // Kararlılık için Kirkeby/regularization ekleyelim
        Complex[] irFft = new Complex[fftSize];
        double epsilon = 1e-6; // Gürültü tabanı koruması

        for (int i = 0; i < fftSize; i++)
        {
            double magSq = originalFft[i].Magnitude * originalFft[i].Magnitude;
            irFft[i] = recordedFft[i] * Complex.Conjugate(originalFft[i]) / (magSq + epsilon);
        }

        // 4. IFFT ile zaman domenine dön
        float[] ir = FftService.Inverse(irFft);

        // 5. Zaman kaymasını (time shift) düzelt
        // Genelde en büyük tepe noktası sinyalin başlangıcına yakın olmalıdır.
        // Ama sweep dekonvolüsyonunda IR zaten başta oluşur.
        
        return ir;
    }

    private static float[] Pad(float[] data, int size)
    {
        if (data.Length == size) return data;
        float[] padded = new float[size];
        Array.Copy(data, padded, data.Length);
        return padded;
    }
}
