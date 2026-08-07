namespace Kst.Acoustics.Core.Simulation;

public static class AcousticParameterCalculator
{
    private static readonly int[] BandFreqs = [63, 125, 250, 500, 1000, 2000, 4000, 8000];

    public static AcousticParameters Compute(EchogramData echogram, SimulationParameters? parameters = null)
    {
        var arrivals = echogram.Arrivals;
        var nRays  = parameters?.RayCount       ?? 5_000;
        // Yarıçap yalnız G (Strength) referansında kullanılır; G referansı 10m metre
        // cinsindendir → yarıçabı metreye çevir (birim-değişmez G için — C2).
        var radius = (parameters?.ReceiverRadius ?? 1.0) * (parameters?.UnitScale ?? 1.0);

        if (arrivals.Count == 0)
        {
            var empty = BandFreqs.Select(f =>
                new BandParameters(f, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, [])).ToArray();
            return new AcousticParameters(echogram.SourceId, echogram.ReceiverId,
                echogram.SourceLabel, echogram.ReceiverLabel, empty);
        }

        var bands = BandFreqs
            .Select((freq, bi) => ComputeBand(arrivals, bi, freq, nRays, radius))
            .ToArray();

        var sti = ComputeSTI(arrivals);
        return new AcousticParameters(echogram.SourceId, echogram.ReceiverId,
            echogram.SourceLabel, echogram.ReceiverLabel, bands) { STI = sti };
    }

    // ─── Tek bant hesaplama ───────────────────────────────────────────────────

    // Schroeder eğrisi zaman binleme adımı. Monte Carlo gelişleri tekil ve gürültülü
    // olduğundan, RT regresyonu önce sabit zaman aralıklarına toplanmış enerji
    // histogramı üzerinden kurulur → daha gürbüz ve örnekleme-bağımsız bozunma eğrisi.
    private const double DecayBinSeconds = 0.005; // 5 ms

    private static BandParameters ComputeBand(
        IReadOnlyList<EchogramArrival> arrivals, int bi, int freq,
        int nRays, double receiverRadius)
    {
        var times    = arrivals.Select(a => a.TimeSeconds).ToArray();
        var energies = arrivals.Select(a => BandEnergy(a, bi)).ToArray();

        // Toplam enerji (ham) — Ts/D50/G enerji oranları için.
        double totalEnergy = 0;
        for (var i = 0; i < energies.Length; i++) totalEnergy += energies[i];
        if (totalEnergy < 1e-30)
            return new BandParameters(freq, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, []);

        // ── Zaman-binlenmiş Schroeder bozunma eğrisi ──
        var (binTimes, binDb, dynamicRangeDb) = BuildBinnedDecay(times, energies, totalEnergy);

        var t20 = ComputeRTxx(binTimes, binDb, -5.0, -25.0, out _);
        var t30 = ComputeRTxx(binTimes, binDb, -5.0, -35.0, out var r2T30);
        var edt = ComputeRTxx(binTimes, binDb,  0.0, -10.0, out _);

        // RT60 hibrit seçim (ISO 3382-1 pratik uygulaması):
        // T30 tercih edilir (en geniş bozunma aralığı, en gürbüz).
        // Yeterli bozunma yoksa T30 NaN olur → T20'ye düş; o da yoksa EDT'ye.
        var rt60    = !double.IsNaN(t30) ? t30
                    : !double.IsNaN(t20) ? t20
                    : edt;

        var ts      = ComputeTs(times, energies, totalEnergy);
        var g       = ComputeG(totalEnergy, nRays, receiverRadius);
        var c50     = ComputeClarity(times, energies, 0.050);
        var c80     = ComputeClarity(times, energies, 0.080);
        var d50     = ComputeDefinition(times, energies, 0.050, totalEnergy);

        var lf      = ComputeLateralFraction(arrivals, bi, false);
        var lfc     = ComputeLateralFraction(arrivals, bi, true);

        var stEarly = ComputeSupport(times, energies, 0.020, 0.100);
        var stLate  = ComputeSupport(times, energies, 0.100, 1.000);

        var curve = DownsampleCurve(binTimes, binDb, 300);

        return new BandParameters(freq, rt60, t20, t30, edt, ts, g, c50, c80, d50, lf, lfc, stEarly, stLate, curve, r2T30, dynamicRangeDb);
    }

