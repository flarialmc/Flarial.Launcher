using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Flarial.Runtime.Exceptions;
using Flarial.Runtime.Services;

namespace Flarial.Runtime.Versions;

public sealed class VersionItem
{
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

    /// <summary>Canonical "major.minor.build" string.</summary>
    public string Version => $"{_version._major}.{_version._minor}.{_version._build}";
    public IReadOnlyList<string> DownloadUris => _downloadUris;
    /// <summary>gamelaunchhelper.dll to place in the game folder after install.</summary>
    public byte[] GameLaunchHelper => _gameLaunchHelper;

    readonly struct OnInstall<T>(T progress) where T : IProgress<(int, bool)>
    {
        internal void Report(int percentage, bool installing) => progress.Report((percentage, installing));
    }

    public async Task<Task?> InstallAsync<T>(T progress) where T : IProgress<(int, bool)>
    {
        var game = Platform.Platform.Game;

        if (!game.IsGamingServicesInstalled)
            throw new GamingServicesNotInstalledException();

        if (game.RequiresInstalledGame && !game.IsInstalled)
            throw new MinecraftNotInstalledException();

        if (game.IsSideloaded)
            throw new MinecraftSideloadedException();

        if (await HttpService.ProbeAsync(_downloadUris) is not { } uri)
            return null;

        OnInstall<T> callback = new(progress);
        return game.InstallAsync(this, uri, callback.Report);
    }
}
