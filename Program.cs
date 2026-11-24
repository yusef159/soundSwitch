namespace SoundSwitch;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var instance = new Mutex(false, @"Local\SoundSwitch.OutputSwitcher", out bool firstInstance);
        if (!firstInstance)
            return;

        ApplicationConfiguration.Initialize();
        using var app = new TrayApplication();
        Application.Run(app);
    }
}
