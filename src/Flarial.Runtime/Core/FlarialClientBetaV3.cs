using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Flarial.Runtime.Services;
using Flarial.Runtime.Unmanaged;
using static Windows.Win32.PInvoke;
using static Windows.Win32.System.LibraryLoader.LOAD_LIBRARY_FLAGS;

namespace Flarial.Runtime.Core;

/*
    - Flarial V3 beta product.
    - Same contract as the dll-css beta, with `product=v3` on both routes.
    - Uses its own cache file, so it never touches the dll-css beta cache.
*/

public sealed class FlarialClientBetaV3 : FlarialClient<FlarialClientBetaV3>
{
    const string DownloadUri = "https://api.flarial.xyz/api/v2/beta/dll?product=v3";
    const string CommitHeader = "X-Flarial-Beta-Commit";
    const long MaximumLength = 32L * 1024 * 1024;

    private protected override string HashName => "commitHash";
    private protected override string FileName => "Flarial.Client.Beta.V3.dll";
    private protected override string HashesUri => "https://api.flarial.xyz/api/v2/beta/dll/hash?product=v3";

    string PartialFileName => $"{FileName}.partial";

    // Commit hash returned by the last hash check, required to validate a download.
    string? _commitHash;

    internal static bool IsCommitHash(string? value)
    {
        return value is { Length: 40 } && value.All(static _ => _ is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    }

    static unsafe bool HasCommitExport(string path, string commitHash)
    {
        fixed (byte* ptr = Encoding.UTF8.GetBytes($"_{commitHash}_\0"))
        {
            if (DONT_RESOLVE_DLL_REFERENCES.Open(Path.GetFullPath(path)) is not { } module)
                return false;

            using (module)
                return !GetProcAddress(module, new(ptr)).IsNull;
        }
    }

    private protected override async Task<bool> VerifyAsync()
    {
        _commitHash = null;

        /*
            - V3 answers 503 `beta_build_unavailable` until a V3 build is published.
            - Treat any hash failure as "no usable V3 build" instead of crashing the launcher.
        */

        string hash;
        try { hash = await GetRemoteHashAsync(); }
        catch { return false; }

        if (!IsCommitHash(hash))
            return false;

        _commitHash = hash;
        return HasCommitExport(FileName, hash);
    }

    private protected override async Task<bool> OnDownloadAsync<T>(T progress)
    {
        if (_commitHash is not { } commitHash || AccessToken is not { } accessToken)
            return false;

        var success = false;
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Post, DownloadUri);
            request.Headers.Authorization = new("Bearer", accessToken);

            // Redirects are never followed, so a 3xx fails the exact-200 check below.
            using var response = await HttpService.SendWithoutRedirectsAsync(request);
            if (response.StatusCode is not HttpStatusCode.OK)
                return false;

            if (response.Headers.TryGetValues(CommitHeader, out var values) && values.SingleOrDefault() != commitHash)
                return false;

            var length = response.Content.Headers.ContentLength;
            if (length > MaximumLength)
                return false;

            using (var destination = File.Create(PartialFileName))
            using (var source = await response.Content.ReadAsStreamAsync())
            {
                long total = 0;
                int count;

                var buffer = new byte[Environment.SystemPageSize];
                while ((count = await source.ReadAsync(buffer)) != 0)
                {
                    if ((total += count) > MaximumLength)
                        return false;

                    await destination.WriteAsync(buffer.AsMemory(0, count));

                    if (length > 0)
                        progress.Report((int)((double)total / length.Value * 100));
                }
            }

            if (!HasCommitExport(PartialFileName, commitHash))
                return false;

            File.Move(PartialFileName, FileName, true);
            return success = true;
        }
        catch { return false; }
        finally
        {
            if (!success)
                try { File.Delete(PartialFileName); } catch { }
        }
    }
}
