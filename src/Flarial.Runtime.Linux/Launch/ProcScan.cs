using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Flarial.Runtime.Linux.Launch;

/// <summary>/proc helpers: processes living in our Wine prefix (environ WINEPREFIX=Paths.Prefix, or Paths.Prefix/pfx as proton rewrites it).</summary>
static class ProcScan
{
    [DllImport("libc", SetLastError = true)] static extern int kill(int pid, int sig);

    public static bool Signal(int pid, int sig) => kill(pid, sig) == 0;

    public static string[] CmdLine(int pid)
    {
        try { return File.ReadAllText($"/proc/{pid}/cmdline").Split('\0', StringSplitOptions.RemoveEmptyEntries); }
        catch { return []; }
    }

    static bool InPrefix(int pid)
    {
        // proton rewrites WINEPREFIX to "<prefix>/pfx/" inside the container (pfx -> . symlink)
        try
        {
            return File.ReadAllText($"/proc/{pid}/environ").Split('\0').Any(e => e.StartsWith("WINEPREFIX=") &&
                e[11..].TrimEnd('/') is var w && (w == Paths.Prefix || w == Paths.Prefix + "/pfx"));
        }
        catch { return false; }
    }

    /// <summary>All pids (except ours) whose environ has our WINEPREFIX.</summary>
    public static List<int> PrefixPids()
    {
        List<int> r = [];
        foreach (var d in Directory.EnumerateDirectories("/proc"))
            if (int.TryParse(Path.GetFileName(d), out var pid) && pid != Environment.ProcessId && InPrefix(pid)) r.Add(pid);
        return r;
    }

    public static bool Alive(int pid)
    {
        try { return !File.ReadAllText($"/proc/{pid}/stat").Contains(") Z"); }
        catch { return false; }
    }
}
