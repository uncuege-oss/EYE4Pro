using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kst.Acoustics.Core.Measurement.Analysis;
using Kst.Acoustics.Core.Measurement.Capture;
using Kst.Acoustics.Core.Measurement.Comparison;
using Kst.Acoustics.Core.Measurement.Data;
using Kst.Acoustics.Core.Measurement.Processing;
using Kst.Acoustics.Core.Measurement.Signals;
using Kst.Acoustics.Core.Measurement.Spatial;

namespace Kst.Acoustics.App.ViewModels;

public partial class MeasurementViewModel : ViewModelBase
{
    private readonly AudioPlaybackService _playbackService = new();
    private readonly AudioCaptureService _captureService = new();

    [ObservableProperty]
    private ObservableCollection<AudioDevice> _inputDevices = [];

    [ObservableProperty]
    private ObservableCollection<AudioDevice> _outputDevices = [];

    [ObservableProperty]
    private ObservableCollection<MeasurementItemViewModel> _measurements = [];

    [ObservableProperty]
    private MeasurementItemViewModel? _selectedMeasurement;

    [ObservableProperty]
    private ObservableCollection<AcousticParameterRowViewModel> _parameterRows = [];
    partial void OnSelectedMeasurementChanged(MeasurementItemViewModel? value)
    {
        ParameterRows.Clear();
        if (value?.Data.Parameters != null)
        {
            foreach (var kvp in value.Data.Parameters)
            {
                ParameterRows.Add(new AcousticParameterRowViewModel(kvp.Key, kvp.Value));
            }
        }
    }

    [ObservableProperty]
    private AudioDevice? _selectedInputDevice;

    [ObservableProperty]
    private AudioDevice? _selectedOutputDevice;

    [ObservableProperty]
    private string _statusMessage = "Hazır";

    [ObservableProperty]
    private string _statusColor = "#7E94A7";

    [ObservableProperty]
    private bool _isAudioCaptureSupported;

    [ObservableProperty]
    private bool _isDirty;

    public string PencereBasligi => IsDirty ? "Yerinde Ölçüm Modülü *" : "Yerinde Ölçüm Modülü";

    partial void OnIsDirtyChanged(bool value) => OnPropertyChanged(nameof(PencereBasligi));

    [ObservableProperty]
    private string _levelDisplayText = "-120.0 dBFS";

    [ObservableProperty]
    private double _levelMeterValue = -100;

    [ObservableProperty]
    private double _levelMeterMinimum = -100;

    [ObservableProperty]
    private double _levelMeterMaximum = 0;

    [ObservableProperty]
    private bool _isSplCalibrated;

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private bool _isRtaActive;

    [ObservableProperty]
    private double _sweepDuration = 5.0;

    [ObservableProperty]
    private double _latencyMs;

    [ObservableProperty]
    private string? _calibrationFilePath;

    [ObservableProperty]
    private double _referenceSpl = 94.0;

    [ObservableProperty]
    private float[] _realTimeLevels = [];

    [ObservableProperty]
    private double _currentSpl = -120;

    [ObservableProperty]
    private double _splOffset = 0;

    [ObservableProperty]
    private CalibrationProfile? _activeCalibration;

    [ObservableProperty]
    private FrequencyResponseData? _predictedResponse;

    [ObservableProperty]
    private List<BiquadFilter> _activeFilters = [];

    private readonly RtaAnalyzer _rtaAnalyzer = new();
    private readonly SplMeter _splMeter = new();

    public Action? RequestClose { get; set; }

    public MeasurementViewModel()
    {
        RefreshDevices();
        _captureService.DataAvailable += OnDataAvailable;
        Measurements.CollectionChanged += (_, _) => IsDirty = true;
        SetupPoints.CollectionChanged += (_, _) => IsDirty = true;
    }

