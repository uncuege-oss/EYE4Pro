namespace Kst.Acoustics.Core.RoomModes.Diffusers;

public record SkylineCell(
    int Row,
    int Column,
    int Residue,
    double SizeMm,
    double DepthMm
);
