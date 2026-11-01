namespace Flarial.Runtime.Core;

public sealed class FlarialClientBeta : FlarialClient.Private<FlarialClientBeta>
{
    private protected override string HashName => "commitHash";
    private protected override string FileName => "Flarial.Client.Beta.dll";
    private protected override string DownloadUri => "https://api.flarial.xyz/api/v2/beta/dll";
    private protected override string HashesUri => "https://api.flarial.xyz/api/v2/beta/dll/hash";
}