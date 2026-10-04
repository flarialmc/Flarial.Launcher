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
    public Task EnsureInjectorExeAsync(CancellationToken ct)
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("injector.exe")!;
        using var m = new MemoryStream(); s.CopyTo(m);
        var bytes = m.ToArray();
        if (File.Exists(Paths.Injector) && File.ReadAllBytes(Paths.Injector).AsSpan().SequenceEqual(bytes)) return Task.CompletedTask;
        Directory.CreateDirectory(Paths.Cache);
        var tmp = Paths.Injector + ".tmp";
        File.WriteAllBytes(tmp, bytes);
        File.SetUnixFileMode(tmp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.Move(tmp, Paths.Injector, true);
        return Task.CompletedTask;
    }

    static bool WinedbgRunning() => ProcScan.PrefixPids().Any(p => ProcScan.CmdLine(p).FirstOrDefault() is { } a && Path.GetFileName(a.Replace('\\', '/')).StartsWith("winedbg", StringComparison.OrdinalIgnoreCase));

    static async Task<bool> GameSurvives(int pid, TimeSpan time, CancellationToken ct)
    {
        for (var end = DateTime.UtcNow + time; DateTime.UtcNow < end; await Task.Delay(250, ct))
            if (!ProcScan.Alive(pid) || WinedbgRunning()) return false;
        return true;
    }

    public async Task<InjectResult> InjectAsync(string dllPath, uint gamePid, TimeSpan delay, CancellationToken ct)
    {
        var dll = Path.GetFullPath(dllPath);
        if (!dll.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || !File.Exists(dll)) return new(false, -1, "The DLL is missing or not a .dll file.");
        var pid = (int)gamePid;
        if (!ProcScan.Alive(pid)) return new(false, -1, "Minecraft is not running.");
        if (WinedbgRunning()) return new(false, -1, "Wine crash handler (winedbg) is active.");

        await EnsureInjectorExeAsync(ct);
        if (!await GameSurvives(pid, delay, ct)) return new(false, -1, "Minecraft exited before injection.");

        var env = new Dictionary<string, string?> { ["WINEPREFIX"] = Paths.Prefix, ["WINEESYNC"] = "1", ["WINEFSYNC"] = "1", ["WINEDEBUG"] = "-all" };
        var info = Proc.Info(engine.Wine, [Paths.Injector, "Z:" + dll.Replace('/', '\\'), "Minecraft.Windows.exe"], env);
        var code = await Proc.RunAsync(info, Path.Combine(Paths.Logs, "injector.log"), TimeSpan.FromSeconds(60));
        if (code != 0) return new(false, code, Describe(code));
        return await GameSurvives(pid, TimeSpan.FromSeconds(3), ct) ? new(true, 0, "Injected.") : new(false, 0, "Minecraft exited or crashed after injection.");
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
