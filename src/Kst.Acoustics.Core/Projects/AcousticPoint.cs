using Kst.Acoustics.Core.Geometry;

namespace Kst.Acoustics.Core.Projects;

public enum AcousticPointType { Source, Receiver }

/// <summary>Kaynak yönelim modeli için standart örüntüler.</summary>
public enum DirectivityPattern
{
    Omnidirectional,  // Her yöne eşit
    Cardioid,         // Kardiyoid yönlülük yanıtı: R(θ) = (1+cosθ)/2
    SuperCardioid,    // Süper kardiyoid yönlülük yanıtı: R(θ) = 0.366 + 0.634·cosθ
    Figure8           // Çift kutuplu yönlülük yanıtı: R(θ) = |cosθ|
}

/// <summary>
/// Bir ses kaynağı (Source) veya alıcı (Receiver) noktasını temsil eder.
/// Kaynaklar için yönelim (Directivity) ve yön açıları belirlenebilir.
/// </summary>
public sealed record AcousticPoint(
    int                Id,
    string             Label,
    AcousticPointType  Type,
    Point3D            Position,
    DirectivityPattern Directivity            = DirectivityPattern.Omnidirectional,
    double             DirectivityAzimuthDeg  = 0.0,    // yatay yön açısı (Yaw / 0=+X ekseni)
    double             DirectivityElevationDeg = 0.0,   // dikey yükselim açısı (Pitch)
    double             DirectivityRollDeg     = 0.0,    // Kendi ekseninde dönüş (Roll)
    string?            LoudspeakerId          = null);  // CLF/CF1 tabanlı hoparlör ID'si (null ise standart Pattern kullanılır)
