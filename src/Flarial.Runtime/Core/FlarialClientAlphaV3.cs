using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Flarial.Runtime.Services;

namespace Flarial.Runtime.Core;

// Flarial V3 beta, surfaced in the launcher as "Alpha (v3)".
public sealed class FlarialClientAlphaV3 : FlarialClient.Private<FlarialClientAlphaV3>
{
    const string CommitHeader = "X-Flarial-Beta-Commit";
    const long MaximumLength = 32L * 1024 * 1024;
    const long MaximumHashLength = 64 * 1024;

    private protected override string HashName => "commitHash";
    private protected override string FileName => "Flarial.Client.Alpha.dll";
    private protected override string HashesUri => "https://api.flarial.xyz/api/v2/beta/dll/hash?product=v3";
    private protected override string DownloadUri => "https://api.flarial.xyz/api/v2/beta/dll?product=v3";

    private protected override bool DeleteBeforeDownload => false;

    string PartialFileName => $"{FileName}.partial";

    // Commit hash returned by the last hash check, required to validate a download.
    string? _commitHash;

    public FlarialClientAlphaV3Failure Failure { get; private set; }

    static bool IsCommitHash(string? value)
    {
        return value is { Length: 40 } && value.All(static _ => _ is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    }

    static FlarialClientAlphaV3Failure GetFailure(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => FlarialClientAlphaV3Failure.Unauthorized,
        HttpStatusCode.ServiceUnavailable => FlarialClientAlphaV3Failure.Unavailable,
        _ => FlarialClientAlphaV3Failure.Failed
    };

    async Task<string?> GetCommitHashAsync()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, HashesUri);
        using var response = await HttpService.SendWithoutRedirectsAsync(request);

        if (response.StatusCode is not HttpStatusCode.OK)
        {
            Failure = GetFailure(response.StatusCode);
            return null;
        }

        using MemoryStream stream = new();

        await response.CopyToAsync(stream, MaximumHashLength);

        stream.Position = 0;
        var json = JsonService.Default.Read<Dictionary<string, string>>(stream);

        return json.TryGetValue(HashName, out var hash) && IsCommitHash(hash) ? hash : null;
    }

    private protected override async Task<bool> VerifyAsync()
    {
        _commitHash = null;
        Failure = FlarialClientAlphaV3Failure.Failed;

        try { _commitHash = await GetCommitHashAsync(); }
        catch { return false; }

        if (_commitHash is null || !HasCommitExport(FileName, _commitHash))
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

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Post, DownloadUri);
            request.Headers.Authorization = new("Bearer", accessToken);

            using var response = await HttpService.SendWithoutRedirectsAsync(request);

            if (response.StatusCode is not HttpStatusCode.OK)
            {
                Failure = GetFailure(response.StatusCode);
                return false;
            }

            if (response.Headers.TryGetValues(CommitHeader, out var values) && values.SingleOrDefault() != commitHash)
                return false;

            using (var destination = File.Create(PartialFileName))
                await response.CopyToAsync(destination, MaximumLength, progress);

            if (!HasCommitExport(PartialFileName, commitHash))
                return false;

            File.Move(PartialFileName, FileName, true);

            Failure = FlarialClientAlphaV3Failure.None;
            return true;
        }
        catch { return false; }
        finally { try { File.Delete(PartialFileName); } catch { } }
    }
}