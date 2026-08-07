using System;
using System.Collections.Generic;
using System.Linq;

namespace Kst.Acoustics.Core.Measurement.Comparison;

public enum ComparisonStatus
{
    Ok,
    Warn,
    Critical,
    NoMeasurement,
    NoSimulation,
}

public sealed record ParameterComparison(
    string Parameter,
    int FrequencyHz,
    double Measured,
    double Simulated,
    double Delta,
    ComparisonStatus Status);

public sealed record SimulationComparisonInput(
    string ProjectName,
    string SourcePath,
    DateTime SimulatedAtUtc,
    IReadOnlyDictionary<string, IReadOnlyList<(int FreqHz, double Value)>> Parameters,
    int EchogramCount,
    double RoomVolumeM3,
    bool RecoveredFromBackup = false,
    IReadOnlyList<string>? SessionWarnings = null)
{
    /// <summary>
    /// Yalnız bu yükleme oturumuna ait uyarılar. Proje verisinin parçası değildir ve
    /// karşılaştırma sonucu yeniden kaydedilirse kalıcılaştırılmamalıdır.
    /// </summary>
    public IReadOnlyList<string> SessionWarnings { get; init; } =
        SessionWarnings?.Where(w => !string.IsNullOrWhiteSpace(w)).ToArray() ?? [];
}

public sealed record ComparisonReport(
    SimulationComparisonInput Simulation,
    string MeasurementLabel,
    IReadOnlyList<ParameterComparison> Rows,
    ToleranceProfile Tolerances);

public sealed record ToleranceProfile(
    double RtSecondsOk = 0.10,
    double RtSecondsWarn = 0.20,
    double CDbOk = 1.0,
    double CDbWarn = 2.0,
    double D50Ok = 0.10,
    double D50Warn = 0.20,
    double TsMsOk = 10.0,
    double TsMsWarn = 20.0,
    double GDbOk = 1.0,
    double GDbWarn = 2.0)
{
    public static ToleranceProfile Default { get; } = new();
}
