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
    const string Uri = "https://api.flarial.xyz/api/v2/account";

    static readonly SemaphoreSlim s_semaphore = new(1, 1);

    static string? AccessToken
    {
        set => Interlocked.Exchange(ref field, value);
        get => Interlocked.CompareExchange(ref field, null, null);
    }

    public static async Task<bool> AuthenticateAsync()
    {
        await s_semaphore.WaitAsync(); try
        {
            return await AuthenticationManager.AuthenticateAsync();
        }
        finally { s_semaphore.Release(); }
    }

    static Task<HttpResponseMessage> GetAccountDetailsAsync(string? token)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, Uri);
        request.Headers.Authorization = new("Bearer", token);
        return HttpService.SendAsync(request);
    }

    static async Task<string?> FetchAccessTokenAsync()
    {
        using var response = await GetAccountDetailsAsync(AccessToken);

        if (!response.IsSuccessStatusCode)
            AccessToken = await AuthenticationManager.GetAccessTokenAsync();

        return AccessToken;
    }

    public static async Task<AccountDetails?> LoginAsync()
    {
        await s_semaphore.WaitAsync(); try
        {
            if (await FetchAccessTokenAsync() is not { } accessToken)
                return null;

            using var response = await GetAccountDetailsAsync(accessToken);
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

    internal static async Task<string?> GetAccessTokenAsync()
    {
        await s_semaphore.WaitAsync(); try
        {
            return await FetchAccessTokenAsync();
        }
        finally { s_semaphore.Release(); }
    }

    static async Task LogoutAsync()
    {
        await s_semaphore.WaitAsync(); try
        {
            AccessToken = null;
            AuthenticationManager.RevokeRefreshToken();
        }
        finally { s_semaphore.Release(); }
    }

    public static void Logout() => _ = LogoutAsync();
}