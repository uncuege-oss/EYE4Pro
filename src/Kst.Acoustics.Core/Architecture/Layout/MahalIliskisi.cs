using System;

namespace Kst.Acoustics.Core.Architecture.Layout;

/// <summary>
/// M32 iliski matrisi icin mahal bloklari arasindaki veya tek blok uzerindeki tasarim niyetini tasir.
/// </summary>
public class MahalIliskisi
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string KaynakMahalId { get; set; } = "";
    public string HedefMahalId { get; set; } = "";
    public IliskiTipi IliskiTipi { get; set; } = IliskiTipi.Tanimsiz;
    public IliskiOnceligi Oncelik { get; set; } = IliskiOnceligi.Normal;
    public string Aciklama { get; set; } = "";

    public int OncelikPuani => (int)Oncelik;
    public bool HedefliMi => !string.IsNullOrWhiteSpace(HedefMahalId);
}
