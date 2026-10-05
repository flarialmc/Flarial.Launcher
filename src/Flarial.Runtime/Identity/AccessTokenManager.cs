using System;
using System.Text.Json;
using System.Threading;
using Flarial.Runtime.Services;

namespace Flarial.Runtime.Identity;

sealed class AccessTokenManager : CredentialService<AccessTokenManager>
{
    private protected override string Username => "Flarial Access v1";
    private protected override string Resource => "Flarial Launcher";

    long _expiresAt;
    internal long ExpiresAt => Interlocked.Read(ref _expiresAt);

    internal void Publish(string accessToken, long expiresAt)
    {
        Set(JsonSerializer.Serialize(new
        {
            version = 1,
            access_token = accessToken,
            expires_at = expiresAt
        }));
        Interlocked.Exchange(ref _expiresAt, expiresAt);
    }

    internal void Clear()
    {
        Interlocked.Exchange(ref _expiresAt, 0);
        Remove();
    }
}
