using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Flarial.Runtime.Linux;

/// <summary>SHA-256 of a file, remembered by (size, mtime) in cache/hashes.tsv so big files are not re-hashed on every launch.</summary>
static class HashCache
{
    static readonly object s_lock = new();
    static string Store => Path.Combine(Paths.Cache, "hashes.tsv");

    public static async Task<string> Sha256Async(string path)
    {
        var fi = new FileInfo(path);
        var stamp = $"{fi.Length}\t{fi.LastWriteTimeUtc.Ticks}";
        var full = Path.GetFullPath(path);
        Dictionary<string, string> all = [];
        lock (s_lock)
            try { foreach (var l in File.ReadAllLines(Store)) if (l.Split('\t') is [var p, var sz, var mt, var sha]) all[p] = $"{sz}\t{mt}\t{sha}"; } catch { }
        if (all.TryGetValue(full, out var v) && v.StartsWith(stamp + "\t")) return v[(stamp.Length + 1)..];

        var h = await Download.Sha256Async(path);
        lock (s_lock)
            try
            {
                all[full] = $"{stamp}\t{h}";
                Directory.CreateDirectory(Paths.Cache);
                var tmp = Store + ".tmp";
                File.WriteAllLines(tmp, all.Select(kv => $"{kv.Key}\t{kv.Value}"));
                File.Move(tmp, Store, true);
            }
            catch { }
        return h;
    }
}
