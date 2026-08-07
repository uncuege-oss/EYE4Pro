namespace Kst.Acoustics.Core.PanelDesign;

/// <summary>
/// Represents an abstract layer in a multi-layered acoustic panel structure.
/// </summary>
public abstract class AcousticLayer
{
    /// <summary>
    /// The thickness of the layer in meters.
    /// </summary>
    public double Thickness { get; set; }

    /// <summary>
    /// Calculates and returns the transfer matrix of this layer for a given frequency.
    /// </summary>
    /// <param name="frequency">The acoustic frequency in Hertz.</param>
    /// <returns>A 2x2 complex transfer matrix.</returns>
    public abstract TransferMatrix GetTransferMatrix(double frequency);
}