    // ─── Zaman-binlenmiş bozunma eğrisi (gürbüz Schroeder) ───────────────────

    private static (double[] times, double[] db, double dynamicRangeDb) BuildBinnedDecay(
        double[] times, double[] energies, double totalEnergy)
    {
        var t0 = times.Length > 0 ? times[0] : 0.0;
        var tEnd = times.Length > 0 ? times[^1] : 0.0;
        var span = tEnd - t0;

        int binCount = Math.Max(1, (int)Math.Ceiling(span / DecayBinSeconds) + 1);
        var binEnergy = new double[binCount];

        for (var i = 0; i < times.Length; i++)
        {
            int idx = (int)((times[i] - t0) / DecayBinSeconds);
            if (idx < 0) idx = 0;
            if (idx >= binCount) idx = binCount - 1;
            binEnergy[idx] += energies[i];
        }

        // Geriye kümülatif → Schroeder enerji eğrisi.
        var cum = new double[binCount + 1];
        for (var i = binCount - 1; i >= 0; i--)
            cum[i] = cum[i + 1] + binEnergy[i];

        var binTimes = new double[binCount];
        var binDb     = new double[binCount];
        var minDb = 0.0;
        for (var i = 0; i < binCount; i++)
        {
            binTimes[i] = t0 + (i + 0.5) * DecayBinSeconds;
            binDb[i]    = 10 * Math.Log10(Math.Max(cum[i] / totalEnergy, 1e-30));
            if (binDb[i] > -300 && binDb[i] < minDb) minDb = binDb[i];
        }

        return (binTimes, binDb, minDb);
    }

    private static double ComputeTs(double[] times, double[] energies, double totalEnergy)
    {
        if (totalEnergy < 1e-30) return 0;
        double sum = 0;
        double t0 = times.Length > 0 ? times[0] : 0;
        for (int i = 0; i < times.Length; i++)
            sum += (times[i] - t0) * energies[i];
        return (sum / totalEnergy) * 1000.0; // ms
    }

    private static double ComputeG(double totalEnergy, int nRays, double receiverRadius)
    {
        // G = 10 * log10(E_total / E_free_10m)
        var eRef = nRays * receiverRadius * receiverRadius / AcousticConstants.GReferenceArea;
        if (eRef < 1e-30 || totalEnergy < 1e-30) return double.NaN;
        return 10 * Math.Log10(totalEnergy / eRef);
    }

    private static double ComputeLateralFraction(IReadOnlyList<EchogramArrival> arrivals, int bi, bool cosine)
    {
        double lateral = 0;
        double total = 0;
        double t0 = arrivals.Count > 0 ? arrivals[0].TimeSeconds : 0;
        var lateralAxis = EstimateLateralAxis(arrivals);

        foreach (var a in arrivals)
        {
            var dt = a.TimeSeconds - t0;
            if (dt < 0.005 || dt > 0.080) continue;

            var energy = BandEnergy(a, bi);
            total += energy;

            double cosTheta = Math.Abs((a.DirX * lateralAxis.X) + (a.DirY * lateralAxis.Y));
            lateral += energy * (cosine ? cosTheta : cosTheta * cosTheta);
        }

        if (total < 1e-30) return 0;
        return lateral / total;
    }

