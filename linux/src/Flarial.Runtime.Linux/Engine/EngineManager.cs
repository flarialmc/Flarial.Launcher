using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Flarial.Runtime.Linux.Engine;

/// <summary>GDK-Proton-xuser (BedrockOnLinux release, never rehosted) + pinned umu-run. A seeded dev install counts as ready.</summary>
sealed class EngineManager : IEngine
{
    // Pins from BedrockOnLinux (MIT) / umu-launcher.
    public const string PinnedRevision = "wow64-archs-native20";
    public const string EngineUrl = "https://github.com/Wyze3306/BedrockOnLinux/releases/download/engine-" + PinnedRevision + "/GDK-Proton-xuser-" + PinnedRevision + ".tar.gz";
    const string EngineSha = "2f30533b249954438bf6b425631539c8d8036261ce25cffa0d1ea5d30f1d3e27";
    public const string UmuUrl = "https://github.com/Open-Wine-Components/umu-launcher/releases/download/1.4.3/umu-launcher-1.4.3-zipapp.tar";
    const string UmuSha = "3f8fdc033f547afdb3408ea48ad07194769405148dcfa2b2f945b7fb368a33bb";
    const string UmuRunSha = "577181dbff2eccdaa78b411c0fd1aa7fde574028449c3e0e99f508536a76870e";

    static bool Seeded => File.Exists(Path.Combine(Paths.Root, ".seeded"));
    static readonly UnixFileMode Exec = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    public string ProtonDir => Paths.Engine;
    public string Wine => Paths.Wine;
    public string Wineserver => Paths.Wineserver;
    public string UmuRun => Paths.UmuRun;
    public string Revision => File.Exists(Paths.EngineRev) ? File.ReadAllText(Paths.EngineRev).Trim() : "";

    // ponytail: umu-run is hashed on every check (~1 MB); cache if it ever shows in a profile.
    public bool IsProtonReady => File.Exists(Wine) && (Seeded || Revision == PinnedRevision);
    public bool IsUmuReady => File.Exists(UmuRun) && (Seeded || Download.Sha256Async(UmuRun).GetAwaiter().GetResult() == UmuRunSha);

    public async Task EnsureProtonAsync(IProgress<double>? progress, CancellationToken ct)
    {
        Paths.Ensure();
        if (IsProtonReady) { progress?.Report(1); return; }

        var tar = Path.Combine(Paths.Cache, $"GDK-Proton-xuser-{PinnedRevision}.tar.gz");
        await Download.FileAsync(EngineUrl, tar, EngineSha, (d, t) => progress?.Report(t > 0 ? 0.85 * d / t : 0), ct);
        ct.ThrowIfCancellationRequested();

        var tmp = Path.Combine(Paths.ProtonDir, ".extract");
        if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
        await Extract(tar, tmp, ct);
        progress?.Report(0.95);

        var inner = Path.Combine(tmp, "GDK-Proton-xuser");
        if (!File.Exists(Path.Combine(inner, "files", "bin", "wine"))) throw new InvalidDataException("engine archive has no files/bin/wine");
        Remove(Paths.Engine);
        Directory.Move(inner, Paths.Engine);
        Directory.Delete(tmp, true);
        File.WriteAllText(Paths.EngineRev, PinnedRevision);
        File.Delete(tar); // ~860 MB; the hash-pinned download is repeated only on reinstall
        progress?.Report(1);
    }

    public async Task EnsureUmuAsync(IProgress<double>? progress, CancellationToken ct)
    {
        Paths.Ensure();
        if (IsUmuReady) { progress?.Report(1); return; }

        var tar = Path.Combine(Paths.Cache, "umu-launcher-1.4.3-zipapp.tar");
        await Download.FileAsync(UmuUrl, tar, UmuSha, (d, t) => progress?.Report(t > 0 ? 0.8 * d / t : 0), ct);
        ct.ThrowIfCancellationRequested();

        var tmp = Path.Combine(Paths.Cache, "umu-extract");
        if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
        await Extract(tar, tmp, ct);
        var src = Directory.EnumerateFiles(tmp, "umu-run", SearchOption.AllDirectories).FirstOrDefault() ?? throw new FileNotFoundException("umu-run missing from package");
        if (await Download.Sha256Async(src) != UmuRunSha) throw new InvalidDataException("umu-run SHA-256 mismatch");

        Remove(Paths.UmuDir); // only drops a seed symlink; a real dir is kept (steamrt3 cache)
        Directory.CreateDirectory(Paths.UmuDir);
        var dst = Paths.UmuRun + ".tmp";
        File.Copy(src, dst, true);
        File.SetUnixFileMode(dst, Exec);
        File.Move(dst, Paths.UmuRun, true);
        Directory.Delete(tmp, true);
        progress?.Report(1);
        // The ~900 MB steamrt3 download is umu's own job on the first `umu-run wineboot` (PrefixManager); no separate pre-warm.
    }

    static async Task Extract(string tar, string dest, CancellationToken ct)
    {
        Directory.CreateDirectory(dest);
        // plain -xf: GNU tar detects gz/xz/zstd itself
        var code = await Proc.RunAsync(Proc.Info("tar", ["-xf", tar, "-C", dest]), Path.Combine(Paths.Logs, "setup.log"), TimeSpan.FromMinutes(20), ct);
        ct.ThrowIfCancellationRequested();
        if (code != 0) throw new IOException($"tar failed ({code}) extracting {Path.GetFileName(tar)}");
    }

    static void Remove(string path)
    {
        if (new FileInfo(path).LinkTarget is { }) File.Delete(path);
        else if (Directory.Exists(path)) Directory.Delete(path, true);
    }
}
