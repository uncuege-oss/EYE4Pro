using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kst.Acoustics.Core.Measurement.Analysis;
using Kst.Acoustics.Core.Measurement.Data;
using Kst.Acoustics.Core.Measurement.Signals;

namespace Kst.Acoustics.App.ViewModels;

public partial class MeasurementViewModel
{
    [ObservableProperty] private double _thdToneHz = 1000;
    [ObservableProperty] private double _thdTestDurationSec = 3.0;

    [ObservableProperty] private double _imdTone1Hz = 250;
    [ObservableProperty] private double _imdTone2Hz = 8000;
    [ObservableProperty] private double _imdTestDurationSec = 3.0;

    /// <summary>Tek sabit ton ile THD ölçümü. Tonu çal, kayıt al, harmonikleri analiz et.</summary>
    [RelayCommand]
    private async Task StartThdTest()
    {
        if (IsRecording) return;
        if (!EnsureAudioReady()) return;

        IsRecording = true;
        StatusMessage = "THD testi oynatılıyor...";
        StatusColor = "#EAB308";

        try
        {
            var tone = NoiseGenerator.Multitone([(int)Math.Round(ThdToneHz)], ThdTestDurationSec, 48000, amplitude: 0.4f);

            if (IsRtaActive)
            {
                _captureService.StopCapture();
                IsRtaActive = false;
            }

            _captureService.StartCapture(48000, SelectedInputDevice?.Id);
            await _playbackService.PlayBufferAsync(tone, 48000, SelectedOutputDevice?.Id);
            await Task.Delay(400);
            var recorded = _captureService.StopCapture();

            if (recorded.Length < 4096)
            {
                StatusMessage = "THD: Yeterli veri alınamadı.";
                StatusColor = "#EF4444";
                return;
            }

            await Task.Run(() =>
            {
                var thd = DistortionAnalyzer.Analyze(recorded, 48000, ThdToneHz);
                var data = new MeasurementData
                {
                    Label = $"THD {ThdToneHz:0}Hz",
                    SignalType = "SineTone",
                    SignalDurationMs = ThdTestDurationSec * 1000,
                    Distortion = thd
                };
                Dispatcher.UIThread.Post(() =>
                {
                    var item = new MeasurementItemViewModel(data, "#F59E0B");
                    Measurements.Add(item);
                    SelectedMeasurement = item;
                    StatusMessage = $"THD: %{thd.THD:F2}";
                    StatusColor = "#22C55E";
                });
            });
        }
        catch (Exception ex)
        {
            _captureService.StopCapture();
            StatusMessage = $"THD hatası: {ex.Message}";
            StatusColor = "#EF4444";
        }
        finally
        {
            IsRecording = false;
        }
    }

    /// <summary>SMPTE benzeri 2-ton IMD ölçümü. Tonları çal, kayıt al, multitone FFT analizini IMD'ye çevir.</summary>
    [RelayCommand]
    private async Task StartImdTest()
    {
        if (IsRecording) return;
        if (!EnsureAudioReady()) return;

        IsRecording = true;
        StatusMessage = "IMD testi oynatılıyor...";
        StatusColor = "#EAB308";

        try
        {
            int[] freqs = { (int)Math.Round(ImdTone1Hz), (int)Math.Round(ImdTone2Hz) };
            var tone = NoiseGenerator.Multitone(freqs, ImdTestDurationSec, 48000, amplitude: 0.4f);

            if (IsRtaActive)
            {
                _captureService.StopCapture();
                IsRtaActive = false;
            }

            _captureService.StartCapture(48000, SelectedInputDevice?.Id);
            await _playbackService.PlayBufferAsync(tone, 48000, SelectedOutputDevice?.Id);
            await Task.Delay(400);
            var recorded = _captureService.StopCapture();

            if (recorded.Length < 4096)
            {
                StatusMessage = "IMD: Yeterli veri alınamadı.";
                StatusColor = "#EF4444";
                return;
            }

            await Task.Run(() =>
            {
                double[] dfreqs = { ImdTone1Hz, ImdTone2Hz };
                var imd = DistortionAnalyzer.AnalyzeIntermodulation(recorded, 48000, dfreqs);
                var data = new MeasurementData
                {
                    Label = $"IMD {ImdTone1Hz:0}+{ImdTone2Hz:0}Hz",
                    SignalType = "Multitone",
                    SignalDurationMs = ImdTestDurationSec * 1000,
                    Intermodulation = imd
                };
                Dispatcher.UIThread.Post(() =>
                {
                    var item = new MeasurementItemViewModel(data, "#A855F7");
                    Measurements.Add(item);
                    SelectedMeasurement = item;
                    StatusMessage = $"IMD: %{imd.ImdPercent:F2}  ·  Gürültü tabanı {imd.NoiseFloorDbr:F1} dBr";
                    StatusColor = "#22C55E";
                });
            });
        }
        catch (Exception ex)
        {
            _captureService.StopCapture();
            StatusMessage = $"IMD hatası: {ex.Message}";
            StatusColor = "#EF4444";
        }
        finally
        {
            IsRecording = false;
        }
    }
}
