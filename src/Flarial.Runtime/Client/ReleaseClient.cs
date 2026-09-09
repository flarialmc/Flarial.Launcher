namespace Flarial.Runtime.Client;

public sealed class ReleaseClient : BaseClient<ReleaseClient>
{
    private protected override string BlobName => "latest";
    private protected override string HashName => "Release";
}