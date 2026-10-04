// Module contracts for the Linux backend. Each module lives in its own folder/namespace and implements one interface;
// LinuxGameService / LinuxInjector / LinuxMicrosoftAccount wire them together. Shared helpers: Paths, Download, Proc, Json, Settings.
// Rules for every module: no UI references; progress is 0..1 (IProgress<double>); long work honours CancellationToken;
// never print or log secrets (tokens, keyring contents, device.json); log to Paths.Logs.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Flarial.Runtime.Linux
{
    internal enum Edition { Release, Preview }

    /// <summary>One downloadable build. Version = "major.minor.build" (VersionItem.Version format, e.g. "1.26.51"); FullVersion keeps the 4th part ("1.26.51.1").</summary>
    internal sealed record GameBuild(Edition Edition, string Version, string FullVersion, IReadOnlyList<string> Urls);

    internal sealed record AccountInfo(bool SignedIn, string? Gamertag, string? Email);

    internal sealed record DeviceCode(string UserCode, string VerificationUri, string DeviceCodeValue, TimeSpan ExpiresIn, TimeSpan Interval);

    /// <summary>Result of a per-launch refresh. Online = device.json complete (set WINEGDK_PREAUTH_DEVICE); RefreshToken goes into the prefix registry.</summary>
    internal sealed record XboxSession(bool Online, string? DeviceJsonPath, string? RefreshToken, string? Gamertag);

    internal sealed record LaunchSettings(string CustomEnv, bool RayTracing, bool Diagnostics)
    {
        public static LaunchSettings Current => new(Settings.CustomEnv, Settings.RayTracing, Settings.Diagnostics);
    }

    internal sealed record InjectResult(bool Ok, int ExitCode, string Message);
}

namespace Flarial.Runtime.Linux.Xodus
{
    /// <summary>xodus-cli (GPL-3.0, downloaded and run as a subprocess only). Impl class: <c>XodusClient : IXodus</c>, parameterless ctor. HOME/XDG_* of xodus-cli = Paths.XodusHome.</summary>
    internal interface IXodus
    {
        /// <summary>Download/verify pinned xodus-cli into Paths.XodusDir (skips if rev matches; honours Paths seeded dev install).</summary>
        Task EnsureInstalledAsync(IProgress<double>? progress, CancellationToken ct);

        /// <summary>Keyring file contains both user-tokens and user-DA.</summary>
        bool IsLoggedIn { get; }

        /// <summary>Best-effort account info without exposing tokens (email/gamertag may be null).</summary>
        Task<AccountInfo> GetAccountAsync(CancellationToken ct);

        /// <summary>`xodus-cli login` (webview window); true when signed in afterwards.</summary>
        Task<bool> LoginAsync(CancellationToken ct);

        /// <summary>`xodus-cli logout` and clear webview state; keeps device keyring semantics from the spec.</summary>
        Task LogoutAsync(CancellationToken ct);

        /// <summary>Microsoft current build + GdkLinks urls.json (validated hosts/paths), newest first.</summary>
        Task<IReadOnlyList<GameBuild>> ListVersionsAsync(Edition edition, CancellationToken ct);

        /// <summary>`xodus-cli streaming` into destDir (pty for progress, mirrors, disk-space check, needs login). Success = exe + appxmanifest + package cache.</summary>
        Task InstallAsync(GameBuild build, string destDir, IProgress<double>? progress, CancellationToken ct);

        /// <summary>exe + appxmanifest.xml + .xodus-streaming.msixvc present.</summary>
        bool IsInstalled(string gameDir);

        /// <summary>
        /// Starts `xodus-cli run gameDir wrapperPath` (xodus env HOME/XDG, real HOME/XDG passed through FLARIAL_REAL_HOME / FLARIAL_REAL_XDG_*
        /// for Resources/launch-wrapper.sh) with <paramref name="env"/> as the game environment; stdout/stderr appended to logPath.
        /// Returns the started process (the xodus -> wrapper -> umu chain).
        /// </summary>
        Process StartRun(string gameDir, string wrapperPath, IDictionary<string, string?> env, string workingDirectory, string logPath);
    }
}

namespace Flarial.Runtime.Linux.Xbox
{
    /// <summary>Device-code MSA login (client 0000000048183522) + XBL/XSTS/SISU chain producing device.json. Impl class: <c>XboxAuth : IXboxAuth</c>, parameterless ctor.</summary>
    internal interface IXboxAuth
    {
        bool HasToken { get; }

        /// <summary>device.json present, complete and not expiring within 60 s.</summary>
        bool IsOnlineReady { get; }

        Task<DeviceCode> BeginDeviceCodeAsync(CancellationToken ct);

        /// <summary>Polls until the code is entered (true, token saved to Paths.MsaToken 0600), expired/denied (false) or cancelled.</summary>
        Task<bool> PollDeviceCodeAsync(DeviceCode code, CancellationToken ct);

        /// <summary>Refresh + rotate the refresh token (transport failure keeps the cached token) and (re)build device.json unless >=30 min remain. Never throws for network/account problems: returns Online=false.</summary>
        Task<XboxSession> RefreshAsync(CancellationToken ct);

