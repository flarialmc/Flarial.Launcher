// Wine prefix + game-directory fixups. Behaviour ported from BedrockOnLinux (MIT, see Native/LICENSE-BedrockOnLinux);
// XCurl.dll / libHttpClient.GDK.dll come from minecraft-linux/mcpelauncher-gdk-dependencies (MIT, see LICENSE-mcpelauncher-gdk-dependencies).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Flarial.Runtime.Linux.Engine;

namespace Flarial.Runtime.Linux.Prefix;

internal sealed class PrefixManager(IEngine engine) : IPrefix
{
    const int SetupVersion = 1; // bump to re-apply registry tweaks
    const string GdkDepsUrl = "https://github.com/minecraft-linux/mcpelauncher-gdk-dependencies/releases/download/v0.0.0";
    const string CacertUrl = "https://curl.se/ca/cacert.pem";
    const string WineGdkKey = @"HKEY_LOCAL_MACHINE\Software\Wine\WineGDK";

    static readonly (string Name, string Sha)[] GdkDeps =
    [
        ("libHttpClient.GDK.dll", "718f0c6842c8e35d7cac5c18ec025404c83be03ec035f0d3904e70d6508b9e24"),
        ("XCurl.dll", "28443194e9860733f24294219bcb45226677636f9721d4bf145094d977f7161f"),
    ];

    const string Tweaks = """
        Windows Registry Editor Version 5.00

        [HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\OEM]
        "ConsoleMode"=dword:00000008

        [HKEY_LOCAL_MACHINE\Software\Microsoft\WindowsRuntime\ActivatableClassId\Microsoft.Windows.Storage.Pickers.FileOpenPicker]
        "DllPath"="C:\\windows\\system32\\windows.storage.dll"

        [HKEY_LOCAL_MACHINE\Software\Microsoft\WindowsRuntime\ActivatableClassId\Windows.Storage.Pickers.FileSavePicker]
        "DllPath"="C:\\windows\\system32\\windows.storage.dll"

        [HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Internet Settings\WinHttp]
        "DefaultSecureProtocols"=dword:000009a0

        [HKEY_LOCAL_MACHINE\Software\Microsoft\SchannelTLS\Protocols\TLS 1.3\Client]
        "DisabledByDefault"=dword:00000001

        [HKEY_CURRENT_USER\Environment]
        "MICROSOFT_WINDOWSAPPRUNTIME_BOOTSTRAP_INITIALIZE_SHOWUI"="0"
        "MICROSOFT_WINDOWSAPPRUNTIME_BOOTSTRAP_INITIALIZE_FAILFAST"="0"
        "MICROSOFT_WINDOWSAPPRUNTIME_DEPLOYMENT_INITIALIZE_ONERRORSHOWUI"="0"
        """;

    static string Pfx => Paths.Prefix;
    static string Log => Path.Combine(Paths.Logs, "prefix.log");
    static string Marker => Path.Combine(Pfx, ".flarial-prefix");
    string Want => $"{SetupVersion}|{engine.Revision}";

    static bool Valid =>
        Directory.Exists(Path.Combine(Pfx, "drive_c", "windows", "system32")) && StartsWithReg("system.reg") && StartsWithReg("user.reg");

    static bool StartsWithReg(string f)
    {
        try
        {
            using var s = File.OpenRead(Path.Combine(Pfx, f));
            var b = new byte[22]; var n = s.Read(b);
            return Encoding.ASCII.GetString(b, 0, n) == "WINE REGISTRY Version ";
        }
        catch { return false; }
    }

    public bool IsReady => Valid && File.Exists(Marker) && File.ReadAllText(Marker).Trim() == Want;

    // ---- process helpers -------------------------------------------------------------------------------------------

    /// <summary>Headless env for wine run directly against the prefix (no window, no menu entries).</summary>
    Dictionary<string, string?> WineEnv() => new()
    {
        ["WINEPREFIX"] = Pfx, ["WINEDEBUG"] = "-all", ["WINEDLLOVERRIDES"] = "winemenubuilder.exe=d",
        ["DISPLAY"] = null, ["WAYLAND_DISPLAY"] = null, ["XAUTHORITY"] = null, ["SDL_VIDEODRIVER"] = "dummy",
    };

