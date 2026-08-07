using System.Collections.Generic;
using System.Linq;

namespace Kst.Acoustics.Core.ImpactSurface
{
    public static class ImpactMaterialDatabase
    {
        private static readonly List<ImpactMaterial> _materials = new List<ImpactMaterial>
        {
            // Elastomerik
            new ImpactMaterial { Code = "EVA", Name = "EVA Köpüğü", Density = 115, ElasticModulus = 2.75, ImpactAbsorption = 70, AlphaW = 0.15, Source = "ASTM F1292", Category = "Impact" },
            new ImpactMaterial { Code = "EPDM-G", Name = "EPDM Granül", Density = 550, ElasticModulus = 4.5, ImpactAbsorption = 65, AlphaW = 0.20, Source = "EN 1177", Category = "Impact" },
            new ImpactMaterial { Code = "EPDM-S", Name = "EPDM Levha", Density = 750, ElasticModulus = 9.0, ImpactAbsorption = 60, AlphaW = 0.05, Source = "EN 14808", Category = "Wear" },
            new ImpactMaterial { Code = "NBR", Name = "Nitril Kauçuk", Density = 1050, ElasticModulus = 12.5, ImpactAbsorption = 52, AlphaW = 0.05, Source = "ISO 4649", Category = "Impact" },
            new ImpactMaterial { Code = "NEO", Name = "Neopren", Density = 1150, ElasticModulus = 19.0, ImpactAbsorption = 45, AlphaW = 0.05, Source = "ASTM D2000", Category = "Impact" },
            new ImpactMaterial { Code = "NR", Name = "Doğal Kauçuk", Density = 1000, ElasticModulus = 3.0, ImpactAbsorption = 75, AlphaW = 0.10, Source = "ISO 48-2", Category = "Impact" },

            // Köpük
            new ImpactMaterial { Code = "PU-VE", Name = "Visko PU (Memory)", Density = 55, ElasticModulus = 0.11, ImpactAbsorption = 90, AlphaW = 0.65, Source = "ASTM D3574", Category = "Acoustic" },
            new ImpactMaterial { Code = "PU-HR", Name = "Yüksek Esneklikli PU", Density = 42, ElasticModulus = 0.27, ImpactAbsorption = 79, AlphaW = 0.75, Source = "ASTM D3574", Category = "Acoustic" },
            new ImpactMaterial { Code = "PE-LD", Name = "Düşük Yoğunluklu PE", Density = 35, ElasticModulus = 0.06, ImpactAbsorption = 82, AlphaW = 0.40, Source = "ISO 1798", Category = "Impact" },
            new ImpactMaterial { Code = "PE-XL", Name = "Çapraz Bağlı PE", Density = 115, ElasticModulus = 1.05, ImpactAbsorption = 77, AlphaW = 0.30, Source = "ASTM D1056", Category = "Impact" },
            new ImpactMaterial { Code = "EPS", Name = "Gen. Polistiren", Density = 22, ElasticModulus = 6.5, ImpactAbsorption = 50, AlphaW = 0.10, Source = "EN 826", Category = "Structural" },
            new ImpactMaterial { Code = "XPS", Name = "Ekstrüde Polistiren", Density = 36, ElasticModulus = 40.0, ImpactAbsorption = 40, AlphaW = 0.05, Source = "EN 826", Category = "Structural" },
            new ImpactMaterial { Code = "XLPE", Name = "Çapraz Bağlı PE", Density = 65, ElasticModulus = 1.65, ImpactAbsorption = 79, AlphaW = 0.35, Source = "ASTM D1056", Category = "Impact" },

            // Doğal
            new ImpactMaterial { Code = "CORK", Name = "Mantar Levha", Density = 175, ElasticModulus = 17.5, ImpactAbsorption = 60, AlphaW = 0.45, Source = "EN 12235", Category = "Acoustic" },
            new ImpactMaterial { Code = "CORK-R", Name = "Geri Dönüşüm Mantar", Density = 225, ElasticModulus = 11.5, ImpactAbsorption = 63, AlphaW = 0.50, Source = "EN 1177", Category = "Impact" },
            new ImpactMaterial { Code = "COCONU", Name = "Hindistan Cevizi Lifi", Density = 90, ElasticModulus = 1.75, ImpactAbsorption = 55, AlphaW = 0.70, Source = "LND", Category = "Acoustic" },
            new ImpactMaterial { Code = "WOOL-M", Name = "Mineral Yün", Density = 120, ElasticModulus = 0.30, ImpactAbsorption = 67, AlphaW = 0.90, Source = "EN ISO 354", Category = "Acoustic" },
            new ImpactMaterial { Code = "WOOL-N", Name = "Doğal Yün Keçe", Density = 300, ElasticModulus = 1.75, ImpactAbsorption = 72, AlphaW = 0.85, Source = "LND", Category = "Acoustic" },
            new ImpactMaterial { Code = "HEMP", Name = "Kenevir Lifi", Density = 115, ElasticModulus = 3.0, ImpactAbsorption = 62, AlphaW = 0.80, Source = "LND", Category = "Acoustic" },

            // Geri Dönüşüm
            new ImpactMaterial { Code = "SBR-G", Name = "SBR Granül", Density = 650, ElasticModulus = 6.0, ImpactAbsorption = 63, AlphaW = 0.25, Source = "EN 1177", Category = "Impact" },
            new ImpactMaterial { Code = "SBR-P", Name = "SBR Preslenmiş", Density = 850, ElasticModulus = 12.5, ImpactAbsorption = 59, AlphaW = 0.15, Source = "EN 14808", Category = "Wear" },
            new ImpactMaterial { Code = "CRUMB", Name = "Kramp Granül", Density = 575, ElasticModulus = 7.5, ImpactAbsorption = 61, AlphaW = 0.20, Source = "EN 1177", Category = "Impact" },
            new ImpactMaterial { Code = "FELT-R", Name = "Geri Dönüşüm Keçe", Density = 200, ElasticModulus = 2.75, ImpactAbsorption = 69, AlphaW = 0.75, Source = "LND", Category = "Acoustic" },

            // Özel
            new ImpactMaterial { Code = "VE-PAD", Name = "Visko Akustik Pad", Density = 1150, ElasticModulus = 30.0, ImpactAbsorption = 80, AlphaW = 0.30, Source = "LND", Category = "Impact" },
            new ImpactMaterial { Code = "GEL", Name = "Silikon Jel", Density = 1000, ElasticModulus = 0.55, ImpactAbsorption = 94, AlphaW = 0.10, Source = "ASTM D575", Category = "Impact" },
            new ImpactMaterial { Code = "AIR", Name = "Hava Katmanı", Density = 1.2, ElasticModulus = 0.01, ImpactAbsorption = 97, AlphaW = 0.05, Source = "LND", Category = "Drainage" },
            new ImpactMaterial { Code = "D3D-TPU", Name = "3B Baskı TPU", Density = 175, ElasticModulus = 5.25, ImpactAbsorption = 87, AlphaW = 0.45, Source = "LND", Category = "Impact" },
            
            // Yapısal
            new ImpactMaterial { Code = "CONC", Name = "Beton", Density = 2400, ElasticModulus = 30000, ImpactAbsorption = 1, AlphaW = 0.02, Source = "EN 1992", Category = "Structural" },
            new ImpactMaterial { Code = "WOOD", Name = "Ahşap", Density = 600, ElasticModulus = 10000, ImpactAbsorption = 5, AlphaW = 0.10, Source = "EN 1995", Category = "Structural" },
            new ImpactMaterial { Code = "STEEL", Name = "Çelik", Density = 7850, ElasticModulus = 210000, ImpactAbsorption = 0.5, AlphaW = 0.01, Source = "EN 1993", Category = "Structural" },
            new ImpactMaterial { Code = "GRAVEL", Name = "Çakıl", Density = 1600, ElasticModulus = 50.0, ImpactAbsorption = 30, AlphaW = 0.30, Source = "LND", Category = "Drainage" }
        };

        public static List<ImpactMaterial> GetAll() => _materials;
        
        public static ImpactMaterial? GetByCode(string code) => 
            _materials.FirstOrDefault(m => m.Code == code);

        public static List<ImpactMaterial> GetByCategory(string category) => 
            _materials.Where(m => m.Category == category).ToList();
    }
}
