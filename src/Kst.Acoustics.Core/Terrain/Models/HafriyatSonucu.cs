using System.Collections.Generic;

namespace Kst.Acoustics.Core.Terrain.Models;

/// Tek bir tasarım bölgesinin yarma/dolgu dökümü.
public class HafriyatBolgeSonucu
{
    public string BolgeAdi { get; set; } = "";
    public double HedefKotM { get; set; }
    public double YarmaHacmiM3 { get; set; }
    public double DolguHacmiM3 { get; set; }
    public double NetHacimM3 => YarmaHacmiM3 - DolguHacmiM3;
    public int HucreSayisi { get; set; }
}

/// Yarma/dolgu hesabı sonucu. Hacimler m³ cinsindendir.
/// Yarma (cut) = mevcut zemin tasarım kotunun ÜSTünde → kazılır.
/// Dolgu (fill) = mevcut zemin tasarım kotunun ALTında → doldurulur.
public class HafriyatSonucu
{
    public double YarmaHacmiM3 { get; set; }
    public double DolguHacmiM3 { get; set; }

    /// Net = Yarma − Dolgu. Pozitif: fazla kazı (depoya), negatif: dış dolgu ihtiyacı.
    public double NetHacimM3 => YarmaHacmiM3 - DolguHacmiM3;

    public double HucreAlaniM2 { get; set; }
    public int YarmaHucreSayisi { get; set; }
    public int DolguHucreSayisi { get; set; }
    public int DegerlendirilenHucreSayisi { get; set; }

    /// Tasarım bölgesi içinde olup mevcut yüzey kapsamı dışında kalan (kot bulunamayan)
    /// hücre sayısı — uyarı amaçlı.
    public int KapsamDisiHucreSayisi { get; set; }

    /// Tasarım bölgesi başına yarma/dolgu dökümü.
    public List<HafriyatBolgeSonucu> BolgeSonuclari { get; set; } = new();
}
