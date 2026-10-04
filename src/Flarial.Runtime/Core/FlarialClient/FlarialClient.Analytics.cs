using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Flarial.Runtime.Services;

namespace Flarial.Runtime.Core;

partial class FlarialClient
{
    const string UserAgent = "Samsung Smart Fridge";
    const string AnalyticsUri = "https://api.flarial.xyz/launcher/events/launch";

    static readonly Uri s_uri;
    static readonly string s_identifier;

    static FlarialClient()
    {
        // Windows used SystemIdentification; here a random id persisted in the launcher data directory.
        const string file = "install-id";
        string identifier;
        try { identifier = File.ReadAllText(file).Trim(); }
        catch
        {
            identifier = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            try { File.WriteAllText(file, identifier); } catch { }
        }

        s_uri = new(AnalyticsUri);
        s_identifier = identifier;
    }

    static async Task PostAnalyticsAsync()
    {
        var timestamp = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        var json = $"{{\"timestamp\":\"{timestamp}\",\"installId\":\"{s_identifier}\"}}";

        using StringContent content = new(json, Encoding.UTF8, "application/json");
        using HttpRequestMessage request = new(HttpMethod.Post, s_uri) { Content = content };

        request.Headers.Add("User-Agent", UserAgent);
        request.Headers.Add("X-Flarial.Timestamp", timestamp);
        request.Headers.Add("X-Flarial-InstallId", s_identifier);
        request.Headers.Add("X-Flarial-Nonce", $"{Guid.NewGuid():N}");

        using (await HttpService.SendAsync(request)) { }
    }
}
