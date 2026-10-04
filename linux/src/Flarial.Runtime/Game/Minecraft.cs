using System;
using Flarial.Runtime.Platform;

namespace Flarial.Runtime.Game;

public static class Minecraft
{
    public static bool IsInstalled => Platform.Platform.Game.IsInstalled;
    public static bool IsRunning => Platform.Platform.Game.IsRunning;
    public static bool IsSideloaded => Platform.Platform.Game.IsSideloaded;

    public static event Action? PackageStatusChanged
    {
        add => Platform.Platform.Game.StatusChanged += value;
        remove => Platform.Platform.Game.StatusChanged -= value;
    }
}
