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

    static readonly CredentialLock s_credentials = new(CredentialLock.CurrentUserName);
    static readonly SemaphoreSlim s_lock = new(1, 1);

    public static Task<bool> AuthenticateAsync() =>
        RunAsync(() => AuthenticationManager.AuthenticateAsync(s_credentials));

    public static Task<AccountDetails?> LoginAsync() => RunAsync(() => s_credentials.RunAsync<AccountDetails?>(async () =>
    {
        try
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
    }));

    public static Task LogoutAsync() => RunAsync(async () =>
    {
        await s_credentials.RunAsync(AuthenticationManager.RevokeAsync);
        return true;
    });

    static async Task<T> RunAsync<T>(Func<Task<T>> operation)
    {
        await s_lock.WaitAsync();
        try { return await operation(); }
        finally { s_lock.Release(); }
    }
}
