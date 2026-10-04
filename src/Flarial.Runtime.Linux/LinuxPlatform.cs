using System;
using Flarial.Runtime.Platform;

namespace Flarial.Runtime.Linux;

public static class LinuxPlatform
{
    /// <summary>Launcher settings/data directory ($XDG_DATA_HOME/Flarial/Launcher); always absolute.</summary>
    public static string LauncherDataDirectory => System.IO.Path.Combine(Paths.DataHome, "Flarial", "Launcher");

    /// <summary>Flarial client data folder as the game sees it (%LOCALAPPDATA%\\Flarial\\Client inside the Wine prefix); created on demand.</summary>
    public static string ClientDirectory => System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Paths.Prefix, "drive_c", "users", "steamuser", "AppData", "Local", "Flarial", "Client")).FullName;

    /// <summary>UI hook for backend messages (set by the launcher; shown as a notification).</summary>
    public static Action<string>? Notify { get; set; }

    /// <summary>Dev checks (--selftest-engine / --selftest-xodus); returns the exit code.</summary>
    public static System.Threading.Tasks.Task<int> SelfTestAsync(string name) => name switch
    {
        "engine" => Engine.EngineSelfTest.RunAsync(),
        "xodus" => Xodus.XodusSelfTest.RunAsync(),
        _ => System.Threading.Tasks.Task.FromResult(2)
    };

    /// <summary>Registers the Linux backend. Call once at startup.</summary>
    public static void Use()
    {
        Paths.Ensure(); // creates the data dirs and applies the dev seed (FLARIAL_LINUX_SEED_FROM) before anything scans for installed games
        Platform.Platform.Game = new LinuxGameService();
        Platform.Platform.Injector = new LinuxInjector();
        Platform.Platform.MicrosoftAccount = new LinuxMicrosoftAccount();
        Platform.Platform.Credentials = new LinuxCredentialStore();
    }
}
