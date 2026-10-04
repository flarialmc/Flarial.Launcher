using System;
using System.Threading;
using System.Threading.Tasks;

namespace Flarial.Runtime.Linux;

/// <summary>Public facade over the Microsoft (xodus) and Xbox Live (device code) accounts for the Accounts settings page.</summary>
public static class LinuxAccounts
{
    static DeviceCode? s_code;

    public static async Task<(bool SignedIn, string? Gamertag, string? Email)> GetMicrosoftAsync(CancellationToken ct)
    {
        if (!Backend.Xodus.IsLoggedIn) return (false, null, null);
        var a = await Backend.Xodus.GetAccountAsync(ct);
        return (a.SignedIn, a.Gamertag, a.Email);
    }

    public static async Task<bool> SignInMicrosoftAsync(CancellationToken ct)
    {
        await Backend.Xodus.EnsureInstalledAsync(null, ct);
        return await Backend.Xodus.LoginAsync(ct);
    }

    public static async Task SignOutMicrosoftAsync(CancellationToken ct)
    {
        await Backend.Xodus.EnsureInstalledAsync(null, ct);
        await Backend.Xodus.LogoutAsync(ct);
    }

    public static (bool SignedIn, bool OnlineReady, string? Gamertag) GetXbox() =>
        (Backend.Xbox.HasToken, Backend.Xbox.IsOnlineReady, Json.Field(Paths.DeviceJson, "xbl_gamertag"));

    public static async Task<(string UserCode, string Uri, TimeSpan ExpiresIn)> BeginXboxAsync(CancellationToken ct)
    {
        var c = s_code = await Backend.Xbox.BeginDeviceCodeAsync(ct);
        return (c.UserCode, c.VerificationUri, c.ExpiresIn);
    }

    public static async Task<bool> PollXboxAsync(CancellationToken ct)
    {
        if (s_code is not { } c) return false;
        if (!await Backend.Xbox.PollDeviceCodeAsync(c, ct)) return false;
        await Backend.Xbox.RefreshAsync(ct); // builds device.json so the gamertag/online state is known immediately
        return true;
    }

    public static Task SignOutXboxAsync() => Backend.Xbox.SignOutAsync();
}
