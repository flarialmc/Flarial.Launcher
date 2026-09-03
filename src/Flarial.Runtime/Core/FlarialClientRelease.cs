using System.Threading.Tasks;
using Flarial.Runtime.Services;

namespace Flarial.Runtime.Core;

public sealed class FlarialClientRelease : FlarialClient<FlarialClientRelease>
{
    const string DownloadUri = "https://cdn.flarial.xyz/dll/latest.dll";

    private protected override string Build => "Release";
    private protected override string FileName => "Flarial.Client.Release.dll";
    private protected override string HashesUri => "https://cdn.flarial.xyz/dll_hashes.json";

    private protected override async Task<bool> DownloadClientAsync<T>(T progress)
    {
        await HttpService.DownloadAsync(DownloadUri, FileName, progress);
        return true;
    }
}