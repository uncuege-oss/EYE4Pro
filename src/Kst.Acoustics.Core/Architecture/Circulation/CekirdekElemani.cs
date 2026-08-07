using System;
using System.Collections.Generic;

namespace Kst.Acoustics.Core.Architecture.Circulation;

public class CekirdekElemani
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Ad { get; set; } = "Cekirdek Elemani";
    public CekirdekTipi Tip { get; set; } = CekirdekTipi.Tanimsiz;
    public int KatNo { get; set; } = 1;
    public int BaslangicKatNo { get; set; } = 1;
    public int BitisKatNo { get; set; } = 1;
    public double X { get; set; }
    public double Y { get; set; }
    public double GenislikM { get; set; }
    public double DerinlikM { get; set; }
    public double NetGenislikM { get; set; }
    public int KapasiteKisi { get; set; }
    public string AksKodu { get; set; } = "";
    public string CekirdekGrubuId { get; set; } = "";
    public List<string> BagliKoridorIdleri { get; set; } = new();
    public List<string> BagliMahalIdleri { get; set; } = new();
    public bool Kilitli { get; set; }
    public string Notlar { get; set; } = "";

    public int AltKatNo => Math.Min(BaslangicKatNo, BitisKatNo);
    public int UstKatNo => Math.Max(BaslangicKatNo, BitisKatNo);
    public double AlanM2 => Math.Max(0, GenislikM) * Math.Max(0, DerinlikM);
    public double MerkezX => X + GenislikM / 2.0;
    public double MerkezY => Y + DerinlikM / 2.0;
    public bool YerlesmisMi => GenislikM > 0 && DerinlikM > 0;
    public bool DuseySurekliMi => AltKatNo != UstKatNo;

    public bool KatAraligindaMi(int katNo)
        => katNo >= AltKatNo && katNo <= UstKatNo;
}
