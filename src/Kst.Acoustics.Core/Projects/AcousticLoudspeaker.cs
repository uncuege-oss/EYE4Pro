using System;
using System.Collections.Generic;

namespace Kst.Acoustics.Core.Projects;

/// <summary>
/// Yönelim balonuna (Directivity Balloon) sahip bir hoparlör modelini temsil eder.
/// </summary>
public sealed record AcousticLoudspeaker(
    string Id,
    string Manufacturer,
    string ModelName,
    double[] Sensitivity, // [63, 125, 250, 500, 1k, 2k, 4k, 8k] Hz frekans bandı duyarlılıkları (dB)
    DirectivityBalloon[] Balloons // Frekans bandı sırasına göre yönelim balonları
)
{
    public string DisplayName => $"{Manufacturer} {ModelName}".Trim();
    public override string ToString() => DisplayName;
}

/// <summary>
/// Tek bir frekans bandı için hoparlörün yönelim zayıflatma (attenuation) haritası.
/// </summary>
public sealed class DirectivityBalloon
{
    /// <summary>
    /// Açısal çözünürlük (Derece cinsinden, genelde 5 veya 10).
    /// </summary>
    public int Resolution { get; }

    /// <summary>
    /// Zayıflatma verisi (dB cinsinden). Pozitif veya negatif olabilir.
    /// Satırlar (1. boyut): Theta (Dikey/Polar açı) [0..180]
    /// Sütunlar (2. boyut): Phi (Yatay/Azimuth açı) [0..355]
    /// </summary>
    public double[,] AttenuationData { get; }

    public DirectivityBalloon(int resolution, double[,] attenuationData)
    {
        Resolution = resolution;
        AttenuationData = attenuationData;
    }

    /// <summary>
    /// Belirtilen Theta (0-180, 0=Ön/Yukarı, 180=Arka/Aşağı) ve 
    /// Phi (0-360) açılarındaki dB zayıflamasını döndürür.
    /// Bilinear veya En Yakın Komşu (Nearest Neighbor) interpolasyonu yapar.
    /// </summary>
    public double GetAttenuation(double theta, double phi)
    {
        // En yakın komşu (Nearest Neighbor) interpolasyonu:
        int t = (int)Math.Round(theta / Resolution);
        int p = (int)Math.Round(phi / Resolution);

        int maxT = 180 / Resolution;
        int maxP = 360 / Resolution;

        t = Math.Clamp(t, 0, maxT);
        
        p = p % maxP;
        if (p < 0) p += maxP;

        // Phi 360 olduğunda 0'a sarar (maxP indexi yoktur, 0'dan maxP-1'e kadardır)
        if (p == maxP) p = 0;

        // Eğer data dizisi boyutlarını aşarsa güvenli şekilde 0 döner (idealde aşmamalı)
        if (t >= AttenuationData.GetLength(0)) t = AttenuationData.GetLength(0) - 1;
        if (p >= AttenuationData.GetLength(1)) p = AttenuationData.GetLength(1) - 1;

        return AttenuationData[t, p];
    }
}
