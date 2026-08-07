using System;
using System.Collections.Generic;

namespace Kst.Acoustics.Core.Architecture.Materials;

/// <summary>
/// M35 3D eleman Id'sine yapi elemani tipi ve istege bagli malzemeleri baglar.
/// </summary>
public class MalzemeAtamasi
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public MalzemeAtamaHedefTuru KaynakElemanTuru { get; set; } = MalzemeAtamaHedefTuru.Tanimsiz;
    public string KaynakElemanId { get; set; } = "";
    public int KatNo { get; set; } = 1;
    public string YapiElemaniTipiId { get; set; } = "";
    public List<string> MalzemeIdleri { get; set; } = new();
    public DateTime AtamaTarihiUtc { get; set; } = DateTime.UtcNow;
    public string Notlar { get; set; } = "";

    public bool KaynakBaglantisiVar
        => KaynakElemanTuru != MalzemeAtamaHedefTuru.Tanimsiz && !string.IsNullOrWhiteSpace(KaynakElemanId);

    public bool TipBaglantisiVar => !string.IsNullOrWhiteSpace(YapiElemaniTipiId);
    public bool MalzemeBaglantisiVar => MalzemeIdleri.Count > 0;
    public bool GecerliMi => KaynakBaglantisiVar && TipBaglantisiVar;
}
