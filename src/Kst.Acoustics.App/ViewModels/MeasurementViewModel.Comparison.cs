using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kst.Acoustics.Core.Measurement.Comparison;

namespace Kst.Acoustics.App.ViewModels;

public partial class MeasurementViewModel
{
    [ObservableProperty] private SimulationComparisonInput? _loadedSimulation;
    [ObservableProperty] private string? _simulationLabel;
    [ObservableProperty] private double _simulationRoomVolume;
    [ObservableProperty] private int _simulationEchogramCount;

    [ObservableProperty] private ObservableCollection<ComparisonRowViewModel> _comparisonRows = [];
    [ObservableProperty] private bool _isComparisonReady;

    [ObservableProperty] private double _rtToleranceOk = 0.10;     // s
    [ObservableProperty] private double _rtToleranceWarn = 0.20;
    [ObservableProperty] private double _cToleranceOk = 1.0;       // dB
    [ObservableProperty] private double _cToleranceWarn = 2.0;

    [RelayCommand]
    private async Task LoadSimulation()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow?.StorageProvider == null) return;

        var files = await desktop.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Hacim Akustiği Projesi Aç (M01)",
            FileTypeFilter =
            [
                new FilePickerFileType("EYE4 Akustik Proje") { Patterns = ["*.e4p01", "*.eye4proj.json"] },
                new FilePickerFileType("JSON") { Patterns = ["*.json"] }
            ]
        });
        if (files.Count == 0) return;

        try
        {
            var sim = await SimulationComparisonService.LoadFromProjectFileAsync(files[0].Path.LocalPath);
            LoadedSimulation = sim;
            SimulationLabel = sim.RecoveredFromBackup
                ? $"⚠ {sim.ProjectName}  ·  {sim.EchogramCount} ekogram  ·  yedekten kurtarıldı"
                : $"{sim.ProjectName}  ·  {sim.EchogramCount} ekogram";
            SimulationRoomVolume = sim.RoomVolumeM3;
            SimulationEchogramCount = sim.EchogramCount;
            RunComparison();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Simülasyon yüklenemedi: {ex.Message}";
            StatusColor = "#EF4444";
        }
    }

    [RelayCommand]
    private void RunComparison()
    {
        ComparisonRows.Clear();
        IsComparisonReady = false;

        var sim = LoadedSimulation;
        var meas = SelectedMeasurement?.Data;
        if (sim == null || meas == null) return;

        var tol = new ToleranceProfile(
            RtSecondsOk: RtToleranceOk,
            RtSecondsWarn: RtToleranceWarn,
            CDbOk: CToleranceOk,
            CDbWarn: CToleranceWarn);

        var report = SimulationComparisonService.Compare(sim, meas, tol);
        foreach (var row in report.Rows)
            ComparisonRows.Add(new ComparisonRowViewModel(row));
        IsComparisonReady = true;
        if (sim.RecoveredFromBackup)
        {
            var warning = sim.SessionWarnings.FirstOrDefault()
                ?? "Ana proje okunamadı; son sağlam yedek kullanıldı.";
            StatusMessage = $"Karşılaştırma hazır: {report.Rows.Count} satır · ⚠ {warning}";
            StatusColor = "#F59E0B";
        }
        else
        {
            StatusMessage = $"Karşılaştırma hazır: {report.Rows.Count} satır";
            StatusColor = "#22C55E";
        }
    }
}

public sealed class ComparisonRowViewModel
{
    public string Parameter { get; }
    public int FrequencyHz { get; }
    public string FrequencyLabel { get; }
    public string Measured { get; }
    public string Simulated { get; }
    public string Delta { get; }
    public string StatusColor { get; }
    public string StatusLabel { get; }

    public ComparisonRowViewModel(ParameterComparison row)
    {
        Parameter = row.Parameter;
        FrequencyHz = row.FrequencyHz;
        FrequencyLabel = row.FrequencyHz >= 1000 ? $"{row.FrequencyHz / 1000.0:0.#} kHz" : $"{row.FrequencyHz} Hz";

        var unit = UnitFor(row.Parameter);
        Measured = double.IsNaN(row.Measured) ? "—" : $"{row.Measured:F2}{unit}";
        Simulated = double.IsNaN(row.Simulated) ? "—" : $"{row.Simulated:F2}{unit}";
        Delta = double.IsNaN(row.Delta) ? "—" : $"{(row.Delta >= 0 ? "+" : "")}{row.Delta:F2}{unit}";

        (StatusColor, StatusLabel) = row.Status switch
        {
            ComparisonStatus.Ok            => ("#22C55E", "✓"),
            ComparisonStatus.Warn          => ("#EAB308", "!"),
            ComparisonStatus.Critical      => ("#EF4444", "✗"),
            ComparisonStatus.NoMeasurement => ("#536879", "—"),
            ComparisonStatus.NoSimulation  => ("#536879", "?"),
            _                              => ("#536879", "—"),
        };
    }

    private static string UnitFor(string param) => param switch
    {
        "RT60" or "RT30" or "RT20" or "EDT" => " s",
        "C50" or "C80" or "G"               => " dB",
        "Ts"                                => " ms",
        _                                   => "",
    };
}
