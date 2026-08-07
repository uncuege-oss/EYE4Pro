using System;
using System.Collections.Generic;

namespace Kst.Acoustics.Core.Architecture.Circulation;

public class SaftTaslagi
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Ad { get; set; } = "Saft";
    public SaftTesisatTuru TesisatTuru { get; set; } = SaftTesisatTuru.Tanimsiz;
    public int KatNo { get; set; } = 1;
    public int BaslangicKatNo { get; set; } = 1;
    public int BitisKatNo { get; set; } = 1;
    public double X { get; set; }
    public double Y { get; set; }
    public double GenislikM { get; set; }
    public double DerinlikM { get; set; }
    public string AksKodu { get; set; } = "";
    public List<string> BagliMahalIdleri { get; set; } = new();
    public bool Kilitli { get; set; }
    public string Notlar { get; set; } = "";

    public int AltKatNo => Math.Min(BaslangicKatNo, BitisKatNo);
    public int UstKatNo => Math.Max(BaslangicKatNo, BitisKatNo);
    public double AlanM2 => Math.Max(0, GenislikM) * Math.Max(0, DerinlikM);
    public double MerkezX => X + GenislikM / 2.0;
    public double MerkezY => Y + DerinlikM / 2.0;
    public bool YerlesmisMi => GenislikM > 0 && DerinlikM > 0;

    public bool KatAraligindaMi(int katNo)
        => katNo >= AltKatNo && katNo <= UstKatNo;
}
