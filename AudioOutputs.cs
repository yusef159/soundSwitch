using System.Runtime.InteropServices;

namespace SoundSwitch;

internal sealed record AudioOutput(string Id, string Name);

internal sealed class AudioOutputs : IDisposable
{
    private const uint DeviceStateActive = 1;
    private const int EndpointNotFound = unchecked((int)0x80070490);
    private readonly NotificationClient notifications;
    private IDeviceEnumerator? enumerator;
    private IPolicyConfig? policy;
    private bool subscribed;

    internal AudioOutputs(Action devicesChanged) => notifications = new NotificationClient(devicesChanged);

    private IDeviceEnumerator GetEnumerator()
    {
        enumerator ??= (IDeviceEnumerator)new DeviceEnumeratorCom();
        if (!subscribed)
        {
            CheckResult(enumerator.RegisterEndpointNotificationCallback(notifications));
            subscribed = true;
        }

        return enumerator;
    }

    internal AudioOutput[] GetActiveOutputs()
    {
        CheckResult(GetEnumerator().EnumAudioEndpoints(AudioFlow.Render, DeviceStateActive, out var collection));
        try
        {
            CheckResult(collection.GetCount(out uint count));
            var outputs = new List<AudioOutput>((int)count);
            for (uint i = 0; i < count; i++)
            {
                if (collection.Item(i, out var device) < 0)
                    continue;

                try
                {
                    if (device.GetId(out string id) >= 0)
                        outputs.Add(new AudioOutput(id, GetName(device)));
                }
                finally
                {
                    Marshal.ReleaseComObject(device);
                }
            }

            return outputs.OrderBy(output => output.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(output => output.Id, StringComparer.Ordinal)
                .ToArray();
        }
        finally
        {
            Marshal.ReleaseComObject(collection);
        }
    }

    private static string GetName(IAudioDevice device)
    {
        const string fallback = "Audio output";
        if (device.OpenPropertyStore(0, out var store) < 0)
            return fallback;

        try
        {
            int result = store.GetValue(PropertyKey.FriendlyName, out var value);
            try
            {
                string? name = result >= 0 ? value.ReadString() : null;
                return string.IsNullOrWhiteSpace(name) ? fallback : name;
            }
            finally
            {
                PropVariantClear(ref value);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(store);
        }
    }

    internal string? GetDefaultId(AudioRole role = AudioRole.Multimedia)
    {
        int result = GetEnumerator().GetDefaultAudioEndpoint(AudioFlow.Render, role, out var device);
        if (result == EndpointNotFound)
            return null;

        CheckResult(result);
        try
        {
            CheckResult(device.GetId(out string id));
            return id;
        }
        finally
        {
            Marshal.ReleaseComObject(device);
        }
    }

    internal void SetDefault(string id)
    {
        policy ??= (IPolicyConfig)new PolicyConfigCom();
        CheckResult(policy.SetDefaultEndpoint(id, AudioRole.Multimedia));
        int console = policy.SetDefaultEndpoint(id, AudioRole.Console);
        int communications = policy.SetDefaultEndpoint(id, AudioRole.Communications);
        CheckResult(console < 0 ? console : communications);
    }

    private static void CheckResult(int result)
    {
        if (result < 0)
            throw new COMException("A Windows Core Audio call failed.", result);
    }

    public void Dispose()
    {
        if (enumerator is not null)
        {
            if (subscribed)
                enumerator.UnregisterEndpointNotificationCallback(notifications);

            Marshal.ReleaseComObject(enumerator);
        }

        if (policy is not null)
            Marshal.ReleaseComObject(policy);
    }

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int PropVariantClear(ref PropVariant value);

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    private sealed class NotificationClient(Action changed) : INotificationClient
    {
        public int OnDeviceStateChanged(string id, uint state) => SignalChange();
        public int OnDeviceAdded(string id) => SignalChange();
        public int OnDeviceRemoved(string id) => SignalChange();
        public int OnDefaultDeviceChanged(AudioFlow flow, AudioRole role, string? id) =>
            flow == AudioFlow.Render && role == AudioRole.Multimedia ? SignalChange() : 0;

        public int OnPropertyValueChanged(string id, PropertyKey key) =>
            key == PropertyKey.FriendlyName ? SignalChange() : 0;

        private int SignalChange()
        {
            changed();
            return 0;
        }
    }
}
