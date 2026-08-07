namespace Kst.Acoustics.Core.ImpactSurface
{
    /// <summary>
    /// Darbe ve akustik hesaplama sonuçlarını tutan DTO.
    /// </summary>
    public class ImpactCalculationResult
    {
        public double Gmax { get; set; }
        public double HIC { get; set; }
        public double CriticalFallHeight { get; set; } // CFH (m)
        public double CoefficientOfRestitution { get; set; } // COR (0-1)
        public double TotalAlphaW { get; set; }
        public double TotalThickness { get; set; } // mm
        public double TotalWeight { get; set; } // kg/m²
        public double Deformation { get; set; } // mm
        public double PulseDurationMs { get; set; } // İvme pulsesinin toplam süresi

        public double[] AccelSeries { get; set; } = System.Array.Empty<double>();
        
        public bool IsPassed => IsValid && Gmax < 200 && HIC < 1000;
        public bool IsValid { get; set; } = true;
        public List<string> Errors { get; set; } = new();
        public string StatusMessage { get; set; } = string.Empty;
    }
}
