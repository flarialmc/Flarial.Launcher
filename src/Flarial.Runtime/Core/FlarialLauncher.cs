using System.Reflection;
using System.Threading.Tasks;
using Flarial.Runtime.Services;

namespace Flarial.Runtime.Core;

// Self-update/migration removed on Linux (original downloaded a replacement package and restarted); see Flarial.Runtime.Windows.
public static class FlarialLauncher
{
    public static string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";

    const string AcceptedUri = "https://cdn.flarial.xyz/202.txt";

    public static async Task<bool> CanConnectAsync()
    {
        return await HttpService.ProbeAsync(AcceptedUri, default) is { };
    }
}
