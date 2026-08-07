namespace Kst.Acoustics.Core.ImpactSurface
{
    /// <summary>
    /// Darbe emici malzeme özelliklerini tutan model.
    /// </summary>
    public class ImpactMaterial
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public double Density { get; set; } // kg/m³
        public double ElasticModulus { get; set; } // MPa
        public double ImpactAbsorption { get; set; } // % (0-100)
        public double AlphaW { get; set; } // Ağırlıklı ses emme katsayısı (0-1)
        public string Source { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty; // Wear, Acoustic, Impact, Drainage, Structural
    }
}
