using System.IO;
using System.Net.Http;
using System.Text;
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

    private protected override string HashName => "commitHash";
    private protected override string FileName => "Flarial.Client.Beta.dll";
    private protected override string HashesUri => "https://api.flarial.xyz/api/v2/beta/dll/hash";

    internal string? AccessToken
    {
        set => Interlocked.Exchange(ref field, value);
        get => Interlocked.CompareExchange(ref field, null, null);
    }

    private protected override async Task<bool> VerifyClientAsync()
    {
        /*
            - Inspect the client's commit hash via an exported symbol.
            - Somewhat "efficient" over an actual hash when updating.
        */

        var hash = $"_{await GetRemoteHashAsync()}_"; unsafe
        {
            fixed (byte* ptr = Encoding.UTF8.GetBytes(hash))
            {
                var path = Path.GetFullPath(FileName);

                if (DONT_RESOLVE_DLL_REFERENCES.Open(path) is not { } module)
                    return false;

                using (module)
                    return !GetProcAddress(module, new(ptr)).IsNull;
            }
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