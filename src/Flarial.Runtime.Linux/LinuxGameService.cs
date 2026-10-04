using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flarial.Runtime.Linux.Launch;
using Flarial.Runtime.Platform;
using Flarial.Runtime.Versions;

namespace Flarial.Runtime.Linux;

/// <summary>Wine/Proton game backend: installs builds with xodus-cli, launches them through umu + GDK-Proton-xuser.</summary>
public sealed class LinuxGameService : IGameService
{
    internal static readonly ILauncherCore Core = new LauncherCore(Backend.Engine, Backend.Xodus, Backend.Prefix, Backend.Xbox);
    internal static DateTime LastLaunchUtc;

    public bool IsGamingServicesInstalled => true; // Windows-only concept
    public bool RequiresInstalledGame => false;    // installing a version creates the install on Linux
    public bool IsSideloaded => false;

    public bool IsInstalled => InstalledVersions.Count > 0;
    public bool IsRunning => Core.IsRunning;

    static IEnumerable<string> ReleaseDirs()
    {
        var root = Path.Combine(Paths.Games, "release");
        if (!Directory.Exists(root)) yield break;
        foreach (var d in Directory.GetDirectories(root).OrderByDescending(d => d, StringComparer.Ordinal))
            if (Path.GetFileName(d) != "etc" && Backend.Xodus.IsInstalled(d)) yield return d;
    }

    /// <summary>The selected build: the Paths.Content link target when valid, else the newest installed.</summary>
    internal static string? ActiveDir()
    {
        var target = new FileInfo(Paths.Content).LinkTarget;
        if (target is { } && Backend.Xodus.IsInstalled(target)) return target;
        return ReleaseDirs().FirstOrDefault();
    }

    public string? InstalledVersion => ActiveDir() is { } d ? Path.GetFileName(d) : null;
    public IReadOnlyList<string> InstalledVersions => ReleaseDirs().Select(Path.GetFileName).ToList()!;

    // ---- status events (polled: the game can start/stop outside the launcher)
    Action? _changed; SynchronizationContext? _context; Timer? _timer; string _last = "";

    public event Action? StatusChanged
    {
        add
        {
            _changed += value; _context ??= SynchronizationContext.Current;
            _timer ??= new(_ => Poll(), null, 2000, 2000);
        }
        remove { _changed -= value; }
    }

    string State() => $"{InstalledVersion}|{IsRunning}";

    void Poll() { var s = State(); if (s != _last) { _last = s; Raise(); } }

    void Raise()
    {
        var h = _changed; if (h is null) return;
        if (_context is { } c) c.Post(_ => h(), null); else h();
    }

    // ---- install
    public async Task InstallAsync(VersionItem version, string uri, Action<int, bool> progress)
    {
        var ct = CancellationToken.None;
        var engine = Backend.Engine;
        var needProton = !engine.IsProtonReady; var needUmu = !engine.IsUmuReady; var needPrefix = !Backend.Prefix.IsReady;

        // work units: engine download dominates; the game download is the rest
        double wP = needProton ? 30 : 0, wU = needUmu ? 3 : 0, wX = 3, wPf = needPrefix ? 12 : 0, wG = 45, wPrep = 3;
        var total = wP + wU + wX + wPf + wG + wPrep; double done = 0;
        void Step(double w, bool installing, double f) => progress((int)Math.Min(100, (done + w * f) / total * 100), installing);
        IProgress<double> Rep(double w, bool installing) => new Progress<double>(f => Step(w, installing, f));

        progress(0, false);
        await Backend.Xodus.EnsureInstalledAsync(Rep(wX, false), ct); done += wX;
        if (needProton) { await engine.EnsureProtonAsync(Rep(wP, false), ct); done += wP; }
        if (needUmu) { await engine.EnsureUmuAsync(Rep(wU, false), ct); done += wU; }

        if (!Backend.Xodus.IsLoggedIn && !await new LinuxMicrosoftAccount().SignInAsync())
            throw new InvalidOperationException("Sign in with the Microsoft account that owns Minecraft to download it.");

        if (needPrefix) { await Backend.Prefix.EnsureSetupAsync(Rep(wPf, true), ct); done += wPf; }

        var dest = Paths.GameDir("release", version.Version);
        var urls = new[] { uri }.Concat(version.DownloadUris.Where(u => u != uri)).ToList();
        GameBuild build = new(Edition.Release, version.Version, version.Version, urls);
        await Backend.Xodus.InstallAsync(build, dest, Rep(wG, false), ct); done += wG;

        progress((int)(done / total * 100), true);
        await Backend.Prefix.PrepareGameAsync(dest, null, ct);

        // the installed build becomes the selected one
        var link = new FileInfo(Paths.Content);
        if (link.LinkTarget is { } || link.Exists) link.Delete();
        File.CreateSymbolicLink(Paths.Content, dest);
        progress(100, true);
        Raise();
    }

    // ---- launch
    public uint? Launch()
    {
        if (Core.FindGamePid() is { } running) return running;
        if (ActiveDir() is not { } dir) return null;
        try
        {
            var pid = Core.LaunchAsync(dir, LaunchSettings.Current, CancellationToken.None).GetAwaiter().GetResult();
            if (pid is { }) LastLaunchUtc = DateTime.UtcNow;
            Raise();
            return pid;
        }
        catch (Exception e)
        {
            try { File.AppendAllText(Path.Combine(Paths.Logs, "launcher.log"), $"{DateTime.Now:s} launch failed: {e}\n"); } catch { }
            return null;
        }
    }
}
