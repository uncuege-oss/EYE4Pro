using System;

namespace Kst.Acoustics.Core.Architecture.Materials;

/// <summary>
/// Katmanli duvar, doseme, tavan, kapi ve pencere tiplerinin tek katmanini temsil eder.
/// </summary>
public class YapiElemaniKatmani
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int SiraNo { get; set; } = 1;
    public string Ad { get; set; } = "";
    public string MalzemeId { get; set; } = "";
    public double KalinlikM { get; set; }
    public MalzemeKatmanFonksiyonu Fonksiyon { get; set; } = MalzemeKatmanFonksiyonu.Tanimsiz;
    public string Notlar { get; set; } = "";

    public double KullanilanKalinlikM => Math.Max(0, KalinlikM);
    public bool MalzemeBaglantisiVar => !string.IsNullOrWhiteSpace(MalzemeId);
    public bool KalinlikliMi => KullanilanKalinlikM > 0;
}
