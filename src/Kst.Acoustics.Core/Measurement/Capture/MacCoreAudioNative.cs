using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Kst.Acoustics.Core.Measurement.Capture;

/// <summary>
/// macOS CoreAudio / AudioToolbox / CoreFoundation P/Invoke bağları.
/// Yalnızca macOS üzerinde çağrılmalıdır; framework yolları sistem sabitidir.
/// </summary>
internal static class MacCoreAudioNative
{
    private const string AudioToolboxLib = "/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox";
    private const string CoreAudioLib = "/System/Library/Frameworks/CoreAudio.framework/CoreAudio";
    private const string CoreFoundationLib = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    // FourCC sabitleri
    internal const uint FormatLinearPcm = 0x6C70636D;             // 'lpcm'
    internal const uint FormatFlagIsFloat = 1u << 0;
    internal const uint FormatFlagIsPacked = 1u << 3;
    internal const uint QueuePropertyCurrentDevice = 0x61716364;  // 'aqcd'
    internal const uint QueuePropertyIsRunning = 0x6171726E;      // 'aqrn'

    internal const uint SystemObjectId = 1;                        // kAudioObjectSystemObject
    internal const uint HardwarePropertyDevices = 0x64657623;      // 'dev#'
    internal const uint HardwarePropertyDefaultInput = 0x64496E20; // 'dIn '
    internal const uint HardwarePropertyDefaultOutput = 0x644F7574;// 'dOut'
    internal const uint ScopeGlobal = 0x676C6F62;                  // 'glob'
    internal const uint ScopeInput = 0x696E7074;                   // 'inpt'
    internal const uint ScopeOutput = 0x6F757470;                  // 'outp'
    internal const uint PropertyStreamConfiguration = 0x736C6179;  // 'slay'
    internal const uint PropertyDeviceUid = 0x75696420;            // 'uid '
    internal const uint PropertyName = 0x6C6E616D;                 // 'lnam'

    private const uint CfStringEncodingUtf8 = 0x08000100;

    [StructLayout(LayoutKind.Sequential)]
    internal struct AudioStreamBasicDescription
    {
        public double SampleRate;
        public uint FormatId;
        public uint FormatFlags;
        public uint BytesPerPacket;
        public uint FramesPerPacket;
        public uint BytesPerFrame;
        public uint ChannelsPerFrame;
        public uint BitsPerChannel;
        public uint Reserved;
    }

    // 64-bit hizalama: Capacity(0), AudioData(8), ByteSize(16), UserData(24)
    [StructLayout(LayoutKind.Sequential)]
    internal struct AudioQueueBuffer
    {
        public uint AudioDataBytesCapacity;
        public IntPtr AudioData;
        public uint AudioDataByteSize;
        public IntPtr UserData;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct AudioObjectPropertyAddress
    {
        public uint Selector;
        public uint Scope;
        public uint Element;
    }

    internal delegate void AudioQueueInputCallback(
        IntPtr userData, IntPtr queue, IntPtr buffer, IntPtr startTime, uint packetCount, IntPtr packetDescs);

    internal delegate void AudioQueueOutputCallback(IntPtr userData, IntPtr queue, IntPtr buffer);

    internal delegate void AudioQueuePropertyListener(IntPtr userData, IntPtr queue, uint propertyId);

    [DllImport(AudioToolboxLib)]
    internal static extern int AudioQueueNewInput(
        ref AudioStreamBasicDescription format, AudioQueueInputCallback callback, IntPtr userData,
        IntPtr runLoop, IntPtr runLoopMode, uint flags, out IntPtr queue);

    [DllImport(AudioToolboxLib)]
    internal static extern int AudioQueueNewOutput(
        ref AudioStreamBasicDescription format, AudioQueueOutputCallback callback, IntPtr userData,
        IntPtr runLoop, IntPtr runLoopMode, uint flags, out IntPtr queue);

    [DllImport(AudioToolboxLib)]
    internal static extern int AudioQueueAllocateBuffer(IntPtr queue, uint byteSize, out IntPtr buffer);

