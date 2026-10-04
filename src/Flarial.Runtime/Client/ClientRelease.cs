using System;

namespace Flarial.Runtime.Client;

[Obsolete(" ", true)]
public sealed class ClientRelease : ClientBase<ClientRelease>
{
    private protected override string BlobName => "latest";
    private protected override string HashName => "Release";
}