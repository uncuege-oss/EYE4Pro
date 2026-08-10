using Kst.Acoustics.Core.Simulation;
using Kst.Acoustics.Core;

namespace Kst.Acoustics.Core.Projects;

public enum TargetPerformanceStatus
{
    Uygun,
    Sinirda,
    Iyilestirilmeli,
    VeriYok
}

public sealed record TargetMetricEvaluation(
    string Metric,
    string Label,
    int? FrequencyHz,
    double? Value,
    double? TargetMin,
    double? TargetMax,
    string Unit,
    TargetPerformanceStatus Status,
    string Explanation)
{
    public string StatusText => Status switch
    {
        TargetPerformanceStatus.Uygun => "Uygun",
        TargetPerformanceStatus.Sinirda => "Sınırda",
        TargetPerformanceStatus.Iyilestirilmeli => "İyileştirilmeli",
        _ => "Veri yok"
    };
}

public sealed record TargetPerformanceReport(
    RoomType RoomType,
    string RoomTypeName,
    TargetPerformanceStatus OverallStatus,
    string Summary,
    IReadOnlyList<TargetMetricEvaluation> Metrics,
    double? AdditionalEquivalentAbsorptionAreaM2,
    string AbsorptionRecommendation,
    bool UsesStaleResult)
{
    public bool HasData => Metrics.Any(m => m.Status != TargetPerformanceStatus.VeriYok);
    public string OverallStatusText => OverallStatus switch
    {
        TargetPerformanceStatus.Uygun => "Uygun",
        TargetPerformanceStatus.Sinirda => "Sınırda",
        TargetPerformanceStatus.Iyilestirilmeli => "İyileştirilmeli",
        _ => "Veri yok"
    };
}

/// <summary>
/// Simülasyonun kalıcı ComputedParameters verisini oda türü hedeflerine göre
/// yorumlar. Ham ekogram gerektirmez; yeniden açılan proje sonuçlarıyla çalışır.
/// </summary>
public static class TargetPerformanceService
{
    private static readonly int[] OctaveBands = [63, 125, 250, 500, 1000, 2000, 4000, 8000];
    private static readonly double[] RtBandFactors = [1.25, 1.15, 1.07, 1.00, 1.00, 0.96, 0.92, 0.90];

