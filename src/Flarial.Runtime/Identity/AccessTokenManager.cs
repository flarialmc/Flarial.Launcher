using System.Buffers;
using System.Text;
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
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", 1);
            writer.WriteString("access_token", accessToken);
            writer.WriteNumber("expires_at", expiresAt);
            writer.WriteEndObject();
        }
        Set(Encoding.UTF8.GetString(buffer.WrittenSpan));
        Interlocked.Exchange(ref _expiresAt, expiresAt);
    }

    internal void Clear()
    {
        Interlocked.Exchange(ref _expiresAt, 0);
        Remove();
    }
}
