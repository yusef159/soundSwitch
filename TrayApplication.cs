using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security;

namespace SoundSwitch;

internal sealed class TrayApplication : ApplicationContext
{
    private readonly MessageWindow window = new();
    private readonly Icon icon = SpeakerIcon.Create();
    private readonly ContextMenuStrip menu = new();
    private readonly ToolStripMenuItem shortcutItem = new();
    private readonly ToolStripMenuItem startupItem = new("Start with Windows");
    private readonly NotifyIcon tray;
    private readonly AudioOutputs audio;
    private AudioOutput[]? outputs;
    private OutputOverlay? overlay;
    private ShortcutDialog? shortcutDialog;
    private Hotkey shortcut;
    private bool hotkeyAvailable;
    private bool disposed;
    private int outputItems;

    internal TrayApplication()
    {
        audio = new AudioOutputs(window.QueueAudioChanged);
        window.AudioChanged += () =>
        {
            outputs = null;
            if (menu.Visible)
                BuildMenu();
        };
        window.HotkeyPressed += CycleOutput;
        shortcut = Preferences.LoadHotkey();
        try
        {
            window.SetHotkey(shortcut);
            hotkeyAvailable = true;
        }
        catch (Win32Exception)
        {
            // Tray controls remain available if another application has registered the shortcut.
        }

        shortcutItem.Click += (_, _) => EditShortcut();
        startupItem.Click += (_, _) => ToggleStartup();
        menu.Items.AddRange([new ToolStripSeparator(), shortcutItem, startupItem, new ToolStripMenuItem("Exit", null, (_, _) => ExitThread())]);
        menu.Opening += (_, _) => BuildMenu();
        tray = new NotifyIcon { Icon = icon, ContextMenuStrip = menu, Text = "SoundSwitch", Visible = true };
        tray.MouseClick += (_, args) =>
        {
            if (args.Button == MouseButtons.Left)
                CycleOutput();
        };

        try
        {
            outputs = audio.GetActiveOutputs();
        }
        catch (Exception error) when (IsAudioError(error))
        {
            // The next tray or shortcut action can retry initialization.
        }
    }

    private void BuildMenu()
    {
        while (outputItems > 0)
        {
            var item = menu.Items[0];
            menu.Items.RemoveAt(0);
            item.Dispose();
            outputItems--;
        }

        try
        {
            var devices = outputs ??= audio.GetActiveOutputs();
            string? selected = audio.GetDefaultId();
            foreach (var device in devices)
            {
                var item = new ToolStripMenuItem(device.Name.Replace("&", "&&"))
                {
                    Checked = device.Id == selected,
                    ToolTipText = device.Name
                };
                item.Click += (_, _) => SwitchOutput(device);
                menu.Items.Insert(outputItems++, item);
            }

            if (outputItems == 0)
                AddMenuStatus("No active audio outputs");
        }
        catch (Exception error) when (IsAudioError(error))
        {
            outputs = null;
            AddMenuStatus("Windows audio unavailable");
        }

        shortcutItem.Text = hotkeyAvailable ? $"Keyboard shortcut ({shortcut})…" : $"Keyboard shortcut ({shortcut} unavailable)…";
        try
        {
            startupItem.Checked = Preferences.StartsWithWindows();
            startupItem.Enabled = true;
        }
        catch (Exception error) when (IsSettingsError(error))
        {
            startupItem.Enabled = false;
        }
    }

    private void AddMenuStatus(string text) =>
        menu.Items.Insert(outputItems++, new ToolStripMenuItem(text) { Enabled = false });

    private void CycleOutput()
    {
        try
        {
            var devices = outputs ??= audio.GetActiveOutputs();
            if (devices.Length == 0)
            {
                ShowOverlay("No active audio outputs");
                return;
            }

            string? selected = audio.GetDefaultId();
            int index = Array.FindIndex(devices, device => device.Id == selected);
            var next = devices[(index + 1) % devices.Length];
            if (next.Id == selected)
                ShowOverlay(next.Name);
            else
                SwitchOutput(next);
        }
        catch (Exception error) when (IsAudioError(error))
        {
            outputs = null;
            ShowOverlay("Windows audio unavailable");
        }
    }

    private void SwitchOutput(AudioOutput device)
    {
        try
        {
            audio.SetDefault(device.Id);
            ShowOverlay(device.Name);
        }
        catch (Exception error) when (IsAudioError(error))
        {
            outputs = null;
            ShowOverlay("Couldn’t switch audio output");
        }
    }

    private void ShowOverlay(string text) => (overlay ??= new OutputOverlay()).ShowOutput(text);

    private void EditShortcut()
    {
        if (shortcutDialog is not null)
        {
            shortcutDialog.Activate();
            return;
        }

        using var dialog = new ShortcutDialog(shortcut, SaveShortcut);
        shortcutDialog = dialog;
        try
        {
            dialog.ShowDialog();
        }
        finally
        {
            shortcutDialog = null;
        }
    }

    private string? SaveShortcut(Hotkey change)
    {
        try
        {
            window.SetHotkey(change, () => Preferences.SaveHotkey(change));
            shortcut = change;
            hotkeyAvailable = true;
            return null;
        }
        catch (Win32Exception)
        {
            return "Windows couldn’t register this shortcut. Try another combination.";
        }
        catch (Exception error) when (IsSettingsError(error))
        {
            return "Couldn’t save the shortcut.";
        }
    }

    private void ToggleStartup()
    {
        try
        {
            Preferences.SetStartup(!Preferences.StartsWithWindows());
        }
        catch (Exception error) when (IsSettingsError(error))
        {
            MessageBox.Show("Couldn’t update the startup setting.", "SoundSwitch", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static bool IsSettingsError(Exception error) => error is IOException or UnauthorizedAccessException or SecurityException;
    private static bool IsAudioError(Exception error) => error is COMException or InvalidCastException;

    protected override void ExitThreadCore()
    {
        shortcutDialog?.Close();
        tray.Visible = false;
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            tray.Dispose();
            menu.Dispose();
            overlay?.Dispose();
            icon.Dispose();
            audio.Dispose();
            window.Dispose();
        }

        base.Dispose(disposing);
    }
}
