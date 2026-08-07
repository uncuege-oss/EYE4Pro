using System;
using System.Collections.Generic;
using System.Numerics;
using Kst.Acoustics.Core.Measurement.Processing;

namespace Kst.Acoustics.Core.Measurement.Analysis;

public record WaterfallData(float[] Frequencies, float[] TimeOffsets, float[][] Data);

public static class WaterfallAnalyzer
{
    public static WaterfallData Analyze(float[] ir, int sampleRate, int numSlices = 50, int fftSize = 4096)
    {
        if (ir == null || ir.Length == 0)
            return new WaterfallData(Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float[]>());

        // 1. Parametreler — tüm IR uzunluğu taranır (eskiden yalnız ilk yarısı taranıyordu)
        int sliceStep = Math.Max(1, ir.Length / numSlices);
        int numBins = fftSize / 2 + 1;
        float[] frequencies = new float[numBins];
        float[] timeOffsets = new float[numSlices];
        float[][] data = new float[numSlices][];

        double binFreqWidth = (double)sampleRate / fftSize;
        for (int i = 0; i < numBins; i++) frequencies[i] = (float)(i * binFreqWidth);

        for (int s = 0; s < numSlices; s++)
        {
            int start = s * sliceStep;
            timeOffsets[s] = (float)((double)start / sampleRate * 1000.0); // ms

            // Pencerele ve FFT al
            float[] slice = new float[fftSize];
            int copyLen = Math.Min(fftSize, ir.Length - start);
            if (copyLen > 0)
            {
                Array.Copy(ir, start, slice, 0, copyLen);
                WindowingService.Apply(slice, WindowType.Hann);
            }

            Complex[] fft = FftService.Forward(slice);
            data[s] = new float[numBins];

            for (int i = 0; i < numBins; i++)
            {
                double mag = fft[i].Magnitude;
                data[s][i] = (float)(20 * Math.Log10(Math.Max(mag, 1e-12)));
            }
        }

        return new WaterfallData(frequencies, timeOffsets, data);
    }
}
