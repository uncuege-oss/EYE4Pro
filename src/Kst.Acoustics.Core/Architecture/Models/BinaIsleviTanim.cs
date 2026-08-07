namespace Kst.Acoustics.Core.Architecture.Models;

/// <summary>
/// Bina islevinin UI ve ihtiyac programi uretimi icin okunabilir tanimi.
/// </summary>
public class BinaIsleviTanim
{
    public BinaIslevi Islev { get; set; } = BinaIslevi.Tanimsiz;
    public string Ad { get; set; } = "";
    public string Aciklama { get; set; } = "";
    public double VarsayilanSirkulasyonOrani { get; set; } = 0.18;
    public double VarsayilanTeknikAlanOrani { get; set; } = 0.05;
    public double VarsayilanKatYuksekligiM { get; set; } = 3.0;
    public string KapasiteEtiketi { get; set; } = "Kişi sayısı";
}
