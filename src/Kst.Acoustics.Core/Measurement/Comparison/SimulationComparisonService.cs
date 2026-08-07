using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kst.Acoustics.Core.Measurement.Data;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Simulation;

namespace Kst.Acoustics.Core.Measurement.Comparison;

public static class SimulationComparisonService
{
    /// <summary>
    /// M01 proje dosyasını (.e4p01) okur, gömülü SimulationResult'tan her echogram için akustik
    /// parametreleri hesaplar ve oktav bantlarında ortalamasını alır.
    /// </summary>
    public static async Task<SimulationComparisonInput> LoadFromProjectFileAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
            throw new FileNotFoundException("Proje dosyası bulunamadı.", path);

        var service = new ProjectFileService();
        var loadResult = await service.LoadWithStatusAsync(path).ConfigureAwait(false);
        var project = loadResult.Project;

        var result = project.LastResult
            ?? throw new InvalidOperationException("Proje dosyasında simülasyon sonucu bulunmuyor. Önce M01'de simülasyonu çalıştırıp kaydedin.");

        if (result.IsStale)
            throw new InvalidOperationException(
                "Projedeki simülasyon sonucu eski olarak işaretli. Ölçümle karşılaştırmadan önce M01'de simülasyonu yeniden çalıştırıp projeyi kaydedin.");

