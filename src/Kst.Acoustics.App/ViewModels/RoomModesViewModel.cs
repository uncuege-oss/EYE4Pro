using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kst.Acoustics.Core.RoomModes;
using Kst.Acoustics.Core.RoomModes.Diffusers;

namespace Kst.Acoustics.App.ViewModels;

public partial class RoomModesViewModel : ViewModelBase
{
    [ObservableProperty]
    private double _lengthX = 5.0;

    [ObservableProperty]
    private double _lengthY = 4.0;

    [ObservableProperty]
    private double _lengthZ = 3.0;

    [ObservableProperty]
    private double _temperature = 20.0;

    [ObservableProperty]
    private double _maxFrequency = 500.0;

    [ObservableProperty]
    private double? _rt60;

    [ObservableProperty]
    private RoomModeResult? _result;

    [ObservableProperty]
    private RoomMode? _selectedMode;

    [ObservableProperty]
    private List<PressurePoint> _pressurePoints = new();

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private double _diffuserTargetFrequency = 250.0;

    [ObservableProperty]
    private int _diffuserPrimeOrder = 7;

    [ObservableProperty]
    private double _diffuserWellWidthMm = 70.0;

    [ObservableProperty]
    private double _diffuserPanelHeightMm = 600.0;

    [ObservableProperty]
    private double _diffuserMaxDepthMm = 250.0;

    [ObservableProperty]
    private DiffuserDesignResult? _diffuserResult;

    [ObservableProperty]
    private SkylineDiffuserResult? _skylineResult;

    private RoomModeInput? _lastInput;
    private bool _isCalculating;
    private bool _isUpdatingDiffuserTarget;

    public Action? RequestClose { get; set; }

    public RoomModesViewModel()
    {
        Calculate();
    }

    partial void OnLengthXChanged(double value) => CalculateIfReady();
    partial void OnLengthYChanged(double value) => CalculateIfReady();
    partial void OnLengthZChanged(double value) => CalculateIfReady();
    partial void OnTemperatureChanged(double value) => CalculateIfReady();
    partial void OnMaxFrequencyChanged(double value) => CalculateIfReady();
    partial void OnRt60Changed(double? value) => CalculateIfReady();
    partial void OnDiffuserTargetFrequencyChanged(double value) => CalculateDiffuserIfReady();
    partial void OnDiffuserPrimeOrderChanged(int value) => CalculateDiffuserIfReady();
    partial void OnDiffuserWellWidthMmChanged(double value) => CalculateDiffuserIfReady();
    partial void OnDiffuserPanelHeightMmChanged(double value) => CalculateDiffuserIfReady();
    partial void OnDiffuserMaxDepthMmChanged(double value) => CalculateDiffuserIfReady();

    [RelayCommand]
    private void Close()
    {
        RequestClose?.Invoke();
    }

    [RelayCommand]
    public void Calculate()
    {
        if (_isCalculating)
            return;

        _isCalculating = true;
        var input = new RoomModeInput
        {
            LengthX = LengthX,
            LengthY = LengthY,
            LengthZ = LengthZ,
            Temperature = Temperature,
            MaxFrequency = MaxFrequency,
            RT60 = Rt60
        };

        _lastInput = input;
        Result = RoomModeCalculator.Calculate(input);

        StatusMessage = Result.Warnings.Count > 0
            ? string.Join(" ", Result.Warnings)
            : $"Hesap tamamlandı. {Result.Modes.Count} mod bulundu.";

        if (Result.Modes.Any())
        {
            SelectedMode = Result.Modes.First();
        }
        else
        {
            SelectedMode = null;
            PressurePoints = new List<PressurePoint>();
        }

        _isCalculating = false;
        CalculateDiffuser();
    }

    private void CalculateIfReady()
    {
        if (_isCalculating)
            return;

        Calculate();
    }

    partial void OnSelectedModeChanged(RoomMode? value)
    {
        UpdatePressureField();
        if (value != null)
        {
            _isUpdatingDiffuserTarget = true;
            DiffuserTargetFrequency = Math.Round(value.Frequency, 1);
            _isUpdatingDiffuserTarget = false;
            CalculateDiffuser();
        }
    }

    private void UpdatePressureField()
    {
        if (SelectedMode == null || _lastInput == null)
        {
            PressurePoints = new List<PressurePoint>();
            return;
        }

        PressurePoints = PressureFieldSampler.SampleSurface(SelectedMode, _lastInput);
    }

    [RelayCommand]
    public void CalculateDiffuser()
    {
        var input = new DiffuserDesignInput
        {
            Type = DiffuserType.Qrd1D,
            TargetFrequencyHz = DiffuserTargetFrequency,
            PrimeOrder = DiffuserPrimeOrder,
            WellWidthMm = DiffuserWellWidthMm,
            PanelHeightMm = DiffuserPanelHeightMm,
            MaxDepthLimitMm = DiffuserMaxDepthMm,
            SoundSpeed = Result?.SpeedOfSound > 0 ? Result.SpeedOfSound : RoomModeCalculator.CalculateSpeedOfSound(Temperature),
            SchroederFrequencyHz = Result?.SchroederFrequency
        };

        DiffuserResult = QrdDiffuserCalculator.Calculate(input);
        SkylineResult = SkylineDiffuserCalculator.Calculate(input);
    }

    private void CalculateDiffuserIfReady()
    {
        if (_isUpdatingDiffuserTarget)
            return;

        CalculateDiffuser();
    }
}
