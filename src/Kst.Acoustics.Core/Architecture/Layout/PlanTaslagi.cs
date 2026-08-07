using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Architecture.Circulation;
using Kst.Acoustics.Core.Architecture.Model3D;

namespace Kst.Acoustics.Core.Architecture.Layout;

/// <summary>
/// M32 konsept planinin kat, blok, iliski ve parametrelerini tek kokte toplar.
/// </summary>
public class PlanTaslagi
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProjeId { get; set; } = "";
    public List<KatTaslagi> Katlar { get; set; } = new();
    public List<MahalIliskisi> Iliskiler { get; set; } = new();
    public KacisGrafi KacisGrafi { get; set; } = new();
    public Bim3DModel? Model3D { get; set; }
    public YerlesimParametreleri Parametreler { get; set; } = new();
    public string Notlar { get; set; } = "";

    public int KatSayisi => Katlar.Count;
    public int ToplamBlokSayisi => Katlar.Sum(k => k.BlokSayisi);
    public int ToplamCekirdekSayisi => Katlar.Sum(k => k.CekirdekSayisi);
    public int ToplamSaftSayisi => Katlar.Sum(k => k.SaftSayisi);
    public double ToplamHedefAlanM2 => Katlar.Sum(k => k.HedefAlanM2 > 0 ? k.HedefAlanM2 : k.BlokHedefAlanM2);
    public double ToplamTaslakAlanM2 => Katlar.Sum(k => k.ToplamTaslakAlanM2);
    public double Toplam3DMahalHacmiM3 => Model3D?.ToplamMahalHacmiM3 ?? 0;
    public double Toplam3DDosemeAlaniM2 => Model3D?.ToplamDosemeAlaniM2 ?? 0;

    public KatTaslagi? KatBul(int katNo)
        => Katlar.FirstOrDefault(k => k.KatNo == katNo);
}

public class YerlesimParametreleri
{
    public double VarsayilanKoridorGenisligiM { get; set; } = 1.5;
    public double BloklarArasiBoslukM { get; set; } = 1.0;
    public double CepheDerinligiM { get; set; } = 6.0;
    public double AlanToleransiOrani { get; set; } = 0.10;
    public int VarsayilanKatSayisi { get; set; } = 1;
}
