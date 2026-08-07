using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace Kst.Acoustics.Core.Measurement.Capture;

public record AudioDevice(string Id, string Name, int Channels);

/// <summary>
/// Sistemdeki ses giriş ve çıkış cihazlarını listeler.
/// </summary>
public static class AudioDeviceEnumerator
{
    public static bool IsCaptureSupported =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    public static List<AudioDevice> GetInputDevices()
    {
        var devices = new List<AudioDevice>();

        if (!IsCaptureSupported)
        {
            return devices;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            try
            {
                return MacAudioDeviceEnumerator.GetDevices(input: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Giriş cihazları listelenirken hata: {ex.Message}");
                return devices;
            }
        }

        try
        {
            var enumerator = new MMDeviceEnumerator();
            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                devices.Add(new AudioDevice(device.ID, device.FriendlyName, device.AudioClient.MixFormat.Channels));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Giriş cihazları listelenirken hata: {ex.Message}");
        }

        return devices;
    }

    public static List<AudioDevice> GetOutputDevices()
    {
        var devices = new List<AudioDevice>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            try
            {
                return MacAudioDeviceEnumerator.GetDevices(input: false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Çıkış cihazları listelenirken hata: {ex.Message}");
                devices.Add(new AudioDevice(MacAudioDeviceEnumerator.DefaultOutputId, "Varsayılan Çıkış (Sistem)", 2));
                return devices;
            }
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            devices.Add(new AudioDevice("default_out", "Varsayılan Çıkış (Generic)", 2));
            return devices;
        }

        try
        {
            var enumerator = new MMDeviceEnumerator();
            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                devices.Add(new AudioDevice(device.ID, device.FriendlyName, device.AudioClient.MixFormat.Channels));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Çıkış cihazları listelenirken hata: {ex.Message}");
        }

        return devices;
    }
}
