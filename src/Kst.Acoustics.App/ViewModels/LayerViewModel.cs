using CommunityToolkit.Mvvm.ComponentModel;
using Kst.Acoustics.Core.PanelDesign;

namespace Kst.Acoustics.App.ViewModels;

public partial class LayerViewModel : ViewModelBase
{
    private readonly AcousticLayer _model;
    private static readonly IReadOnlyList<string> PatternNames = ["Düz", "Şaşırtmalı"];

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private bool _isIncludedInCalculation = true;

    public AcousticLayer Model => _model;

    public LayerViewModel(AcousticLayer model, string name)
    {
        _model = model;
        _name = name;
    }

    public double Thickness
    {
        get => _model.Thickness * 1000.0; // UI uses mm
        set
        {
            double clamped = Math.Clamp(value, 0.0, 5000.0);
            if (SetProperty(_model.Thickness, clamped / 1000.0, _model, (m, v) => m.Thickness = v))
            {
                OnPropertyChanged(nameof(Thickness));
                OnLayerChanged();
            }
        }
    }

    // Properties for PorousLayer
    public bool IsAir => _model is AirLayer;
    public bool IsPorous => _model is PorousLayer;
    public bool IsFabric => _model is FabricLayer;
    public double FlowResistivity
    {
        get => _model is PorousLayer p ? p.FlowResistivity : 0;
        set
        {
            double clamped = Math.Clamp(value, 1.0, 1_000_000.0);
            if (_model is PorousLayer p && p.FlowResistivity != clamped)
            {
                p.FlowResistivity = clamped;
                OnPropertyChanged();
                OnLayerChanged();
            }
        }
    }

    public IReadOnlyList<string> FabricPresetOptions => ["Özel", "SoundTex S 65", "SoundTex S 100", "SoundTex S 130"];

    public string SelectedFabricPreset
    {
        get => _selectedFabricPreset;
        set
        {
            if (SetProperty(ref _selectedFabricPreset, value) && _model is FabricLayer f)
            {
                if (value == "SoundTex S 65") FabricLayer.ApplySoundTex65(f);
                else if (value == "SoundTex S 100") FabricLayer.ApplySoundTex100(f);
                else if (value == "SoundTex S 130") FabricLayer.ApplySoundTex130(f);
                
                OnPropertyChanged(nameof(SpecificFlowResistance));
                OnPropertyChanged(nameof(SurfaceDensity));
                OnLayerChanged();
            }
        }
    }
    private string _selectedFabricPreset = "Özel";

    public double SpecificFlowResistance
    {
        get => _model is FabricLayer f ? f.SpecificFlowResistance : 0;
        set
        {
            double clamped = Math.Clamp(value, 0.0, 100_000.0);
            if (_model is FabricLayer f && f.SpecificFlowResistance != clamped)
            {
                f.SpecificFlowResistance = clamped;
                _selectedFabricPreset = "Özel";
                OnPropertyChanged(nameof(SelectedFabricPreset));
                OnPropertyChanged();
                OnLayerChanged();
            }
        }
    }

    public double SurfaceDensity
    {
        get => _model is FabricLayer f ? f.SurfaceDensity : 0;
        set
        {
            double clamped = Math.Clamp(value, 0.0, 5.0);
            if (_model is FabricLayer f && f.SurfaceDensity != clamped)
            {
                f.SurfaceDensity = clamped;
                _selectedFabricPreset = "Özel";
                OnPropertyChanged(nameof(SelectedFabricPreset));
                OnPropertyChanged();
                OnLayerChanged();
            }
        }
    }

    // Properties for PerforatedLayer
    public bool IsPerforated => _model is PerforatedLayer;
    public bool UsesCustomOpenings => _model is PerforatedLayer { CustomOpenings.Count: > 0 };
    public int CustomOpeningCount => _model is PerforatedLayer { CustomOpenings.Count: > 0 } p ? p.CustomOpenings.Count : 0;
    public IReadOnlyList<string> HolePatternOptions => PatternNames;

