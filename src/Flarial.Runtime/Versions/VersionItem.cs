using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Flarial.Runtime.Exceptions;
using Flarial.Runtime.Game;
using Flarial.Runtime.Services;

namespace Flarial.Runtime.Versions;

public sealed class VersionItem
{
    public override string ToString() => _string;

    internal VersionItem(string version, string[] downloadUris, byte[] gameLaunchHelper)
    {
        _version = version;
        _downloadUris = downloadUris;
        _gameLaunchHelper = gameLaunchHelper;
        _string = new GameVersion(version).ToString();
    }

    readonly string _string;
    readonly string[] _downloadUris;
    readonly byte[] _gameLaunchHelper;
    internal readonly string _version;

    /// <summary>Canonical "major.minor.build" string.</summary>
    public string Version => _version;
    public IReadOnlyList<string> DownloadUris => _downloadUris;
    /// <summary>gamelaunchhelper.dll to place in the game folder after install.</summary>
    public byte[] GameLaunchHelper => _gameLaunchHelper;

    public async Task<Task?> InstallAsync(Action<int, bool> callback)
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

        return game.InstallAsync(this, uri, callback);
    }
}