    public static TargetPerformanceReport Evaluate(
        RoomType roomType,
        double roomVolumeM3,
        SimulationResult? result)
    {
        var criteria = RoomTypeRegistry.All.FirstOrDefault(c => c.Type == roomType)
            ?? RoomTypeRegistry.All.First(c => c.Type == RoomType.General);

        if (result?.ComputedParameters is not { Count: > 0 } computed)
            return NoData(roomType, criteria.DisplayName,
                "Hedef değerlendirmesi için kayıtlı akustik parametre bulunmuyor. Önce simülasyon çalıştırın.",
                result?.IsStale ?? false);

        var parameterSets = computed.Where(p => p is not null).ToArray();
        if (parameterSets.Length == 0)
            return NoData(roomType, criteria.DisplayName,
                "Kayıtlı sonuçta değerlendirilebilir kaynak–alıcı çifti bulunmuyor.",
                result.IsStale);

        var effectiveVolume = double.IsFinite(roomVolumeM3) && roomVolumeM3 > 0
            ? roomVolumeM3
            : 100.0;
        var (rtMinMid, rtMaxMid) = criteria.GetTargetRtMid(effectiveVolume);
        var rows = new List<TargetMetricEvaluation>();

        for (var i = 0; i < OctaveBands.Length; i++)
        {
            var frequency = OctaveBands[i];
            var value = Average(parameterSets
                .SelectMany(p => p.Bands)
                .Where(b => b.FrequencyHz == frequency)
                .Select(b => b.RT60));
            var min = rtMinMid * RtBandFactors[i];
            var max = rtMaxMid * RtBandFactors[i];
            rows.Add(EvaluateRange(
                "RT60", $"RT60 · {FormatFrequency(frequency)}", frequency,
                value, min, max, "s", 0.15,
                "Bu bant için sonlu RT60 üretilemedi."));
        }

        var (stiTarget, c50Target, c80Range, gRange) = TargetsFor(roomType);
        rows.Add(EvaluateMinimum(
            "STI", "Konuşma iletim indisi (STI)", null,
            Average(parameterSets.Select(p => p.STI)), stiTarget, "", 0.05,
            "STI hesaplanamadı; kaynak seviyesi veya ekogram enerjisi yetersiz olabilir."));
        rows.Add(EvaluateMinimum(
            "C50", "Konuşma berraklığı (C50)", null,
            AverageMid(parameterSets, b => b.C50), c50Target, "dB", 2.0,
            "500 Hz–1 kHz C50 verisi bulunmuyor."));
        rows.Add(EvaluateRange(
            "C80", "Müzik berraklığı (C80)", null,
            AverageMid(parameterSets, b => b.C80), c80Range.Min, c80Range.Max, "dB", 0.25,
            "500 Hz–1 kHz C80 verisi bulunmuyor."));
        rows.Add(EvaluateRange(
            "G", "Ses gücü (G)", null,
            AverageMid(parameterSets, b => b.G), gRange.Min, gRange.Max, "dB", 0.20,
            "500 Hz–1 kHz G verisi bulunmuyor."));

        var available = rows.Where(r => r.Status != TargetPerformanceStatus.VeriYok).ToArray();
        var overall = available.Length == 0
            ? TargetPerformanceStatus.VeriYok
            : available.Any(r => r.Status == TargetPerformanceStatus.Iyilestirilmeli)
                ? TargetPerformanceStatus.Iyilestirilmeli
                : available.Any(r => r.Status == TargetPerformanceStatus.Sinirda)
                    ? TargetPerformanceStatus.Sinirda
                    : TargetPerformanceStatus.Uygun;

        var rtMid = Average(parameterSets.Select(p => p.RT60_Mid));
        var absorption = EstimateAdditionalAbsorption(roomVolumeM3, rtMid, rtMaxMid);
        var absorptionText = absorption switch
        {
            null => "Ek emilim alanı tahmini için pozitif hacim ve sonlu orta frekans RT60 gerekir.",
            <= 0.05 => "Orta frekans RT60 üst hedefi aşmıyor; ek eşdeğer emilim alanı gerekmiyor.",
            _ => $"Orta frekans RT60'ı yaklaşık {rtMaxMid:F2} s üst hedefine indirmek için " +
                 $"yaklaşık {absorption.Value:F1} m² Sabin ek eşdeğer emilim gerekir. " +
                 "Bu ön boyutlandırmadır; yerleşim ve bant davranışı ayrıca doğrulanmalıdır."
        };

        var stalePrefix = result.IsStale
            ? "Sonuç eski olarak işaretli; aşağıdaki değerleri karar vermeden önce yeniden hesaplayın. "
            : string.Empty;
        var summary = overall switch
        {
            TargetPerformanceStatus.Uygun => "Değerlendirilebilen hedeflerin tamamı uygun aralıkta.",
            TargetPerformanceStatus.Sinirda => "Bazı hedefler tolerans sınırında; kritik alıcı noktalarını doğrulayın.",
            TargetPerformanceStatus.Iyilestirilmeli => "En az bir hedef belirgin biçimde hedef aralığının dışında.",
            _ => "Değerlendirilebilir hedef verisi bulunmuyor."
        };

        return new TargetPerformanceReport(
            roomType,
            criteria.DisplayName,
            overall,
            stalePrefix + summary,
            rows,
            absorption,
            absorptionText,
            result.IsStale);
    }

    private static TargetPerformanceReport NoData(
        RoomType type,
        string name,
        string summary,
        bool stale) =>
        new(type, name, TargetPerformanceStatus.VeriYok, summary, [], null,
            "Ek emilim alanı tahmini için simülasyon sonucu bulunmuyor.", stale);

