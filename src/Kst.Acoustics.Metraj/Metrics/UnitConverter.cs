using Kst.Acoustics.Metraj.Models;

namespace Kst.Acoustics.Metraj.Metrics;

public enum MetrajUnit
{
    Millimeters,
    Centimeters,
    Meters
}

public class UnitConverter
{
    public static double GetScaleFactor(MetrajSourceUnit from, MetrajUnit to)
    {
        double metersPerFromUnit = from switch
        {
            MetrajSourceUnit.Inches => 0.0254,
            MetrajSourceUnit.Feet => 0.3048,
            MetrajSourceUnit.Miles => 1609.344,
            MetrajSourceUnit.Millimeters => 0.001,
            MetrajSourceUnit.Centimeters => 0.01,
            MetrajSourceUnit.Meters => 1.0,
            MetrajSourceUnit.Kilometers => 1000.0,
            MetrajSourceUnit.Yards => 0.9144,
            MetrajSourceUnit.Decimeters => 0.1,
            _ => 0.001 // Unitless or unknown -> Assume mm as fallback
        };

        return to switch
        {
            MetrajUnit.Millimeters => metersPerFromUnit * 1000.0,
            MetrajUnit.Centimeters => metersPerFromUnit * 100.0,
            MetrajUnit.Meters => metersPerFromUnit,
            _ => 1.0
        };
    }

    public static double Convert(double value, MetrajSourceUnit from, MetrajUnit to)
    {
        return value * GetScaleFactor(from, to);
    }
}
