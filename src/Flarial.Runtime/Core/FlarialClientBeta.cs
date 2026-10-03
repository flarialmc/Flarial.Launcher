using System;
using System.Buffers.Text;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Flarial.Runtime.Identity.Flarial;
using System.Diagnostics.CodeAnalysis;
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

[Experimental("Flarial_Runtime_Identity_Flarial")]
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

    // Personalized builds are single-use activation material. Fetch again for a new launch.
    private protected override Task<bool> VerifyClientAsync() => Task.FromResult(false);

    string? _downloadAccount;

    static string AccountId(string accessToken)
    {
        var parts = accessToken.Split('.');
        if (parts.Length != 3) throw new InvalidOperationException("Expected a Flarial access token.");
        using var document = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1]));
        return document.RootElement.GetProperty("sub").GetString()
            ?? throw new InvalidOperationException("Missing account ID.");
    }

    private protected override async Task<string?> PrepareLaunchAsync()
    {
        var token = await AuthenticationManager.AuthenticateSilentlyAsync().ConfigureAwait(false);
        if (token is null || _downloadAccount is null || AccountId(token) != _downloadAccount)
            throw new InvalidOperationException("Sign in and download beta with the same account before launching.");
        _downloadAccount = null; // Do not attempt to reuse consumed activation material.
        return token;
    }

    public override Task<bool> DownloadAsync<T>(T progress) => DownloadClientAsync(progress);

    private protected override async Task<bool> DownloadClientAsync<T>(T progress)
    {
        _downloadAccount = null;
        var token = await AuthenticationManager.AuthenticateSilentlyAsync().ConfigureAwait(false);
        if (token is null) return false;
        var accountId = AccountId(token);
        var temp = FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
            using HttpRequestMessage request = new(HttpMethod.Post, DownloadUri);
            request.Headers.Authorization = new("Bearer", token);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            if (response.StatusCode != System.Net.HttpStatusCode.OK) return false;
            var commit = response.Headers.GetValues("X-Flarial-Beta-Commit").Single();
            var expectedHash = response.Headers.GetValues("X-Flarial-Dll-Sha256").Single();
            if (commit.Length != 40 || commit.Any(c => !char.IsAsciiHexDigit(c))) return false;
            const int maximum = 32 * 1024 * 1024;
            if (response.Content.Headers.ContentLength > maximum) return false;
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            using (var destination = File.Create(temp))
            using (var source = await response.Content.ReadAsStreamAsync(cancellation.Token).ConfigureAwait(false))
            {
                byte[] buffer = new byte[64 * 1024];
                int total = 0, count;
                while ((count = await source.ReadAsync(buffer, cancellation.Token).ConfigureAwait(false)) != 0)
                {
                    total += count;
                    if (total > maximum) throw new IOException("Beta DLL exceeds 32 MiB.");
                    hash.AppendData(buffer, 0, count);
                    await destination.WriteAsync(buffer.AsMemory(0, count), cancellation.Token).ConfigureAwait(false);
                    if (response.Content.Headers.ContentLength is > 0 and var size)
                        progress.Report((int)(100L * total / size));
                }
            }
            if (!Convert.ToHexString(hash.GetHashAndReset()).Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Beta DLL SHA-256 mismatch.");
            if (!HasCommitExport(temp, commit)) throw new IOException("Beta DLL commit export mismatch.");
            File.Move(temp, FileName, true);
            _downloadAccount = accountId;
            return true;
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    static unsafe bool HasCommitExport(string path, string commit)
    {
        using var module = DONT_RESOLVE_DLL_REFERENCES.Open(Path.GetFullPath(path));
        if (module is null) return false;
        fixed (byte* ptr = Encoding.UTF8.GetBytes("_" + commit + "_\0"))
            return !GetProcAddress(module.Value, new(ptr)).IsNull;
    }
}
