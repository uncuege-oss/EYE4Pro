using System.Collections.Generic;
using System.Linq;

namespace Kst.Acoustics.Core.ImpactSurface
{
    /// <summary>
    /// Katman koleksiyonunu ve temel yığın özelliklerini yöneten sınıf.
    /// </summary>
    public class ImpactLayerStack
    {
        public List<ImpactLayer> Layers { get; set; } = new List<ImpactLayer>();

        public double TotalThickness => Layers.Where(l => l.IsEnabled).Sum(l => l.ThicknessMm);

        public double TotalWeight
        {
            get
            {
                double weight = 0;
                foreach (var layer in Layers.Where(l => l.IsEnabled))
                {
                    var material = layer.Material ?? ImpactMaterialDatabase.GetByCode(layer.MaterialCode);
                    if (material != null)
                    {
                        // kg/m² = (kg/m³) * (mm / 1000)
                        weight += material.Density * (layer.ThicknessMm / 1000.0);
                    }
                }
                return weight;
            }
        }

        /// <summary>
        /// Basitleştirilmiş kompozit AlphaW hesabı.
        /// Gerçekte katman sırası ve kalınlık frekansa bağlıdır, 
        /// ancak burada ağırlıklı bir ortalama veya baskın katman yaklaşımı kullanılabilir.
        /// </summary>
        public double CalculateCompositeAlphaW()
        {
            var activeLayers = Layers.Where(l => l.IsEnabled).ToList();
            if (activeLayers.Count == 0) return 0;

            double totalAlpha = 0;
            double totalWeighting = 0;

            foreach (var layer in activeLayers)
            {
                var material = layer.Material ?? ImpactMaterialDatabase.GetByCode(layer.MaterialCode);
                if (material != null)
                {
                    // Kalınlık arttıkça akustik katkı artar (logaritmik veya lineer proxy)
                    double weight = layer.ThicknessMm;
                    totalAlpha += material.AlphaW * weight;
                    totalWeighting += weight;
                }
            }

            return totalWeighting > 0 ? totalAlpha / totalWeighting : 0;
        }

        public void Reindex()
        {
            for (int i = 0; i < Layers.Count; i++)
            {
                Layers[i].Index = i;
            }
        }
    }
}
