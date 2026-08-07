using System;

namespace Kst.Acoustics.Core.Measurement.Analysis;

public enum SplWeighting { A, C, Z }
public enum SplTimeConstant { Fast, Slow, Impuls }

/// <summary>
/// Ses Basınç Seviyesi (SPL) ölçüm cihazı mantığı.
/// </summary>
public class SplMeter
{
    private double _currentLevel;
    private readonly double _timeConstantMs;
    private double _calibrationOffset = 0; // dBSPL kalibrasyonu için

    public SplMeter(SplTimeConstant timeConstant = SplTimeConstant.Fast)
    {
        _timeConstantMs = timeConstant switch
        {
            SplTimeConstant.Fast => 125,
            SplTimeConstant.Slow => 1000,
            SplTimeConstant.Impuls => 35,
            _ => 125
        };
    }

    public void SetCalibration(double offset) => _calibrationOffset = offset;

    public double Process(float[] samples, int sampleRate)
    {
        if (samples.Length == 0) return _currentLevel;

        // 1. RMS hesapla (Z-weighting varsayımıyla, A/C filtreleri dışarıda uygulanabilir)
        double rms = 0;
        foreach (var s in samples) rms += s * s;
        rms = Math.Sqrt(rms / samples.Length);

        // 2. dBFS'e çevir
        double dbfs = 20 * Math.Log10(Math.Max(rms, 1e-12));

        // 3. Kalibrasyon ekle
        double targetLevel = dbfs + _calibrationOffset;

        // 4. Zaman sabiti ile yumuşat (Exponential averaging)
        // alpha = 1 - exp(-t / tau)
        double t = (double)samples.Length / sampleRate * 1000.0;
        double alpha = 1.0 - Math.Exp(-t / _timeConstantMs);
        
        _currentLevel = _currentLevel + alpha * (targetLevel - _currentLevel);

        return _currentLevel;
    }

    public double CurrentLevel => _currentLevel;
}
