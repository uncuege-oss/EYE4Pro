using System;
using System.Collections.Generic;

namespace Kst.Acoustics.Core.Architecture.Layout;

/// <summary>
/// Kat icindeki kaba dolasim aksini temsil eden taslak koridor verisi.
/// </summary>
public class KoridorTaslagi
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Ad { get; set; } = "Koridor";
    public double BaslangicX { get; set; }
    public double BaslangicY { get; set; }
    public double BitisX { get; set; }
    public double BitisY { get; set; }
    public double GenislikM { get; set; } = 1.5;
    public List<string> BagliBlokIdleri { get; set; } = new();

    public double UzunlukM
    {
        get
        {
            var dx = BitisX - BaslangicX;
            var dy = BitisY - BaslangicY;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }

    public double AlanM2 => UzunlukM * Math.Max(0, GenislikM);
    public double MerkezX => (BaslangicX + BitisX) / 2.0;
    public double MerkezY => (BaslangicY + BitisY) / 2.0;
    public bool YatayMi => UzunlukM > 0 && Math.Abs(BaslangicY - BitisY) <= 1e-9;
    public bool DikeyMi => UzunlukM > 0 && Math.Abs(BaslangicX - BitisX) <= 1e-9;
}
