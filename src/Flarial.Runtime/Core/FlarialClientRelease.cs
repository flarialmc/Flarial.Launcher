using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Flarial.Runtime.Services;
using static System.StringComparison;

namespace Flarial.Runtime.Core;

public sealed class FlarialClientRelease : FlarialClient<FlarialClientRelease>
{
    const string DownloadUri = "https://cdn.flarial.xyz/dll/latest.dll";

    private protected override string HashName => "Release";
    private protected override string FileName => "Flarial.Client.Release.dll";
    private protected override string HashesUri => "https://cdn.flarial.xyz/dll_hashes.json";

    async Task<string> GetLocalHashAsync()
    {
        try
        {
            using var stream = File.OpenRead(FileName);
            var array = await SHA256.HashDataAsync(stream);
            return Convert.ToHexString(array);
        }
        catch { return string.Empty; }
    }

    private protected override async Task<bool> VerifyClientAsync()
    {
        var localHashTask = GetLocalHashAsync();
        var remoteHashTask = GetRemoteHashAsync();
        await Task.WhenAll(localHashTask, remoteHashTask);

        var localHash = await localHashTask;
        var remoteHash = await remoteHashTask;

        return localHash.Equals(remoteHash, OrdinalIgnoreCase);
    }

    private protected override async Task<bool> DownloadClientAsync<T>(T progress)
    {
        await HttpService.DownloadAsync(DownloadUri, FileName, progress);
        return true;
    }
}