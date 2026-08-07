namespace Kst.Acoustics.Core.NoiseBarrier;

public class BuildingModel
{
    public double Width { get; set; } = 20.0;
    public double Depth { get; set; } = 10.0;
    public double Height { get; set; } = 12.0;
    public double FloorHeight { get; set; } = 3.0;
    
    // Grid settings for receivers
    public int FacadeGridColumns { get; set; } = 10;
    public int FacadeGridRows { get; set; } = 4;
    public double FacadeReceiverOffset { get; set; } = 0.05; // Offset from facade for calculation

    // Position of the front-center of the building (typically 0,0,0)
    public double CenterX { get; set; } = 0;
    public double CenterY { get; set; } = 0;
    public double BaseZ { get; set; } = 0;
}
