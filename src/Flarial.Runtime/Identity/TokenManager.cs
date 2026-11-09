using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Flarial.Runtime.Services;

namespace Flarial.Runtime.Identity;

static class TokenManager
{
    static string? Token
    {
        set => Interlocked.Exchange(ref field, value);
        get => Interlocked.CompareExchange(ref field, null, null);
    }

    const string Uri = "https://api.flarial.xyz/api/v2/account";

    internal static Task<HttpResponseMessage> GetAccountAsync()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, Uri);
        request.Headers.Authorization = new("Bearer", Token);
        return HttpService.SendAsync(request);
    }

    internal static async Task<string?> GetTokenAsync()
    {
        using var response = await GetAccountAsync();

        if (!response.IsSuccessStatusCode)
            Token = await AuthenticationManager.AuthenticateSilentlyAsync();

        return Token;
    }
}