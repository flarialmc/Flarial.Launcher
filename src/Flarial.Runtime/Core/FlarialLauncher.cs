using System;
using System.IO;
using System.Threading.Tasks;
using Flarial.Runtime.Services;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using static Windows.Win32.PInvoke;

namespace Flarial.Runtime.Core;

public static class FlarialLauncher
{
    public static string Version => s_version is { } ? s_version : "0.0.0.0";

    static readonly string s_path;
    static readonly string? s_version;

    static FlarialLauncher()
    {
        s_path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            if (Package.Current is { IsDevelopmentMode: false } package)
            {
                var _ = package.Id.Version;
                s_version = $"{_.Major}.{_.Minor}.{_.Build}.{_.Revision}";
            }
        }
        catch { }
    }

    const string AcceptedUri = "https://cdn.flarial.xyz/202.txt";
    const string LauncherVersionUri = "https://cdn.flarial.xyz/launcher/Flarial.Launcher.json";
    const string LauncherPackageUri = "https://cdn.flarial.xyz/launcher/Flarial.Launcher.msix";

    public static async Task<bool> CanConnectAsync()
    {
        return await HttpService.ProbeAsync(AcceptedUri, default) is { };
    }

    public static async Task<bool> CheckForUpdatesAsync()
    {
        var version = await HttpService.GetJsonAsync<string>(LauncherVersionUri);
        return s_version is { } && version != s_version;
    }

    readonly struct OnDownloadAsync<T>(T progress) : IProgress<int>, IProgress<DeploymentProgress> where T : IProgress<int>
    {
        public void Report(int value)
        {
            progress.Report(value);
        }

        public void Report(DeploymentProgress value)
        {
            progress.Report((int)value.percentage);
        }
    }

    public static async Task DownloadAsync<T>(T progress) where T : IProgress<int>
    {
        if (s_version is null) return;
        await HttpService.DownloadAsync(LauncherPackageUri, s_path, progress);

        RegisterApplicationRestart(null, default);
        await PackageService.AddAsync(new(s_path), new OnDownloadAsync<T>(progress));
    }
}