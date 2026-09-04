using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Flarial.Runtime.Game;
using Flarial.Runtime.Services;
using Windows.Networking.Vpn;
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

    private protected abstract string HashName { get; }
    private protected abstract string FileName { get; }
    private protected abstract string HashesUri { get; }

    private protected abstract Task<string> GetLocalHashAsync();
    private protected abstract Task<bool> DownloadClientAsync<T>(T progress) where T : IProgress<int>;

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

    private protected async Task<string> GetRemoteHashAsync()
    {
        var json = await HttpService.GetJsonAsync<Dictionary<string, string>>(HashesUri);
        return json[HashName];
    }

    /*
        - Hash comparisons for beta builds could be improved.
        - Hence decoupling this functionality in case that happens.
    */

    private protected virtual async Task<bool> VerifyClientAsync()
    {
        var localHashTask = GetLocalHashAsync();
        var remoteHashTask = GetRemoteHashAsync();
        await Task.WhenAll(localHashTask, remoteHashTask);

        var localHash = await localHashTask;
        var remoteHash = await remoteHashTask;

        return localHash.Equals(remoteHash, OrdinalIgnoreCase);
    }

    public async Task<bool> DownloadAsync<T>(T progress) where T : IProgress<int>
    {
        if (await VerifyClientAsync())
            return true;

        try { File.Delete(FileName); }
        catch { return false; }

        return await DownloadClientAsync(progress);
    }
}