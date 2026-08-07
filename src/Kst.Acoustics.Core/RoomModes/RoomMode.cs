namespace Kst.Acoustics.Core.RoomModes;

/// <summary>
/// Tek bir oda modunun verilerini tutar.
/// </summary>
public record RoomMode(
    double Frequency,
    int Nx,
    int Ny,
    int Nz,
    RoomModeType Type
)
{
    /// <summary>
    /// Mod indekslerini (nx, ny, nz) formatlı string olarak döndürür.
    /// </summary>
    public string Indices => $"({Nx}, {Ny}, {Nz})";
}
