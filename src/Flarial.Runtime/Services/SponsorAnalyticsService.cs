using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Flarial.Runtime.Services;

public static class SponsorAnalyticsService
{
    public const string Endpoint = "https://api.flarial.xyz/api/v2/service/launcher/events/sponsor";
    public const string ImpressionDefinition = "home_banner_rendered_v1";
    static readonly HttpClient s_client = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(5)
    };
    static readonly SemaphoreSlim s_sender = new(1);
    static int s_pending;

    public static bool TryIdentify(string destination, string? campaign, out string sponsorId, out string campaignId)
    {
        sponsorId = campaignId = "";
        if (!Uri.TryCreate(destination, UriKind.Absolute, out var uri) || uri.Scheme != "https") return false;
        sponsorId = uri.Host switch
        {
            "salad.com" or "www.salad.com" => "salad",
            "litebyte.co" or "www.litebyte.co" => "litebyte",
            _ => ""
        };
        if (sponsorId.Length == 0) return false;
        campaignId = campaign ?? $"launcher-{sponsorId}";
        return IsCampaignValid(campaignId);
    }

    public static bool IsCampaignValid(string campaign)
    {
        if (campaign.Length is < 1 or > 64 || !IsAlphaNumeric(campaign[0])) return false;
        foreach (var c in campaign)
            if (!IsAlphaNumeric(c) && c != '_' && c != '-') return false;
        return true;
    }

    static bool IsAlphaNumeric(char c) => c is >= 'a' and <= 'z' or >= '0' and <= '9';

    public static string CreatePayload(string sponsorId, string campaignId, string eventType)
    {
        if (sponsorId is not ("salad" or "litebyte") || !IsCampaignValid(campaignId) ||
            eventType is not ("impression" or "click")) throw new ArgumentException("Invalid sponsor event");
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("event_id", Guid.NewGuid().ToString());
            writer.WriteString("event_type", eventType);
            writer.WriteString("occurred_at", DateTime.UtcNow.ToString("O"));
            writer.WriteString("sponsor_id", sponsorId);
            writer.WriteString("campaign_id", campaignId);
            writer.WriteString("platform", "windows");
            writer.WriteString("placement", "home_banner");
            writer.WriteString("impression_definition", ImpressionDefinition);
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static void Record(string sponsorId, string campaignId, string eventType)
    {
        if (Interlocked.Increment(ref s_pending) > 32)
        {
            Interlocked.Decrement(ref s_pending);
            return;
        }
        try
        {
            var payload = CreatePayload(sponsorId, campaignId, eventType);
            _ = Task.Run(async () =>
            {
                try { await SendAsync(payload, s_client, static delay => Task.Delay(delay)); }
                finally { Interlocked.Decrement(ref s_pending); }
            });
        }
        catch { Interlocked.Decrement(ref s_pending); }
    }

    internal static async Task SendAsync(string payload, HttpClient client, Func<TimeSpan, Task> delay)
    {
        await s_sender.WaitAsync();
        try
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    using StringContent content = new(payload, Encoding.UTF8, "application/json");
                    using var response = await client.PostAsync(Endpoint, content);
                    if (response.IsSuccessStatusCode) return;
                    if (response.StatusCode != HttpStatusCode.TooManyRequests && (int)response.StatusCode < 500) return;
                }
                catch (HttpRequestException) { }
                catch (TaskCanceledException) { }
                if (attempt < 2) await delay(TimeSpan.FromSeconds(attempt + 1));
            }
        }
        catch { }
        finally
        {
            s_sender.Release();
        }
    }
}
