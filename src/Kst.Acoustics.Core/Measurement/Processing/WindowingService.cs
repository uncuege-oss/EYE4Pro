using System;

namespace Kst.Acoustics.Core.Measurement.Processing;

public enum WindowType
{
    Rectangular,
    Hann,
    Hamming,
    Tukey,
    Blackman
}

public static class WindowingService
{
    public static void Apply(float[] data, WindowType type, double alpha = 0.5)
    {
        int n = data.Length;
        for (int i = 0; i < n; i++)
        {
            data[i] *= (float)GetWeight(i, n, type, alpha);
        }
    }

    public static double GetWeight(int index, int total, WindowType type, double alpha = 0.5)
    {
        double x = (double)index / (total - 1);
        
        return type switch
        {
            WindowType.Rectangular => 1.0,
            WindowType.Hann => 0.5 * (1 - Math.Cos(2 * Math.PI * x)),
            WindowType.Hamming => 0.54 - 0.46 * Math.Cos(2 * Math.PI * x),
            WindowType.Tukey => TukeyWeight(x, alpha),
            WindowType.Blackman => 0.42 - 0.5 * Math.Cos(2 * Math.PI * x) + 0.08 * Math.Cos(4 * Math.PI * x),
            _ => 1.0
        };
    }

    private static double TukeyWeight(double x, double alpha)
    {
        if (x < alpha / 2)
            return 0.5 * (1 + Math.Cos(Math.PI * (2 * x / alpha - 1)));
        if (x > 1 - alpha / 2)
            return 0.5 * (1 + Math.Cos(Math.PI * (2 * (1 - x) / alpha - 1)));
        return 1.0;
    }
}
