using System;
using System.Collections.Generic;

namespace Kst.Acoustics.Core.Architecture.Model3D;

/// <summary>
/// 3D modeldeki kaba duvar segmenti.
/// </summary>
public class Duvar3D
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string KaynakMahalBloguId { get; set; } = "";
    public int KatNo { get; set; } = 1;
    public double BaslangicX { get; set; }
    public double BaslangicY { get; set; }
    public double BitisX { get; set; }
    public double BitisY { get; set; }
    public double TabanKotuM { get; set; }
    public double YukseklikM { get; set; } = 3.0;
    public double KalinlikM { get; set; } = 0.2;
    public string DuvarTipiId { get; set; } = "";
    public List<string> AciklikIdleri { get; set; } = new();
    public string Notlar { get; set; } = "";

    public double UzunlukM => Math.Sqrt(Math.Pow(BitisX - BaslangicX, 2) + Math.Pow(BitisY - BaslangicY, 2));
    public double AlanM2 => UzunlukM * Math.Max(0, YukseklikM);
    public double HacimM3 => AlanM2 * Math.Max(0, KalinlikM);
    public double UstKotuM => TabanKotuM + Math.Max(0, YukseklikM);
    public double MerkezX => (BaslangicX + BitisX) / 2.0;
    public double MerkezY => (BaslangicY + BitisY) / 2.0;
    public double MerkezZ => TabanKotuM + YukseklikM / 2.0;
    public bool YerlesmisMi => UzunlukM > 0 && YukseklikM > 0 && KalinlikM > 0;
}
