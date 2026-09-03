using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Flarial.Runtime.Game;
using Flarial.Runtime.Services;
using static System.StringComparison;

namespace Flarial.Runtime.Core;

public abstract class FlarialClient<T> : FlarialClient where T : FlarialClient<T>, new()
{
    public static readonly T _ = new();

    private protected FlarialClient()
    {
        if (_ is null) return;
        throw new InvalidOperationException();
    }
}

public abstract partial class FlarialClient
{
    const string ClassName = "Flarial Client";

    private protected abstract string Build { get; }
    private protected abstract string FileName { get; }
    private protected abstract string HashesUri { get; }
    private protected abstract Task<string> GetDownloadUriAsync();

    private protected FlarialClient() { }

    public static bool IsRunning
    {
        get
        {
            if (Minecraft.GetWindow(className: ClassName) is not { } clientWindow)
                return false;

            if (Minecraft.GetWindow(clientWindow._processId) is not { } minecraftWindow)
                return false;

            return minecraftWindow.IsVisible;
        }
    }

    public bool Launch()
    {
        if (!IsRunning && Injector.Launch(new(FileName)))
        {
            _ = PostAnalyticsAsync();
            return true;
        }
        return false;
    }

    async Task<string> GetRemoteHashAsync()
    {
        var json = await HttpService.GetJsonAsync<Dictionary<string, string>>(HashesUri);
        return json[Build];
    }

    protected private virtual async Task<string> GetLocalHashAsync()
    {
        try
        {
            using var stream = File.OpenRead(FileName);
            var array = await SHA256.HashDataAsync(stream);
            return Convert.ToHexString(array);
        }
        catch { return string.Empty; }
    }

    public async Task<bool> DownloadAsync<T>(T progress) where T : IProgress<int>
    {
        var localHashTask = GetLocalHashAsync();
        var remoteHashTask = GetRemoteHashAsync();
        await Task.WhenAll(localHashTask, remoteHashTask);

        var localHash = await localHashTask;
        var remoteHash = await remoteHashTask;

        if (localHash.Equals(remoteHash, OrdinalIgnoreCase))
            return true;

        try { File.Delete(FileName); }
        catch { return false; }

        var downloadUri = await GetDownloadUriAsync();
        await HttpService.DownloadAsync(downloadUri, FileName, progress);

        return true;
    }
}