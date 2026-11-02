using System;
using System.Net;
using System.Net.Http;

namespace Flarial.Runtime.Services;

static partial class HttpService
{
    static readonly HttpClient s_client = new(new HttpClientHandler
    {
        AllowAutoRedirect = true,
        AutomaticDecompression = DecompressionMethods.All
    }, true);

    // The beta contract requires redirects to be rejected, so this client never follows them.
    static readonly HttpClient s_directClient = new(new HttpClientHandler
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.All
    }, true);

    static readonly int s_length = Environment.SystemPageSize;
}