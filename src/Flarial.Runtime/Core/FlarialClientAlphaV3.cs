using System;

namespace Flarial.Runtime.Core;

public sealed class FlarialClientAlphaV3 : FlarialClient.Private<FlarialClientAlphaV3>
{
    private protected override string HashName => "commitHash";
    private protected override string FileName => "Flarial.Client.Alpha.dll";
    private protected override string DownloadUri => "https://api.flarial.xyz/api/v2/beta/dll?product=v3";
    private protected override string HashesUri => "https://api.flarial.xyz/api/v2/beta/dll/hash?product=v3";
}