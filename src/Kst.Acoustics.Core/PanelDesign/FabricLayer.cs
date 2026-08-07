using System;
using System.Numerics;

namespace Kst.Acoustics.Core.PanelDesign;

/// <summary>
/// Represents a thin acoustically transparent backing fabric as a resistive sheet.
/// </summary>
public class FabricLayer : AcousticLayer
{
    /// <summary>
    /// Specific airflow resistance in Pa.s/m (MKS Rayl).
    /// </summary>
    public double SpecificFlowResistance { get; set; } = 500.0;

    /// <summary>
    /// Surface density in kg/m². Used for the small mass reactance of thin fabrics.
    /// </summary>
    public double SurfaceDensity { get; set; } = 0.06;

    public static void ApplySoundTex65(FabricLayer layer)
    {
        layer.SpecificFlowResistance = 350.0;
        layer.SurfaceDensity = 0.04;
    }

    public static void ApplySoundTex100(FabricLayer layer)
    {
        layer.SpecificFlowResistance = 500.0;
        layer.SurfaceDensity = 0.06;
    }

    public static void ApplySoundTex130(FabricLayer layer)
    {
        layer.SpecificFlowResistance = 750.0;
        layer.SurfaceDensity = 0.08;
    }

    public override TransferMatrix GetTransferMatrix(double frequency)
    {
        if (frequency <= 0 || SpecificFlowResistance < 0)
        {
            return new TransferMatrix(1, 0, 0, 1);
        }

        double omega = 2 * Math.PI * frequency;
        double resistance = Math.Clamp(SpecificFlowResistance, 0.0, 100_000.0);
        double surfaceDensity = Math.Clamp(SurfaceDensity, 0.0, 5.0);
        Complex impedance = resistance + Complex.ImaginaryOne * omega * surfaceDensity;

        return new TransferMatrix(1, impedance, 0, 1);
    }
}
