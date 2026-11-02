using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Flarial.Runtime.Services;
using Flarial.Runtime.Unmanaged;
using static Windows.Win32.PInvoke;
using static Windows.Win32.System.LibraryLoader.LOAD_LIBRARY_FLAGS;

namespace Flarial.Runtime.Core;

partial class FlarialClient
{
    public abstract class Private : FlarialClient
    {
        private protected override async Task<bool> VerifyAsync()
        {
            /*
                - Inspect the client's commit hash via an exported symbol.
                - Somewhat "efficient" over an actual hash when updating.
            */

            return HasCommitExport(FileName, await GetRemoteHashAsync());
        }

        private protected static unsafe bool HasCommitExport(string path, string hash)
        {
            fixed (byte* ptr = Encoding.UTF8.GetBytes($"_{hash}_\0"))
            {
                if (DONT_RESOLVE_DLL_REFERENCES.Open(Path.GetFullPath(path)) is not { } module)
                    return false;

                using (module)
                    return !GetProcAddress(module, new(ptr)).IsNull;
            }
        }

        private protected override async Task<bool> OnDownloadAsync<T>(T progress)
        {
            using HttpRequestMessage request = new(HttpMethod.Post, DownloadUri);
            request.Headers.Authorization = new("Bearer", AccessToken);

            using var response = await HttpService.SendAsync(request);
            if (!response.IsSuccessStatusCode) return false;

            await response.DownloadAsync(FileName, progress);
            return true;
        }
    }
}