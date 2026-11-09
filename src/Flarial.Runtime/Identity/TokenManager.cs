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

    internal static Task<HttpResponseMessage> GetAccountAsync(string? token)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, Uri);
        request.Headers.Authorization = new("Bearer", token);
        return HttpService.SendAsync(request);
    }

    internal static async Task<string?> GetTokenAsync()
    {
        using var response = await GetAccountAsync(Token);

        if (!response.IsSuccessStatusCode)
            Token = await AuthenticationManager.AuthenticateSilentlyAsync();

        return Token;
    }
}