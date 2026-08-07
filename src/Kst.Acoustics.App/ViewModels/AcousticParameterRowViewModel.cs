using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Kst.Acoustics.App.ViewModels;

public partial class AcousticParameterRowViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _parameterName;

    public double[] Values { get; }

    public string B63 => Format(Values[0]);
    public string B125 => Format(Values[1]);
    public string B250 => Format(Values[2]);
    public string B500 => Format(Values[3]);
    public string B1k => Format(Values[4]);
    public string B2k => Format(Values[5]);
    public string B4k => Format(Values[6]);
    public string B8k => Format(Values[7]);

    public AcousticParameterRowViewModel(string name, double[] values)
    {
        ParameterName = name;
        Values = values;
    }

    private string Format(double v)
    {
        if (double.IsNaN(v)) return "—";
        if (ParameterName.StartsWith("C") || ParameterName.StartsWith("D"))
            return v.ToString("F1");
        return v.ToString("F2");
    }
}
