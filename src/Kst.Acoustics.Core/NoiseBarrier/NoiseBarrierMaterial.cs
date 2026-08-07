using System.Collections.Generic;

namespace Kst.Acoustics.Core.NoiseBarrier;

public class NoiseBarrierMaterial
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public double ThicknessMm { get; set; }
    public double SurfaceMass { get; set; } // kg/m2
    
    // Absorption coefficients per octave band (63Hz to 8kHz)
    public Dictionary<double, double> AbsorptionCoefficients { get; set; } = new();
    
    // Transmission Loss per octave band
    public Dictionary<double, double> TransmissionLoss { get; set; } = new();
    
    public double Rw { get; set; }
    public double Nrc { get; set; }

    // Helper properties for UI binding
    public double Tl63 { get => GetTl(63); set => SetTl(63, value); }
    public double Tl125 { get => GetTl(125); set => SetTl(125, value); }
    public double Tl250 { get => GetTl(250); set => SetTl(250, value); }
    public double Tl500 { get => GetTl(500); set => SetTl(500, value); }
    public double Tl1k { get => GetTl(1000); set => SetTl(1000, value); }
    public double Tl2k { get => GetTl(2000); set => SetTl(2000, value); }
    public double Tl4k { get => GetTl(4000); set => SetTl(4000, value); }
    public double Tl8k { get => GetTl(8000); set => SetTl(8000, value); }

    public double Alpha63 { get => GetAlpha(63); set => SetAlpha(63, value); }
    public double Alpha125 { get => GetAlpha(125); set => SetAlpha(125, value); }
    public double Alpha250 { get => GetAlpha(250); set => SetAlpha(250, value); }
    public double Alpha500 { get => GetAlpha(500); set => SetAlpha(500, value); }
    public double Alpha1k { get => GetAlpha(1000); set => SetAlpha(1000, value); }
    public double Alpha2k { get => GetAlpha(2000); set => SetAlpha(2000, value); }
    public double Alpha4k { get => GetAlpha(4000); set => SetAlpha(4000, value); }
    public double Alpha8k { get => GetAlpha(8000); set => SetAlpha(8000, value); }

    private double GetTl(double freq) => TransmissionLoss.TryGetValue(freq, out var v) ? v : Rw;
    private void SetTl(double freq, double v) => TransmissionLoss[freq] = v;
    private double GetAlpha(double freq) => AbsorptionCoefficients.TryGetValue(freq, out var v) ? v : Nrc;
    private void SetAlpha(double freq, double v) => AbsorptionCoefficients[freq] = v;

    public string Source { get; set; } = string.Empty;
    public int Confidence { get; set; } = 100;
}
