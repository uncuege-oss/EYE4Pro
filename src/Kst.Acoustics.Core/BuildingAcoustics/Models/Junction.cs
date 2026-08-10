using System;

namespace Kst.Acoustics.Core.BuildingAcoustics.Models;

public enum JunctionKenarTipi
{
    Alt,
    Ust,
    Sol,
    Sag
}

public enum JunctionGeometriTipi
{
    T,
    L,
    X,
    Arti
}

/// Ek2 — Junction'da strüktürel güçlendirme tipi. Annex E rijit tabanın üzerine
/// stiffener tipine bağlı ek decoupling katar.
///   Yok        : sıradan birleşim (varsayılan, ek 0)
///   Kiris      : yatay strüktürel kiriş — sınırlı vibrasyon yayılımı (+3 dB)
///   PerdeDuvar : düşey perde duvar / shear wall (diyafram aksı) — yüksek süreklilik (+5 dB)
public enum JunctionStrukturelTipi
{
    Yok,
    Kiris,
    PerdeDuvar,
}

public class Junction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public JunctionKenarTipi Kenar { get; set; }
    public JunctionGeometriTipi GeometriTipi { get; set; } = JunctionGeometriTipi.T;
    public BaglantiTipi BaglantiTipi { get; set; } = BaglantiTipi.Rijit;
    /// Ek2 — bu birleşimde strüktürel güçlendirme (kiriş / perde duvar / yok).
    public JunctionStrukturelTipi StrukturelTipi { get; set; } = JunctionStrukturelTipi.Yok;
    public double Uzunluk { get; set; }
    public double KijOverrideDb { get; set; }
    public string DetayReferansi { get; set; } = "";
    public string UygulamaNotu { get; set; } = "";

    public double KijDb => KijOverrideDb > 0 ? KijOverrideDb : BaglantiKij.GetKij(BaglantiTipi);
}
