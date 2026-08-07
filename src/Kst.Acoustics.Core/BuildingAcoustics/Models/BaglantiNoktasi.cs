namespace Kst.Acoustics.Core.BuildingAcoustics.Models;

/// <summary>
/// A6 — TS EN 12354-1 Annex E: Bağlantı (junction) tipi.
/// Flanking iletim yolundaki Kij titreşim azaltma indisini belirler.
/// </summary>
public enum BaglantiTipi
{
    /// Rijit beton-beton veya tuğla-tuğla bağlantı: Kij ≈ +5 dB
    Rijit,
    /// Yarı-elastik (hafif bölme - ağır duvar): Kij ≈ +10 dB
    YariElastik,
    /// Elastik (neopren, kesme yünü bantlı): Kij ≈ +15 dB
    Elastik,
    /// Tamamen kesintili (floating slab, ayrık duvar): pratik üst sınır ≈ +20 dB
    Kesintili,
}

public static class BaglantiKij
{
    /// TS EN 12354-1 Annex E Tablo E.1 — bağlantı tipine göre titreşim azaltma indisi (dB)
    public static double GetKij(BaglantiTipi tip) => tip switch
    {
        BaglantiTipi.Rijit       =>  5.0,
        BaglantiTipi.YariElastik => 10.0,
        BaglantiTipi.Elastik     => 15.0,
        BaglantiTipi.Kesintili   => 20.0,
        _                        =>  5.0,
    };
}
