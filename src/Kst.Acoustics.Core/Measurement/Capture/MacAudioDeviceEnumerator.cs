using System;
using System.Collections.Generic;

namespace Kst.Acoustics.Core.Measurement.Capture;

/// <summary>
/// macOS CoreAudio HAL üzerinden ses giriş/çıkış cihazlarını listeler.
/// Cihaz Id olarak CoreAudio UID kullanılır; AudioQueue cihaz seçimi bu UID ile yapılır.
/// </summary>
internal static class MacAudioDeviceEnumerator
{
    public const string DefaultInputId = "default_in";
    public const string DefaultOutputId = "default_out";

    public static List<AudioDevice> GetDevices(bool input)
    {
        var devices = new List<AudioDevice>
        {
            // Varsayılan cihaz her zaman listenin başında; UID verilmediğinde sistem varsayılanı kullanılır
            new(input ? DefaultInputId : DefaultOutputId,
                input ? "Varsayılan Giriş (Sistem)" : "Varsayılan Çıkış (Sistem)",
                input ? 1 : 2)
        };

        foreach (uint deviceId in MacCoreAudioNative.GetAllDeviceIds())
        {
            int channels = MacCoreAudioNative.GetChannelCount(deviceId, input);
            if (channels <= 0)
                continue;

            string? uid = MacCoreAudioNative.GetDeviceStringProperty(deviceId, MacCoreAudioNative.PropertyDeviceUid);
            string? name = MacCoreAudioNative.GetDeviceStringProperty(deviceId, MacCoreAudioNative.PropertyName);
            if (string.IsNullOrEmpty(uid))
                continue;

            devices.Add(new AudioDevice(uid, name ?? uid, channels));
        }

        return devices;
    }
}
