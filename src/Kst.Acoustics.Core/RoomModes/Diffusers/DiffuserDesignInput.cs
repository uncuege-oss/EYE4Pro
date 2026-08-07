namespace Kst.Acoustics.Core.RoomModes.Diffusers;

public class DiffuserDesignInput
{
    public DiffuserType Type { get; set; } = DiffuserType.Qrd1D;

    public double TargetFrequencyHz { get; set; } = 250;

    public double? LowerFrequencyHz { get; set; }

    public double? UpperFrequencyHz { get; set; }

    public int PrimeOrder { get; set; } = 7;

    public double WellWidthMm { get; set; } = 70;

    public double PanelHeightMm { get; set; } = 600;

    public double MaxDepthLimitMm { get; set; } = 250;

    public double SoundSpeed { get; set; } = 343.2;

    public double? SchroederFrequencyHz { get; set; }
}
