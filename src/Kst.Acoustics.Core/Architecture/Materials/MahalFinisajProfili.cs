using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Architecture.Models;

namespace Kst.Acoustics.Core.Architecture.Materials;

/// <summary>
/// Bir mahal tipi (ve istege bagli kullanim sinifi) icin onerilen zemin/duvar/tavan
/// yapi elemani tipi profili. Marka onerisi degil, mimara baslangic finisaj karari onerisidir.
/// </summary>
public class MahalFinisajProfili
{
    public string Kod { get; set; } = "";
    public string Ad { get; set; } = "";
    public MahalTipi MahalTipi { get; set; } = MahalTipi.Tanimsiz;
    public KullanimSinifi KullanimSinifi { get; set; } = KullanimSinifi.Tanimsiz;
    public string ZeminTipiId { get; set; } = "";
    public string DuvarTipiId { get; set; } = "";
    public string TavanTipiId { get; set; } = "";
    public List<string> UyariNotlari { get; set; } = new();
    public string KaynakKodu { get; set; } = "M36";
    public string Notlar { get; set; } = "";

    public bool ZeminOnerisiVar => !string.IsNullOrWhiteSpace(ZeminTipiId);
    public bool DuvarOnerisiVar => !string.IsNullOrWhiteSpace(DuvarTipiId);
    public bool TavanOnerisiVar => !string.IsNullOrWhiteSpace(TavanTipiId);
    public bool TamMi => ZeminOnerisiVar && DuvarOnerisiVar && TavanOnerisiVar;
    public bool OnerisiVar => ZeminOnerisiVar || DuvarOnerisiVar || TavanOnerisiVar;

    public IReadOnlyList<string> OnerilenTipIdleri => new[] { ZeminTipiId, DuvarTipiId, TavanTipiId }
        .Where(id => !string.IsNullOrWhiteSpace(id))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    public MahalFinisajProfili Kopyala()
        => new()
        {
            Kod = Kod,
            Ad = Ad,
            MahalTipi = MahalTipi,
            KullanimSinifi = KullanimSinifi,
            ZeminTipiId = ZeminTipiId,
            DuvarTipiId = DuvarTipiId,
            TavanTipiId = TavanTipiId,
            UyariNotlari = [.. UyariNotlari],
            KaynakKodu = KaynakKodu,
            Notlar = Notlar
        };
}
