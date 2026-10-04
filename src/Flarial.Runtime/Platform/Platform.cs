using System;
using System.Threading.Tasks;
using Flarial.Runtime.Versions;

namespace Flarial.Runtime.Platform;

/// <summary>Game install/launch backend. Implemented per OS (Flarial.Runtime.Linux = Wine/Proton).</summary>
public interface IGameService
{
    /// <summary>Windows-only concept (Gaming Services); return true where not applicable.</summary>
    bool IsGamingServicesInstalled { get; }

    /// <summary>True when a version install needs an existing game install first (Windows Store); false where installing creates it.</summary>
    bool RequiresInstalledGame { get; }

    bool IsInstalled { get; }
    bool IsSideloaded { get; }
    bool IsRunning { get; }

    /// <summary>Installed game version as "major.minor.build" where build = package build / 100 (e.g. "1.21.100", "1.26.30"); null if not installed.</summary>
    string? InstalledVersion { get; }

    /// <summary>Every version installed locally, same format as <see cref="InstalledVersion"/>.</summary>
    System.Collections.Generic.IReadOnlyList<string> InstalledVersions { get; }

    /// <summary>Raised when the installed/running status changes.</summary>
    event Action? StatusChanged;

    /// <summary>Download (from the already probed <paramref name="uri"/>) and install a version. progress(percent, installing).</summary>
    Task InstallAsync(VersionItem version, string uri, Action<int, bool> progress);

    /// <summary>Makes an already downloaded build the one the launcher starts. False when it is not installed or the game is running.</summary>
    bool SelectVersion(string version);

    /// <summary>Removes a downloaded build from disk. False when it is not installed or the game is running.</summary>
    bool DeleteVersion(string version);

    /// <summary>Start the game if needed and return its process id, or null on failure.</summary>
    uint? Launch();
}

/// <summary>Injects a DLL into the running game process.</summary>
public interface IInjector
{
    bool Inject(string dllPath, uint processId);

    /// <summary>True when the Flarial client is already loaded in the game.</summary>
    bool IsClientRunning { get; }
}

/// <summary>Microsoft account sign-in used to download the game.</summary>
public interface IMicrosoftAccount
{
    bool IsSignedIn { get; }
    Task<bool> SignInAsync();
    Task SignOutAsync();
}

/// <summary>Secure storage for the Flarial/Discord refresh token.</summary>
public interface ICredentialStore
{
    string? Get(string resource, string username);
    void Set(string resource, string username, string value);
    void Remove(string resource, string username);
}

/// <summary>Registry for the OS backend; set once at startup before any runtime call.</summary>
public static class Platform
{
    public static IGameService Game { get; set; } = null!;
    public static IInjector Injector { get; set; } = null!;
    public static IMicrosoftAccount MicrosoftAccount { get; set; } = null!;
    public static ICredentialStore Credentials { get; set; } = null!;
}
