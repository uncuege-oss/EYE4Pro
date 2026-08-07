using System;

namespace Kst.Acoustics.Core.Salon.Models;

/// Kademeli (raked) seyirci alanı. Dikdörtgen footprint; derinlik (Y) boyunca N kademeye
/// bölünür, her kademe bir öncekinden <see cref="KademeYuksekligiM"/> yükselir.
/// Önde (sahneye yakın) alçak, arkada yüksek. <see cref="TersYon"/> yükselme yönünü çevirir.
public class SeyirciSekciyonu : ISeyirciSekciyonu
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Ad { get; set; } = "Seyirci";

    /// Genişlik (X, m) — bakış yönüne dik.
    public double GenislikM { get; set; } = 16.0;
    /// Derinlik (Y, m) — ön (sahne) → arka.
    public double DerinlikM { get; set; } = 12.0;

    /// Kademe (parça) sayısı.
    public int KademeSayisi { get; set; } = 6;
    /// Kademe başına yükselme (m). Eğim = KademeYuksekligiM / (DerinlikM / KademeSayisi).
    public double KademeYuksekligiM { get; set; } = 0.45;

    /// Ön kenar (en alçak kademe) kotu (m).
    public double TabanKotuM { get; set; } = 0.0;

    /// Footprint merkezi (m).
    public double MerkezX { get; set; }
    public double MerkezY { get; set; }

    /// false = +Y yönünde yükselir (arka = +Y); true = −Y yönünde yükselir.
    public bool TersYon { get; set; }

    public override string ToString()
        => $"{Ad} ({GenislikM:F0}×{DerinlikM:F0} m, {KademeSayisi} kademe)";
}
