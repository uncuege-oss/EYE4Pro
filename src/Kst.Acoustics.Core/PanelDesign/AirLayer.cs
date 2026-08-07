using System;
using System.Numerics;

namespace Kst.Acoustics.Core.PanelDesign;

/// <summary>
/// Represents an air gap layer.
/// </summary>
public class AirLayer : AcousticLayer
{
    public override TransferMatrix GetTransferMatrix(double frequency)
    {
        if (frequency <= 0 || Thickness <= 0)
        {
            return new TransferMatrix(1, 0, 0, 1);
        }

        double omega = 2 * Math.PI * frequency;
        double rho0 = 1.21;
        double c0 = 343.0;
        double z0 = rho0 * c0;

        double k = omega / c0;
        double kd = k * Thickness;

        Complex coskd = Math.Cos(kd);
        Complex sinkd = Math.Sin(kd);
        Complex j = Complex.ImaginaryOne;

        return new TransferMatrix(
            coskd,
            j * z0 * sinkd,
            j * sinkd / z0,
            coskd
        );
    }
}
