using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.IO;
using Kst.Acoustics.Core.Simulation;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.App.ViewModels;

public sealed partial class ProjectWindowViewModel
{
    // ── Çift seçimi (kaynak–alıcı) ────────────────────────────────────────────

    public IReadOnlyList<string> EchogramPairLabels
    {
        get => _echogramPairLabels;
        private set => SetProperty(ref _echogramPairLabels, value);
    }
    private IReadOnlyList<string> _echogramPairLabels = [];

    public int SelectedPairIndex
    {
        get => _selectedPairIndex;
        set
        {
            if (SetProperty(ref _selectedPairIndex, value))
            {
                RefreshResults();
                OnPropertyChanged(nameof(CanAuralizeSelectedPair));
                OnPropertyChanged(nameof(ImpulseResponseAvailabilityText));
            }
        }
    }
    private int _selectedPairIndex;

    // ── Bant seçimi ───────────────────────────────────────────────────────────

    public IReadOnlyList<string> BandLabels { get; } =
        ["63 Hz", "125 Hz", "250 Hz", "500 Hz", "1k Hz", "2k Hz", "4k Hz", "8k Hz"];

    public int SelectedBandIndex
    {
        get => _selectedBandIndex;
        set
        {
            if (SetProperty(ref _selectedBandIndex, value))
                RefreshPlotBand();
        }
    }
    private int _selectedBandIndex = 3; // 500 Hz (8-bant sisteminde index 3)

    // ── Parametreler tablosu ──────────────────────────────────────────────────

    public IReadOnlyList<ParameterRowViewModel> ParameterRows
    {
        get => _parameterRows;
        private set => SetProperty(ref _parameterRows, value);
    }
    private IReadOnlyList<ParameterRowViewModel> _parameterRows = [];

    // ── Çizim verisi ──────────────────────────────────────────────────────────

    public BandParameters? SelectedBandData
    {
        get => _selectedBandData;
        private set => SetProperty(ref _selectedBandData, value);
    }
    private BandParameters? _selectedBandData;

    public IReadOnlyList<EchogramArrival>? PlotArrivals
    {
        get => _plotArrivals;
        private set => SetProperty(ref _plotArrivals, value);
    }
    private IReadOnlyList<EchogramArrival>? _plotArrivals;

    public string ResultsSummaryText
    {
        get => _resultsSummaryText;
        private set => SetProperty(ref _resultsSummaryText, value);
    }
    private string _resultsSummaryText = "Simülasyon tamamlandıktan sonra sonuçlar burada görünür.";

    public string ReliabilitySummaryText
    {
        get => _reliabilitySummaryText;
        private set => SetProperty(ref _reliabilitySummaryText, value);
    }
    private string _reliabilitySummaryText = "Güvenilirlik analizi simülasyondan sonra görünür.";

    public string ReliabilityScoreText
    {
        get => _reliabilityScoreText;
        private set => SetProperty(ref _reliabilityScoreText, value);
    }
    private string _reliabilityScoreText = "—";

    public string ReliabilityLabelText
    {
        get => _reliabilityLabelText;
        private set => SetProperty(ref _reliabilityLabelText, value);
    }
    private string _reliabilityLabelText = string.Empty;

    public string ReliabilityColor
    {
        get => _reliabilityColor;
        private set => SetProperty(ref _reliabilityColor, value);
    }
    private string _reliabilityColor = "#60809A";

    public IReadOnlyList<string> ReliabilityWarnings
    {
        get => _reliabilityWarnings;
        private set => SetProperty(ref _reliabilityWarnings, value);
    }
    private IReadOnlyList<string> _reliabilityWarnings = [];

    public IReadOnlyList<string> QualityActionItems
    {
        get => _qualityActionItems;
        private set => SetProperty(ref _qualityActionItems, value);
    }
    private IReadOnlyList<string> _qualityActionItems = [];

    public bool HasReliabilityReport => ReliabilityWarnings.Count > 0 || ReliabilityScoreText != "—";

    // ── STI ───────────────────────────────────────────────────────────────────

    public string StiValueText
    {
        get => _stiValueText;
        private set => SetProperty(ref _stiValueText, value);
    }
    private string _stiValueText = "—";

