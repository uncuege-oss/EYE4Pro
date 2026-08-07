namespace Kst.Acoustics.Core.SoundInsulation;

/// <summary>
/// Ses yalıtımı hesabında kullanılacak motor seçimi.
/// </summary>
public enum CalculationMethod
{
    /// <summary>Mevcut mass-law + ampirik düzeltmeler (varsayılan).</summary>
    Empirical = 0,

    /// <summary>Transfer Matrix Method — plate, hava ve Johnson-Champoux-Allard poröz katman matrisleri.</summary>
    TMM = 1,

    /// <summary>Düşük frekans (≤500 Hz): TMM; yüksek frekans (>500 Hz): Empirical. Geçiş bandı yumuşatılır.</summary>
    Hybrid = 2,
}
