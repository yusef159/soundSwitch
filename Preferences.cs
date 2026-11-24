using System.Text.Json;
using Microsoft.Win32;

namespace SoundSwitch;

internal static class Preferences
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "SoundSwitch.OutputSwitcher";
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SoundSwitch", "settings.json");

    internal static Hotkey LoadHotkey()
    {
        try
        {
            var shortcut = JsonSerializer.Deserialize<Hotkey>(File.ReadAllText(SettingsPath));
            if (shortcut.IsValid)
                return shortcut;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            // A missing, inaccessible, or invalid settings file falls back to the default shortcut.
        }

        return Hotkey.Default;
    }

    internal static void SaveHotkey(Hotkey shortcut)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        string temporaryPath = SettingsPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(shortcut));
        File.Move(temporaryPath, SettingsPath, overwrite: true);
    }

    internal static bool StartsWithWindows()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(RunValue) is string;
    }

    internal static void SetStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            key.SetValue(RunValue, "\"" + Application.ExecutablePath + "\"", RegistryValueKind.String);
        else
            key.DeleteValue(RunValue, throwOnMissingValue: false);
    }
}
