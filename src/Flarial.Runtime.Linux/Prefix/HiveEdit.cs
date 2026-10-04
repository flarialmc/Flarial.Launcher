using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Flarial.Runtime.Linux.Prefix;

/// <summary>
/// Edits one string value of a Wine registry hive file (system.reg / user.reg) in place. Only valid while no wineserver has the prefix
/// loaded (it would overwrite the file on exit): saves the ~4 s of `wine reg import` + wineserver idle linger per launch.
/// Latin-1 both ways so every byte round-trips.
/// </summary>
static class HiveEdit
{
    /// <summary>key like Software\\Wine\\WineGDK (hive-escaped); value null removes it. False = could not do it safely (use wine instead).</summary>
    public static bool SetString(string hivePath, string key, string name, string? value)
    {
        if (value is { } && (value.Any(c => c < 32 || c > 126))) return false;
        try
        {
            var lines = new List<string>(File.ReadAllLines(hivePath, Encoding.Latin1));
            if (lines.Count == 0 || !lines[0].StartsWith("WINE REGISTRY Version ", StringComparison.Ordinal)) return false;
            var header = "[" + key + "]";
            var h = lines.FindIndex(l => l.StartsWith(header, StringComparison.OrdinalIgnoreCase) && (l.Length == header.Length || l[header.Length] == ' '));
            var line = value is null ? null : $"\"{name}\"=\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            var prefix = $"\"{name}\"=";
            if (h < 0)
            {
                if (line is null) return true;
                var t = DateTimeOffset.UtcNow;
                lines.Add("");
                lines.Add($"{header} {t.ToUnixTimeSeconds()}");
                lines.Add($"#time={t.ToFileTime():x}");
                lines.Add(line);
            }
            else
            {
                var end = h + 1;
                while (end < lines.Count && lines[end].Length > 0) end++;
                var at = lines.FindIndex(h + 1, end - h - 1, l => l.StartsWith(prefix, StringComparison.Ordinal));
                // a multi-line (continued) value is not ours to rewrite
                if (at >= 0 && lines[at].EndsWith('\\')) return false;
                if (at >= 0) { if (line is null) lines.RemoveAt(at); else lines[at] = line; }
                else if (line is { }) lines.Insert(end, line);
            }
            var tmp = hivePath + ".flarial.tmp";
            File.WriteAllText(tmp, string.Join('\n', lines) + "\n", Encoding.Latin1);
            try { File.SetUnixFileMode(tmp, File.GetUnixFileMode(hivePath)); } catch { }
            File.Move(tmp, hivePath, true);
            return true;
        }
        catch { return false; }
    }
}
