using System;
using Flarial.Runtime.Platform;

namespace Flarial.Runtime.Linux;

/// <summary>SEAM: DLL injection into the Wine process hosting Minecraft (e.g. via a helper running inside the prefix).</summary>
public sealed class LinuxInjector : IInjector
{
    public bool IsClientRunning => false;

    public bool Inject(string dllPath, uint processId) => throw new NotImplementedException("Injection into the Wine game process is not implemented yet.");
}
