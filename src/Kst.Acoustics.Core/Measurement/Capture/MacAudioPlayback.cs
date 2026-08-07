using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Kst.Acoustics.Core.Measurement.Capture;

/// <summary>
/// macOS AudioQueue (AudioToolbox) tabanlı oynatma.
/// Tüm örnekler baştan kuyruğa yüklenir; kuyruk boşalınca Task tamamlanır.
/// </summary>
internal static class MacAudioPlayback
{
    private const int ChunkFramesPerSecond = 1; // her buffer ~1 saniyelik ses taşır

    // Delegate'ler GC tarafından toplanmasın diye statik alanda köklenir
    private static readonly MacCoreAudioNative.AudioQueueOutputCallback OutputCallback = static (_, _, _) => { };
    private static readonly MacCoreAudioNative.AudioQueuePropertyListener RunningListener = OnRunningChanged;

    private sealed class PlaybackState
    {
        public IntPtr Queue;
        public readonly TaskCompletionSource Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public GCHandle SelfHandle;
        public int Finished; // Interlocked bayrağı: temizlik yalnız bir kez yapılır
    }

    public static Task PlayAsync(float[] samples, int sampleRate, string? deviceUid)
    {
        if (samples.Length == 0)
            return Task.CompletedTask;

        var format = MacCoreAudioNative.CreateMonoFloatFormat(sampleRate);
        var state = new PlaybackState();
        state.SelfHandle = GCHandle.Alloc(state);

        int status = MacCoreAudioNative.AudioQueueNewOutput(
            ref format, OutputCallback, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out state.Queue);
        if (status != 0)
        {
            state.SelfHandle.Free();
            throw new InvalidOperationException($"Ses oynatma kuyruğu açılamadı (AudioQueueNewOutput: {status}).");
        }

        try
        {
            if (!string.IsNullOrEmpty(deviceUid) && deviceUid != MacAudioDeviceEnumerator.DefaultOutputId)
                MacCoreAudioNative.SetQueueDevice(state.Queue, deviceUid);

            // Tüm sinyali ~1 saniyelik buffer'lar halinde baştan kuyruğa yükle
            int chunkSamples = sampleRate * ChunkFramesPerSecond;
            for (int offset = 0; offset < samples.Length; offset += chunkSamples)
            {
                int count = Math.Min(chunkSamples, samples.Length - offset);
                uint byteSize = (uint)count * 4;

                status = MacCoreAudioNative.AudioQueueAllocateBuffer(state.Queue, byteSize, out IntPtr bufferPtr);
                if (status != 0)
                    throw new InvalidOperationException($"Oynatma buffer'ı ayrılamadı (AudioQueueAllocateBuffer: {status}).");

                var buffer = Marshal.PtrToStructure<MacCoreAudioNative.AudioQueueBuffer>(bufferPtr);
                Marshal.Copy(samples, offset, buffer.AudioData, count);
                Marshal.WriteInt32(bufferPtr, (int)Marshal.OffsetOf<MacCoreAudioNative.AudioQueueBuffer>(
                    nameof(MacCoreAudioNative.AudioQueueBuffer.AudioDataByteSize)), (int)byteSize);

                MacCoreAudioNative.AudioQueueEnqueueBuffer(state.Queue, bufferPtr, 0, IntPtr.Zero);
            }

            MacCoreAudioNative.AudioQueueAddPropertyListener(
                state.Queue, MacCoreAudioNative.QueuePropertyIsRunning, RunningListener, GCHandle.ToIntPtr(state.SelfHandle));

            status = MacCoreAudioNative.AudioQueueStart(state.Queue, IntPtr.Zero);
            if (status != 0)
                throw new InvalidOperationException($"Oynatma başlatılamadı (AudioQueueStart: {status}).");

            // Kuyruktaki buffer'lar bitince asenkron durdur → IsRunning listener'ı tetiklenir
            MacCoreAudioNative.AudioQueueStop(state.Queue, false);
        }
        catch
        {
            Cleanup(state);
            throw;
        }

        // Güvenlik ağı: listener hiç tetiklenmezse süre + 3 sn sonra zorla tamamla
        double durationMs = (double)samples.Length / sampleRate * 1000.0;
        _ = Task.Delay(TimeSpan.FromMilliseconds(durationMs + 3000)).ContinueWith(_ => Cleanup(state));

        return state.Completion.Task;
    }

    private static void OnRunningChanged(IntPtr userData, IntPtr queue, uint propertyId)
    {
        var handle = GCHandle.FromIntPtr(userData);
        if (handle.Target is not PlaybackState state)
            return;

        uint size = 4;
        if (MacCoreAudioNative.AudioQueueGetProperty(queue, MacCoreAudioNative.QueuePropertyIsRunning, out uint running, ref size) == 0
            && running == 0)
        {
            // Dispose callback thread'inde kilitlenebilir; temizliği thread pool'a taşı
            ThreadPool.QueueUserWorkItem(_ => Cleanup(state));
        }
    }

    private static void Cleanup(PlaybackState state)
    {
        if (Interlocked.Exchange(ref state.Finished, 1) != 0)
            return;

        if (state.Queue != IntPtr.Zero)
        {
            MacCoreAudioNative.AudioQueueDispose(state.Queue, true);
            state.Queue = IntPtr.Zero;
        }
        if (state.SelfHandle.IsAllocated)
            state.SelfHandle.Free();

        state.Completion.TrySetResult();
    }
}
