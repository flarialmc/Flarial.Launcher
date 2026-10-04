using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Flarial.Runtime.Linux.Xbox;

/// <summary>Ports bol/auth.py: MSA device-code login (client 0000000048183522) and the XBL device/user/XSTS/SISU chain that fills device.json for winegdk.</summary>
internal sealed class XboxAuth : IXboxAuth
{
    const string ClientId = "0000000048183522";
    const string Scope = "service::user.auth.xboxlive.com::MBI_SSL";
    const string ConnectUrl = "https://login.live.com/oauth20_connect.srf";
    const string TokenUrl = "https://login.live.com/oauth20_token.srf";
    const int ReuseMargin = 1800, MinTtl = 60;

    // fields that must be present; Expiry (if any) must also be live
    static readonly (string Token, string? Expiry)[] Required =
    [
        ("device_token", "device_token_expiry"), ("user_token", "user_token_expiry"), ("xbl_token", "xbl_token_expiry"),
        ("sisu_token", "sisu_expiry"), ("sisu_rp", null), ("sisu_uhs", null),
        ("mp_token", "mp_expiry"), ("mp_rp", null), ("mp_uhs", null),
        ("realms_token", "realms_expiry"), ("realms_rp", null), ("realms_uhs", null), ("xbl_xuid", null),
    ];
    static readonly string[] EpochFields = ["user_token", "xbl_token", "achievements", "sisu", "mp", "realms", "lic"];

    static string KeyPath => Path.Combine(Paths.PreauthDir, "device-key.pem");
    static string IdPath => Path.Combine(Paths.PreauthDir, "device-id.txt");
    static string EpochPath => Path.Combine(Paths.PreauthDir, ".account-epoch");

    public bool HasToken => Json.Field(Paths.MsaToken, "refresh_token") is { Length: > 0 };
    public bool IsOnlineReady => IsComplete(Json.ReadObject(Paths.DeviceJson), MinTtl);

    // ---- MSA ----
    static async Task<JsonObject> PostForm(string url, Dictionary<string, string> form, CancellationToken ct)
    {
        using var res = await Download.Http.PostAsync(url, new FormUrlEncodedContent(form), ct);
        return JsonNode.Parse(await res.Content.ReadAsStringAsync(ct))?.AsObject() ?? new();
    }

    static int Num(JsonNode? n, int def) { try { return n is null ? def : (int)n.GetValue<JsonElement>().GetDouble(); } catch { return def; } }

    public async Task<DeviceCode> BeginDeviceCodeAsync(CancellationToken ct)
    {
        var d = await PostForm(ConnectUrl, new() { ["client_id"] = ClientId, ["scope"] = Scope, ["response_type"] = "device_code" }, ct);
        if (d["device_code"]?.GetValue<string>() is not { } dc || d["user_code"]?.GetValue<string>() is not { } uc)
            throw new InvalidOperationException("Microsoft device-code request failed: " + (d["error_description"] ?? d["error"]));
        var url = d["verification_uri"]?.GetValue<string>() is { Length: > 0 } v ? v : "https://www.microsoft.com/link";
        return new(uc, url, dc, TimeSpan.FromSeconds(Num(d["expires_in"], 900)), TimeSpan.FromSeconds(Math.Max(1, Num(d["interval"], 5))));
    }

