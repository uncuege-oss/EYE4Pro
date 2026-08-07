namespace Kst.Acoustics.Core.Architecture.Rules;

/// <summary>
/// Mimari proje on kontrol sonucunu tasir.
/// </summary>
public class MimariKontrolSonucu
{
    public KontrolSeviyesi Seviye { get; set; } = KontrolSeviyesi.Bilgi;
    public string Kod { get; set; } = "";
    public string Mesaj { get; set; } = "";
    public string? MahalId { get; set; }
    public string? Kaynak { get; set; }
    public string? CozumOnerisi { get; set; }

    public bool KritikMi => Seviye == KontrolSeviyesi.Kritik;
}
