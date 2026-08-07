namespace Kst.Acoustics.Core.Salon.Models;

/// Hazır salon şablonu tipi (plan biçimi).
public enum SalonTipi { Dikdortgen, AtNali, Yelpaze }

/// Hazır salon şablonu parametreleri. Kullanıcı yalnız ölçü/tip girer; servis tam salonu üretir.
public class SalonSablonu
{
    public SalonTipi SalonTip { get; set; } = SalonTipi.Dikdortgen;
    public double SalonGenislikM { get; set; } = 24.0;
    public double SalonDerinlikM { get; set; } = 30.0;
    public double SalonYuksekligiM { get; set; } = 10.0;

    public SahneTipi SahneTip { get; set; } = SahneTipi.Dikdortgen;
    public SahneKonum SahneKonum { get; set; } = SahneKonum.SalonIci;
    public double SahneGenislikM { get; set; } = 14.0;
    public double SahneDerinlikM { get; set; } = 8.0;
    public double SahneYuksekligiM { get; set; } = 1.0;

    public int SeyirciKademe { get; set; } = 8;
    public double SeyirciKademeYukM { get; set; } = 0.5;
}
