using System.Threading;
using System.Threading.Tasks;
using Flarial.Runtime.Linux;

namespace Flarial.Launcher.Management;

/// <summary>Real Accounts backend (xodus + Xbox device code) over the Linux runtime.</summary>
sealed class LinuxAccountsService : IAccountsService
{
    public async Task<MicrosoftAccountStatus> GetMicrosoftAsync(CancellationToken ct)
    {
        var (s, g, e) = await LinuxAccounts.GetMicrosoftAsync(ct);
        return new(s, g, e);
    }

    public Task<bool> SignInMicrosoftAsync(CancellationToken ct) => LinuxAccounts.SignInMicrosoftAsync(ct);
    public Task SignOutMicrosoftAsync(CancellationToken ct) => LinuxAccounts.SignOutMicrosoftAsync(ct);

    public Task<XboxStatus> GetXboxAsync(CancellationToken ct)
    {
        var (s, o, g) = LinuxAccounts.GetXbox();
        return Task.FromResult(new XboxStatus(s, o, g));
    }

    public async Task<XboxDeviceCode> BeginXboxDeviceCodeAsync(CancellationToken ct)
    {
        var (c, u, e) = await LinuxAccounts.BeginXboxAsync(ct);
        return new(c, u, e);
    }

    public Task<bool> PollXboxDeviceCodeAsync(CancellationToken ct) => LinuxAccounts.PollXboxAsync(ct);
    public Task SignOutXboxAsync(CancellationToken ct) => LinuxAccounts.SignOutXboxAsync();
}
