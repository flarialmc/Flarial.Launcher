using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Flarial.Runtime.Linux.Engine;
using Flarial.Runtime.Linux.Launch;

namespace Flarial.Runtime.Linux.Injection;

sealed class InjectorCore(IEngine engine) : IInjectorCore
{
    static bool s_extracted;

    public Task EnsureInjectorExeAsync(CancellationToken ct)
    {
        if (s_extracted && File.Exists(Paths.Injector)) return Task.CompletedTask;
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("injector.exe")!;
        using var m = new MemoryStream(); s.CopyTo(m);
        var bytes = m.ToArray();
        if (File.Exists(Paths.Injector) && File.ReadAllBytes(Paths.Injector).AsSpan().SequenceEqual(bytes)) { s_extracted = true; return Task.CompletedTask; }
        Directory.CreateDirectory(Paths.Cache);
        var tmp = Paths.Injector + ".tmp";
        File.WriteAllBytes(tmp, bytes);
        File.SetUnixFileMode(tmp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.Move(tmp, Paths.Injector, true);
        s_extracted = true;
        return Task.CompletedTask;
    }

    static bool WinedbgRunning() => ProcScan.PrefixPids().Any(p => ProcScan.CmdLine(p).FirstOrDefault() is { } a && Path.GetFileName(a.Replace('\\', '/')).StartsWith("winedbg", StringComparison.OrdinalIgnoreCase));

    static async Task<bool> GameSurvives(int pid, TimeSpan time, CancellationToken ct)
    {
        for (var end = DateTime.UtcNow + time; DateTime.UtcNow < end; await Task.Delay(250, ct))
            if (!ProcScan.Alive(pid) || WinedbgRunning()) return false;
        return true;
    }

    static readonly System.Text.RegularExpressions.Regex Swapchain = new(@"dxgi_vk_swap_chain_init: Creating swapchain \((\d+) x (\d+)\)", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Waits until the game has a real window/renderer instead of sleeping a fixed time: vkd3d-proton (VKD3D_DEBUG=info, stderr -> minecraft.log)
    /// logs "Creating swapchain (W x H)" when the game's presentable window exists (the 100x100 one is a dummy). Then a short settle.
    /// <paramref name="cap"/> is the most we wait (the old fixed delay); no signal by then -> inject anyway, as before. False = game died.
    /// </summary>
    static async Task<bool> WaitReady(int pid, TimeSpan cap, CancellationToken ct)
    {
        if (cap <= TimeSpan.Zero) return ProcScan.Alive(pid) && !WinedbgRunning();
        var log = Path.Combine(Paths.Logs, "minecraft.log");
        var pos = LaunchLog.GameLogOffset;
        var settle = TimeSpan.FromMilliseconds(Settings.InjectSettleMs);
        var end = DateTime.UtcNow + cap; DateTime? signal = null; var tail = "";
        while (true)
        {
            if (!ProcScan.Alive(pid) || WinedbgRunning()) return false;
            if (signal is null && pos >= 0)
                try
                {
                    using var f = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    if (f.Length < pos) pos = 0;
                    f.Seek(pos, SeekOrigin.Begin);
                    var text = tail + new StreamReader(f).ReadToEnd();
                    var nl = text.LastIndexOf('\n');
                    pos = f.Length; tail = nl >= 0 ? text[(nl + 1)..] : text; // keep a partial last line for the next read
                    foreach (System.Text.RegularExpressions.Match m in Swapchain.Matches(text))
                        if (int.Parse(m.Groups[1].Value) > 100 && int.Parse(m.Groups[2].Value) > 100) { signal = DateTime.UtcNow; LaunchLog.Phase($"ready signal: swapchain {m.Groups[1]}x{m.Groups[2]} (settling {settle.TotalMilliseconds:0} ms)"); break; }
                }
                catch { }
            if (signal is { } s && DateTime.UtcNow - s >= settle) return true;
            if (DateTime.UtcNow >= end) { LaunchLog.Phase("no ready signal within the wait cap, injecting anyway"); return true; }
            await Task.Delay(250, ct);
        }
    }

    static string ToWine(string path) => path.Length > 2 && path[1] == ':' && path[2] == '\\' ? path : "Z:" + Path.GetFullPath(path).Replace('/', '\\');

    public async Task<InjectResult> InjectAsync(IReadOnlyList<string> libraries, uint gamePid, TimeSpan delay, CancellationToken ct)
    {
        if (libraries.Count == 0) return new(false, -1, "No library to inject.");
        var dll = Path.GetFullPath(libraries[^1]);
        if (!dll.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || !File.Exists(dll)) return new(false, -1, "The DLL is missing or not a .dll file.");
        var pid = (int)gamePid;
        if (!ProcScan.Alive(pid)) return new(false, -1, "Minecraft is not running.");
        if (WinedbgRunning()) return new(false, -1, "Wine crash handler (winedbg) is active.");

        await EnsureInjectorExeAsync(ct);
        if (!await WaitReady(pid, delay, ct)) return new(false, -1, "Minecraft exited before injection.");
        LaunchLog.Phase($"inject start ({libraries.Count - 1} dependencies + client)");

        var env = new Dictionary<string, string?> { ["WINEPREFIX"] = Paths.Prefix, ["WINEESYNC"] = "1", ["WINEFSYNC"] = "1", ["WINEDEBUG"] = "-all" };
        var info = Proc.Info(engine.Wine, [Paths.Injector, "Minecraft.Windows.exe", .. libraries.Select(ToWine)], env);
        var code = await Proc.RunAsync(info, Path.Combine(Paths.Logs, "injector.log"), TimeSpan.FromSeconds(60 + 5 * libraries.Count));
        LaunchLog.Phase($"injector.exe finished (exit {code})");
        if (code != 0) return new(false, code, Describe(code));
        return await GameSurvives(pid, TimeSpan.FromMilliseconds(1500), ct) ? new(true, 0, "Injected.") : new(false, 0, "Minecraft exited or crashed after injection.");
    }

    static string Describe(int code) => code switch
    {
        -1 => "Injector timed out.",
        2 => "Injector usage error.",
        3 => "Game process not found by the injector.",
        4 => "Could not open the game process.",
        5 => "Could not write to the game process.",
        6 => "Could not create the remote thread.",
        7 => "LoadLibrary failed (bad, 32-bit or missing dependencies).",
        8 => "LoadLibrary timed out.",
        _ => $"Injector failed (exit {code})."
    };

    public bool IsModuleLoaded(uint gamePid, string fileNameContains)
    {
        try
        {
            return File.ReadLines($"/proc/{gamePid}/maps").Any(l =>
                l.IndexOf('/') is >= 0 and var i && Path.GetFileName(l[i..].Replace(" (deleted)", "")).Contains(fileNameContains, StringComparison.OrdinalIgnoreCase));
        }
        catch { return false; }
    }
}
