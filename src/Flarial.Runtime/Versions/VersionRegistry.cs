using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Flarial.Runtime.Game;
using Flarial.Runtime.Services;

namespace Flarial.Runtime.Versions;

public sealed class VersionRegistry : IEnumerable<VersionItem>
{
    sealed class VersionItemComparer : IComparer<VersionItem>
    {
        public int Compare(VersionItem? x, VersionItem? y)
        {
            GameVersion a = x!._version;
            GameVersion b = y!._version;

            if (b._major != a._major)
                return b._major.CompareTo(a._major);

            if (b._minor != a._minor)
                return b._minor.CompareTo(a._minor);

            return b._build.CompareTo(a._build);
        }
    }

    public static string InstalledVersion
    {
        get
        {
            var version = Minecraft.Package.Id.Version;
            return new GameVersion(version).ToString();
        }
    }

    static readonly VersionItemComparer s_comparer = new();

    const string SupportedVersionsUri = "https://cdn.flarial.xyz/launcher/Versions.json";
    const string GameLaunchHelperUri = "https://cdn.flarial.xyz/launcher/gamelaunchhelper.dll";
    const string DownloadLinksUri = "https://cdn.jsdelivr.net/gh/MinecraftBedrockArchiver/GdkLinks@latest/urls.min.json";

    readonly List<VersionItem> _versionItems;
    readonly HashSet<GameVersion> _gameVersions;

    VersionRegistry(HashSet<GameVersion> gameVersions, List<VersionItem> versionItems)
    {
        _gameVersions = gameVersions;
        _versionItems = versionItems;
        PreferredVersion = $"{_versionItems[0]}";
    }

    public string PreferredVersion { get; }

    public bool IsSupported
    {
        get
        {
            var packageVersion = Minecraft.Package.Id.Version;
            GameVersion gameVersion = new(packageVersion);

            var truncatedVersion = gameVersion.Truncate();
            return _gameVersions.Contains(truncatedVersion);
        }
    }

    public static Task<VersionRegistry> GetAsync() => Task.Run(static async () =>
    {
        var gameLaunchHelperTask = HttpService.GetBytesAsync(GameLaunchHelperUri);
        var supportedVersionsTask = HttpService.GetJsonAsync<List<string>>(SupportedVersionsUri);
        var downloadLinksTask = HttpService.GetJsonAsync<Dictionary<string, Dictionary<string, string[]>>>(DownloadLinksUri);

        await Task.WhenAll(gameLaunchHelperTask, supportedVersionsTask, downloadLinksTask);

        var downloadLinks = await downloadLinksTask;
        var gameLaunchHelper = await gameLaunchHelperTask;
        var supportedVersions = await supportedVersionsTask;

        List<VersionItem> versionItems = [];
        HashSet<GameVersion> gameVersions = new(supportedVersions.Count);

        foreach (var version in supportedVersions)
        {
            GameVersion gameVersion = new(version);
            gameVersions.Add(gameVersion.Truncate());
        }

        foreach (var item in downloadLinks["release"])
        {
            var index = item.Key.LastIndexOf('.');
            var downloadVersion = item.Key[..index];

            GameVersion gameVersion = new(downloadVersion);
            var truncatedVersion = gameVersion.Truncate();

            if (!gameVersions.Contains(truncatedVersion))
                continue;

            versionItems.Add(new(gameVersion, item.Value, gameLaunchHelper));
        }

        versionItems.Sort(s_comparer);
        return new VersionRegistry(gameVersions, versionItems);
    });

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public IEnumerator<VersionItem> GetEnumerator() => _versionItems.GetEnumerator();
}