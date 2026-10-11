using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Flarial.Runtime.Game;
using Flarial.Runtime.Identity;

namespace Flarial.Runtime.Core;

// The canary is a local, build-pinned artifact. It never uses the production
// release/beta download owners or their paths. Missing pins fail closed.
public static class PreproductionCanary
{
    const string PathKey = "Flarial.PreproductionDllPath";
    const string HashKey = "Flarial.PreproductionDllSha256";

    static string? BuildMetadata(string key)
    {
        foreach (var item in typeof(PreproductionCanary).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
            if (item.Key == key) return item.Value;
        return null;
    }

    // The same file owner is exercised by the headless authority check and the
    // launch path. The returned handle must remain open through injection.
    internal static FileStream? OpenVerifiedArtifact(string? path, string? expectedHex)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) ||
            string.IsNullOrWhiteSpace(expectedHex) || expectedHex.Length != 64)
            return null;

        FileStream? file = null;
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                return null;

            var expected = Convert.FromHexString(expectedHex);
            file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var actual = SHA256.HashData(file);
            if (!CryptographicOperations.FixedTimeEquals(actual, expected))
            {
                file.Dispose();
                return null;
            }

            if (new ModificationLibrary(path).IsLoadable)
                return file;

            file.Dispose();
            return null;
        }
        catch
        {
            file?.Dispose();
            return null;
        }
    }

    public static bool Launch()
    {
        if (!AccountAuthorityProfile.IsPreproduction || string.IsNullOrEmpty(FlarialClient.AccessToken))
            return false;

        var path = BuildMetadata(PathKey);
        using var file = OpenVerifiedArtifact(path, BuildMetadata(HashKey));
        if (file is null)
            return false;

        // FileShare.Read denies writers until the loader's thread wait returns.
        try
        {
            return !FlarialClient.IsRunning &&
                   FlarialClient.LaunchTrusted(new ModificationLibrary(path));
        }
        catch
        {
            return false;
        }
    }
}
