using System.Linq;

namespace Kst.Acoustics.Core.Architecture.Models;

/// <summary>
/// Mahal tipi/kullanim sinifi uzerinden modulleri ortak kullanan siniflandirma yardimcilari.
/// Moduller (M33 sirkulasyon, M36 finisaj vb.) ayni tanimi paylasmali; tanim burada tek noktada tutulur.
/// </summary>
public static class MahalTipiYardimcilari
{
    private static readonly MahalTipi[] IslakHacimMahalTipleri =
    [
        MahalTipi.Wc,
        MahalTipi.Banyo,
        MahalTipi.Mutfak,
        MahalTipi.Restoran,
        MahalTipi.Kafe,
        MahalTipi.Laboratuvar
    ];

    /// <summary>
    /// Mahal su yalitimi/kaymazlik gerektiren bir islak hacim mi?
    /// Kullanim sinifi IslakHacim ise veya mahal tipi islak hacim listesindeyse dogru doner.
    /// </summary>
    public static bool IslakHacimMi(MahalTipi mahalTipi, KullanimSinifi kullanimSinifi)
        => kullanimSinifi == KullanimSinifi.IslakHacim || IslakHacimMahalTipleri.Contains(mahalTipi);
}
