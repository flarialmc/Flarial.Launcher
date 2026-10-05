using System;
using System.Net.Http;
using System.Threading.Tasks;

// Synthetic credential/provider boundary; never reads PasswordVault or sends HTTP.
namespace Flarial.Runtime.Identity
{
    sealed class RefreshTokenManager
    {
        internal static readonly RefreshTokenManager _ = new();
        internal string? Value;
        internal string? Get() => Value;
        internal void Set(string value) => Value = value;
        internal void Remove() => Value = null;
    }
}
namespace Flarial.Runtime.Core
{
    sealed class FlarialClientBeta
    {
        internal static readonly FlarialClientBeta _ = new();
        internal string? AccessToken;
    }
}
namespace Flarial.Runtime.Services
{
    static class HttpService
    {
        internal static Func<string, HttpContent, Task<HttpResponseMessage>> Post =
            (_, _) => throw new InvalidOperationException("No test provider configured.");
        internal static Task<HttpResponseMessage> PostAsync(string uri, HttpContent content) => Post(uri, content);
    }
}
namespace Flarial.Runtime.Unmanaged
{
    static class NativeMethods
    {
        internal static void ShellExecute(string _) => throw new InvalidOperationException("No browser allowed in this test.");
    }
}
