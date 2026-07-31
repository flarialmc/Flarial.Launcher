using System;
using System.Threading.Tasks;
using Flarial.Runtime.Services;
using Windows.ApplicationModel;
using static Windows.Win32.PInvoke;

namespace Flarial.Runtime.Core;

public static class FlarialLauncher
{
    static readonly string? s_version;

    static FlarialLauncher()
    {
        try
        {
            var package = Package.Current;

            if (package.IsDevelopmentMode)
                return;

            var packageVersion = package.Id.Version;
            s_version = $"{packageVersion.Major}.{packageVersion.Minor}.{packageVersion.Build}.{packageVersion.Revision}";
        }
        catch { }
    }

    const string AcceptedUri = "https://cdn.flarial.xyz/202.txt";
    const string LauncherVersionUri = "https://github.com/flarialmc/newcdn/raw/refs/heads/main/launcher/Flarial.Launcher.json";
    const string LauncherPackageUri = "https://github.com/flarialmc/newcdn/raw/refs/heads/main/launcher/Flarial.Launcher.msix";

    public static async Task<bool> CanConnectAsync()
    {
        return await HttpService.ProbeAsync(AcceptedUri, default) is { };
    }

    public static async Task<bool> CheckForUpdatesAsync()
    {
        var version = await HttpService.GetJsonAsync<string>(LauncherVersionUri);
        return s_version is { } && version != s_version;
    }

    public static Task DownloadAsync(Action<int> callback)
    {
        if (s_version is { })
        {
            RegisterApplicationRestart(null, default);
            return Task.Run(() => PackageService.Add(new(LauncherPackageUri), callback));
        }
        return Task.CompletedTask;
    }
}