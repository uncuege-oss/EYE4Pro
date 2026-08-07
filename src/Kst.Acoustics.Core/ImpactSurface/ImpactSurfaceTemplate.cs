using System.Collections.Generic;

namespace Kst.Acoustics.Core.ImpactSurface
{
    public class ImpactSurfaceTemplate
    {
        public string Name { get; set; } = string.Empty;
        public List<ImpactLayer> Layers { get; set; } = new List<ImpactLayer>();

        public static List<ImpactSurfaceTemplate> GetDefaultTemplates()
        {
            return new List<ImpactSurfaceTemplate>
            {
                new ImpactSurfaceTemplate
                {
                    Name = "Çocuk Oyun Alanı",
                    Layers = new List<ImpactLayer>
                    {
                        new ImpactLayer { Index = 0, MaterialCode = "CONC", ThicknessMm = 150, FunctionTag = "Structural" },
                        new ImpactLayer { Index = 1, MaterialCode = "PE-XL", ThicknessMm = 20, FunctionTag = "Impact" },
                        new ImpactLayer { Index = 2, MaterialCode = "SBR-G", ThicknessMm = 40, FunctionTag = "Impact" }
                    }
                },
                new ImpactSurfaceTemplate
                {
                    Name = "Spor Salonu",
                    Layers = new List<ImpactLayer>
                    {
                        new ImpactLayer { Index = 0, MaterialCode = "WOOD", ThicknessMm = 22, FunctionTag = "Structural" },
                        new ImpactLayer { Index = 1, MaterialCode = "PU-HR", ThicknessMm = 20, FunctionTag = "Acoustic" },
                        new ImpactLayer { Index = 2, MaterialCode = "EVA", ThicknessMm = 12, FunctionTag = "Wear" }
                    }
                },
                new ImpactSurfaceTemplate
                {
                    Name = "Konser Salonu",
                    Layers = new List<ImpactLayer>
                    {
                        new ImpactLayer { Index = 0, MaterialCode = "CONC", ThicknessMm = 200, FunctionTag = "Structural" },
                        new ImpactLayer { Index = 1, MaterialCode = "PU-VE", ThicknessMm = 30, FunctionTag = "Acoustic" },
                        new ImpactLayer { Index = 2, MaterialCode = "VE-PAD", ThicknessMm = 10, FunctionTag = "Impact" },
                        new ImpactLayer { Index = 3, MaterialCode = "WOOL-M", ThicknessMm = 50, FunctionTag = "Acoustic" },
                        new ImpactLayer { Index = 4, MaterialCode = "EPDM-S", ThicknessMm = 5, FunctionTag = "Wear" }
                    }
                },
                new ImpactSurfaceTemplate
                {
                    Name = "Açık Hava Sahne",
                    Layers = new List<ImpactLayer>
                    {
                        new ImpactLayer { Index = 0, MaterialCode = "CONC", ThicknessMm = 150, FunctionTag = "Structural" },
                        new ImpactLayer { Index = 1, MaterialCode = "GRAVEL", ThicknessMm = 30, FunctionTag = "Drainage" },
                        new ImpactLayer { Index = 2, MaterialCode = "SBR-P", ThicknessMm = 20, FunctionTag = "Impact" },
                        new ImpactLayer { Index = 3, MaterialCode = "EPDM-G", ThicknessMm = 50, FunctionTag = "Wear" }
                    }
                }
            };
        }
    }
}