    // ponytail: cancelling stops waiting, not the child; wine children finish on their own.
    static async Task<int> Run(System.Diagnostics.ProcessStartInfo i, TimeSpan timeout, CancellationToken ct) =>
        await Proc.RunAsync(i, Log, timeout).WaitAsync(ct);

    async Task Wine(IEnumerable<string> args, TimeSpan timeout, CancellationToken ct)
    {
        var code = await Run(Proc.Info(engine.Wine, args, WineEnv()), timeout, ct);
        // let the prefix wineserver exit so the registry hits disk
        await Run(Proc.Info(engine.Wineserver, ["-w"], WineEnv()), TimeSpan.FromSeconds(60), ct);
        if (code != 0) throw new IOException($"wine {string.Join(' ', args)} failed ({code}); see {Log}");
    }

    Dictionary<string, string?> UmuEnv(bool nativeCryptbase) => new()
    {
        ["PROTONPATH"] = engine.ProtonDir, ["PROTON_VERB"] = "run", ["WINEPREFIX"] = Pfx,
        ["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = Paths.SteamCompat, ["UMU_FOLDERS_PATH"] = Paths.Root,
        ["UMU_RUNTIME_UPDATE"] = "0", ["PROTON_USE_WOW64"] = "1", ["GAMEID"] = "umu-default",
        ["DISPLAY"] = null, ["WAYLAND_DISPLAY"] = null, ["XAUTHORITY"] = null, ["PROTON_ENABLE_WAYLAND"] = null,
        ["SDL_VIDEODRIVER"] = "dummy", ["WINEDEBUG"] = "-all",
        ["WINEDLLOVERRIDES"] = (nativeCryptbase ? "cryptbase=n,b" : "cryptbase=b") + ";winevulkan=;dxgi=;d3d11=;d3d12=",
    };

    string EngineDlls(string arch) => Path.Combine(engine.ProtonDir, "files", "lib", "wine", arch);

    // ---- IPrefix ---------------------------------------------------------------------------------------------------

    public async Task EnsureSetupAsync(IProgress<double>? progress, CancellationToken ct)
    {
        Paths.Ensure();
        if (IsReady) { progress?.Report(1); return; }
        Directory.CreateDirectory(Pfx);

        if (!Valid)
        {
            // first wineboot can abort on advapi32.SystemFunction036 -> cryptbase; seed native cryptbase and retry once
            for (var attempt = 0; attempt < 2 && !Valid; attempt++)
            {
                if (attempt == 1) SeedCryptbase();
                await Run(Proc.Info("python3", [engine.UmuRun, "wineboot", "-u"], UmuEnv(attempt == 1), Paths.Root), TimeSpan.FromMinutes(30), ct);
                progress?.Report(0.6);
            }
            if (!Valid) throw new IOException($"wineboot did not create a prefix; see {Log}");
        }
        else RefreshEngineDlls();
        // wineboot via umu may leave the wineserver running; wait for it before we poke the registry
        await Run(Proc.Info(engine.Wineserver, ["-w"], WineEnv()), TimeSpan.FromSeconds(60), ct);

        await ImportRegAsync(Tweaks, ct);
        File.WriteAllText(Marker, Want);
        progress?.Report(1);
    }

    void SeedCryptbase()
    {
        var sys32 = Path.Combine(Pfx, "drive_c", "windows", "system32");
        Directory.CreateDirectory(sys32);
        File.Copy(Path.Combine(EngineDlls("x86_64-windows"), "cryptbase.dll"), Path.Combine(sys32, "cryptbase.dll"), true);
    }

    /// <summary>Engine changed: re-copy its builtin DLLs over the prefix's (saves live elsewhere and are untouched).</summary>
    void RefreshEngineDlls()
    {
        foreach (var (arch, dst) in new[] { ("x86_64-windows", "system32"), ("i386-windows", "syswow64") })
        {
            var to = Path.Combine(Pfx, "drive_c", "windows", dst);
            if (!Directory.Exists(EngineDlls(arch)) || !Directory.Exists(to)) continue;
            foreach (var f in Directory.EnumerateFiles(EngineDlls(arch)))
                File.Copy(f, Path.Combine(to, Path.GetFileName(f)), true);
        }
    }

    async Task ImportRegAsync(string reg, CancellationToken ct)
    {
        var file = Path.Combine(Paths.Run, "prefix-" + Guid.NewGuid().ToString("N") + ".reg");
        Directory.CreateDirectory(Paths.Run);
        await using (var fs = new FileStream(file, new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite }))
            await fs.WriteAsync(new UTF8Encoding(false).GetBytes(reg.Replace("\r", "").Replace("\n", "\r\n")), ct);
        try { await Wine(["reg", "import", WinPath(file)], TimeSpan.FromMinutes(3), ct); }
        finally { File.Delete(file); }
    }

