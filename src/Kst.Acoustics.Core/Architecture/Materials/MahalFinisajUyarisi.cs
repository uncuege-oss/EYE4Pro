namespace Kst.Acoustics.Core.Architecture.Materials;

/// <summary>
/// Mahal finisaj on kontrolunun urettigi tek uyari. M34 (Rules) tarafinda KuralSonucu'na
/// donusturulur; Materials katmanini Rules'a baglamamak icin sade bir DTO olarak tutulur.
/// </summary>
public class MahalFinisajUyarisi
{
    public string KuralKodu { get; set; } = "";
    public string Mesaj { get; set; } = "";
    public string CozumOnerisi { get; set; } = "";

    /// <summary>
    /// True ise eksik veri (kontrol edilemedi) sonucu; false ise uyari seviyesi uretilir.
    /// </summary>
    public bool EksikVeriMi { get; set; }

    public string? EtkilenenMahalId { get; set; }
    public string? VeriEksigi { get; set; }
}