        /// <summary>Delete token + device.json; keep device-key.pem / device-id.txt.</summary>
        Task SignOutAsync();
    }
}

namespace Flarial.Runtime.Linux.Engine
{
    /// <summary>GDK-Proton-xuser engine + umu-run. Impl class: <c>EngineManager : IEngine</c>, parameterless ctor. Both Ensure* are hash-pinned, resumable and idempotent; a seeded dev install (Paths seeding) counts as ready.</summary>
    internal interface IEngine
    {
        bool IsProtonReady { get; }
        bool IsUmuReady { get; }
        Task EnsureProtonAsync(IProgress<double>? progress, CancellationToken ct);
        Task EnsureUmuAsync(IProgress<double>? progress, CancellationToken ct);

        string ProtonDir { get; }
        /// <summary>files/bin/wine (also used by the injector).</summary>
        string Wine { get; }
        /// <summary>files/bin-wow64/wineserver.</summary>
        string Wineserver { get; }
        string UmuRun { get; }
        /// <summary>Engine revision string actually installed (for prefix refresh).</summary>
        string Revision { get; }
    }
}

namespace Flarial.Runtime.Linux.Prefix
{
    /// <summary>The Wine prefix (Paths.Prefix) and the game-directory fixups. Impl class: <c>PrefixManager : IPrefix</c>, ctor(IEngine).</summary>
    internal interface IPrefix
    {
        bool IsReady { get; }

        /// <summary>Create via headless `umu-run wineboot -u` if needed (cryptbase seed + one retry), refresh stale engine DLLs, apply registry tweaks (ConsoleMode, WinRT pickers, TLS, WindowsAppRuntime env). Prefix must be idle.</summary>
        Task EnsureSetupAsync(IProgress<double>? progress, CancellationToken ct);

        /// <summary>Per game dir, idempotent: XCurl.dll/libHttpClient.GDK.dll swap (backups), CA bundle to etc/ssl/certs, GameInput redist from Installers/ into the prefix. Downloads cacert/gdkdeps into Paths.Cache.</summary>
        Task PrepareGameAsync(string gameDir, IProgress<double>? progress, CancellationToken ct);

        /// <summary>HKLM\Software\Wine\WineGDK RefreshToken (null removes it). Prefix must be idle.</summary>
        Task SetRefreshTokenAsync(string? token, CancellationToken ct);
    }
}

namespace Flarial.Runtime.Linux.Launch
{
    /// <summary>Starts/monitors the game. Impl class: <c>LauncherCore : ILauncherCore</c>, ctor(IEngine, IXodus, IPrefix, IXboxAuth).</summary>
    internal interface ILauncherCore
    {
        /// <summary>
        /// Full launch: refuse if running; Prefix.EnsureSetup + PrepareGame; Xbox.RefreshAsync + SetRefreshToken (offline on failure);
        /// build env (spec 5.2/5.3, X11 default, WINEGDK_PREAUTH_DEVICE only if online); IXodus.StartRun with Resources/launch-wrapper.sh
        /// (FLARIAL_UMU_RUN etc.), log to Paths.Logs/minecraft.log; wait (max ~120 s) for the game process. Returns the host pid of the
        /// Minecraft.Windows.exe wine process, or null if the chain exited / never appeared.
        /// </summary>
        Task<uint?> LaunchAsync(string gameDir, LaunchSettings settings, CancellationToken ct);

        /// <summary>Host pid of a process whose environ has WINEPREFIX=Paths.Prefix and cmdline Minecraft.Windows.exe.</summary>
        uint? FindGamePid();
        bool IsRunning { get; }

        /// <summary>wineserver -k with engine wine + WINEPREFIX, then SIGKILL stragglers.</summary>
        Task KillAsync(CancellationToken ct);
    }
}

namespace Flarial.Runtime.Linux.Injection
{
    /// <summary>`wine injector.exe` against the live prefix. Impl class: <c>InjectorCore : IInjectorCore</c>, ctor(IEngine).</summary>
    internal interface IInjectorCore
    {
        /// <summary>Write the embedded injector.exe resource to Paths.Injector (replace if bytes differ).</summary>
        Task EnsureInjectorExeAsync(CancellationToken ct);

        /// <summary>
        /// Validate dll (.dll, exists), game alive, no winedbg; sleep <paramref name="delay"/> (aborting if the game exits);
        /// run engine wine + Paths.Injector Z:\...dll Minecraft.Windows.exe (WINEPREFIX, outside the container, 60 s timeout), log to
        /// Paths.Logs/injector.log; then 3 s post-check that the game did not exit/enter winedbg.
        /// </summary>
        Task<InjectResult> InjectAsync(string dllPath, uint gamePid, TimeSpan delay, CancellationToken ct);

        /// <summary>True if /proc/pid/maps lists a mapped file whose name matches (case-insensitive file-name contains).</summary>
        bool IsModuleLoaded(uint gamePid, string fileNameContains);
    }
}