    static string WinPath(string unix) => "Z:" + unix.Replace('/', '\\');

    public async Task SetRefreshTokenAsync(string? token, CancellationToken ct)
    {
        var value = token is null ? "-" : "\"" + token.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        await ImportRegAsync($"Windows Registry Editor Version 5.00\n\n[{WineGdkKey}]\n\"RefreshToken\"={value}\n", ct);
    }

    public async Task PrepareGameAsync(string gameDir, IProgress<double>? progress, CancellationToken ct)
    {
        // CA bundle (Proton needs it next to the game, else all TLS fails); refreshed every run
        var cacert = Path.Combine(Paths.Cache, "cacert.pem");
        if (!File.Exists(cacert)) await Download.FileAsync(CacertUrl, cacert, null);
        foreach (var b in new[] { gameDir, Path.GetDirectoryName(Path.GetFullPath(gameDir).TrimEnd('/'))! })
        {
            var crt = Path.Combine(b, "etc", "ssl", "certs", "ca-bundle.crt");
            Directory.CreateDirectory(Path.GetDirectoryName(crt)!);
            File.Copy(cacert, crt, true);
        }
        progress?.Report(0.2);

        // XCurl.dll / libHttpClient.GDK.dll swap, original kept as *.flarial-orig
        foreach (var (name, sha) in GdkDeps)
        {
            var cached = Path.Combine(Paths.Cache, "gdkdeps-" + name);
            await Download.FileAsync($"{GdkDepsUrl}/{name}", cached, sha);
            var dst = Path.Combine(gameDir, name);
            if (File.Exists(dst) && await Download.Sha256Async(dst) == sha) continue;
            if (File.Exists(dst) && !File.Exists(dst + ".flarial-orig") && !File.Exists(dst + ".bol-orig")) File.Copy(dst, dst + ".flarial-orig");
            File.Copy(cached, dst, true);
        }
        progress?.Report(0.4);

        await InstallGameInputAsync(Path.Combine(gameDir, "Installers", "GameInputRedist.msi"), ct);
        progress?.Report(1);
    }

    async Task InstallGameInputAsync(string msi, CancellationToken ct)
    {
        if (!File.Exists(msi) || !Valid) return;
        var sha = await Download.Sha256Async(msi);
        var done = Path.Combine(Pfx, ".flarial-gameinput");
        var dll = Path.Combine(Pfx, "drive_c", "Program Files", "Microsoft GameInput", "x64", "GameInputRedist.dll");
        if (File.Exists(done) && File.ReadAllText(done).Trim() == sha && File.Exists(dll)) return;
        await Wine(["msiexec", "/i", WinPath(msi), "/qn"], TimeSpan.FromMinutes(3), ct);
        if (!File.Exists(dll)) throw new IOException($"GameInput redist install produced no GameInputRedist.dll; see {Log}");
        // the MSI places the files but not the loader's RedistDir
        await ImportRegAsync("""
            Windows Registry Editor Version 5.00

            [HKEY_LOCAL_MACHINE\Software\Microsoft\GameInput]
            "RedistDir"="C:\\Program Files\\Microsoft GameInput\\x64"

            [HKEY_LOCAL_MACHINE\Software\Wow6432Node\Microsoft\GameInput]
            "RedistDir"="C:\\Program Files\\Microsoft GameInput\\x64"
            """, ct);
        File.WriteAllText(done, sha);
    }
}
