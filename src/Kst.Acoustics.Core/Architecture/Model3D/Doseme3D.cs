using System;

namespace Kst.Acoustics.Core.Architecture.Model3D;

/// <summary>
/// Kattaki kaba doseme prizmasi.
/// </summary>
public class Doseme3D
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string KaynakKatTaslagiId { get; set; } = "";
    public int KatNo { get; set; } = 1;
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double GenislikM { get; set; }
    public double DerinlikM { get; set; }
    public double KalinlikM { get; set; } = 0.2;
    public string DosemeTipiId { get; set; } = "";
    public string Notlar { get; set; } = "";

    public double AlanM2 => Math.Max(0, GenislikM) * Math.Max(0, DerinlikM);
    public double HacimM3 => AlanM2 * Math.Max(0, KalinlikM);
    public double UstKotuM => Z + Math.Max(0, KalinlikM);
    public double MerkezX => X + GenislikM / 2.0;
    public double MerkezY => Y + DerinlikM / 2.0;
    public double MerkezZ => Z + KalinlikM / 2.0;
    public bool YerlesmisMi => GenislikM > 0 && DerinlikM > 0 && KalinlikM > 0;
}
