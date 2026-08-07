using System;
using System.Collections.Generic;

namespace Kst.Acoustics.Core.ImpactSurface
{
    public class ImpactSurfaceProject
    {
        public string Name { get; set; } = "Yeni Darbe Emici Yüzey Projesi";
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public List<ImpactLayer> Layers { get; set; } = new();
        public List<ImpactMaterial> CustomMaterials { get; set; } = new();
        public double DropHeight { get; set; } = 1.5;
        public double HeadMass { get; set; } = 5.0;
        public double ContactArea { get; set; } = 0.01;
        public string? TemplateName { get; set; }

        public ImpactCalculationResult? LastResult { get; set; }
    }
}
