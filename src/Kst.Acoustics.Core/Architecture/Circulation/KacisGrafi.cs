using System;
using System.Collections.Generic;
using System.Linq;

namespace Kst.Acoustics.Core.Architecture.Circulation;

public class KacisGrafi
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PlanTaslagiId { get; set; } = "";
    public List<KacisDugumu> Dugumler { get; set; } = new();
    public List<KacisKenari> Kenarlar { get; set; } = new();
    public string Notlar { get; set; } = "";

    public int DugumSayisi => Dugumler.Count;
    public int KenarSayisi => Kenarlar.Count;
    public IReadOnlyList<KacisDugumu> MahalDugumleri => Dugumler.Where(d => d.Tip == KacisDugumTipi.Mahal).ToList();
    public IReadOnlyList<KacisDugumu> MerdivenDugumleri => Dugumler.Where(d => d.KacisHedefiMi).ToList();
}

public class KacisDugumu
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string KaynakId { get; set; } = "";
    public string KatId { get; set; } = "";
    public int KatNo { get; set; } = 1;
    public string Ad { get; set; } = "";
    public KacisDugumTipi Tip { get; set; } = KacisDugumTipi.Tanimsiz;
    public double X { get; set; }
    public double Y { get; set; }

    public bool KacisHedefiMi => Tip is KacisDugumTipi.Merdiven or KacisDugumTipi.YanginMerdiveni;
}

public class KacisKenari
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string KaynakDugumId { get; set; } = "";
    public string HedefDugumId { get; set; } = "";
    public int KatNo { get; set; } = 1;
    public KacisKenariTipi Tip { get; set; } = KacisKenariTipi.Tanimsiz;
    public double MesafeM { get; set; }
    public string Notlar { get; set; } = "";
}

public enum KacisDugumTipi
{
    Tanimsiz = 0,
    Mahal = 1,
    Koridor = 2,
    Merdiven = 3,
    YanginMerdiveni = 4
}

public enum KacisKenariTipi
{
    Tanimsiz = 0,
    MahalKoridor = 1,
    KoridorMerdiven = 2
}
