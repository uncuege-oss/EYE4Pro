namespace Kst.Acoustics.Core;

public static class AcousticConstants
{
    /// <summary>Sabine formülü sabiti (0.161 s/m).</summary>
    public const double SabineConstant = 0.161;

    /// <summary>Schroeder çınlama süresi azalma eşiği (-60 dB için 13.8 log birimi).</summary>
    public const double SchroederDecayFactor = 13.8;

    /// <summary>Referans hoparlör duyarlılığı (dB @ 1m, 1W).</summary>
    public const double ReferenceSensitivityDb = 90.0;

    /// <summary>G (Güç Faktörü) referans alanı (10m mesafedeki küre alanı/400).</summary>
    public const double GReferenceArea = 400.0;

    /// <summary>Hava yoğunluğu (kg/m3) - Standart koşullar.</summary>
    public const double AirDensity = 1.204;

    // ── ISO 3382-1 / IEC 60268-16 "Just Noticeable Difference" (JND) eşikleri ──
    // İki sonucun bu farktan az ayrılması işitsel olarak ayırt edilemez kabul
    // edilir → yakınsama/stabilite kararlarının fiziksel ölçütüdür.

    /// <summary>RT60 ve EDT için JND: değerin %5'i (göreli).</summary>
    public const double JndReverbRelative = 0.05;

    /// <summary>C80 ve G (Strength) için JND: 1 dB.</summary>
    public const double JndClarityDb = 1.0;

    /// <summary>STI için JND: 0.03.</summary>
    public const double JndSti = 0.03;
}
