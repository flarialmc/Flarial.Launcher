using System;

namespace Flarial.Runtime.Core;

public sealed class FlarialClientAlphaV3 : FlarialClient.Private<FlarialClientAlphaV3>
{
    private protected override string FileName => "Flarial.Client.Alpha.dll";
    private protected override string HashName => throw new NotImplementedException();
    private protected override string HashesUri => throw new NotImplementedException();
    private protected override string DownloadUri => throw new NotImplementedException();
}