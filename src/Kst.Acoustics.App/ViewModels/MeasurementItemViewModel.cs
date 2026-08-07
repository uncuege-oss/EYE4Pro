using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Kst.Acoustics.Core.Measurement.Data;

namespace Kst.Acoustics.App.ViewModels;

public partial class MeasurementItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _label;

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private string _color = "#4EA8DE";

    public MeasurementData Data { get; }

    public MeasurementItemViewModel(MeasurementData data, string color)
    {
        Data = data;
        Label = data.Label;
        Color = color;
    }
}
