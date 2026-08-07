using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Kst.Acoustics.Core.NoiseBarrier;

public enum SourceType
{
    Point,
    Line,
    Traffic
}

public enum TrafficEmissionModel
{
    EngineeringPreview,
    CnossosEuPreview,
    FhwaTnmPreview,
    NmpbRoutesPreview
}

public class NoiseSource : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get; set; } = "Yeni Kaynak";

    private SourceType _type = SourceType.Point;
    public SourceType Type
    {
        get => _type;
        set
        {
            if (_type == value) return;
            _type = value;
            Raise(nameof(Type));
            Raise(nameof(IsLineLike));
            Raise(nameof(IsTraffic));
            Raise(nameof(ShowManualLw));
        }
    }

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    
    // Position
    public double X { get; set; }
    public double Y { get; set; } // Should be < -DistanceFromBuilding
    public double Z { get; set; } = 0.5; // Typical source height
    
    // Line source ends
    public double XEnd { get; set; }
    public double YEnd { get; set; }
    
    // Spectrum (Octave bands 63Hz to 8kHz)
    public Dictionary<double, double> Lw { get; set; } = new()
    {
        { 63, 100 }, { 125, 100 }, { 250, 100 }, { 500, 100 }, 
        { 1000, 100 }, { 2000, 100 }, { 4000, 100 }, { 8000, 100 }
    };
    
    public double OverallLw { get; set; } = 100;
    public double TrafficFlowVehiclesPerHour { get; set; } = 1200;
    public double AverageSpeedKmh { get; set; } = 70;
    public double HeavyVehiclePercent { get; set; } = 10;
    public TrafficEmissionModel TrafficModel { get; set; } = TrafficEmissionModel.EngineeringPreview;
    public bool IsEnabled { get; set; } = true;

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsLineLike => Type == SourceType.Line || Type == SourceType.Traffic;

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsTraffic => Type == SourceType.Traffic;

    [System.Text.Json.Serialization.JsonIgnore]
    public bool ShowManualLw => Type != SourceType.Traffic;
}

public class ReceiverPoint
{
    public int Id { get; set; }
    public double X { get; set; }
    public double Y { get; set; } // Usually very close to facade (e.g. -0.05)
    public double Z { get; set; }
    
    public int FloorIndex { get; set; }
    public bool IsEnabled { get; set; } = true;
    
    // Results
    public Dictionary<double, double> LevelNoBarrier { get; set; } = new();
    public Dictionary<double, double> LevelWithBarrier { get; set; } = new();
    public double TotalDbANoBarrier { get; set; }
    public double TotalDbAWithBarrier { get; set; }
    public double InsertionLoss { get; set; }
}