    [RelayCommand]
    private void Close()
    {
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void RefreshDevices()
    {
        IsAudioCaptureSupported = AudioDeviceEnumerator.IsCaptureSupported;
        InputDevices = new ObservableCollection<AudioDevice>(AudioDeviceEnumerator.GetInputDevices());
        OutputDevices = new ObservableCollection<AudioDevice>(AudioDeviceEnumerator.GetOutputDevices());

        SelectedInputDevice = InputDevices.FirstOrDefault();
        SelectedOutputDevice = OutputDevices.FirstOrDefault();

        if (!IsAudioCaptureSupported)
        {
            StatusMessage = "Ses kaydı bu platformda desteklenmiyor (Windows WASAPI ve macOS CoreAudio destekleniyor).";
            StatusColor = "#EAB308";
        }
    }

    [RelayCommand]
    private void ToggleRta()
    {
        if (IsRtaActive)
        {
            _captureService.StopCapture();
            IsRtaActive = false;
            StatusMessage = "Canlı RTA durduruldu.";
            StatusColor = "#7E94A7";
        }
        else
        {
            try
            {
                if (!EnsureAudioReady())
                    return;

                _captureService.StartCapture(48000, SelectedInputDevice?.Id);
                IsRtaActive = true;
                StatusMessage = "Canlı RTA başladı.";
                StatusColor = "#22C55E";
            }
            catch (Exception ex)
            {
                IsRtaActive = false;
                StatusMessage = $"RTA başlatılamadı: {ex.Message}";
                StatusColor = "#EF4444";
            }
        }
    }

    private void OnDataAvailable(float[] samples)
    {
        if (IsRecording) return; // Ölçüm sırasında RTA'yı durdurmuyoruz ama önceliği ona vermiyoruz

        var levels = _rtaAnalyzer.Process(samples);
        var spl = _splMeter.Process(samples, 48000);

        Dispatcher.UIThread.Post(() =>
        {
            RealTimeLevels = levels;
            CurrentSpl = spl;
            LevelDisplayText = FormatLevel(spl);
            LevelMeterValue = FormatMeterValue(spl);
        });
    }

    [RelayCommand]
    private void CalibrateSpl()
    {
        if (!IsRtaActive)
        {
            StatusMessage = "SPL kalibrasyonu için önce Canlı RTA'yı başlatın (referans ton okunmalı).";
            StatusColor = "#EAB308";
            return;
        }

        if (CurrentSpl < -90)
        {
            StatusMessage = "SPL kalibrasyonu için yeterli referans seviye algılanmadı.";
            StatusColor = "#EF4444";
            return;
        }

        // 94 dB referans tonu (1kHz) geldiğini varsayıyoruz
        double rawDb = CurrentSpl - SplOffset; // Ham dBFS seviyesi
        SplOffset = ReferenceSpl - rawDb;
        _splMeter.SetCalibration(SplOffset);
        IsSplCalibrated = true;
        LevelMeterMinimum = 0;
        LevelMeterMaximum = 120;
        CurrentSpl = ReferenceSpl;
        LevelDisplayText = FormatLevel(CurrentSpl);
        LevelMeterValue = FormatMeterValue(CurrentSpl);
        StatusMessage = $"SPL Kalibre Edildi. Offset: {SplOffset:F1} dB";
        StatusColor = "#22C55E";
    }

    [RelayCommand]
    private async Task LoadMicCalibration()
    {
        var desktop = Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
        if (desktop?.MainWindow?.StorageProvider == null) return;

        var files = await desktop.MainWindow.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Mikrofon Kalibrasyon Dosyası Seç",
            FileTypeFilter = new[] 
            { 
                new Avalonia.Platform.Storage.FilePickerFileType("Kalibrasyon Dosyaları") { Patterns = new[] { "*.cal", "*.txt" } } 
            }
        });

