using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Kst.Acoustics.Core.Measurement.Capture;

public class AudioCaptureService : IDisposable
{
    private readonly object _lock = new();
    private List<float> _recordedSamples = [];
    private WasapiCapture? _capture;
    private MacAudioCapture? _macCapture;

    public event Action<float[]>? DataAvailable;

    public void StartCapture(int sampleRate = 48000, string? deviceId = null)
    {
        StopCapture();
        lock (_lock)
        {
            _recordedSamples.Clear();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _capture = string.IsNullOrEmpty(deviceId)
                ? new WasapiCapture()
                : new WasapiCapture(GetDeviceById(deviceId));

            _capture.WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _capture.DataAvailable += (s, e) =>
            {
                float[] buffer = new float[e.BytesRecorded / 4];
                Buffer.BlockCopy(e.Buffer, 0, buffer, 0, e.BytesRecorded);
                OnSamples(buffer);
            };

            _capture.StartRecording();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            _macCapture = new MacAudioCapture();
            _macCapture.DataAvailable += OnSamples;
            _macCapture.Start(sampleRate, deviceId);
        }
        else
        {
            throw new PlatformNotSupportedException(
                "Gerçek zamanlı ses yakalama bu platformda desteklenmiyor (Windows WASAPI ve macOS CoreAudio destekleniyor).");
        }
    }

    private void OnSamples(float[] buffer)
    {
        lock (_lock)
        {
            _recordedSamples.AddRange(buffer);
        }
        DataAvailable?.Invoke(buffer);
    }

    public float[] StopCapture()
    {
        if (_capture != null)
        {
            _capture.StopRecording();
            _capture.Dispose();
            _capture = null;
        }

        if (_macCapture != null)
        {
            _macCapture.DataAvailable -= OnSamples;
            _macCapture.Dispose();
            _macCapture = null;
        }

        lock (_lock)
        {
            var result = _recordedSamples.ToArray();
            _recordedSamples.Clear();
            return result;
        }
    }

    private MMDevice GetDeviceById(string id)
    {
        var enumerator = new MMDeviceEnumerator();
        return enumerator.GetDevice(id);
    }

    public void Dispose()
    {
        StopCapture();
    }
}