    public string StiCategoryText
    {
        get => _stiCategoryText;
        private set => SetProperty(ref _stiCategoryText, value);
    }
    private string _stiCategoryText = string.Empty;

    public string GlobalIndicesText
    {
        get => _globalIndicesText;
        private set => SetProperty(ref _globalIndicesText, value);
    }
    private string _globalIndicesText = string.Empty;

    public bool HasStiResult
    {
        get => _hasStiResult;
        private set => SetProperty(ref _hasStiResult, value);
    }
    private bool _hasStiResult;

    // ── Izgara harita ─────────────────────────────────────────────────────────

    public GridMapResult? GridMapData
    {
        get => _gridMapData;
        private set
        {
            if (SetProperty(ref _gridMapData, value))
                OnPropertyChanged(nameof(HasGridMap));
        }
    }
    private GridMapResult? _gridMapData;

    public bool HasGridMap => _gridMapData is not null;

    public bool CanAuralizeSelectedPair =>
        !IsSimulationResultStale &&
        _lastResult is not null &&
        SelectedPairIndex >= 0 &&
        SelectedPairIndex < _lastResult.Echograms.Count &&
        _lastResult.Echograms[SelectedPairIndex].HasImpulseResponseData;

    public string ImpulseResponseAvailabilityText => CanAuralizeSelectedPair
        ? "Ham geliş verisi mevcut; IR dinlenebilir."
        : _lastResult is { ComputedParameters.Count: > 0 }
            ? "Kayıtlı kompakt sonuçta ham geliş verisi yok; parametreler görüntülenir ancak IR için simülasyonu yeniden çalıştırın."
            : "IR için geçerli bir simülasyon sonucu gerekli.";

    public IReadOnlyList<string> GridMapParameterOptions { get; } =
        ["RT60", "C80 (dB)", "C50 (dB)", "Tahmini STI"];

    public int SelectedGridParameterIndex
    {
        get => _selectedGridParamIdx;
        set
        {
            if (SetProperty(ref _selectedGridParamIdx, value))
                OnPropertyChanged(nameof(SelectedGridParameter));
        }
    }
    private int _selectedGridParamIdx;

    public GridMapParameter SelectedGridParameter => (GridMapParameter)_selectedGridParamIdx;

    // ── Auralizasyon ──────────────────────────────────────────────────────────

    [RelayCommand]
    private void ExportAuralization()
    {
        if (_lastResult == null || SelectedPairIndex < 0 || SelectedPairIndex >= _lastResult.Echograms.Count)
        {
            StatusMessage = "Dürtü yanıtı için önce simülasyon sonucu seçin.";
            return;
        }

        var echogram = _lastResult.Echograms[SelectedPairIndex];
        if (IsSimulationResultStale)
        {
            StatusMessage = "IR üretilemedi: sonuç girdilere göre eski. Simülasyonu yeniden çalıştırın.";
            return;
        }
        if (!echogram.HasImpulseResponseData)
        {
            StatusMessage = "Kayıtlı sonuç ham gelişleri içermiyor. IR için simülasyonu yeniden çalıştırın.";
            return;
        }
        var tempPath = Path.Combine(Path.GetTempPath(), $"RIR_{SanitizeAudioFilePart(echogram.SourceLabel)}_{SanitizeAudioFilePart(echogram.ReceiverLabel)}.wav");

        try
        {
            AuralizationService.GenerateImpulseResponseWav(echogram, tempPath);

            if (OperatingSystem.IsMacOS())
                Process.Start(new ProcessStartInfo("afplay", $"\"{tempPath}\"") { UseShellExecute = false });
            else if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
            StatusMessage = $"Dürtü yanıtı kaydedildi: {tempPath}";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Auralization error: {ex.Message}");
            StatusMessage = $"Dürtü yanıtı oynatılamadı: {ex.Message}";
        }
    }

    public string? CustomAudioPath
    {
        get => _customAudioPath;
        set
        {
            if (SetProperty(ref _customAudioPath, value))
                OnPropertyChanged(nameof(HasCustomAudio));
        }
    }
    private string? _customAudioPath;

    public bool HasCustomAudio => !string.IsNullOrEmpty(CustomAudioPath);

