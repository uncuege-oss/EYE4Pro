using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Kst.Acoustics.Core.AbsorptiveSurface;

namespace Kst.Acoustics.App.ViewModels;

public partial class AbsorptiveLayerRowViewModel : ViewModelBase
{
    [ObservableProperty] private int _index;
    [ObservableProperty] private string _materialCode = string.Empty;
    [ObservableProperty] private AbsorptiveLayerType _layerType = AbsorptiveLayerType.PorousAbsorber;
    [ObservableProperty] private double _thicknessMm = 25;
    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private double _openAreaPercent = 15;
    [ObservableProperty] private double _holeDiameterMm = 6;
    [ObservableProperty] private double _slotWidthMm = 8;
    [ObservableProperty] private double _slotSpacingMm = 40;
    [ObservableProperty] private AbsorptiveMaterial? _material;

    public ObservableCollection<AbsorptiveMaterial> AvailableMaterials { get; }
    public AbsorptiveLayerType[] LayerTypes { get; } = System.Enum.GetValues<AbsorptiveLayerType>();

    public AbsorptiveLayerRowViewModel(AbsorptiveLayer layer, ObservableCollection<AbsorptiveMaterial> sharedMaterials)
    {
        AvailableMaterials = sharedMaterials;
        _index = layer.Index;
        _materialCode = layer.MaterialCode;
        _layerType = layer.LayerType;
        _thicknessMm = layer.ThicknessMm;
        _isEnabled = layer.IsEnabled;
        _openAreaPercent = layer.OpenAreaPercent;
        _holeDiameterMm = layer.HoleDiameterMm;
        _slotWidthMm = layer.SlotWidthMm;
        _slotSpacingMm = layer.SlotSpacingMm;
        UpdateMaterial();
    }

    partial void OnMaterialCodeChanged(string value) => UpdateMaterial();

    partial void OnMaterialChanged(AbsorptiveMaterial? value)
    {
        if (value == null)
            return;

        if (MaterialCode != value.Code)
            MaterialCode = value.Code;
        LayerType = value.LayerType;
    }

    private void UpdateMaterial()
    {
        var newMaterial = AvailableMaterials.FirstOrDefault(m => m.Code == MaterialCode)
                          ?? AbsorptiveMaterialDatabase.GetByCode(MaterialCode);
        if (Material != newMaterial)
            Material = newMaterial;
    }

    public AbsorptiveLayer ToModel() => new()
    {
        Index = Index,
        MaterialCode = MaterialCode,
        LayerType = LayerType,
        ThicknessMm = ThicknessMm,
        IsEnabled = IsEnabled,
        OpenAreaPercent = OpenAreaPercent,
        HoleDiameterMm = HoleDiameterMm,
        SlotWidthMm = SlotWidthMm,
        SlotSpacingMm = SlotSpacingMm,
        Material = Material
    };
}
