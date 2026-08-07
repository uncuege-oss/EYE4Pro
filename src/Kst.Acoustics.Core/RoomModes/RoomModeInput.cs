namespace Kst.Acoustics.Core.RoomModes;

/// <summary>
/// Oda modu hesaplaması için gerekli giriş parametreleri.
/// </summary>
public class RoomModeInput
{
    /// <summary>
    /// Oda uzunluğu (X ekseni) [m].
    /// </summary>
    public double LengthX { get; set; } = 5.0;

    /// <summary>
    /// Oda genişliği (Y ekseni) [m].
    /// </summary>
    public double LengthY { get; set; } = 4.0;

    /// <summary>
    /// Oda yüksekliği (Z ekseni) [m].
    /// </summary>
    public double LengthZ { get; set; } = 3.0;

    /// <summary>
    /// Hava sıcaklığı [°C].
    /// </summary>
    public double Temperature { get; set; } = 20.0;

    /// <summary>
    /// Maksimum hesaplanacak frekans [Hz].
    /// </summary>
    public double MaxFrequency { get; set; } = 500.0;

    /// <summary>
    /// [Opsiyonel] RT60 süresi [s]. Schroeder frekansı için kullanılır.
    /// </summary>
    public double? RT60 { get; set; }

    /// <summary>
    /// Oda hacmi [m³].
    /// </summary>
    public double Volume => LengthX * LengthY * LengthZ;
}
