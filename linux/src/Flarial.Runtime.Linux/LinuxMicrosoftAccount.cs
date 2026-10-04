using System.Threading;
using System.Threading.Tasks;
using Flarial.Runtime.Platform;

namespace Flarial.Runtime.Linux;

/// <summary>Microsoft account used to download/license the game: xodus-cli login (webview window).</summary>
public sealed class LinuxMicrosoftAccount : IMicrosoftAccount
{
    public bool IsSignedIn => Backend.Xodus.IsLoggedIn;

    public async Task<bool> SignInAsync()
    {
        await Backend.Xodus.EnsureInstalledAsync(null, default);
        return await Backend.Xodus.LoginAsync(default);
    }

    public async Task SignOutAsync()
    {
        await Backend.Xodus.EnsureInstalledAsync(null, default);
        await Backend.Xodus.LogoutAsync(default);
    }
}
