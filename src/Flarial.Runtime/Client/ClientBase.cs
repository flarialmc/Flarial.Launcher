using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Flarial.Runtime.Core;
using Flarial.Runtime.Services;
using static System.StringComparison;

namespace Flarial.Runtime.Client;

public abstract class ClientBase<T> : ClientBase where T : ClientBase<T>, new()
{
    public static readonly T _ = new();

    private protected ClientBase()
    {
        if (_ is null)
        {
            FileName = $"Flarial.Client.{HashName}.dll";
            DownloadUri = $"https://cdn.flarial.xyz/dll/{BlobName}.dll";
            return;
        }
        throw new InvalidOperationException();
    }

    private protected abstract string BlobName { get; }

    private protected override string FileName { get; }
    private protected override string DownloadUri { get; }
}

public abstract class ClientBase : FlarialClient
{
    private protected ClientBase() { }

    private protected abstract string DownloadUri { get; }
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