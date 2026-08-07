using System;
using System.Collections.Generic;
using System.Linq;

namespace Kst.Acoustics.Core.Architecture.Model3D;

/// <summary>
/// Mimari plan taslagindan turetilen hafif 3D BIM model kok nesnesi.
/// </summary>
public class Bim3DModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Ad { get; set; } = "3D BIM Model";
    public string KaynakPlanId { get; set; } = "";
    public DateTime UretimZamaniUtc { get; set; } = DateTime.UtcNow;
    public List<Kat3DModel> Katlar { get; set; } = new();
    public string Notlar { get; set; } = "";

    public int ToplamKatSayisi => Katlar.Count;
    public int ToplamMahalSayisi => Katlar.Sum(k => k.Mahaller.Count);
    public int ToplamDuvarSayisi => Katlar.Sum(k => k.Duvarlar.Count);
    public int ToplamAciklikSayisi => Katlar.Sum(k => k.Acikliklar.Count);
    public int ToplamCekirdekHacmiSayisi => Katlar.Sum(k => k.CekirdekHacimleri.Count);
    public int ToplamSaftHacmiSayisi => Katlar.Sum(k => k.SaftHacimleri.Count);
    public double ToplamMahalHacmiM3 => Katlar.Sum(k => k.ToplamMahalHacmiM3);
    public double ToplamDosemeAlaniM2 => Katlar.Sum(k => k.ToplamDosemeAlaniM2);
    public double ToplamDuvarAlaniM2 => Katlar.Sum(k => k.ToplamDuvarAlaniM2);

    public Kat3DModel? KatBul(int katNo)
        => Katlar.FirstOrDefault(k => k.KatNo == katNo);
}
