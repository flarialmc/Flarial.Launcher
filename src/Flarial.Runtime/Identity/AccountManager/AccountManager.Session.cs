using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Flarial.Runtime.Services;

namespace Flarial.Runtime.Identity;

partial class AccountManager
{
    static class Session
    {
        static string? Token
        {
            set => Interlocked.Exchange(ref field, value);
            get => Interlocked.CompareExchange(ref field, null, null);
        }

        internal static async Task<string?> GetAsync()
        {
            using var response = await GetAccountDetailsAsync(Token);

            if (!response.IsSuccessStatusCode)
                Token = await AuthenticationManager.AuthenticateSilentlyAsync();

            return Token;
        }

        internal static void Revoke()
        {
            Token = null;
            AuthenticationManager.Revoke();
        }
    }
}