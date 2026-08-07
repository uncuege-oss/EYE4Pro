using System;
using System.Numerics;
using Kst.Acoustics.Core.Measurement.Data;

namespace Kst.Acoustics.Core.Measurement.Processing;

public static class FrequencyResponseAnalyzer
{
        public static FrequencyResponseData Analyze(float[] impulseResponse, int sampleRate, CalibrationProfile? calibration = null, double levelOffsetDb = 0)
    {
        // 1. FFT
        int fftSize = FftService.GetNextPowerOfTwo(impulseResponse.Length);
        Complex[] fft = FftService.Forward(impulseResponse);

        // 2. Sadece pozitif frekansları al
        int numBins = fft.Length / 2 + 1;
        float[] frequencies = new float[numBins];
        float[] magnitudes = new float[numBins];
        float[] phases = new float[numBins];

        double binFreqWidth = (double)sampleRate / fft.Length;

        for (int i = 0; i < numBins; i++)
        {
            double f = i * binFreqWidth;
            frequencies[i] = (float)f;

                // ir zaten dekonvolüsyonda H(f)=Y(f)/X(f) olarak normalize edilmiş bir impuls yanıtı;
                // Matlab FFT/IFFT çifti Forward(Inverse(X))=X sağladığından burada 1/N ile tekrar
                // bölmek yalnızca H(f)'i fft uzunluğu kadar (~100 dB) yapay olarak zayıflatırdı.
                double mag = fft[i].Magnitude;
                double db = 20 * Math.Log10(Math.Max(mag, 1e-12));
                db += levelOffsetDb;
            
            // Kalibrasyon Uygula
            if (calibration != null)
            {
                db -= calibration.GetCorrection(f); // .cal dosyaları genelde mikrofon cevabını verir, biz tersini toplarız/çıkartırız
            }

            magnitudes[i] = (float)db;
            phases[i] = (float)fft[i].Phase;
        }

        return new FrequencyResponseData(frequencies, magnitudes, phases);
    }
}
