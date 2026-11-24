# SoundSwitch

SoundSwitch is a Windows 10/11 tray app for switching playback devices. Press **Ctrl+Alt+F11** or left-click the tray icon to cycle through active outputs in alphabetical order. The overlay shows the selected device for 1.5 seconds without taking keyboard focus.

Right-click the tray icon to select an output directly, change the global keyboard shortcut, enable **Start with Windows**, or exit. Windows may place the icon in the hidden-icons area. The shortcut can use Ctrl, Alt, Shift, or Win with a letter, number, or function key. Windows reserves some combinations, including F12. If a shortcut is unavailable, the tray menu still works and lets you choose another one.

## Build and install

The standalone `SoundSwitch.exe` includes the .NET Desktop Runtime. You can double-click it from any folder. For **Start with Windows**, move it to a permanent location before enabling the option.

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) to build from source. From the source directory in PowerShell, publish and copy the executable to a per-user location.

```powershell
dotnet publish .\SoundSwitch.csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=embedded -o .\bin\standalone\win-x64
$installPath = Join-Path $env:LOCALAPPDATA 'Programs\SoundSwitch'
New-Item -ItemType Directory -Path $installPath -Force | Out-Null
Copy-Item '.\bin\standalone\win-x64\SoundSwitch.exe' $installPath -Force
Start-Process -FilePath (Join-Path $installPath 'SoundSwitch.exe')
```

For native ARM64 or x86, replace each occurrence of `win-x64` with `win-arm64` or `win-x86`. See Microsoft's [.NET on Windows documentation](https://learn.microsoft.com/dotnet/core/install/windows) for supported Windows versions. The app runs under your Windows account and requires no elevation. To update, exit before replacing the executable. To uninstall, turn off **Start with Windows**, exit, then delete the executable. To remove the saved shortcut, also delete `%LOCALAPPDATA%\SoundSwitch`.

## Implementation

- `AudioOutputs.cs` enumerates active Windows Core Audio render endpoints and subscribes to endpoint notifications. It switches the default playback endpoint for the multimedia, console, and communications roles. Apps explicitly routed to a particular output may keep using that device.
- `AudioInterop.cs` defines the COM interfaces. Reading endpoints and receiving notifications use the documented MMDevice API. Setting the Windows default uses the undocumented `IPolicyConfig::SetDefaultEndpoint` interface.
- `Hotkey.cs` registers the global shortcut with Windows and queues Core Audio notifications on the WinForms message thread. The tray invalidates its device cache when a notification arrives. The app has no background polling loop, keyboard hook, telemetry, or third-party packages.
- `Preferences.cs` stores the shortcut in a local JSON file and updates the current user's Windows startup registration. Startup is off by default. Windows starts the app at user sign-in when enabled.

## Verify on Windows

1. With two active playback devices, press **Ctrl+Alt+F11** twice. Check the selected device in Windows Sound settings and confirm the overlay leaves focus on the current app.
2. Connect or disconnect a playback device while SoundSwitch is running. Reopen the tray menu and confirm the device list and cycle reflect the change.
3. Change the shortcut, exit, and reopen the app. Confirm that the new shortcut works, then enable **Start with Windows** and sign out and back in to verify startup.
