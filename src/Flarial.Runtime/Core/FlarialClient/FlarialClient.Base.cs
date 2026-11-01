using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Flarial.Runtime.Game;
using Flarial.Runtime.Services;
using Windows.Security.Cryptography;
using Windows.System.Profile;
using static Windows.Win32.PInvoke;


namespace Flarial.Runtime.Core;

public abstract partial class FlarialClient
{
    unsafe static FlarialClient()
    {
        var info = SystemIdentification.GetSystemIdForPublisher();
        s_identifier = CryptographicBuffer.EncodeToHexString(info.Id);

        fixed (char* moduleNamePtr = "Kernel32")
        fixed (byte* procedureNamePtr = "LoadLibraryW"u8)
        {
            var module = GetModuleHandle(moduleNamePtr);
            var address = GetProcAddress(module, new(procedureNamePtr));

            s_apc = (delegate* unmanaged[Stdcall]<nuint, void>)(nint)address;
            s_start = (delegate* unmanaged[Stdcall]<void*, uint>)(nint)address;
        }
    }

    const string ClassName = "Flarial Client";

    private protected abstract string HashName { get; }
    private protected abstract string FileName { get; }
    private protected abstract string HashesUri { get; }
    private protected abstract string DownloadUri { get; }

    private protected abstract Task<bool> VerifyAsync();
    private protected abstract Task<bool> OnDownloadAsync<T>(T progress) where T : IProgress<int>;

    internal static string? AccessToken
    {
        set => Interlocked.Exchange(ref field, value);
        get => Interlocked.CompareExchange(ref field, null, null);
    }

    private protected FlarialClient() { }

    private protected async Task<string> GetRemoteHashAsync()
    {
        var json = await HttpService.GetJsonAsync<Dictionary<string, string>>(HashesUri);
        return json[HashName];
    }

    public bool Launch()
    {
        if (!IsRunning && Activate())
        {
            _ = PostAnalyticsAsync();
            return true;
        }
        return false;
    }

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

    public async Task<bool> DownloadAsync<T>(T progress) where T : IProgress<int>
    {
        if (await VerifyAsync())
            return true;

        try { File.Delete(FileName); }
        catch { return false; }

        return await OnDownloadAsync(progress);
    }
}