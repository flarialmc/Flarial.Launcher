using System;

namespace Flarial.Runtime.Core;

public sealed class FlarialClientAlpha : FlarialClient.Private<FlarialClientAlpha>
{
    private protected override string FileName => "Flarial.Client.Alpha.dll";
    private protected override string HashName => throw new NotImplementedException();
    private protected override string HashesUri => throw new NotImplementedException();
    private protected override string DownloadUri => throw new NotImplementedException();
}