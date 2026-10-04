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

    /// <summary>"1.26.51.1" -> "1.26.51" (VersionItem.Version format).</summary>
    static string VersionOf(string dir) => string.Join('.', Path.GetFileName(dir.TrimEnd('/')).Split('.').Take(3));

    public string? InstalledVersion => ActiveDir() is { } d ? VersionOf(d) : null;
    public IReadOnlyList<string> InstalledVersions => ReleaseDirs().Select(VersionOf).ToList();

    static void PointContentAt(string? dir)
    {
        var link = new FileInfo(Paths.Content);
        if (link.LinkTarget is { } || link.Exists) link.Delete();
        if (dir is { }) File.CreateSymbolicLink(Paths.Content, dir);
    }

    public bool SelectVersion(string version)
    {
        var dir = Paths.GameDir("release", version);
        if (IsRunning || !Backend.Xodus.IsInstalled(dir)) return false;
        PointContentAt(dir);
        Raise();
        return true;
    }

    public bool DeleteVersion(string version)
    {
        var dir = Paths.GameDir("release", version);
        if (IsRunning || !Directory.Exists(dir)) return false;
        var wasActive = ActiveDir() is { } a && VersionOf(a) == version;

        // a dev-seed entry is a symlink into another install: drop only the link, never follow it
        if (new FileInfo(dir).LinkTarget is { }) new FileInfo(dir).Delete();
        else
        {
            var games = Path.GetFullPath(Paths.Games) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(dir).StartsWith(games, StringComparison.Ordinal)) return false;
            Directory.Delete(dir, true);
        }

        if (wasActive || new FileInfo(Paths.Content).LinkTarget is { } t && !Backend.Xodus.IsInstalled(t))
            PointContentAt(ReleaseDirs().FirstOrDefault());
        Raise();
        return true;
    }

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
        if (!Backend.Xodus.IsLoggedIn) throw new InvalidOperationException("Sign in to your Microsoft account in Settings > Accounts to download Minecraft.");
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

        // game first: sign-in / ownership / disk problems fail within seconds, before the multi-GB engine download and the prefix setup
        var dest = Paths.GameDir("release", version.Version);
        var urls = new[] { uri }.Concat(version.DownloadUris.Where(u => u != uri)).ToList();
        GameBuild build = new(Edition.Release, version.Version, version.Version, urls);
        await Backend.Xodus.InstallAsync(build, dest, Rep(wG, false), ct); done += wG;

        if (needProton) { await engine.EnsureProtonAsync(Rep(wP, false), ct); done += wP; }
        if (needUmu) { await engine.EnsureUmuAsync(Rep(wU, false), ct); done += wU; }

        if (needPrefix)
        {
            LinuxPlatform.Notify?.Invoke("Preparing Wine for the first time, this can take a few minutes.");
            var t0 = DateTime.UtcNow; // no real progress from wineboot: creep towards the end of its slice
            var rep = Rep(wPf, true); // Progress<T> posts to the UI thread; the timer thread must not touch the view models
            using var tick = new Timer(_ => rep.Report(1 - Math.Exp(-(DateTime.UtcNow - t0).TotalSeconds / 90)), null, 0, 1000);
            await Backend.Prefix.EnsureSetupAsync(null, ct); done += wPf;
            LinuxPlatform.Notify?.Invoke("Wine is ready.");
        }

        progress((int)(done / total * 100), true);
        await Backend.Prefix.PrepareGameAsync(dest, null, ct);

        // the installed build becomes the selected one
        PointContentAt(dest);
        progress(100, true);
        Raise();
    }

    // ---- launch

    /// <summary>xodus-cli run refuses before Minecraft starts (license/device/sign-in problems); say why instead of only "Launch Failure".</summary>
    static string? LaunchError(string log, long from)
    {
        string text;
        try
        {
            using var f = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            f.Seek(Math.Min(from, f.Length), SeekOrigin.Begin);
            text = new StreamReader(f).ReadToEnd();
        }
        catch { return null; }

        if (text.Contains("device group is full", StringComparison.OrdinalIgnoreCase))
            return "Minecraft cannot start: this Microsoft account has no free Store device slot (ten maximum). Remove old devices at account.microsoft.com/devices/content, then sign in again in Settings > Accounts.";
        if (text.Contains("not entitled to this content", StringComparison.OrdinalIgnoreCase) || text.Contains("package was not found", StringComparison.OrdinalIgnoreCase))
            return "Minecraft cannot start: this Microsoft account does not own Minecraft Bedrock Edition.";
        if (System.Text.RegularExpressions.Regex.IsMatch(text, "unable to initialize credentials|invalid sts token|no user token|not logged in|didn't log in|failed to get exchange ms token", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            return "Minecraft cannot start: the Microsoft sign-in has expired. Sign in again in Settings > Accounts.";
        return null;
    }
    public uint? Launch()
    {
        if (Core.FindGamePid() is { } running) return running;
        if (ActiveDir() is not { } dir) return null;
        try
        {
            if (!Backend.Prefix.IsReady)
            {
                LinuxPlatform.Notify?.Invoke("Preparing Wine for the first time, this can take a few minutes.");
                Backend.Prefix.EnsureSetupAsync(null, CancellationToken.None).GetAwaiter().GetResult();
                LinuxPlatform.Notify?.Invoke("Wine is ready, starting Minecraft.");
            }
            var log = Path.Combine(Paths.Logs, "minecraft.log");
            var logStart = File.Exists(log) ? new FileInfo(log).Length : 0;
            var pid = Core.LaunchAsync(dir, LaunchSettings.Current, CancellationToken.None).GetAwaiter().GetResult();
            if (pid is { }) LastLaunchUtc = DateTime.UtcNow;
            else if (LaunchError(log, logStart) is { } why) LinuxPlatform.Notify?.Invoke(why);
            Raise();
            return pid;
        }
        catch (Exception e)
        {
            try { File.AppendAllText(Path.Combine(Paths.Logs, "launcher.log"), $"{DateTime.Now:s} launch failed: {e}\n"); } catch { }
            LinuxPlatform.Notify?.Invoke("Minecraft could not be started: " + e.Message);
            return null;
        }
    }
}
