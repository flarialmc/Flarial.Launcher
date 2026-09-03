using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Flarial.Runtime.Core;
using Flarial.Runtime.Identity;
using Flarial.Runtime.Services;

namespace Flarial.Runtime.Discord;

public static class AccountManager
{
    const string UserAgent = "Samsung AI-Powered Washing Machine";
    const string AvatarUri = "https://cdn.discordapp.com/avatars/{0}/{1}";
    const string PremiumUri = "https://api.flarial.xyz/android/premium/discord";

    static readonly SemaphoreSlim s_semaphore = new(1, 1);

    public static async Task<AccountDetails?> LoginAsync()
    {
        await s_semaphore.WaitAsync(); try
        {
            if (await AuthenticationManager.AuthenticateSilentlyAsync() is not { } accessToken)
                return null;

            using HttpRequestMessage request = new(HttpMethod.Post, PremiumUri);

            request.Headers.UserAgent.ParseAdd(UserAgent);
            request.Headers.Authorization = new("Bearer", accessToken);

            using var response = await HttpService.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            FlarialClientBeta._.AccessToken = accessToken;

            using var stream = await response.Content.ReadAsStreamAsync();
            var entitlements = await JsonService.Default.ReadAsync<AccountEntitlements>(stream);

            var avatarUri = string.Format(AvatarUri, entitlements.DiscordId, entitlements.Avatar);
            return new(entitlements.Username, avatarUri, entitlements.HasFlarialPlus, entitlements.HasTesterRole);
        }
        finally { s_semaphore.Release(); }
    }

    public static async Task LogoutAsync()
    {
        await s_semaphore.WaitAsync(); try
        {
            FlarialClientBeta._.AccessToken = null;
            RefreshTokenManager._.Remove();
        }
        finally { s_semaphore.Release(); }
    }
}