    private static (double X, double Y) EstimateLateralAxis(IReadOnlyList<EchogramArrival> arrivals)
    {
        if (arrivals.Count == 0) return (0, 1);

        var direct = arrivals[0];
        var len = Math.Sqrt((direct.DirX * direct.DirX) + (direct.DirY * direct.DirY));
        if (len < 1e-9) return (0, 1);

        var frontX = direct.DirX / len;
        var frontY = direct.DirY / len;
        return (-frontY, frontX);
    }

    private static double ComputeSupport(double[] times, double[] energies, double start, double end)
    {
        double direct = 0;
        double reflected = 0;
        double t0 = times.Length > 0 ? times[0] : 0;

        for (int i = 0; i < times.Length; i++)
        {
            var dt = times[i] - t0;
            if (dt <= 0.010) direct += energies[i];
            if (dt >= start && dt <= end) reflected += energies[i];
        }

        if (direct < 1e-30 || reflected < 1e-30) return -99;
        return 10 * Math.Log10(reflected / direct);
    }

    // ─── RT hesaplama (-5→-35 dB aralığından -60 dB'e extrapolasyon) ─────────

    private static double ComputeRTxx(double[] times, double[] schroeder, double dbStart, double dbEnd, out double r2)
    {
        r2 = double.NaN;
        double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0, sumY2 = 0;
        int count = 0;

        for (int i = 0; i < schroeder.Length; i++)
        {
            var db = schroeder[i];
            if (db <= dbStart && db >= dbEnd)
            {
                var t = times[i];
                sumX += t;
                sumY += db;
                sumXY += t * db;
                sumX2 += t * t;
                sumY2 += db * db;
                count++;
            }
        }

        if (count >= 5)
        {
            var meanX = sumX / count;
            var meanY = sumY / count;
            var denominator = sumX2 - sumX * meanX;
            if (Math.Abs(denominator) > 1e-20)
            {
                var slope = (sumXY - sumX * meanY) / denominator;

                // Doğrusal uyum kalitesi R² (Pearson² regresyon belirleme katsayısı).
                var varX = sumX2 - sumX * meanX;
                var varY = sumY2 - sumY * meanY;
                var covXY = sumXY - sumX * meanY;
                r2 = (varX > 1e-20 && varY > 1e-20)
                    ? Math.Clamp((covXY * covXY) / (varX * varY), 0.0, 1.0)
                    : double.NaN;

                if (slope < 0) return -60.0 / slope;
            }
        }

        var t1 = InterpolateTime(times, schroeder, dbStart);
        var t2 = InterpolateTime(times, schroeder, dbEnd);

        if (double.IsNaN(t1) || double.IsNaN(t2) || t2 <= t1) return double.NaN;

        var m = (dbEnd - dbStart) / (t2 - t1);
        return -60.0 / m;
    }

    private static double InterpolateTime(double[] times, double[] schroeder, double targetDb)
    {
        for (var i = 0; i < schroeder.Length - 1; i++)
        {
            if (schroeder[i] >= targetDb && schroeder[i + 1] <= targetDb)
            {
                var span = schroeder[i + 1] - schroeder[i];
                if (Math.Abs(span) < 1e-15) return times[i];
                var frac = (targetDb - schroeder[i]) / span;
                return times[i] + frac * (times[i + 1] - times[i]);
            }
        }
        return double.NaN;
    }

    // ─── Netlik ve Tanımlılık ─────────────────────────────────────────────────

    private static double ComputeClarity(double[] times, double[] energies, double splitSec)
    {
        double early = 0, late = 0;
        double t0 = times.Length > 0 ? times[0] : 0.0;
        for (var i = 0; i < times.Length; i++)
        {
            if (times[i] - t0 <= splitSec) early += energies[i];
            else late += energies[i];
        }
        if (early < 1e-30 || late < 1e-30) return double.NaN;
        return 10 * Math.Log10(early / late);
    }

    private static double ComputeDefinition(double[] times, double[] energies, double splitSec, double total)
    {
        if (total < 1e-30) return double.NaN;
        double early = 0;
        double t0 = times.Length > 0 ? times[0] : 0.0;
        for (var i = 0; i < times.Length; i++)
            if (times[i] - t0 <= splitSec) early += energies[i];
        return early / total;
    }

