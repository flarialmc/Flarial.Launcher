using System.Net;
using System.Text.Json;
using Flarial.Runtime.Services;

static void Check(bool value, string message)
{
    if (!value) throw new Exception(message);
}

Check(SponsorAnalyticsService.TryIdentify("https://salad.com/download?utm_campaign=private", null, out var sponsor, out var campaign)
      && sponsor == "salad" && campaign == "launcher-salad", "Salad fallback");
Check(SponsorAnalyticsService.TryIdentify("https://litebyte.co/minecraft", "summer_2026", out sponsor, out campaign)
      && sponsor == "litebyte" && campaign == "summer_2026", "Litebyte explicit campaign");
foreach (var uri in new[] { "http://salad.com", "https://salad.com.evil.test", "https://evil.test/salad.com", "invalid" })
    Check(!SponsorAnalyticsService.TryIdentify(uri, null, out _, out _), "Unknown sponsor rejected");
foreach (var slug in new[] { "", "A", "-campaign", "has space", new string('a', 65), "https://example.com" })
    Check(!SponsorAnalyticsService.TryIdentify("https://salad.com", slug, out _, out _), "Invalid campaign rejected");
Check(SponsorAnalyticsService.IsCampaignValid(new string('a', 64)), "Campaign length boundary");
var payload = SponsorAnalyticsService.CreatePayload("salad", "launcher-salad", "impression");
using var json = JsonDocument.Parse(payload);
var root = json.RootElement;
var fields = root.EnumerateObject().Select(x => x.Name).Order().ToArray();
Check(fields.SequenceEqual(new[] { "campaign_id", "event_id", "event_type", "impression_definition", "occurred_at", "placement", "platform", "sponsor_id" }), "Exact privacy-safe contract fields");
var id = root.GetProperty("event_id").GetString()!;
Check(Guid.TryParse(id, out _) && id[14] == '4', "UUID v4");
Check(root.GetProperty("occurred_at").GetString()!.EndsWith('Z'), "UTC Z timestamp");
Check(root.GetProperty("impression_definition").GetString() == "home_banner_rendered_v1", "Impression definition");
Check(root.GetProperty("platform").GetString() == "windows" && root.GetProperty("placement").GetString() == "home_banner", "Context");
Check(payload.Length < 2048 && !payload.Contains("private"), "Bounded body without URL");
var click = SponsorAnalyticsService.CreatePayload("litebyte", "launcher-litebyte", "click");
Check(click != payload && click.Contains("home_banner_rendered_v1"), "Click has separate ID and definition");
foreach (var status in new[] { HttpStatusCode.NoContent, HttpStatusCode.BadRequest, HttpStatusCode.TooManyRequests, HttpStatusCode.InternalServerError })
{
    var handler = new RecordingHandler(status);
    using var client = new HttpClient(handler);
    await SponsorAnalyticsService.SendAsync(payload, client, _ => Task.CompletedTask);
    var expected = status is HttpStatusCode.TooManyRequests or HttpStatusCode.InternalServerError ? 3 : 1;
    Check(handler.Bodies.Count == expected, "Bounded retries and permanent-error handling");
    Check(handler.Bodies.All(x => x == payload), "Retry payload/ID stable");
}
RenderChecks.Run();
Console.WriteLine("Sponsor contract, validation, privacy, and retry checks passed.");

sealed class RecordingHandler(HttpStatusCode status) : HttpMessageHandler
{
    public List<string> Bodies { get; } = [];
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        if (request.RequestUri!.AbsoluteUri != SponsorAnalyticsService.Endpoint) throw new Exception("Wrong endpoint");
        Bodies.Add(await request.Content!.ReadAsStringAsync(token));
        return new HttpResponseMessage(status);
    }
}
