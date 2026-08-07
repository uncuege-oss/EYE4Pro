using System;
using System.Linq;
using System.Threading.Tasks;

namespace Kst.Acoustics.Core.Measurement.Capture;

/// <summary>
/// Ses kartı ve sistem gecikmesini (latency) ölçmek için kullanılır.
/// Çıkışı girişe bağlayarak (loopback) ölçüm yapılır.
/// </summary>
public class LoopbackLatencyCalibrator
{
    private readonly AudioPlaybackService _playback;
    private readonly AudioCaptureService _capture;

    public LoopbackLatencyCalibrator(AudioPlaybackService playback, AudioCaptureService capture)
    {
        _playback = playback;
        _capture = capture;
    }

    /// <summary>
    /// Gecikmeyi milisaniye cinsinden ölçer.
    /// </summary>
    public async Task<double> MeasureLatencyMs(int sampleRate = 48000, string? inputDeviceId = null, string? outputDeviceId = null)
    {
        // 1. Kısa bir impuls sinyali üret (Dirac delta benzeri)
        float[] pulse = new float[sampleRate / 10]; // 100ms sessizlik
        pulse[100] = 1.0f; // 100. örnekte bir klik

        // 2. Kaydı başlat
        _capture.StartCapture(sampleRate, inputDeviceId);

        // 3. İmpulsu çal
        _playback.PlayBuffer(pulse, sampleRate, outputDeviceId);

        // 4. Bekle
        await Task.Delay(500);

        // 5. Kaydı durdur ve analiz et
        float[] recorded = _capture.StopCapture();

        if (recorded.Length == 0) return 0;

        // 6. En yüksek tepe noktasını bul
        float maxVal = 0;
        int peakIndex = -1;
        for (int i = 0; i < recorded.Length; i++)
        {
            float absVal = Math.Abs(recorded[i]);
            if (absVal > maxVal)
            {
                maxVal = absVal;
                peakIndex = i;
            }
        }

        if (peakIndex == -1 || maxVal < 0.01f) return 0;

        // 7. Gecikmeyi hesapla (Gönderilen 100. örnek ile alınan peak arasındaki fark)
        int latencySamples = Math.Max(0, peakIndex - 100);
        double latencyMs = (double)latencySamples / sampleRate * 1000.0;

        return latencyMs;
    }
}