    [RelayCommand]
    private void PlayOriginalAudio()
    {
        if (string.IsNullOrEmpty(CustomAudioPath))
        {
            StatusMessage = "Ham ses için önce WAV dosyası seçin.";
            return;
        }

        try
        {
            if (OperatingSystem.IsMacOS())
                Process.Start(new ProcessStartInfo("afplay", $"\"{CustomAudioPath}\"") { UseShellExecute = false });
            else if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo(CustomAudioPath) { UseShellExecute = true });
            else
                StatusMessage = "Ham ses oynatma bu platformda desteklenmiyor.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ham ses oynatılamadı: {ex.Message}";
        }
    }

    [RelayCommand]
    public void AuralizeCustomAudio()
    {
        var path = CustomAudioPath;
        if (string.IsNullOrEmpty(path))
        {
            StatusMessage = "Oda etkisi için önce WAV dosyası seçin.";
            return;
        }
        if (_lastResult == null || SelectedPairIndex < 0 || SelectedPairIndex >= _lastResult.Echograms.Count)
        {
            StatusMessage = "Oda etkisi için önce simülasyon sonucu seçin.";
            return;
        }

        var echogram = _lastResult.Echograms[SelectedPairIndex];
        if (IsSimulationResultStale || !echogram.HasImpulseResponseData)
        {
            StatusMessage = IsSimulationResultStale
                ? "Oda etkisi uygulanamadı: simülasyon sonucu eski."
                : "Kayıtlı sonuç ham gelişleri içermiyor; simülasyonu yeniden çalıştırın.";
            return;
        }
        var outPath  = Path.Combine(Path.GetTempPath(), $"Auralization_Result.wav");

        try
        {
            AuralizationService.ConvolveSourceWithEchogram(path, echogram, outPath);

            if (!File.Exists(outPath) || new FileInfo(outPath).Length < 44)
            {
                StatusMessage = "Oda etkili ses üretilemedi: çıktı dosyası oluşmadı.";
                return;
            }

            if (OperatingSystem.IsMacOS())
            {
                Process.Start(new ProcessStartInfo("afplay", $"\"{outPath}\"") { UseShellExecute = false });
                StatusMessage = $"Oda etkili ses çalıyor: {outPath}";
            }
            else if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo(outPath) { UseShellExecute = true });
                StatusMessage = $"Oda etkili ses çalıyor: {outPath}";
            }
            else
            {
                StatusMessage = $"Oda etkili ses kaydedildi: {outPath}";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Auralization custom error: {ex.Message}");
            StatusMessage = $"Oda etkili ses üretilemedi: {ex.Message}";
        }
    }

    // ── Simülasyon bitince çağrılır ───────────────────────────────────────────

    internal void OnSimulationCompleted(SimulationResult result)
    {
        var isStoredProjectResult = ReferenceEquals(result, _project.LastResult);

        // Constructor tarafından geri yüklenen sonucu, dosyanın bugünkü girdileriyle
        // yeniden parmak izleyerek doğrula. Elle düzenlenmiş/eksik bir proje veya
        // sonradan değiştirilmiş materyal/hoparlör kütüphanesi eski sonucu
        // yanlışlıkla güncel gösteremez. Kopya almak ayrıca alt pencerenin stale
        // işaretini parent MainWindow'un baseline nesnesine sızdırmasını engeller.
        if (isStoredProjectResult)
        {
            result = CloneSimulationResult(result);
            if (string.IsNullOrWhiteSpace(result.InputFingerprint))
            {
                result.IsStale = true;
                result.StaleReason ??= "Kayıtlı sonuçta girdi izi yok; güncelliği doğrulanamadı.";
            }
            else
            {
                var currentParameters = _project.SimulationSettings ?? result.Parameters;
                var currentFingerprint = SimulationInputFingerprint.Compute(_project, currentParameters);
                if (!string.Equals(currentFingerprint, result.InputFingerprint, StringComparison.Ordinal))
                {
                    result.IsStale = true;
                    result.StaleReason ??= "Kayıtlı sonuç ile proje girdilerinin izi eşleşmiyor.";
                }
            }
        }

        LastResult = result;
        IsSimulationResultStale = result.IsStale;
        var labels = result.Echograms
            .Select(e => $"{e.SourceLabel} → {e.ReceiverLabel}")
            .ToArray();

        EchogramPairLabels  = labels;
        SelectedPairIndex   = 0;
        ResultsSummaryText  = $"{result.TotalArrivals:N0} geliş — {result.Elapsed.TotalSeconds:F1} sn";
        GridMapData         = result.GridMap;
        OnPropertyChanged(nameof(ImpulseResponseAvailabilityText));
        OnPropertyChanged(nameof(SimulationResultStateText));
        RefreshReliability(result.Reliability);
        RefreshResults();
        GenerateReport();
        RefreshTargetPerformance();
        RefreshScenarioComparisons();
    }

    /// <summary>
    /// Sonuç modelinde yalnız stale alanları mutable'dır. Alt proje penceresinin bu
    /// alanları parent pencerenin kaydedilmiş baseline'ında yer alan aynı referans
    /// üzerinde değiştirmemesi için sığ ama bağımsız bir sonuç kabuğu üretir.
    /// İç koleksiyonlar simülasyon tamamlandıktan sonra salt-okunur kullanılır.
    /// </summary>
    private static SimulationResult CloneSimulationResult(SimulationResult source) => new()
    {
        Echograms = source.Echograms,
        Parameters = source.Parameters,
        Elapsed = source.Elapsed,
        ComputedAt = source.ComputedAt,
        GridMap = source.GridMap,
        Reliability = source.Reliability,
        ComputedParameters = source.ComputedParameters,
        InputFingerprint = source.InputFingerprint,
        IsStale = source.IsStale,
        StaleReason = source.StaleReason
    };

    private void ClearSimulationResults(string summary)
    {
        LastResult = null;
        IsSimulationResultStale = false;
        EchogramPairLabels = [];
        SelectedPairIndex = 0;
        ParameterRows = [];
        SelectedBandData = null;
        PlotArrivals = null;
        ResultsSummaryText = summary;
        RefreshReliability(null);
        StiValueText = "—";
        StiCategoryText = string.Empty;
        GlobalIndicesText = string.Empty;
        HasStiResult = false;
        GridMapData = null;
        TargetRtText = "Hedef RT60: —";
        OnPropertyChanged(nameof(CanAuralizeSelectedPair));
        OnPropertyChanged(nameof(ImpulseResponseAvailabilityText));
        RefreshTargetPerformance();
        RefreshScenarioComparisons();
    }

    private void RefreshReliability(SimulationReliabilityReport? reliability)
    {
        if (reliability is null)
        {
            ReliabilitySummaryText = "Güvenilirlik analizi yok.";
            ReliabilityScoreText = "—";
            ReliabilityLabelText = string.Empty;
            ReliabilityColor = "#60809A";
            ReliabilityWarnings = [];
            QualityActionItems = [];
            OnPropertyChanged(nameof(HasReliabilityReport));
            return;
        }

        ReliabilitySummaryText = reliability.Summary;
        ReliabilityScoreText = $"{reliability.ConfidenceScore:F0}/100";
        ReliabilityLabelText = reliability.ConfidenceLabel;
        ReliabilityColor = reliability.ConfidenceScore switch
        {
            >= 85 => "#40EFA0",
            >= 70 => "#B7E06A",
            >= 55 => "#E0C85A",
            >= 40 => "#E08A50",
            _ => "#FF6060"
        };
        ReliabilityWarnings = reliability.Warnings.Take(4).ToArray();
        QualityActionItems = reliability.Recommendations
            .Concat([MaterialAuditText, ConvergenceSummaryText])
            .Where(s => !string.IsNullOrWhiteSpace(s) && !s.Contains("henüz", StringComparison.OrdinalIgnoreCase))
            .Take(6)
            .ToArray();
        OnPropertyChanged(nameof(HasReliabilityReport));
    }

    // ── Yenile ────────────────────────────────────────────────────────────────

    public string TargetRtText
    {
        get => _targetRtText;
        private set => SetProperty(ref _targetRtText, value);
    }
    private string _targetRtText = "Hedef RT60: —";

    private void RefreshResults()
    {
        var result = LastResult;
        if (result is null || result.Echograms.Count == 0) return;

        var idx      = Math.Clamp(SelectedPairIndex, 0, result.Echograms.Count - 1);
        var echogram = result.Echograms[idx];

        PlotArrivals = echogram.HasImpulseResponseData ? echogram.Arrivals : [];

        var ap = result.ComputedParameters != null && result.ComputedParameters.Count > idx
            ? result.ComputedParameters[idx]
            : AcousticParameterCalculator.Compute(echogram, result.Parameters);

        var criteria = RoomTypeRegistry.All.FirstOrDefault(c => c.Type == CurrentRoomType) ?? RoomTypeRegistry.All[0];
        var (min, max) = criteria.GetTargetRtMid(RoomVolume);
        TargetRtText = $"Hedef RT60 (Mid): {min:F2} - {max:F2} s ({criteria.DisplayName})";

        var fs = ap.SchroederFrequency(RoomVolume);
        ParameterRows = BuildParameterRows(ap, min, max, result.Reliability, fs);
        var fsText = double.IsNaN(fs) ? "—" : $"{fs:F0} Hz";
        var iaccText = double.IsNaN(ap.IACC) ? "—" : ap.IACC.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        GlobalIndicesText = $"BR: {ap.BR:F2}  |  TR: {ap.TR:F2}  |  IACC: {iaccText}  |  fₛ: {fsText}";
        RefreshPlotBand(ap);
        RefreshSti(ap);

        var rt = ap.RT60_Mid;
        var totalArrivals = result.ComputedParameters != null && echogram.Arrivals.Count == 0 ? "(Sonuçlardan yüklendi)" : $"{echogram.Arrivals.Count:N0} geliş";
        ResultsSummaryText = double.IsNaN(rt)
            ? $"{totalArrivals} — RT60 hesaplanamadı"
            : $"{totalArrivals} — RT60 (mid) = {rt:F2} s";
    }

    public string StiReliabilityText
    {
        get => _stiReliabilityText;
        private set => SetProperty(ref _stiReliabilityText, value);
    }
    private string _stiReliabilityText = "Güven: —";

    public string StiReliabilityColor
    {
        get => _stiReliabilityColor;
        private set => SetProperty(ref _stiReliabilityColor, value);
    }
    private string _stiReliabilityColor = "#60809A";

    private void RefreshSti(AcousticParameters ap)
    {
        var sti = ap.STI;
        if (double.IsNaN(sti))
        {
            StiValueText    = "—";
            StiCategoryText = "Hesaplanamadı";
            StiReliabilityText = "Güven: Düşük";
            StiReliabilityColor = "#FF6060";
            HasStiResult    = false;
            return;
        }

        StiValueText = sti.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        StiCategoryText = sti switch
        {
            >= 0.75 => "Tahmini: Mükemmel",
            >= 0.60 => "Tahmini: İyi",
            >= 0.45 => "Tahmini: Orta",
            >= 0.30 => "Tahmini: Zayıf",
            _       => "Tahmini: Kötü"
        };
        var stiReliability = EstimateParameterReliability(
            "STI", ap.Bands, b => sti, LastResult?.Reliability, ap.SchroederFrequency(RoomVolume));
        StiReliabilityText = $"Güven: {stiReliability.Label}";
        StiReliabilityColor = stiReliability.Color;
        HasStiResult = true;
    }

    private static string SanitizeAudioFilePart(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "Secim" : cleaned;
    }

    private void RefreshPlotBand(AcousticParameters? ap = null)
    {
        if (ap is null)
        {
            var result = LastResult;
            if (result is null || result.Echograms.Count == 0) return;
            var idx = Math.Clamp(SelectedPairIndex, 0, result.Echograms.Count - 1);
            ap = result.ComputedParameters != null && result.ComputedParameters.Count > idx
                ? result.ComputedParameters[idx]
                : AcousticParameterCalculator.Compute(result.Echograms[idx], result.Parameters);
        }

        var bi = Math.Clamp(SelectedBandIndex, 0, ap.Bands.Count - 1);
        SelectedBandData = ap.Bands[bi];
    }

    private static IReadOnlyList<ParameterRowViewModel> BuildParameterRows(
        AcousticParameters ap,
        double minRt,
        double maxRt,
        SimulationReliabilityReport? reliability,
        double schroederFrequency)
    {
        return
        [
            BuildRow("RT60",  "s",  ap.Bands, b => b.RT60, "{0:F2}", "RT", reliability, schroederFrequency, v => v >= minRt && v <= maxRt),
            BuildRow("T20",   "s",  ap.Bands, b => b.T20,  "{0:F2}", "RT", reliability, schroederFrequency),
            BuildRow("T30",   "s",  ap.Bands, b => b.T30,  "{0:F2}", "RT", reliability, schroederFrequency),
            BuildRow("EDT",   "s",  ap.Bands, b => b.EDT,  "{0:F2}", "EDT", reliability, schroederFrequency),
            BuildRow("Ts",    "ms", ap.Bands, b => b.Ts,   "{0:F1}", "ENERJI", reliability, schroederFrequency),
            BuildRow("G",     "dB", ap.Bands, b => b.G,    "{0:+0.0;-0.0}", "ENERJI", reliability, schroederFrequency),
            BuildRow("C50",   "dB", ap.Bands, b => b.C50,  "{0:+0.0;-0.0}", "ERKEN", reliability, schroederFrequency),
            BuildRow("C80",   "dB", ap.Bands, b => b.C80,  "{0:+0.0;-0.0}", "ERKEN", reliability, schroederFrequency),
            BuildRow("D50",   "",   ap.Bands, b => b.D50,  "{0:P0}", "ERKEN", reliability, schroederFrequency),
            BuildRow("LF",    "",   ap.Bands, b => b.LF,   "{0:F2}", "LATERAL", reliability, schroederFrequency),
            BuildRow("LFC",   "",   ap.Bands, b => b.LFC,  "{0:F2}", "LATERAL", reliability, schroederFrequency),
            BuildRow("STear", "dB", ap.Bands, b => b.STearly, "{0:+0.0;-0.0}", "SUPPORT", reliability, schroederFrequency),
            BuildRow("STlat", "dB", ap.Bands, b => b.STlate,  "{0:+0.0;-0.0}", "SUPPORT", reliability, schroederFrequency),
        ];
    }

    private static ParameterRowViewModel BuildRow(
        string name,
        string unit,
        IReadOnlyList<BandParameters> bands,
        Func<BandParameters, double> selector,
        string format,
        string kind,
        SimulationReliabilityReport? reliability,
        double schroederFrequency,
        Func<double, bool>? validator = null)
    {
        var pr = EstimateParameterReliability(kind, bands, selector, reliability, schroederFrequency);
        return new ParameterRowViewModel(name, unit, bands, selector, format, validator, pr.Label, pr.Color, pr.Reason, schroederFrequency);
    }

    private static (string Label, string Color, string Reason) EstimateParameterReliability(
        string kind,
        IReadOnlyList<BandParameters> bands,
        Func<BandParameters, double> selector,
        SimulationReliabilityReport? reliability,
        double schroederFrequency)
    {
        var score = reliability?.ConfidenceScore ?? 55;
        var values = bands.Select(selector).ToArray();
        var finiteRatio = values.Count(double.IsFinite) / (double)Math.Max(1, values.Length);
        score -= (1.0 - finiteRatio) * 35.0;

        if (double.IsFinite(schroederFrequency))
        {
            var lowBandRatio = bands.Count(b => b.FrequencyHz < schroederFrequency) / (double)Math.Max(1, bands.Count);
            score -= lowBandRatio * 12.0;
        }

        if (kind is "ERKEN" or "ENERJI")
            score -= 8;
        else if (kind is "LATERAL")
            score -= 18;
        else if (kind is "SUPPORT")
            score -= 12;
        else if (kind is "STI")
            score -= 15;
        else if (kind is "EDT")
            score -= 5;

        if (reliability?.Metrics.TryGetValue("Geliş / çift", out var arrivals) == true)
        {
            if (arrivals < 200) score -= 18;
            else if (arrivals < 1000) score -= 8;
        }

        score = Math.Clamp(score, 0, 100);
        var label = score switch
        {
            >= 85 => ("Yüksek", "#40EFA0"),
            >= 70 => ("Orta+", "#B7E06A"),
            >= 55 => ("Orta", "#E0C85A"),
            >= 40 => ("Düşük+", "#E08A50"),
            _ => ("Düşük", "#FF6060")
        };
        return (label.Item1, label.Item2, BuildReliabilityReason(kind, score, finiteRatio, schroederFrequency, reliability));
    }

    private static string BuildReliabilityReason(
        string kind,
        double score,
        double finiteRatio,
        double schroederFrequency,
        SimulationReliabilityReport? reliability)
    {
        var parts = new List<string>
        {
            $"Puan: {score:F0}/100",
            $"Hesaplanabilir bant: {finiteRatio:P0}"
        };
        if (double.IsFinite(schroederFrequency))
            parts.Add($"Schroeder frekansı: {schroederFrequency:F0} Hz");
        if (kind is "LATERAL")
            parts.Add("Lateral parametreler kaynak/alıcı yönüne ve yan yansımalara duyarlıdır.");
        if (kind is "ERKEN" or "ENERJI" or "SUPPORT")
            parts.Add("Erken enerji metrikleri ışın sayısı ve saçılım verisine duyarlıdır.");
        if (kind is "STI")
            parts.Add("STI ekogram, kaynak seviyesi, gürültü ve hoparlör yönelimi verilerine duyarlıdır.");
        if (reliability?.Metrics.TryGetValue("Geliş / çift", out var arrivals) == true)
            parts.Add($"Geliş/çift: {arrivals:N0}");
        return string.Join(" | ", parts);
    }
}

