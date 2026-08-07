namespace Kst.Acoustics.Core.Terrain.Models;

/// Tekil arazi kotu (spot height). Koordinatlar metre cinsindendir.
public class AraziNoktasi : IAraziNokta
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public AraziVeriKaynagi Kaynak { get; set; } = AraziVeriKaynagi.SpotKot;

    public AraziNoktasi() { }

    public AraziNoktasi(double x, double y, double z, AraziVeriKaynagi kaynak = AraziVeriKaynagi.SpotKot)
    {
        X = x;
        Y = y;
        Z = z;
        Kaynak = kaynak;
    }
}
