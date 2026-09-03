using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Flarial.Runtime.Services;
using Flarial.Runtime.Unmanaged;
using static Windows.Win32.PInvoke;
using static Windows.Win32.System.LibraryLoader.LOAD_LIBRARY_FLAGS;

namespace Flarial.Runtime.Core;

public sealed class FlarialClientBeta : FlarialClient<FlarialClientBeta>
{
    const string DownloadUri = "https://cdn.flarial.xyz/dll/beta.dll";

    private protected override string Build => "commitHash";
    private protected override string FileName => "Flarial.Client.Beta.dll";
    private protected override string HashesUri => "https://api.flarial.xyz/api/v2/beta/dll/hash";

    private protected unsafe override Task<string> GetLocalHashAsync()
    {
        var path = Path.GetFullPath(FileName);

        if (DONT_RESOLVE_DLL_REFERENCES.Open(path) is not { } module)
            return Task.FromResult(string.Empty);

        using (module) fixed (byte* ptr = "FlarialGetCommitHash"u8)
        {
            if (GetProcAddress(module, new(ptr)) is not { IsNull: false } procedure)
                return Task.FromResult(string.Empty);

            var action = (delegate* unmanaged[Stdcall]<sbyte*>)(nint)procedure;
            return Task.FromResult<string>(new(action()));
        }
    }

    private protected override Task<string> GetDownloadUriAsync()
    {
        return Task.FromResult(DownloadUri);
    }
}