    [DllImport(AudioToolboxLib)]
    internal static extern int AudioQueueEnqueueBuffer(IntPtr queue, IntPtr buffer, uint packetDescCount, IntPtr packetDescs);

    [DllImport(AudioToolboxLib)]
    internal static extern int AudioQueueStart(IntPtr queue, IntPtr startTime);

    [DllImport(AudioToolboxLib)]
    internal static extern int AudioQueueStop(IntPtr queue, [MarshalAs(UnmanagedType.I1)] bool immediate);

    [DllImport(AudioToolboxLib)]
    internal static extern int AudioQueueDispose(IntPtr queue, [MarshalAs(UnmanagedType.I1)] bool immediate);

    [DllImport(AudioToolboxLib)]
    internal static extern int AudioQueueSetProperty(IntPtr queue, uint propertyId, ref IntPtr data, uint dataSize);

    [DllImport(AudioToolboxLib)]
    internal static extern int AudioQueueGetProperty(IntPtr queue, uint propertyId, out uint data, ref uint dataSize);

    [DllImport(AudioToolboxLib)]
    internal static extern int AudioQueueAddPropertyListener(IntPtr queue, uint propertyId, AudioQueuePropertyListener listener, IntPtr userData);

    [DllImport(CoreAudioLib)]
    internal static extern int AudioObjectGetPropertyDataSize(
        uint objectId, ref AudioObjectPropertyAddress address, uint qualifierSize, IntPtr qualifier, out uint dataSize);

    [DllImport(CoreAudioLib)]
    internal static extern int AudioObjectGetPropertyData(
        uint objectId, ref AudioObjectPropertyAddress address, uint qualifierSize, IntPtr qualifier, ref uint dataSize, IntPtr data);

    [DllImport(CoreFoundationLib)]
    private static extern IntPtr CFStringCreateWithCString(IntPtr allocator, byte[] cString, uint encoding);

    [DllImport(CoreFoundationLib)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CFStringGetCString(IntPtr cfString, byte[] buffer, nint bufferSize, uint encoding);

    [DllImport(CoreFoundationLib)]
    private static extern nint CFStringGetLength(IntPtr cfString);

    [DllImport(CoreFoundationLib)]
    internal static extern void CFRelease(IntPtr obj);

    internal static AudioStreamBasicDescription CreateMonoFloatFormat(int sampleRate) => new()
    {
        SampleRate = sampleRate,
        FormatId = FormatLinearPcm,
        FormatFlags = FormatFlagIsFloat | FormatFlagIsPacked,
        BytesPerPacket = 4,
        FramesPerPacket = 1,
        BytesPerFrame = 4,
        ChannelsPerFrame = 1,
        BitsPerChannel = 32,
        Reserved = 0
    };

    internal static IntPtr CreateCfString(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value + "\0");
        return CFStringCreateWithCString(IntPtr.Zero, bytes, CfStringEncodingUtf8);
    }

    internal static string? CfStringToString(IntPtr cfString)
    {
        if (cfString == IntPtr.Zero) return null;
        nint length = CFStringGetLength(cfString);
        var buffer = new byte[length * 4 + 1];
        if (!CFStringGetCString(cfString, buffer, buffer.Length, CfStringEncodingUtf8)) return null;
        int end = Array.IndexOf(buffer, (byte)0);
        return Encoding.UTF8.GetString(buffer, 0, end < 0 ? buffer.Length : end);
    }

    /// <summary>Kuyruğun kullanacağı cihazı UID ile seçer; hata kodunu döndürür (0 = başarı).</summary>
    internal static int SetQueueDevice(IntPtr queue, string deviceUid)
    {
        IntPtr cfUid = CreateCfString(deviceUid);
        if (cfUid == IntPtr.Zero) return -1;
        try
        {
            return AudioQueueSetProperty(queue, QueuePropertyCurrentDevice, ref cfUid, (uint)IntPtr.Size);
        }
        finally
        {
            CFRelease(cfUid);
        }
    }

