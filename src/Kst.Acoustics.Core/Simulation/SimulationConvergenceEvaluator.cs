namespace Kst.Acoustics.Core.Simulation;

/// <summary>Ardışık iki simülasyon koşusundan özet akustik metrikler.</summary>
public readonly record struct SimulationConvergenceMetrics(
    double Rt60Mid,
    double C80,
    double Sti,
    double EdtMid,
    double StrengthG);

/// <summary>Yakınsama kararıyla birlikte kullanıcıya raporlanacak mutlak farklar.</summary>
public readonly record struct SimulationConvergenceComparison(
    bool IsStable,
    int ValidMetricCount,
    double Rt60Difference,
    double C80Difference,
    double StiDifference,
    double EdtDifference,
    double StrengthDifference);

/// <summary>
/// Yakınsama kararını sonlu değer ve JND kurallarıyla tek noktada uygular.
/// Ölçülemeyen (NaN/Infinity) değerler hiçbir zaman "değişmedi" sayılmaz.
/// </summary>
public static class SimulationConvergenceEvaluator
{
    public static SimulationConvergenceComparison Compare(
        SimulationConvergenceMetrics lower,
        SimulationConvergenceMetrics higher)
    {
        var drt = AbsDiff(lower.Rt60Mid, higher.Rt60Mid);
        var dc80 = AbsDiff(lower.C80, higher.C80);
        var dsti = AbsDiff(lower.Sti, higher.Sti);
        var dedt = AbsDiff(lower.EdtMid, higher.EdtMid);
        var dg = AbsDiff(lower.StrengthG, higher.StrengthG);
        var validMetricCount = new[] { drt, dc80, dsti, dedt, dg }.Count(double.IsFinite);

        // En az üç karşılaştırılabilir metrik ve mutlaka RT60 olmadan erken
        // durma yoktur. Böylece boş ekogramlar sahte yakınsama üretemez.
        var stable = validMetricCount >= 3
                     && double.IsFinite(drt)
                     && drt <= ReverbJnd(lower.Rt60Mid, higher.Rt60Mid)
                     && (!double.IsFinite(dc80) || dc80 <= AcousticConstants.JndClarityDb)
                     && (!double.IsFinite(dsti) || dsti <= AcousticConstants.JndSti)
                     && (!double.IsFinite(dedt) || dedt <= ReverbJnd(lower.EdtMid, higher.EdtMid))
                     && (!double.IsFinite(dg) || dg <= AcousticConstants.JndClarityDb);

        return new SimulationConvergenceComparison(stable, validMetricCount, drt, dc80, dsti, dedt, dg);
    }

    private static double AbsDiff(double a, double b) =>
        double.IsFinite(a) && double.IsFinite(b) ? Math.Abs(a - b) : double.NaN;

    private static double ReverbJnd(double a, double b)
    {
        var magnitude = Math.Max(double.IsFinite(a) ? Math.Abs(a) : 0,
                                 double.IsFinite(b) ? Math.Abs(b) : 0);
        return Math.Max(0.02, magnitude * AcousticConstants.JndReverbRelative);
    }
}