    private static TargetMetricEvaluation EvaluateRange(
        string metric,
        string label,
        int? frequency,
        double? value,
        double min,
        double max,
        string unit,
        double borderlineFraction,
        string missingText)
    {
        if (value is null)
            return new(metric, label, frequency, null, min, max, unit,
                TargetPerformanceStatus.VeriYok, missingText);

        var margin = Math.Max(Math.Abs(max - min) * borderlineFraction,
            Math.Max(Math.Abs(min), Math.Abs(max)) * 0.05);
        var status = value >= min && value <= max
            ? TargetPerformanceStatus.Uygun
            : value >= min - margin && value <= max + margin
                ? TargetPerformanceStatus.Sinirda
                : TargetPerformanceStatus.Iyilestirilmeli;
        var explanation = status switch
        {
            TargetPerformanceStatus.Uygun => $"Hedef {min:F2}–{max:F2} {unit} aralığında.",
            TargetPerformanceStatus.Sinirda => $"Hedef {min:F2}–{max:F2} {unit}; tolerans sınırında.",
            _ => value > max
                ? $"Hedef üst sınırından {value.Value - max:F2} {unit} yüksek."
                : $"Hedef alt sınırından {min - value.Value:F2} {unit} düşük."
        };
        return new(metric, label, frequency, value, min, max, unit, status, explanation);
    }

    private static TargetMetricEvaluation EvaluateMinimum(
        string metric,
        string label,
        int? frequency,
        double? value,
        double minimum,
        string unit,
        double borderlineWidth,
        string missingText)
    {
        if (value is null)
            return new(metric, label, frequency, null, minimum, null, unit,
                TargetPerformanceStatus.VeriYok, missingText);

        var status = value >= minimum
            ? TargetPerformanceStatus.Uygun
            : value >= minimum - borderlineWidth
                ? TargetPerformanceStatus.Sinirda
                : TargetPerformanceStatus.Iyilestirilmeli;
        var explanation = status switch
        {
            TargetPerformanceStatus.Uygun => $"Minimum {minimum:F2} {unit} hedefini karşılıyor.",
            TargetPerformanceStatus.Sinirda => $"Minimum {minimum:F2} {unit} hedefinin hemen altında.",
            _ => $"Minimum {minimum:F2} {unit} hedefinden {minimum - value.Value:F2} {unit} düşük."
        };
        return new(metric, label, frequency, value, minimum, null, unit, status, explanation);
    }

    private static (double Sti, double C50, (double Min, double Max) C80, (double Min, double Max) G)
        TargetsFor(RoomType roomType) => roomType switch
        {
            RoomType.ConferenceHall or RoomType.Classroom or RoomType.Theater or
                RoomType.HospitalRoom or RoomType.OfficeOpenPlan or RoomType.Library =>
                (0.60, 2.0, (-1.0, 4.0), (-2.0, 10.0)),
            RoomType.MusicHall or RoomType.RecordingStudio =>
                (0.45, -1.0, (-2.0, 2.0), (0.0, 10.0)),
            RoomType.Cinema =>
                (0.60, 2.0, (0.0, 5.0), (-2.0, 10.0)),
            RoomType.Restaurant or RoomType.SportsHall or RoomType.SwimmingPool =>
                (0.50, 0.0, (-1.0, 5.0), (-2.0, 12.0)),
            _ => (0.50, 0.0, (-2.0, 4.0), (-2.0, 10.0))
        };

    private static double? EstimateAdditionalAbsorption(double volume, double? currentRt, double targetRt)
    {
        if (!double.IsFinite(volume) || volume <= 0 || currentRt is null || currentRt <= 0 ||
            !double.IsFinite(targetRt) || targetRt <= 0)
            return null;

        var currentA = AcousticConstants.SabineConstant * volume / currentRt.Value;
        var targetA = AcousticConstants.SabineConstant * volume / targetRt;
        return Math.Max(0, targetA - currentA);
    }

    private static double? AverageMid(
        IEnumerable<AcousticParameters> parameters,
        Func<BandParameters, double> selector) =>
        Average(parameters.SelectMany(p => p.Bands)
            .Where(b => b.FrequencyHz is 500 or 1000)
            .Select(selector));

    private static double? Average(IEnumerable<double> values)
    {
        var finite = values.Where(double.IsFinite).ToArray();
        return finite.Length == 0 ? null : finite.Average();
    }

    private static string FormatFrequency(int value) => value >= 1000
        ? $"{value / 1000.0:G3} kHz"
        : $"{value} Hz";
}
