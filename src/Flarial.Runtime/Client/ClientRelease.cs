using System;

namespace Flarial.Runtime.Client;

[Obsolete("Discord authentication is deprecated.", true)]
sealed class ClientRelease : ClientBase<ClientRelease>
{
    private protected override string BlobName => "latest";
    private protected override string HashName => "Release";
}