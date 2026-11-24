using System.Runtime.InteropServices;

namespace SoundSwitch;

internal enum AudioFlow { Render, Capture, All }
internal enum AudioRole { Console, Multimedia, Communications }

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct PropertyKey(Guid FormatId, uint Id)
{
    internal static readonly PropertyKey FriendlyName = new(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);
}

[StructLayout(LayoutKind.Explicit)]
internal struct PropVariant
{
    [FieldOffset(0)] private ushort type;
    [FieldOffset(8)] private IntPtr pointer;
    // The native union also contains counted arrays, which determine its ABI size.
    [FieldOffset(8)] private CountedArray array;

    internal readonly string? ReadString() => type == 31 ? Marshal.PtrToStringUni(pointer) : null; // VT_LPWSTR

    [StructLayout(LayoutKind.Sequential)]
    private struct CountedArray
    {
        private uint count;
        private IntPtr elements;
    }
}

[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class DeviceEnumeratorCom { }

[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDeviceEnumerator
{
    [PreserveSig] int EnumAudioEndpoints(AudioFlow flow, uint state, out IDeviceCollection devices);
    [PreserveSig] int GetDefaultAudioEndpoint(AudioFlow flow, AudioRole role, out IAudioDevice device);
    [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IAudioDevice device);
    [PreserveSig] int RegisterEndpointNotificationCallback(INotificationClient callback);
    [PreserveSig] int UnregisterEndpointNotificationCallback(INotificationClient callback);
}

[ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDeviceCollection
{
    [PreserveSig] int GetCount(out uint count);
    [PreserveSig] int Item(uint index, out IAudioDevice device);
}

[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioDevice
{
    [PreserveSig] int Activate(in Guid iid, uint clsContext, IntPtr activationParams, out IntPtr instance);
    [PreserveSig] int OpenPropertyStore(uint access, out IPropertyStore properties);
    [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
}

[ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyStore
{
    [PreserveSig] int GetCount(out uint count);
    [PreserveSig] int GetAt(uint index, out PropertyKey key);
    [PreserveSig] int GetValue(in PropertyKey key, out PropVariant value);
}

[ComImport, Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface INotificationClient
{
    [PreserveSig] int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string id, uint state);
    [PreserveSig] int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string id);
    [PreserveSig] int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string id);
    [PreserveSig] int OnDefaultDeviceChanged(AudioFlow flow, AudioRole role, [MarshalAs(UnmanagedType.LPWStr)] string? id);
    [PreserveSig] int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string id, PropertyKey key);
}

[ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
internal class PolicyConfigCom { }

// Windows does not publish this interface. The unused slots preserve the native vtable order.
[ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPolicyConfig
{
    void GetMixFormat();
    void GetDeviceFormat();
    void ResetDeviceFormat();
    void SetDeviceFormat();
    void GetProcessingPeriod();
    void SetProcessingPeriod();
    void GetShareMode();
    void SetShareMode();
    void GetPropertyValue();
    void SetPropertyValue();
    [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, AudioRole role);
}
