using System;
using System.Diagnostics;
using System.IO;

namespace Flarial.Runtime.Linux.Launch;

/// <summary>logs/launch.log: one line per launch phase with an ISO timestamp, ms since the click and ms since the previous phase.</summary>
static class LaunchLog
{
    static readonly object s_lock = new();
    static readonly Stopwatch s_clock = new();
    static long s_prev;

    /// <summary>Length of minecraft.log when this launch started: readiness is searched only after it.</summary>
    public static long GameLogOffset { get; set; } = -1;

    public static void Begin(string what)
    {
        lock (s_lock) { s_clock.Restart(); s_prev = 0; Write($"---- {what}"); }
    }

    public static void Phase(string name)
    {
        lock (s_lock)
        {
            var now = s_clock.ElapsedMilliseconds;
            Write($"+{now,6} ms  (+{now - s_prev,5})  {name}");
            s_prev = now;
        }
    }

    /// <summary>Times an async step.</summary>
    public static async System.Threading.Tasks.Task<T> Time<T>(string name, System.Threading.Tasks.Task<T> t) { try { return await t; } finally { Phase(name); } }
    public static async System.Threading.Tasks.Task Time(string name, System.Threading.Tasks.Task t) { try { await t; } finally { Phase(name); } }

    static void Write(string line)
    {
        try
        {
            Directory.CreateDirectory(Paths.Logs);
            File.AppendAllText(Path.Combine(Paths.Logs, "launch.log"), $"{DateTime.Now:yyyy-MM-ddTHH:mm:ss.fff} {line}\n");
        }
        catch { }
    }
}
