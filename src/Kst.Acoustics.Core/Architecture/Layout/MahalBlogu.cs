using System;
using Kst.Acoustics.Core.Architecture.Models;

namespace Kst.Acoustics.Core.Architecture.Layout;

/// <summary>
/// M31 mahal gereksiniminin M32 konsept plan uzerindeki blok temsilidir.
/// </summary>
public class MahalBlogu
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string MahalGereksinimiId { get; set; } = "";
    public string Ad { get; set; } = "";
    public MahalTipi MahalTipi { get; set; } = MahalTipi.Tanimsiz;
    public KullanimSinifi KullanimSinifi { get; set; } = KullanimSinifi.Tanimsiz;
    public double HedefAlanM2 { get; set; }
    public double HedefEnM { get; set; }
    public double HedefBoyM { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double GenislikM { get; set; }
    public double DerinlikM { get; set; }
    public int KatNo { get; set; } = 1;
    public bool Kilitli { get; set; }
    public string Notlar { get; set; } = "";

    public double AlanM2 => Math.Max(0, GenislikM) * Math.Max(0, DerinlikM);
    public double AlanFarkiM2 => AlanM2 - HedefAlanM2;
    public double AlanFarkOrani => HedefAlanM2 <= 0 ? 0 : AlanFarkiM2 / HedefAlanM2;
    public double MerkezX => X + GenislikM / 2.0;
    public double MerkezY => Y + DerinlikM / 2.0;
    public bool YerlesmisMi => GenislikM > 0 && DerinlikM > 0;

    public static MahalBlogu Gereksinimden(MahalGereksinimi gereksinim, int katNo = 1)
    {
        ArgumentNullException.ThrowIfNull(gereksinim);

        var hedefAlan = Math.Max(0, gereksinim.ToplamHedefAlanM2);
        var kenar = hedefAlan > 0 ? Math.Sqrt(hedefAlan) : 0;

        return new MahalBlogu
        {
            MahalGereksinimiId = gereksinim.Id,
            Ad = gereksinim.Ad,
            MahalTipi = gereksinim.MahalTipi,
            KullanimSinifi = gereksinim.KullanimSinifi,
            HedefAlanM2 = hedefAlan,
            HedefEnM = kenar,
            HedefBoyM = kenar,
            GenislikM = kenar,
            DerinlikM = kenar,
            KatNo = Math.Max(1, katNo),
            Kilitli = false,
            Notlar = gereksinim.Notlar
        };
    }
}