    public async Task<bool> PollDeviceCodeAsync(DeviceCode code, CancellationToken ct)
    {
        var interval = code.Interval;
        var deadline = DateTime.UtcNow + code.ExpiresIn;
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(interval, ct);
            // legacy live.com grant string, as WineGDK uses; no scope
            var t = await PostForm(TokenUrl, new() { ["client_id"] = ClientId, ["grant_type"] = "device_code", ["device_code"] = code.DeviceCodeValue }, ct);
            var e = t["error"]?.GetValue<string>();
            if (e == "authorization_pending") continue;
            if (e == "slow_down") { interval += TimeSpan.FromSeconds(5); continue; }
            if (e is not null) { Log("device code failed: " + e); return false; }
            if (t["refresh_token"]?.GetValue<string>() is { Length: > 0 } rt) { SaveToken(rt); return true; }
        }
        return false;
    }

    static void SaveToken(string rt) =>
        Json.WriteSecret(Paths.MsaToken, new JsonObject { ["refresh_token"] = rt, ["obtained"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() }.ToJsonString(new() { WriteIndented = true }) + "\n");

    public Task SignOutAsync()
    {
        foreach (var f in new[] { Paths.MsaToken, Paths.DeviceJson }) try { File.Delete(f); } catch { }
        return Task.CompletedTask;
    }

    // ---- refresh ----
    public async Task<XboxSession> RefreshAsync(CancellationToken ct)
    {
        string? rt = Json.Field(Paths.MsaToken, "refresh_token"), access = null;
        if (string.IsNullOrEmpty(rt)) return new(false, null, null, null);
        try
        {
            var t = await PostForm(TokenUrl, new() { ["client_id"] = ClientId, ["scope"] = Scope, ["grant_type"] = "refresh_token", ["refresh_token"] = rt }, ct);
            if (t["refresh_token"]?.GetValue<string>() is { Length: > 0 } fresh) { rt = fresh; SaveToken(fresh); access = t["access_token"]?.GetValue<string>(); }
            else Log("msa refresh rejected: " + t["error"]);
        }
        catch (Exception e) when (e is not OperationCanceledException) { Log("msa refresh transport failure: " + e.GetType().Name); }

        var cached = Json.ReadObject(Paths.DeviceJson);
        if (IsComplete(cached, ReuseMargin)) return Session(cached, rt);
        if (access is not null)
        {
            try { if (await BuildDeviceJson(access, ct) is { } built) return Session(built, rt); }
            catch (Exception e) when (e is not OperationCanceledException) { Log("xbl chain failed: " + e.GetType().Name + " " + e.Message); }
        }
        // never replace a good file with partial data; a still-valid (>=60 s) cache is usable
        return Session(IsComplete(cached, MinTtl) ? cached : null, rt);
    }

    static XboxSession Session(JsonObject? dev, string? rt) =>
        dev is null ? new(false, null, rt, null) : new(true, Paths.DeviceJson, rt, dev["xbl_gamertag"]?.GetValue<string>());

    /// <summary>All required fields present and every expiry more than minTtl seconds away.</summary>
    static bool IsComplete(JsonObject o, int minTtl)
    {
        foreach (var (tok, exp) in Required)
        {
            if (o[tok] is not JsonValue v || !v.TryGetValue<string>(out var s) || s.Length == 0) return false;
            if (exp is not null && (ParseExpiry(o[exp]) is not { } at || at <= DateTimeOffset.UtcNow.AddSeconds(minTtl))) return false;
        }
        return true;
    }

    // Xbox NotAfter has 7 fractional digits; .NET parses up to 7
    static DateTimeOffset? ParseExpiry(JsonNode? n) =>
        n is JsonValue v && v.TryGetValue<string>(out var s) && DateTimeOffset.TryParse(s, null, System.Globalization.DateTimeStyles.AssumeUniversal, out var d) ? d : null;

    // ---- XBL chain ----
    static (ECDsa Key, string Id) DeviceIdentity()
    {
        var key = ECDsa.Create();
        try
        {
            key.ImportFromPem(File.ReadAllText(KeyPath));
            var id = File.ReadAllText(IdPath).Trim();
            if (id.Length > 0) return (key, id);
        }
        catch { }
        key.Dispose();
        key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var newId = "{" + Guid.NewGuid().ToString().ToUpperInvariant() + "}";
        Json.WriteSecret(KeyPath, key.ExportPkcs8PrivateKeyPem() + "\n");
        Json.WriteSecret(IdPath, newId);
        return (key, newId);
    }

    // header = base64(ver(4) | ts(8) | r||s); signed input has a 0 after every field
    static string SignHeader(ECDsa key, string path, byte[] body)
    {
        var ts = (ulong)((DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0 + 11644473600) * 1e7);
        var ver = new byte[] { 0, 0, 0, 1 };
        var tsb = new byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(tsb, ts);
        using var ms = new MemoryStream();
        foreach (var part in new[] { ver, tsb, "POST"u8.ToArray(), Encoding.UTF8.GetBytes(path), [], body })
        { ms.Write(part); ms.WriteByte(0); }
        var sig = key.SignData(ms.ToArray(), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return Convert.ToBase64String([.. ver, .. tsb, .. sig]);
    }

    static async Task<JsonObject?> Post(ECDsa key, string url, JsonObject body, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(body.ToJsonString());
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new ByteArrayContent(bytes) };
        req.Content.Headers.ContentType = new("application/json");
        req.Headers.TryAddWithoutValidation("User-Agent", "XAL Xbox Live Game (Windows; SDK; 1.0.0.0)");
        req.Headers.TryAddWithoutValidation("x-xbl-contract-version", "1");
        req.Headers.TryAddWithoutValidation("Signature", SignHeader(key, new Uri(url).AbsolutePath, bytes));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(15));
        using var res = await Download.Http.SendAsync(req, cts.Token);
        var text = await res.Content.ReadAsStringAsync(cts.Token);
        if (!res.IsSuccessStatusCode)
        {
            // XErr only, never the body: 2148916233-35 no profile/region, 2148916236-38 age/family, clock skew breaks XSTS
            string? err = null;
            try { err = JsonNode.Parse(text)?["XErr"]?.ToString(); } catch { }
            Log($"{new Uri(url).Host} HTTP {(int)res.StatusCode} XErr={err}");
            return null;
        }
        return JsonNode.Parse(text)?.AsObject();
    }

    static async Task<JsonObject?> BuildDeviceJson(string access, CancellationToken ct)
    {
        Directory.CreateDirectory(Paths.PreauthDir);
        var (key, deviceId) = DeviceIdentity();
        using var _ = key;
        var p = key.ExportParameters(true);
        static string B64(byte[] b) => Convert.ToBase64String(b);
        JsonObject Proof() => new() { ["alg"] = "ES256", ["crv"] = "P-256", ["kty"] = "EC", ["use"] = "sig", ["x"] = B64(p.Q.X!), ["y"] = B64(p.Q.Y!) };

        var dev = await Post(key, "https://device.auth.xboxlive.com/device/authenticate", new()
        {
            ["RelyingParty"] = "http://auth.xboxlive.com", ["TokenType"] = "JWT",
            ["Properties"] = new JsonObject { ["AuthMethod"] = "ProofOfPossession", ["Id"] = deviceId, ["DeviceType"] = "Win32", ["Version"] = "10.0.22631", ["ProofKey"] = Proof() },
        }, ct);
        var deviceToken = dev?["Token"]?.GetValue<string>();
        if (deviceToken is null) return null;

        var user = await Post(key, "https://user.auth.xboxlive.com/user/authenticate", new()
        {
            ["RelyingParty"] = "http://auth.xboxlive.com", ["TokenType"] = "JWT",
            ["Properties"] = new JsonObject { ["AuthMethod"] = "RPS", ["SiteName"] = "user.auth.xboxlive.com", ["RpsTicket"] = "t=" + access },
        }, ct);
        var userToken = user?["Token"]?.GetValue<string>();
        if (userToken is null) return null;

        Task<JsonObject?> Xsts(string rp, bool deviceBound)
        {
            var props = new JsonObject { ["SandboxId"] = "RETAIL", ["UserTokens"] = new JsonArray(userToken) };
            if (deviceBound) { props["DeviceToken"] = deviceToken; props["ProofKey"] = Proof(); }
            return Post(key, "https://xsts.auth.xboxlive.com/xsts/authorize", new() { ["RelyingParty"] = rp, ["TokenType"] = "JWT", ["Properties"] = props }, ct);
        }

        // SISU first (only it returns modern gamertag claims), then device-bound XSTS, then plain XSTS (#149: some accounts get 403 from SISU)
        async Task<JsonObject?> Authorize(string rp)
        {
            var sisu = await Post(key, "https://sisu.xboxlive.com/authorize", new()
            {
                ["AccessToken"] = "t=" + access, ["AppId"] = ClientId, ["deviceToken"] = deviceToken, ["Sandbox"] = "RETAIL",
                ["UseModernGamertag"] = true, ["SiteName"] = "user.auth.xboxlive.com", ["RelyingParty"] = rp,
                ["OfferTermsAcceptance"] = true, ["AcceptOffers"] = true, ["ProofKey"] = Proof(),
            }, ct);
            if (sisu?["AuthorizationToken"] is JsonObject a && a["Token"] is not null) return a;
            foreach (var bound in new[] { true, false })
                if (await Xsts(rp, bound) is { } x && x["Token"] is not null) return x;
            return null;
        }

        static string? Uhs(JsonObject? o) => o?["DisplayClaims"]?["xui"]?[0]?["uhs"]?.GetValue<string>();
        static string? Exp(JsonObject? o) => o?["NotAfter"]?.GetValue<string>();
        static string? Tok(JsonObject? o) => o?["Token"]?.GetValue<string>();

        var ach = await Xsts("http://xboxlive.com", false);
        var xbl = await Authorize("http://xboxlive.com");
        var claims = xbl?["DisplayClaims"]?["xui"]?[0]?.AsObject();
        const string pfRp = "https://b980a380.minecraft.playfabapi.com/", mpRp = "https://multiplayer.minecraft.net/", realmsRp = "https://pocket.realms.minecraft.net/", licRp = "http://licensing.xboxlive.com";
        var pf = await Authorize(pfRp);
        var mp = await Authorize(mpRp);
        var realms = await Authorize(realmsRp);
        var lic = await Authorize(licRp);

        string? Claim(string n) => claims?[n]?.GetValue<string>();
        // BCRYPT_ECCPRIVATE_BLOB: magic 'ECS2' LE | 32 LE | X | Y | d (big-endian)
        var blob = new List<byte> { 0x45, 0x43, 0x53, 0x32, 32, 0, 0, 0 };
        blob.AddRange(p.Q.X!); blob.AddRange(p.Q.Y!); blob.AddRange(p.D!);

        var o = new JsonObject
        {
            ["_account_epoch"] = File.Exists(EpochPath) ? File.ReadAllText(EpochPath).Trim() : "legacy",
            ["device_id"] = deviceId,
            ["ecc_private_blob_b64"] = B64([.. blob]),
            ["device_token"] = deviceToken,
            ["device_token_expiry"] = Exp(dev),
            ["user_token"] = userToken,
            ["user_token_expiry"] = Exp(user),
            ["xbl_token"] = Tok(xbl),
            ["xbl_token_expiry"] = Exp(xbl),
            ["xbl_xuid"] = Claim("xid"),
            ["xbl_gamertag"] = Claim("gtg"),
            ["xbl_age_group"] = Claim("agg"),
            ["xbl_uhs"] = Claim("uhs"),
            ["sisu_rp"] = Tok(pf) is null ? null : pfRp, ["sisu_token"] = Tok(pf), ["sisu_uhs"] = Uhs(pf), ["sisu_expiry"] = Exp(pf),
            ["mp_rp"] = Tok(mp) is null ? null : mpRp, ["mp_token"] = Tok(mp), ["mp_uhs"] = Uhs(mp), ["mp_expiry"] = Exp(mp),
            ["realms_rp"] = Tok(realms) is null ? null : realmsRp, ["realms_token"] = Tok(realms), ["realms_uhs"] = Uhs(realms), ["realms_expiry"] = Exp(realms),
            ["lic_rp"] = Tok(lic) is null ? null : licRp, ["lic_token"] = Tok(lic), ["lic_uhs"] = Uhs(lic), ["lic_expiry"] = Exp(lic),
            ["obtained"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };
        // optional achievements block: all three or none, expiry must be live
        if (Tok(ach) is { } at && Uhs(ach) is { } au && ParseExpiry(ach!["NotAfter"]) is { } ae && ae > DateTimeOffset.UtcNow.AddSeconds(MinTtl))
        { o["achievements_token"] = at; o["achievements_uhs"] = au; o["achievements_expiry"] = Exp(ach); }
        foreach (var (c, f) in new[] { ("mgt", "xbl_modern_gamertag"), ("mgs", "xbl_modern_gamertag_suffix"), ("umg", "xbl_unique_modern_gamertag") })
            if (Claim(c) is { } val) o[f] = val;
        if (claims?["prv"] is { } prv) o["xbl_privileges"] = Privileges(prv);

        // winegdk cannot parse ISO; give it decimal epoch seconds
        foreach (var f in EpochFields)
            if (ParseExpiry(o[f + "_expiry"]) is { } d) o[f + "_expiry_epoch"] = d.ToUnixTimeSeconds().ToString();

        if (!IsComplete(o, MinTtl)) { Log("xbl chain incomplete; keeping existing device.json"); return null; }
        Json.WriteSecret(Paths.DeviceJson, o.ToJsonString(new() { WriteIndented = true }));
        return o;
    }

    // space-separated sorted unique uint list (accepts string or array claim)
    static string Privileges(JsonNode raw)
    {
        var parts = raw is JsonArray arr ? arr.Select(x => x?.ToString() ?? "") : raw.ToString().Split([' ', ',', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts.Select(s => uint.TryParse(s, out var u) ? (uint?)u : null).Where(u => u.HasValue).Select(u => u!.Value).Distinct().Order());
    }

    static void Log(string msg)
    {
        try { Directory.CreateDirectory(Paths.Logs); File.AppendAllText(Path.Combine(Paths.Logs, "xbox.log"), $"{DateTime.Now:s} {msg}\n"); } catch { }
    }
}
