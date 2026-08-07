using System;
using System.Collections.Generic;

namespace Kst.Acoustics.Core.Architecture.Circulation;

public class CekirdekGrubu
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Ad { get; set; } = "Cekirdek Grubu";
    public string AksKodu { get; set; } = "";
    public int BaslangicKatNo { get; set; } = 1;
    public int BitisKatNo { get; set; } = 1;
    public double MerkezX { get; set; }
    public double MerkezY { get; set; }
    public List<string> ElemanIdleri { get; set; } = new();
    public string Notlar { get; set; } = "";

    public int AltKatNo => Math.Min(BaslangicKatNo, BitisKatNo);
    public int UstKatNo => Math.Max(BaslangicKatNo, BitisKatNo);

    public bool KatAraligindaMi(int katNo)
        => katNo >= AltKatNo && katNo <= UstKatNo;
}
