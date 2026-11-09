using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Flarial.Runtime.Core;
using Flarial.Runtime.Services;

namespace Flarial.Runtime.Identity;

public static partial class AccountManager
{
    static readonly SemaphoreSlim s_semaphore = new(1, 1);

    public static async Task<bool> AuthenticateAsync()
    {
        await s_semaphore.WaitAsync(); try
        {
            return await AuthenticationManager.AuthenticateAsync();
        }
        finally { s_semaphore.Release(); }
    }

    const string Uri = "https://api.flarial.xyz/api/v2/account";

    static Task<HttpResponseMessage> GetAccountDetailsAsync(string? token)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, Uri);
        request.Headers.Authorization = new("Bearer", token);
        return HttpService.SendAsync(request);
    }

    public static async Task<AccountDetails?> LoginAsync()
    {
        await s_semaphore.WaitAsync(); try
        {
            if (await Session.GetAsync() is not { } token)
                return null;

            using var response = await GetAccountDetailsAsync(token);
            response.EnsureSuccessStatusCode();

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
        catch { _ = LogoutAsync(); throw; }
        finally { s_semaphore.Release(); }
    }

    // The client gets one access token at injection and cannot refresh it.
    // Access tokens last 12 hours, so get a new one for each launch instead of
    // reusing the token from launcher startup. On failure, launch with the current one.
    [Obsolete(" ", true)]
    public static async Task RefreshAsync()
    {
        await s_semaphore.WaitAsync(); try
        {
            if (FlarialClient.AccessToken is null)
                return;

            await AuthenticationManager.AuthenticateSilentlyAsync();
        }
        catch { }
        finally { s_semaphore.Release(); }
    }

    internal static async Task<string?> GetAccessTokenAsync()
    {
        await s_semaphore.WaitAsync();
        try { return await Session.GetAsync(); }
        finally { s_semaphore.Release(); }
    }

    static async Task LogoutAsync()
    {
        await s_semaphore.WaitAsync();
        try { Session.Revoke(); }
        finally { s_semaphore.Release(); }
    }

    public static void Logout() => _ = LogoutAsync();
}