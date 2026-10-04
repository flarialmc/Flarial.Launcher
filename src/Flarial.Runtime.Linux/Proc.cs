using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Flarial.Runtime.Linux;

static class Proc
{
    public static ProcessStartInfo Info(string file, IEnumerable<string> args, IDictionary<string, string?>? env = null, string? cwd = null)
    {
        ProcessStartInfo i = new(file) { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = cwd ?? "" };
        foreach (var a in args) i.ArgumentList.Add(a);
        if (env is { })
            foreach (var (k, v) in env)
                if (v is null) i.Environment.Remove(k); else i.Environment[k] = v;
        return i;
    }

    /// <summary>Runs to completion appending combined output to a log file; returns exit code (-1 on timeout).</summary>
    public static async Task<int> RunAsync(ProcessStartInfo info, string? logPath, TimeSpan timeout)
    {
        using var p = Process.Start(info)!;
        p.StandardInput.Close();
        StreamWriter? log = null;
        if (logPath is { }) { Directory.CreateDirectory(Path.GetDirectoryName(logPath)!); log = new(logPath, true, Encoding.UTF8) { AutoFlush = true }; }
        async Task Pump(StreamReader r) { string? l; while ((l = await r.ReadLineAsync()) is { }) { if (log is { }) lock (log) log.WriteLine(l); } }
        var pumps = Task.WhenAll(Pump(p.StandardOutput), Pump(p.StandardError));
        using var cts = new System.Threading.CancellationTokenSource(timeout);
        try { await p.WaitForExitAsync(cts.Token); await pumps; return p.ExitCode; }
        catch (OperationCanceledException) { try { p.Kill(true); } catch { } return -1; }
        finally { log?.Dispose(); }
    }

    public static int Run(string file, IEnumerable<string> args) => RunAsync(Info(file, args), null, TimeSpan.FromMinutes(10)).GetAwaiter().GetResult();

    public static bool OnPath(string name)
    {
        foreach (var d in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(':'))
            if (d.Length > 0 && File.Exists(Path.Combine(d, name))) return true;
        return false;
    }
}
