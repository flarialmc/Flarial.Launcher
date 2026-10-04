using Flarial.Runtime.Platform;

namespace Flarial.Runtime.Game;

public static class GamingServices
{
    public static bool IsInstalled => Platform.Platform.Game.IsGamingServicesInstalled;
}
