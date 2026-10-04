using System;
using System.IO;
using System.Threading;
using Flarial.Runtime.Linux.Injection;
using Flarial.Runtime.Platform;

namespace Flarial.Runtime.Linux;

/// <summary>Injects a DLL into the Wine process hosting Minecraft with `wine injector.exe` against the live prefix.</summary>
public sealed class LinuxInjector : IInjector
{
    static readonly IInjectorCore Core = new InjectorCore(Backend.Engine);
    static string s_last = "Flarial.Client.";

    public bool IsClientRunning => LinuxGameService.Core.FindGamePid() is { } pid && (Core.IsModuleLoaded(pid, "Flarial.Client.") || Core.IsModuleLoaded(pid, s_last));

    public bool Inject(string dllPath, uint processId)
    {
        // the configured delay counts from a launch we did ourselves; an already running game needs none
        var wait = TimeSpan.FromSeconds(Settings.InjectDelaySeconds) - (DateTime.UtcNow - LinuxGameService.LastLaunchUtc);
        if (wait < TimeSpan.Zero) wait = TimeSpan.Zero;
        var r = Core.InjectAsync(dllPath, processId, wait, CancellationToken.None).GetAwaiter().GetResult();
        if (r.Ok) s_last = Path.GetFileName(dllPath);
        else try { File.AppendAllText(Path.Combine(Paths.Logs, "launcher.log"), $"{DateTime.Now:s} inject failed: {r.Message}\n"); } catch { }
        return r.Ok;
    }
}
