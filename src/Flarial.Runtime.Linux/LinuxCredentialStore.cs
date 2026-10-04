using System;
using System.Diagnostics;
using System.IO;
using Flarial.Runtime.Linux.Prefix;
using Flarial.Runtime.Platform;

namespace Flarial.Runtime.Linux;

/// <summary>Refresh-token storage: libsecret (secret-tool) when available, otherwise a 0600 file in the launcher data directory.</summary>
public sealed class LinuxCredentialStore : ICredentialStore
{
    static string FilePath(string resource, string username) => $"credential.{Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(resource + "/" + username))}";

    static (int Code, string Output) Secret(string? stdin, params string[] args)
    {
        try
        {
            ProcessStartInfo info = new("secret-tool") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            foreach (var arg in args) info.ArgumentList.Add(arg);

            using var process = Process.Start(info)!;
            if (stdin is { }) process.StandardInput.Write(stdin);
            process.StandardInput.Close();

            var output = process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, output);
        }
        catch { return (-1, string.Empty); }
    }

    public string? Get(string resource, string username)
    {
        // the injected client may have rotated the refresh token inside the prefix vault: that copy is the newest
        if (VaultBridge.Get(resource, username) is { Length: > 0 } rotated) return rotated;
        var stored = GetStored(resource, username);
        if (stored is { }) VaultBridge.Set(resource, username, stored); // prefix created after login
        return stored;
    }

    static string? GetStored(string resource, string username)
    {
        var (code, output) = Secret(null, "lookup", "service", resource, "account", username);
        if (code == 0 && output.Length > 0) return output;

        try { return File.ReadAllText(FilePath(resource, username)); }
        catch { return null; }
    }

    public void Set(string resource, string username, string value)
    {
        SetStored(resource, username, value);
        VaultBridge.Set(resource, username, value);
    }

    static void SetStored(string resource, string username, string value)
    {
        var (code, _) = Secret(value, "store", "--label", resource, "service", resource, "account", username);
        if (code == 0) { try { File.Delete(FilePath(resource, username)); } catch { } return; }

        var path = FilePath(resource, username);
        File.WriteAllText(path, value);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    public void Remove(string resource, string username)
    {
        VaultBridge.Remove(resource, username);
        Secret(null, "clear", "service", resource, "account", username);
        try { File.Delete(FilePath(resource, username)); } catch { }
    }
}
