using System;
using System.Threading;
using System.Threading.Tasks;

namespace Flarial.Launcher.Management;

/// <summary>Microsoft account used by xodus (game download/license). Gamertag/Email may be null.</summary>
public sealed record MicrosoftAccountStatus(bool SignedIn, string? Gamertag, string? Email);

/// <summary>Xbox Live in-game sign-in (device-code MSA login). OnlineReady = device.json usable.</summary>
public sealed record XboxStatus(bool SignedIn, bool OnlineReady, string? Gamertag);

public sealed record XboxDeviceCode(string UserCode, string VerificationUri, TimeSpan ExpiresIn);

/// <summary>
/// UI-facing backend for the Settings > Accounts page (Microsoft + Xbox Live). The Flarial/Discord account does not
/// go through this: the page binds to the existing SettingsGeneralViewModel/DiscordAccountManager.
/// Integrator: map to IXodus (IsLoggedIn/GetAccountAsync/LoginAsync/LogoutAsync) and IXboxAuth
/// (Begin/PollDeviceCodeAsync, SignOutAsync, RefreshAsync for gamertag) and set <see cref="AccountsService.Current"/> at startup.
/// </summary>
public interface IAccountsService
{
    Task<MicrosoftAccountStatus> GetMicrosoftAsync(CancellationToken ct);
    /// <summary>Opens the xodus login window; returns true when signed in afterwards.</summary>
    Task<bool> SignInMicrosoftAsync(CancellationToken ct);
    Task SignOutMicrosoftAsync(CancellationToken ct);

    Task<XboxStatus> GetXboxAsync(CancellationToken ct);
    Task<XboxDeviceCode> BeginXboxDeviceCodeAsync(CancellationToken ct);
    /// <summary>Polls the code from the last Begin call until entered (true), expired/denied (false) or cancelled.</summary>
    Task<bool> PollXboxDeviceCodeAsync(CancellationToken ct);
    Task SignOutXboxAsync(CancellationToken ct);
}

public static class AccountsService
{
    public static IAccountsService Current { get; set; } = new FakeAccountsService();
}

/// <summary>In-memory stand-in for design/verification until the real backends are bound.</summary>
sealed class FakeAccountsService : IAccountsService
{
    bool _ms, _xbox;

    public Task<MicrosoftAccountStatus> GetMicrosoftAsync(CancellationToken ct) =>
        Task.FromResult(new MicrosoftAccountStatus(_ms, _ms ? "FlarialPlayer" : null, _ms ? "player@outlook.com" : null));

    public async Task<bool> SignInMicrosoftAsync(CancellationToken ct) { await Task.Delay(800, ct); return _ms = true; }
    public Task SignOutMicrosoftAsync(CancellationToken ct) { _ms = false; return Task.CompletedTask; }

    public Task<XboxStatus> GetXboxAsync(CancellationToken ct) =>
        Task.FromResult(new XboxStatus(_xbox, _xbox, _xbox ? "FlarialPlayer" : null));

    public Task<XboxDeviceCode> BeginXboxDeviceCodeAsync(CancellationToken ct) =>
        Task.FromResult(new XboxDeviceCode("ABCD1234", "https://www.microsoft.com/link", TimeSpan.FromMinutes(15)));

    public async Task<bool> PollXboxDeviceCodeAsync(CancellationToken ct) { await Task.Delay(Timeout.Infinite, ct); return _xbox = true; }
    public Task SignOutXboxAsync(CancellationToken ct) { _xbox = false; return Task.CompletedTask; }
}
