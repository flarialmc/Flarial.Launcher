using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Flarial.Runtime.Linux.Engine;
using Flarial.Runtime.Linux.Prefix;
using Flarial.Runtime.Linux.Xbox;
using Flarial.Runtime.Linux.Xodus;

namespace Flarial.Runtime.Linux.Launch;

sealed class LauncherCore(IEngine engine, IXodus xodus, IPrefix prefix, IXboxAuth xbox) : ILauncherCore
{
    const string Exe = "Minecraft.Windows.exe";
    static readonly TimeSpan GameWait = TimeSpan.FromSeconds(120);

    public bool IsRunning => FindGamePid() is { };

    /// <summary>The wine process hosting the game: argv[0] is the NT path of the exe (umu/pressure-vessel wrappers only carry it as an argument).</summary>
    public uint? FindGamePid()
    {
        foreach (var pid in ProcScan.PrefixPids())
            if (ProcScan.CmdLine(pid).FirstOrDefault() is { } a && a.Replace('\\', '/').EndsWith("/" + Exe, StringComparison.OrdinalIgnoreCase)) return (uint)pid;
        return null;
    }

    static string Uid() => File.ReadAllLines("/proc/self/status").First(l => l.StartsWith("Uid:")).Split('\t')[1];

    public async Task<uint?> LaunchAsync(string gameDir, LaunchSettings settings, CancellationToken ct)
    {
        if (IsRunning) throw new InvalidOperationException("Minecraft is already running.");
        if (!File.Exists(Path.Combine(gameDir, Exe))) throw new FileNotFoundException("Game executable not found.", Path.Combine(gameDir, Exe));

        LaunchLog.Begin("launch " + gameDir);
        var mlog = Path.Combine(Paths.Logs, "minecraft.log");
        LaunchLog.GameLogOffset = File.Exists(mlog) ? new FileInfo(mlog).Length : 0;
        LaunchLog.Phase("settings loaded (diagnostics=" + settings.Diagnostics + ")");

        // the Xbox refresh is network only and independent of the prefix work: run both at once
        var xboxTask = Task.Run(async () =>
        {
            try { return await xbox.RefreshAsync(ct); }
            catch (OperationCanceledException) { throw; }
            catch { return new XboxSession(false, null, null, null); }
        }, ct);
        var prep = Task.Run(async () =>
        {
            await LaunchLog.Time("prefix ensure setup", prefix.EnsureSetupAsync(null, ct));
            await LaunchLog.Time("prefix prepare game (cacert/gdk deps/gameinput)", prefix.PrepareGameAsync(gameDir, null, ct));
            LinkContent(gameDir);
            LaunchLog.Phase("content link");
        }, ct);
        var session = await LaunchLog.Time("xbox refresh (parallel)", xboxTask);
        await prep;
        await LaunchLog.Time($"set refresh token (online={session.Online})", prefix.SetRefreshTokenAsync(session.RefreshToken, ct));

        WriteWrapper();
        var env = BuildEnv(session, settings);
        env["FLARIAL_LAUNCH_LOG"] = Path.Combine(Paths.Logs, "launch.log");
        Directory.CreateDirectory(Paths.Logs);
        using var chain = xodus.StartRun(gameDir, Paths.Wrapper, env, Paths.Content, Path.Combine(Paths.Logs, "minecraft.log"));
        LaunchLog.Phase("xodus-cli run started (decrypt + licence check, then wrapper, then umu)");

        var deadline = DateTime.UtcNow + GameWait;
        DateTime? exited = null;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            if (FindGamePid() is { } pid) { LaunchLog.Phase($"game process found (pid {pid})"); return pid; }
            // umu may hand off and exit while wine keeps starting; give it a moment before giving up.
            if (chain.HasExited) { exited ??= DateTime.UtcNow; if (DateTime.UtcNow - exited > TimeSpan.FromSeconds(3)) { LaunchLog.Phase("chain exited, no game process"); return null; } }
            await Task.Delay(150, ct);
        }
        LaunchLog.Phase("timed out waiting for the game process");
        return null;
    }

    /// <summary>Paths.Content -> gameDir (the launch cwd and the NT path the wrapper passes to umu).</summary>
    static void LinkContent(string gameDir)
    {
        var info = new FileInfo(Paths.Content);
        if (info.LinkTarget == gameDir) return;
        if (info.LinkTarget is { }) info.Delete();
        else if (Directory.Exists(Paths.Content) || info.Exists) throw new IOException($"{Paths.Content} is not a symlink.");
        Directory.CreateDirectory(Path.GetDirectoryName(Paths.Content)!);
        File.CreateSymbolicLink(Paths.Content, gameDir);
    }

    static void WriteWrapper()
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("launch-wrapper.sh")!;
        using var m = new MemoryStream(); s.CopyTo(m);
        var bytes = m.ToArray();
        if (File.Exists(Paths.Wrapper) && File.ReadAllBytes(Paths.Wrapper).AsSpan().SequenceEqual(bytes)) return;
        Directory.CreateDirectory(Paths.Run);
        var tmp = Paths.Wrapper + ".tmp";
        File.WriteAllBytes(tmp, bytes);
        File.SetUnixFileMode(tmp, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        File.Move(tmp, Paths.Wrapper, true);
    }

    Dictionary<string, string?> BuildEnv(XboxSession session, LaunchSettings s)
    {
        var steam = Path.Combine(Paths.Home, ".steam", "steam");
        var xauth = new[] { Environment.GetEnvironmentVariable("XAUTHORITY"), Path.Combine(Paths.Home, ".Xauthority"), $"/run/user/{Uid()}/.mutter-Xwaylandauth.0" }
            .FirstOrDefault(p => !string.IsNullOrEmpty(p) && File.Exists(p));
        var vkd3d = (Environment.GetEnvironmentVariable("VKD3D_CONFIG") is { Length: > 0 } c ? c + "," : "") + "force_raw_va_cbv" + (s.RayTracing ? "" : ",nodxr");
        var overrides = "cryptbase=n,b;vrclient=;vrclient_x64=;openvr_api=;wineopenxr=;amd_ags_x64=" +
            (Environment.GetEnvironmentVariable("WINEDLLOVERRIDES") is { Length: > 0 } o ? ";" + o : "");

        Dictionary<string, string?> env = new()
        {
            ["PROTONPATH"] = engine.ProtonDir,
            ["PROTON_VERB"] = "run",
            ["WINEPREFIX"] = Paths.Prefix,
            ["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = Directory.Exists(steam) ? steam : Paths.SteamCompat,
            ["UMU_FOLDERS_PATH"] = Paths.Root,
            ["UMU_RUNTIME_UPDATE"] = "0",
            ["PROTON_USE_WOW64"] = "1",
            ["GAMEID"] = "umu-default",
            ["VKD3D_CONFIG"] = vkd3d,
            ["VKD3D_DEBUG"] = "info",
            ["VKD3D_SHADER_CACHE_PATH"] = Paths.GraphicsCache,
            ["DXVK_SHADER_CACHE_PATH"] = Paths.GraphicsCache,
            ["WINEDLLOVERRIDES"] = overrides,
            ["MICROSOFT_WINDOWSAPPRUNTIME_BOOTSTRAP_INITIALIZE_SHOWUI"] = "0",
            ["MICROSOFT_WINDOWSAPPRUNTIME_BOOTSTRAP_INITIALIZE_FAILFAST"] = "0",
            ["MICROSOFT_WINDOWSAPPRUNTIME_DEPLOYMENT_INITIALIZE_ONERRORSHOWUI"] = "0",
            ["GNUTLS_SYSTEM_PRIORITY_FILE"] = null,
            ["GNUTLS_SYSTEM_PRIORITY_FAIL_ON_INVALID"] = null,
            ["PROTON_ENABLE_WAYLAND"] = "0",
            ["PROTON_PREFER_SDL"] = "1",
            ["FLARIAL_UMU_RUN"] = Paths.UmuRun,
            ["FLARIAL_EXE_NAME"] = Exe,
            ["WINEDEBUG"] = "-all",
        };
        if (xauth is { }) env["XAUTHORITY"] = xauth;
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"))) env["WINE_DISABLE_VULKAN_OPWR"] = "1";
        env["WINEGDK_PREAUTH_DEVICE"] = session.Online && session.DeviceJsonPath is { } dj ? "Z:" + dj.Replace('/', '\\') : null;
        if (s.Diagnostics)
        {
            env["PROTON_LOG"] = "1";
            env["PROTON_LOG_DIR"] = Paths.Logs;
            env["WINEDEBUG"] = "+gdkc,trace-gdkc,+xgameruntime,trace-xgameruntime,fixme-all";
        }
        foreach (var kv in s.CustomEnv.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            if (kv.IndexOf('=') is > 0 and var i) env[kv[..i]] = kv[(i + 1)..];
        return env;
    }

    public async Task KillAsync(CancellationToken ct)
    {
        var env = new Dictionary<string, string?> { ["WINEPREFIX"] = Paths.Prefix };
        try { await Proc.RunAsync(Proc.Info(engine.Wineserver, ["-k"], env), Path.Combine(Paths.Logs, "minecraft.log"), TimeSpan.FromSeconds(15)); } catch { }
        foreach (var (sig, wait) in new[] { (0, 5), (15, 3), (9, 3) })
        {
            if (sig != 0) foreach (var p in ProcScan.PrefixPids()) ProcScan.Signal(p, sig);
            for (var i = 0; i < wait * 4; i++)
            {
                if (ProcScan.PrefixPids().Count == 0) return;
                await Task.Delay(250, ct);
            }
        }
    }
}
