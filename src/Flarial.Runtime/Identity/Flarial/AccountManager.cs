using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Flarial.Runtime.Services;

namespace Flarial.Runtime.Identity.Flarial;

[Experimental("Flarial_Runtime_Identity_Flarial")]
public static class AccountManager
{
    const string AccountUri = "https://api.flarial.xyz/api/v2/account";

    static readonly SemaphoreSlim s_semaphore = new(1, 1);

    public static async Task<AccountDetails?> LoginAsync()
    {
        await s_semaphore.WaitAsync(); try
        {
            if (await AuthenticationManager.AuthenticateSilentlyAsync() is not { } accessToken)
                return null;

            using HttpRequestMessage request = new(HttpMethod.Get, AccountUri);
            request.Headers.Authorization = new("Bearer", accessToken);

            using var response = await HttpService.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                throw new(await response.Content.ReadAsStringAsync());

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

    public static async Task LogoutAsync()
    {
        await s_semaphore.WaitAsync(); try
        {
            RefreshTokenManager._.Remove();
        }
        finally { s_semaphore.Release(); }
    }
}