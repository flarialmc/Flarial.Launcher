using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Flarial.Runtime.Linux.Xodus;

/// <summary>Hidden check for `--selftest-xodus` (wire from Program if wanted). Uses the real data dir / seed; never prints tokens.</summary>
internal static class XodusSelfTest
{
    public static async Task<int> RunAsync()
    {
        void Ok(bool c, string what) { Console.WriteLine((c ? "ok   " : "FAIL ") + what); if (!c) Environment.ExitCode = 1; }
        Paths.Ensure();
        var x = new XodusClient();

        // pure parsing
        Ok(XodusClient.ParseProgress("\x1b[2K  Downloading   12.00 MiB/ 24.00 MiB  3 MiB/s [####    ] 50%") is 0.5, "progress: total bar parsed");
        Ok(XodusClient.ParseProgress("  hurt_land1.fsb   1.00 MiB/ 2.00 MiB [##] 50%") is null, "progress: per-file bar ignored");
        const string cid = "7792d9ce-355a-493c-afbd-768f4a77c3b0";
        var good = $"http://assets1.xboxlive.com/10/x/{cid}/1/a.msixvc";
        var json = """ {"release":{"1.26.51.1":["GOOD","http://evil.com/CID/a.msixvc","http://assets1.xboxlive.com/CID/a.zip","http://assets1.xboxlive.com/other/a.msixvc"],"1.26.51.2":["GOOD"],"1.9.0.1":["GOOD"],"1.26.9.0":["GOOD"],"1.26.60.0":["http://evil.com/CID/a.msixvc"]},"preview":{} } """.Replace("GOOD", good).Replace("CID", cid);
        var parsed = XodusClient.ParseGdkLinks(json, Edition.Release);
        Ok(parsed.Select(b => b.FullVersion).SequenceEqual(["1.26.51.2", "1.26.9.0", "1.9.0.1"]), "gdklinks: validated, sorted, deduped: " + string.Join(",", parsed.Select(b => b.FullVersion)));
        Ok(parsed[0].Version == "1.26.51" && parsed[0].Urls.Count == 1, "gdklinks: Version is 3-part");

        // live
        await x.EnsureInstalledAsync(null, default);
        Ok(File.Exists(Paths.XodusBin), "EnsureInstalled: xodus-cli present");
        foreach (var e in new[] { Edition.Release, Edition.Preview })
        {
            var l = await x.ListVersionsAsync(e, default);
            Ok(l.Count > 0 && l.All(b => b.Urls.Count > 0), $"ListVersions {e}: {l.Count} builds, newest {l.FirstOrDefault()?.FullVersion}");
        }
        Console.WriteLine($"IsLoggedIn={x.IsLoggedIn}");
        var acc = await x.GetAccountAsync(default);
        Console.WriteLine($"account: signedIn={acc.SignedIn} hasName={acc.Email is { Length: > 0 }}");
        foreach (var d in Directory.Exists(Path.Combine(Paths.Games, "release")) ? Directory.GetDirectories(Path.Combine(Paths.Games, "release")) : [])
            Console.WriteLine($"IsInstalled({Path.GetFileName(d)})={x.IsInstalled(d)}");
        Ok(!x.IsInstalled(Path.GetTempPath()), "IsInstalled false for empty dir");

        // install starts and cancels cleanly
        if (x.IsLoggedIn)
        {
            var build = (await x.ListVersionsAsync(Edition.Release, default))[0];
            var dest = Path.Combine(Paths.Cache, "selftest-install");
            Directory.CreateDirectory(dest);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            double last = 0; var sw = Stopwatch.StartNew();
            try { await x.InstallAsync(build, dest, new Progress<double>(v => last = v), cts.Token); Ok(false, "install unexpectedly finished"); }
            catch (OperationCanceledException) { Ok(true, $"install cancelled after {sw.ElapsedMilliseconds} ms (progress {last:P0})"); }
            catch (Exception e) { Ok(false, "install failed: " + e.Message); }
            await Task.Delay(500);
            Ok(Process.GetProcessesByName("xodus-cli").All(p => p.StartInfo.Arguments != "" || !SafeCmd(p).Contains(dest)), "no leftover xodus-cli streaming process");
            try { Directory.Delete(dest, true); } catch { }
        }
        return Environment.ExitCode;
    }

    static string SafeCmd(Process p) { try { return File.ReadAllText($"/proc/{p.Id}/cmdline"); } catch { return ""; } }
}
