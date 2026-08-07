using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Kst.Acoustics.Core.Measurement.Capture;

public class AudioPlaybackService : IDisposable
{
    private IWavePlayer? _wavePlayer;

    public void PlayBuffer(float[] samples, int sampleRate, string? deviceId = null)
    {
        _ = PlayBufferAsync(samples, sampleRate, deviceId);
    }

    public Task PlayBufferAsync(float[] samples, int sampleRate, string? deviceId = null)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Stop();

            var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            var provider = new RawSourceWaveStream(new System.IO.MemoryStream(SamplesToBytes(samples)), waveFormat);

            _wavePlayer = string.IsNullOrEmpty(deviceId) 
                ? new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, 100) 
                : new WasapiOut(GetDeviceById(deviceId), NAudio.CoreAudioApi.AudioClientShareMode.Shared, false, 100);

            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _wavePlayer.PlaybackStopped += (_, _) => completion.TrySetResult();
            _wavePlayer.Init(provider);
            _wavePlayer.Play();
            return completion.Task;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            try
            {
                return MacAudioPlayback.PlayAsync(samples, sampleRate, deviceId);
            }
            catch
            {
                // CoreAudio kuyruğu açılamazsa afplay geçici dosya yoluna düş
                return Task.Run(() =>
                {
                    string tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"kst_sweep_{Guid.NewGuid()}.wav");
                    WriteWavFile(tempFile, samples, sampleRate);
                    var process = System.Diagnostics.Process.Start("afplay", tempFile);
                    process?.WaitForExit();
                    if (System.IO.File.Exists(tempFile)) System.IO.File.Delete(tempFile);
                });
            }
        }

        return Task.CompletedTask;
    }

    private void WriteWavFile(string path, float[] samples, int sampleRate)
    {
        using var fs = new System.IO.FileStream(path, System.IO.FileMode.Create);
        using var bw = new System.IO.BinaryWriter(fs);
        bw.Write(new char[] { 'R', 'I', 'F', 'F' });
        bw.Write(36 + samples.Length * 4);
        bw.Write(new char[] { 'W', 'A', 'V', 'E' });
        bw.Write(new char[] { 'f', 'm', 't', ' ' });
        bw.Write(16);
        bw.Write((short)3); // IEEE Float
        bw.Write((short)1); // Mono
        bw.Write(sampleRate);
        bw.Write(sampleRate * 4);
        bw.Write((short)4);
        bw.Write((short)32);
        bw.Write(new char[] { 'd', 'a', 't', 'a' });
        bw.Write(samples.Length * 4);
        foreach (var s in samples) bw.Write(s);
    }

    public void Stop()
    {
        _wavePlayer?.Stop();
        _wavePlayer?.Dispose();
        _wavePlayer = null;
    }

    private byte[] SamplesToBytes(float[] samples)
    {
        byte[] bytes = new byte[samples.Length * 4];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private MMDevice GetDeviceById(string id)
    {
        var enumerator = new MMDeviceEnumerator();
        return enumerator.GetDevice(id);
    }

    public void Dispose()
    {
        Stop();
    }
}
