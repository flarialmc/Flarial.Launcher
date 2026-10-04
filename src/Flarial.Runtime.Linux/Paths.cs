using System;
using System.IO;
using System.Linq;

namespace Flarial.Runtime.Linux;

/// <summary>All Linux backend locations. Root = $XDG_DATA_HOME/Flarial/Linux.</summary>
static class Paths
{
    /// <summary>XDG data home; never relative (an unset HOME used to make the launcher write into the cwd).</summary>
    public static string DataHome
    {
        get
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (!string.IsNullOrEmpty(xdg) && Path.IsPathRooted(xdg)) return xdg;
            var home = Home;
            if (!string.IsNullOrEmpty(home)) return Path.Combine(home, ".local", "share");
            return Path.Combine(Path.GetTempPath(), "flarial-" + Environment.UserName);
        }
    }

    public static string Home
    {
        get
        {
            var home = Environment.GetEnvironmentVariable("HOME");
            if (string.IsNullOrEmpty(home) || !Path.IsPathRooted(home)) home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return home;
        }
    }

    public static string Root { get; } = Directory.CreateDirectory(Path.Combine(DataHome, "Flarial", "Linux")).FullName;

    static string Dir(params string[] parts) => Path.Combine([Root, .. parts]);

    public static string Cache => Dir("cache");
    public static string Logs => Dir("logs");
    public static string Run => Dir("run");
    public static string ProtonDir => Dir("proton");
    public static string Engine => Dir("proton", "GDK-Proton-xuser");
    public static string EngineRev => Dir("proton", "engine.rev");
    public static string Wine => Path.Combine(Engine, "files", "bin", "wine");
    public static string Wineserver => Path.Combine(Engine, "files", "bin-wow64", "wineserver");
    public static string UmuDir => Dir("umu");
    public static string UmuRun => Dir("umu", "umu-run");
    public static string XodusDir => Dir("xodus");
    public static string XodusBin => Dir("xodus", "xodus-cli");
    public static string XodusHome => Dir("xodus-home");
    public static string Prefix => Dir("compatdata", "pfx");
    public static string Games => Dir("games");
    public static string Content => Dir("content");
    public static string MsaToken => Dir("msa", "token.json");
    public static string PreauthDir => Dir("winegdk-preauth");
    public static string DeviceJson => Dir("winegdk-preauth", "device.json");
    public static string GraphicsCache => Dir("graphics-cache");
    public static string SteamCompat => Dir("steamcompat");
    public static string Settings => Dir("settings.json");
    public static string Wrapper => Dir("run", "launch-wrapper.sh");
    public static string Injector => Dir("cache", "injector.exe");

    public static string GameDir(string edition, string version) => Dir("games", edition, version);

    public static void Ensure()
    {
        foreach (var d in new[] { Cache, Logs, Run, ProtonDir, Games, Dir("compatdata"), GraphicsCache, SteamCompat })
            Directory.CreateDirectory(d);
        foreach (var d in new[] { Dir("msa"), PreauthDir, XodusHome, GraphicsCache })
        {
            Directory.CreateDirectory(d);
            File.SetUnixFileMode(d, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        SeedFromExisting();
    }

    /// <summary>
    /// Dev only: FLARIAL_LINUX_SEED_FROM=&lt;BedrockOnLinux dir&gt; links an existing engine/umu/xodus/game and copies logins once
    /// so multi-GB downloads are not repeated. The source install is only read.
    /// </summary>
    static void SeedFromExisting()
    {
        if (Environment.GetEnvironmentVariable("FLARIAL_LINUX_SEED_FROM") is not { Length: > 0 } src) return;
        var marker = Dir(".seeded");
        if (File.Exists(marker)) return;

        void Link(string from, string to)
        {
            if (!Path.Exists(from) || Path.Exists(to) || Directory.Exists(to)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.CreateSymbolicLink(to, from);
        }
        void Copy(string from, string to, bool overwrite = false)
        {
            if (!File.Exists(from) || (File.Exists(to) && !overwrite)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(from, to);
            File.SetUnixFileMode(to, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        Link(Path.Combine(src, "proton", "GDK-Proton-xuser"), Engine);
        if (File.Exists(Path.Combine(Engine, "files", "bin", "wine")) && Json.Field(Path.Combine(Engine, "engine-manifest.json"), "build_rev") is { } rev)
            File.WriteAllText(EngineRev, rev);
        Link(Path.Combine(src, "umu"), UmuDir);
        Link(Path.Combine(src, "xodus"), XodusDir);
        foreach (var edition in new[] { "release", "preview" })
        {
            var root = Path.Combine(src, "games", edition);
            if (!Directory.Exists(root)) continue;
            foreach (var d in Directory.GetDirectories(root).Where(d => Path.GetFileName(d) != "etc"))
            {
                var parts = Path.GetFileName(d).Split('.');
                Link(d, GameDir(edition, string.Join('.', parts.Take(3))));
            }
        }
        foreach (var f in new[] { "cacert.pem", "gdkdeps-libHttpClient.GDK.dll", "gdkdeps-XCurl.dll" })
            Copy(Path.Combine(src, "cache", f), Path.Combine(Cache, f));

        // logins: private copies, so the source install's tokens are never touched by us
        foreach (var f in Directory.Exists(Path.Combine(src, "xodus-home")) ? Directory.GetFiles(Path.Combine(src, "xodus-home"), ".xodus*") : [])
            Copy(f, Path.Combine(XodusHome, Path.GetFileName(f)), true); // seeding means "use that account": a keyring from a fresh launcher login would register a new Store device (and fail when the group is full)
        Copy(Path.Combine(src, "msa", "token.json"), MsaToken);
        foreach (var f in new[] { "device-key.pem", "device-id.txt" })
            Copy(Path.Combine(src, "winegdk-preauth", f), Path.Combine(PreauthDir, f));
        File.WriteAllText(marker, src);
    }
}
