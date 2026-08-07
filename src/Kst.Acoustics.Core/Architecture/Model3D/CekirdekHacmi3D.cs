using System;
using Kst.Acoustics.Core.Architecture.Circulation;

namespace Kst.Acoustics.Core.Architecture.Model3D;

/// <summary>
/// M33 cekirdek elemaninin kat bazli kaba 3D hacmidir.
/// </summary>
public class CekirdekHacmi3D
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string KaynakCekirdekId { get; set; } = "";
    public string Ad { get; set; } = "Cekirdek Hacmi";
    public CekirdekTipi Tip { get; set; } = CekirdekTipi.Tanimsiz;
    public int KatNo { get; set; } = 1;
    public int BaslangicKatNo { get; set; } = 1;
    public int BitisKatNo { get; set; } = 1;
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double GenislikM { get; set; }
    public double DerinlikM { get; set; }
    public double YukseklikM { get; set; }
    public string AksKodu { get; set; } = "";
    public string Notlar { get; set; } = "";

    public double AlanM2 => Math.Max(0, GenislikM) * Math.Max(0, DerinlikM);
    public double HacimM3 => AlanM2 * Math.Max(0, YukseklikM);
    public double UstKotuM => Z + Math.Max(0, YukseklikM);
    public double MerkezX => X + GenislikM / 2.0;
    public double MerkezY => Y + DerinlikM / 2.0;
    public double MerkezZ => Z + YukseklikM / 2.0;
    public bool YerlesmisMi => GenislikM > 0 && DerinlikM > 0 && YukseklikM > 0;
}
