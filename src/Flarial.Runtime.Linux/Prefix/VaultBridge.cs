using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Flarial.Runtime.Linux.Prefix;

/// <summary>
/// Host side of the fake Windows PasswordVault (Native/flarial_vault.cpp). Same file, same format:
/// one line per entry, hex(resource) ' ' hex(user) ' ' hex(password), UTF-8 inside the hex.
/// Only exists once the prefix does; the file is 0600 inside a 0700 directory.
/// </summary>
static class VaultBridge
{
    static readonly object s_lock = new();
    static string PrefixDir => Path.Combine(Paths.Prefix, "drive_c");
    static string Dir => Path.Combine(PrefixDir, "ProgramData", "Flarial", "Vault");
    static string FilePath => Path.Combine(Dir, "vault.dat");

    static string Hex(string s) => Convert.ToHexString(Encoding.UTF8.GetBytes(s)).ToLowerInvariant();
    static string Unhex(string s) => Encoding.UTF8.GetString(Convert.FromHexString(s));

    static List<(string R, string U, string P)> Load()
    {
        List<(string, string, string)> all = [];
        try
        {
            foreach (var line in File.ReadAllLines(FilePath))
            {
                var f = line.Trim().Split(' ');
                if (f.Length == 3) all.Add((Unhex(f[0]), Unhex(f[1]), Unhex(f[2])));
            }
        }
        catch { }
        return all;
    }

    static void Save(List<(string R, string U, string P)> all)
    {
        const UnixFileMode Private = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        Directory.CreateDirectory(Dir);
        File.SetUnixFileMode(Dir, Private);

        var tmp = FilePath + ".launcher.tmp";
        File.WriteAllText(tmp, string.Concat(all.Select(e => $"{Hex(e.R)} {Hex(e.U)} {Hex(e.P)}\n")));
        File.SetUnixFileMode(tmp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.Move(tmp, FilePath, true); // atomic rename
    }

    public static string? Get(string resource, string username)
    {
        lock (s_lock)
            foreach (var e in Load()) if (e.R == resource && e.U == username) return e.P;
        return null;
    }

    public static void Set(string resource, string username, string value)
    {
        if (!Directory.Exists(PrefixDir)) return;
        lock (s_lock)
        {
            try
            {
                var all = Load();
                all.RemoveAll(e => e.R == resource && e.U == username);
                all.Add((resource, username, value));
                Save(all);
            }
            catch { } // the vault only serves the injected client; never fail the launcher over it
        }
    }

    public static void Remove(string resource, string username)
    {
        if (!File.Exists(FilePath)) return;
        lock (s_lock)
        {
            try
            {
                var all = Load();
                if (all.RemoveAll(e => e.R == resource && e.U == username) > 0) Save(all);
            }
            catch { }
        }
    }
}