        if (!string.IsNullOrWhiteSpace(result.InputFingerprint))
        {
            var currentParameters = project.SimulationSettings ?? result.Parameters;
            var currentFingerprint = SimulationInputFingerprint.Compute(project, currentParameters);
            if (!string.Equals(currentFingerprint, result.InputFingerprint, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Projedeki simülasyon sonucu güncel proje girdileriyle eşleşmiyor. Ölçümle karşılaştırmadan önce M01'de simülasyonu yeniden çalıştırıp projeyi kaydedin.");
        }

        // Yeni proje dosyaları ham gelişleri bilinçli olarak saklamaz; dosya boyutu
        // küçük kalırken sonuç/rapor tekrar açılabilsin diye ComputedParameters
        // persist edilir. Yalnız eski/bellek-içi sonuçlarda ham gelişe geri düş.
        var paramSets = result.ComputedParameters?
            .Where(p => p is not null && p.Bands.Count > 0)
            .ToList() ?? [];
        if (paramSets.Count == 0)
        {
            foreach (var echo in result.Echograms.Where(e => e.HasImpulseResponseData))
            {
                try { paramSets.Add(AcousticParameterCalculator.Compute(echo, result.Parameters)); }
                catch { /* tek bir echogram başarısız olabilir, atla */ }
            }
        }
        if (paramSets.Count == 0)
            throw new InvalidOperationException(
                "Proje dosyasında karşılaştırılabilir akustik parametre bulunmuyor. " +
                "M01'de simülasyonu yeniden çalıştırıp projeyi kaydedin.");

        var byBand = AverageByBand(paramSets);
        return new SimulationComparisonInput(
            ProjectName: project.Name,
            SourcePath: path,
            SimulatedAtUtc: result.ComputedAt,
            Parameters: byBand,
            EchogramCount: result.Echograms.Count,
            RoomVolumeM3: project.RoomVolume,
            RecoveredFromBackup: loadResult.RecoveredFromBackup,
            SessionWarnings: loadResult.SessionWarnings);
    }

    /// <summary>Bir ölçümün parametrelerini simülasyon ortalamasıyla band-band karşılaştırır.</summary>
    public static ComparisonReport Compare(
        SimulationComparisonInput sim,
        MeasurementData measurement,
        ToleranceProfile? tolerances = null)
    {
        ArgumentNullException.ThrowIfNull(sim);
        ArgumentNullException.ThrowIfNull(measurement);
        var tol = tolerances ?? ToleranceProfile.Default;

        var rows = new List<ParameterComparison>();
        AddRows(rows, sim, measurement, "RT30", "RT60", tol, IsRtParam: true);
        AddRows(rows, sim, measurement, "EDT", "EDT", tol, IsRtParam: true);
        AddRows(rows, sim, measurement, "C50", "C50", tol);
        AddRows(rows, sim, measurement, "C80", "C80", tol);
        AddRows(rows, sim, measurement, "D50", "D50", tol);
        AddRows(rows, sim, measurement, "Ts",  "Ts",  tol);

        return new ComparisonReport(sim, measurement.Label, rows, tol);
    }

    private static void AddRows(
        List<ParameterComparison> rows,
        SimulationComparisonInput sim,
        MeasurementData measurement,
        string measKey,
        string simKey,
        ToleranceProfile tol,
        bool IsRtParam = false)
    {
        if (!sim.Parameters.TryGetValue(simKey, out var simBands)) return;
        int[] measBandsHz = { 63, 125, 250, 500, 1000, 2000, 4000, 8000 };
        var measValues = measurement.Parameters != null && measurement.Parameters.TryGetValue(measKey, out var arr) ? arr : null;

        foreach (var (freq, sval) in simBands)
        {
            double mval = double.NaN;
            if (measValues != null)
            {
                int idx = Array.IndexOf(measBandsHz, freq);
                if (idx >= 0 && idx < measValues.Length)
                    mval = measValues[idx];
            }

            var status = ClassifyStatus(measKey, mval, sval, tol);
            var delta = double.IsNaN(mval) || double.IsNaN(sval) ? double.NaN : mval - sval;
            rows.Add(new ParameterComparison(measKey, freq, mval, sval, delta, status));
        }
    }

    private static ComparisonStatus ClassifyStatus(string parameter, double meas, double sim, ToleranceProfile tol)
    {
        if (double.IsNaN(meas)) return ComparisonStatus.NoMeasurement;
        if (double.IsNaN(sim)) return ComparisonStatus.NoSimulation;

        var d = Math.Abs(meas - sim);
        (double ok, double warn) = parameter switch
        {
            "RT30" or "RT20" or "EDT" => (tol.RtSecondsOk, tol.RtSecondsWarn),
            "C50" or "C80" or "G"     => (tol.CDbOk, tol.CDbWarn),
            "D50"                     => (tol.D50Ok, tol.D50Warn),
            "Ts"                      => (tol.TsMsOk, tol.TsMsWarn),
            _                         => (tol.RtSecondsOk, tol.RtSecondsWarn),
        };
        if (d <= ok)  return ComparisonStatus.Ok;
        if (d <= warn) return ComparisonStatus.Warn;
        return ComparisonStatus.Critical;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<(int FreqHz, double Value)>> AverageByBand(
        IReadOnlyList<AcousticParameters> sets)
    {
        var result = new Dictionary<string, IReadOnlyList<(int, double)>>();
        var bandsByFreq = sets
            .SelectMany(s => s.Bands.Select(b => (Set: s, Band: b)))
            .GroupBy(x => x.Band.FrequencyHz)
            .OrderBy(g => g.Key)
            .ToList();

        result["RT60"] = bandsByFreq.Select(g => (g.Key, Mean(g.Select(x => x.Band.RT60)))).ToList();
        result["EDT"]  = bandsByFreq.Select(g => (g.Key, Mean(g.Select(x => x.Band.EDT)))).ToList();
        result["C50"]  = bandsByFreq.Select(g => (g.Key, Mean(g.Select(x => x.Band.C50)))).ToList();
        result["C80"]  = bandsByFreq.Select(g => (g.Key, Mean(g.Select(x => x.Band.C80)))).ToList();
        result["D50"]  = bandsByFreq.Select(g => (g.Key, Mean(g.Select(x => x.Band.D50)))).ToList();
        result["Ts"]   = bandsByFreq.Select(g => (g.Key, Mean(g.Select(x => x.Band.Ts)))).ToList();
        result["G"]    = bandsByFreq.Select(g => (g.Key, Mean(g.Select(x => x.Band.G)))).ToList();
        return result;
    }

    private static double Mean(IEnumerable<double> xs)
    {
        double sum = 0; int n = 0;
        foreach (var x in xs)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) continue;
            sum += x; n++;
        }
        return n == 0 ? double.NaN : sum / n;
    }
}
