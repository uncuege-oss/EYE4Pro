using System.Collections.Generic;
using System.Linq;

namespace Kst.Acoustics.Core.Architecture.Coordination;

/// <summary>
/// Mutabakat bulgusunun türü.
/// </summary>
public enum MutabakatBulguTuru
{
    /// Malzeme ataması, 3D modelde artık var olmayan bir elemana bağlı.
    AskidaAtama,
    /// Atama veya finisaj profili, katalogda bulunmayan bir yapı elemanı tipine işaret ediyor.
    GecersizTipReferansi
}

/// <summary>
/// Tek bir mutabakat bulgusu.
/// </summary>
public sealed class MutabakatBulgusu
{
    public MutabakatBulguTuru Tur { get; init; }
    public string KuralKodu { get; init; } = "";
    public string Aciklama { get; init; } = "";
    /// İlgili atama Id'si veya finisaj profil kodu.
    public string KaynakId { get; init; } = "";
    public string? EtkilenenElemanId { get; init; }
}

/// <summary>
/// Mimari projenin içsel tutarlılık (mutabakat) raporu: askıda atamalar ve kırık tip referansları.
/// </summary>
public sealed class MutabakatRaporu
{
    public IReadOnlyList<MutabakatBulgusu> Bulgular { get; init; } = [];

    public int AskidaAtamaSayisi => Bulgular.Count(b => b.Tur == MutabakatBulguTuru.AskidaAtama);
    public int GecersizTipReferansiSayisi => Bulgular.Count(b => b.Tur == MutabakatBulguTuru.GecersizTipReferansi);
    public int ToplamBulguSayisi => Bulgular.Count;
    public bool TutarliMi => Bulgular.Count == 0;
}
