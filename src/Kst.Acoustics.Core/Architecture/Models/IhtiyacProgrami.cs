using System.Collections.Generic;

namespace Kst.Acoustics.Core.Architecture.Models;

/// <summary>
/// Bina islevi, kapasite ve hedef alanlardan turetilen mimari ihtiyac programi.
/// </summary>
public class IhtiyacProgrami
{
    public BinaIslevi BinaIslevi { get; set; } = BinaIslevi.Tanimsiz;
    public string ProjeTipi { get; set; } = "";
    public int KullaniciKapasitesi { get; set; }
    public int KatSayisi { get; set; } = 1;
    public double HedefToplamAlanM2 { get; set; }
    public double VarsayilanKatYuksekligiM { get; set; } = 3.0;
    public double SirkulasyonOrani { get; set; } = 0.18;
    public double TeknikAlanOrani { get; set; } = 0.05;
    public string Notlar { get; set; } = "";

    public List<MahalGereksinimi> Mahaller { get; set; } = new();

    public double HedefNetAlanM2
    {
        get
        {
            double toplam = 0;
            foreach (var mahal in Mahaller)
            {
                toplam += mahal.ToplamHedefAlanM2;
            }

            return toplam;
        }
    }

    public double OnerilenSirkulasyonAlanM2 => HedefNetAlanM2 * SirkulasyonOrani;
    public double OnerilenTeknikAlanM2 => HedefNetAlanM2 * TeknikAlanOrani;
    public double OnerilenBrutAlanM2 => HedefNetAlanM2 + OnerilenSirkulasyonAlanM2 + OnerilenTeknikAlanM2;
}
