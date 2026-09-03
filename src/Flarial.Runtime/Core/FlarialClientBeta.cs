using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Flarial.Runtime.Services;
using Flarial.Runtime.Unmanaged;
using static Windows.Win32.PInvoke;
using static Windows.Win32.System.LibraryLoader.LOAD_LIBRARY_FLAGS;

namespace Flarial.Runtime.Core;

public sealed class FlarialClientBeta : FlarialClient<FlarialClientBeta>
{
    const string DownloadUri = "https://api.flarial.xyz/api/v2/beta/dll";

    private protected override string Build => "commitHash";
    private protected override string FileName => "Flarial.Client.Beta.dll";
    private protected override string HashesUri => "https://api.flarial.xyz/api/v2/beta/dll/hash";

    internal string? AccessToken
    {
        set => Interlocked.Exchange(ref field, value);
        get => Interlocked.CompareExchange(ref field, null, null);
    }

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

    private protected override async Task<bool> DownloadClientAsync<T>(T progress)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, DownloadUri);
        request.Headers.Authorization = new("Bearer", AccessToken);

        using var response = await HttpService.SendAsync(request);
        if (!response.IsSuccessStatusCode) return false;

        await response.DownloadAsync(FileName, progress);
        return true;
    }
}