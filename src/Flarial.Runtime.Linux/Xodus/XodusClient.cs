using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Flarial.Runtime.Linux.Xodus;

/// <summary>Drives the downloaded xodus-cli (GPL-3.0, subprocess only; see NOTICE.txt). Behavior ported from BedrockOnLinux (MIT).</summary>
internal sealed partial class XodusClient : IXodus
{
    const string Rev = "64d39eb87a56-p5";
    const string CliUrl = "https://github.com/Wyze3306/BedrockOnLinux/releases/download/xodus-" + Rev + "/xodus-cli-" + Rev + ".tar.gz";
    const string CliSha = "bb507341bcd2eb56406e9aafab2a58978510b396df5d51a577bb0ba08560f8d8";
    const string GdkLinksUrl = "https://raw.githubusercontent.com/MinecraftBedrockArchiver/GdkLinks/master/urls.json";
    const string PackageCache = ".xodus-streaming.msixvc";
    const string Exe = "Minecraft.Windows.exe";
    static readonly TimeSpan GdkLinksTtl = TimeSpan.FromHours(12);
    static readonly TimeSpan StallLimit = TimeSpan.FromMinutes(10);

    static string Log => Path.Combine(Paths.Logs, "xodus.log");
    static string Keyring => Path.Combine(Paths.XodusHome, ".xodus-keyring.ron");
    static bool Seeded => File.Exists(Path.Combine(Paths.Root, ".seeded"));

    static string ContentId(Edition e) => e == Edition.Release ? "7792d9ce-355a-493c-afbd-768f4a77c3b0" : "98bd2335-9b01-4e4c-bd05-ccc01614078b";

    // ---------------------------------------------------------------- binary

