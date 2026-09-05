using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Flarial.Runtime.Core;
using Flarial.Runtime.Services;

namespace Flarial.Runtime.Identity;

public static class AccountManager
{
    const string UserAgent = "Samsung AI-Powered Washing Machine";
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

            using var stream = await response.Content.ReadAsStreamAsync();
            var metadata = await JsonService.Default.ReadAsync<AccountMetadata>(stream);

            FlarialClientBeta._.AccessToken = accessToken;
            return new(metadata);
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