using System;
using System.Numerics;

namespace Kst.Acoustics.Core.PanelDesign;

/// <summary>
/// Model types for porous materials.
/// </summary>
public enum PorousModel
{
    DelanyBazley,
    Miki
}

/// <summary>
/// Represents a porous acoustic material layer (e.g., mineral wool, foam).
/// </summary>
public class PorousLayer : AcousticLayer
{
    /// <summary>
    /// Flow resistivity in Pa.s/m² (Rayl/m).
    /// </summary>
    public double FlowResistivity { get; set; }

    /// <summary>
    /// The physical model to use for calculations.
    /// </summary>
    public PorousModel Model { get; set; } = PorousModel.Miki;

    public const double EmpiricalXMin = 0.01;  // ρf/σ alt sınır (Miki/DB geçerlilik)
    public const double EmpiricalXMax = 1.0;   // ρf/σ üst sınır
    private const double AirDensity = 1.21;
    private const double SpeedOfSound = 343.0;

    public bool IsFrequencyOutsideEmpiricalRange(double frequency)
    {
        if (frequency <= 0 || FlowResistivity <= 0 || Thickness <= 0)
        {
            return false;
        }

        double x = CalculateEmpiricalParameter(frequency);
        return x < EmpiricalXMin || x > EmpiricalXMax;
    }

    public override TransferMatrix GetTransferMatrix(double frequency)
    {
        if (frequency <= 0 || FlowResistivity <= 0 || Thickness <= 0)
        {
            return new TransferMatrix(1, 0, 0, 1);
        }

        double omega = 2 * Math.PI * frequency;
        double rho0 = AirDensity; // Air density
        double c0 = SpeedOfSound; // Speed of sound
        double z0 = rho0 * c0;

        Complex zc;
        Complex k;

        // X = ρ₀·f/σ (Miki/DB ampirik parametresi). Geçerlilik [0.01, 1.0].
        double f_sigma = Math.Clamp(CalculateEmpiricalParameter(frequency), EmpiricalXMin, EmpiricalXMax);

        if (Model == PorousModel.DelanyBazley)
        {
            // Delany-Bazley (Original)
            zc = z0 * new Complex(
                1 + 0.0497 * Math.Pow(f_sigma, -0.754),
                -0.0758 * Math.Pow(f_sigma, -0.732)
            );
            k = (omega / c0) * new Complex(
                1 + 0.0822 * Math.Pow(f_sigma, -0.7),
                -0.175 * Math.Pow(f_sigma, -0.59)
            );
        }
        else
        {
            // Miki Model (More stable at low frequencies than Delany-Bazley)
            // Reference: Miki, Y. (1990). "Acoustical properties of porous materials - 
            // Modifications of Delany-Bazley models", J. Acoust. Soc. Jpn. (E), 11(1), 19-24.
            zc = z0 * new Complex(
                1 + 0.070 * Math.Pow(f_sigma, -0.632),
                -0.107 * Math.Pow(f_sigma, -0.632)
            );
            k = (omega / c0) * new Complex(
                1 + 0.109 * Math.Pow(f_sigma, -0.618),
                -0.160 * Math.Pow(f_sigma, -0.618)
            );
        }

        // TMM for a fluid layer:
        // T11 = cos(k*d)
        // T12 = j * zc * sin(k*d)
        // T21 = j * sin(k*d) / zc
        // T22 = cos(k*d)

        Complex kd = k * Thickness;
        Complex coskd = Complex.Cos(kd);
        Complex sinkd = Complex.Sin(kd);
        Complex j = Complex.ImaginaryOne;

        return new TransferMatrix(
            coskd,
            j * zc * sinkd,
            j * sinkd / zc,
            coskd
        );
    }

    private double CalculateEmpiricalParameter(double frequency)
    {
        return AirDensity * frequency / FlowResistivity;
    }
}
