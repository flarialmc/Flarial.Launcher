using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Flarial.Runtime.Services;
using Flarial.Runtime.Unmanaged;
using static Windows.Win32.PInvoke;
using static Windows.Win32.System.LibraryLoader.LOAD_LIBRARY_FLAGS;

namespace Flarial.Runtime.Core;

/*
    - Flarial V3 beta product, surfaced in the launcher as "Alpha (v3)".
    - Same contract as the dll-css beta, with `product=v3` on both routes.
    - Uses its own cache & partial files, so it never touches the dll-css beta cache.
    - Requests carry the current Flarial access token & never follow redirects.
*/

public enum FlarialClientAlphaV3Failure
{
    None,

    // No access token or the API rejected it (401 / 403).
    Unauthorized,

    // The API answered 503 `beta_build_unavailable`, no V3 build is published.
    Unavailable,

    // Anything else: network errors, unexpected statuses, malformed or oversized responses, commit mismatches.
    Invalid
}

public sealed class FlarialClientAlphaV3 : FlarialClient.Private<FlarialClientAlphaV3>
{
    const string CommitHeader = "X-Flarial-Beta-Commit";
    const long MaximumLength = 32L * 1024 * 1024;
    const int MaximumHashLength = 64 * 1024;

    private protected override string HashName => "commitHash";
    private protected override string FileName => "Flarial.Client.Alpha.dll";
    private protected override string HashesUri => "https://api.flarial.xyz/api/v2/beta/dll/hash?product=v3";
    private protected override string DownloadUri => "https://api.flarial.xyz/api/v2/beta/dll?product=v3";

    string PartialFileName => $"{FileName}.partial";

    // Commit hash returned by the last hash check, required to validate a download.
    string? _commitHash;

    public FlarialClientAlphaV3Failure Failure { get; private set; }

    internal static bool IsCommitHash(string? value)
    {
        return value is { Length: 40 } && value.All(static _ => _ is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    }

    static FlarialClientAlphaV3Failure GetFailure(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => FlarialClientAlphaV3Failure.Unauthorized,
        HttpStatusCode.ServiceUnavailable => FlarialClientAlphaV3Failure.Unavailable,
        _ => FlarialClientAlphaV3Failure.Invalid
    };

    static unsafe bool HasCommitExport(string path, string commitHash)
    {
        if (!File.Exists(path))
            return false;

        fixed (byte* ptr = Encoding.UTF8.GetBytes($"_{commitHash}_\0"))
        {
            if (DONT_RESOLVE_DLL_REFERENCES.Open(Path.GetFullPath(path)) is not { } module)
                return false;

            using (module)
                return !GetProcAddress(module, new(ptr)).IsNull;
        }
    }

    async Task<string?> GetCommitHashAsync(string accessToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, HashesUri);
        request.Headers.Authorization = new("Bearer", accessToken);

        // Redirects are never followed, so a 3xx fails the exact-200 check below.
        using var response = await HttpService.SendWithoutRedirectsAsync(request);

        if (response.StatusCode is not HttpStatusCode.OK)
        {
            Failure = GetFailure(response.StatusCode);
            return null;
        }

        if (response.Content.Headers.ContentLength > MaximumHashLength)
            return null;

        using MemoryStream buffer = new();
        using (var source = await response.Content.ReadAsStreamAsync())
            if (!await CopyBoundedAsync(source, buffer, MaximumHashLength, null, null))
                return null;

        using var document = JsonDocument.Parse(buffer.ToArray());

        if (document.RootElement.ValueKind is not JsonValueKind.Object ||
            !document.RootElement.TryGetProperty(HashName, out var property) ||
            property.ValueKind is not JsonValueKind.String)
            return null;

        var hash = property.GetString();
        return IsCommitHash(hash) ? hash : null;
    }

    static async Task<bool> CopyBoundedAsync(Stream source, Stream destination, long maximum, long? length, IProgress<int>? progress)
    {
        long total = 0;
        int count;

        var buffer = new byte[Environment.SystemPageSize];
        while ((count = await source.ReadAsync(buffer)) != 0)
        {
            if ((total += count) > maximum)
                return false;

            await destination.WriteAsync(buffer.AsMemory(0, count));

            if (length > 0)
                progress?.Report((int)Math.Min(100, (double)total / length.Value * 100));
        }

        return true;
    }

    private protected override async Task<bool> VerifyAsync()
    {
        _commitHash = null;
        Failure = FlarialClientAlphaV3Failure.Invalid;

        /*
            - V3 answers 503 `beta_build_unavailable` until a V3 build is published.
            - Treat any hash failure as "no usable V3 build" instead of crashing the launcher.
        */

        if (AccessToken is not { } accessToken)
        {
            Failure = FlarialClientAlphaV3Failure.Unauthorized;
            return false;
        }

        string? hash;
        try { hash = await GetCommitHashAsync(accessToken); }
        catch { return false; }

        if (hash is null)
            return false;

        _commitHash = hash;

        if (!HasCommitExport(FileName, hash))
            return false;

        Failure = FlarialClientAlphaV3Failure.None;
        return true;
    }

    private protected override async Task<bool> OnDownloadAsync<T>(T progress)
    {
        // A failed hash check already recorded why there is nothing to download.
        if (_commitHash is not { } commitHash)
            return false;

        if (AccessToken is not { } accessToken)
        {
            Failure = FlarialClientAlphaV3Failure.Unauthorized;
            return false;
        }

        Failure = FlarialClientAlphaV3Failure.Invalid;

        var success = false;
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Post, DownloadUri);
            request.Headers.Authorization = new("Bearer", accessToken);

            // Redirects are never followed, so a 3xx fails the exact-200 check below.
            using var response = await HttpService.SendWithoutRedirectsAsync(request);

            if (response.StatusCode is not HttpStatusCode.OK)
            {
                Failure = GetFailure(response.StatusCode);
                return false;
            }

            // The build must be the one the hash check announced.
            if (response.Headers.TryGetValues(CommitHeader, out var values) && values.SingleOrDefault() != commitHash)
                return false;

            var length = response.Content.Headers.ContentLength;
            if (length > MaximumLength)
                return false;

            using (var destination = File.Create(PartialFileName))
            using (var source = await response.Content.ReadAsStreamAsync())
                if (!await CopyBoundedAsync(source, destination, MaximumLength, length, progress))
                    return false;

            if (!HasCommitExport(PartialFileName, commitHash))
                return false;

            File.Move(PartialFileName, FileName, true);

            Failure = FlarialClientAlphaV3Failure.None;
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
