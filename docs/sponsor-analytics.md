# Sponsor banner analytics

Salad and Litebyte banners emit best-effort events to the public endpoint
`POST https://api.flarial.xyz/api/v2/service/launcher/events/sponsor`.
The launcher sends no credentials, destination URLs, install IDs, or user IDs.
Existing launch analytics stays unchanged.

An impression (`home_banner_rendered_v1`) means a decoded, nonempty bitmap was
drawn in Home while the banner and its ancestors were visible, the banner
intersected the window and ancestor clipping bounds, and the window was active
and not minimized. It counts once per banner per Home view attachment. Downloads,
redraws, and focus changes within that attachment do not add impressions.
Navigating away and back can add another impression. This is a rendering measure;
it does not prove that a person looked at the banner or that another application
was not covering it.

A left-button activation opens the existing sponsor URL first, then queues a click
event. Telemetry never waits on the click path. Failed image downloads or decoding
produce no banner impression. Other promotions still open normally without sponsor
telemetry. Sponsor IDs come from exact HTTPS destination hosts (`salad.com`,
`litebyte.co`, and their `www` variants). The destination itself is never sent.

Each event has exactly these fields:

- `event_id`: UUID v4, reused for all attempts of that event.
- `event_type`: `impression` or `click`.
- `occurred_at`: UTC ISO 8601 timestamp ending in `Z`.
- `sponsor_id`: `salad` or `litebyte`.
- `campaign_id`: optional promotion manifest `CampaignId`; otherwise
  `launcher-salad` or `launcher-litebyte`. It must match
  `[a-z0-9][a-z0-9_-]{0,63}`. Invalid campaign metadata disables telemetry for that
  banner without blocking its display or destination.
- `platform`: `windows`.
- `placement`: `home_banner`.
- `impression_definition`: `home_banner_rendered_v1` on both event types.

The client retains at most 32 outstanding events, sends one at a time, and makes
at most three attempts per event with a five-second request timeout. It retries
network failures, 429, and server errors using the same payload; other client
errors stop retries. No events are persisted across launcher exits. The API accepts
or deduplicates an event with 204, limits bodies to 2048 bytes, and validates event
age (past 24 hours, future five minutes). Server event IDs are globally unique,
with the first write retained.

The API/panel groups events by sponsor, campaign, platform, placement, and UTC time
window. CTR is `clicks / impressions * 100`, or null when there are no impressions.
These counts measure events rather than unique people. Repeated clicks and dropped
telemetry can produce CTR above 100%.

## Validation

Run the cross-platform contract and headless rendering checks:

```sh
dotnet run --project tests/SponsorAnalytics.Tests
```

Build the launcher on a non-Windows host:

```sh
dotnet build src/Flarial.Launcher -c Release -p:EnableWindowsTargeting=true -p:EnableMsixTooling=false -p:EnableCoreMrtTooling=false -p:ExpandPriResources=false
```

These overrides skip Windows-only MSIX/PRI tooling for the Linux compile check.
A full MSIX build still requires Windows.

Before release, check on Windows that both live banners display, each URL opens
with its existing UTM tags when the API is unavailable, and navigation back to Home
allows a fresh impression. Review the API/panel companion changes before enabling
reporting. No deployment is part of this PR.
