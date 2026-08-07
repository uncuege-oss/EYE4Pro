using System.Collections.ObjectModel;
using System.Linq;
using Kst.Acoustics.Core.ImpactSurface;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Kst.Acoustics.App.ViewModels
{
    public partial class ImpactLayerRowViewModel : ViewModelBase
    {
        [ObservableProperty]
        private int _index;

        [ObservableProperty]
        private string _materialCode = string.Empty;

        [ObservableProperty]
        private double _thicknessMm;

        [ObservableProperty]
        private bool _isEnabled = true;

        [ObservableProperty]
        private string _functionTag = string.Empty;

        [ObservableProperty]
        private ImpactMaterial? _material;

        public ObservableCollection<ImpactMaterial> AvailableMaterials { get; }

        public ImpactLayerRowViewModel(ImpactLayer layer, ObservableCollection<ImpactMaterial>? sharedMaterials = null)
        {
            _index = layer.Index;
            _materialCode = layer.MaterialCode;
            _thicknessMm = layer.ThicknessMm;
            _isEnabled = layer.IsEnabled;
            _functionTag = layer.FunctionTag;
            
            AvailableMaterials = sharedMaterials ?? new ObservableCollection<ImpactMaterial>(ImpactMaterialDatabase.GetAll());
            UpdateMaterial();
        }

        partial void OnMaterialCodeChanged(string value)
        {
            UpdateMaterial();
        }

        partial void OnMaterialChanged(ImpactMaterial? value)
        {
            if (value != null && MaterialCode != value.Code)
            {
                MaterialCode = value.Code;
                FunctionTag = value.Category;
            }
        }

        private void UpdateMaterial()
        {
            var newMat = AvailableMaterials.FirstOrDefault(m => m.Code == MaterialCode)
                         ?? ImpactMaterialDatabase.GetByCode(MaterialCode);
            if (Material != newMat)
            {
                Material = newMat;
            }
        }

        public ImpactLayer ToModel()
        {
            return new ImpactLayer
            {
                Index = Index,
                MaterialCode = MaterialCode,
                ThicknessMm = ThicknessMm,
                IsEnabled = IsEnabled,
                FunctionTag = FunctionTag,
                Material = Material
            };
        }
    }
}
