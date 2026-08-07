using System;
using System.Runtime.InteropServices;

namespace Kst.Acoustics.Core.Measurement.Capture;

/// <summary>
/// macOS AudioQueue (AudioToolbox) tabanlı mikrofon yakalama.
/// 32-bit float mono örnek üretir; DataAvailable olayı AudioQueue iç thread'inde tetiklenir.
/// </summary>
internal sealed class MacAudioCapture : IDisposable
{
    private const int BufferCount = 3;
    private const double BufferSeconds = 0.1;

    // Delegate GC tarafından toplanmasın diye statik alanda köklenir
    private static readonly MacCoreAudioNative.AudioQueueInputCallback InputCallback = OnInput;

    private IntPtr _queue;
    private GCHandle _selfHandle;
    private volatile bool _running;

    public event Action<float[]>? DataAvailable;

    public void Start(int sampleRate, string? deviceUid)
    {
        Stop();

        var format = MacCoreAudioNative.CreateMonoFloatFormat(sampleRate);
        _selfHandle = GCHandle.Alloc(this);

        int status = MacCoreAudioNative.AudioQueueNewInput(
            ref format, InputCallback, GCHandle.ToIntPtr(_selfHandle),
            IntPtr.Zero, IntPtr.Zero, 0, out _queue);
        if (status != 0)
        {
            ReleaseSelfHandle();
            throw new InvalidOperationException($"Ses yakalama kuyruğu açılamadı (AudioQueueNewInput: {status}).");
        }

        // Cihaz seçimi başarısız olursa varsayılan giriş cihazıyla devam edilir
        if (!string.IsNullOrEmpty(deviceUid) && deviceUid != MacAudioDeviceEnumerator.DefaultInputId)
            MacCoreAudioNative.SetQueueDevice(_queue, deviceUid);

        uint bufferBytes = (uint)(sampleRate * BufferSeconds) * 4;
        for (int i = 0; i < BufferCount; i++)
        {
            if (MacCoreAudioNative.AudioQueueAllocateBuffer(_queue, bufferBytes, out IntPtr buffer) == 0)
                MacCoreAudioNative.AudioQueueEnqueueBuffer(_queue, buffer, 0, IntPtr.Zero);
        }

        _running = true;
        status = MacCoreAudioNative.AudioQueueStart(_queue, IntPtr.Zero);
        if (status != 0)
        {
            Stop();
            throw new InvalidOperationException(
                $"Ses kaydı başlatılamadı (AudioQueueStart: {status}). Sistem Ayarları'ndan mikrofon iznini kontrol edin.");
        }
    }

    public void Stop()
    {
        _running = false;
        if (_queue != IntPtr.Zero)
        {
            // immediate dispose bekleyen callback'lerin bitmesini bekler; buffer'lar kuyruğa aittir
            MacCoreAudioNative.AudioQueueStop(_queue, true);
            MacCoreAudioNative.AudioQueueDispose(_queue, true);
            _queue = IntPtr.Zero;
        }
        ReleaseSelfHandle();
    }

    private void ReleaseSelfHandle()
    {
        if (_selfHandle.IsAllocated)
            _selfHandle.Free();
    }

    private static void OnInput(IntPtr userData, IntPtr queue, IntPtr bufferPtr, IntPtr startTime, uint packetCount, IntPtr packetDescs)
    {
        var handle = GCHandle.FromIntPtr(userData);
        if (handle.Target is not MacAudioCapture self || !self._running)
            return;

        var buffer = Marshal.PtrToStructure<MacCoreAudioNative.AudioQueueBuffer>(bufferPtr);
        int sampleCount = (int)(buffer.AudioDataByteSize / 4);
        if (sampleCount > 0 && buffer.AudioData != IntPtr.Zero)
        {
            float[] samples = new float[sampleCount];
            Marshal.Copy(buffer.AudioData, samples, 0, sampleCount);
            self.DataAvailable?.Invoke(samples);
        }

        if (self._running)
            MacCoreAudioNative.AudioQueueEnqueueBuffer(queue, bufferPtr, 0, IntPtr.Zero);
    }

    public void Dispose()
    {
        Stop();
    }
}