    public async Task EnsureInstalledAsync(IProgress<double>? progress, CancellationToken ct)
    {
        var revFile = Path.Combine(Paths.XodusDir, ".rev");
        if (File.Exists(Paths.XodusBin) && (Seeded || File.Exists(revFile) && File.ReadAllText(revFile).Trim() == Rev)) { progress?.Report(1); return; }

        Directory.CreateDirectory(Paths.Cache);
        var tar = Path.Combine(Paths.Cache, $"xodus-cli-{Rev}.tar.gz");
        await Download.FileAsync(CliUrl, tar, CliSha, (d, t) => progress?.Report(t > 0 ? 0.9 * d / t : 0));
        ct.ThrowIfCancellationRequested();

        var tmp = Paths.XodusDir + ".new";
        if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
        Directory.CreateDirectory(tmp);
        if (await Proc.RunAsync(Proc.Info("tar", ["-xzf", tar, "-C", tmp]), Log, TimeSpan.FromMinutes(2)) != 0 || !File.Exists(Path.Combine(tmp, "xodus-cli")))
            throw new IOException("could not extract xodus-cli");
        File.WriteAllText(Path.Combine(tmp, ".rev"), Rev);
        File.SetUnixFileMode(Path.Combine(tmp, "xodus-cli"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        // a seeded dev install is a symlink to somebody else's directory: only the link is removed
        if (new FileInfo(Paths.XodusDir).LinkTarget is { }) File.Delete(Paths.XodusDir);
        else if (Directory.Exists(Paths.XodusDir)) Directory.Delete(Paths.XodusDir, true);
        Directory.Move(tmp, Paths.XodusDir);
        progress?.Report(1);
    }

    static void RequireBinary()
    {
        if (!File.Exists(Paths.XodusBin)) throw new FileNotFoundException("xodus-cli is not installed", Paths.XodusBin);
    }

    // ---------------------------------------------------------------- environment

    /// <summary>xodus-cli runs with HOME/XDG_* inside Paths.XodusHome so its keyring/device registration persists and never touches the real HOME.</summary>
    static void ApplyXodusHome(IDictionary<string, string?> env)
    {
        var h = Paths.XodusHome;
        env["HOME"] = h;
        env["XDG_DATA_HOME"] = Path.Combine(h, ".local", "share");
        env["XDG_CACHE_HOME"] = Path.Combine(h, ".cache");
        env["XDG_STATE_HOME"] = Path.Combine(h, ".local", "state");
        // XDG_CONFIG_HOME is left alone as in the original (xodus keeps its keyring directly in HOME)
    }

    static Dictionary<string, string?> CliEnv(bool pty = false)
    {
        Dictionary<string, string?> env = [];
        ApplyXodusHome(env);
        if (pty && Environment.GetEnvironmentVariable("TERM") is null or "" or "dumb" or "unknown") env["TERM"] = "xterm-256color";
        return env;
    }

    // ---------------------------------------------------------------- account

    public bool IsLoggedIn
    {
        get
        {
            try
            {
                var b = File.ReadAllBytes(Keyring);
                return Contains(b, "\"user-tokens\""u8) && Contains(b, "\"user-DA\""u8);
            }
            catch { return false; }
        }
    }

    static bool Contains(byte[] hay, ReadOnlySpan<byte> needle) => hay.AsSpan().IndexOf(needle) >= 0;

    /// <summary>Reads only the "username" field of the user-DA entry (account name, not a token). Gamertag is not stored by xodus.</summary>
    public Task<AccountInfo> GetAccountAsync(CancellationToken ct)
    {
        if (!IsLoggedIn) return Task.FromResult(new AccountInfo(false, null, null));
        string? name = null;
        try
        {
            // RON: ... user: "user-DA") : { "<uuid>": (secret: [123, 34, ...]) } ...; the secret is a JSON blob given as bytes.
            var text = File.ReadAllText(Keyring, Encoding.Latin1);
            var at = text.IndexOf("user: \"user-DA\"", StringComparison.Ordinal);
            if (at >= 0)
            {
                var m = SecretBytes().Match(text, at);
                var next = text.IndexOf("user: \"", at + 10, StringComparison.Ordinal);
                if (m.Success && (next < 0 || m.Index < next))
                {
                    var bytes = m.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(byte.Parse).ToArray();
                    name = JsonNode.Parse(bytes)?["username"]?.GetValue<string>();
                }
            }
        }
        catch { }
        return Task.FromResult(new AccountInfo(true, null, name is { Length: > 0 } ? name : null));
    }

    [GeneratedRegex(@"secret:\s*\[([\d,\s]+)\]")]
    private static partial Regex SecretBytes();

    Process? _login;

    public async Task<bool> LoginAsync(CancellationToken ct)
    {
        RequireBinary();
        if (_login is { HasExited: false }) throw new InvalidOperationException("A Microsoft sign-in window is already open.");
        ResetWebviewState(); // a half-finished earlier sign-in is otherwise resumed into a blank page
        // setsid: own session so the webview's children die with the kill below
        var info = Proc.Info("setsid", ["-w", Paths.XodusBin, "login"], CliEnv());
        using var p = Process.Start(info)!;
        _login = p;
        p.StandardInput.Close();
        // output is only logged: login prints no secrets, but nothing needs it
        var log = new StreamWriter(Log, true, Encoding.UTF8) { AutoFlush = true };
        async Task Pump(StreamReader r) { string? l; while ((l = await r.ReadLineAsync()) is { }) lock (log) log.WriteLine(l); }
        var pumps = Task.WhenAll(Pump(p.StandardOutput), Pump(p.StandardError));
        try
        {
            using (ct.Register(() => Kill(p))) await p.WaitForExitAsync(CancellationToken.None);
            await pumps;
            return !ct.IsCancellationRequested && p.ExitCode == 0 && IsLoggedIn;
        }
        finally { _login = null; log.Dispose(); }
    }

    public async Task LogoutAsync(CancellationToken ct)
    {
        RequireBinary();
        await Proc.RunAsync(Proc.Info(Paths.XodusBin, ["logout"], CliEnv()), Log, TimeSpan.FromMinutes(1));
        ResetWebviewState();
    }

    static void ResetWebviewState()
    {
        foreach (var sub in new[] { Path.Combine(".local", "share"), ".cache", Path.Combine(".local", "state") })
            try { Directory.Delete(Path.Combine(Paths.XodusHome, sub, "xodus-cli"), true); } catch (DirectoryNotFoundException) { } catch { }
    }

    static void Kill(Process p) { try { p.Kill(true); } catch { } }

    // ---------------------------------------------------------------- versions

    public async Task<IReadOnlyList<GameBuild>> ListVersionsAsync(Edition edition, CancellationToken ct)
    {
        // ponytail: the Store's "current build" is only reachable through a licensed xodus request; GdkLinks tracks it, so only GdkLinks is listed.
        var json = await FetchGdkLinksAsync();
        return ParseGdkLinks(json, edition);
    }

    static async Task<string> FetchGdkLinksAsync()
    {
        var cache = Path.Combine(Paths.Cache, "gdk-links.json");
        if (File.Exists(cache) && DateTime.UtcNow - File.GetLastWriteTimeUtc(cache) < GdkLinksTtl) return File.ReadAllText(cache);
        try
        {
            var text = await Download.StringAsync(GdkLinksUrl);
            JsonNode.Parse(text); // reject garbage before it replaces a good cache
            Directory.CreateDirectory(Paths.Cache);
            File.WriteAllText(cache, text);
            return text;
        }
        catch when (File.Exists(cache)) { return File.ReadAllText(cache); } // offline: stale list beats none
        catch (Exception e) { throw new IOException("Could not read the list of Minecraft builds: " + e.Message, e); }
    }

    internal static IReadOnlyList<GameBuild> ParseGdkLinks(string json, Edition edition)
    {
        var channel = JsonNode.Parse(json)?[edition == Edition.Release ? "release" : "preview"]?.AsObject()
            ?? throw new InvalidDataException("The list of Minecraft builds has no such section.");
        var cid = ContentId(edition);
        List<(int[] Key, GameBuild Build)> found = [];
        foreach (var (full, node) in channel)
        {
            if (node is not JsonArray arr) continue;
            var urls = arr.Select(n => n?.GetValue<string>() ?? "").Where(u => ValidUrl(u, cid)).ToList();
            var parts = full.Split('.');
            if (urls.Count == 0 || parts.Length < 3 || !parts.All(p => int.TryParse(p, out _))) continue;
            found.Add((parts.Select(int.Parse).ToArray(), new(edition, string.Join('.', parts.Take(3)), full, urls)));
        }
        // newest first; for equal 3-part versions the higher 4th part wins and the other is dropped
        return found.OrderByDescending(f => f.Key, Comparer<int[]>.Create(Cmp)).Select(f => f.Build)
            .DistinctBy(b => b.Version).ToList();

        static int Cmp(int[] a, int[] b)
        {
            for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                var c = (i < a.Length ? a[i] : 0).CompareTo(i < b.Length ? b[i] : 0);
                if (c != 0) return c;
            }
            return 0;
        }
    }

    /// <summary>The index decides what gets downloaded, so each entry must be Microsoft's asset host, this edition's content id and an msixvc.</summary>
    static bool ValidUrl(string url, string contentId)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme is not ("http" or "https")) return false;
        return (u.Host == "xboxlive.com" || u.Host.EndsWith(".xboxlive.com", StringComparison.OrdinalIgnoreCase))
            && u.AbsolutePath.EndsWith(".msixvc", StringComparison.OrdinalIgnoreCase)
            && u.AbsolutePath.Contains(contentId, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------- install

    public bool IsInstalled(string gameDir) =>
        File.Exists(Path.Combine(gameDir, Exe)) && File.Exists(Path.Combine(gameDir, "appxmanifest.xml")) && File.Exists(Path.Combine(gameDir, PackageCache));

    static bool HasBuild(string dir) => File.Exists(Path.Combine(dir, Exe)) && File.Exists(Path.Combine(dir, "appxmanifest.xml"));

    [GeneratedRegex(@"package was not found|is it owned by the user|not entitled to this content", RegexOptions.IgnoreCase)] private static partial Regex NotOwned();
    [GeneratedRegex(@"unable to initialize credentials|invalid sts token|no user token|not logged in|didn't log in|un(sup|sp)ported (user )?token|failed to get exchange ms token", RegexOptions.IgnoreCase)] private static partial Regex NoCreds();
    [GeneratedRegex(@"not enough free disk space|failed to determine available space", RegexOptions.IgnoreCase)] private static partial Regex NoRoom();
    [GeneratedRegex(@"device group is full", RegexOptions.IgnoreCase)] private static partial Regex DeviceFull();
    [GeneratedRegex(@"^\s*(?<msg>\S[^\d]*?)\s+(?<done>[\d.]+)\s*(?<du>[KMGT]?i?B)\s*/\s*(?<total>[\d.]+)\s*(?<tu>[KMGT]?i?B)")] private static partial Regex Bar();
    [GeneratedRegex(@"\x1b\[[0-9;?]*[A-Za-z]")] private static partial Regex Ansi();

    static long Bytes(string v, string unit) => (long)(double.Parse(v, System.Globalization.CultureInfo.InvariantCulture) * unit switch { "KiB" => 1L << 10, "MiB" => 1L << 20, "GiB" => 1L << 30, "TiB" => 1L << 40, _ => 1L });

    /// <summary>Parses one pty line; reports only the aggregate "Initializing"/"Downloading" bar (per-file bars would jump backwards).</summary>
    internal static double? ParseProgress(string line)
    {
        var m = Bar().Match(Ansi().Replace(line, ""));
        if (!m.Success || m.Groups["msg"].Value.Trim() is not ("Initializing" or "Downloading")) return null;
        try
        {
            var total = Bytes(m.Groups["total"].Value, m.Groups["tu"].Value);
            return total > 0 ? Math.Min(1.0, (double)Bytes(m.Groups["done"].Value, m.Groups["du"].Value) / total) : null;
        }
        catch (FormatException) { return null; }
    }

    public async Task InstallAsync(GameBuild build, string destDir, IProgress<double>? progress, CancellationToken ct)
    {
        RequireBinary();
        if (!IsLoggedIn) throw new InvalidOperationException("Minecraft is downloaded with your own Microsoft account: sign in first.");
        if (build.Urls.Count == 0) throw new InvalidOperationException("This Minecraft build has no download location.");
        Directory.CreateDirectory(destDir);

        // a cache with no build beside it can only make the delta look empty ("succeeds", installs nothing)
        DropCache(destDir);

        if (!HasBuild(destDir) || !IsInstalled(destDir))
        {
            var size = await PackageSizeAsync(build.Urls, ct);
            long free; try { free = new DriveInfo(destDir).AvailableFreeSpace; } catch { free = long.MaxValue; }
            var need = size > 0 ? size + Math.Max(size / 8, 256L << 20) : 1L << 30;
            if (free < need) throw new IOException($"Not enough free disk space in {destDir}: need {need >> 20} MiB, have {free >> 20} MiB.");
        }

        var failure = "";
        foreach (var url in build.Urls)
        {
            ct.ThrowIfCancellationRequested();
            var (code, tail) = await RunStreamingAsync(url, destDir, progress, ct);
            if (code == 0 && IsInstalled(destDir)) { progress?.Report(1); return; }
            DropCache(destDir);

            var text = string.Join('\n', tail);
            // most of these exit 0 after printing their reason; none of them is helped by another mirror
            if (DeviceFull().IsMatch(text)) throw new InvalidOperationException("The account has reached its limit of ten Microsoft Store download devices. Remove some at https://account.microsoft.com/devices/content and try again.");
            if (NotOwned().IsMatch(text)) throw new InvalidOperationException("This Microsoft account does not own Minecraft Bedrock Edition.");
            if (NoCreds().IsMatch(text)) throw new InvalidOperationException("The Microsoft sign-in has expired: sign in again.");
            if (NoRoom().IsMatch(text)) throw new IOException("Not enough free disk space for the Minecraft download.");
            var last = tail.LastOrDefault(l => l.Length > 0);
            failure = "The Minecraft download " + (code == 0 ? "did not complete" : "failed") + (last is { } ? ": " + last : "") + $" (see {Log}).";
        }
        throw new IOException(failure);
    }

    static void DropCache(string dest)
    {
        var stale = Directory.GetFiles(dest, ".xodus-streaming-tmp*.msixvc").ToList();
        if (!HasBuild(dest)) stale.AddRange(Directory.GetFiles(dest, PackageCache));
        foreach (var f in stale) try { File.Delete(f); } catch { }
    }

    static async Task<long> PackageSizeAsync(IEnumerable<string> urls, CancellationToken ct)
    {
        foreach (var u in urls)
            try
            {
                using var res = await Download.Http.SendAsync(new(HttpMethod.Head, u), ct);
                if (res.Content.Headers.ContentLength is > 0 and var n) return n;
            }
            catch (Exception e) when (e is not OperationCanceledException) { }
        return 0;
    }

    /// <summary>xodus-cli draws indicatif bars only on a tty, so it runs under util-linux `script` (a pty) sized 24x120.</summary>
    async Task<(int Code, List<string> Tail)> RunStreamingAsync(string url, string dest, IProgress<double>? progress, CancellationToken ct)
    {
        var env = CliEnv(pty: true);
        env["FLARIAL_X_BIN"] = Paths.XodusBin; env["FLARIAL_X_SRC"] = url; env["FLARIAL_X_DST"] = dest;
        var info = Proc.Info("script", ["-qec", "stty cols 120 rows 24 2>/dev/null; exec \"$FLARIAL_X_BIN\" streaming \"$FLARIAL_X_SRC\" \"$FLARIAL_X_DST\"", "/dev/null"], env);
        info.RedirectStandardError = false;
        using var p = Process.Start(info)!;
        p.StandardInput.Close();
        using var reg = ct.Register(() => Kill(p));

        List<string> tail = [];
        var heard = Stopwatch.StartNew();
        using var log = new StreamWriter(Log, true, Encoding.UTF8) { AutoFlush = true };
        log.WriteLine($"== streaming {Path.GetFileName(new Uri(url).AbsolutePath)} -> {dest}");

        var pump = Task.Run(async () =>
        {
            var buf = new char[8192]; var line = new StringBuilder(); int n;
            void Flush()
            {
                var s = line.ToString().Replace("\0", "").TrimEnd(); line.Clear();
                if (s.Length == 0) return;
                if (ParseProgress(s) is { } f) { progress?.Report(f); return; }
                s = Ansi().Replace(s, "").Trim();
                if (s.Length == 0 || Bar().IsMatch(s)) return;
                if (s.Length > 1000) s = s[..1000];
                log.WriteLine(s); tail.Add(s); if (tail.Count > 40) tail.RemoveAt(0);
            }
            while ((n = await p.StandardOutput.ReadAsync(buf)) > 0)
            {
                heard.Restart();
                foreach (var c in buf.AsSpan(0, n)) if (c is '\r' or '\n') Flush(); else line.Append(c);
            }
            Flush();
        });

        // a stalled connection is retried by xodus-cli forever without a word: give up on this mirror after StallLimit of silence
        while (!p.HasExited)
        {
            await Task.WhenAny(p.WaitForExitAsync(CancellationToken.None), Task.Delay(2000));
            if (heard.Elapsed > StallLimit && !p.HasExited) { tail.Add("Nothing reached the download for 10 minutes; attempt stopped."); Kill(p); }
        }
        await pump;
        ct.ThrowIfCancellationRequested();
        return (p.ExitCode, tail);
    }

    // ---------------------------------------------------------------- run

    public Process StartRun(string gameDir, string wrapperPath, IDictionary<string, string?> env, string workingDirectory, string logPath)
    {
        RequireBinary();
        Dictionary<string, string?> e = new(env);
        // the wrapper restores the game's real HOME/XDG_* from FLARIAL_REAL_*; only vars that were actually set are handed over
        string? Real(string k) => e.TryGetValue(k, out var v) ? v : Environment.GetEnvironmentVariable(k);
        e["FLARIAL_REAL_HOME"] = Real("HOME") ?? Paths.Home;
        foreach (var k in new[] { "CONFIG_HOME", "CACHE_HOME", "DATA_HOME", "STATE_HOME" })
            e["FLARIAL_REAL_XDG_" + k] = Real("XDG_" + k);
        ApplyXodusHome(e);

        // sh only opens the log and execs: no pumping threads, and the returned pid is xodus-cli itself. Its memfds are inherited by the wrapper.
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        ProcessStartInfo info = new("/bin/sh") { UseShellExecute = false, WorkingDirectory = workingDirectory };
        foreach (var a in new[] { "-c", "l=$1; shift; exec \"$@\" </dev/null >>\"$l\" 2>&1", "sh", logPath, Paths.XodusBin, "run", gameDir, wrapperPath }) info.ArgumentList.Add(a);
        foreach (var (k, v) in e)
            if (v is null) info.Environment.Remove(k); else info.Environment[k] = v;
        return Process.Start(info)!;
    }
}
