using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Flarial.Runtime.Services;

static partial class HttpService
{
    internal static async Task DownloadAsync<T>(string uri, string path, T progress) where T : IProgress<int>
    {
        using var response = await GetAsync(uri, default);
        await response.DownloadAsync(path, progress);
    }

    extension(HttpResponseMessage response)
    {
        internal async Task DownloadAsync<T>(string path, T progress) where T : IProgress<int>
        {
            try
            {
                response.EnsureSuccessStatusCode();

                using var destination = File.Create(path);
                await response.CopyToAsync(destination, long.MaxValue, progress);
            }
            catch { try { File.Delete(path); } catch { } throw; }
        }

        internal async Task CopyToAsync(Stream destination, long maximum, IProgress<int>? progress = null)
        {
            var length = response.Content.Headers.ContentLength ?? 0;

            if (length > maximum)
                throw new InvalidDataException("Response exceeds the download limit.");

            using var source = await response.Content.ReadAsStreamAsync();

            int count = 0;
            long received = 0;

            var buffer = new byte[s_length];

            while ((count = await source.ReadAsync(buffer)) != 0)
            {
                if (count > maximum - received)
                    throw new InvalidDataException("Response exceeds the download limit.");

                var memory = buffer.AsMemory(0, count);
                await destination.WriteAsync(memory);

                received += count;

                if (length <= 0 || progress is null)
                    continue;

                progress.Report((int)((double)received / length * 100));
            }
        }
    }
}