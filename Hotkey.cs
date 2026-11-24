using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SoundSwitch;

[Flags]
internal enum HotkeyModifiers : uint
{
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8
}

internal readonly record struct Hotkey(HotkeyModifiers Modifiers, Keys Key)
{
    internal static Hotkey Default => new(HotkeyModifiers.Control | HotkeyModifiers.Alt, Keys.F11);

    internal bool IsValid =>
        (Modifiers & ~(HotkeyModifiers.Alt | HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Windows)) == 0
        && Modifiers != 0
        && (Key is >= Keys.A and <= Keys.Z or >= Keys.D0 and <= Keys.D9
            or >= Keys.F1 and <= Keys.F11 or >= Keys.F13 and <= Keys.F24);

    public override string ToString()
    {
        var parts = new List<string>(5);
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Windows)) parts.Add("Win");
        parts.Add(KeyLabel(Key));
        return string.Join("+", parts);
    }

    internal static string KeyLabel(Keys key) =>
        key is >= Keys.D0 and <= Keys.D9 ? ((int)(key - Keys.D0)).ToString() : key.ToString();
}

internal sealed class MessageWindow : NativeWindow, IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint WmAudioChanged = 0x8001;
    private const uint ModNoRepeat = 0x4000;
    private IntPtr notificationTarget;
    private int refreshQueued;
    private int hotkeyId;
    private Hotkey hotkey;

    internal event Action? HotkeyPressed;
    internal event Action? AudioChanged;

    internal MessageWindow()
    {
        CreateHandle(new CreateParams { Parent = new IntPtr(-3) }); // HWND_MESSAGE
        notificationTarget = Handle;
    }

    internal void SetHotkey(Hotkey shortcut, Action? persist = null)
    {
        if (hotkeyId != 0 && hotkey == shortcut)
        {
            persist?.Invoke();
            return;
        }

        int newId = hotkeyId == 1 ? 2 : 1;
        if (!RegisterHotKey(Handle, newId, (uint)shortcut.Modifiers | ModNoRepeat, (uint)shortcut.Key))
            throw new Win32Exception();

        try
        {
            persist?.Invoke();
        }
        catch
        {
            UnregisterHotKey(Handle, newId);
            throw;
        }

        if (hotkeyId != 0)
            UnregisterHotKey(Handle, hotkeyId);

        hotkeyId = newId;
        hotkey = shortcut;
    }

    internal void QueueAudioChanged()
    {
        IntPtr target = Volatile.Read(ref notificationTarget);
        if (target == IntPtr.Zero || Interlocked.Exchange(ref refreshQueued, 1) != 0)
            return;

        if (!PostMessageW(target, WmAudioChanged, IntPtr.Zero, IntPtr.Zero))
            Interlocked.Exchange(ref refreshQueued, 0);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmHotkey && hotkeyId != 0 && message.WParam == (IntPtr)hotkeyId)
        {
            HotkeyPressed?.Invoke();
            return;
        }

        if (message.Msg == WmAudioChanged)
        {
            Interlocked.Exchange(ref refreshQueued, 0);
            AudioChanged?.Invoke();
            return;
        }

        base.WndProc(ref message);
    }

    public void Dispose()
    {
        Volatile.Write(ref notificationTarget, IntPtr.Zero);
        if (hotkeyId != 0)
            UnregisterHotKey(Handle, hotkeyId);

        DestroyHandle();
    }

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessageW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
