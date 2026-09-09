namespace Flarial.Runtime.Client;

public sealed class ClientRelease : ClientBase<ClientRelease>
{
    private protected override string BlobName => "latest";
    private protected override string HashName => "Release";
}