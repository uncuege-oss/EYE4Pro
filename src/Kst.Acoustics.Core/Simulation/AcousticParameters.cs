namespace Kst.Acoustics.Core.Simulation;

/// <summary>Schroeder bozunma grafiğinin JSON-uyumlu zaman/seviye noktası.</summary>
public readonly record struct SchroederCurvePoint(double Time, double Db);

public sealed record BandParameters(
    int    FrequencyHz,
    double RT60,            // saniye (T30 extrapolated)
    double T20,             // saniye
    double T30,             // saniye
    double EDT,             // saniye
    double Ts,              // ms (Center Time)
    double G,               // dB (Strength)
    double C50,             // dB
    double C80,             // dB
    double D50,             // 0-1
    double LF,              // 0-1 (Lateral Fraction)
    double LFC,             // 0-1 (Lateral Fraction Cosine)
    double STearly,         // dB (Support)
    double STlate,          // dB (Support)
    SchroederCurvePoint[] SchroederCurve,
    double DecayFitR2     = double.NaN,   // T30 bozunma doğrusal uyum kalitesi (0–1); 1=mükemmel doğrusal
    double DynamicRangeDb = double.NaN);  // Schroeder eğrisinin ulaştığı en düşük seviye (dB, negatif)

public sealed class AcousticParameters
{
    public AcousticParameters() { }

    public AcousticParameters(
        int sourceId,
        int receiverId,
        string sourceLabel,
        string receiverLabel,
        BandParameters[] bands)
    {
        SourceId = sourceId;
        ReceiverId = receiverId;
        SourceLabel = sourceLabel;
        ReceiverLabel = receiverLabel;
        Bands = bands;
    }

    public int    SourceId      { get; init; }
    public int    ReceiverId    { get; init; }
    public string SourceLabel   { get; init; } = "";
    public string ReceiverLabel { get; init; } = "";

    public IReadOnlyList<BandParameters> Bands { get; init; } = Array.Empty<BandParameters>();

    /// <summary>ISO 3382 mid-frequency RT60 — 500 Hz ve 1 kHz ortalaması.</summary>
    public double RT60_Mid
    {
        get
        {
            var rt500 = Bands.FirstOrDefault(b => b.FrequencyHz == 500)?.RT60 ?? double.NaN;
            var rt1k  = Bands.FirstOrDefault(b => b.FrequencyHz == 1000)?.RT60 ?? double.NaN;
            if (double.IsNaN(rt500) || double.IsNaN(rt1k)) return double.NaN;
            return (rt500 + rt1k) / 2;
        }
    }

    /// <summary>Bass Ratio (BR) = (RT125 + RT250) / (RT500 + RT1000)</summary>
    public double BR
    {
        get
        {
            var r1 = (Bands.FirstOrDefault(b => b.FrequencyHz == 125)?.RT60 ?? 0) +
                     (Bands.FirstOrDefault(b => b.FrequencyHz == 250)?.RT60 ?? 0);
            var r2 = (Bands.FirstOrDefault(b => b.FrequencyHz == 500)?.RT60 ?? 0) +
                     (Bands.FirstOrDefault(b => b.FrequencyHz == 1000)?.RT60 ?? 0);
            return r2 > 0 ? r1 / r2 : 0;
        }
    }

    /// <summary>Treble Ratio (TR) = (RT2k + RT4k) / (RT500 + RT1k)</summary>
    public double TR
    {
        get
        {
            var r1 = (Bands.FirstOrDefault(b => b.FrequencyHz == 2000)?.RT60 ?? 0) +
                     (Bands.FirstOrDefault(b => b.FrequencyHz == 4000)?.RT60 ?? 0);
            var r2 = (Bands.FirstOrDefault(b => b.FrequencyHz == 500)?.RT60 ?? 0) +
                     (Bands.FirstOrDefault(b => b.FrequencyHz == 1000)?.RT60 ?? 0);
            return r2 > 0 ? r1 / r2 : 0;
        }
    }

    /// <summary>Inter-Aural Cross-Correlation. Mono alıcı verisinde hesaplanamaz.</summary>
    public double IACC { get; init; } = double.NaN;

    /// <summary>Ekogram tabanlı modulation-transfer STI tahmini (0–1). NaN = hesaplanamadı.</summary>
    public double STI { get; init; } = double.NaN;

    /// <summary>
    /// Schroeder sıklığı f_S = 2000 × √(RT60_mid / V).
    /// Geometrik akustiğin geçerli olmaya başladığı frekans eşiğidir.
    /// Hacim bilinmiyorsa NaN döner.
    /// </summary>
    public double SchroederFrequency(double roomVolumeM3)
    {
        var rt = RT60_Mid;
        if (double.IsNaN(rt) || rt <= 0 || roomVolumeM3 <= 0) return double.NaN;
        return 2000 * Math.Sqrt(rt / roomVolumeM3);
    }
}
