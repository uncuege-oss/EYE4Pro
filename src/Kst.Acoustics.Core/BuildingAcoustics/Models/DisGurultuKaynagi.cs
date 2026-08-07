namespace Kst.Acoustics.Core.BuildingAcoustics.Models;

public enum DisGurultuTipi
{
    Trafik,
    Demiryolu,
    Endustriyel,
    Ozel
}

/// <summary>
/// Cephenin biçimi / dış gürültüye maruz kalma durumu.
/// ΔLfs (cephe form düzeltmesi) değerini belirler — TS EN 12354-3 Tablo 1.
/// ΔLfs değerleri <see cref="Calculation.DisGurultuServisi.DeltaLfs"/> içinde tutulur.
/// Not: Geriye dönük uyum için SerbestAlan/TrafikCephesi adları korunmuştur.
/// </summary>
public enum CepheMaruziyet
{
    // ISO 12354-3 Tablo 1: D_2m,nT = R' + ΔL_fs + Vnorm (ΔL_fs EKLENİR).
    /// Düz cephe, serbest alan — ΔL_fs = 0 dB.
    SerbestAlan,
    /// Yansıtıcı karşı cephe / sokak kanyonu — ΔL_fs = −3 dB (gelen seviye artar).
    TrafikCephesi,
    /// Balkon, yutucu (emici) tavan α≥0,9 — ΔL_fs ≈ +3 dB.
    BalkonYutucuTavanli,
    /// Balkon, yansıtıcı tavan α≤0,3 — ΔL_fs ≈ 0 dB.
    BalkonYansiticiTavanli,
    /// Loggia (derin/kapalı niş) — ΔL_fs ≈ +3 dB.
    Loggia,
}

public class DisGurultuKaynagi
{
    public string Ad { get; set; } = "";
    public DisGurultuTipi Tip { get; set; }
    
    // Ses Seviyeleri (dB)
    public double Lden { get; set; }
    public double Lnight { get; set; }
    
    // Oktav Bant Seviyeleri (Opsiyonel)
    public double[] OctaveLevels { get; set; } = []; // 63Hz - 8kHz
}
