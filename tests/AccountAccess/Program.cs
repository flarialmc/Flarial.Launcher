using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Flarial.Runtime.Core;
using Flarial.Runtime.Identity;
using Flarial.Runtime.Services;

static class Program
{
    static void Require(bool condition, string failure)
    {
        if (!condition) throw new InvalidOperationException(failure);
    }

    static HttpResponseMessage Tokens(string access = "fixture.access.token", string refresh = "rotated-refresh", int ttl = 3600)
        => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new
        {
            access_token = access, refresh_token = refresh, expires_in = ttl
        })) };

    static void CheckRecord(string access)
    {
        var json = CredentialService.Vault[("Flarial Launcher", "Flarial Access v1")];
        using var document = JsonDocument.Parse(json);
        var record = document.RootElement;
        Require(record.GetProperty("version").GetInt32() == 1, "Wrong access record version.");
        Require(record.GetProperty("access_token").GetString() == access, "Wrong published access token.");
        Require(record.GetProperty("expires_at").GetInt64() == AccessTokenManager._.ExpiresAt,
            "Published expiry differs from renewal state.");
        Require(!record.TryGetProperty("refresh_token", out _), "Refresh token leaked into access entry.");
    }

    static async Task Main()
    {
        RefreshTokenManager._.Set("fixture-refresh");
        HttpService.Post = async (uri, content, _) =>
        {
            Require(uri.EndsWith("/token"), "Unexpected endpoint.");
            var body = await content.ReadAsStringAsync();
            Require(body.Contains("refresh_token=fixture-refresh"), "Wrong refresh owner credential.");
            return Tokens();
        };
        var started = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Require(await AuthenticationManager.AuthenticateSilentlyAsync() == "fixture.access.token", "Refresh failed.");
        Require(RefreshTokenManager._.Get() == "rotated-refresh", "Launcher did not persist rotation.");
        Require(AccessTokenManager._.ExpiresAt >= started + 3600, "Expiry did not use provider TTL.");
        CheckRecord("fixture.access.token");

        int renewals = 0;
        HttpService.Post = (_, _, _) => { ++renewals; return Task.FromResult(Tokens()); };
        await AccountManager.RenewAccessAsync();
        Require(renewals == 0, "Healthy access renewed too early.");
        AccessTokenManager._.Publish("near-expiry", DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60);
        HttpService.Post = (_, _, token) =>
        {
            Require(token.CanBeCanceled, "Background exchange has no timeout.");
            ++renewals;
            return Task.FromResult(Tokens("renewed.access", "renewed-refresh"));
        };
        await AccountManager.RenewAccessAsync();
        Require(renewals == 1 && RefreshTokenManager._.Get() == "renewed-refresh", "Renewal did not save rotation.");
        CheckRecord("renewed.access");

        AccessTokenManager._.Publish("near-expiry", DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60);
        var beforeFailure = AccessTokenManager._.Get();
        HttpService.Post = (_, _, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        await AccountManager.RenewAccessAsync();
        Require(RefreshTokenManager._.Get() == "renewed-refresh" && AccessTokenManager._.Get() == beforeFailure,
            "Transient provider failure deleted login or usable access.");

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        HttpService.Post = async (uri, _, _) =>
        {
            Require(uri.EndsWith("/token"), "Unexpected concurrent endpoint.");
            entered.SetResult();
            await finish.Task;
            return Tokens("account.b.access", "account-b-refresh");
        };
        HttpService.Send = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"user\":{\"avatar_url\":null,\"display_name\":\"Fixture\"},\"entitlements\":{\"beta\":{\"active\":true},\"flarial_plus\":{\"active\":true}}}")
        });
        var login = AccountManager.LoginAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await AccountManager.RenewAccessAsync();
        finish.SetResult();
        Require(await login is not null, "Concurrent login failed.");
        CheckRecord("account.b.access");

        var revoking = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishRevoke = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        HttpService.Post = async (uri, _, _) =>
        {
            if (uri.EndsWith("/token")) return new(HttpStatusCode.Unauthorized);
            Require(uri.EndsWith("/revoke"), "Unexpected revocation endpoint.");
            revoking.SetResult();
            await finishRevoke.Task;
            return new(HttpStatusCode.OK);
        };
        var denied = AccountManager.LoginAsync();
        await revoking.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Require(!denied.IsCompleted, "Failed refresh returned before cleanup finished.");
        Require(RefreshTokenManager._.Get() is null && AccessTokenManager._.Get() is null &&
            FlarialClientBeta._.AccessToken is null && AccessTokenManager._.ExpiresAt == 0,
            "Revocation left credentials or beta access.");
        finishRevoke.SetResult();
        Require(await denied is null, "Failed refresh logged in.");
        AccessTokenManager._.Publish("stale-access", DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60);
        FlarialClientBeta._.AccessToken = "stale-access";
        await AccountManager.LogoutAsync();
        Require(AccessTokenManager._.Get() is null && FlarialClientBeta._.AccessToken is null,
            "Missing refresh entry left access behind after logout.");

        RefreshTokenManager._.Set("fixture-refresh");
        HttpService.Post = (_, _, _) => Task.FromResult(Tokens(ttl: 1));
        Require(await AuthenticationManager.AuthenticateSilentlyAsync() is null, "Near-expired token was published.");
        Require(RefreshTokenManager._.Get() == "rotated-refresh" && AccessTokenManager._.Get() is null,
            "Unusable access lost the rotated login credential or leaked access.");
        Console.WriteLine("Launcher access handoff, renewal, rotation, exclusion, logout and failure checks passed.");
    }
}
