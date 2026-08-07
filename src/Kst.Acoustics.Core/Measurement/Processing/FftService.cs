using System;
using System.Numerics;
using MathNet.Numerics.IntegralTransforms;

namespace Kst.Acoustics.Core.Measurement.Processing;

/// <summary>
/// MathNet.Numerics tabanlı FFT ve IFFT işlemleri.
/// </summary>
public static class FftService
{
    public static Complex[] Forward(float[] data)
    {
        // FFT boyutu 2'nin kuvveti olmalı
        int n = GetNextPowerOfTwo(data.Length);
        Complex[] complexData = new Complex[n];
        for (int i = 0; i < data.Length; i++)
        {
            complexData[i] = new Complex(data[i], 0);
        }

        Fourier.Forward(complexData, FourierOptions.Matlab);
        return complexData;
    }

    public static float[] Inverse(Complex[] data)
    {
        Complex[] copy = new Complex[data.Length];
        data.CopyTo(copy, 0);
        
        Fourier.Inverse(copy, FourierOptions.Matlab);
        
        float[] result = new float[copy.Length];
        for (int i = 0; i < copy.Length; i++)
        {
            result[i] = (float)copy[i].Real;
        }
        return result;
    }

    public static int GetNextPowerOfTwo(int n)
    {
        if (n <= 0) return 1;
        if ((n & (n - 1)) == 0) return n;
        int p = 1;
        while (p < n) p <<= 1;
        return p;
    }
}
