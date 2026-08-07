using System;
using System.Collections.Generic;
using System.Linq;

namespace Kst.Acoustics.Core.Measurement.Processing;

public record AcousticBandParameters(
    double RT20, 
    double RT30, 
    double EDT, 
    double C50, 
    double C80, 
    double D50, 
    double Ts);

public static class AcousticParameterExtractor
{
    public static AcousticBandParameters Extract(float[] ir, int sampleRate)
    {
        // 0. Peak Hizalaması (Direct sound arrival tespiti)
        int peakIndex = 0;
        float maxVal = 0;
        for (int i = 0; i < Math.Min(ir.Length, sampleRate); i++) // İlk 1 saniye içinde ara
        {
            float absVal = Math.Abs(ir[i]);
            if (absVal > maxVal) { maxVal = absVal; peakIndex = i; }
        }
        
        // Peak'ten sonrasını al
        float[] alignedIr = ir.Skip(peakIndex).ToArray();

        // 1. Enerji Çürüme Eğrisi (EDC)
        float[] edc = SchroederIntegrator.CalculateEDC(alignedIr);

        // 2. RT60 Hesaplamaları (Lineer regresyon ile)
        double rt20 = CalculateRT(edc, sampleRate, -5, -25);
        double rt30 = CalculateRT(edc, sampleRate, -5, -35);
        double edt = CalculateRT(edc, sampleRate, 0, -10);

        // 3. Enerji Oranları
        double c50 = CalculateClarity(alignedIr, sampleRate, 50);
        double c80 = CalculateClarity(alignedIr, sampleRate, 80);
        double d50 = CalculateDefinition(alignedIr, sampleRate, 50);
        
        // 4. Center Time (Merkez Zaman)
        double ts = CalculateCenterTime(alignedIr, sampleRate);

        return new AcousticBandParameters(rt20, rt30, edt, c50, c80, d50, ts);
    }

    private static double CalculateRT(float[] edc, int fs, double startDb, double endDb)
    {
        var points = new List<(double Time, double Db)>();

        for (int i = 0; i < edc.Length; i++)
        {
            if (edc[i] <= startDb && edc[i] >= endDb)
            {
                points.Add(((double)i / fs, edc[i]));
            }
        }

        // Regresyon için yetersiz veri = hesaplanamaz (0 dB/s eğim veya geçersiz oda kararı gibi
        // yanıltıcı bir "geçerli sonuç" değil, NaN ile "ölçülemedi" olarak işaretlenir).
        if (points.Count < 2) return double.NaN;

        double meanTime = points.Average(p => p.Time);
        double meanDb = points.Average(p => p.Db);
        double covariance = 0;
        double variance = 0;

        foreach (var point in points)
        {
            var dt = point.Time - meanTime;
            covariance += dt * (point.Db - meanDb);
            variance += dt * dt;
        }

        if (variance <= 0) return double.NaN;

        var slopeDbPerSecond = covariance / variance;
        if (slopeDbPerSecond >= 0 || !double.IsFinite(slopeDbPerSecond))
            return double.NaN;

        return -60.0 / slopeDbPerSecond;
    }

    private static double CalculateClarity(float[] ir, int fs, double ms)
    {
        int limit = (int)(ms * fs / 1000.0);
        if (limit >= ir.Length) return double.NaN;

        double earlyEnergy = 0;
        double lateEnergy = 0;

        for (int i = 0; i < ir.Length; i++)
        {
            double e = ir[i] * ir[i];
            if (i < limit) earlyEnergy += e;
            else lateEnergy += e;
        }

        if (lateEnergy == 0) return double.NaN;
        return 10 * Math.Log10(earlyEnergy / lateEnergy);
    }

    private static double CalculateDefinition(float[] ir, int fs, double ms)
    {
        int limit = (int)(ms * fs / 1000.0);
        double earlyEnergy = 0;
        double totalEnergy = 0;

        for (int i = 0; i < ir.Length; i++)
        {
            double e = ir[i] * ir[i];
            if (i < limit) earlyEnergy += e;
            totalEnergy += e;
        }

        if (totalEnergy == 0) return double.NaN;
        return earlyEnergy / totalEnergy;
    }

    private static double CalculateCenterTime(float[] ir, int fs)
    {
        double numerator = 0;
        double denominator = 0;

        for (int i = 0; i < ir.Length; i++)
        {
            double t = (double)i / fs;
            double e = ir[i] * ir[i];
            numerator += t * e;
            denominator += e;
        }

        if (denominator == 0) return double.NaN;
        return (numerator / denominator) * 1000.0; // ms
    }
}
