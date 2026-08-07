using System;

namespace Kst.Acoustics.Core.Architecture.Materials;

/// <summary>
/// Malzemenin metraj ve agirlik hesabinda kullanilacak hafif teknik verisi.
/// </summary>
public class MetrajBilgisi
{
    public MetrajBirimi VarsayilanBirim { get; set; } = MetrajBirimi.Metrekare;
    public double BirimAgirlikKg { get; set; }
    public double YogunlukKgM3 { get; set; }
    public string BirimMaliyetNotu { get; set; } = "";
    public string MetrajNotu { get; set; } = "";

    public bool MetrajlanabilirMi => VarsayilanBirim != MetrajBirimi.Tanimsiz;
    public double KullanilanBirimAgirlikKg => Math.Max(0, BirimAgirlikKg);
    public double KullanilanYogunlukKgM3 => Math.Max(0, YogunlukKgM3);
}
