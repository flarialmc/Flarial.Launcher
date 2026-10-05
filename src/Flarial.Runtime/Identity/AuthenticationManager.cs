using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Flarial.Runtime.Core;
using Flarial.Runtime.Services;
using Flarial.Runtime.Unmanaged;

namespace Flarial.Runtime.Identity;

static class AuthenticationManager
{
    static readonly byte[] s_response = [.. "You may close this window now."u8];

    const string AccessToken = "access_token";
    const string RefreshToken = "refresh_token";
    const string AuthorizationCode = "authorization_code";

    const string ClientId = "flarial-desktop";
    const string Scope = "openid profile entitlements offline_access";

    const string ResourceUri = "https://flarial.xyz/api";
    const string AuthenticateUri = $"{ResourceUri}/auth/oauth2";

    const string TokenUri = $"{AuthenticateUri}/token";
    const string RevokeUri = $"{AuthenticateUri}/revoke";
    const string AuthorizeUri = $"{AuthenticateUri}/authorize?response_type=code&code_challenge_method=S256&resource={ResourceUri}&client_id={ClientId}&scope={Scope}&state={{0}}&code_challenge={{1}}&redirect_uri={{2}}";

    static async Task<(string AuthorizationCode, string CodeVerifier, string RedirectUri)?> GetAuthorizationAsync()
    {
        var state = RequestHelper.CreateApplicationState();
        var (verifier, challenge) = RequestHelper.CreateCodeExchange();

        var redirectUri = $"{RequestHelper.CreateRedirectUri()}/oauth/callback";
        var requestUri = string.Format(AuthorizeUri, state, challenge, redirectUri);

        using HttpListener listener = new();
        listener.Prefixes.Add($"{redirectUri}/");

        listener.Start(); try
        {
            NativeMethods.ShellExecute(requestUri);

            var context = await listener.GetContextAsync();
            using var stream = context.Response.OutputStream;

            context.Response.ContentLength64 = s_response.Length;
            await stream.WriteAsync(s_response);

            if (context.Request.QueryString["state"] != state)
                return null;

            if (context.Request.QueryString["code"] is not { } code)
                return null;

            return new()
            {
                CodeVerifier = verifier,
                AuthorizationCode = code,
                RedirectUri = redirectUri
            };
        }
        finally { listener.Stop(); }
    }

    static async Task<(string AccessToken, string RefreshToken, long ExpiresAt)?> ParseTokensAsync(
        HttpResponseMessage response, long requestedAt)
    {
        using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);

        var accessToken = document.RootElement.GetProperty(AccessToken);
        var refreshToken = document.RootElement.GetProperty(RefreshToken);
        long expiresIn = 0;
        if (document.RootElement.TryGetProperty("expires_in", out var expiry) &&
            expiry.ValueKind == JsonValueKind.Number)
            expiry.TryGetInt64(out expiresIn);
        var access = accessToken.GetString();
        var refresh = refreshToken.GetString();
        if (string.IsNullOrEmpty(access) || access.Length > 4096 ||
            string.IsNullOrEmpty(refresh) || refresh.Length > 4096)
            return null;
        foreach (var character in access)
            if (!char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_' and not '.')
                return null;

        return new()
        {
            AccessToken = access,
            RefreshToken = refresh,
            ExpiresAt = expiresIn is > 30 and <= 86400 ? requestedAt + expiresIn : 0
        };
    }

    static async Task<(string AccessToken, string RefreshToken, long ExpiresAt)?> GetTokensAsync()
    {
        if (await GetAuthorizationAsync() is not { } tuple)
            return null;

        using FormUrlEncodedContent content = new(new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["resource"] = ResourceUri,
            ["grant_type"] = AuthorizationCode,
            ["code"] = tuple.AuthorizationCode,
            ["redirect_uri"] = tuple.RedirectUri,
            ["code_verifier"] = tuple.CodeVerifier
        });

        var requestedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var response = await HttpService.PostAsync(TokenUri, content);
        if (!response.IsSuccessStatusCode) return null;

        return await ParseTokensAsync(response, requestedAt);
    }

    internal static async Task<bool> AuthenticateAsync()
    {
        if (await GetTokensAsync() is { } token)
        {
            ClearAccess();
            RefreshTokenManager._.Set(token.RefreshToken);
            if (token.ExpiresAt <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 30) return false;
            AccessTokenManager._.Publish(token.AccessToken, token.ExpiresAt);
            FlarialClientBeta._.AccessToken = token.AccessToken;
            return true;
        }
        return false;
    }

    internal static async Task RevokeAsync(CancellationToken cancellationToken = default)
    {
        ClearAccess();
        if (RefreshTokenManager._.Get() is { } refreshToken)
        {
            RefreshTokenManager._.Remove();

            using FormUrlEncodedContent content = new(new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["token"] = refreshToken,
                ["token_type_hint"] = RefreshToken,
            });

            using (await HttpService.PostAsync(RevokeUri, content, cancellationToken)) { }
        }
    }

    internal static void ClearAccess()
    {
        FlarialClientBeta._.AccessToken = null;
        AccessTokenManager._.Clear();
    }

    internal static async Task<string?> AuthenticateSilentlyAsync(CancellationToken cancellationToken = default)
    {
        if (RefreshTokenManager._.Get() is not { } refreshToken)
        {
            ClearAccess();
            return null;
        }

        using FormUrlEncodedContent content = new(new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["resource"] = ResourceUri,
            ["grant_type"] = RefreshToken,
            [RefreshToken] = refreshToken
        });

        var requestedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var response = await HttpService.PostAsync(TokenUri, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
                await RevokeAsync(cancellationToken);
            return null;
        }

        if (await ParseTokensAsync(response, requestedAt) is not { } tuple)
            return null;

        ClearAccess();
        RefreshTokenManager._.Set(tuple.RefreshToken);
        if (tuple.ExpiresAt <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 30) return null;
        AccessTokenManager._.Publish(tuple.AccessToken, tuple.ExpiresAt);
        FlarialClientBeta._.AccessToken = tuple.AccessToken;

        return tuple.AccessToken;
    }
}
