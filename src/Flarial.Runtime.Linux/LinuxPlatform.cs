using Flarial.Runtime.Platform;

namespace Flarial.Runtime.Linux;

public static class LinuxPlatform
{
    /// <summary>Launcher settings/data directory ($XDG_DATA_HOME/Flarial/Launcher); always absolute.</summary>
    public static string LauncherDataDirectory => System.IO.Path.Combine(Paths.DataHome, "Flarial", "Launcher");

    /// <summary>Registers the Linux backend. Call once at startup.</summary>
    public static void Use()
    {
        Platform.Platform.Game = new LinuxGameService();
        Platform.Platform.Injector = new LinuxInjector();
        Platform.Platform.MicrosoftAccount = new LinuxMicrosoftAccount();
        Platform.Platform.Credentials = new LinuxCredentialStore();
    }
}
