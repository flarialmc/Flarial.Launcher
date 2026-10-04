using System;

namespace Flarial.Runtime.Client;

[Obsolete(" ", true)]
public sealed class ClientBeta : ClientBase<ClientBeta>
{
    private protected override string BlobName => "beta";
    private protected override string HashName => "Beta";
}