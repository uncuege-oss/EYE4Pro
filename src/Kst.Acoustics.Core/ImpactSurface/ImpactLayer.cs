namespace Kst.Acoustics.Core.ImpactSurface
{
    /// <summary>
    /// Tek bir darbe emici katmanın özelliklerini tutan model.
    /// </summary>
    public class ImpactLayer
    {
        public int Index { get; set; }
        public string MaterialCode { get; set; } = string.Empty;
        public double ThicknessMm { get; set; }
        public bool IsEnabled { get; set; } = true;
        public string FunctionTag { get; set; } = string.Empty; // Wear, Acoustic, Impact, Drainage, Structural
        public double Opacity { get; set; } = 1.0;

        // UI ve hesaplama kolaylığı için
        public ImpactMaterial? Material { get; set; }
    }
}
