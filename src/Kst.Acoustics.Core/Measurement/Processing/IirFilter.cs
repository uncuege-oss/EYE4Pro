using System;

namespace Kst.Acoustics.Core.Measurement.Processing;

public enum FilterType { LowShelf, HighShelf, Peak, Notch, LowPass, HighPass }

public class BiquadFilter
{
    public double A0, A1, A2, B0, B1, B2;

    public static BiquadFilter CreatePeak(double freq, double gainDb, double q, double fs)
    {
        var f = new BiquadFilter();
        double a = Math.Pow(10, gainDb / 40.0);
        double w0 = 2 * Math.PI * freq / fs;
        double alpha = Math.Sin(w0) / (2 * q);

        f.B0 = 1 + alpha * a;
        f.B1 = -2 * Math.Cos(w0);
        f.B2 = 1 - alpha * a;
        f.A0 = 1 + alpha / a;
        f.A1 = -2 * Math.Cos(w0);
        f.A2 = 1 - alpha / a;

        // Normalize
        f.B0 /= f.A0; f.B1 /= f.A0; f.B2 /= f.A0;
        f.A1 /= f.A0; f.A2 /= f.A0; f.A0 = 1.0;

        return f;
    }

    public double GetResponse(double freq, double fs)
    {
        double w = 2 * Math.PI * freq / fs;
        double phi = Math.Pow(Math.Sin(w / 2), 2);
        
        double magSq = (Math.Pow(B0 + B1 + B2, 2) - 4 * (B0 * B1 + 4 * B0 * B2 + B1 * B2) * phi + 16 * B0 * B2 * phi * phi) /
                       (Math.Pow(1 + A1 + A2, 2) - 4 * (A1 + 4 * A2 + A1 * A2) * phi + 16 * A2 * phi * phi);
        
        return 10 * Math.Log10(Math.Max(magSq, 1e-12));
    }
}
