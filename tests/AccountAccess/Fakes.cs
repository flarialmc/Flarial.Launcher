using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Flarial.Runtime.Services
{
    abstract class CredentialService<T> : CredentialService where T : CredentialService<T>, new()
    {
        internal static readonly T _ = new();
    }

    abstract class CredentialService
    {
        private protected abstract string Resource { get; }
        private protected abstract string Username { get; }
        internal static readonly Dictionary<(string, string), string> Vault = [];
        internal string? Get() => Vault.GetValueOrDefault((Resource, Username));
        internal void Set(string value) => Vault[(Resource, Username)] = value;
        internal void Remove() => Vault.Remove((Resource, Username));
    }

    static class HttpService
    {
        internal static Func<string, HttpContent, CancellationToken, Task<HttpResponseMessage>> Post =
            (_, _, _) => throw new InvalidOperationException("No test provider configured.");
        internal static Func<HttpRequestMessage, Task<HttpResponseMessage>> Send =
            _ => throw new InvalidOperationException("No test account provider configured.");
        internal static Task<HttpResponseMessage> PostAsync(string uri, HttpContent content, CancellationToken token = default)
            => Post(uri, content, token);
        internal static Task<HttpResponseMessage> SendAsync(HttpRequestMessage request) => Send(request);
        internal static Task<byte[]?> TryGetBytesAsync(string uri)
            => throw new InvalidOperationException("No real avatar requests allowed.");
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

namespace Flarial.Runtime.Unmanaged
{
    static class NativeMethods
    {
        internal static void ShellExecute(string uri)
            => throw new InvalidOperationException("No browser allowed in this test.");
    }
}
