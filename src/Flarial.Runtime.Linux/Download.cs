using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Flarial.Runtime.Linux;

/// <summary>Resumable HTTP download with optional pinned SHA-256.</summary>
static class Download
{
    public static readonly HttpClient Http = Create();

    static HttpClient Create()
    {
        var c = new HttpClient(new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(30), AutomaticDecompression = DecompressionMethods.None }) { Timeout = Timeout.InfiniteTimeSpan };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("Flarial-Linux/1.0");
        return c;
    }

    public static async Task<string> Sha256Async(string path)
    {
        await using var s = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(s));
    }

    /// <summary>Downloads to <paramref name="dest"/> (via .part, resuming). progress(done,total).</summary>
    public static async Task FileAsync(string url, string dest, string? sha256, Action<long, long>? progress = null)
    {
        if (File.Exists(dest) && (sha256 is null || await Sha256Async(dest) == sha256)) { var n = new FileInfo(dest).Length; progress?.Invoke(n, n); return; }
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        var part = dest + ".part";

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                long have = File.Exists(part) ? new FileInfo(part).Length : 0;
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                if (have > 0) req.Headers.Range = new(have, null);
                using var res = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);

                if (res.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable) { File.Delete(part); continue; }
                res.EnsureSuccessStatusCode();
                if (res.StatusCode != HttpStatusCode.PartialContent) have = 0;
                var total = have + (res.Content.Headers.ContentLength ?? 0);

                await using (var src = await res.Content.ReadAsStreamAsync())
                await using (var dst = new FileStream(part, have > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write))
                {
                    var buf = new byte[1 << 17]; int r; long done = have, lastReport = 0;
                    while ((r = await src.ReadAsync(buf)) > 0)
                    {
                        await dst.WriteAsync(buf.AsMemory(0, r)); done += r;
                        if (done - lastReport >= 1 << 20 || done == total) { lastReport = done; progress?.Invoke(done, total); }
                    }
                }

                if (sha256 is not null && await Sha256Async(part) != sha256)
                {
                    File.Delete(part);
                    throw new InvalidDataException($"SHA-256 mismatch for {url}");
                }
                File.Move(part, dest, true);
                return;
            }
            catch (Exception e) when (attempt < 4 && e is HttpRequestException or IOException && e is not InvalidDataException)
            {
                await Task.Delay(2000 * (attempt + 1));
            }
        }
    }

    public static Task<string> StringAsync(string url) => Http.GetStringAsync(url);
}
