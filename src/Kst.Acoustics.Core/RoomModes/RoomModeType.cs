namespace Kst.Acoustics.Core.RoomModes;

/// <summary>
/// Oda modu tipini temsil eder.
/// </summary>
public enum RoomModeType
{
    /// <summary>
    /// Eksenel mod (Sadece bir yönde nx, ny veya nz > 0). En güçlü modlar.
    /// </summary>
    Axial,

    /// <summary>
    /// Teğetsel mod (Tam iki yönde nx, ny, nz > 0). Orta güçte modlar.
    /// </summary>
    Tangential,

    /// <summary>
    /// Eğik mod (Üç yönde de nx, ny, nz > 0). En zayıf modlar.
    /// </summary>
    Oblique
}