    public string HolePatternName
    {
        get => _model is PerforatedLayer p && p.HolePattern == HolePattern.Staggered ? "Şaşırtmalı" : "Düz";
        set
        {
            if (_model is not PerforatedLayer p) return;

            var pattern = value == "Şaşırtmalı" ? HolePattern.Staggered : HolePattern.Square;
            if (p.HolePattern == pattern) return;

            p.HolePattern = pattern;
            ClearDxfImportState(p);
            p.OpenAreaRatio = PerforatedLayer.CalculateOpenAreaRatio(p.HoleDiameter, p.HoleSpacing, p.HolePattern);
            OnPropertyChanged();
            OnPropertyChanged(nameof(OpenAreaRatio));
            OnLayerChanged();
        }
    }

    private bool _isUpdating;

    public double OpenAreaRatio
    {
        get => _model is PerforatedLayer p ? p.OpenAreaRatio * 100.0 : 0; // UI uses %
        set
        {
            if (_isUpdating) return;
            double clamped = Math.Clamp(
                value,
                PerforatedLayer.MinOpenAreaRatio * 100.0,
                PerforatedLayer.MaxOpenAreaRatio * 100.0);
            if (_model is PerforatedLayer p && (p.OpenAreaRatio * 100.0) != clamped)
            {
                _isUpdating = true;
                try
                {
                    p.OpenAreaRatio = clamped / 100.0;
                    ClearDxfImportState(p);
                    p.HoleSpacing = PerforatedLayer.CalculateSpacingForOpenAreaRatio(p.HoleDiameter, p.OpenAreaRatio, p.HolePattern);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HoleSpacing));
                    OnLayerChanged();
                }
                finally { _isUpdating = false; }
            }
        }
    }

    public double HoleDiameter
    {
        get => _model is PerforatedLayer p ? p.HoleDiameter * 1000.0 : 0; // UI uses mm
        set
        {
            if (_isUpdating) return;
            double clamped = Math.Clamp(value, 0.01, 200.0);
            if (_model is PerforatedLayer p && (p.HoleDiameter * 1000.0) != clamped)
            {
                _isUpdating = true;
                try
                {
                    p.HoleDiameter = clamped / 1000.0;
                    ClearDxfImportState(p);
                    p.OpenAreaRatio = PerforatedLayer.CalculateOpenAreaRatio(p.HoleDiameter, p.HoleSpacing, p.HolePattern);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(OpenAreaRatio));
                    OnLayerChanged();
                }
                finally { _isUpdating = false; }
            }
        }
    }

    public double HoleSpacing
    {
        get => _model is PerforatedLayer p ? p.HoleSpacing * 1000.0 : 0; // UI uses mm
        set
        {
            if (_isUpdating) return;
            double clamped = Math.Clamp(value, 0.1, 1000.0);
            if (_model is PerforatedLayer p && (p.HoleSpacing * 1000.0) != clamped)
            {
                _isUpdating = true;
                try
                {
                    p.HoleSpacing = clamped / 1000.0;
                    ClearDxfImportState(p);
                    p.OpenAreaRatio = PerforatedLayer.CalculateOpenAreaRatio(p.HoleDiameter, p.HoleSpacing, p.HolePattern);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(OpenAreaRatio));
                    OnLayerChanged();
                }
                finally { _isUpdating = false; }
            }
        }
    }

    [ObservableProperty]
    private IReadOnlyList<DxfEntity>? _dxfEntities;

    public void ApplyDxfImport(
        IReadOnlyList<DxfEntity> entities,
        IReadOnlyList<PerforationOpening>? customOpenings)
    {
        if (_model is not PerforatedLayer p) return;

        p.CustomOpenings = customOpenings;
        DxfEntities = entities;
        OnPropertyChanged(nameof(UsesCustomOpenings));
        OnPropertyChanged(nameof(CustomOpeningCount));
        OnLayerChanged();
    }

    public event EventHandler? LayerChanged;

    private void OnLayerChanged() => LayerChanged?.Invoke(this, EventArgs.Empty);

    partial void OnIsIncludedInCalculationChanged(bool value) => OnLayerChanged();

    private void ClearDxfImportState(PerforatedLayer layer)
    {
        bool hadCustomOpenings = layer.CustomOpenings is { Count: > 0 };
        bool hadDxfEntities = DxfEntities is { Count: > 0 };

        layer.CustomOpenings = null;
        if (DxfEntities != null)
            DxfEntities = null;

        if (hadCustomOpenings || hadDxfEntities)
        {
            OnPropertyChanged(nameof(UsesCustomOpenings));
            OnPropertyChanged(nameof(CustomOpeningCount));
        }
    }
}
