using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Flarial.Runtime.Platform;
using Flarial.Runtime.Versions;

namespace Flarial.Runtime.Linux;

/// <summary>
/// SEAM: Wine/Proton game backend. Currently reports "not installed".
/// TODO(engineer): detect an installed prefix, install GDK builds (VersionItem.DownloadUris / GameLaunchHelper) into it,
/// start Minecraft under Wine/Proton in Launch() and raise StatusChanged on any change.
/// </summary>
public sealed class LinuxGameService : IGameService
{
    public bool IsGamingServicesInstalled => true; // Windows-only concept
    public bool RequiresInstalledGame => false;    // installing a version creates the install on Linux

    public bool IsInstalled => false;
    public bool IsSideloaded => false;
    public bool IsRunning => false;
    public string? InstalledVersion => null;
    public IReadOnlyList<string> InstalledVersions => [];

    public event Action? StatusChanged { add { } remove { } }

    public Task InstallAsync(VersionItem version, string uri, Action<int, bool> progress) => throw new NotImplementedException("Wine/Proton game install is not implemented yet.");

    public uint? Launch() => throw new NotImplementedException("Wine/Proton game launch is not implemented yet.");
}
