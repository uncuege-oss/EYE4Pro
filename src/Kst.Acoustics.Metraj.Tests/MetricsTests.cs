using Kst.Acoustics.Metraj.Metrics;
using Kst.Acoustics.Metraj.Models;
using System;
using System.Collections.Generic;

namespace Kst.Acoustics.Metraj.Tests;

public class MetricsTests
{
    [Theory]
    [InlineData(1, MetrajSourceUnit.Meters, MetrajUnit.Millimeters, 1000)]
    [InlineData(1, MetrajSourceUnit.Millimeters, MetrajUnit.Meters, 0.001)]
    [InlineData(100, MetrajSourceUnit.Centimeters, MetrajUnit.Meters, 1.0)]
    [InlineData(1, MetrajSourceUnit.Inches, MetrajUnit.Millimeters, 25.4)]
    [InlineData(1, MetrajSourceUnit.Feet, MetrajUnit.Millimeters, 304.8)]
    public void UnitConverter_Convert_ShouldReturnCorrectValues(double value, MetrajSourceUnit from, MetrajUnit to, double expected)
    {
        double result = UnitConverter.Convert(value, from, to);
        Assert.Equal(expected, result, 5);
    }

    [Fact]
    public void LengthCalculator_Line_ShouldBeCorrect()
    {
        var calc = new LengthCalculator();
        var line = new MetrajLine("0", new MetrajColor(256), new MetrajPoint(0,0,0), new MetrajPoint(10,0,0));
        Assert.Equal(10, calc.CalculateLength(line));
    }

    [Fact]
    public void LengthCalculator_Circle_ShouldBeCorrect()
    {
        var calc = new LengthCalculator();
        var circle = new MetrajCircle("0", new MetrajColor(256), new MetrajPoint(0,0,0), 10);
        Assert.Equal(2 * Math.PI * 10, calc.CalculateLength(circle), 5);
    }

    [Fact]
    public void LengthCalculator_Arc_ShouldBeCorrect()
    {
        var calc = new LengthCalculator();
        // 90 degree arc, radius 10
        var arc = new MetrajArc("0", new MetrajColor(256), new MetrajPoint(0,0,0), 10, 0, 90);
        Assert.Equal(0.5 * Math.PI * 10, calc.CalculateLength(arc), 5);
    }

    [Fact]
    public void LengthCalculator_Polyline_ShouldBeCorrect()
    {
        var calc = new LengthCalculator();
        var poly = new MetrajPolyline("0", new MetrajColor(256), new List<MetrajPoint> {
            new MetrajPoint(0,0,0), new MetrajPoint(10,0,0), new MetrajPoint(10,10,0)
        }, false);
        Assert.Equal(20, calc.CalculateLength(poly));
    }

    [Fact]
    public void LengthCalculator_Ellipse_ShouldBeCorrect()
    {
        var calc = new LengthCalculator();
        // Circle-like ellipse (Major=10, Minor=10)
        var ell = new MetrajEllipse("0", new MetrajColor(256), new MetrajPoint(0,0,0), 10, 0, 0, 1.0, 0, 360);
        Assert.Equal(2 * Math.PI * 10, calc.CalculateLength(ell), 1);
    }
}
