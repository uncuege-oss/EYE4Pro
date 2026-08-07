namespace Kst.Acoustics.Core.Architecture.Circulation;

public class SirkulasyonKontrolUyarisi
{
    public string KuralKodu { get; set; } = "";
    public string Mesaj { get; set; } = "";
    public string CozumOnerisi { get; set; } = "";
    public string? EtkilenenKatId { get; set; }
    public string? EtkilenenMahalId { get; set; }
    public string? EtkilenenElemanId { get; set; }
    public string? AksKodu { get; set; }
}
