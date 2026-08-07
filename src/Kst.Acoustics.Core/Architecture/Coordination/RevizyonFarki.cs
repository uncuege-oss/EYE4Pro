using System.Collections.Generic;
using System.Linq;

namespace Kst.Acoustics.Core.Architecture.Coordination;

/// <summary>
/// Revizyon farkında eklenen/silinen bir mahalin özet girişi.
/// </summary>
public sealed class RevizyonMahalGirisi
{
    public string OdaId { get; init; } = "";
    public string Ad { get; init; } = "";
    public int KatNo { get; init; }
    public double AlanM2 { get; init; }
}

/// <summary>
/// İki revizyonda da bulunan ama ad/alan/kat'ı değişen mahal.
/// </summary>
public sealed class RevizyonMahalDegisikligi
{
    public string OdaId { get; init; } = "";
    public string EskiAd { get; init; } = "";
    public string YeniAd { get; init; } = "";
    public int EskiKatNo { get; init; }
    public int YeniKatNo { get; init; }
    public double EskiAlanM2 { get; init; }
    public double YeniAlanM2 { get; init; }

    public double AlanDeltaM2 => YeniAlanM2 - EskiAlanM2;
    public bool AdDegisti => !string.Equals(EskiAd, YeniAd, System.StringComparison.Ordinal);
    public bool KatDegisti => EskiKatNo != YeniKatNo;
}

/// <summary>
/// İki mimari tasarım/`.e4bina` arasındaki revizyon farkı raporu.
/// </summary>
public sealed class RevizyonFarki
{
    public IReadOnlyList<RevizyonMahalGirisi> EklenenMahaller { get; init; } = [];
    public IReadOnlyList<RevizyonMahalGirisi> SilinenMahaller { get; init; } = [];
    public IReadOnlyList<RevizyonMahalDegisikligi> DegisenMahaller { get; init; } = [];
    public IReadOnlyList<int> EklenenKatlar { get; init; } = [];
    public IReadOnlyList<int> SilinenKatlar { get; init; } = [];
    public double ToplamAlanDeltaM2 { get; init; }

    public int EklenenMahalSayisi => EklenenMahaller.Count;
    public int SilinenMahalSayisi => SilinenMahaller.Count;
    public int DegisenMahalSayisi => DegisenMahaller.Count;

    public bool FarkVar =>
        EklenenMahaller.Count > 0 || SilinenMahaller.Count > 0 || DegisenMahaller.Count > 0 ||
        EklenenKatlar.Count > 0 || SilinenKatlar.Count > 0;
}