        if (files.Count > 0)
        {
            string path = files[0].Path.LocalPath;
            if (File.Exists(path))
            {
                ActiveCalibration = CalibrationProfile.LoadFromFile(path);
                CalibrationFilePath = path;
                IsDirty = true;
                StatusMessage = $"Mikrofon kalibrasyonu yüklendi: {ActiveCalibration.MicrophoneName}";
            }
        }
    }

    [RelayCommand]
    private async Task MeasureLatency()
    {
        try
        {
            StatusMessage = "Gecikme ölçülüyor...";
            StatusColor = "#EAB308";
            var calibrator = new LoopbackLatencyCalibrator(_playbackService, _captureService);
            LatencyMs = await calibrator.MeasureLatencyMs(48000, SelectedInputDevice?.Id, SelectedOutputDevice?.Id);
            StatusMessage = $"Gecikme: {LatencyMs:F2} ms";
            StatusColor = "#22C55E";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Gecikme Hatası: {ex.Message}";
            StatusColor = "#EF4444";
        }
    }

    [RelayCommand]
    private void GenerateEq()
    {
        if (SelectedMeasurement?.Data.FrequencyResponse == null) return;
        
        var target = TargetCurveService.GetHarmanCurve();
        ActiveFilters = EqGenerator.GenerateFilters(SelectedMeasurement.Data.FrequencyResponse, target, 48000);
        
        // Tahmini yanıtı hesapla
        var fr = SelectedMeasurement.Data.FrequencyResponse;
        float[] newMags = new float[fr.Magnitudes.Length];
        for (int i = 0; i < fr.Magnitudes.Length; i++)
        {
            double eqGain = 0;
            foreach (var filter in ActiveFilters) eqGain += filter.GetResponse(fr.Frequencies[i], 48000);
            newMags[i] = (float)(fr.Magnitudes[i] + eqGain);
        }
        PredictedResponse = new FrequencyResponseData(fr.Frequencies, newMags, fr.Phases);
        StatusMessage = $"{ActiveFilters.Count} EQ filtresi üretildi.";
    }

    [RelayCommand]
    private async Task SaveSession()
    {
        try
        {
            var session = BuildCurrentSession();
            var path = await PickSavePathAsync("Ölçüm Oturumu Kaydet", "measurement_session.eye4meas.json", "EYE4 Measurement", "*.eye4meas.json");
            if (string.IsNullOrWhiteSpace(path))
                return;

            await MeasurementRepository.SaveSessionAsync(session, path);
            IsDirty = false;
            StatusMessage = $"Oturum kaydedildi: {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Hata: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LoadSession()
    {
        try
        {
            var desktop = Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            if (desktop?.MainWindow?.StorageProvider == null) return;

            var files = await desktop.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Ölçüm Oturumu Aç",
                FileTypeFilter = [new FilePickerFileType("EYE4 Measurement") { Patterns = ["*.eye4meas.json"] }]
            });

            if (files.Count == 0)
                return;

            var path = files[0].Path.LocalPath;
            var session = await MeasurementRepository.LoadSessionAsync(path);
            if (session == null)
            {
                StatusMessage = "Oturum okunamadı.";
                StatusColor = "#EF4444";
                return;
            }

            string[] colors = { "#4EA8DE", "#22C55E", "#EAB308", "#EF4444", "#A855F7" };
            Measurements.Clear();
            var index = 0;
            foreach (var measurement in session.Measurements)
            {
                Measurements.Add(new MeasurementItemViewModel(measurement, colors[index % colors.Length]));
                index++;
            }

            SelectedMeasurement = Measurements.FirstOrDefault();
            CalibrationFilePath = session.MicCalibrationFilePath;

            LatencyMs = session.LatencyMs;
            SplOffset = session.SplOffsetDb;
            IsSplCalibrated = session.IsSplCalibrated;
            _splMeter.SetCalibration(SplOffset);
            if (IsSplCalibrated)
            {
                LevelMeterMinimum = 0;
                LevelMeterMaximum = 120;
            }

            if (!string.IsNullOrWhiteSpace(session.MicCalibrationFilePath) && File.Exists(session.MicCalibrationFilePath))
            {
                try { ActiveCalibration = CalibrationProfile.LoadFromFile(session.MicCalibrationFilePath); }
                catch { /* kalibrasyon dosyası bozuksa sessizce atla, kullanıcı yeniden yükler */ }
            }

            // Spatial state'i geri yükle
            if (session.RoomDimensions.L > 0 && session.RoomDimensions.W > 0 && session.RoomDimensions.H > 0)
            {
                RoomLength = session.RoomDimensions.L;
                RoomWidth  = session.RoomDimensions.W;
                RoomHeight = session.RoomDimensions.H;
            }
            DxfRoomPath = session.DxfFilePath;
            SetupPoints = new ObservableCollection<SetupPoint>(session.SetupPoints);

            if (!string.IsNullOrWhiteSpace(session.DxfFilePath) && File.Exists(session.DxfFilePath))
            {
                try { RoomGeometry = await DxfRoomLoader.LoadAsync(session.DxfFilePath); }
                catch { RoomGeometry = RoomGeometry.FromBox(RoomLength, RoomWidth, RoomHeight); }
            }
            else if (RoomLength > 0 && RoomWidth > 0 && RoomHeight > 0)
            {
                RoomGeometry = RoomGeometry.FromBox(RoomLength, RoomWidth, RoomHeight);
            }

            IsDirty = false;
            StatusMessage = $"Oturum yüklendi: {Path.GetFileName(path)}";
            StatusColor = "#22C55E";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Oturum yükleme hatası: {ex.Message}";
            StatusColor = "#EF4444";
        }
    }

    [RelayCommand]
    private async Task ExportReportPdf()
    {
        if (Measurements.Count == 0)
        {
            StatusMessage = "Önce en az bir ölçüm alın.";
            StatusColor = "#EAB308";
            return;
        }

        try
        {
            var path = await PickSavePathAsync("PDF Raporu Kaydet", "yerinde_olcum_raporu.pdf", "PDF Belgesi", "*.pdf");
            if (string.IsNullOrWhiteSpace(path))
                return;

            var session = BuildCurrentSession();
            ComparisonReport? cmp = null;
            if (LoadedSimulation != null && SelectedMeasurement != null)
            {
                var tol = new ToleranceProfile(
                    RtSecondsOk: RtToleranceOk, RtSecondsWarn: RtToleranceWarn,
                    CDbOk: CToleranceOk, CDbWarn: CToleranceWarn);
                cmp = SimulationComparisonService.Compare(LoadedSimulation, SelectedMeasurement.Data, tol);
            }

            var bytes = MeasurementReportPdf.Build(session, cmp);
            await File.WriteAllBytesAsync(path, bytes);
            StatusMessage = $"PDF rapor kaydedildi: {Path.GetFileName(path)}";
            StatusColor = "#22C55E";
        }
        catch (Exception ex)
        {
            StatusMessage = $"PDF hatası: {ex.Message}";
            StatusColor = "#EF4444";
        }
    }

    private MeasurementSession BuildCurrentSession() => new()
    {
        Name = "Yerinde Ölçüm Oturumu",
        Measurements = Measurements.Select(m => m.Data).ToList(),
        InputDeviceName = SelectedInputDevice?.Name,
        OutputDeviceName = SelectedOutputDevice?.Name,
        MicCalibrationFilePath = CalibrationFilePath,
        LatencyMs = LatencyMs,
        SplOffsetDb = SplOffset,
        IsSplCalibrated = IsSplCalibrated,
        DxfFilePath = DxfRoomPath,
        RoomDimensions = (RoomLength, RoomWidth, RoomHeight),
        SetupPoints = SetupPoints.ToList(),
        SimulationResultPath = LoadedSimulation?.SourcePath,
    };

    [RelayCommand]
    private async Task ExportSelected()
    {
        if (SelectedMeasurement == null) return;
        try
        {
            var path = await PickSavePathAsync("Frekans Yanıtı CSV Kaydet", $"{SelectedMeasurement.Label}_FR.csv", "CSV", "*.csv");
            if (string.IsNullOrWhiteSpace(path))
                return;

            await ExportService.ExportToCsvAsync(SelectedMeasurement.Data, path);
            StatusMessage = $"Dışa aktarıldı: {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Hata: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task StartSweep()
    {
        if (IsRecording) return;

        if (!EnsureAudioReady())
            return;

        IsRecording = true;
        StatusMessage = "Sweep oynatılıyor ve kaydediliyor...";
        StatusColor = "#EAB308";

        string[] colors = { "#4EA8DE", "#22C55E", "#EAB308", "#EF4444", "#A855F7" };
        string color = colors[Measurements.Count % colors.Length];
        var measurementNumber = Measurements.Count + 1;

        var sweep = LogSweepGenerator.Generate(20, 20000, SweepDuration, 48000);

        try
        {
            if (IsRtaActive)
            {
                _captureService.StopCapture();
                IsRtaActive = false;
            }

            _captureService.StartCapture(48000, SelectedInputDevice?.Id);
            var playbackTask = _playbackService.PlayBufferAsync(sweep, 48000, SelectedOutputDevice?.Id);
            await playbackTask;
            await Task.Delay(500);

            var recorded = _captureService.StopCapture();

            if (recorded.Length > 0)
            {
                StatusMessage = "Analiz ediliyor...";

                await Task.Run(() =>
                {
                    var rawIr = ImpulseResponseExtractor.Extract(recorded, sweep);
                    rawIr = ApplyLatencyCompensation(rawIr, 48000, LatencyMs);

                    // Peak'e hizala, analiz için makul uzunluğa kırp, yalnız kuyruğa pencere uygula
                    // (direkt sesi silen eski tam-tampon Tukey penceresi kaldırıldı)
                    var ir = ImpulseResponseWindow.AlignAndWindow(rawIr, 48000);

                    var fr = FrequencyResponseAnalyzer.Analyze(ir, 48000, ActiveCalibration, SplOffset);

                    // Multi-band Akustik Parametre Analizi
                    int[] centerFreqs = { 63, 125, 250, 500, 1000, 2000, 4000, 8000 };
                    var paramDict = new Dictionary<string, double[]>();
                    paramDict["RT20"] = new double[8];
                    paramDict["RT30"] = new double[8];
                    paramDict["EDT"] = new double[8];
                    paramDict["C50"] = new double[8];
                    paramDict["C80"] = new double[8];
                    paramDict["D50"] = new double[8];
                    paramDict["Ts"] = new double[8];
                    // NaN ile başlatılır: STI/MTI hesabı başarısız olursa dizi varsayılan 0'da kalıp
                    // UI'da geçerli bir sonuçmuş gibi "0.00" göstermemeli, "—" göstermelidir.
                    paramDict["MTI"] = Enumerable.Repeat(double.NaN, 8).ToArray();
                    paramDict["STI"] = Enumerable.Repeat(double.NaN, 8).ToArray();

                    for (int i = 0; i < centerFreqs.Length; i++)
                    {
                        var filter = new OctaveBandFilter(centerFreqs[i], 48000);
                        var filteredIr = filter.Apply(ir);
                        var bandParams = AcousticParameterExtractor.Extract(filteredIr, 48000);

                        paramDict["RT20"][i] = bandParams.RT20;
                        paramDict["RT30"][i] = bandParams.RT30;
                        paramDict["EDT"][i] = bandParams.EDT;
                        paramDict["C50"][i] = bandParams.C50;
                        paramDict["C80"][i] = bandParams.C80;
                        paramDict["D50"][i] = bandParams.D50;
                        paramDict["Ts"][i] = bandParams.Ts;
                    }

                    // STI (IEC 60268-16) — IR'den 7 bant MTF üzerinden hesap
                    try
                    {
                        var stiResult = SpeechTransmissionIndex.Compute(ir, 48000);
                        // STI 8 bantta da aynı (genel oda skoru)
                        for (int i = 0; i < 8; i++) paramDict["STI"][i] = stiResult.Value;
                        // MTI 7 oktav bantta (125–8k); 63 Hz boş
                        paramDict["MTI"][0] = double.NaN;
                        for (int k = 0; k < stiResult.ModulationTransferIndex.Count && k + 1 < 8; k++)
                            paramDict["MTI"][k + 1] = stiResult.ModulationTransferIndex[k];
                    }
                    catch { /* STI hesabı başarısızsa sessizce atla */ }

                    SpectrogramData? spectrogram = null;
                    try { spectrogram = SpectrogramAnalyzer.Analyze(recorded, 48000, 2048, 0.5); }
                    catch { /* spektrogram opsiyonel */ }

                    var data = new MeasurementData
                    {
                        Label = $"Ölçüm {measurementNumber}",
                        SignalDurationMs = SweepDuration * 1000,
                        ImpulseResponse = ir,
                        StepResponse = StepResponseCalculator.Calculate(ir),
                        Waterfall = WaterfallAnalyzer.Analyze(ir, 48000),
                        Spectrogram = spectrogram,
                        FrequencyResponse = fr,
                        Parameters = paramDict
                    };

                    Dispatcher.UIThread.Post(() =>
                    {
                        var item = new MeasurementItemViewModel(data, color);
                        Measurements.Add(item);
                        SelectedMeasurement = item;
                        StatusMessage = "Ölçüm tamamlandı.";
                        StatusColor = "#22C55E";
                    });
                });
            }
            else
            {
                StatusMessage = "Hata: Veri alınamadı.";
                StatusColor = "#EF4444";
            }
        }
        catch (Exception ex)
        {
            _captureService.StopCapture();
            StatusMessage = $"Ölçüm hatası: {ex.Message}";
            StatusColor = "#EF4444";
        }
        finally
        {
            IsRecording = false;
        }
    }

    private static float[] ApplyLatencyCompensation(float[] ir, int sampleRate, double latencyMs)
    {
        var latencySamples = (int)Math.Round(latencyMs / 1000.0 * sampleRate);
        if (latencySamples <= 0 || latencySamples >= ir.Length)
            return ir;

        var shifted = new float[ir.Length];
        Array.Copy(ir, latencySamples, shifted, 0, ir.Length - latencySamples);
        return shifted;
    }

    private static async Task<string?> PickSavePathAsync(string title, string suggestedName, string typeName, string pattern)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow?.StorageProvider == null)
            return null;

        var file = await desktop.MainWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            FileTypeChoices = [new FilePickerFileType(typeName) { Patterns = [pattern] }]
        });

        return file?.Path.LocalPath;
    }

    private bool EnsureAudioReady()
    {
        if (!IsAudioCaptureSupported)
        {
            StatusMessage = "Ölçüm başlatılamadı: ses yakalama bu platformda desteklenmiyor.";
            StatusColor = "#EF4444";
            return false;
        }

        if (SelectedInputDevice == null)
        {
            StatusMessage = "Ölçüm başlatılamadı: giriş cihazı seçilmedi.";
            StatusColor = "#EF4444";
            return false;
        }

        return true;
    }

    private string FormatLevel(double value)
    {
        return IsSplCalibrated ? $"{value:F1} dB SPL" : $"{value:F1} dBFS";
    }

    private double FormatMeterValue(double value)
    {
        return IsSplCalibrated ? Math.Clamp(value, 0, 120) : Math.Clamp(value, -100, 0);
    }

    public void Dispose()
    {
        _playbackService.Dispose();
        _captureService.Dispose();
    }
}
