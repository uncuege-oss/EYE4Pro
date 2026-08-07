using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Kst.Acoustics.Core.Measurement.Processing;

namespace Kst.Acoustics.Core.Measurement.Analysis;

    public class RtaAnalyzer
    {
        private readonly int _sampleRate;
        private readonly int _fftSize;
        private readonly float[] _bands; // 1/3 oktav merkez frekansları
        private readonly float[] _ringBuffer;
        private int _ringWriteIndex;
        private int _ringCount;

    // Hann penceresinin uyumlu (coherent) genlik kazancı ~0.5; pencereleme enerjiyi
    // sistematik olarak zayıflatır, güç ölçütlerinde 1/gain^2 ile telafi edilmelidir.
    private const double HannCoherentGain = 0.5;

    public RtaAnalyzer(int sampleRate = 48000, int fftSize = 8192)
    {
            _sampleRate = sampleRate;
            _fftSize = fftSize;
            _ringBuffer = new float[_fftSize];
        
        // ISO 1/3 Oktav Merkez Frekansları
        _bands = new float[] 
        { 
            20, 25, 31.5f, 40, 50, 63, 80, 100, 125, 160, 200, 250, 315, 400, 500, 630, 800, 
            1000, 1250, 1600, 2000, 2500, 3150, 4000, 5000, 6300, 8000, 10000, 12500, 16000, 20000 
        };
    }

    public float[] Bands => _bands;

    public float[] Process(float[] samples)
    {
            if (samples.Length == 0) return new float[_bands.Length];

            foreach (var sample in samples)
            {
                _ringBuffer[_ringWriteIndex] = sample;
                _ringWriteIndex = (_ringWriteIndex + 1) % _fftSize;
                if (_ringCount < _fftSize)
                    _ringCount++;
            }

            if (_ringCount < _fftSize)
                return new float[_bands.Length];

            // 1. FFT
            float[] buffer = new float[_fftSize];
            for (int i = 0; i < _fftSize; i++)
                buffer[i] = _ringBuffer[(_ringWriteIndex + i) % _fftSize];
        WindowingService.Apply(buffer, WindowType.Hann);
        
        Complex[] fft = FftService.Forward(buffer);
        int numBins = fft.Length / 2 + 1;
        double binWidth = (double)_sampleRate / _fftSize;

        float[] levels = new float[_bands.Length];

        // 2. FFT binlerini 1/3 oktav bantlarına topla
        for (int i = 0; i < _bands.Length; i++)
        {
            float fCenter = _bands[i];
            float fLow = fCenter / 1.122f; // 2^(1/6)
            float fHigh = fCenter * 1.122f;

            double sumEnergy = 0;
            int count = 0;

            int binStart = (int)Math.Floor(fLow / binWidth);
            int binEnd = (int)Math.Ceiling(fHigh / binWidth);

            for (int b = binStart; b <= binEnd && b < numBins; b++)
            {
                sumEnergy += fft[b].Magnitude * fft[b].Magnitude;
                count++;
            }

            if (count > 0)
            {
                var normalizedEnergy = sumEnergy / (_fftSize * _fftSize) / (HannCoherentGain * HannCoherentGain);
                levels[i] = (float)(10 * Math.Log10(Math.Max(normalizedEnergy, 1e-12)));
            }
            else
            {
                levels[i] = -120;
            }
        }

        return levels;
    }
}
