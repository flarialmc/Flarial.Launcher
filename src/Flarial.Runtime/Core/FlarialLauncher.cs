using System.Threading.Tasks;
using Flarial.Runtime.Services;

namespace Flarial.Runtime.Core;

// Self-update/migration removed on Linux (original downloaded a replacement exe and exited); see Flarial.Runtime.Windows.
public static class FlarialLauncher
{
    const string AcceptedUri = "https://cdn.flarial.xyz/202.txt";

    public static async Task<bool> CanConnectAsync()
    {
        return await HttpService.ProbeAsync(AcceptedUri, default) is { };
    }
}
