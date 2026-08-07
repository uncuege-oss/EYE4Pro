using System.Numerics;

namespace Kst.Acoustics.Core.PanelDesign;

/// <summary>
/// Represents a 2x2 complex transfer matrix used in the Transfer Matrix Method (TMM)
/// for calculating acoustic properties of layered structures.
/// </summary>
public readonly struct TransferMatrix
{
    public Complex T11 { get; }
    public Complex T12 { get; }
    public Complex T21 { get; }
    public Complex T22 { get; }

    public TransferMatrix(Complex t11, Complex t12, Complex t21, Complex t22)
    {
        T11 = t11;
        T12 = t12;
        T21 = t21;
        T22 = t22;
    }

    /// <summary>
    /// Multiplies two transfer matrices: M = A * B
    /// </summary>
    public static TransferMatrix operator *(TransferMatrix a, TransferMatrix b)
    {
        var t11 = a.T11 * b.T11 + a.T12 * b.T21;
        var t12 = a.T11 * b.T12 + a.T12 * b.T22;
        var t21 = a.T21 * b.T11 + a.T22 * b.T21;
        var t22 = a.T21 * b.T12 + a.T22 * b.T22;

        return new TransferMatrix(t11, t12, t21, t22);
    }
}
