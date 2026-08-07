using System;
using System.Collections.Generic;

namespace Kst.Acoustics.Core.BuildingAcoustics.Models;

/// <summary>
/// A8 — Havalandırma menfezi / küçük element. Geometrik alan yerine
/// EN 12354-3 element-normalized level difference Dn,e (dB) ile karakterize edilir.
/// Composite formülünde A_0 = 10 m² referans soğurma alanı üzerinden katkı verir:
///   τ_composite·S_partition = Σ S_i·τ_i + Σ A_0·10^(-Dn,e/10)
/// </summary>
public enum MenfezTipi
{
    /// Pasif hava giriş menfezi (trickle vent). Tipik Dn,e ≈ 32 dB.
    Trickle,
    /// Standart mekanik havalandırma menfezi / kanal sonu. Tipik Dn,e ≈ 38 dB.
    Mekanik,
    /// Akustik söndürücülü (atenüatörlü) menfez. Tipik Dn,e ≈ 48 dB.
    Akustik,
    /// Kullanıcı tanımlı; varsayılan Dn,e değeri uygulanmaz.
    Bagimsiz,
}

public class Menfez
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Ad { get; set; } = "";
    public MenfezTipi Tip { get; set; } = MenfezTipi.Mekanik;

    /// EN 12354-3 element-normalized level difference (dB), referans A_0 = 10 m².
    /// Trickle: 28–36, Mekanik: 32–42, Akustik söndürücülü: 45–55.
    public double DneDb { get; set; } = 38.0;

    /// Opsiyonel 16-band 1/3 oktav Dn,e(f), 100–3150 Hz.
    /// Boş ise tek sayı DneDb tüm bantlara uygulanır.
    public List<double> DneThirdOctave { get; set; } = new();

    public bool HasSpectrum => DneThirdOctave.Count >= 16;
}

public static class MenfezVarsayilan
{
    /// EN 12354-3 Annex C tipik Dn,e değerleri (üretici verisi yokken).
    public static double DneIcin(MenfezTipi tip) => tip switch
    {
        MenfezTipi.Trickle => 32.0,
        MenfezTipi.Mekanik => 38.0,
        MenfezTipi.Akustik => 48.0,
        _ => 38.0,
    };

    /// EN 12354-3 — küçük element referans soğurma alanı (m²).
    public const double A0 = 10.0;
}