// ─── Tablo satırı ─────────────────────────────────────────────────────────────

public sealed class ParameterRowViewModel(
    string name,
    string unit,
    IReadOnlyList<BandParameters> bands,
    Func<BandParameters, double> selector,
    string format,
    Func<double, bool>? validator = null,
    string reliabilityLabel = "—",
    string reliabilityColor = "#60809A",
    string reliabilityReason = "",
    double schroederFrequency = double.NaN)
{
    public string Name { get; } = unit.Length > 0 ? $"{name} ({unit})" : name;
    public string ReliabilityLabel { get; } = reliabilityLabel;
    public string ReliabilityColor { get; } = reliabilityColor;
    public string ReliabilityReason { get; } = reliabilityReason;

    private string Fmt(double v) =>
        double.IsNaN(v) ? "—" : string.Format(System.Globalization.CultureInfo.InvariantCulture, format, v);

    public string V63Hz  => Fmt(selector(bands[0]));
    public string V125Hz => Fmt(selector(bands[1]));
    public string V250Hz => Fmt(selector(bands[2]));
    public string V500Hz => Fmt(selector(bands[3]));
    public string V1kHz  => Fmt(selector(bands[4]));
    public string V2kHz  => Fmt(selector(bands[5]));
    public string V4kHz  => Fmt(selector(bands[6]));
    public string V8kHz  => Fmt(selector(bands[7]));

    private static string OkBrush  => "#A0C8A0";
    private static string ErrBrush => "#FF5050";

    private string CellBrush(int index, string okBrush = "#A0C8A0")
    {
        var value = selector(bands[index]);
        if (validator?.Invoke(value) == false) return ErrBrush;
        if (double.IsFinite(schroederFrequency) && bands[index].FrequencyHz < schroederFrequency)
            return "#E0C85A";
        return okBrush;
    }

    public string V63Brush  => CellBrush(0);
    public string V125Brush => CellBrush(1);
    public string V250Brush => CellBrush(2);
    public string V500Brush => CellBrush(3, validator is null ? OkBrush : "#E0E080");
    public string V1kBrush  => CellBrush(4, validator is null ? OkBrush : "#E0E080");
    public string V2kBrush  => CellBrush(5);
    public string V4kBrush  => CellBrush(6);
    public string V8kBrush  => CellBrush(7);

    // Ortalama: 63 Hz (idx 0) ve 8 kHz (idx 7) hariç — ISO 3382-1 pratik uygulama
    public string VAverage
    {
        get
        {
            var valid = bands.Skip(1).Take(6).Select(selector).Where(v => !double.IsNaN(v)).ToList();
            return valid.Count > 0 ? Fmt(valid.Average()) : "—";
        }
    }

    public string VAvgBrush
    {
        get
        {
            var valid = bands.Skip(1).Take(6).Select(selector).Where(v => !double.IsNaN(v)).ToList();
            return (valid.Count == 0 || validator?.Invoke(valid.Average()) != false) ? "#85B5DF" : ErrBrush;
        }
    }
}
