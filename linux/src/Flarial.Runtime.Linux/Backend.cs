using Flarial.Runtime.Linux.Engine;
using Flarial.Runtime.Linux.Prefix;
using Flarial.Runtime.Linux.Xbox;
using Flarial.Runtime.Linux.Xodus;

namespace Flarial.Runtime.Linux;

/// <summary>Module singletons shared by the platform services.</summary>
static class Backend
{
    public static readonly IEngine Engine = new EngineManager();
    public static readonly IXodus Xodus = new XodusClient();
    public static readonly IXboxAuth Xbox = new XboxAuth();
    public static readonly IPrefix Prefix = new PrefixManager(Engine);
}
