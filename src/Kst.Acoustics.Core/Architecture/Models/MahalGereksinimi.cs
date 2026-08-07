using System;

namespace Kst.Acoustics.Core.Architecture.Models;

/// <summary>
/// Ihtiyac programindaki tek mahal veya mahal grubu gereksinimi.
/// </summary>
public class MahalGereksinimi
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public MahalTipi MahalTipi { get; set; } = MahalTipi.Tanimsiz;
    public KullanimSinifi KullanimSinifi { get; set; } = KullanimSinifi.Tanimsiz;
    public string Ad { get; set; } = "";
    public int Adet { get; set; } = 1;
    public int KisiKapasitesi { get; set; }
    public double MinimumAlanM2 { get; set; }
    public double IdealAlanM2 { get; set; }
    public double HedefAlanM2 { get; set; }
    public double MinimumNetYukseklikM { get; set; } = 2.6;
    public double HedefNetYukseklikM { get; set; } = 3.0;
    public bool Zorunlu { get; set; } = true;
    public string Notlar { get; set; } = "";

    public double ToplamHedefAlanM2 => Adet * HedefAlanM2;
    public double ToplamHedefHacimM3 => ToplamHedefAlanM2 * HedefNetYukseklikM;
}
