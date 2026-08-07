using System;

namespace Kst.Acoustics.Core.Architecture.Rules;

/// <summary>
/// Bir M34 kuralinin kullaniciya gosterilecek sonucunu tasir.
/// </summary>
public class KuralSonucu
{
    public KontrolSeviyesi Seviye { get; set; } = KontrolSeviyesi.Bilgi;
    public string KuralKodu { get; set; } = "";
    public string KaynakKodu { get; set; } = "";
    public string Baslik { get; set; } = "";
    public string Mesaj { get; set; } = "";
    public string CozumOnerisi { get; set; } = "";
    public string? EtkilenenMahalId { get; set; }
    public string? EtkilenenKatId { get; set; }
    public string? EtkilenenElemanId { get; set; }
    public string? VeriEksigi { get; set; }
    public DateTime KontrolZamani { get; set; } = DateTime.Now;

    public bool KritikMi => Seviye == KontrolSeviyesi.Kritik;
    public bool KontrolEdilemediMi => Seviye == KontrolSeviyesi.KontrolEdilemedi;

    public MimariKontrolSonucu MimariSonucaDonustur()
        => new()
        {
            Seviye = Seviye,
            Kod = KuralKodu,
            Mesaj = string.IsNullOrWhiteSpace(Mesaj) ? Baslik : Mesaj,
            CozumOnerisi = CozumOnerisi,
            MahalId = EtkilenenMahalId,
            Kaynak = KaynakKodu
        };
}
