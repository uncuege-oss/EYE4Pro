namespace Kst.Acoustics.Core.Terrain.Models;

public sealed class AraziKaynakBilgisi
{
    public string DosyaYolu { get; set; } = "";
    public string Birim { get; set; } = "";
    public double MetrePerBirim { get; set; } = 1;
    public List<string> Katmanlar { get; set; } = new();
    public bool MetinKotlariOkundu { get; set; }
}
