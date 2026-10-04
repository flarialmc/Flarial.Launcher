using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Flarial.Runtime.Game;
using Flarial.Runtime.Services;

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
            - The export table is read from the PE image instead of calling GetProcAddress on a loaded module.
        */

        var symbol = $"_{await GetRemoteHashAsync()}_";
        ModificationLibrary library = new(FileName);

        if (library.Open() is not { } image)
            return false;

        return image.Exports.Contains(symbol);
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