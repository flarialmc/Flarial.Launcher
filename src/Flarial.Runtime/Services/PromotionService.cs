using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Flarial.Runtime.Services;

public static class PromotionService
{
    const string PromotionsUri = "https://cdn.flarial.xyz/launcher/Promotions.json";

    public static async Task<Promotion[]> GetAsync()
    {
        try { return await HttpService.GetJsonAsync<Promotion[]>(PromotionsUri); }
        catch { return []; }
    }
}

public sealed class Promotion
{
    const string ImpressionUri = "https://api.flarial.xyz/api/v2/service/launcher/events/sponsor";

    readonly string _identifier;
    readonly Task<byte[]?> _task;

    public string Uri { get; }
    public string Image { get; }

    [JsonConstructor]
    internal Promotion(string uri, string image)
    {
        Uri = uri;
        Image = image;

        _task = HttpService.TryGetBytesAsync(image);
        _identifier = new Uri(uri).GetLeftPart(UriPartial.Authority);
    }

    public Task<byte[]?> GetImageAsync() => _task;

    public async Task OnClickAsync()
    {
        var payload = JsonService.Default.Write<Dictionary<string, string>>(new()
        {
            ["event_type"] = "click",
            ["promotion_id"] = _identifier,
            ["event_id"] = $"{Guid.NewGuid()}",
            ["occurred_at"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        });

        using StringContent content = new(payload, Encoding.UTF8, "application/json");
        using (await HttpService.PostAsync(ImpressionUri, content)) { }
    }
}