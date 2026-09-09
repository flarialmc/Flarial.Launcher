namespace Flarial.Runtime.Client;

public sealed class BetaClient : BaseClient<BetaClient>
{
    private protected override string BlobName => "beta";
    private protected override string HashName => "Beta";
}