using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Flarial.Runtime.Services;

namespace Flarial.Runtime.Identity;

public static class AccountManager
{
    const string AccountUri = AccountAuthorityProfile.AccountUrl;

    static readonly SemaphoreSlim s_semaphore = new(1, 1);

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

            using var request = CreateAccountRequest(accessToken);
            using var response = await HttpService.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);

            return ReadAccountResponse(document.RootElement);
        }
        catch { _ = LogoutAsync(); throw; }
        finally { s_semaphore.Release(); }
    }

    internal static HttpRequestMessage CreateAccountRequest(string accessToken)
    {
        HttpRequestMessage request = new(HttpMethod.Get, AccountUri);
        request.Headers.Authorization = new("Bearer", accessToken);
        return request;
    }

    internal static AccountDetails ReadAccountResponse(JsonElement root)
    {
        var user = root.GetProperty("user");
        var entitlements = root.GetProperty("entitlements");
        var avatarUrl = user.GetProperty("avatar_url").GetString();
        var displayName = user.GetProperty("display_name").GetString()!;
        var access = entitlements.GetProperty(AccountAuthorityProfile.IsPreproduction ? "tester" : "beta")
            .GetProperty("active");
        var flarialPlus = entitlements.GetProperty("flarial_plus");

        return new(new()
        {
            AvatarUrl = avatarUrl,
            DisplayName = displayName,
            // The website's unavailable Discord-role decision is null. It must
            // not grant tester access or prevent this private canary login.
            HasBetaAccess = AccountAuthorityProfile.IsPreproduction && access.ValueKind == JsonValueKind.Null
                ? false : access.GetBoolean(),
            HasFlarialPlus = flarialPlus.GetProperty("active").GetBoolean(),
        });
    }

    public static async Task LogoutAsync()
    {
        await s_semaphore.WaitAsync(); try
        {
            await AuthenticationManager.RevokeAsync();
        }
        finally { s_semaphore.Release(); }
    }
}