    // ─── Schroeder eğrisi için nokta örneklemesi (binlenmiş eğriden) ─────────

    private static SchroederCurvePoint[] DownsampleCurve(
        double[] times, double[] db, int maxPoints)
    {
        if (times.Length == 0) return [new SchroederCurvePoint(0.0, 0.0)];
        var step = Math.Max(1, times.Length / maxPoints);
        var list = new List<SchroederCurvePoint>(maxPoints + 2)
        {
            new(times[0], 0.0)
        };
        for (var i = 0; i < times.Length; i += step)
            list.Add(new SchroederCurvePoint(times[i], db[i]));
        list.Add(new SchroederCurvePoint(times[^1], db[^1]));
        return [.. list];
    }

    // ─── Bant enerji erişimi ──────────────────────────────────────────────────

    private static double BandEnergy(EchogramArrival a, int bi) => bi switch
    {
        0 => a.E63,  1 => a.E125, 2 => a.E250, 3 => a.E500,
        4 => a.E1k,  5 => a.E2k,  6 => a.E4k,  7 => a.E8k,
        _ => 0
    };

    // ─── STI (ekogram tabanlı MTF, 7 bant: 125 Hz – 8 kHz) ───────────────────

    private static readonly double[] ModFreqs =
        [0.63, 0.80, 1.00, 1.25, 1.60, 2.00, 2.50, 3.15, 4.00, 5.00, 6.30, 8.00, 10.0, 12.5];

    // IEC 60268-16 erkek konuşma ağırlıkları — 7 bant (125..8k Hz), normalize
    // Orijinal: 125→0.085, 250→0.127, 500→0.230, 1k→0.233, 2k→0.309, 4k→0.224, 8k→0.173  (toplam 1.381)
    private static readonly double[] BandWeights =
        [0.0616, 0.0920, 0.1665, 0.1688, 0.2238, 0.1622, 0.1253];

    private static double ComputeSTI(IReadOnlyList<EchogramArrival> arrivals)
    {
        double sti       = 0;
        double weightSum = 0;

        // bi=0 63 Hz bandı — IEC 60268-16 kapsamı dışı, atla
        for (int bi = 1; bi < BandFreqs.Length && (bi - 1) < BandWeights.Length; bi++)
        {
            var bandEnergies = arrivals.Select(a => BandEnergy(a, bi)).ToArray();
            var totalEnergy  = bandEnergies.Sum();
            if (totalEnergy < 1e-30) continue;

            double mtiSum = 0;
            foreach (var F in ModFreqs)
            {
                double real = 0;
                double imag = 0;
                for (int i = 0; i < arrivals.Count; i++)
                {
                    var e = bandEnergies[i];
                    if (e <= 0) continue;

                    var phase = 2.0 * Math.PI * F * arrivals[i].TimeSeconds;
                    real += e * Math.Cos(phase);
                    imag -= e * Math.Sin(phase);
                }

                double m   = Math.Clamp(Math.Sqrt((real * real) + (imag * imag)) / totalEnergy, 0.0, 1.0);
                double snr = 10.0 * Math.Log10(Math.Max(m, 1e-10) / Math.Max(1 - m, 1e-10));
                snr        = Math.Clamp(snr, -15.0, 15.0);
                mtiSum    += (snr + 15.0) / 30.0;
            }

            double mti    = mtiSum / ModFreqs.Length;
            double w      = BandWeights[bi - 1];
            sti          += w * mti;
            weightSum    += w;
        }

        if (weightSum < 1e-10)
        {
            System.Diagnostics.Debug.WriteLine("STI Fail: weightSum < 1e-10. Echogram empty or all bands below energy threshold.");
            return double.NaN;
        }
        return Math.Clamp(sti / weightSum, 0.0, 1.0);
    }
}
