namespace Kst.Acoustics.Core.Architecture.Materials;

/// <summary>
/// Katmanli yapi elemani tipinin malzeme katalogundan hesaplanan hafif teknik ozeti.
/// </summary>
public class YapiElemaniPerformansOzeti
{
    public string YapiElemaniTipiId { get; set; } = "";
    public double ToplamKalinlikM { get; set; }
    public double TahminiAgirlikKgM2 { get; set; }
    public int YanginDayanimDakika { get; set; }
    public string YanginTepkiSinifi { get; set; } = "";
    public double AkustikRw { get; set; }
    public double YutuculukAw { get; set; }
    public double UDegeriWm2K { get; set; }
    public int KatmanSayisi { get; set; }
    public int MalzemeSayisi { get; set; }

    public bool YanginBilgisiVar => YanginDayanimDakika > 0 || !string.IsNullOrWhiteSpace(YanginTepkiSinifi);
    public bool AkustikBilgisiVar => AkustikRw > 0 || YutuculukAw > 0;
    public bool IsiBilgisiVar => UDegeriWm2K > 0;
    public bool AgirlikBilgisiVar => TahminiAgirlikKgM2 > 0;
}