    /// <summary>Sistemdeki tüm ses cihazlarının AudioObjectID listesini döndürür.</summary>
    internal static uint[] GetAllDeviceIds()
    {
        var address = new AudioObjectPropertyAddress
        {
            Selector = HardwarePropertyDevices,
            Scope = ScopeGlobal,
            Element = 0
        };

        if (AudioObjectGetPropertyDataSize(SystemObjectId, ref address, 0, IntPtr.Zero, out uint size) != 0 || size == 0)
            return [];

        IntPtr data = Marshal.AllocHGlobal((int)size);
        try
        {
            if (AudioObjectGetPropertyData(SystemObjectId, ref address, 0, IntPtr.Zero, ref size, data) != 0)
                return [];

            int count = (int)(size / 4);
            var ids = new uint[count];
            for (int i = 0; i < count; i++)
                ids[i] = (uint)Marshal.ReadInt32(data, i * 4);
            return ids;
        }
        finally
        {
            Marshal.FreeHGlobal(data);
        }
    }

    /// <summary>Varsayılan giriş/çıkış cihazının AudioObjectID değerini döndürür (0 = yok).</summary>
    internal static uint GetDefaultDeviceId(bool input)
    {
        var address = new AudioObjectPropertyAddress
        {
            Selector = input ? HardwarePropertyDefaultInput : HardwarePropertyDefaultOutput,
            Scope = ScopeGlobal,
            Element = 0
        };

        uint size = 4;
        IntPtr data = Marshal.AllocHGlobal(4);
        try
        {
            if (AudioObjectGetPropertyData(SystemObjectId, ref address, 0, IntPtr.Zero, ref size, data) != 0)
                return 0;
            return (uint)Marshal.ReadInt32(data);
        }
        finally
        {
            Marshal.FreeHGlobal(data);
        }
    }

    /// <summary>Cihazın giriş veya çıkış yönündeki toplam kanal sayısını döndürür.</summary>
    internal static int GetChannelCount(uint deviceId, bool input)
    {
        var address = new AudioObjectPropertyAddress
        {
            Selector = PropertyStreamConfiguration,
            Scope = input ? ScopeInput : ScopeOutput,
            Element = 0
        };

        if (AudioObjectGetPropertyDataSize(deviceId, ref address, 0, IntPtr.Zero, out uint size) != 0 || size == 0)
            return 0;

        IntPtr data = Marshal.AllocHGlobal((int)size);
        try
        {
            if (AudioObjectGetPropertyData(deviceId, ref address, 0, IntPtr.Zero, ref size, data) != 0)
                return 0;

            // AudioBufferList: mNumberBuffers (0), hizalama sonrası mBuffers 8'de; AudioBuffer 16 bayt, kanal sayısı ofset 0'da
            int bufferCount = Marshal.ReadInt32(data);
            int channels = 0;
            for (int i = 0; i < bufferCount; i++)
                channels += Marshal.ReadInt32(data, 8 + i * 16);
            return channels;
        }
        finally
        {
            Marshal.FreeHGlobal(data);
        }
    }

    /// <summary>CFString türünde bir cihaz özelliğini okur (ad, UID).</summary>
    internal static string? GetDeviceStringProperty(uint deviceId, uint selector)
    {
        var address = new AudioObjectPropertyAddress
        {
            Selector = selector,
            Scope = ScopeGlobal,
            Element = 0
        };

        uint size = (uint)IntPtr.Size;
        IntPtr data = Marshal.AllocHGlobal(IntPtr.Size);
        try
        {
            if (AudioObjectGetPropertyData(deviceId, ref address, 0, IntPtr.Zero, ref size, data) != 0)
                return null;

            IntPtr cfString = Marshal.ReadIntPtr(data);
            if (cfString == IntPtr.Zero) return null;
            try
            {
                return CfStringToString(cfString);
            }
            finally
            {
                CFRelease(cfString);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(data);
        }
    }
}
