namespace Kst.Acoustics.Core.NoiseBarrier;

public class BarrierModel
{
    public string Name { get; set; } = "Bariyer";
    public double Length { get; set; } = 30.0;
    public double Height { get; set; } = 4.0;
    public double DistanceFromBuilding { get; set; } = 5.0;
    public double CenterX { get; set; } = 0;
    public double BaseZ { get; set; } = 0;

    public string MaterialId { get; set; } = string.Empty;
    public bool HasAbsorptiveSourceSide { get; set; } = true;
    public bool HasAbsorptiveReceiverSide { get; set; } = false;
    public bool IsEnabled { get; set; } = true;

    public double PositionY => -DistanceFromBuilding;
}
