using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Flarial.Runtime.Services;

namespace Flarial.Runtime.Identity;

public static class AccountManager
{
    const string AccountUri = "https://api.flarial.xyz/api/v2/account";

    static readonly SemaphoreSlim s_semaphore = new(1, 1);
    static AccountManager() { _ = RenewAccessLoopAsync(); }

    public static async Task<bool> AuthenticateAsync()
    {
        await s_semaphore.WaitAsync(); try
        {
            return await AuthenticationManager.AuthenticateAsync();
        }
        finally { s_semaphore.Release(); }
    }

    public static async Task<AccountDetails?> LoginAsync()
    {
        await s_semaphore.WaitAsync(); try
        {
            if (await AuthenticationManager.AuthenticateSilentlyAsync() is not { } accessToken)
                return null;

            using HttpRequestMessage request = new(HttpMethod.Get, AccountUri);
            request.Headers.Authorization = new("Bearer", accessToken);

            using var response = await HttpService.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);

            var user = document.RootElement.GetProperty("user");
            var entitlements = document.RootElement.GetProperty("entitlements");

            var avatarUrl = user.GetProperty("avatar_url").GetString();
            var displayName = user.GetProperty("display_name").GetString()!;

            var beta = entitlements.GetProperty("beta");
            var flarialPlus = entitlements.GetProperty("flarial_plus");

            return new(new()
            {
                AvatarUrl = avatarUrl,
                DisplayName = displayName,
                HasBetaAccess = beta.GetProperty("active").GetBoolean(),
                HasFlarialPlus = flarialPlus.GetProperty("active").GetBoolean(),
            });
        }
        catch { await AuthenticationManager.RevokeAsync(); throw; }
        finally { s_semaphore.Release(); }
    }

    public static async Task LogoutAsync()
    {
        await s_semaphore.WaitAsync(); try
        {
            await AuthenticationManager.RevokeAsync();
        }
        finally { s_semaphore.Release(); }
    }

    static async Task RenewAccessLoopAsync()
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync())
        {
            try { await RenewAccessAsync(); }
            catch { /* Retry on the next tick; transient failures leave login intact. */ }
        }
    }

    internal static async Task RenewAccessAsync()
    {
        if (!await s_semaphore.WaitAsync(0)) return;
        try
        {
            var expiresAt = AccessTokenManager._.ExpiresAt;
            if (expiresAt == 0 || expiresAt > DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 120) return;
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
            await AuthenticationManager.AuthenticateSilentlyAsync(timeout.Token);
        }
        finally { s_semaphore.Release(); }
    }
}
