using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    /// <summary>Imports are resolved inside the prefix's system directory.</summary>
    public string SystemDirectory => @"C:\windows\system32";

    static bool IsApiSet(string path)
    {
        var n = path[(path.LastIndexOfAny(['\\', '/']) + 1)..];
        return n.StartsWith("api-ms-", StringComparison.OrdinalIgnoreCase) || n.StartsWith("ext-ms-", StringComparison.OrdinalIgnoreCase);
    }

    public bool Inject(IReadOnlyList<string> libraries, uint processId)
    {
        var dllPath = libraries[^1];
        // the configured delay counts from a launch we did ourselves; an already running game needs none
        var wait = TimeSpan.FromSeconds(Settings.InjectDelaySeconds) - (DateTime.UtcNow - LinuxGameService.LastLaunchUtc);
        if (wait < TimeSpan.Zero) wait = TimeSpan.Zero;
        // api-ms-win-* / ext-ms-win-* are virtual api-set names the loader resolves itself: there is no such file in system32, so loading
        // them by path always fails (upstream Windows has stubs on disk). Skip them; the client's real dependencies are still pre-loaded.
        var last = libraries.Count - 1;
        libraries = libraries.Where((l, i) => i == last || !IsApiSet(l)).ToList();
        var r = Core.InjectAsync(libraries, processId, wait, CancellationToken.None).GetAwaiter().GetResult();
        if (r.Ok) s_last = Path.GetFileName(dllPath);
        else try { File.AppendAllText(Path.Combine(Paths.Logs, "launcher.log"), $"{DateTime.Now:s} inject failed: {r.Message}\n"); } catch { }
        return r.Ok;
    }
}
