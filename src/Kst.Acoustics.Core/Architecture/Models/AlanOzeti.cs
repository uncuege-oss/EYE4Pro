using System.Collections.Generic;

namespace Kst.Acoustics.Core.Architecture.Models;

/// <summary>
/// Ihtiyac programindan turetilen net/brut alan ve hacim ozeti.
/// </summary>
public class AlanOzeti
{
    public double NetAlanM2 { get; set; }
    public double SirkulasyonAlanM2 { get; set; }
    public double TeknikAlanM2 { get; set; }
    public double BrutAlanM2 { get; set; }
    public double HedefToplamAlanM2 { get; set; }
    public double HedefFarkM2 { get; set; }
    public double HedefFarkOrani { get; set; }
    public double NetHacimM3 { get; set; }
    public int MahalSayisi { get; set; }
    public int ToplamMahalAdedi { get; set; }
    public List<KullanimSinifiAlanOzeti> KullanimSinifiOzetleri { get; set; } = new();
}

/// <summary>
/// Alan ozetinin kullanim sinifi bazinda kirilimi.
/// </summary>
public class KullanimSinifiAlanOzeti
{
    public KullanimSinifi KullanimSinifi { get; set; } = KullanimSinifi.Tanimsiz;
    public int MahalSayisi { get; set; }
    public int ToplamAdet { get; set; }
    public double AlanM2 { get; set; }
    public double HacimM3 { get; set; }
    public double AlanOrani { get; set; }
}
