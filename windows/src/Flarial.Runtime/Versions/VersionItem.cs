using System;
using System.IO;
using System.Threading.Tasks;
using Flarial.Runtime.Exceptions;
using Flarial.Runtime.Game;
using Flarial.Runtime.Services;
using Windows.Management.Deployment;

namespace Flarial.Runtime.Versions;

public sealed class VersionItem
{
    static readonly string s_path = Path.GetTempPath();

    internal VersionItem(GameVersion version, string[] downloadUris, byte[] gameLaunchHelper)
    {
        _version = version;
        _string = version.ToString();

        _downloadUris = downloadUris;
        _gameLaunchHelper = gameLaunchHelper;
    }

    readonly string _string;
    readonly string[] _downloadUris;
    readonly byte[] _gameLaunchHelper;

    internal readonly GameVersion _version;
    public override string ToString() => _string;

    readonly struct OnInstallAsync<T>(T progress) : IProgress<int>, IProgress<DeploymentProgress> where T : IProgress<(int, bool)>
    {
        public void Report(int value)
        {
            progress.Report((value, false));
        }

        public void Report(DeploymentProgress value)
        {
            progress.Report(((int)value.percentage, true));
        }
    }

    async Task InstallAsync<T>(string uri, T progress) where T : IProgress<(int, bool)>
    {
        OnInstallAsync<T> callback = new(progress);
        var packagePath = Path.Combine(s_path, Path.GetRandomFileName());

        try
        {
            await HttpService.DownloadAsync(uri, packagePath, callback);
            await PackageService.AddAsync(new(packagePath), callback);

            var installedPath = Minecraft.Package.InstalledPath;
            var gameLaunchHelperPath = Path.Combine(installedPath, "gamelaunchhelper.dll");

            await File.WriteAllBytesAsync(gameLaunchHelperPath, _gameLaunchHelper);
        }
        finally
        {
            try { File.Delete(packagePath); }
            catch { }
        }
    }

    public async Task<Task?> InstallAsync<T>(T progress) where T : IProgress<(int, bool)>
    {
        if (!GamingServices.IsInstalled)
            throw new GamingServicesNotInstalledException();

        if (!Minecraft.IsInstalled)
            throw new MinecraftNotInstalledException();

        if (Minecraft.IsSideloaded)
            throw new MinecraftSideloadedException();

        if (await HttpService.ProbeAsync(_downloadUris) is not { } uri)
            return null;

        return InstallAsync(uri, progress);
    }
}