using System;

namespace Kst.Acoustics.Core.Architecture.Model3D;

public enum Aciklik3DTipi
{
    Tanimsiz,
    Kapi,
    Pencere,
    Gecis,
    SaftBosluk
}

/// <summary>
/// Duvar uzerindeki kapi, pencere veya gecis boslugu placeholder'i.
/// </summary>
public class Aciklik3D
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string KaynakMahalBloguId { get; set; } = "";
    public string DuvarId { get; set; } = "";
    public Aciklik3DTipi Tip { get; set; } = Aciklik3DTipi.Tanimsiz;
    public int KatNo { get; set; } = 1;
    public double MerkezX { get; set; }
    public double MerkezY { get; set; }
    public double AltKotuM { get; set; }
    public double GenislikM { get; set; }
    public double YukseklikM { get; set; }
    public string Notlar { get; set; } = "";

    public double AlanM2 => Math.Max(0, GenislikM) * Math.Max(0, YukseklikM);
    public double UstKotuM => AltKotuM + Math.Max(0, YukseklikM);
    public bool BoyutluMu => GenislikM > 0 && YukseklikM > 0;
}
